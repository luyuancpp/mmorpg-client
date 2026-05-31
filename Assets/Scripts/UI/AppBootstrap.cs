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
            ExcludeFairyGuiLayerFromWorldCameras();
            TryLoadUiPackage();

            Session    = new SessionModel();
            Gateway    = new GatewayHttpClient(Session.GatewayBaseUrl);
            GameClient = new GameClient(Session.GatewayBaseUrl);
            GameClient.OnLog += s => Debug.Log("[GameClient] " + s);

            BuildRoot();
            Router = new ScreenRouter(this, _host);
            GRoot.inst.onSizeChanged.Add(OnRootResize);

            // Boot directly into the V3 login flow. The router stack is
            // LoginV3Screen → ServersV3Screen → SceneV3Screen — see those
            // classes for the layout contracts with the FairyGUI components.
            Router.Show<LoginV3Screen>();
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

            // Default font for ALL text nodes that don't explicitly set a font.
            // FairyGUI looks up the first name via Resources.Load<Font>("Fonts/<name>")
            // — we ship Assets/Resources/Fonts/SimKai.ttf so this resolves on every
            // platform, regardless of what fonts the user has installed. Subsequent
            // names act as graceful fallbacks if the bundled .ttf is ever stripped.
            //
            // This must be set BEFORE any text-bearing UIPackage is loaded; once a
            // GTextField has captured the old default it won't re-read this value.
            UIConfig.defaultFont = Theme.BodyFontName;

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

            // The qdao art is authored as one fixed 2560x1080 widescreen
            // composition. Always matching width preserves the whole frame in
            // narrower Unity Game views instead of cropping the right side.
            // Extra vertical space is acceptable letterbox room; losing the
            // character and right panel is not.
            scaler.screenMatchMode = UIContentScaler.ScreenMatchMode.MatchWidth;
            scaler.ApplyChange();
            GRoot.inst.ApplyContentScaleFactor();

            // Sharpening pass — combats the residual blur you get whenever the
            // window's pixel size is not an integer multiple of the design
            // resolution (1920x1080 vs 2560x1080 = 0.75x, 4K = 1.5x, etc.):
            //
            // 1. pixelPerfect on GRoot snaps every child's final transform to
            //    integer pixels, so sprite edges land on texel boundaries
            //    instead of being bilinear-blended across two screen pixels.
            // 2. 4x MSAA on the global QualitySettings smooths the residual
            //    sub-pixel jitter on rotated / non-axis-aligned art (badges).
            //    The Very-High/Ultra quality levels in ProjectSettings have
            //    been bumped to antiAliasing=4 so this is now the asset
            //    default, not just a runtime override.
            // 3. anisoLevel=ForceEnable lets the atlas's aniso=2 setting
            //    actually take effect at runtime.
            // 4. mipmap bias -0.5 (also set on the atlas .meta) keeps the
            //    sampler one mip-level sharper than Unity's auto-choice
            //    when the UI is shrunk by a fractional factor.
            GRoot.inst.displayObject.pixelPerfect = true;
            QualitySettings.antiAliasing = Mathf.Max(QualitySettings.antiAliasing, 4);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.globalTextureMipmapLimit = 0;
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
            // UIContentScaler (configured in EnsureFairyGUIStage) already maps
            // the 2560x1080 design space to the actual window with a single
            // uniform scale + letterbox via MatchWidthOrHeight. We MUST NOT
            // apply a second SetScale on _root, otherwise every sprite goes
            // through two non-integer scales and lands on sub-pixel offsets,
            // which is exactly what makes the entire UI look blurry.
            //
            // _root / _host are now 1:1 children of GRoot, sized to the
            // design canvas. GRoot.width/height already report design units
            // (2560 x 1080-ish, varying with window aspect ratio), so screens
            // can lay themselves out in design coordinates with no scaling
            // applied here.
            _root = new GComponent();
            _root.SetSize(GRoot.inst.width, GRoot.inst.height);
            GRoot.inst.AddChild(_root);

            _host = new GComponent();
            _host.SetSize(GRoot.inst.width, GRoot.inst.height);
            _root.AddChild(_host);

            FitRootToScreen();
        }

        private void OnRootResize()
        {
            FitRootToScreen();
            Router?.OnRootResize();
        }

        /// <summary>
        /// Resize the design canvas to whatever GRoot currently reports.
        /// UIContentScaler handles the actual pixel scaling — we only keep
        /// _root / _host the same size as GRoot so screens fill it cleanly.
        /// No SetScale here on purpose (see BuildRoot for the rationale).
        /// </summary>
        private void FitRootToScreen()
        {
            if (_root == null) return;
            _root.SetSize(GRoot.inst.width, GRoot.inst.height);
            _root.SetXY(0f, 0f);
            if (_host != null)
                _host.SetSize(GRoot.inst.width, GRoot.inst.height);
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

        private static void ExcludeFairyGuiLayerFromWorldCameras()
        {
            int uiLayer = LayerMask.NameToLayer(StageCamera.LayerName);
            if (uiLayer < 0) return;

            int uiMask = 1 << uiLayer;
            foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (camera == null || camera.GetComponent<StageCamera>() != null) continue;
                camera.cullingMask &= ~uiMask;
            }
        }
    }
}
