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
        public bool QdaoPackageLoaded { get; private set; }

        private GComponent _root;
        private GComponent _host;
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

            // Boot into SceneScreen (qdao FairyGUI roster). Switch to
            // Router.Show<LoginScreen>() if you want the login flow first.
            Router.Show<SceneScreen>();
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

            // Without a UIContentScaler, GRoot reports raw pixel size, so the
            // qdao screens authored at 2560x1080 get resized to the window's
            // pixel dimensions while their child art keeps the design-space
            // positions/sizes -> wrong placement and wildly wrong scale.
            // Configure a scaler so 2560x1080 maps consistently to the window.
            var stageGo = Stage.inst.gameObject;
            var scaler = stageGo.GetComponent<UIContentScaler>();
            if (scaler == null)
                scaler = stageGo.AddComponent<UIContentScaler>();
            scaler.scaleMode = UIContentScaler.ScaleMode.ScaleWithScreenSize;
            scaler.designResolutionX = (int)Theme.Art.ReferenceWidth;
            scaler.designResolutionY = (int)Theme.Art.ReferenceHeight;
            scaler.screenMatchMode = UIContentScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.ApplyChange();
            GRoot.inst.ApplyContentScaleFactor();
        }

        private void TryLoadUiPackage()
        {
            if (UIPackage.GetByName(Theme.UiPackageName) != null) return;
            try
            {
                UIPackage.AddPackage(Theme.UiPackagePath);
                QdaoPackageLoaded = true;
                Debug.Log($"[AppBootstrap] loaded FairyGUI package: {Theme.UiPackagePath}");
            }
            catch (Exception ex)
            {
                QdaoPackageLoaded = false;
                Debug.LogWarning($"[AppBootstrap] FairyGUI package not found, fallback to code UI. path={Theme.UiPackagePath}, err={ex.Message}");
            }
        }

        private void BuildRoot()
        {
            // _root is a fixed 2560x1080 design-space canvas. FairyGUI screen
            // components own their visual art and interaction widgets in that
            // same coordinate system. We then uniformly scale & center _root
            // inside GRoot (letterbox / pillarbox) so the canvas keeps its
            // aspect ratio at any window size.
            _root = new GComponent();
            _root.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);
            GRoot.inst.AddChild(_root);

            _host = new GComponent();
            _host.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);
            _root.AddChild(_host);

            FitRootToScreen();
        }

        private void OnRootResize()
        {
            FitRootToScreen();
            Router?.OnRootResize();
        }

        /// <summary>
        /// Uniformly scale the 2560x1080 design canvas to fit GRoot, preserving
        /// aspect ratio (letterbox / pillarbox). Backdrop + UI keep pixel-exact
        /// alignment because they share the same parent transform.
        /// </summary>
        private void FitRootToScreen()
        {
            if (_root == null) return;
            float gw = Mathf.Max(1f, GRoot.inst.width  > 1f ? GRoot.inst.width  : Screen.width);
            float gh = Mathf.Max(1f, GRoot.inst.height > 1f ? GRoot.inst.height : Screen.height);
            float scale = Mathf.Min(gw / Theme.Art.ReferenceWidth, gh / Theme.Art.ReferenceHeight);
            _root.SetScale(scale, scale);
            _root.SetXY((gw - Theme.Art.ReferenceWidth  * scale) * 0.5f,
                        (gh - Theme.Art.ReferenceHeight * scale) * 0.5f);
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
