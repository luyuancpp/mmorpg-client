using System;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Login screen — gateway URL + account + password, plus the announcement
    /// list and a primary "登录" CTA that routes to ServerSelectScreen.
    ///
    /// Layout contract with LoginScreen.xml:
    ///   txtPanelTitle, inputGatewayBox, inputAccountBox, inputPasswordBox,
    ///   listAnnouncement, btnRefresh, btnEnter, txtStatus.
    /// Each input box is a QdaoSearchBox containing a child named "input"
    /// (GTextInput).
    ///
    /// Auth wiring is intentionally minimal: this screen seeds Session
    /// fields and pushes ServerSelectScreen. The actual /api/server-list +
    /// /api/assign-gate calls live in GatewayHttpClient and are kicked off
    /// from ServerSelectScreen + the confirm flow — keeps the login screen
    /// reusable for offline/code-only fallback when the gateway is down.
    /// </summary>
    public sealed class LoginScreen : IScreen
    {
        private AppBootstrap _app;
        private GComponent _root;
        private GTextInput _inputGateway;
        private GTextInput _inputAccount;
        private GTextInput _inputPassword;
        private GList _listAnnouncement;
        private GTextField _txtStatus;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            _root = Theme.TryCreateFromPackage("LoginScreen");
            if (_root == null)
            {
                _root = BuildBlank();
                return _root;
            }

            _inputGateway     = FindInput("inputGatewayBox");
            _inputAccount     = FindInput("inputAccountBox");
            _inputPassword    = FindInput("inputPasswordBox");
            _listAnnouncement = Theme.Find<GList>(_root, "listAnnouncement");
            _txtStatus        = Theme.Find<GTextField>(_root, "txtStatus");

            if (_root.GetChild("txtPanelTitle") is GTextField title)
                title.text = "登录";

            SeedFromSession();
            FillAnnouncements();
            BindButtons();
            SetStatus("");

            return _root;
        }

        public void OnEnter() { }
        public void OnExit()  { CommitInputsToSession(); }
        public void Tick(float dt) { }

        // ── Population ──────────────────────────────────────────────

        /// <summary>
        /// QdaoSearchBox is a composite component whose actual editable
        /// field is a child named "input". GetChild always returns the
        /// composite root, so we go one level deeper.
        /// </summary>
        private GTextInput FindInput(string boxName)
        {
            var box = _root.GetChild(boxName) as GComponent;
            return box?.GetChild("input") as GTextInput;
        }

        private void SeedFromSession()
        {
            if (_app?.Session == null) return;
            if (_inputGateway  != null) _inputGateway.text  = _app.Session.GatewayBaseUrl ?? "";
            if (_inputAccount  != null) _inputAccount.text  = _app.Session.Account ?? "";
            if (_inputPassword != null) _inputPassword.text = _app.Session.Password ?? "";
        }

        private void CommitInputsToSession()
        {
            if (_app?.Session == null) return;
            if (_inputGateway  != null && !string.IsNullOrWhiteSpace(_inputGateway.text))
                _app.Session.GatewayBaseUrl = _inputGateway.text.Trim();
            if (_inputAccount  != null && !string.IsNullOrWhiteSpace(_inputAccount.text))
                _app.Session.Account = _inputAccount.text.Trim();
            if (_inputPassword != null && !string.IsNullOrWhiteSpace(_inputPassword.text))
                _app.Session.Password = _inputPassword.text;
        }

        private void FillAnnouncements()
        {
            if (_listAnnouncement == null) return;

            // Static placeholder announcements until /api/announcements is
            // wired through GatewayHttpClient. Keep these strings short —
            // the FUI list is 760×140 with line gap 8, ~3 visible rows.
            string[] lines =
            {
                "开服福利:登录即送 7 日体验包",
                "新手引导上线,完成可领紫装一件",
                "本周维护时间:周三 03:00 - 05:00",
            };

            _listAnnouncement.RemoveChildrenToPool();
            for (int i = 0; i < lines.Length; i++)
            {
                // Reuse QdaoServerCard as a plain row container — it has
                // title + subtitle + badge slots that we partially fill.
                var row = UIPackage.CreateObjectFromURL("ui://qdao/QdaoServerCard") as GComponent;
                if (row == null) continue;
                _listAnnouncement.AddChild(row);
                if (row.GetChild("title")    is GTextField t) t.text = $"公告 {i + 1}";
                if (row.GetChild("subtitle") is GTextField s) s.text = lines[i];
                // Hide the status dot — announcements don't have a state.
                var statusCtrl = row.GetController("status");
                if (statusCtrl != null) statusCtrl.selectedIndex = 2;
            }
            _listAnnouncement.selectionMode = ListSelectionMode.None;
        }

        // ── Buttons ─────────────────────────────────────────────────

        private void BindButtons()
        {
            BindIfPresent("btnEnter",   OnLogin);
            BindIfPresent("btnRefresh", OnRefreshAnnouncements);
        }

        private void BindIfPresent(string childName, Action handler)
        {
            var go = _root.GetChild(childName);
            if (go == null) return;
            go.onClick.Set(_ => handler?.Invoke());
        }

        private void OnLogin()
        {
            CommitInputsToSession();

            if (_app?.Session == null)
            {
                SetStatus("会话未初始化");
                return;
            }
            if (string.IsNullOrWhiteSpace(_app.Session.Account))
            {
                SetStatus("请输入账号");
                return;
            }
            if (string.IsNullOrWhiteSpace(_app.Session.GatewayBaseUrl))
            {
                SetStatus("请输入网关地址");
                return;
            }

            SetStatus("正在进入服务器选择…");
            Debug.Log($"[LoginScreen] login: account={_app.Session.Account}, gw={_app.Session.GatewayBaseUrl}");
            _app.Router.Show<ServerSelectScreen>();
        }

        private void OnRefreshAnnouncements()
        {
            Debug.Log("[LoginScreen] refresh announcements");
            FillAnnouncements();
            SetStatus("公告已刷新");
        }

        private void SetStatus(string text)
        {
            if (_txtStatus != null) _txtStatus.text = text ?? string.Empty;
        }

        // ── Fallback (FUI package missing) ──────────────────────────

        private GComponent BuildBlank()
        {
            var c = new GComponent();
            c.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);

            var bg = new GGraph();
            bg.SetSize(c.width, c.height);
            bg.DrawRect(c.width, c.height, 0, Color.clear, new Color(0.08f, 0.10f, 0.10f));
            c.AddChild(bg);

            var label = new GTextField();
            label.SetSize(c.width, 80);
            label.SetXY(0, c.height * 0.5f - 80);
            label.text = "LoginScreen — qdao package missing";
            label.textFormat = new TextFormat
            {
                font = Theme.BodyFontName, color = new Color(0.6f, 0.6f, 0.6f),
                size = 40, align = AlignType.Center,
            };
            label.verticalAlign = VertAlignType.Middle;
            c.AddChild(label);

            // Click anywhere to advance — keeps the demo flow usable even
            // when the qdao package fails to load (e.g. in a fresh clone
            // before the user runs F8 publish).
            var hit = new GGraph();
            hit.SetSize(c.width, c.height);
            hit.DrawRect(c.width, c.height, 0, Color.clear, Color.clear);
            c.AddChild(hit);
            hit.onClick.Set(_ => _app?.Router.Show<ServerSelectScreen>());

            return c;
        }
    }
}
