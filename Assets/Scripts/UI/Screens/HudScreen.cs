using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// In-world HUD overlay. Minimal v1: top bar with role/zone, bottom right
    /// skill button, bottom log panel. Replace with proper HUD UXML later.
    /// </summary>
    public sealed class HudScreen : IScreen
    {
        private AppBootstrap _app;
        private Label _topbarLabel;
        private ScrollView _logView;
        private System.Collections.Generic.Queue<string> _logBuffer = new();

        public VisualElement Build(AppBootstrap app)
        {
            _app = app;
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.pickingMode = PickingMode.Ignore;

            // Top bar
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.alignItems = Align.Center;
            top.style.height = 36;
            top.style.paddingLeft = top.style.paddingRight = 12;
            top.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            _topbarLabel = new Label();
            _topbarLabel.style.color = Theme.TextPrim;
            _topbarLabel.style.flexGrow = 1;
            top.Add(_topbarLabel);
            top.Add(Theme.GhostButton("登出", OnLogout));
            root.Add(top);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            root.Add(spacer);

            // Bottom row
            var bottom = new VisualElement();
            bottom.style.flexDirection = FlexDirection.Row;
            bottom.style.alignItems = Align.FlexEnd;
            bottom.style.paddingLeft = bottom.style.paddingRight = 16;
            bottom.style.paddingBottom = 16;

            // Log panel (left)
            var logPanel = new VisualElement();
            logPanel.style.width = 360;
            logPanel.style.height = 160;
            logPanel.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            logPanel.style.paddingLeft = logPanel.style.paddingRight = 8;
            logPanel.style.paddingTop = logPanel.style.paddingBottom = 6;
            logPanel.style.borderTopLeftRadius = logPanel.style.borderTopRightRadius =
                logPanel.style.borderBottomLeftRadius = logPanel.style.borderBottomRightRadius = 4;
            _logView = new ScrollView { mode = ScrollViewMode.Vertical };
            _logView.style.flexGrow = 1;
            logPanel.Add(_logView);
            bottom.Add(logPanel);

            var bottomSpacer = new VisualElement();
            bottomSpacer.style.flexGrow = 1;
            bottom.Add(bottomSpacer);

            // Skill button (right)
            var skill = Theme.PrimaryButton("释放技能", OnReleaseSkill);
            skill.style.width = 140;
            skill.style.height = 56;
            skill.style.fontSize = 16;
            bottom.Add(skill);

            root.Add(bottom);
            return root;
        }

        public void OnEnter()
        {
            _app.GameClient.OnLog += AppendLog;
            UpdateTopBar();
        }

        public void OnExit()
        {
            _app.GameClient.OnLog -= AppendLog;
        }

        public void Tick(float dt) { }

        private void UpdateTopBar()
        {
            var z = _app.Session.SelectedZone;
            string archetype = (_app.Session.RoleArchetypeIndex >= 0 && _app.Session.RoleArchetypeIndex < 3)
                ? new[] { "游侠", "灵符师", "剑客" }[_app.Session.RoleArchetypeIndex] : "?";
            _topbarLabel.text = $"{_app.Session.RoleNickname} ({archetype})  ·  区服: {(z != null ? z.name : "?")}  ·  PlayerId: {_app.GameClient.PlayerId}";
        }

        private void AppendLog(string msg)
        {
            _logBuffer.Enqueue(msg);
            while (_logBuffer.Count > 60) _logBuffer.Dequeue();
            if (_logView == null) return;
            _logView.Clear();
            foreach (var s in _logBuffer)
            {
                var l = new Label(s);
                l.style.color = Theme.TextDim;
                l.style.fontSize = 11;
                l.style.whiteSpace = WhiteSpace.Normal;
                _logView.Add(l);
            }
        }

        private void OnReleaseSkill()
        {
            // pick first non-local actor as target if available
            ulong target = 0;
            var world = _app.GameClient.World;
            if (world != null)
            {
                var local = world.LocalEntity;
                foreach (var kv in world.Actors)
                {
                    if (kv.Value.Entity != local) { target = kv.Value.Entity; break; }
                }
            }
            _app.GameClient.ReleaseSkill(1001, target);
            AppendLog($"[skill] release 1001 -> {target}");
        }

        private void OnLogout()
        {
            _app.Router.Show<LoginScreen>();
        }
    }
}
