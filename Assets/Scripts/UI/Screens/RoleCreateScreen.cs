using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Cosmetic role preset picker + nickname. proto CreatePlayerRequest is
    /// empty so the choice is purely client-side flavour; the actual
    /// LoginAndEnterGame call fires when the user clicks "登录并进入".
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
        private VisualElement _previewSwatch;
        private Label _previewLabel;
        private TextField _nickField;
        private Label _statusLabel;
        private Button _enterBtn;
        private bool _busy;

        public VisualElement Build(AppBootstrap app)
        {
            _app = app;
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var card = new VisualElement();
            card.style.width = 760;
            card.style.maxWidth = new Length(92, LengthUnit.Percent);
            Theme.StylePanel(card);
            root.Add(card);

            card.Add(Theme.H1("选择门派"));
            var z = _app.Session.SelectedZone;
            card.Add(Theme.P(z != null ? $"将进入: #{z.zone_id} {z.name}" : "未选择区服"));

            // preset row
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 12;
            for (int i = 0; i < Archetypes.Length; i++)
            {
                int idx = i;
                var btn = Theme.GhostButton(Archetypes[i], () => SetArchetype(idx));
                btn.style.flexGrow = 1;
                btn.style.height = 44;
                row.Add(btn);
            }
            card.Add(row);

            // preview block
            var preview = new VisualElement();
            preview.style.flexDirection = FlexDirection.Row;
            preview.style.marginTop = 16;
            preview.style.alignItems = Align.Center;
            _previewSwatch = new VisualElement();
            _previewSwatch.style.width = 96;
            _previewSwatch.style.height = 96;
            _previewSwatch.style.borderTopLeftRadius = _previewSwatch.style.borderTopRightRadius =
                _previewSwatch.style.borderBottomLeftRadius = _previewSwatch.style.borderBottomRightRadius = 48;
            _previewSwatch.style.marginRight = 16;
            _previewLabel = Theme.P("", dim: false);
            _previewLabel.style.fontSize = 16;
            preview.Add(_previewSwatch);
            preview.Add(_previewLabel);
            card.Add(preview);

            _nickField = Theme.LabeledField("角色名", _app.Session.RoleNickname);
            _nickField.style.marginTop = 14;
            card.Add(_nickField);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 16;
            _enterBtn = Theme.PrimaryButton("登录并进入", OnEnterPressed);
            btnRow.Add(_enterBtn);
            btnRow.Add(Theme.GhostButton("返回", () => _app.Router.Show<ServerSelectScreen>()));
            card.Add(btnRow);

            _statusLabel = Theme.P("");
            _statusLabel.style.marginTop = 8;
            card.Add(_statusLabel);

            ApplyPreview();
            return root;
        }

        public void OnEnter() { ApplyPreview(); }
        public void OnExit() { }
        public void Tick(float dt) { }

        private void SetArchetype(int idx)
        {
            _app.Session.RoleArchetypeIndex = idx;
            ApplyPreview();
        }

        private void ApplyPreview()
        {
            int i = Mathf.Clamp(_app.Session.RoleArchetypeIndex, 0, Archetypes.Length - 1);
            _previewSwatch.style.backgroundColor = Tints[i];
            _previewLabel.text = $"{Archetypes[i]} · 武器: {Weapons[i]}";
        }

        private void OnEnterPressed()
        {
            if (_busy) return;
            _app.Session.RoleNickname = _nickField.value;
            var z = _app.Session.SelectedZone;
            if (z == null)
            {
                _statusLabel.text = "未选择区服";
                _statusLabel.style.color = Theme.TextWarn;
                return;
            }
            _busy = true;
            _enterBtn.SetEnabled(false);
            _statusLabel.style.color = Theme.TextDim;
            _statusLabel.text = "登录中...";
            _app.Run(DoLogin(z.zone_id));
        }

        private IEnumerator DoLogin(uint zoneId)
        {
            // Re-create GameClient with possibly-edited gateway URL.
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
                    _statusLabel.style.color = Theme.TextWarn;
                    _busy = false;
                    _enterBtn.SetEnabled(true);
                });
        }
    }
}
