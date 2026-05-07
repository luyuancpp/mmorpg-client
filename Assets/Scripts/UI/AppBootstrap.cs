using System.Collections;
using System;
using FairyGUI;
using MmorpgClient.Game;
using MmorpgClient.Net;
using MmorpgClient.UI.Screens;
using UnityEngine;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Single entry point for the production client. Auto-spawns on play
    /// (no scene asset required), bootstraps the FairyGUI Stage / GRoot,
    /// owns the long-lived <see cref="GameClient"/> + <see cref="GatewayHttpClient"/>
    /// and drives the screen stack via <see cref="Router"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AppBootstrap : MonoBehaviour
    {
        public const string GameObjectName = "[MmorpgClient]";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<AppBootstrap>() != null) return;
            var go = new GameObject(GameObjectName);
            DontDestroyOnLoad(go);
            go.AddComponent<AppBootstrap>();
        }

        public SessionModel Session { get; private set; }
        public GameClient   GameClient { get; private set; }
        public GatewayHttpClient Gateway { get; private set; }
        public ScreenRouter Router { get; private set; }

        private GComponent _root;
        private GComponent _host;
        private GGraph     _backdropTop;
        private GGraph     _backdropBottom;

        private void Awake()
        {
            Debug.Log("[AppBootstrap] Awake (FairyGUI mode)");

            EnsureSceneRig();
            EnsureFairyGUIStage();
            TryLoadUiPackage();

            Session    = new SessionModel();
            Gateway    = new GatewayHttpClient(Session.GatewayBaseUrl);
            GameClient = new GameClient(Session.GatewayBaseUrl);
            GameClient.OnLog += s => Debug.Log("[GameClient] " + s);

            BuildRoot();
            Router = new ScreenRouter(this, _host);
            GRoot.inst.onSizeChanged.Add(OnRootResize);

            Router.Show<LoginScreen>();
        }

        private void Update()
        {
            GameClient?.Tick();
            GameClient?.World?.Tick();
            Router?.Tick(Time.deltaTime);
        }

        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

        // ───────────────────────────────────────────────────────────

        private void EnsureFairyGUIStage()
        {
            // Touching Stage.inst lazily creates the FairyGUI stage if absent.
            // Touch GRoot.inst to ensure the root container exists.
            _ = Stage.inst;
            _ = GRoot.inst;
        }

        private void TryLoadUiPackage()
        {
            if (UIPackage.GetByName(Theme.UiPackageName) != null) return;
            try
            {
                UIPackage.AddPackage(Theme.UiPackagePath);
                Debug.Log($"[AppBootstrap] loaded FairyGUI package: {Theme.UiPackagePath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AppBootstrap] FairyGUI package not found, fallback to code UI. path={Theme.UiPackagePath}, err={ex.Message}");
            }
        }

        private void BuildRoot()
        {
            _root = new GComponent();
            _root.SetSize(GRoot.inst.width, GRoot.inst.height);
            _root.AddRelation(GRoot.inst, RelationType.Size);
            GRoot.inst.AddChild(_root);

            // backdrop layers
            _backdropTop = new GGraph();
            _backdropTop.DrawRect(_root.width, _root.height * 0.52f, 0, Color.clear, Theme.BgTop);
            _root.AddChild(_backdropTop);

            _backdropBottom = new GGraph();
            _backdropBottom.SetXY(0, _root.height * 0.52f);
            _backdropBottom.DrawRect(_root.width, _root.height * 0.48f, 0, Color.clear, Theme.BgBottom);
            _root.AddChild(_backdropBottom);

            var moon = new GGraph();
            moon.SetXY(_root.width - 210, 44);
            moon.DrawEllipse(120, 120, new Color(1f, 0.97f, 0.84f, 0.32f));
            _root.AddChild(moon);

            _host = new GComponent();
            _host.SetSize(_root.width, _root.height);
            _root.AddChild(_host);
        }

        private void OnRootResize()
        {
            float w = GRoot.inst.width, h = GRoot.inst.height;
            _root.SetSize(w, h);
            _backdropTop.DrawRect(w, h * 0.52f, 0, Color.clear, Theme.BgTop);
            _backdropBottom.SetXY(0, h * 0.52f);
            _backdropBottom.DrawRect(w, h * 0.48f, 0, Color.clear, Theme.BgBottom);
            _host.SetSize(w, h);
            Router?.OnRootResize();
        }

        private void EnsureSceneRig()
        {
            if (Camera.main == null)
            {
                var camGo = new GameObject("[MainCamera]");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.20f, 0.23f, 0.19f);
                cam.transform.position = new UnityEngine.Vector3(0f, 4f, -10f);
                cam.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
                camGo.AddComponent<AudioListener>();
            }
            if (FindAnyObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("[DirLight]");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                lightGo.transform.rotation = Quaternion.Euler(40f, 30f, 0f);
            }
        }
    }
}
