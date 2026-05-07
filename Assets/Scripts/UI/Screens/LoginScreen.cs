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
            var root = new GComponent();
            root.SetSize(GRoot.inst.width, GRoot.inst.height);

            const float CW = 720, CH = 600;
            _card = Theme.Card(CW, CH);
            _card.SetXY((root.width - CW) * 0.5f, (root.height - CH) * 0.5f);
            _card.AddRelation(root, RelationType.Center_Center);
            root.AddChild(_card);

            float x = 22, y = 18;
            var h1 = Theme.H1("云岚纪行");
            h1.SetXY(x, y);
            _card.AddChild(h1);
            y += 38;

            var sub = Theme.P("Mmorpg Client · 正式登录界面 (FairyGUI)");
            sub.SetXY(x, y);
            _card.AddChild(sub);
            y += 24;

            var h2 = Theme.H2("江湖快讯");
            h2.SetXY(x, y);
            _card.AddChild(h2);
            y += 28;

            // announcement list container with simple clipping
            _announceList = new GComponent();
            _announceList.SetXY(x, y);
            _announceList.SetSize(CW - x * 2, 200);
            _announceList.opaque = false;
            _card.AddChild(_announceList);
            y += 210;

            var h3 = Theme.H2("账号");
            h3.SetXY(x, y);
            _card.AddChild(h3);
            y += 28;

            (var gwRow, _gatewayField)  = Theme.LabeledInput("Gateway",  app.Session.GatewayBaseUrl, CW - x * 2);
            gwRow.SetXY(x, y); _card.AddChild(gwRow); y += 34;
            (var acRow, _accountField)  = Theme.LabeledInput("Account",  app.Session.Account,        CW - x * 2);
            acRow.SetXY(x, y); _card.AddChild(acRow); y += 34;
            (var pwRow, _passwordField) = Theme.LabeledInput("Password", app.Session.Password,       CW - x * 2, isPassword: true);
            pwRow.SetXY(x, y); _card.AddChild(pwRow); y += 40;

            var btnEnter   = Theme.PrimaryButton("进入选服", OnEnterServerSelect, 130);
            btnEnter.SetXY(x, y);
            _card.AddChild(btnEnter);
            var btnRefresh = Theme.GhostButton("刷新公告", () => _app.Run(LoadAnnouncements()));
            btnRefresh.SetXY(x + 140, y);
            _card.AddChild(btnRefresh);
            y += 42;

            _statusLabel = Theme.P("");
            _statusLabel.SetXY(x, y);
            _card.AddChild(_statusLabel);

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
            _statusLabel.text = "正在拉取公告...";
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
                    _statusLabel.text = "公告拉取失败: " + err;
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
                head.ApplyFormat();
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
