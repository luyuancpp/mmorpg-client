using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Google.Protobuf;
using MmorpgClient.Net;
using UnityEngine;

// Top-level proto types from .proto files that declare no `package` are
// emitted into the global namespace by protoc. Loginpb.* lives in the
// `Loginpb` namespace because login.proto declares `package loginpb;`.
using Loginpb;

namespace MmorpgClient.Game
{
    /// <summary>
    /// High-level facade that drives the login → enter scene → cast skill flow.
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

        public ulong PlayerId { get; private set; }
        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }

        public event Action<string> OnLog;

        public GameClient(string gatewayBaseUrl)
        {
            _http = new GatewayHttpClient(gatewayBaseUrl);
            _codec = new MuduoCodec();
            // top-level (no package) protos emitted by protoc
            _codec.Register<ClientRequest>();
            _codec.Register<MessageContent>();
            _codec.Register<ClientTokenVerifyRequest>();
            _codec.Register<ClientTokenVerifyResponse>();
        }

        public void Tick() => _gate?.Poll();

        public void OnNotify(uint messageId, Action<MessageContent> handler)
            => _notifyHandlers[messageId] = handler;

        // ── Phase 1: HTTP gateway → assign gate ──────────────────

        /// <summary>
        /// Run the full pre-game pipeline:
        ///   AssignGate → TCP connect → token verify → Login → (CreatePlayer) → EnterGame.
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
            byte[] payload   = Convert.FromBase64String(assigned.token_payload   ?? "");
            byte[] signature = Convert.FromBase64String(assigned.token_signature ?? "");

            try { ConnectGate(assigned.gate_ip, (int)assigned.gate_port); }
            catch (Exception ex) { onError($"connect gate: {ex.Message}"); yield break; }

            // ── Phase 2: token verify (first protobuf message) ──
            bool tokenDone = false; string tokenErr = null;
            _gate.OnMessage += msg =>
            {
                if (msg is ClientTokenVerifyResponse resp)
                {
                    if (resp.Success) tokenDone = true;
                    else tokenErr = string.IsNullOrEmpty(resp.Error) ? "token rejected" : resp.Error;
                }
            };
            _gate.Send(new ClientTokenVerifyRequest
            {
                Payload = ByteString.CopyFrom(payload),
                Signature = ByteString.CopyFrom(signature),
            });
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!tokenDone && tokenErr == null && Time.realtimeSinceStartup < deadline) { Tick(); yield return null; }
            if (tokenErr != null) { onError($"token verify: {tokenErr}"); yield break; }
            if (!tokenDone) { onError("token verify timeout"); yield break; }
            Log("gate token verified");

            // After this point all replies arrive as MessageContent.
            _gate.OnMessage += DispatchInbound;

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
            Log($"enter game ok, player_id={PlayerId}");
            onSuccess();
        }

        /// <summary>
        /// Send a fire-and-forget skill release. Server-pushed
        /// <c>NotifySkillUsed</c>/<c>NotifySkillInterrupted</c> messages are
        /// delivered through the notify handler registered via
        /// <see cref="OnNotify"/>.
        /// </summary>
        public void ReleaseSkill(uint skillTableId, ulong targetEntity)
        {
            var req = new ReleaseSkillRequest
            {
                SkillTableId = skillTableId,
                TargetId = targetEntity,
            };
            SendOneWay(MessageIds.ReleaseSkill, req);
        }

        // ── internals ────────────────────────────────────────────

        private void ConnectGate(string host, int port)
        {
            _gate = new GateTcpClient(_codec);
            _gate.OnError += e => Log($"[gate] error: {e}");
            _gate.OnDisconnected += () => Log("[gate] disconnected");
            _gate.Connect(host, port);
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
            if (msg is GateTcpClient.DisconnectedSentinel) return;
            if (msg is not MessageContent mc) return;

            if (_pending.Remove(mc.Id, out var cb))
            {
                cb(mc);
                return;
            }
            if (_notifyHandlers.TryGetValue(mc.MessageId, out var nh)) nh(mc);
            else Log($"[unhandled] message_id={mc.MessageId} bytes={mc.SerializedMessage.Length}");
        }

        private void Log(string s) => OnLog?.Invoke(s);

        public void Disconnect() => _gate?.Dispose();
    }
}
