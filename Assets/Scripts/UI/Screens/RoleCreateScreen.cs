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
        private static readonly string[] Archetypes = { "云游小道", "灵符学徒", "青锋少侠" };
        private static readonly string[] Weapons    = { "桃木小剑", "朱砂灵符", "练习木剑" };
        private static readonly Color[]  Tints      =
        {
            new Color(0.36f, 0.74f, 0.67f),
            new Color(0.63f, 0.56f, 0.82f),
            new Color(0.78f, 0.48f, 0.34f),
        };

        private AppBootstrap _app;
        private GComponent _card;
        private GGraph _previewSwatch;
        private GImage _previewPortrait;
        private GTextField _previewLabel;
        private GTextInput _nickField;
        private GTextField _statusLabel;
        private GComponent _enterBtn;
        private bool _busy;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;
            var packagedRoot = BuildFromPackage(app);
            if (packagedRoot != null)
                return packagedRoot;

            var root = new GComponent();
            root.SetSize(GRoot.inst.width, GRoot.inst.height);

            const float CW = 780, CH = 560;
            _card = Theme.Card(CW, CH);
            _card.SetXY((root.width - CW) * 0.5f, (root.height - CH) * 0.5f);
            _card.AddRelation(root, RelationType.Center_Center);
            root.AddChild(_card);

            float x = 24, y = 20;
            var h1 = Theme.H1("道心初定"); h1.SetXY(x, y); _card.AddChild(h1); y += 44;
            var z = _app.Session.SelectedZone;
            var sub = Theme.P(z != null ? $"即将踏入: #{z.zone_id} {z.name}" : "尚未择域", dim: false);
            sub.SetXY(x, y); _card.AddChild(sub); y += 32;

            // archetype buttons row
            float btnW = (CW - x * 2 - 16) / 3f;
            for (int i = 0; i < Archetypes.Length; i++)
            {
                int idx = i;
                var btn = Theme.GhostButton(Archetypes[i], () => SetArchetype(idx), btnW, 48);
                btn.SetXY(x + i * (btnW + 8), y);
                _card.AddChild(btn);
            }
            y += 66;

            // preview block
            _previewSwatch = new GGraph();
            _previewSwatch.SetXY(x, y);
            _previewSwatch.SetSize(104, 104);
            _previewSwatch.DrawEllipse(104, 104, Tints[0]);
            _card.AddChild(_previewSwatch);

            _previewPortrait = Theme.Image(Theme.Art.RoleWanderer, 104, 104);
            if (_previewPortrait != null)
            {
                _previewPortrait.SetXY(x, y);
                _card.AddChild(_previewPortrait);
            }

            _previewLabel = Theme.P("", dim: false);
            _previewLabel.SetXY(x + 124, y + 40);
            _previewLabel.SetSize(CW - x - 144, 28);
            _previewLabel.textFormat = new TextFormat { color = Theme.TextPrim, size = 16 };
            _card.AddChild(_previewLabel);
            y += 118;

            var (nickRow, nickField) = Theme.LabeledInput("角色名", _app.Session.RoleNickname, CW - x * 2);
            _nickField = nickField;
            nickRow.SetXY(x, y); _card.AddChild(nickRow); y += 46;

            _enterBtn = Theme.PrimaryButton("结缘入世", OnEnterPressed, 160, 40);
            _enterBtn.SetXY(x, y); _card.AddChild(_enterBtn);
            var backBtn = Theme.GhostButton("返择域", () => _app.Router.Show<ServerSelectScreen>(), 120, 40);
            backBtn.SetXY(x + 172, y); _card.AddChild(backBtn);
            y += 48;

            _statusLabel = Theme.P(""); _statusLabel.SetXY(x, y); _card.AddChild(_statusLabel);

            ApplyPreview();
            return root;
        }

        private GComponent BuildFromPackage(AppBootstrap app)
        {
            var root = Theme.TryCreateFromPackage(Theme.UiId.RoleRoot);
            if (root == null) return null;

            _nickField = Theme.Find<GTextInput>(root, Theme.UiId.RoleNickInput);
            _statusLabel = Theme.Find<GTextField>(root, Theme.UiId.RoleStatus);
            _previewLabel = Theme.Find<GTextField>(root, Theme.UiId.RolePreviewText);
            _previewSwatch = Theme.Find<GGraph>(root, Theme.UiId.RolePreviewSwatch);
            _enterBtn = Theme.Find<GComponent>(root, Theme.UiId.RoleEnterBtn);

            var btnBack = Theme.Find<GButton>(root, Theme.UiId.RoleBackBtn);
            var btnClass1 = Theme.Find<GButton>(root, Theme.UiId.RoleClassBtn1);
            var btnClass2 = Theme.Find<GButton>(root, Theme.UiId.RoleClassBtn2);
            var btnClass3 = Theme.Find<GButton>(root, Theme.UiId.RoleClassBtn3);

            if (_nickField == null || _statusLabel == null || _previewLabel == null || _enterBtn == null || btnBack == null)
            {
                root.Dispose();
                return null;
            }

            _nickField.text = app.Session.RoleNickname;
            if (_enterBtn is GButton enterButton)
                enterButton.onClick.Add(_ => OnEnterPressed());
            else
                _enterBtn.onClick.Add(_ => OnEnterPressed());

            btnBack.onClick.Add(_ => _app.Router.Show<ServerSelectScreen>());
            btnClass1?.onClick.Add(_ => SetArchetype(0));
            btnClass2?.onClick.Add(_ => SetArchetype(1));
            btnClass3?.onClick.Add(_ => SetArchetype(2));
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
            _previewSwatch?.DrawEllipse(104, 104, Tints[i]);
            if (_previewPortrait != null)
                Theme.SetImageTexture(_previewPortrait, i == 0 ? Theme.Art.RoleWanderer : i == 1 ? Theme.Art.RoleTalisman : Theme.Art.RoleSword);
            if (_previewLabel != null)
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
