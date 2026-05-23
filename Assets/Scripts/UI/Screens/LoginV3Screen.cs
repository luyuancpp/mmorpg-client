using System;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// V3 login screen.
    ///
    /// Layout contract with assets/qdao/LoginV3.xml:
    ///   banner (V3Banner)                    — header with title text
    ///   ornTL, ornTR        (GImage)          — corner ornaments
    ///   inputGateway / inputAccount / inputPassword (V3Input)
    ///                                         — each has child "edit" (GTextInput)
    ///   btnEnter            (V3Btn)           — primary green CTA
    ///   status              (GTextField)      — bottom feedback line
    /// </summary>
    public sealed class LoginV3Screen : IScreen
    {
        private const string PkgComponent = "LoginV3";

        private AppBootstrap _app;
        private GComponent   _root;
        private GTextInput   _editGateway;
        private GTextInput   _editAccount;
        private GTextInput   _editPassword;
        private GTextField   _status;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            _root = Theme.TryCreateFromPackage(PkgComponent);
            if (_root == null) { _root = BuildMissingPackagePlaceholder(); return _root; }

            _editGateway  = FindEdit("inputGateway");
            _editAccount  = FindEdit("inputAccount");
            _editPassword = FindEdit("inputPassword");
            _status       = Theme.Find<GTextField>(_root, "status");

            SeedInputs();
            BindIfPresent("btnEnter", OnEnterClicked);
            SetStatus("");

            return _root;
        }

        public void OnEnter() { }
        public void OnExit()  { CommitInputs(); }
        public void Tick(float _) { }

        // ── helpers ───────────────────────────────────────────────────

        /// <summary>
        /// V3Input is a composite; the actual GTextInput is its "edit"
        /// child (defined in common/V3Input.xml). We never bind to the
        /// composite itself.
        /// </summary>
        private GTextInput FindEdit(string boxName)
        {
            var box = _root.GetChild(boxName) as GComponent;
            return box?.GetChild("edit") as GTextInput;
        }

        private void SeedInputs()
        {
            var s = _app?.Session; if (s == null) return;
            if (_editGateway  != null) _editGateway.text  = s.GatewayBaseUrl ?? "";
            if (_editAccount  != null) _editAccount.text  = s.Account        ?? "";
            if (_editPassword != null) _editPassword.text = s.Password       ?? "";
        }

        private void CommitInputs()
        {
            var s = _app?.Session; if (s == null) return;
            if (_editGateway  != null && !string.IsNullOrWhiteSpace(_editGateway.text))
                s.GatewayBaseUrl = _editGateway.text.Trim();
            if (_editAccount  != null && !string.IsNullOrWhiteSpace(_editAccount.text))
                s.Account = _editAccount.text.Trim();
            if (_editPassword != null && !string.IsNullOrWhiteSpace(_editPassword.text))
                s.Password = _editPassword.text;
        }

        private void BindIfPresent(string childName, Action handler)
        {
            var go = _root.GetChild(childName);
            if (go == null) return;
            go.onClick.Set(_ => handler?.Invoke());
        }

        private void OnEnterClicked()
        {
            CommitInputs();
            if (_app?.Session == null) { SetStatus("会话未初始化"); return; }
            if (string.IsNullOrWhiteSpace(_app.Session.Account))        { SetStatus("请输入账号"); return; }
            if (string.IsNullOrWhiteSpace(_app.Session.GatewayBaseUrl)) { SetStatus("请输入网关地址"); return; }

            Debug.Log($"[LoginV3] enter: account={_app.Session.Account} gw={_app.Session.GatewayBaseUrl}");
            SetStatus("正在进入选服…");
            _app.Router.Show<ServersV3Screen>();
        }

        private void SetStatus(string text)
        {
            if (_status != null) _status.text = text ?? string.Empty;
        }

        private GComponent BuildMissingPackagePlaceholder()
        {
            var c = new GComponent();
            c.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);
            var t = new GTextField { text = "qdao package missing — open client/fairygui/qdao/qdao.fairy and F8 publish." };
            t.SetSize(c.width, 80);
            t.SetXY(0, c.height * 0.5f - 40);
            t.textFormat = new TextFormat { font = Theme.BodyFontName, size = 32, align = AlignType.Center, color = new Color(0.85f, 0.7f, 0.4f) };
            c.AddChild(t);
            return c;
        }
    }
}
