using System.Collections;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Cosmetic role preset picker + nickname. proto CreatePlayerRequest is
    /// empty so the choice is purely client-side flavour.
    /// </summary>
    public sealed class RoleCreateScreen : IScreen
    {
        private static readonly string[] Archetypes = { "游侠", "灵符师", "剑客" };
        private static readonly string[] Weapons    = { "青锋短剑", "玉简灵符", "古意长剑" };
        private static readonly Color[]  Tints      =
        {
            new Color(0.36f, 0.74f, 0.67f),
            new Color(0.63f, 0.56f, 0.82f),
            new Color(0.78f, 0.48f, 0.34f),
        };

        private AppBootstrap _app;
        private GComponent _card;
        private GGraph _previewSwatch;
        private GTextField _previewLabel;
        private GTextInput _nickField;
        private GTextField _statusLabel;
        private GComponent _enterBtn;
        private bool _busy;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;
            var root = new GComponent();
            root.SetSize(GRoot.inst.width, GRoot.inst.height);

            const float CW = 760, CH = 540;
            _card = Theme.Card(CW, CH);
            _card.SetXY((root.width - CW) * 0.5f, (root.height - CH) * 0.5f);
            _card.AddRelation(root, RelationType.Center_Center);
            root.AddChild(_card);

            float x = 22, y = 18;
            var h1 = Theme.H1("选择门派"); h1.SetXY(x, y); _card.AddChild(h1); y += 38;
            var z = _app.Session.SelectedZone;
            var sub = Theme.P(z != null ? $"将进入: #{z.zone_id} {z.name}" : "未选择区服");
            sub.SetXY(x, y); _card.AddChild(sub); y += 28;

            // archetype buttons row
            float btnW = (CW - x * 2 - 16) / 3f;
            for (int i = 0; i < Archetypes.Length; i++)
            {
                int idx = i;
                var btn = Theme.GhostButton(Archetypes[i], () => SetArchetype(idx), btnW, 44);
                btn.SetXY(x + i * (btnW + 8), y);
                _card.AddChild(btn);
            }
            y += 60;

            // preview block
            _previewSwatch = new GGraph();
            _previewSwatch.SetXY(x, y);
            _previewSwatch.SetSize(96, 96);
            _previewSwatch.DrawEllipse(96, 96, Tints[0]);
            _card.AddChild(_previewSwatch);

            _previewLabel = Theme.P("", dim: false);
            _previewLabel.SetXY(x + 110, y + 36);
            _previewLabel.SetSize(CW - x - 130, 24);
            _previewLabel.textFormat = new TextFormat { color = Theme.TextPrim, size = 16 };
            _card.AddChild(_previewLabel);
            y += 110;

            var (nickRow, nickField) = Theme.LabeledInput("角色名", _app.Session.RoleNickname, CW - x * 2);
            _nickField = nickField;
            nickRow.SetXY(x, y); _card.AddChild(nickRow); y += 40;

            _enterBtn = Theme.PrimaryButton("登录并进入", OnEnterPressed, 150);
            _enterBtn.SetXY(x, y); _card.AddChild(_enterBtn);
            var backBtn = Theme.GhostButton("返回", () => _app.Router.Show<ServerSelectScreen>());
            backBtn.SetXY(x + 160, y); _card.AddChild(backBtn);
            y += 42;

            _statusLabel = Theme.P(""); _statusLabel.SetXY(x, y); _card.AddChild(_statusLabel);

            ApplyPreview();
            return root;
        }

        public void OnEnter() { ApplyPreview(); }
        public void OnExit() { }
        public void Tick(float dt) { }

        private void SetArchetype(int idx) { _app.Session.RoleArchetypeIndex = idx; ApplyPreview(); }

        private void ApplyPreview()
        {
            int i = Mathf.Clamp(_app.Session.RoleArchetypeIndex, 0, Archetypes.Length - 1);
            _previewSwatch.DrawEllipse(96, 96, Tints[i]);
            _previewLabel.text = $"{Archetypes[i]} · 武器: {Weapons[i]}";
        }

        private void OnEnterPressed()
        {
            if (_busy) return;
            _app.Session.RoleNickname = _nickField.text;
            var z = _app.Session.SelectedZone;
            if (z == null) { _statusLabel.text = "未选择区服"; return; }
            _busy = true;
            _enterBtn.touchable = false;
            _enterBtn.alpha = 0.5f;
            _statusLabel.text = "登录中...";
            _app.Run(DoLogin(z.zone_id));
        }

        private IEnumerator DoLogin(uint zoneId)
        {
            yield return _app.GameClient.LoginAndEnterGame(
                zoneId, _app.Session.Account, _app.Session.Password,
                onSuccess: () =>
                {
                    _statusLabel.text = "登录成功，进入世界";
                    _app.Router.Show<HudScreen>();
                },
                onError: e =>
                {
                    _statusLabel.text = "登录失败: " + e;
                    _busy = false;
                    _enterBtn.touchable = true;
                    _enterBtn.alpha = 1f;
                });
        }
    }
}
