using System.Collections.Generic;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// In-world HUD overlay. Minimal v1: top bar with role/zone, bottom-right
    /// skill button, bottom-left scrolling log panel.
    /// </summary>
    public sealed class HudScreen : IScreen
    {
        private AppBootstrap _app;
        private GTextField _topbarLabel;
        private GComponent _logPanel;
        private GTextField _logText;
        private readonly Queue<string> _logBuffer = new();

        public GComponent Build(AppBootstrap app)
        {
            _app = app;
            var packagedRoot = BuildFromPackage(app);
            if (packagedRoot != null)
                return packagedRoot;

            var root = new GComponent();
            root.SetSize(GRoot.inst.width, GRoot.inst.height);
            root.opaque = false;

            // Top bar
            var top = new GComponent();
            top.SetSize(root.width, 42);
            top.AddRelation(root, RelationType.Width);
            var topBg = new GGraph(); topBg.DrawRect(root.width, 42, 1, new Color(0.95f, 0.89f, 0.70f, 0.42f), new Color(0.07f, 0.12f, 0.11f, 0.70f)); topBg.AddRelation(root, RelationType.Width);
            top.AddChild(topBg);

            _topbarLabel = new GTextField();
            _topbarLabel.SetXY(14, 10);
            _topbarLabel.SetSize(root.width - 120, 24);
            _topbarLabel.textFormat = new TextFormat { color = Theme.TextPrim, size = 14, align = AlignType.Left };
            _topbarLabel.AddRelation(root, RelationType.Width);
            top.AddChild(_topbarLabel);

            var logoutBtn = Theme.GhostButton("回山门", () => _app.Router.Show<LoginScreen>(), 88, 30);
            logoutBtn.SetXY(root.width - 100, 6);
            logoutBtn.AddRelation(root, RelationType.Right_Right);
            var gateIcon = Theme.Image(Theme.Art.IconGate, 22, 22);
            if (gateIcon != null)
            {
                gateIcon.SetXY(4, 4);
                logoutBtn.AddChild(gateIcon);
            }
            top.AddChild(logoutBtn);
            root.AddChild(top);

            // Bottom-left log panel
            _logPanel = new GComponent();
            _logPanel.SetSize(360, 160);
            _logPanel.SetXY(16, root.height - 176);
            _logPanel.AddRelation(root, RelationType.Bottom_Bottom);
            var lbg = new GGraph(); lbg.DrawRect(360, 160, 1, new Color(0.94f, 0.87f, 0.66f, 0.40f), new Color(0.08f, 0.12f, 0.12f, 0.72f));
            _logPanel.AddChild(lbg);
            _logText = new GTextField();
            _logText.SetXY(8, 6);
            _logText.SetSize(344, 148);
            _logText.singleLine = false;
            _logText.textFormat = new TextFormat { color = Theme.TextDim, size = 11, align = AlignType.Left };
            _logPanel.AddChild(_logText);
            root.AddChild(_logPanel);

            // Bottom-right skill button
            var skill = Theme.PrimaryButton("御符小术", OnReleaseSkill, 158, 60);
            skill.SetXY(root.width - 166, root.height - 76);
            skill.AddRelation(root, RelationType.Right_Right);
            skill.AddRelation(root, RelationType.Bottom_Bottom);
            var talismanIcon = Theme.Image(Theme.Art.IconTalisman, 46, 46);
            if (talismanIcon != null)
            {
                talismanIcon.SetXY(9, 7);
                skill.AddChild(talismanIcon);
            }
            root.AddChild(skill);

            return root;
        }

        private GComponent BuildFromPackage(AppBootstrap app)
        {
            var root = Theme.TryCreateFromPackage(Theme.UiId.HudRoot);
            if (root == null) return null;

            _topbarLabel = Theme.Find<GTextField>(root, Theme.UiId.HudTopLabel);
            _logText = Theme.Find<GTextField>(root, Theme.UiId.HudLogText);

            var btnLogout = Theme.Find<GButton>(root, Theme.UiId.HudLogoutBtn);
            var btnSkill = Theme.Find<GButton>(root, Theme.UiId.HudSkillBtn);

            if (_topbarLabel == null || _logText == null || btnLogout == null || btnSkill == null)
            {
                root.Dispose();
                return null;
            }

            btnLogout.onClick.Add(_ => _app.Router.Show<LoginScreen>());
            btnSkill.onClick.Add(_ => OnReleaseSkill());
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
                ? new[] { "云游小道", "灵符学徒", "青锋少侠" }[_app.Session.RoleArchetypeIndex] : "?";
            _topbarLabel.text = $"{_app.Session.RoleNickname}（{archetype}）  ·  灵域: {(z != null ? z.name : "?")}  ·  仙籍: {_app.GameClient.PlayerId}";
        }

        private void AppendLog(string msg)
        {
            _logBuffer.Enqueue(msg);
            while (_logBuffer.Count > 60) _logBuffer.Dequeue();
            _logText.text = string.Join("\n", _logBuffer);
        }

        private void OnReleaseSkill()
        {
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
            UpdateTopBar();
        }
    }
}
