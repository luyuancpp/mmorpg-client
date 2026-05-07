using System.Collections;
using MmorpgClient.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// First screen: shows announcements + account/password form. No login
    /// request is fired here - clicking "进入选服" hands off to ServerSelectScreen
    /// which assigns the gate based on the chosen zone.
    /// </summary>
    public sealed class LoginScreen : IScreen
    {
        private AppBootstrap _app;
        private TextField _accountField;
        private TextField _passwordField;
        private TextField _gatewayField;
        private ScrollView _announceList;
        private Label _statusLabel;
        private bool _loading;

        public VisualElement Build(AppBootstrap app)
        {
            _app = app;
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var card = new VisualElement();
            card.style.width = new Length(720, LengthUnit.Pixel);
            card.style.maxWidth = new Length(92, LengthUnit.Percent);
            Theme.StylePanel(card);
            root.Add(card);

            card.Add(Theme.H1("云岚纪行"));
            card.Add(Theme.P("Mmorpg Client · 正式登录界面 (UI Toolkit)"));

            card.Add(Theme.H2("江湖快讯"));
            _announceList = new ScrollView { mode = ScrollViewMode.Vertical };
            _announceList.style.height = 180;
            _announceList.style.marginBottom = 8;
            card.Add(_announceList);

            card.Add(Theme.H2("账号"));
            _gatewayField  = Theme.LabeledField("Gateway", _app.Session.GatewayBaseUrl);
            _accountField  = Theme.LabeledField("Account", _app.Session.Account);
            _passwordField = Theme.LabeledField("Password", _app.Session.Password, isPassword: true);
            card.Add(_gatewayField);
            card.Add(_accountField);
            card.Add(_passwordField);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 12;
            btnRow.Add(Theme.PrimaryButton("进入选服", OnEnterServerSelect));
            btnRow.Add(Theme.GhostButton("刷新公告", () => _app.Run(LoadAnnouncements())));
            card.Add(btnRow);

            _statusLabel = Theme.P("", dim: true);
            _statusLabel.style.marginTop = 8;
            card.Add(_statusLabel);

            return root;
        }

        public void OnEnter()
        {
            if (_app.Session.Announcements.Count == 0 && !_loading)
                _app.Run(LoadAnnouncements());
            else
                RebuildAnnouncementList();
        }

        public void OnExit() { }
        public void Tick(float dt) { }

        private void OnEnterServerSelect()
        {
            _app.Session.GatewayBaseUrl = _gatewayField.value;
            _app.Session.Account  = _accountField.value;
            _app.Session.Password = _passwordField.value;
            _app.Router.Show<ServerSelectScreen>();
        }

        private IEnumerator LoadAnnouncements()
        {
            _loading = true;
            _statusLabel.text = "正在拉取公告...";
            _statusLabel.style.color = Theme.TextDim;

            yield return _app.Gateway.GetAnnouncements(
                resp =>
                {
                    _app.Session.Announcements.Clear();
                    if (resp?.items != null) _app.Session.Announcements.AddRange(resp.items);
                    RebuildAnnouncementList();
                    _statusLabel.text = $"公告 {_app.Session.Announcements.Count} 条";
                },
                err =>
                {
                    _statusLabel.text = "公告拉取失败: " + err;
                    _statusLabel.style.color = Theme.TextWarn;
                });
            _loading = false;
        }

        private void RebuildAnnouncementList()
        {
            _announceList.Clear();
            if (_app.Session.Announcements.Count == 0)
            {
                _announceList.Add(Theme.P("暂无公告"));
                return;
            }
            foreach (var a in _app.Session.Announcements)
            {
                var row = new VisualElement();
                row.style.marginBottom = 6;
                row.style.paddingTop = row.style.paddingBottom = 4;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = new Color(1, 1, 1, 0.05f);
                var head = new Label($"[{a.type}] {a.title}");
                head.style.color = Theme.Accent;
                head.style.unityFontStyleAndWeight = FontStyle.Bold;
                row.Add(head);
                var body = Theme.P(a.content ?? "");
                body.style.whiteSpace = WhiteSpace.Normal;
                row.Add(body);
                _announceList.Add(row);
            }
        }
    }
}
