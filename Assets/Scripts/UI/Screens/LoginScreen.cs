using System.Collections;
using FairyGUI;
using MmorpgClient.UI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// First screen: announcements + account/password form. No login request
    /// fires here - clicking 进入 hands off to <see cref="ServerSelectScreen"/>.
    ///
    /// All visuals come from the qdao FairyGUI package. We bind named children
    /// and use the announcement <see cref="GList"/>'s itemRenderer to fill rows
    /// from session data (no hardcoded grid).
    /// </summary>
    public sealed class LoginScreen : IScreen
    {
        private AppBootstrap _app;
        private GTextInput _gatewayField, _accountField, _passwordField;
        private GList _announceList;
        private GTextField _statusLabel;
        private bool _loading;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            var packagedRoot = BuildFromPackage(app);
            if (packagedRoot != null)
                return packagedRoot;

            // Fallback: package missing -> minimal placeholder so we don't crash.
            var root = new GComponent();
            root.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);
            var warn = Theme.ArtText("LoginScreen package missing — open qdao.fairy and republish.",
                                     Theme.TextWarn, 28, true, AlignType.Center);
            warn.SetSize(root.width, root.height);
            warn.verticalAlign = VertAlignType.Middle;
            root.AddChild(warn);
            return root;
        }

        private GComponent BuildFromPackage(AppBootstrap app)
        {
            var root = Theme.TryCreateFromPackage(Theme.UiId.LoginRoot);
            if (root == null) return null;

            _announceList  = Theme.Find<GList>(root,        Theme.UiId.LoginAnnouncementList);
            _gatewayField  = Theme.Find<GTextInput>(root,   Theme.UiId.LoginGatewayInput);
            _accountField  = Theme.Find<GTextInput>(root,   Theme.UiId.LoginAccountInput);
            _passwordField = Theme.Find<GTextInput>(root,   Theme.UiId.LoginPasswordInput);
            _statusLabel   = Theme.Find<GTextField>(root,   Theme.UiId.LoginStatus);

            var btnEnter   = Theme.Find<GButton>(root,      Theme.UiId.LoginEnterBtn);
            var btnRefresh = Theme.Find<GButton>(root,      Theme.UiId.LoginRefreshBtn);

            if (_announceList == null || _gatewayField == null || _accountField == null
                || _passwordField == null || _statusLabel == null || btnEnter == null || btnRefresh == null)
            {
                Debug.LogWarning("[LoginScreen] FairyGUI LoginScreen is missing required children; check qdao package.");
                root.Dispose();
                return null;
            }

            // Inputs
            _gatewayField.text  = app.Session.GatewayBaseUrl;
            _accountField.text  = app.Session.Account;
            _passwordField.text = app.Session.Password;
            var inputFmt = new TextFormat
            {
                font = Theme.BodyFontName, color = Theme.TextPrim, size = 22, align = AlignType.Left
            };
            _gatewayField.textFormat  = inputFmt;
            _accountField.textFormat  = inputFmt;
            _passwordField.textFormat = inputFmt;

            // Announcement list - we'll fill it data-driven in Rebind()
            _announceList.RemoveChildrenToPool();
            _announceList.itemRenderer = RenderAnnounceItem;

            btnEnter.onClick.Add(_ => OnEnterServerSelect());
            btnRefresh.onClick.Add(_ => _app.Run(LoadAnnouncements()));
            return root;
        }

        public void OnEnter()
        {
            // Pull announcements once per app session; subsequent revisits just rebind.
            if (_app.Session.Announcements.Count == 0 && !_loading)
                _app.Run(LoadAnnouncements());
            else
                Rebind();
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
                    Rebind();
                    _statusLabel.text = _app.Session.Announcements.Count > 0
                        ? string.Format(Text("login.announcementCount", "公告 {0} 条"),
                                        _app.Session.Announcements.Count)
                        : string.Empty;
                },
                err =>
                {
                    Rebind();
                    _statusLabel.text = Text("login.announcementError", "公告暂未开放");
                });
            _loading = false;
        }

        private void Rebind()
        {
            if (_announceList == null) return;
            Theme.FillList(_announceList, Theme.ItemUrl.AnnounceItem, Theme.ItemUrl.AnnounceItemById,
                           _app.Session.Announcements.Count, RenderAnnounceItem);
        }

        // Per-row binder. FairyGUI GList virtualization will call this for every
        // visible item index whenever scroll / numItems changes.
        private void RenderAnnounceItem(int index, GObject item)
        {
            if (item is not GComponent row) return;
            if (index < 0 || index >= _app.Session.Announcements.Count) return;

            var a = _app.Session.Announcements[index];
            var title    = row.GetChild(Theme.UiId.AnnounceItemTitle)    as GTextField;
            var subtitle = row.GetChild(Theme.UiId.AnnounceItemSubtitle) as GTextField;
            var badge    = row.GetChild(Theme.UiId.ServerRowBadge)       as GTextField;

            if (title != null)    title.text = string.IsNullOrEmpty(a.title) ? "" : a.title;
            if (subtitle != null) subtitle.text = a.content ?? string.Empty;
            if (badge != null)    badge.text = string.IsNullOrEmpty(a.type) ? "" : a.type;
        }

        private static string Text(string key, string fallback) => QdaoUiText.Get(key, fallback);
    }
}
