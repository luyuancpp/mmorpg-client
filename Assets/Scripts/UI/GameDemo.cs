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
    /// </summary>
    public sealed class GameDemo : MonoBehaviour
    {
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

        private GameClient _client;
        private readonly List<string> _log = new();
        private string _status = "idle";
        private Vector2 _scroll;
        private Camera _camera;

        private void Awake()
        {
            EnsureSceneRig();

            _client = new GameClient(GatewayBaseUrl);
            _client.OnLog += Append;
            _client.OnDisconnected += () => { _status = "disconnected"; Append("[gate] disconnected"); };
        }

        private void Update()
        {
            _client.Tick();
            if (_camera != null && _client.World != null && _client.World.LocalEntity != 0
                && _client.World.TryGetActor(_client.World.LocalEntity, out var me))
            {
                // Simple chase: 6m back, 5m up, look at player.
                var t = me.Go.transform;
                var desired = t.position + new Vector3(0, 5f, -6f);
                _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, 0.1f);
                _camera.transform.LookAt(t.position + Vector3.up);
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 460, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("MMORPG Unity Client Demo");
            GatewayBaseUrl = LabelField("Gateway",  GatewayBaseUrl);
            Account        = LabelField("Account",  Account);
            Password       = LabelField("Password", Password);
            SceneConfigId  = ParseUInt (LabelField("Scene Cfg", SceneConfigId.ToString()), SceneConfigId);
            SkillTableId   = ParseUInt (LabelField("Skill ID",  SkillTableId.ToString()),  SkillTableId);

            GUILayout.Space(6);
            if (GUILayout.Button("1. Login + Enter Game"))
            {
                _status = "logging in...";
                StartCoroutine(_client.LoginAndEnterGame(ZoneId, Account, Password,
                    () => _status = "in game",
                    e  => { _status = "FAILED"; Append("ERR " + e); }));
            }
            if (GUILayout.Button("2. (debug) Send EnterScene C2S"))
            {
                if (!_client.InGame) { Append("not in game yet"); }
                else
                {
                    StartCoroutine(_client.EnterScene(SceneConfigId, SceneId,
                        () => Append($"enter-scene OK cfg={SceneConfigId}"),
                        e  => Append("enter-scene ERR " + e)));
                }
            }
            if (GUILayout.Button($"3. Release Skill {SkillTableId} on auto target"))
            {
                ulong target = TargetEntity;
                if (target == 0)
                {
                    var first = _client.World.Actors.Values
                        .FirstOrDefault(a => a.Entity != _client.World.LocalEntity);
                    if (first != null) target = first.Entity;
                }
                _client.ReleaseSkill(SkillTableId, target);
                Append($"sent ReleaseSkill skill={SkillTableId} target={target}");
            }

            GUILayout.Space(6);
            GUILayout.Label($"status: {_status}");
            GUILayout.Label($"scene={_client?.CurrentSceneId}  player={_client?.PlayerId}  " +
                            $"actors={_client?.World?.Actors.Count}");
            GUILayout.Label("log:");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Screen.height - 320));
            for (int i = _log.Count - 1; i >= 0 && i > _log.Count - 200; i--)
                GUILayout.Label(_log[i]);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
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
                camGo.transform.position = new Vector3(0, 8f, -10f);
                camGo.transform.rotation = Quaternion.Euler(35f, 0f, 0f);
            }
            if (FindObjectOfType<Light>() == null)
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
                ground.transform.localScale = new Vector3(20f, 1f, 20f);
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
