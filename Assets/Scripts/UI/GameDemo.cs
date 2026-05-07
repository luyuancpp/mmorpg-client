using System.Collections.Generic;
using Google.Protobuf;
using MmorpgClient.Game;
using MmorpgClient.Net;
using UnityEngine;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Minimal IMGUI panel that drives the full client demo:
    ///   1. Login (gateway HTTP + Gate TCP token verify + Login + EnterGame)
    ///   2. Enter scene 1
    ///   3. Cast skill 1
    ///
    /// Drop this on any GameObject in the scene. Set GatewayBaseUrl to
    /// the Java gateway, e.g. http://127.0.0.1:8080 in local dev.
    /// </summary>
    public sealed class GameDemo : MonoBehaviour
    {
        [Header("Server")]
        public string GatewayBaseUrl = "http://127.0.0.1:8080";
        public uint   ZoneId         = 1;

        [Header("Account")]
        public string Account  = "demo";
        public string Password = "demo";

        [Header("Skill")]
        public uint   SkillTableId = 1001;
        public ulong  TargetEntity = 0;

        private GameClient _client;
        private readonly List<string> _log = new();
        private string _status = "idle";
        private Vector2 _scroll;

        private void Awake()
        {
            _client = new GameClient(GatewayBaseUrl);
            _client.OnLog += s => Append(s);
            _client.OnNotify(MessageIds.NotifyEnterScene,       _ => Append("[notify] entered scene"));
            _client.OnNotify(MessageIds.NotifySceneInfo,        _ => Append("[notify] scene info"));
            _client.OnNotify(MessageIds.NotifyActorListCreate,  _ => Append("[notify] actor list create"));
            _client.OnNotify(MessageIds.NotifyActorCreate,      _ => Append("[notify] actor create"));
            _client.OnNotify(MessageIds.NotifyActorDestroy,     _ => Append("[notify] actor destroy"));
            _client.OnNotify(MessageIds.NotifySkillUsed,        _ => Append("[notify] skill used"));
            _client.OnNotify(MessageIds.NotifySkillInterrupted, _ => Append("[notify] skill interrupted"));
            _client.OnNotify(MessageIds.TipToClient,            _ => Append("[notify] server tip"));
        }

        private void Update() => _client.Tick();

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 460, Screen.height - 20), GUI.skin.box);
            GUILayout.Label("MMORPG Unity Client Demo");
            GatewayBaseUrl = LabelField("Gateway",  GatewayBaseUrl);
            Account        = LabelField("Account",  Account);
            Password       = LabelField("Password", Password);
            SkillTableId   = (uint)int.Parse(LabelField("Skill ID", SkillTableId.ToString()));

            GUILayout.Space(6);
            if (GUILayout.Button("1. Login + Enter Game"))
            {
                _status = "logging in...";
                StartCoroutine(_client.LoginAndEnterGame(ZoneId, Account, Password,
                    () => _status = "in game",
                    e  => { _status = "FAILED"; Append("ERR " + e); }));
            }
            // Scene transition is normally driven by EnterGame on the server side
            // (it pushes NotifyEnterScene). The explicit C2S EnterScene RPC below
            // is here for ad-hoc testing during dev.
            if (GUILayout.Button("2. (debug) Send EnterScene C2S"))
            {
                Append("not yet wired -- see README for SceneInfoComp construction");
            }
            if (GUILayout.Button($"3. Release Skill {SkillTableId}"))
            {
                _client.ReleaseSkill(SkillTableId, TargetEntity);
                Append($"sent ReleaseSkill skill={SkillTableId}");
            }

            GUILayout.Space(6);
            GUILayout.Label($"status: {_status}");
            GUILayout.Label("log:");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(Screen.height - 280));
            for (int i = _log.Count - 1; i >= 0 && i > _log.Count - 200; i--)
                GUILayout.Label(_log[i]);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void OnDestroy() => _client?.Disconnect();

        private static string LabelField(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(80));
            value = GUILayout.TextField(value);
            GUILayout.EndHorizontal();
            return value;
        }

        private void Append(string s)
        {
            _log.Add(s);
            Debug.Log("[GameDemo] " + s);
        }
    }
}
