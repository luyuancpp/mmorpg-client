using UnityEngine;
using FairyGUI;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Centralised colours / spacing / helper builders for code-built FairyGUI
    /// components. When the art team ships .fui packages this file becomes
    /// pure colour/font constants used by skin scripts.
    /// </summary>
    public static class Theme
    {
        public const string UiPackageName = "qdao";
        public const string UiPackagePath = "UI/qdao/qdao";

        public static class Art
        {
            public const string Backdrop = "UI/qdao/qdao_backdrop";
            public const string LoginBanner = "UI/qdao/qdao_login_banner";
            public const string ServerScroll = "UI/qdao/qdao_server_scroll";
            public const string RoleWanderer = "UI/qdao/qdao_role_wanderer";
            public const string RoleTalisman = "UI/qdao/qdao_role_talisman";
            public const string RoleSword = "UI/qdao/qdao_role_sword";
            public const string IconTalisman = "UI/qdao/qdao_icon_talisman";
            public const string IconGate = "UI/qdao/qdao_icon_gate";
            public const string CloudCorner = "UI/qdao/qdao_cloud_corner";
        }

        public static class UiId
        {
            public const string LoginRoot = "LoginScreen";
            public const string LoginGatewayInput = "inputGateway";
            public const string LoginAccountInput = "inputAccount";
            public const string LoginPasswordInput = "inputPassword";
            public const string LoginAnnouncementList = "listAnnouncement";
            public const string LoginStatus = "txtStatus";
            public const string LoginEnterBtn = "btnEnter";
            public const string LoginRefreshBtn = "btnRefresh";

            public const string ServerRoot = "ServerSelectScreen";
            public const string ServerList = "listServer";
            public const string ServerStatus = "txtStatus";
            public const string ServerConfirmBtn = "btnConfirm";
            public const string ServerRefreshBtn = "btnRefresh";
            public const string ServerBackBtn = "btnBack";

            public const string RoleRoot = "RoleCreateScreen";
            public const string RoleNickInput = "inputNick";
            public const string RoleStatus = "txtStatus";
            public const string RolePreviewText = "txtPreview";
            public const string RolePreviewSwatch = "previewSwatch";
            public const string RoleEnterBtn = "btnEnter";
            public const string RoleBackBtn = "btnBack";
            public const string RoleClassBtn1 = "btnClass1";
            public const string RoleClassBtn2 = "btnClass2";
            public const string RoleClassBtn3 = "btnClass3";

            public const string HudRoot = "HudScreen";
            public const string HudTopLabel = "txtTop";
            public const string HudLogText = "txtLog";
            public const string HudLogoutBtn = "btnLogout";
            public const string HudSkillBtn = "btnSkill";
        }

        public static readonly Color BgTop       = new Color(0.17f, 0.31f, 0.28f);
        public static readonly Color BgBottom    = new Color(0.85f, 0.79f, 0.66f);
        public static readonly Color Panel       = new Color(0.17f, 0.24f, 0.20f, 0.90f);
        public static readonly Color PanelEdge   = new Color(0.90f, 0.82f, 0.62f, 0.92f);
        public static readonly Color PanelInner  = new Color(0.11f, 0.16f, 0.14f, 0.86f);
        public static readonly Color TextPrim    = new Color(0.97f, 0.93f, 0.83f);
        public static readonly Color TextDim     = new Color(0.72f, 0.79f, 0.72f);
        public static readonly Color TextWarn    = new Color(0.96f, 0.50f, 0.42f);
        public static readonly Color BtnPrim     = new Color(0.27f, 0.63f, 0.47f);
        public static readonly Color BtnPrimLine = new Color(0.88f, 0.95f, 0.89f, 0.56f);
        public static readonly Color BtnGhost    = new Color(0.22f, 0.30f, 0.27f, 0.92f);
        public static readonly Color ZoneIdle    = new Color(0.19f, 0.24f, 0.25f, 0.95f);
        public static readonly Color ZoneSel     = new Color(0.30f, 0.44f, 0.38f, 0.98f);
        public static readonly Color Accent      = new Color(0.95f, 0.82f, 0.52f);
        public static readonly Color AccentSoft  = new Color(0.93f, 0.88f, 0.72f, 0.34f);

        public static GComponent TryCreateFromPackage(string componentName)
        {
            if (UIPackage.GetByName(UiPackageName) == null) return null;
            return UIPackage.CreateObject(UiPackageName, componentName) as GComponent;
        }

        public static T Find<T>(GComponent root, string childName) where T : GObject
        {
            if (root == null || string.IsNullOrEmpty(childName)) return null;
            return root.GetChild(childName) as T;
        }

        public static GImage Image(string resourcePath, float w, float h)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null) return null;

            var img = new GImage();
            img.texture = new NTexture(tex);
            img.SetSize(w, h);
            img.touchable = false;
            return img;
        }

        public static void SetImageTexture(GImage img, string resourcePath)
        {
            if (img == null) return;
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex != null)
                img.texture = new NTexture(tex);
        }

        // ── Builders (placeholder skin until .fui packages are authored) ──

        /// <summary>Card panel: solid fill + 1px gold edge, no .fui asset required.</summary>
        public static GComponent Card(float w, float h)
        {
            var c = new GComponent();
            c.SetSize(w, h);
            c.opaque = true;

            var shadow = new GGraph();
            shadow.SetXY(6, 7);
            shadow.DrawRect(w, h, 0, Color.clear, new Color(0.02f, 0.03f, 0.03f, 0.36f));
            c.AddChild(shadow);

            var bg = new GGraph();
            bg.DrawRect(w, h, 1, PanelEdge, Panel);
            c.AddChild(bg);

            var inner = new GGraph();
            inner.SetXY(6, 6);
            inner.DrawRect(w - 12, h - 12, 1, new Color(1f, 1f, 1f, 0.08f), PanelInner);
            c.AddChild(inner);

            var corner = Image(Art.CloudCorner, 84, 84);
            if (corner != null)
            {
                corner.SetXY(w - 92, h - 88);
                corner.alpha = 0.55f;
                c.AddChild(corner);
            }

            var titleBar = new GGraph();
            titleBar.SetXY(16, 12);
            titleBar.DrawRect(w - 32, 16, 0, Color.clear, AccentSoft);
            c.AddChild(titleBar);
            return c;
        }

        public static GTextField H1(string text)
        {
            var t = new GTextField();
            t.text = text;
            t.textFormat = new TextFormat { color = Accent, size = 30, bold = true, align = AlignType.Left };
            t.autoSize = AutoSizeType.Both;
            return t;
        }

        public static GTextField H2(string text)
        {
            var t = new GTextField();
            t.text = text;
            t.textFormat = new TextFormat { color = TextPrim, size = 19, bold = true, align = AlignType.Left };
            t.autoSize = AutoSizeType.Both;
            return t;
        }

        public static GTextField P(string text, bool dim = true)
        {
            var t = new GTextField();
            t.text = text;
            t.textFormat = new TextFormat
            {
                color = dim ? TextDim : TextPrim,
                size  = 14,
                align = AlignType.Left,
            };
            t.autoSize = AutoSizeType.Both;
            t.singleLine = false;
            return t;
        }

        public static GComponent FlatButton(string text, float w, float h, Color baseCol, Color textCol, System.Action onClick)
        {
            var btn = new GComponent();
            btn.SetSize(w, h);
            btn.touchable = true;
            btn.opaque = true;

            var bg = new GGraph();
            bg.DrawRect(w, h, 1, BtnPrimLine, baseCol);
            btn.AddChild(bg);

            var shine = new GGraph();
            shine.SetXY(2, 2);
            shine.DrawRect(w - 4, Mathf.Max(8, h * 0.44f), 0, Color.clear, new Color(1f, 1f, 1f, 0.08f));
            btn.AddChild(shine);

            var label = new GTextField();
            label.SetSize(w, h);
            label.text = text;
            label.textFormat = new TextFormat
            {
                color = textCol, size = 15, bold = true,
                align = AlignType.Center,
            };
            label.verticalAlign = VertAlignType.Middle;
            btn.AddChild(label);

            var hover = new Color(Mathf.Min(1, baseCol.r * 1.18f), Mathf.Min(1, baseCol.g * 1.18f), Mathf.Min(1, baseCol.b * 1.18f), baseCol.a);
            btn.onRollOver.Add(() => bg.DrawRect(w, h, 1, BtnPrimLine, hover));
            btn.onRollOut.Add(() => bg.DrawRect(w, h, 1, BtnPrimLine, baseCol));
            btn.onClick.Add(_ => onClick?.Invoke());
            return btn;
        }

        public static GComponent PrimaryButton(string text, System.Action onClick, float w = 140, float h = 36)
            => FlatButton(text, w, h, BtnPrim, Color.white, onClick);

        public static GComponent GhostButton(string text, System.Action onClick, float w = 110, float h = 32)
            => FlatButton(text, w, h, BtnGhost, TextPrim, onClick);

        public static (GComponent row, GTextInput field) LabeledInput(string label, string value, float rowW, bool isPassword = false)
        {
            var row = new GComponent();
            row.SetSize(rowW, 34);

            var l = new GTextField();
            l.SetXY(0, 7);
            l.SetSize(110, 24);
            l.text = "「" + label + "」";
            l.textFormat = new TextFormat { color = TextDim, size = 13, align = AlignType.Left, bold = true };
            row.AddChild(l);

            var bg = new GGraph();
            bg.SetXY(112, 1);
            bg.DrawRect(rowW - 112, 32, 1, new Color(1f, 1f, 1f, 0.24f), new Color(0.06f, 0.10f, 0.09f, 0.50f));
            row.AddChild(bg);

            var input = new GTextInput();
            input.SetXY(120, 5);
            input.SetSize(rowW - 132, 24);
            input.text = value;
            input.displayAsPassword = isPassword;
            input.textFormat = new TextFormat { color = TextPrim, size = 14, align = AlignType.Left };
            row.AddChild(input);

            return (row, input);
        }
    }
}
