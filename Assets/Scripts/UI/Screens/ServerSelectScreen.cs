using System;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Server selection screen.
    ///
    /// Layout contract with ServerSelectScreen.xml (see invariants comment
    /// in that file): listServer is a fixed 2x4 pagination grid; selecting
    /// a card flips its checked controller, then btnConfirm enters.
    ///
    /// Why this code looks the way it does:
    ///  - All highlight state is driven by FairyGUI controllers (`checked`
    ///    on QdaoServerCard, default selection on QdaoTabIdle). No
    ///    hand-rolled "remember last index, paint old white, paint new red"
    ///    logic — every previous regression we hit on this screen ("tab
    ///    not clear", "selection sticky", "two cards red at once") was a
    ///    symptom of duplicating controller state in C#.
    ///  - Tab clicks don't drive anything else here. listTabs.selectedIndex
    ///    is the source of truth; we just log it. Filtering by tab is a
    ///    future step that needs real Session.Zones data anyway.
    ///  - Card buttons (btnEnter/btnDetail) were removed from
    ///    QdaoServerCard.xml; entry is now only via the bottom-bar
    ///    btnConfirm. Don't reintroduce per-card click handlers — the
    ///    whole card IS the hit area now.
    /// </summary>
    public sealed class ServerSelectScreen : IScreen
    {
        // Inline tab data — keep in sync with ServerSelectScreen.xml.
        // The FUI <item> entries inside <list> are design-time only; the
        // published .bytes ships empty lists at runtime, so we repopulate.
        private static readonly (string label, string iconUrl)[] Tabs =
        {
            ("近期", "ui://qdao/icon_alt_left_tab_icon_1.png"),
            ("推荐", "ui://qdao/icon_alt_left_tab_icon_2.png"),
            ("全部", "ui://qdao/icon_alt_left_tab_icon_3.png"),
            ("新服", "ui://qdao/icon_alt_left_tab_icon_4.png"),
            ("官方", "ui://qdao/icon_category_yinyang.png"),
            ("海外", "ui://qdao/icon_category_swirl.png"),
        };

        // Inline server data — replaced by Session.Zones once the
        // /api/server-list fetch is wired up here.
        private static readonly (string name, string badgeUrl, int status, string subtitle)[] Servers =
        {
            ("云海宗", "ui://qdao/icon_server_badge_pavilion.png",  0, "畅通  人气旺"),
            ("清风谷", "ui://qdao/icon_server_badge_pagoda.png",    0, "畅通  人气旺"),
            ("碧落渊", "ui://qdao/icon_server_badge_sword.png",     1, "拥挤"),
            ("紫霄峰", "ui://qdao/icon_server_badge_whirlpool.png", 0, "畅通  人气旺"),
            ("沧浪洲", "ui://qdao/icon_server_badge_yinyang.png",   0, "畅通  人气旺"),
            ("昆仑墟", "ui://qdao/icon_server_badge_mountain.png",  1, "拥挤"),
            ("蓬莱岛", "ui://qdao/icon_server_badge_lotus.png",     0, "畅通  人气旺"),
            ("瀛洲海", "ui://qdao/icon_server_badge_talisman.png",  2, "维护中"),
            ("九霄殿", "ui://qdao/icon_server_badge_pavilion.png",  0, "畅通  人气旺"),
            ("太虚观", "ui://qdao/icon_server_badge_pagoda.png",    0, "畅通  人气旺"),
            ("玄都阁", "ui://qdao/icon_server_badge_sword.png",     1, "拥挤"),
            ("赤霞岭", "ui://qdao/icon_server_badge_whirlpool.png", 0, "畅通  人气旺"),
        };

        private const string TabItemUrl    = "ui://qdao/QdaoTabIdle";
        private const string ServerCardUrl = "ui://qdao/QdaoServerCard";

        private AppBootstrap _app;
        private GComponent _root;
        private GList _listTabs;
        private GList _listServer;
        private GTextField _txtStatus;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            _root = Theme.TryCreateFromPackage("ServerSelectScreen");
            if (_root == null)
            {
                _root = BuildBlank();
                return _root;
            }

            _listTabs   = Theme.Find<GList>(_root, "listTabs");
            _listServer = Theme.Find<GList>(_root, "listServer");
            _txtStatus  = Theme.Find<GTextField>(_root, "txtStatus");

            FillTabs();
            FillServers();
            BindButtons();
            SetStatus("");

            return _root;
        }

        public void OnEnter()
        {
            // Default initial selection — first server. Easier than asking
            // the user to click before they realise the bottom 进入 button
            // works.
            if (_app?.Session != null)
                _app.Session.SelectedZoneIndex = Mathf.Clamp(_app.Session.SelectedZoneIndex, 0, Mathf.Max(0, Servers.Length - 1));
            else
                ApplyServerSelection(0);
            ApplyServerSelection(_app?.Session?.SelectedZoneIndex ?? 0);
        }

        public void OnExit() { }
        public void Tick(float dt) { }

        // ── Population ──────────────────────────────────────────────

        private void FillTabs()
        {
            if (_listTabs == null) return;

            _listTabs.RemoveChildrenToPool();
            for (int i = 0; i < Tabs.Length; i++)
            {
                var item = UIPackage.CreateObjectFromURL(TabItemUrl) as GComponent;
                if (item == null) continue;
                _listTabs.AddChild(item);
                FillTab(item, i);
            }
            _listTabs.selectionMode = ListSelectionMode.Single;
            _listTabs.selectedIndex = 1; // start on "推荐"
            _listTabs.onClickItem.Set(OnTabClicked);

            // Tabs container is exact-fit (180 × 472 for 6×72 + 5×8); even
            // if FairyGUI built a ScrollPane for it, pin to top so it can't
            // drift on entry. touchEffect=false keeps stray drags from
            // scrolling the container 1px off-zero.
            if (_listTabs.scrollPane != null)
            {
                _listTabs.scrollPane.SetPosX(0f, false);
                _listTabs.scrollPane.SetPosY(0f, false);
                _listTabs.scrollPane.touchEffect = false;
                _listTabs.scrollPane.bouncebackEffect = false;
            }
        }

        private static void FillTab(GComponent item, int index)
        {
            if (item.GetChild("title") is GTextField title)
                title.text = Tabs[index].label;
            if (item.GetChild("icon") is GLoader icon)
                icon.url = Tabs[index].iconUrl;
        }

        private void FillServers()
        {
            if (_listServer == null) return;

            _listServer.RemoveChildrenToPool();
            for (int i = 0; i < Servers.Length; i++)
            {
                var item = UIPackage.CreateObjectFromURL(ServerCardUrl) as GComponent;
                if (item == null) continue;
                _listServer.AddChild(item);
                FillServer(item, i);
            }
            _listServer.selectionMode = ListSelectionMode.Single;
            _listServer.onClickItem.Set(OnServerClicked);

            VerifyGridShape();
        }

        private static void FillServer(GComponent card, int index)
        {
            var (name, badgeUrl, status, subtitle) = Servers[index];

            if (card.GetChild("title") is GTextField title)
                title.text = name;
            if (card.GetChild("subtitle") is GTextField sub)
                sub.text = subtitle;
            if (card.GetChild("badge") is GLoader badge)
                badge.url = badgeUrl;

            // status: 0 = green, 1 = red, 2 = hidden (maintenance). The
            // controller in QdaoServerCard.xml drives the dot's display.
            var statusCtrl = card.GetController("status");
            if (statusCtrl != null) statusCtrl.selectedIndex = Mathf.Clamp(status, 0, 2);

            // Make sure no card is left in a "checked" state from a prior
            // pool reuse — selection is reapplied centrally in
            // ApplyServerSelection().
            var checkedCtrl = card.GetController("checked");
            if (checkedCtrl != null) checkedCtrl.selectedIndex = 0;
        }

        /// <summary>
        /// Sanity-check that the published FUI grid actually renders as 2
        /// columns. Catches the recurring regression where the .bytes was
        /// republished with layout="flow_vt" or a wrong container width
        /// and silently turned into 3-or-4 columns. We don't auto-fix —
        /// only log so the next person sees the cause in the editor log.
        /// </summary>
        private void VerifyGridShape()
        {
            if (_listServer == null || _listServer.numChildren < 2) return;

            var first  = _listServer.GetChildAt(0);
            var second = _listServer.GetChildAt(1);
            if (first == null || second == null) return;

            // In a 2-column grid the first two cards share the SAME y and
            // differ in x. In a 1-column flow they share x; in a 3+ column
            // flow_vt grid (the regression we keep hitting) the first
            // column fills top-to-bottom, so cards 0 and 1 share x but
            // differ in y.
            bool isTwoColumn = Mathf.Approximately(first.y, second.y) && !Mathf.Approximately(first.x, second.x);
            if (isTwoColumn) return;

            int cols = CountFirstRowColumns();
            Debug.LogWarning(
                $"[ServerSelectScreen] listServer is rendering as {cols} column(s), expected 2. " +
                "Check ServerSelectScreen.xml layout=\"pagination\" + columnGap and republish qdao_fui.bytes.");
        }

        private int CountFirstRowColumns()
        {
            if (_listServer == null || _listServer.numChildren == 0) return 0;
            float row0Y = _listServer.GetChildAt(0).y;
            int cols = 0;
            for (int i = 0; i < _listServer.numChildren; i++)
            {
                if (Mathf.Approximately(_listServer.GetChildAt(i).y, row0Y)) cols++;
                else break;
            }
            return cols;
        }

        // ── Bottom action bar ───────────────────────────────────────

        private void BindButtons()
        {
            BindIfPresent("btnBack",    () => _app.Router.Show<LoginScreen>());
            BindIfPresent("btnRefresh", OnRefresh);
            BindIfPresent("btnConfirm", OnConfirm);
        }

        private void BindIfPresent(string childName, Action handler)
        {
            var go = _root.GetChild(childName);
            if (go == null) return;
            go.onClick.Set(_ => handler?.Invoke());
        }

        // ── Click handlers ──────────────────────────────────────────

        private void OnTabClicked(EventContext ctx)
        {
            int idx = _listTabs.GetChildIndex(ctx.data as GObject);
            if (idx < 0 || idx >= Tabs.Length) return;
            Debug.Log($"[ServerSelectScreen] tab: {Tabs[idx].label}");
            // Real tab-filtering goes here once Session.Zones is populated.
        }

        private void OnServerClicked(EventContext ctx)
        {
            int idx = _listServer.GetChildIndex(ctx.data as GObject);
            if (idx < 0 || idx >= Servers.Length) return;
            ApplyServerSelection(idx);
        }

        private void ApplyServerSelection(int index)
        {
            if (_listServer == null) return;
            if (index < 0 || index >= _listServer.numChildren) return;

            for (int i = 0; i < _listServer.numChildren; i++)
            {
                if (_listServer.GetChildAt(i) is GComponent card)
                {
                    var c = card.GetController("checked");
                    if (c != null) c.selectedIndex = (i == index) ? 1 : 0;
                }
            }
            _listServer.selectedIndex = index;
            if (_app?.Session != null) _app.Session.SelectedZoneIndex = index;
            SetStatus($"已选择: {Servers[index].name}");
        }

        private void OnRefresh()
        {
            Debug.Log("[ServerSelectScreen] refresh");
            FillServers();
            ApplyServerSelection(Mathf.Clamp(_app?.Session?.SelectedZoneIndex ?? 0, 0, Servers.Length - 1));
        }

        private void OnConfirm()
        {
            int idx = _app?.Session?.SelectedZoneIndex ?? -1;
            if (idx < 0 || idx >= Servers.Length)
            {
                SetStatus("请先选择一个服务器");
                return;
            }
            Debug.Log($"[ServerSelectScreen] enter: {Servers[idx].name}");
            _app.Router.Show<RoleCreateScreen>();
        }

        private void SetStatus(string text)
        {
            if (_txtStatus != null) _txtStatus.text = text ?? string.Empty;
        }

        // ── Fallback (FUI package missing) ──────────────────────────

        private static GComponent BuildBlank()
        {
            var c = new GComponent();
            c.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);

            var bg = new GGraph();
            bg.SetSize(c.width, c.height);
            bg.DrawRect(c.width, c.height, 0, Color.clear, new Color(0.08f, 0.10f, 0.10f));
            c.AddChild(bg);

            var label = new GTextField();
            label.SetSize(c.width, 80);
            label.SetXY(0, c.height * 0.5f - 40);
            label.text = "ServerSelectScreen — qdao package missing";
            label.textFormat = new TextFormat
            {
                font = Theme.BodyFontName,
                color = new Color(0.6f, 0.6f, 0.6f),
                size = 40, align = AlignType.Center,
            };
            label.verticalAlign = VertAlignType.Middle;
            c.AddChild(label);
            return c;
        }
    }
}
