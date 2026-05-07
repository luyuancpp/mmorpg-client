using System;
using System.Collections;
using MmorpgClient.Game;
using MmorpgClient.Net;
using MmorpgClient.UI.Screens;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Single entry point for the production client. Auto-spawns on play (no
    /// scene asset needed), constructs the UI Toolkit root, the screen router,
    /// the long-lived <see cref="GameClient"/> and the scene rig (camera +
    /// directional light). Screens drive transitions through <see cref="Router"/>.
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

        private UIDocument _doc;
        private PanelSettings _panel;

        private void Awake()
        {
            Debug.Log("[AppBootstrap] Awake");

            EnsureSceneRig();

            Session    = new SessionModel();
            Gateway    = new GatewayHttpClient(Session.GatewayBaseUrl);
            GameClient = new GameClient(Session.GatewayBaseUrl);
            GameClient.OnLog += s => Debug.Log("[GameClient] " + s);

            BuildUiDocument();
            Router = new ScreenRouter(this, _doc.rootVisualElement);

            // Background gradient on the root container
            ApplyRootBackground(_doc.rootVisualElement);

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

        private void BuildUiDocument()
        {
            _panel = ScriptableObject.CreateInstance<PanelSettings>();
            _panel.name = "MmorpgClient.PanelSettings";
            _panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panel.referenceResolution = new Vector2Int(1920, 1080);
            _panel.match = 0.5f;
            _panel.sortingOrder = 100;
            // Do not assign a theme stylesheet - styles are inlined via Theme.cs.
            _panel.themeStyleSheet = null;

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = _panel;
            _doc.rootVisualElement.style.flexGrow = 1;
        }

        private static void ApplyRootBackground(VisualElement root)
        {
            // Vertical-ish gradient via two stacked layers.
            root.style.backgroundColor = Theme.BgTop;
            var bottom = new VisualElement();
            bottom.style.position = Position.Absolute;
            bottom.style.left = 0;
            bottom.style.right = 0;
            bottom.style.bottom = 0;
            bottom.style.height = new Length(60, LengthUnit.Percent);
            bottom.style.backgroundColor = Theme.BgBottom;
            bottom.style.opacity = 0.85f;
            root.Insert(0, bottom);
        }

        private void EnsureSceneRig()
        {
            if (Camera.main == null)
            {
                var camGo = new GameObject("[MainCamera]");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
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
