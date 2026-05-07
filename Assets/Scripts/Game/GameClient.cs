using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Google.Protobuf;
using MmorpgClient.Net;
using MmorpgClient.World;
using UnityEngine;

// Top-level proto types from .proto files that declare no `package` are
// emitted into the global namespace by protoc. Loginpb.* lives in the
// `Loginpb` namespace because login.proto declares `package loginpb;`.
using Loginpb;

namespace MmorpgClient.Game
{
    /// <summary>
    /// High-level facade that drives the login -> enter scene -> cast skill flow.
    /// Owns the HTTP gateway client and the persistent gate TCP connection.
    /// All callbacks fire on the Unity main thread (driven by <see cref="Tick"/>).
    /// </summary>
    public sealed class GameClient
    {
        private readonly GatewayHttpClient _http;
        private readonly MuduoCodec _codec;
        private GateTcpClient _gate;

        private long _seq;
        private readonly Dictionary<ulong, Action<MessageContent>> _pending = new();
        private readonly Dictionary<uint, Action<MessageContent>> _notifyHandlers = new();

        private long _accessTokenExpire;       // unix seconds
        private float _lastRefreshAttempt;     // realtimeSinceStartup

        public ulong PlayerId { get; private set; }
        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }
        public ActorWorld World { get; }
        public bool TokenVerified { get; private set; }
        public bool InGame { get; private set; }
        public ulong CurrentSceneId { get; private set; }

        public event Action<string> OnLog;
        public event Action OnDisconnected;

        public GameClient(string gatewayBaseUrl)
        {
            _http = new GatewayHttpClient(gatewayBaseUrl);
            _codec = new MuduoCodec();
            // top-level (no package) protos emitted by protoc
            _codec.Register<ClientRequest>();
            _codec.Register<MessageContent>();
            _codec.Register<ClientTokenVerifyRequest>();
            _codec.Register<ClientTokenVerifyResponse>();

            World = new ActorWorld();
            WireSceneNotifyHandlers();
        }

        public void Tick()
        {
            _gate?.Poll();
            MaybeRefreshToken();
        }

        public void OnNotify(uint messageId, Action<MessageContent> handler)
            => _notifyHandlers[messageId] = handler;

        // ── Phase 1: HTTP gateway -> assign gate ──────────────────

        /// <summary>
        /// Run the full pre-game pipeline:
        ///   AssignGate -> TCP connect -> token verify -> Login -> (CreatePlayer) -> EnterGame.
        /// </summary>
        public IEnumerator LoginAndEnterGame(uint zoneId, string account, string password,
                                             Action onSuccess, Action<string> onError)
        {
            AssignGateResult assigned = null;
            string err = null;
            yield return _http.AssignGate(zoneId, r => assigned = r, e => err = e);
            if (err != null) { onError(err); yield break; }
            Log($"assigned gate {assigned.gate_ip}:{assigned.gate_port}");

            // The Java gateway returns token bytes as base64 strings inside JSON.
            byte[] payload = SafeBase64(assigned.token_payload);
            byte[] signature = SafeBase64(assigned.token_signature);

            try { ConnectGate(assigned.gate_ip, (int)assigned.gate_port); }
            catch (Exception ex) { onError($"connect gate: {ex.Message}"); yield break; }

            // ── Phase 2: token verify (first protobuf message) ──
            // Install a single dispatcher that handles both the early
            // ClientTokenVerifyResponse and (after verify) the steady
            // stream of MessageContent frames.
            _gate.OnMessage += DispatchInbound;
            _gate.OnDisconnected += () => { OnDisconnected?.Invoke(); InGame = false; };

            _gate.Send(new ClientTokenVerifyRequest
            {
                Payload = ByteString.CopyFrom(payload),
                Signature = ByteString.CopyFrom(signature),
            });
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!TokenVerified && Time.realtimeSinceStartup < deadline) { Tick(); yield return null; }
            if (!TokenVerified) { onError("token verify timeout"); yield break; }
            Log("gate token verified");

            // ── Phase 3: Login ─────────────────────────────────
            LoginResponse loginResp = null;
            yield return Call(MessageIds.Login,
                new LoginRequest { Account = account, Password = password, AuthType = "password" },
                LoginResponse.Parser, r => loginResp = r,
                e => { onError($"login: {e}"); });
            if (loginResp == null) yield break;
            if (loginResp.ErrorMessage != null && loginResp.ErrorMessage.Id != 0)
            { onError($"login error: tip={loginResp.ErrorMessage.Id}"); yield break; }
            AccessToken  = loginResp.AccessToken;
            RefreshToken = loginResp.RefreshToken;
            _accessTokenExpire = loginResp.AccessTokenExpire;
            Log($"login ok, players={loginResp.Players.Count}");

            // ── Phase 4: Create player if needed ───────────────
            if (loginResp.Players.Count == 0)
            {
                CreatePlayerResponse cpResp = null;
                yield return Call(MessageIds.CreatePlayer, new CreatePlayerRequest(),
                    CreatePlayerResponse.Parser, r => cpResp = r,
                    e => { onError($"create player: {e}"); });
                if (cpResp == null) yield break;
                if (cpResp.ErrorMessage != null && cpResp.ErrorMessage.Id != 0)
                { onError($"create player error: tip={cpResp.ErrorMessage.Id}"); yield break; }
                if (cpResp.Players.Count == 0) { onError("create player returned empty list"); yield break; }
                loginResp.Players.AddRange(cpResp.Players);
                Log("created new player");
            }

            // ── Phase 5: EnterGame ─────────────────────────────
            ulong playerId = loginResp.Players[0].Player.PlayerId;
            EnterGameResponse egResp = null;
            yield return Call(MessageIds.EnterGame,
                new EnterGameRequest { PlayerId = playerId, RequestId = Guid.NewGuid().ToString("N") },
                EnterGameResponse.Parser, r => egResp = r,
                e => { onError($"enter game: {e}"); });
            if (egResp == null) yield break;
            if (egResp.ErrorMessage != null && egResp.ErrorMessage.Id != 0)
            { onError($"enter game error: tip={egResp.ErrorMessage.Id}"); yield break; }
            PlayerId = egResp.PlayerId;
            InGame = true;
            Log($"enter game ok, player_id={PlayerId}");
            onSuccess();
        }

        /// <summary>
        /// Explicit C2S EnterScene. Server normally pushes NotifyEnterScene
        /// automatically after EnterGame, so use this only when switching
        /// scene at runtime (instance / dungeon / mirror).
        /// </summary>
        public IEnumerator EnterScene(uint sceneConfigId, ulong sceneId,
                                      Action onSuccess, Action<string> onError)
        {
            var req = new EnterSceneC2SRequest
            {
                SceneInfo = new SceneInfoComp
                {
                    SceneConfigId = sceneConfigId,
                    SceneId = sceneId,
                },
            };
            EnterSceneC2SResponse resp = null;
            yield return Call(MessageIds.EnterScene, req, EnterSceneC2SResponse.Parser,
                r => resp = r, e => onError($"enter scene: {e}"));
            if (resp == null) yield break;
            if (resp.ErrorMessage != null && resp.ErrorMessage.Id != 0)
            { onError($"enter scene tip={resp.ErrorMessage.Id}"); yield break; }
            onSuccess();
        }

        /// <summary>
        /// Send a fire-and-forget skill release. Server-pushed
        /// NotifySkillUsed/NotifySkillInterrupted messages drive the
        /// world-side FX through the dispatcher set up in
        /// <see cref="WireSceneNotifyHandlers"/>.
        /// </summary>
        public void ReleaseSkill(uint skillTableId, ulong targetEntity, UnityEngine.Vector3? position = null)
        {
            var req = new ReleaseSkillRequest
            {
                SkillTableId = skillTableId,
                TargetId = targetEntity,
            };
            if (position.HasValue)
            {
                req.Position = new global::Vector3 { X = position.Value.x, Y = position.Value.y, Z = position.Value.z };
            }
            SendOneWay(MessageIds.ReleaseSkill, req);
        }

        // ── internals ────────────────────────────────────────────

        private void ConnectGate(string host, int port)
        {
            _gate = new GateTcpClient(_codec);
            _gate.OnError += e => Log($"[gate] error: {e}");
            _gate.Connect(host, port);
        }

        private static byte[] SafeBase64(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<byte>();
            // Java/Jackson emits standard padded base64; .NET wants no
            // surrounding whitespace.
            try { return Convert.FromBase64String(s.Trim()); }
            catch { return Array.Empty<byte>(); }
        }

        private IEnumerator Call<TResp>(uint messageId, IMessage request,
                                        MessageParser<TResp> parser,
                                        Action<TResp> onResp,
                                        Action<string> onError)
            where TResp : IMessage<TResp>
        {
            ulong id = (ulong)Interlocked.Increment(ref _seq);
            var clientReq = new ClientRequest
            {
                Id = id,
                MessageId = messageId,
                Body = request.ToByteString(),
            };

            bool done = false;
            _pending[id] = mc =>
            {
                done = true;
                if (mc.ErrorMessage != null && mc.ErrorMessage.Id != 0)
                {
                    onError($"server tip={mc.ErrorMessage.Id}");
                    return;
                }
                try { onResp(parser.ParseFrom(mc.SerializedMessage)); }
                catch (Exception ex) { onError($"parse response: {ex.Message}"); }
            };

            _gate.Send(clientReq);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!done && Time.realtimeSinceStartup < deadline) { Tick(); yield return null; }
            if (!done) { _pending.Remove(id); onError("rpc timeout"); }
        }

        private void SendOneWay(uint messageId, IMessage request)
        {
            ulong id = (ulong)Interlocked.Increment(ref _seq);
            _gate.Send(new ClientRequest
            {
                Id = id,
                MessageId = messageId,
                Body = request.ToByteString(),
            });
        }

        private void DispatchInbound(IMessage msg)
        {
            // Token verify response is its own top-level type, not a MessageContent.
            if (msg is ClientTokenVerifyResponse tvr)
            {
                if (tvr.Success) TokenVerified = true;
                else Log($"[gate] token rejected: {tvr.Error}");
                return;
            }
            if (msg is GateTcpClient.DisconnectedSentinel) return;
            if (msg is not MessageContent mc) return;

            if (mc.Id != 0 && _pending.Remove(mc.Id, out var cb))
            {
                cb(mc);
                return;
            }
            if (_notifyHandlers.TryGetValue(mc.MessageId, out var nh)) nh(mc);
            else Log($"[unhandled] message_id={mc.MessageId} bytes={mc.SerializedMessage.Length}");
        }

        private void WireSceneNotifyHandlers()
        {
            OnNotify(MessageIds.NotifyEnterScene, mc =>
            {
                var ev = EnterSceneS2C.Parser.ParseFrom(mc.SerializedMessage);
                CurrentSceneId = ev.SceneInfo?.SceneId ?? 0;
                Log($"[scene] entered scene_id={CurrentSceneId}, config={ev.SceneInfo?.SceneConfigId}");
                World.Clear();
            });

            OnNotify(MessageIds.NotifyActorCreate, mc =>
            {
                var ev = ActorCreateS2C.Parser.ParseFrom(mc.SerializedMessage);
                SpawnActorView(ev);
            });

            OnNotify(MessageIds.NotifyActorListCreate, mc =>
            {
                var ev = ActorListCreateS2C.Parser.ParseFrom(mc.SerializedMessage);
                foreach (var a in ev.ActorList) SpawnActorView(a);
                Log($"[scene] +{ev.ActorList.Count} actors");
            });

            OnNotify(MessageIds.NotifyActorDestroy, mc =>
            {
                var ev = ActorDestroyS2C.Parser.ParseFrom(mc.SerializedMessage);
                World.DespawnActor(ev.Entity);
            });

            OnNotify(MessageIds.NotifyActorListDestroy, mc =>
            {
                var ev = ActorListDestroyS2C.Parser.ParseFrom(mc.SerializedMessage);
                foreach (var e in ev.Entity) World.DespawnActor(e);
            });

            OnNotify(MessageIds.NotifySkillUsed, mc =>
            {
                var ev = SkillUsedS2C.Parser.ParseFrom(mc.SerializedMessage);
                Log($"[skill] caster_entity={ev.Entity} skill={ev.SkillTableId} targets={ev.TargetEntity.Count}");
                if (World.TryGetActor(ev.Entity, out var caster))
                {
                    SkillFx.PlayCast(caster.Go);
                    foreach (var tid in ev.TargetEntity)
                    {
                        if (World.TryGetActor(tid, out var tv))
                        {
                            SkillFx.PlayBeam(caster.Go.transform.position + UnityEngine.Vector3.up,
                                             tv.Go.transform.position + UnityEngine.Vector3.up,
                                             new Color(1f, 0.9f, 0.3f));
                            SkillFx.PlayHit(tv.Go, new Color(1f, 0.2f, 0.2f));
                        }
                    }
                }
            });

            OnNotify(MessageIds.NotifySkillInterrupted, mc =>
            {
                var ev = SkillInterruptedS2C.Parser.ParseFrom(mc.SerializedMessage);
                Log($"[skill] interrupted entity={ev.Entity} skill={ev.SkillTableId} reason={ev.ReasonCode}");
            });

            OnNotify(MessageIds.TipToClient, mc =>
            {
                var tip = TipInfoMessage.Parser.ParseFrom(mc.SerializedMessage);
                Log($"[tip] id={tip.Id}");
            });

            OnNotify(MessageIds.KickPlayer, _ =>
            {
                Log("[gate] kicked by server");
                Disconnect();
            });
        }

        private void SpawnActorView(ActorCreateS2C ev)
        {
            var loc = ev.Transform?.Location;
            var rot = ev.Transform?.Rotation;
            // UE-style world (X forward, Y right, Z up) -> Unity (X right, Y up, Z forward).
            // Pick a stable mapping: UE.X -> Unity.Z, UE.Y -> Unity.X, UE.Z -> Unity.Y.
            var pos = loc != null
                ? new UnityEngine.Vector3((float)loc.Y, (float)loc.Z, (float)loc.X)
                : UnityEngine.Vector3.zero;
            var euler = rot != null
                ? new UnityEngine.Vector3((float)rot.Y, (float)rot.Z, (float)rot.X)
                : UnityEngine.Vector3.zero;

            var kind = ev.ActorType switch
            {
                ActorType.Player => ActorKind.Player,
                ActorType.Npc => ActorKind.Npc,
                _ => ActorKind.Unknown,
            };
            World.SpawnActor(ev.Entity, kind, ev.ConfigId, pos, euler);

            // Local player binding: server uses `guid == player_id` for the
            // owning client's actor.
            if (kind == ActorKind.Player && ev.Guid == PlayerId && PlayerId != 0)
            {
                World.SetLocalPlayer(ev.Entity);
            }
        }

        private void MaybeRefreshToken()
        {
            if (MessageIds.RefreshToken == 0) return; // disabled until server publishes ID
            if (string.IsNullOrEmpty(RefreshToken) || _accessTokenExpire == 0) return;
            long nowSec = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (_accessTokenExpire - nowSec > 10 * 60) return;          // >10min left
            if (Time.realtimeSinceStartup - _lastRefreshAttempt < 60f) return; // 60s cooldown
            _lastRefreshAttempt = Time.realtimeSinceStartup;
            Log("refreshing access_token");
            // RefreshToken request mirrors loginpb.RefreshTokenRequest; we
            // send fire-and-forget here -- the server responds with a new
            // pair which we capture via dispatch.
            SendOneWay(MessageIds.RefreshToken, new RefreshTokenRequest { RefreshToken = RefreshToken });
        }

        private void Log(string s) => OnLog?.Invoke(s);

        public void Disconnect()
        {
            InGame = false;
            _gate?.Dispose();
        }
    }
}
