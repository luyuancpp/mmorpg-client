using System.Collections;
using FairyGUI;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// First screen: announcements + account/password form. No login request
    /// fires here - clicking 进入选服 hands off to <see cref="ServerSelectScreen"/>.
    /// </summary>
    public sealed class LoginScreen : IScreen
    {
        private AppBootstrap _app;
        private GComponent _card;
        private GTextInput _gatewayField, _accountField, _passwordField;
        private GComponent _announceList;
        private GTextField _statusLabel;
        private bool _loading;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;
            var packagedRoot = BuildFromPackage(app);
            if (packagedRoot != null)
                return packagedRoot;

            var root = new GComponent();
            root.SetSize(GRoot.inst.width, GRoot.inst.height);

            const float CW = 760, CH = 620;
            _card = Theme.Card(CW, CH);
            _card.SetXY((root.width - CW) * 0.5f, (root.height - CH) * 0.5f);
            _card.AddRelation(root, RelationType.Center_Center);
            root.AddChild(_card);

            var banner = Theme.Image(Theme.Art.LoginBanner, 312, 156);
            if (banner != null)
            {
                banner.SetXY(CW - 348, 22);
                _card.AddChild(banner);
            }

            float x = 26, y = 20;
            var h1 = Theme.H1("Q版本道友");
            h1.SetXY(x, y);
            _card.AddChild(h1);
            y += 44;

            var sub = Theme.P("国风回合 · 正式启程 · 温暖江湖", dim: false);
            sub.SetXY(x, y);
            _card.AddChild(sub);
            y += 28;

            var h2 = Theme.H2("公告");
            h2.SetXY(x, y);
            _card.AddChild(h2);
            y += 34;

            // announcement list container with simple clipping
            _announceList = new GComponent();
            _announceList.SetXY(x, y);
            _announceList.SetSize(CW - x * 2, 214);
            _announceList.opaque = false;
            _card.AddChild(_announceList);
            y += 224;

            var h3 = Theme.H2("账号登录");
            h3.SetXY(x, y);
            _card.AddChild(h3);
            y += 34;

            var (gwRow, gwField)  = Theme.LabeledInput("Gateway",  app.Session.GatewayBaseUrl, CW - x * 2);
            _gatewayField = gwField;
            gwRow.SetXY(x, y); _card.AddChild(gwRow); y += 38;
            var (acRow, acField)  = Theme.LabeledInput("Account",  app.Session.Account,        CW - x * 2);
            _accountField = acField;
            acRow.SetXY(x, y); _card.AddChild(acRow); y += 38;
            var (pwRow, pwField) = Theme.LabeledInput("Password", app.Session.Password,       CW - x * 2, isPassword: true);
            _passwordField = pwField;
            pwRow.SetXY(x, y); _card.AddChild(pwRow); y += 44;

            var btnEnter   = Theme.PrimaryButton("进入选服", OnEnterServerSelect, 140, 40);
            btnEnter.SetXY(x, y);
            _card.AddChild(btnEnter);
            var btnRefresh = Theme.GhostButton("刷新公告", () => _app.Run(LoadAnnouncements()), 124, 40);
            btnRefresh.SetXY(x + 152, y);
            _card.AddChild(btnRefresh);
            y += 48;

            _statusLabel = Theme.P("");
            _statusLabel.SetXY(x, y);
            _card.AddChild(_statusLabel);

            return root;
        }

        private GComponent BuildFromPackage(AppBootstrap app)
        {
            var root = Theme.TryCreateFromPackage(Theme.UiId.LoginRoot);
            if (root == null) return null;

            _announceList = Theme.Find<GComponent>(root, Theme.UiId.LoginAnnouncementList);
            _gatewayField = Theme.Find<GTextInput>(root, Theme.UiId.LoginGatewayInput);
            _accountField = Theme.Find<GTextInput>(root, Theme.UiId.LoginAccountInput);
            _passwordField = Theme.Find<GTextInput>(root, Theme.UiId.LoginPasswordInput);
            _statusLabel = Theme.Find<GTextField>(root, Theme.UiId.LoginStatus);

            var btnEnter = Theme.Find<GButton>(root, Theme.UiId.LoginEnterBtn);
            var btnRefresh = Theme.Find<GButton>(root, Theme.UiId.LoginRefreshBtn);

            if (_announceList == null || _gatewayField == null || _accountField == null || _passwordField == null || _statusLabel == null || btnEnter == null || btnRefresh == null)
            {
                root.Dispose();
                return null;
            }

            _gatewayField.text = app.Session.GatewayBaseUrl;
            _accountField.text = app.Session.Account;
            _passwordField.text = app.Session.Password;

            btnEnter.onClick.Add(_ => OnEnterServerSelect());
            btnRefresh.onClick.Add(_ => _app.Run(LoadAnnouncements()));
            return root;
        }

        public void OnEnter()
        {
            if (_app.Session.Announcements.Count == 0 && !_loading)
                _app.Run(LoadAnnouncements());
            else
                Rebuild();
        }

        public void OnExit() { }
        public void Tick(float dt) { }

        private void OnEnterServerSelect()
        {
            _app.Session.GatewayBaseUrl = _gatewayField.text;
            _app.Session.Account  = _accountField.text;
            _app.Session.Password = _passwordField.text;
            _app.Router.Show<ServerSelectScreen>();
        }

        private IEnumerator LoadAnnouncements()
        {
            _loading = true;
            _statusLabel.text = "正在获取公告...";
            yield return _app.Gateway.GetAnnouncements(
                resp =>
                {
                    _app.Session.Announcements.Clear();
                    if (resp?.items != null) _app.Session.Announcements.AddRange(resp.items);
                    Rebuild();
                    _statusLabel.text = $"公告 {_app.Session.Announcements.Count} 条";
                },
                err =>
                {
                    _statusLabel.text = "公告获取失败: " + err;
                });
            _loading = false;
        }

        private void Rebuild()
        {
            _announceList.RemoveChildren(0, -1, true);
            float y = 0;
            if (_app.Session.Announcements.Count == 0)
            {
                var empty = Theme.P("暂无公告");
                empty.SetXY(4, 0);
                _announceList.AddChild(empty);
                return;
            }
            foreach (var a in _app.Session.Announcements)
            {
                var head = new GTextField();
                head.text = $"[{a.type}] {a.title}";
                head.textFormat = new TextFormat { color = Theme.Accent, size = 14, bold = true };
                head.autoSize = AutoSizeType.Both;
                head.SetXY(4, y);
                _announceList.AddChild(head);
                y += 20;

                var body = Theme.P(a.content ?? "");
                body.SetSize(_announceList.width - 8, 40);
                body.singleLine = false;
                body.SetXY(4, y);
                _announceList.AddChild(body);
                y += 40;
                if (y > _announceList.height - 20) break;
            }
        }
    }
}
