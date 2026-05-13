using System;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Validation harness for the new three-layer qdao layout.
    /// Walks the FUI ServerSelectScreen component, discovers the two GList
    /// children (listTabs + listServer), and seeds them with the same
    /// inline data that lives in the FUI XML so the runtime sees content.
    ///
    /// FUI inline &lt;item&gt; entries on a list are design-time only — the
    /// editor preview shows them, but the published .bytes ships an empty
    /// list at runtime. Code has to repopulate via AddItemFromPool.
    ///
    /// Once the gateway returns real zones we'll replace the hard-coded
    /// arrays with Session.Zones / a fetch coroutine; for now this just
    /// proves the layout renders end-to-end.
    /// </summary>
    public sealed class ServerSelectScreen : IScreen
    {
        // Inline tab data — keep in sync with ServerSelectScreen.xml
        private static readonly (string label, string iconUrl)[] Tabs =
        {
            ("近期", "ui://qdao/icon_alt_left_tab_icon_1.png"),
            ("推荐", "ui://qdao/icon_alt_left_tab_icon_2.png"),
            ("全部", "ui://qdao/icon_alt_left_tab_icon_3.png"),
            ("新服", "ui://qdao/icon_alt_left_tab_icon_4.png"),
            ("官方", "ui://qdao/icon_category_yinyang.png"),
            ("海外", "ui://qdao/icon_category_swirl.png"),
        };

        // Inline server data — same names as the FUI item list, since the
        // gateway-side zone fetch isn't wired here yet.
        private static readonly (string name, string badgeUrl)[] Servers =
        {
            ("云海宗", "ui://qdao/icon_server_badge_pavilion.png"),
            ("清风谷", "ui://qdao/icon_server_badge_pagoda.png"),
            ("碧落渊", "ui://qdao/icon_server_badge_sword.png"),
            ("紫霄峰", "ui://qdao/icon_server_badge_whirlpool.png"),
            ("沧浪洲", "ui://qdao/icon_server_badge_yinyang.png"),
            ("昆仑墟", "ui://qdao/icon_server_badge_mountain.png"),
            ("蓬莱岛", "ui://qdao/icon_server_badge_lotus.png"),
            ("瀛洲海", "ui://qdao/icon_server_badge_talisman.png"),
            ("九霄殿", "ui://qdao/icon_server_badge_pavilion.png"),
            ("太虚观", "ui://qdao/icon_server_badge_pagoda.png"),
            ("玄都阁", "ui://qdao/icon_server_badge_sword.png"),
            ("赤霞岭", "ui://qdao/icon_server_badge_whirlpool.png"),
        };

        private const string TabItemUrl    = "ui://qdao/QdaoTabIdle";
        private const string ServerCardUrl = "ui://qdao/QdaoServerCard";

        private AppBootstrap _app;
        private GComponent _root;
        private GList _listTabs;
        private GList _listServer;
        private int _activeTab = 1;        // start on "推荐"
        private int _selectedServer = -1;

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

            FillTabs();
            FillServers();
            BindButtons();
            return _root;
        }

        public void OnEnter() { }
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
            _listTabs.selectedIndex = Mathf.Clamp(_activeTab, 0, Tabs.Length - 1);
            _listTabs.onClickItem.Add(OnTabClicked);

            // Lock the scroll pane so the tab list never drifts on open —
            // 6×88 + 5×8 = 568 fits in the 440 region with overflow=visible
            // anyway, but if FairyGUI built a ScrollPane for it, pin it.
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
            _listServer.onClickItem.Add(OnServerClicked);
        }

        private void FillServer(GComponent card, int index)
        {
            var (name, badgeUrl) = Servers[index];
            if (card.GetChild("title") is GTextField title)
                title.text = name;
            if (card.GetChild("subtitle") is GTextField subtitle)
                subtitle.text = "畅通  人气旺";
            if (card.GetChild("badge") is GLoader badge)
                badge.url = badgeUrl;

            // Inline buttons inside each card. Capture index by value.
            int idx = index;
            if (card.GetChild("btnEnter") is GButton btnEnter)
                btnEnter.onClick.Set(_ => OnEnterServer(idx));
            if (card.GetChild("btnDetail") is GButton btnDetail)
                btnDetail.onClick.Set(_ => OnShowDetail(idx));
        }

        // ── Bottom action buttons ───────────────────────────────────

        private void BindButtons()
        {
            BindIfPresent("btnBack",      () => _app.Router.Show<LoginScreen>());
            BindIfPresent("btnRefresh",   OnRefresh);
            BindIfPresent("btnConfirm",   OnConfirm);
            BindIfPresent("btnScrollUp",   () => ScrollList(-150f));
            BindIfPresent("btnScrollDown", () => ScrollList( 150f));
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
            _activeTab = idx;
            Debug.Log($"[ServerSelectScreen] tab clicked: {Tabs[idx].label}");
        }

        private void OnServerClicked(EventContext ctx)
        {
            int idx = _listServer.GetChildIndex(ctx.data as GObject);
            if (idx < 0 || idx >= Servers.Length) return;
            _selectedServer = idx;
            Debug.Log($"[ServerSelectScreen] server selected: {Servers[idx].name}");
        }

        private void OnEnterServer(int index)
        {
            if (index < 0 || index >= Servers.Length) return;
            _selectedServer = index;
            Debug.Log($"[ServerSelectScreen] enter: {Servers[index].name}");
            _app.Router.Show<RoleCreateScreen>();
        }

        private void OnShowDetail(int index)
        {
            if (index < 0 || index >= Servers.Length) return;
            Debug.Log($"[ServerSelectScreen] detail: {Servers[index].name}");
            // Future: pop a tooltip / detail panel
        }

        private void OnRefresh()
        {
            Debug.Log("[ServerSelectScreen] refresh");
            FillServers();
        }

        private void OnConfirm()
        {
            if (_selectedServer < 0)
            {
                Debug.Log("[ServerSelectScreen] confirm with no selection");
                return;
            }
            OnEnterServer(_selectedServer);
        }

        private void ScrollList(float deltaY)
        {
            if (_listServer?.scrollPane == null) return;
            float maxY = Mathf.Max(0f, _listServer.scrollPane.contentHeight - _listServer.scrollPane.viewHeight);
            _listServer.scrollPane.posY = Mathf.Clamp(_listServer.scrollPane.posY + deltaY, 0f, maxY);
        }

        // ── Fallback ────────────────────────────────────────────────

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
