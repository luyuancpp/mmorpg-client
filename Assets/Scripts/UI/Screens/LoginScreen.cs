using System.Collections;
using FairyGUI;
using MmorpgClient.UI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// First screen: announcements + account/password form. No login request
    /// fires here - clicking 进入选服 hands off to <see cref="ServerSelectScreen"/>.
    /// </summary>
    public sealed class LoginScreen : IScreen
    {
        private AppBootstrap _app;
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

            _announceList = new GComponent();
            _announceList.SetSize(root.width, root.height);
            root.AddChild(_announceList);

            _gatewayField = AddInput(root, Text("login.gateway", "服务器"), app.Session.GatewayBaseUrl, 656, 190, 250, 54);
            _accountField = AddInput(root, Text("login.account", "账号"), app.Session.Account, 960, 190, 250, 54);
            _passwordField = AddInput(root, Text("login.password", "密码"), app.Session.Password, 1265, 190, 250, 54, true);

            AddHitButton(root, 1490, 872, 170, 50, () => _app.Run(LoadAnnouncements()));
            AddHitButton(root, 1722, 866, 154, 65, OnEnterServerSelect);

            _statusLabel = Theme.ArtText("", Theme.TextDim, 18, false, AlignType.Center);
            Theme.SetArtRect(_statusLabel, root.width, root.height, 1120, 926, 420, 26);
            root.AddChild(_statusLabel);

            return root;
        }

        private GTextInput AddInput(GComponent root, string labelText, string value, float x, float y, float w, float h, bool password = false)
        {
            var input = new GTextInput();
            input.text = value;
            input.displayAsPassword = password;
            input.textFormat = new TextFormat { font = Theme.BodyFontName, color = Theme.TextPrim, size = 14, bold = false, align = AlignType.Left };
            Theme.SetArtRect(input, root.width, root.height, x + 62, y + 8, w - 84, h - 16);
            root.AddChild(input);
            return input;
        }

        private static GComponent AddHitButton(GComponent root, float x, float y, float w, float h, System.Action onClick)
        {
            var btn = new GComponent();
            btn.touchable = true;
            Theme.SetArtRect(btn, root.width, root.height, x, y, w, h);
            btn.onClick.Add(_ => onClick?.Invoke());
            root.AddChild(btn);
            return btn;
        }

        private GComponent BuildFromPackage(AppBootstrap app)
        {
            var root = Theme.TryCreateFromPackage(Theme.UiId.LoginRoot);
            if (root == null)
                return null;

            // ScreenRouter sizes us to the 2560x1080 host. Do NOT bind size to
            // GRoot — that would re-stretch the design canvas to window pixels.
            Theme.SetImageTexture(Theme.Find<GImage>(root, Theme.UiId.SceneBackdrop), Theme.Art.SceneBackdrop);

            _announceList = ReplacePackagedDynamicLayer(root, Theme.UiId.LoginAnnouncementList);
            _gatewayField = Theme.Find<GTextInput>(root, Theme.UiId.LoginGatewayInput);
            _accountField = Theme.Find<GTextInput>(root, Theme.UiId.LoginAccountInput);
            _passwordField = Theme.Find<GTextInput>(root, Theme.UiId.LoginPasswordInput);
            _statusLabel = Theme.Find<GTextField>(root, Theme.UiId.LoginStatus);

            var btnEnter = Theme.Find<GButton>(root, Theme.UiId.LoginEnterBtn);
            var btnRefresh = Theme.Find<GButton>(root, Theme.UiId.LoginRefreshBtn);

            if (_announceList == null || _gatewayField == null || _accountField == null || _passwordField == null || _statusLabel == null || btnEnter == null || btnRefresh == null)
            {
                Debug.LogWarning("[LoginScreen] FairyGUI LoginScreen component is missing required children; fallback to code UI.");
                root.Dispose();
                return null;
            }

            _gatewayField.text = app.Session.GatewayBaseUrl;
            _accountField.text = app.Session.Account;
            _passwordField.text = app.Session.Password;
            _gatewayField.textFormat = new TextFormat { font = Theme.BodyFontName, color = Theme.TextPrim, size = 14, align = AlignType.Left };
            _accountField.textFormat = _gatewayField.textFormat;
            _passwordField.textFormat = _gatewayField.textFormat;

            btnEnter.onClick.Add(_ => OnEnterServerSelect());
            btnRefresh.onClick.Add(_ => _app.Run(LoadAnnouncements()));
            return root;
        }

        private static GComponent ReplacePackagedDynamicLayer(GComponent root, string childName)
        {
            var oldLayer = root.GetChild(childName);
            var index = oldLayer != null ? root.GetChildIndex(oldLayer) : root.numChildren;
            if (oldLayer != null)
                root.RemoveChild(oldLayer, true);

            var layer = new GComponent { name = childName };
            layer.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);
            layer.touchable = false;
            root.AddChildAt(layer, index);
            return layer;
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
            _statusLabel.text = Text("login.loadingAnnouncements", "正在获取公告...");
            yield return _app.Gateway.GetAnnouncements(
                resp =>
                {
                    _app.Session.Announcements.Clear();
                    if (resp?.items != null) _app.Session.Announcements.AddRange(resp.items);
                    Rebuild();
                    _statusLabel.text = _app.Session.Announcements.Count > 0
                        ? string.Format(Text("login.announcementCount", "公告 {0} 条"), _app.Session.Announcements.Count)
                        : string.Empty;
                },
                err =>
                {
                    _statusLabel.text = Text("login.announcementError", "公告暂未开放");
                });
            _loading = false;
        }

        private void Rebuild()
        {
            _announceList.RemoveChildren(0, -1, true);
            if (_app.Session.Announcements.Count == 0)
            {
                return;
            }
            var positions = new (float x, float y)[] { (780, 420), (1280, 420), (780, 540), (1280, 540), (780, 660), (1280, 660) };
            for (int i = 0; i < _app.Session.Announcements.Count && i < positions.Length; ++i)
            {
                var a = _app.Session.Announcements[i];
                var pos = positions[i];

                var head = new GTextField();
                head.text = $"[{a.type}] {a.title}";
                head.textFormat = new TextFormat { font = Theme.BodyFontName, color = Theme.Accent, size = 14, bold = true };
                Theme.SetArtRect(head, _announceList.width, _announceList.height, pos.x, pos.y, 330, 28);
                _announceList.AddChild(head);

                var body = Theme.P(a.content ?? "");
                body.textFormat = new TextFormat { font = Theme.BodyFontName, color = Theme.TextDim, size = 13, align = AlignType.Left };
                body.singleLine = false;
                Theme.SetArtRect(body, _announceList.width, _announceList.height, pos.x, pos.y + 30, 310, 42);
                _announceList.AddChild(body);
            }
        }

        private static string Text(string key, string fallback) => QdaoUiText.Get(key, fallback);
    }
}
