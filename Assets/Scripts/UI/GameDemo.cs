using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MmorpgClient.Game;
using MmorpgClient.Net;
using MmorpgClient.World;
using UnityEngine;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Minimal IMGUI driver for the full client demo:
    ///   1. Login -> token verify -> EnterGame
    ///   2. EnterScene (debug C2S)
    ///   3. Cast skill on first non-local actor
    ///
    /// Auto-creates a camera + directional light if the scene has none, so
    /// you can drop this on a fresh empty GameObject and press Play.
    ///
    /// When no scene file contains a GameDemo GameObject this method
    /// auto-spawns one so pressing Play on any empty scene works out of
    /// the box.
    /// </summary>
    public sealed class GameDemo : MonoBehaviour
    {
        private enum LoginStage
        {
            Landing,
            ServerSelect,
            RoleConfirm,
            InWorld,
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<GameDemo>() != null) return;
            new GameObject("[GameDemo]").AddComponent<GameDemo>();
        }

        [Header("Server")]
        public string GatewayBaseUrl = "http://127.0.0.1:8080";
        public uint   ZoneId         = 1;

        [Header("Account")]
        public string Account  = "demo";
        public string Password = "demo";

        [Header("Scene (debug EnterScene C2S)")]
        public uint  SceneConfigId = 1;
        public ulong SceneId       = 0;

        [Header("Skill")]
        public uint  SkillTableId = 1001;
        public ulong TargetEntity = 0;

        [Header("Reconnect")]
        public bool  AutoReconnect       = true;
        public float ReconnectMinDelay   = 2f;
        public float ReconnectMaxDelay   = 30f;
        public int   ReconnectMaxAttempts = 10;

        [Header("Movement")]
        public bool  EnableWasdMove   = true;
        public float MoveSpeed        = 4.5f;     // units/sec (Unity world)
        public float TurnSpeed        = 180f;     // deg/sec
        public float MoveSyncInterval = 0.25f;    // 4Hz drift heartbeat

        private GameClient _client;
        private GatewayHttpClient _gateway;
        private readonly List<string> _log = new();
        private string _status = "idle";
        private Vector2 _scroll;
        private Camera _camera;

        // login UI state
        private readonly List<ServerListZone> _zones = new();
        private Vector2 _zoneScroll;
        private int _selectedZoneIndex;
        private bool _loadingZones;
        private string _zoneLoadError;
        private bool _isLoggingIn;
        private bool _didTryAutoLoadZones;
        private string _accountDraft;
        private string _passwordDraft;
        private LoginStage _stage = LoginStage.Landing;
        private readonly List<AnnouncementItem> _announcements = new();
        private Vector2 _announceScroll;
        private string _announceError;
        private bool _loadingAnnouncements;

        // lightweight 3D preview avatar for login pages
        private GameObject _previewRoot;
        private GameObject _previewAvatar;

        // visual style resources
        private GUIStyle _rootLabel;
        private GUIStyle _panel;
        private GUIStyle _title;
        private GUIStyle _caption;
        private GUIStyle _buttonPrimary;
        private GUIStyle _buttonGhost;
        private GUIStyle _zoneButton;
        private GUIStyle _zoneButtonActive;
        private GUIStyle _logStyle;
        private Texture2D _bgGradient;
        private Texture2D _mistTex;
        private Texture2D _panelTex;
        private Texture2D _btnPrimaryTex;
        private Texture2D _btnGhostTex;
        private Texture2D _zoneTex;
        private Texture2D _zoneActiveTex;
        private Texture2D _badgeTex;

        // movement client state
        private bool    _moveActive;
        private float   _lastMoveSyncAt;
        private UnityEngine.Vector3 _lastSentDir;

        // reconnect state
        private bool  _wasInGame;
        private int   _reconnectAttempt;
        private float _nextReconnectAt;
        private bool  _reconnecting;

        private void Awake()
        {
            EnsureSceneRig();
            EnsureGuiStyles();
            EnsurePreviewActor();

            _accountDraft = Account;
            _passwordDraft = Password;

            _client = new GameClient(GatewayBaseUrl);
            _gateway = new GatewayHttpClient(GatewayBaseUrl);
            _client.OnLog += Append;
            _client.OnDisconnected += () =>
            {
                _status = "disconnected";
                Append("[gate] disconnected");
                if (AutoReconnect && _wasInGame)
                {
                    _reconnectAttempt = 0;
                    _nextReconnectAt = Time.realtimeSinceStartup + ReconnectMinDelay;
                }
            };
        }

        private void Update()
        {
            if (!_didTryAutoLoadZones)
            {
                _didTryAutoLoadZones = true;
                StartCoroutine(LoadGatewayFrontPage());
            }

            if (_previewRoot != null && _stage != LoginStage.InWorld)
            {
                _previewRoot.transform.Rotate(0f, 25f * Time.deltaTime, 0f, Space.World);
            }
            if (_previewRoot != null)
            {
                _previewRoot.SetActive(_stage != LoginStage.InWorld);
            }

            _client.Tick();
            if (_client.InGame) _wasInGame = true;
            MaybeReconnect();

            // World interpolation (drives non-local actors based on server moves).
            _client.World?.Tick();

            if (_camera != null && _client.World != null && _client.World.LocalEntity != 0
                && _client.World.TryGetActor(_client.World.LocalEntity, out var me))
            {
                if (EnableWasdMove && _client.InGame) DriveLocalMovement(me.Go.transform);

                // Simple chase: 6m back, 5m up, look at player.
                var t = me.Go.transform;
                var desired = t.position + new UnityEngine.Vector3(0, 5f, -6f);
                _camera.transform.position = UnityEngine.Vector3.Lerp(_camera.transform.position, desired, 0.1f);
                _camera.transform.LookAt(t.position + UnityEngine.Vector3.up);
                if (_stage != LoginStage.InWorld) _stage = LoginStage.InWorld;
            }
        }

        private IEnumerator LoadGatewayFrontPage()
        {
            yield return RefreshServerList();
            yield return RefreshAnnouncements();
        }

        /// <summary>
        /// WASD planar movement on the local-player cube. Sends:
        /// - MoveStart on input start / direction change,
        /// - MoveSync at <see cref="MoveSyncInterval"/> while moving (drift heartbeat),
        /// - MoveStop on key release.
        /// The server is authoritative; <c>NotifyActorMove</c> for the local
        /// entity will reconcile via <see cref="ActorWorld.ApplyMove"/>.
        /// </summary>
        private void DriveLocalMovement(UnityEngine.Transform t)
        {
            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S
            var dir = new UnityEngine.Vector3(h, 0f, v);
            bool moving = dir.sqrMagnitude > 0.01f;
            if (moving) dir = dir.normalized;

            // Local prediction (we own this primitive's transform until the
            // server reconciles via NotifyMoveAck / NotifyActorMove).
            if (moving)
            {
                t.position += dir * MoveSpeed * Time.deltaTime;
                var targetYaw = Quaternion.LookRotation(dir, UnityEngine.Vector3.up);
                t.rotation = Quaternion.RotateTowards(t.rotation, targetYaw, TurnSpeed * Time.deltaTime);
            }

            float now = Time.realtimeSinceStartup;
            bool dirChanged = moving && UnityEngine.Vector3.Angle(dir, _lastSentDir) > 15f;

            if (moving && (!_moveActive || dirChanged))
            {
                _client.SendMoveStart(t.position, t.eulerAngles, dir * MoveSpeed);
                _moveActive     = true;
                _lastSentDir    = dir;
                _lastMoveSyncAt = now;
            }
            else if (moving && now - _lastMoveSyncAt >= MoveSyncInterval)
            {
                _client.SendMoveSync(t.position, t.eulerAngles, dir * MoveSpeed);
                _lastMoveSyncAt = now;
            }
            else if (!moving && _moveActive)
            {
                _client.SendMoveStop(t.position, t.eulerAngles);
                _moveActive = false;
                _lastSentDir = UnityEngine.Vector3.zero;
            }
        }

        private void MaybeReconnect()
        {
            if (!AutoReconnect || _reconnecting || _client.InGame) return;
            if (_nextReconnectAt == 0f || Time.realtimeSinceStartup < _nextReconnectAt) return;
            if (_reconnectAttempt >= ReconnectMaxAttempts)
            {
                Append($"[reconnect] gave up after {_reconnectAttempt} attempts");
                _nextReconnectAt = 0f;
                return;
            }
            _reconnectAttempt++;
            _reconnecting = true;
            _status = $"reconnecting #{_reconnectAttempt}";
            Append($"[reconnect] attempt {_reconnectAttempt}");
            // Rebuild GameClient: a stale GateTcpClient cannot be reused after
            // its reader/writer threads have exited.
            _client.Disconnect();
            _client = new GameClient(GatewayBaseUrl);
            _client.OnLog += Append;
            _client.OnDisconnected += () =>
            {
                _status = "disconnected";
                Append("[gate] disconnected");
                if (AutoReconnect && _wasInGame)
                {
                    _nextReconnectAt = Time.realtimeSinceStartup + NextBackoff();
                }
            };
            StartCoroutine(_client.LoginAndEnterGame(ZoneId, Account, Password,
                () => { _status = "in game"; _reconnectAttempt = 0; _nextReconnectAt = 0f; _reconnecting = false; },
                e  => { _status = "reconnect FAILED"; Append("[reconnect] err " + e);
                        _reconnecting = false;
                        _nextReconnectAt = Time.realtimeSinceStartup + NextBackoff(); }));
        }

        private float NextBackoff()
        {
            float d = ReconnectMinDelay * Mathf.Pow(2f, Mathf.Min(_reconnectAttempt, 6));
            return Mathf.Min(d, ReconnectMaxDelay);
        }

        private IEnumerator RefreshServerList()
        {
            _loadingZones = true;
            _zoneLoadError = null;
            ServerListResponse result = null;
            string error = null;
            _gateway = new GatewayHttpClient(GatewayBaseUrl);
            yield return _gateway.GetServerList(r => result = r, e => error = e);

            _loadingZones = false;
            _zones.Clear();
            if (!string.IsNullOrEmpty(error))
            {
                _zoneLoadError = error;
                _status = "server-list load failed";
                Append("[gateway] " + error);
                yield break;
            }

            if (result?.zones == null || result.zones.Length == 0)
            {
                _zoneLoadError = "server-list is empty";
                _status = "server-list empty";
                Append("[gateway] server-list empty");
                yield break;
            }

            _zones.AddRange(result.zones);

            int recommended = _zones.FindIndex(z => z.recommended);
            _selectedZoneIndex = recommended >= 0 ? recommended : 0;
            ZoneId = _zones[_selectedZoneIndex].zone_id;
            _status = $"zones loaded: {_zones.Count}";
            if (_stage == LoginStage.Landing) _stage = LoginStage.ServerSelect;
        }

        private IEnumerator RefreshAnnouncements()
        {
            _loadingAnnouncements = true;
            _announceError = null;
            AnnouncementResponse result = null;
            string error = null;
            _gateway = new GatewayHttpClient(GatewayBaseUrl);
            yield return _gateway.GetAnnouncements(r => result = r, e => error = e);

            _loadingAnnouncements = false;
            _announcements.Clear();
            if (!string.IsNullOrEmpty(error))
            {
                _announceError = error;
                Append("[gateway] " + error);
                yield break;
            }

            if (result?.items == null || result.items.Length == 0)
            {
                yield break;
            }

            _announcements.AddRange(result.items);
        }

        private IEnumerator LoginAndEnterJourney()
        {
            _isLoggingIn = true;
            _status = "logging in...";
            Account = _accountDraft;
            Password = _passwordDraft;

            if (_zones.Count > 0 && _selectedZoneIndex >= 0 && _selectedZoneIndex < _zones.Count)
            {
                ZoneId = _zones[_selectedZoneIndex].zone_id;
            }

            bool loginOk = false;
            string loginErr = null;

            yield return _client.LoginAndEnterGame(ZoneId, Account, Password,
                () => { loginOk = true; _status = "in game"; },
                e => { loginErr = e; _status = "LOGIN FAILED"; Append("ERR " + e); });

            if (!loginOk)
            {
                _isLoggingIn = false;
                if (!string.IsNullOrEmpty(loginErr)) Append("[login] " + loginErr);
                yield break;
            }

            bool enterSceneOk = false;
            yield return _client.EnterScene(SceneConfigId, SceneId,
                () =>
                {
                    enterSceneOk = true;
                    _status = $"entered scene cfg={SceneConfigId}";
                    Append($"enter-scene OK cfg={SceneConfigId}");
                },
                e =>
                {
                    // EnterScene can be auto-pushed after EnterGame, so this is non-fatal.
                    Append("enter-scene hint: " + e);
                });

            if (!enterSceneOk)
            {
                _status = "in game (scene may auto-enter)";
            }

            _stage = LoginStage.InWorld;

            _isLoggingIn = false;
        }

        private void EnsureGuiStyles()
        {
            if (_panel != null) return;

            _bgGradient = BuildVerticalGradientTex(new Color(0.03f, 0.09f, 0.12f), new Color(0.25f, 0.20f, 0.11f));
            _mistTex = BuildCheckerTex(new Color(1f, 1f, 1f, 0.035f), new Color(1f, 1f, 1f, 0.01f));
            _panelTex = BuildSolidTex(new Color(0.08f, 0.10f, 0.13f, 0.92f));
            _btnPrimaryTex = BuildSolidTex(new Color(0.18f, 0.56f, 0.42f, 0.96f));
            _btnGhostTex = BuildSolidTex(new Color(0.14f, 0.17f, 0.20f, 0.84f));
            _zoneTex = BuildSolidTex(new Color(0.12f, 0.14f, 0.18f, 0.88f));
            _zoneActiveTex = BuildSolidTex(new Color(0.22f, 0.36f, 0.30f, 0.96f));
            _badgeTex = BuildSolidTex(new Color(0.76f, 0.62f, 0.31f, 0.95f));

            _rootLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                richText = true,
                normal = { textColor = new Color(0.89f, 0.92f, 0.88f, 0.98f) }
            };

            _panel = new GUIStyle(GUI.skin.box)
            {
                normal = { background = _panelTex },
                border = new RectOffset(10, 10, 10, 10),
                padding = new RectOffset(18, 18, 14, 14)
            };

            _title = new GUIStyle(_rootLabel)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.95f, 0.92f, 0.82f, 1f) }
            };

            _caption = new GUIStyle(_rootLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.70f, 0.75f, 0.70f, 0.95f) }
            };

            _buttonPrimary = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 34,
                normal = { background = _btnPrimaryTex, textColor = Color.white },
                active = { background = _btnPrimaryTex, textColor = new Color(0.9f, 1f, 0.9f) }
            };

            _buttonGhost = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fixedHeight = 30,
                normal = { background = _btnGhostTex, textColor = new Color(0.85f, 0.90f, 0.88f, 1f) },
                active = { background = _btnGhostTex, textColor = Color.white }
            };

            _zoneButton = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 4, 4),
                normal = { background = _zoneTex, textColor = new Color(0.86f, 0.90f, 0.90f, 1f) },
                fixedHeight = 38
            };

            _zoneButtonActive = new GUIStyle(_zoneButton)
            {
                normal = { background = _zoneActiveTex, textColor = new Color(0.96f, 1f, 0.95f, 1f) },
                fontStyle = FontStyle.Bold
            };

            _logStyle = new GUIStyle(_caption)
            {
                wordWrap = true,
                normal = { textColor = new Color(0.77f, 0.81f, 0.78f, 1f) }
            };
        }

        private void EnsurePreviewActor()
        {
            if (_previewRoot != null) return;

            _previewRoot = new GameObject("[LoginPreviewRoot]");
            _previewRoot.transform.position = new UnityEngine.Vector3(-5f, 1.5f, 5f);

            _previewAvatar = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _previewAvatar.name = "[LoginPreviewAvatar]";
            _previewAvatar.transform.SetParent(_previewRoot.transform, false);
            _previewAvatar.transform.localScale = new UnityEngine.Vector3(1f, 1.7f, 1f);
            _previewAvatar.transform.localPosition = UnityEngine.Vector3.zero;
            var rd = _previewAvatar.GetComponent<Renderer>();
            if (rd != null)
            {
                rd.material = new Material(Shader.Find("Standard"));
                rd.material.color = new Color(0.36f, 0.74f, 0.67f);
            }

            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "[PreviewBlade]";
            blade.transform.SetParent(_previewAvatar.transform, false);
            blade.transform.localScale = new UnityEngine.Vector3(0.08f, 0.95f, 0.08f);
            blade.transform.localPosition = new UnityEngine.Vector3(0.45f, 0.6f, 0f);
            blade.transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var br = blade.GetComponent<Renderer>();
            if (br != null)
            {
                br.material = new Material(Shader.Find("Standard"));
                br.material.color = new Color(0.82f, 0.88f, 0.92f);
            }
        }

        private static Texture2D BuildSolidTex(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildCheckerTex(Color a, Color b)
        {
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    bool odd = ((x / 8) + (y / 8)) % 2 == 0;
                    tex.SetPixel(x, y, odd ? a : b);
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D BuildVerticalGradientTex(Color top, Color bottom)
        {
            var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++)
            {
                float t = y / 255f;
                tex.SetPixel(0, y, Color.Lerp(top, bottom, t));
            }
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        private static string ZoneStatusText(ServerListZone z)
        {
            string status = string.IsNullOrWhiteSpace(z.status) ? "OPEN" : z.status;
            string load = string.IsNullOrWhiteSpace(z.load_level) ? "NORMAL" : z.load_level;
            string flag = z.recommended ? " [荐]" : (z.is_new ? " [新]" : string.Empty);
            return $"{status}/{load}{flag}";
        }

        private static string FriendlyStatus(string status)
        {
            return status switch
            {
                "OPEN" => "畅通",
                "MAINTENANCE" => "维护中",
                "CLOSED" => "已关闭",
                "PREVIEW" => "即将开启",
                _ => string.IsNullOrWhiteSpace(status) ? "未知" : status,
            };
        }

        private static string FriendlyLoad(string load)
        {
            return load switch
            {
                "SMOOTH" => "流畅",
                "BUSY" => "繁忙",
                "FULL" => "爆满",
                _ => string.IsNullOrWhiteSpace(load) ? "普通" : load,
            };
        }

        private static string ToClock(long epochSeconds)
        {
            if (epochSeconds <= 0) return "--";
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(epochSeconds).ToLocalTime().ToString("MM-dd HH:mm");
            }
            catch
            {
                return "--";
            }
        }

        private string SelectedZoneSummary()
        {
            if (_zones.Count == 0 || _selectedZoneIndex < 0 || _selectedZoneIndex >= _zones.Count)
            {
                return "No zone selected";
            }

            var z = _zones[_selectedZoneIndex];
            string name = string.IsNullOrWhiteSpace(z.name) ? $"Zone {z.zone_id}" : z.name;
            string extra = string.IsNullOrWhiteSpace(z.maintenance_msg) ? "" : $"  |  {z.maintenance_msg}";
            return $"{name} (#{z.zone_id})  |  {ZoneStatusText(z)}{extra}";
        }

        private void DrawBackground()
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            GUI.DrawTexture(screen, _bgGradient, ScaleMode.StretchToFill);

            float mistOffset = Mathf.Repeat(Time.realtimeSinceStartup * 14f, 64f);
            GUI.DrawTexture(new Rect(-mistOffset, 0, Screen.width + 64f, Screen.height), _mistTex, ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(-mistOffset * 0.6f + 80f, 0, Screen.width + 64f, Screen.height), _mistTex, ScaleMode.StretchToFill);
        }

        private void OnGUI()
        {
            EnsureGuiStyles();
            DrawBackground();

            float panelW = Mathf.Min(780f, Screen.width - 36f);
            float panelH = Mathf.Min(560f, Screen.height - 36f);
            var frame = new Rect((Screen.width - panelW) * 0.5f, (Screen.height - panelH) * 0.5f, panelW, panelH);

            GUILayout.BeginArea(frame, _panel);
            GUILayout.Label("云岚纪行", _title);
            GUILayout.Label("原创国风Q版登录门面 (不使用第三方游戏素材)", _caption);
            GUILayout.Space(6);

            switch (_stage)
            {
                case LoginStage.Landing:
                    DrawLandingStage();
                    break;
                case LoginStage.ServerSelect:
                    DrawServerSelectStage();
                    break;
                case LoginStage.RoleConfirm:
                    DrawRoleConfirmStage();
                    break;
                case LoginStage.InWorld:
                    DrawInWorldStage();
                    break;
            }

            GUILayout.EndArea();
        }

        private void DrawLandingStage()
        {
            GUILayout.Label("江湖快讯", _rootLabel);

            if (_loadingAnnouncements)
            {
                GUILayout.Label("正在拉取公告...", _caption);
            }
            else if (!string.IsNullOrWhiteSpace(_announceError))
            {
                GUILayout.Label(_announceError, _caption);
            }

            _announceScroll = GUILayout.BeginScrollView(_announceScroll, GUILayout.Height(240));
            if (_announcements.Count == 0)
            {
                GUILayout.Label("暂无公告", _caption);
            }
            else
            {
                foreach (var a in _announcements)
                {
                    GUILayout.Label($"[{a.type}] {a.title}", _rootLabel);
                    GUILayout.Label(a.content, _caption);
                    GUILayout.Label($"时间: {ToClock(a.start_time)} - {ToClock(a.end_time)}", _caption);
                    GUILayout.Space(8);
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GatewayBaseUrl = LabelField("Gateway", GatewayBaseUrl);
            _accountDraft = LabelField("Account", _accountDraft);
            _passwordDraft = LabelField("Password", _passwordDraft);

            GUILayout.Space(10);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("进入选服", _buttonPrimary)) _stage = LoginStage.ServerSelect;
            if (GUILayout.Button("刷新门面数据", _buttonGhost) && !_loadingZones && !_loadingAnnouncements)
            {
                StartCoroutine(LoadGatewayFrontPage());
            }
            GUILayout.EndHorizontal();
        }

        private void DrawServerSelectStage()
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(420));
            GUILayout.Label("服务器列表", _rootLabel);
            if (_loadingZones) GUILayout.Label("正在从 Java Gateway 拉取服务器列表...", _caption);
            if (!string.IsNullOrWhiteSpace(_zoneLoadError)) GUILayout.Label(_zoneLoadError, _caption);

            _zoneScroll = GUILayout.BeginScrollView(_zoneScroll, GUILayout.Height(320));
            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                string zoneName = string.IsNullOrWhiteSpace(z.name) ? $"Zone {z.zone_id}" : z.name;
                string text = $"{zoneName}  #{z.zone_id}\n{FriendlyStatus(z.status)} / {FriendlyLoad(z.load_level)}";
                bool active = i == _selectedZoneIndex;
                if (GUILayout.Button(text, active ? _zoneButtonActive : _zoneButton))
                {
                    _selectedZoneIndex = i;
                    ZoneId = z.zone_id;
                    _status = $"selected zone #{ZoneId}";
                }
            }
            GUILayout.EndScrollView();

            GUILayout.Label("状态: " + _status, _caption);
            GUILayout.EndVertical();

            GUILayout.Space(12);
            GUILayout.BeginVertical();
            GUILayout.Label("选区详情", _rootLabel);
            GUILayout.Label(SelectedZoneSummary(), _caption);
            GUILayout.Space(8);
            GUI.DrawTexture(GUILayoutUtility.GetRect(140, 24), _badgeTex, ScaleMode.StretchToFill);
            GUILayout.Label("推荐区服优先", _caption);
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("返回", _buttonGhost)) _stage = LoginStage.Landing;
            if (GUILayout.Button("确认区服", _buttonPrimary)) _stage = LoginStage.RoleConfirm;
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        private void DrawRoleConfirmStage()
        {
            GUILayout.Label("角色确认", _rootLabel);
            GUILayout.Label("此处为原创Q版占位形象，后续可替换为美术角色模型。", _caption);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(430));
            GUILayout.Label("当前角色: 云行客", _rootLabel);
            GUILayout.Label("武器: 青锋短剑", _caption);
            GUILayout.Label("职业: 游侠", _caption);
            GUILayout.Label("目标区服: " + SelectedZoneSummary(), _caption);

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            GUILayout.Label("SceneCfg", GUILayout.Width(80));
            SceneConfigId = ParseUInt(GUILayout.TextField(SceneConfigId.ToString()), SceneConfigId);
            GUILayout.EndHorizontal();

            GUILayout.Space(12);
            if (GUILayout.Button(_isLoggingIn ? "正在进入..." : "进入江湖", _buttonPrimary) && !_isLoggingIn)
            {
                StartCoroutine(LoginAndEnterJourney());
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("返回选服", _buttonGhost)) _stage = LoginStage.ServerSelect;
            if (GUILayout.Button("刷新公告", _buttonGhost) && !_loadingAnnouncements)
            {
                StartCoroutine(RefreshAnnouncements());
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("登录日志", _rootLabel);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(260));
            for (int i = _log.Count - 1; i >= 0 && i > _log.Count - 120; i--)
            {
                GUILayout.Label(_log[i], _logStyle);
            }
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
        }

        private void DrawInWorldStage()
        {
            GUILayout.Label("已进入世界", _rootLabel);
            GUILayout.Label($"scene={_client?.CurrentSceneId}  player={_client?.PlayerId}  actors={_client?.World?.Actors.Count}", _caption);
            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            GUILayout.Label("SkillID", GUILayout.Width(56));
            SkillTableId = ParseUInt(GUILayout.TextField(SkillTableId.ToString()), SkillTableId);
            GUILayout.EndHorizontal();

            if (GUILayout.Button($"释放技能 {SkillTableId}", _buttonGhost))
            {
                ulong target = TargetEntity;
                if (target == 0)
                {
                    var first = _client.World.Actors.Values.FirstOrDefault(a => a.Entity != _client.World.LocalEntity);
                    if (first != null) target = first.Entity;
                }
                _client.ReleaseSkill(SkillTableId, target);
                Append($"sent ReleaseSkill skill={SkillTableId} target={target}");
            }

            GUILayout.Space(8);
            GUILayout.Label("运行日志", _rootLabel);
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(320));
            for (int i = _log.Count - 1; i >= 0 && i > _log.Count - 200; i--)
            {
                GUILayout.Label(_log[i], _logStyle);
            }
            GUILayout.EndScrollView();
        }

        private void OnDestroy() => _client?.Disconnect();

        // ── helpers ──────────────────────────────────────────────

        private void EnsureSceneRig()
        {
            _camera = Camera.main;
            if (_camera == null)
            {
                var camGo = new GameObject("[DemoCamera]");
                _camera = camGo.AddComponent<Camera>();
                camGo.tag = "MainCamera";
                camGo.transform.position = new UnityEngine.Vector3(0, 8f, -10f);
                camGo.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            }
            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("[DemoSun]");
                var l = lightGo.AddComponent<Light>();
                l.type = LightType.Directional;
                l.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }
            // Ground plane so primitives are visible against something.
            if (GameObject.Find("[DemoGround]") == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "[DemoGround]";
                ground.transform.localScale = new UnityEngine.Vector3(20f, 1f, 20f);
                var rend = ground.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.25f, 0.3f, 0.25f);
            }
        }

        private static string LabelField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            value = GUILayout.TextField(value);
            GUILayout.EndHorizontal();
            return value;
        }

        private static uint ParseUInt(string s, uint fallback)
            => uint.TryParse(s, out var v) ? v : fallback;

        private void Append(string s)
        {
            _log.Add(s);
            Debug.Log("[GameDemo] " + s);
        }
    }
}
