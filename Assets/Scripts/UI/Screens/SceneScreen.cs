using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Loads the qdao SceneScreen FairyGUI component (cave-heaven roster).
    /// All visuals are owned by the .fui package; this class only binds named
    /// children to runtime behavior (scroll buttons, back/confirm).
    /// </summary>
    public sealed class SceneScreen : IScreen
    {
        private const float ScrollStepPixels = 110f;

        private AppBootstrap _app;
        private GComponent _root;
        private GList _list;
        private ScrollPane _scroll;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            _root = Theme.TryCreateFromPackage(Theme.UiId.SceneRoot);
            if (_root == null)
            {
                Debug.LogWarning(
                    $"[SceneScreen] FairyGUI package '{Theme.UiPackageName}' missing or " +
                    $"component '{Theme.UiId.SceneRoot}' not published. " +
                    $"Open {Theme.UiPackagePath}.fairy in FairyGUI Editor and republish.");
                _root = BuildPlaceholder();
                return _root;
            }

            _list = Theme.Find<GList>(_root, Theme.UiId.SceneList);
            _scroll = _list != null ? _list.scrollPane : null;

            BindButton(Theme.UiId.SceneScrollUpBtn,   ScrollUp);
            BindButton(Theme.UiId.SceneScrollDownBtn, ScrollDown);
            BindButton(Theme.UiId.SceneBackBtn,       () => _app.Router.Show<LoginScreen>());
            BindButton(Theme.UiId.SceneRefreshBtn,    Refresh);
            BindButton(Theme.UiId.SceneConfirmBtn,    OnConfirm);

            return _root;
        }

        public void OnEnter() { }
        public void OnExit() { }
        public void Tick(float dt) { }

        private void BindButton(string childName, System.Action handler)
        {
            var go = _root.GetChild(childName);
            if (go == null) return;
            go.onClick.Set(_ => handler?.Invoke());
        }

        private void ScrollUp()
        {
            if (_scroll == null) return;
            _scroll.posY = Mathf.Max(0f, _scroll.posY - ScrollStepPixels);
        }

        private void ScrollDown()
        {
            if (_scroll == null) return;
            float maxY = Mathf.Max(0f, _scroll.contentHeight - _scroll.viewHeight);
            _scroll.posY = Mathf.Min(maxY, _scroll.posY + ScrollStepPixels);
        }

        private void Refresh()
        {
            // Placeholder until SceneScreen is wired to live zone data.
            var status = Theme.Find<GTextField>(_root, Theme.UiId.SceneStatus);
            if (status != null) status.text = "refreshed";
        }

        private void OnConfirm()
        {
            _app.Router.Show<RoleCreateScreen>();
        }

        private static GComponent BuildPlaceholder()
        {
            var c = new GComponent();
            c.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);
            var label = Theme.ArtText("SceneScreen package missing", Theme.TextWarn, 28, true, AlignType.Center);
            label.SetSize(c.width, c.height);
            label.verticalAlign = VertAlignType.Middle;
            c.AddChild(label);
            return c;
        }
    }
}
