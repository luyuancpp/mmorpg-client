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

        /// <summary>Item URLs published from the qdao FairyGUI package.</summary>
        public static class ItemUrl
        {
            public const string TabItem      = "ui://qdao2026/QdaoTabItem";
            public const string ServerRow    = "ui://qdao2026/QdaoServerRow";
            public const string AnnounceItem = "ui://qdao2026/QdaoAnnounceItem";
            public const string IconGate     = "ui://qdao2026/qdao_icon_gate";
            public const string IconTalisman = "ui://qdao2026/qdao_icon_talisman";

            // Fallback: by-id URLs in case the publish output stripped names.
            // ids come from package.xml in client/fairygui/qdao/assets/qdao/.
            public const string TabItemById      = "ui://qdao2026a000000m";
            public const string ServerRowById    = "ui://qdao2026a000000l";
            public const string AnnounceItemById = "ui://qdao2026a000000n";
        }

        public static class Art
        {
            public const float ReferenceWidth = 2560f;
            public const float ReferenceHeight = 1080f;
            public const string SceneBackdrop = "UI/qdao/qdao_scene_backdrop";
            public const string LoginBanner = "UI/qdao/qdao_login_banner";
            public const string ServerScroll = "UI/qdao/qdao_server_scroll";
            public const string ServerRow = "UI/qdao/qdao_server_row";
            public const string ServerRowAlt = "UI/qdao/qdao_server_row_alt";
            public const string SearchBox = "UI/qdao/qdao_search_box";
            public const string PrimaryButton = "UI/qdao/qdao_primary_button";
            public const string BottomBar = "UI/qdao/qdao_bottom_bar";
            public const string RoleWanderer = "UI/qdao/qdao_role_wanderer";
            public const string IconTalisman = "UI/qdao/qdao_icon_talisman";
            public const string IconGate = "UI/qdao/qdao_icon_gate";
            public const string CloudCorner = "UI/qdao/qdao_cloud_corner";
        }

        public static class UiId
        {
            public const string SceneBackdrop = "imgSceneBackdrop";
            public const string LoginRoot = "LoginScreen";
            public const string LoginGatewayInput = "inputGateway";
            public const string LoginAccountInput = "inputAccount";
            public const string LoginPasswordInput = "inputPassword";
            public const string LoginAnnouncementList = "listAnnouncement";
            public const string LoginStatus = "txtStatus";
            public const string LoginEnterBtn = "btnEnter";
            public const string LoginRefreshBtn = "btnRefresh";

            // Per-row child names inside the announcement list item.
            // QdaoTabItem (current placeholder) only has `title`; richer
            // QdaoAnnounceItem (when added) also exposes `subtitle`.
            public const string AnnounceItemTitle    = "title";
            public const string AnnounceItemSubtitle = "subtitle";

            public const string ServerRoot = "ServerSelectScreen";
            public const string ServerList = "listServer";
            public const string ServerTabList = "listTabs";
            public const string ServerSearchInput = "inputSearch";
            public const string ServerStatus = "txtStatus";
            public const string ServerConfirmBtn = "btnConfirm";
            public const string ServerRefreshBtn = "btnRefresh";
            public const string ServerBackBtn = "btnBack";

            // Per-row child names inside QdaoServerRow (see common/QdaoServerRow.xml)
            public const string ServerRowTitle    = "title";
            public const string ServerRowIcon     = "icon";
            public const string ServerRowSubtitle = "subtitle";
            public const string ServerRowBadge    = "rightLabel";
            public const string ServerRowDotCtrl  = "dot";
            public const string ServerRowCheckCtrl = "checked";

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

            public const string SceneRoot = "SceneScreen";
            public const string SceneList = "listRows";
            public const string SceneTabList = "listTabs";
            public const string SceneStatus = "txtStatus";
            public const string SceneSearchInput = "inputSearch";
            public const string SceneTitle = "txtPanelTitle";
            public const string SceneTabRecent = "tabRecent";
            public const string SceneTabRecommend = "tabRecommend";
            public const string SceneTabAll = "tabAll";
            public const string SceneScrollUpBtn = "btnScrollUp";
            public const string SceneScrollDownBtn = "btnScrollDown";
            public const string SceneBackBtn = "btnBack";
            public const string SceneRefreshBtn = "btnRefresh";
            public const string SceneConfirmBtn = "btnConfirm";
        }

        public static readonly Color BgTop = new Color(0.35f, 0.55f, 0.52f);
        public static readonly Color BgBottom = new Color(0.89f, 0.80f, 0.58f);
        public static readonly Color Panel = new Color(0.95f, 0.86f, 0.68f, 0.76f);
        public static readonly Color PanelEdge = new Color(0.12f, 0.42f, 0.37f, 0.96f);
        public static readonly Color PanelInner = new Color(1.00f, 0.95f, 0.84f, 0.64f);
        public static readonly Color TextPrim = new Color(0.16f, 0.25f, 0.22f);
        public static readonly Color TextDim = new Color(0.45f, 0.42f, 0.34f);
        public static readonly Color TextWarn    = new Color(0.96f, 0.50f, 0.42f);
        public static readonly Color BtnPrim = new Color(0.10f, 0.50f, 0.44f);
        public static readonly Color BtnPrimLine = new Color(0.99f, 0.94f, 0.72f, 0.76f);
        public static readonly Color BtnGhost = new Color(0.97f, 0.90f, 0.75f, 0.74f);
        public static readonly Color ZoneIdle = new Color(1.00f, 0.94f, 0.83f, 0.84f);
        public static readonly Color ZoneSel = new Color(0.10f, 0.52f, 0.46f, 0.92f);
        public static readonly Color Accent = new Color(0.78f, 0.61f, 0.28f);
        public static readonly Color AccentSoft = new Color(1.00f, 0.96f, 0.72f, 0.45f);

        public const string BodyFontName = "Microsoft YaHei UI,Microsoft YaHei,SimHei";
        public const string TitleFontName = "STKaiti,KaiTi,STXingkai,Microsoft YaHei UI,Microsoft YaHei";
        public const string QdaoFontName = BodyFontName;

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

        public static int FillList(GList list, string itemUrl, int count, ListItemRenderer renderer = null)
        {
            return FillList(list, itemUrl, null, count, renderer);
        }

        /// <summary>
        /// Populate a non-virtual GList. Tries <paramref name="itemUrl"/> (by
        /// name) first; if that resource is not registered in the loaded
        /// package, falls back to <paramref name="fallbackUrl"/> (by id).
        /// Pre-checks UIPackage so we never pass null to GList.AddChild
        /// (which would NRE inside FairyGUI). Returns actual added count.
        /// </summary>
        public static int FillList(GList list, string itemUrl, string fallbackUrl,
                                   int count, ListItemRenderer renderer = null)
        {
            if (list == null) return 0;
            list.RemoveChildrenToPool();
            if (count <= 0) return 0;

            // Pre-flight: verify the URL resolves; try fallback if not.
            string resolvedUrl = itemUrl;
            var probe = !string.IsNullOrEmpty(itemUrl) ? UIPackage.CreateObjectFromURL(itemUrl) : null;
            if (probe == null && !string.IsNullOrEmpty(fallbackUrl))
            {
                probe = UIPackage.CreateObjectFromURL(fallbackUrl);
                if (probe != null)
                {
                    Debug.LogWarning($"[Theme.FillList] by-name URL '{itemUrl}' missing, " +
                                     $"using fallback by-id URL '{fallbackUrl}'.");
                    resolvedUrl = fallbackUrl;
                }
            }
            if (probe == null)
            {
                Debug.LogError($"[Theme.FillList] item URL not found in any loaded package: " +
                               $"name='{itemUrl}' id='{fallbackUrl ?? "(none)"}'. " +
                               $"Republish the qdao FairyGUI package and confirm the component is exported.");
                return 0;
            }

            list.AddChild(probe);
            int added = 1;
            for (int i = 1; i < count; i++)
            {
                var item = UIPackage.CreateObjectFromURL(resolvedUrl);
                if (item == null) break;
                list.AddChild(item);
                added++;
            }

            if (renderer != null)
            {
                list.itemRenderer = renderer;
                for (int i = 0; i < added; i++)
                    renderer(i, list.GetChildAt(i));
            }
            return added;
        }

        public static GImage Image(string resourcePath, float w, float h)
        {
            var tex = Resources.Load<Texture2D>(resourcePath);
            if (tex == null)
            {
                Debug.LogWarning($"[Theme] UI texture not found: Resources/{resourcePath}");
                return null;
            }

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

        public static GComponent ImagePanel(string resourcePath, float w, float h, bool touchable = false, System.Action onClick = null)
        {
            var panel = new GComponent();
            panel.SetSize(w, h);
            panel.touchable = touchable;
            panel.opaque = false;

            var bg = Image(resourcePath, w, h);
            if (bg != null)
                panel.AddChild(bg);

            if (touchable)
                panel.onClick.Add(_ => onClick?.Invoke());
            return panel;
        }

        public static void SetArtRect(GObject obj, float rootW, float rootH, float x, float y, float w, float h)
        {
            float scale = Mathf.Max(rootW / Art.ReferenceWidth, rootH / Art.ReferenceHeight);
            float offsetX = (rootW - Art.ReferenceWidth * scale) * 0.5f;
            float offsetY = (rootH - Art.ReferenceHeight * scale) * 0.5f;
            obj.SetXY(offsetX + x * scale, offsetY + y * scale);
            obj.SetSize(w * scale, h * scale);
        }

        public static GTextField ArtText(string text, Color color, int size, bool bold = false, AlignType align = AlignType.Left)
        {
            var label = new GTextField();
            label.text = text;
            label.textFormat = new TextFormat
            {
                font = BodyFontName,
                color = color,
                size = size,
                bold = bold,
                align = align,
                shadowOffset = new Vector2(0f, 1f),
                shadowColor = new Color(1f, 0.96f, 0.82f, 0.28f)
            };
            label.verticalAlign = VertAlignType.Middle;
            return label;
        }

        public static GTextField TitleText(string text, Color color, int size, AlignType align = AlignType.Center)
        {
            var label = new GTextField();
            label.text = text;
            label.textFormat = new TextFormat
            {
                font = TitleFontName,
                color = color,
                size = size,
                bold = false,
                align = align,
                shadowOffset = new Vector2(0f, 1f),
                shadowColor = new Color(0.08f, 0.12f, 0.10f, 0.18f)
            };
            label.verticalAlign = VertAlignType.Middle;
            return label;
        }

        // ── Builders (placeholder skin until .fui packages are authored) ──

        /// <summary>Card panel: solid fill + 1px gold edge, no .fui asset required.</summary>
        public static GComponent Card(float w, float h)
        {
            var c = new GComponent();
            c.SetSize(w, h);
            c.opaque = false;

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

            var top = Image(Art.ServerScroll, Mathf.Min(w - 40, 680), 88);
            if (top != null)
            {
                top.SetXY((w - top.width) * 0.5f, 4);
                top.alpha = 0.92f;
                c.AddChild(top);
            }

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
            t.textFormat = new TextFormat { font = TitleFontName, color = Accent, size = 30, bold = false, align = AlignType.Left };
            t.autoSize = AutoSizeType.Both;
            return t;
        }

        public static GTextField H2(string text)
        {
            var t = new GTextField();
            t.text = text;
            t.textFormat = new TextFormat { font = BodyFontName, color = TextPrim, size = 19, bold = true, align = AlignType.Left };
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
                font = QdaoFontName,
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
            btn.opaque = false;

            var bgImage = Image(Art.PrimaryButton, w, h);
            if (bgImage != null)
                btn.AddChild(bgImage);
            else
            {
                var bg = new GGraph();
                bg.DrawRect(w, h, 1, BtnPrimLine, baseCol);
                btn.AddChild(bg);
            }

            var shine = new GGraph();
            shine.SetXY(2, 2);
            shine.DrawRect(w - 4, Mathf.Max(8, h * 0.44f), 0, Color.clear, new Color(1f, 1f, 1f, 0.08f));
            btn.AddChild(shine);

            var label = new GTextField();
            label.SetSize(w, h);
            label.text = text;
            label.textFormat = new TextFormat
            {
                font = QdaoFontName,
                color = textCol, size = 15, bold = true,
                align = AlignType.Center,
            };
            label.verticalAlign = VertAlignType.Middle;
            btn.AddChild(label);

            var hover = new Color(Mathf.Min(1, baseCol.r * 1.18f), Mathf.Min(1, baseCol.g * 1.18f), Mathf.Min(1, baseCol.b * 1.18f), baseCol.a);
            btn.onRollOver.Add(() => btn.alpha = 0.88f);
            btn.onRollOut.Add(() => btn.alpha = 1f);
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
            l.textFormat = new TextFormat { font = QdaoFontName, color = TextDim, size = 13, align = AlignType.Left, bold = true };
            row.AddChild(l);

            var bg = new GGraph();
            bg.SetXY(112, 1);
            bg.DrawRect(rowW - 112, 32, 1, new Color(0.72f, 0.53f, 0.28f, 0.42f), new Color(1f, 0.96f, 0.86f, 0.72f));
            row.AddChild(bg);

            var art = Image(Art.SearchBox, rowW - 112, 32);
            if (art != null)
            {
                art.SetXY(112, 1);
                art.alpha = 0.82f;
                row.AddChild(art);
            }

            var input = new GTextInput();
            input.SetXY(120, 5);
            input.SetSize(rowW - 132, 24);
            input.text = value;
            input.displayAsPassword = isPassword;
            input.textFormat = new TextFormat { font = QdaoFontName, color = TextPrim, size = 14, align = AlignType.Left };
            row.AddChild(input);

            return (row, input);
        }
    }
}
