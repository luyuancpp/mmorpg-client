using System;
using System.Collections;
using FairyGUI;
using MmorpgClient.Net;
using MmorpgClient.UI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Pulls the zone list from the Java gateway and lets the user pick one.
    /// On confirm, advances to <see cref="RoleCreateScreen"/>.
    ///
    /// All visuals come from the qdao FairyGUI package; this class only binds
    /// named children and feeds the two GLists (left filter tabs + center zone
    /// rows) from <see cref="SessionModel"/>.
    /// </summary>
    public sealed class ServerSelectScreen : IScreen
    {
        // Filter tabs in left column. Order matches default item order in
        // SceneScreen.xml / ServerSelectScreen.xml `listTabs`.
        private static readonly TabSpec[] Tabs =
        {
            new(0, "近期",  z => z.recommended || z.is_new),
            new(1, "推荐",  z => z.recommended),
            new(2, "全部",  _ => true),
            new(3, "新服",  z => z.is_new),
            new(4, "官方",  _ => true),
            new(5, "合服",  z => string.Equals(z.status, "MERGED", StringComparison.OrdinalIgnoreCase)),
            new(6, "测试",  z => string.Equals(z.status, "PREVIEW", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(z.status, "MAINTENANCE", StringComparison.OrdinalIgnoreCase)),
            new(7, "海外",  _ => false),
        };

        private readonly struct TabSpec
        {
            public readonly int Index;
            public readonly string Label;
            public readonly Func<ServerListZone, bool> Match;
            public TabSpec(int idx, string label, Func<ServerListZone, bool> match)
            { Index = idx; Label = label; Match = match; }
        }

        private AppBootstrap _app;
        private GList _tabList;
        private GList _zoneList;
        private GTextField _statusLabel;
        private GButton _confirmBtn;
        private bool _loading;
        private int  _activeTab = 1;       // default to "推荐"
        private int  _selectedVisibleIndex = -1;
        private readonly System.Collections.Generic.List<int> _visibleZoneIndices = new();

        public GComponent Build(AppBootstrap app)
        {
            _app = app;
            var packagedRoot = BuildFromPackage(app);
            if (packagedRoot != null) return packagedRoot;

            // Fallback: package missing -> minimal placeholder.
            var root = new GComponent();
            root.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);
            var warn = Theme.ArtText("ServerSelectScreen package missing — open qdao.fairy and republish.",
                                     Theme.TextWarn, 28, true, AlignType.Center);
            warn.SetSize(root.width, root.height);
            warn.verticalAlign = VertAlignType.Middle;
            root.AddChild(warn);
            return root;
        }

        private GComponent BuildFromPackage(AppBootstrap app)
        {
            var root = Theme.TryCreateFromPackage(Theme.UiId.ServerRoot);
            if (root == null) return null;

            _tabList     = Theme.Find<GList>(root,      Theme.UiId.ServerTabList);
            _zoneList    = Theme.Find<GList>(root,      Theme.UiId.ServerList);
            _statusLabel = Theme.Find<GTextField>(root, Theme.UiId.ServerStatus);
            _confirmBtn  = Theme.Find<GButton>(root,    Theme.UiId.ServerConfirmBtn);

            var btnRefresh = Theme.Find<GButton>(root, Theme.UiId.ServerRefreshBtn);
            var btnBack    = Theme.Find<GButton>(root, Theme.UiId.ServerBackBtn);

            if (_tabList == null || _zoneList == null || _statusLabel == null
                || _confirmBtn == null || btnRefresh == null || btnBack == null)
            {
                Debug.LogWarning("[ServerSelectScreen] FairyGUI ServerSelectScreen is missing required children; check qdao package.");
                root.Dispose();
                return null;
            }

            // Hide the static placeholder shortcut tabs (n6/n7/n8 in XML) — the
            // real tab strip comes from the GList. They exist only to satisfy
            // legacy Theme.UiId.SceneTabRecent/Recommend/All references.
            HideIfPresent(root, Theme.UiId.SceneTabRecent);
            HideIfPresent(root, Theme.UiId.SceneTabRecommend);
            HideIfPresent(root, Theme.UiId.SceneTabAll);

            // Tab list - explicit URL avoids any defaultItem resolution issues
            if (_tabList != null)
            {
                _tabList.selectionMode = ListSelectionMode.Single;
                _tabList.onClickItem.Add(OnTabClicked);
                Theme.FillList(_tabList, Theme.ItemUrl.TabItem, Theme.ItemUrl.TabItemById, Tabs.Length, RenderTabItem);
                if (_tabList.numChildren > 0)
                    _tabList.selectedIndex = Mathf.Clamp(_activeTab, 0, _tabList.numChildren - 1);
            }

            // Zone list - filled in Rebind() once data arrives
            if (_zoneList != null)
            {
                _zoneList.RemoveChildrenToPool();
                _zoneList.itemRenderer = RenderZoneItem;
                _zoneList.selectionMode = ListSelectionMode.Single;
                _zoneList.onClickItem.Add(OnZoneClicked);
            }

            // CTA buttons
            _confirmBtn.onClick.Add(_ => OnConfirm());
            btnRefresh.onClick.Add(_ => _app.Run(LoadZones()));
            btnBack.onClick.Add(_ => _app.Router.Show<LoginScreen>());

            UpdateConfirmEnabled(false);
            return root;
        }

        private static void HideIfPresent(GComponent root, string childName)
        {
            var go = root.GetChild(childName);
            if (go != null) go.visible = false;
        }

        public void OnEnter()
        {
            if (_app.Session.Zones.Count == 0 && !_loading) _app.Run(LoadZones());
            else Rebind();
        }

        public void OnExit() { }
        public void Tick(float dt) { }

        // ── Data fetch ────────────────────────────────────────────────

        private IEnumerator LoadZones()
        {
            _loading = true;
            _statusLabel.text = Text("server.loading", "正在获取区服...");
            yield return _app.Gateway.GetServerList(
                resp =>
                {
                    _app.Session.Zones.Clear();
                    if (resp?.zones != null) _app.Session.Zones.AddRange(resp.zones);
                    if (_app.Session.SelectedZoneIndex < 0 && _app.Session.Zones.Count > 0)
                        _app.Session.SelectedZoneIndex = FirstVisibleZoneIndex();
                    Rebind();
                    _statusLabel.text = string.Format(
                        Text("server.count", "共 {0} 个区服"), _app.Session.Zones.Count);
                },
                err =>
                {
                    Rebind();
                    _statusLabel.text = string.Format(
                        Text("server.error", "区服获取失败: {0}"), err);
                });
            _loading = false;
        }

        // ── Rebind (called whenever data or active tab changes) ──────

        private void Rebind()
        {
            if (_zoneList == null) return;

            _visibleZoneIndices.Clear();
            var match = Tabs[_activeTab].Match;
            for (int i = 0; i < _app.Session.Zones.Count; i++)
            {
                if (match(_app.Session.Zones[i])) _visibleZoneIndices.Add(i);
            }

            _selectedVisibleIndex = -1;
            int sel = _app.Session.SelectedZoneIndex;
            for (int v = 0; v < _visibleZoneIndices.Count; v++)
            {
                if (_visibleZoneIndices[v] == sel) { _selectedVisibleIndex = v; break; }
            }

            int actual = Theme.FillList(_zoneList, Theme.ItemUrl.ServerRow, Theme.ItemUrl.ServerRowById,
                                        _visibleZoneIndices.Count, RenderZoneItem);
            if (actual > 0 && _selectedVisibleIndex >= 0 && _selectedVisibleIndex < actual)
                _zoneList.selectedIndex = _selectedVisibleIndex;

            UpdateConfirmEnabled(_selectedVisibleIndex >= 0);

            if (_visibleZoneIndices.Count == 0 && !_loading)
                _statusLabel.text = Text("server.empty", "当前分类暂无区服");
        }

        // ── Per-row renderers ────────────────────────────────────────

        private void RenderTabItem(int index, GObject item)
        {
            if (item is not GComponent row) return;
            var title = row.GetChild(Theme.UiId.ServerRowTitle) as GTextField;
            if (title != null) title.text = Tabs[index].Label;

            // Toggle the row's "checked" controller (defined on QdaoTabItem) so
            // selected vs unselected art switches without code redrawing.
            var ctrl = row.GetController(Theme.UiId.ServerRowCheckCtrl);
            if (ctrl != null) ctrl.selectedIndex = (index == _activeTab) ? 1 : 0;
        }

        private void RenderZoneItem(int index, GObject item)
        {
            if (item is not GComponent row) return;
            if (index < 0 || index >= _visibleZoneIndices.Count) return;

            var z = _app.Session.Zones[_visibleZoneIndices[index]];

            var title    = row.GetChild(Theme.UiId.ServerRowTitle)    as GTextField;
            var subtitle = row.GetChild(Theme.UiId.ServerRowSubtitle) as GTextField;
            var badge    = row.GetChild(Theme.UiId.ServerRowBadge)    as GTextField;
            var icon     = row.GetChild(Theme.UiId.ServerRowIcon)     as GLoader;

            if (title != null) title.text = $"#{z.zone_id}  {z.name}";
            if (subtitle != null) subtitle.text = ZoneSubtitle(z);
            if (badge != null)
            {
                if (z.recommended)        badge.text = Text("server.recommended", "推荐");
                else if (z.is_new)        badge.text = Text("server.new", "新");
                else if (string.Equals(z.status, "MAINTENANCE", StringComparison.OrdinalIgnoreCase))
                    badge.text = Text("server.maint", "维护");
                else                      badge.text = "";
                badge.color = ZoneBadgeColor(z);
            }
            if (icon != null)
                icon.url = z.recommended ? "ui://qdao2026/qdao_icon_gate"
                                         : "ui://qdao2026/qdao_icon_talisman";

            // Red-dot controller on QdaoServerRow (page 0 hidden, 1 visible)
            var dot = row.GetController(Theme.UiId.ServerRowDotCtrl);
            if (dot != null) dot.selectedIndex = z.is_new ? 1 : 0;

            // Checked state for selected highlight
            var checkedCtrl = row.GetController(Theme.UiId.ServerRowCheckCtrl);
            if (checkedCtrl != null) checkedCtrl.selectedIndex = (index == _selectedVisibleIndex) ? 1 : 0;
        }

        // ── Click handlers ───────────────────────────────────────────

        private void OnTabClicked(EventContext ctx)
        {
            int idx = _tabList.GetChildIndex(ctx.data as GObject);
            if (idx < 0 || idx >= Tabs.Length) return;
            if (idx == _activeTab) return;
            _activeTab = idx;
            Rebind();
        }

        private void OnZoneClicked(EventContext ctx)
        {
            int visibleIdx = _zoneList.GetChildIndex(ctx.data as GObject);
            if (visibleIdx < 0 || visibleIdx >= _visibleZoneIndices.Count) return;
            int sessionIdx = _visibleZoneIndices[visibleIdx];
            if (sessionIdx == _app.Session.SelectedZoneIndex && visibleIdx == _selectedVisibleIndex)
                return;

            _app.Session.SelectedZoneIndex = sessionIdx;
            _selectedVisibleIndex = visibleIdx;

            // Refresh row highlight without full rebuild
            for (int i = 0; i < _zoneList.numChildren; i++)
                RenderZoneItem(i, _zoneList.GetChildAt(i));

            UpdateConfirmEnabled(true);
        }

        private void OnConfirm()
        {
            if (_app.Session.SelectedZone == null) return;
            _app.Router.Show<RoleCreateScreen>();
        }

        // ── Helpers ──────────────────────────────────────────────────

        private void UpdateConfirmEnabled(bool enabled)
        {
            if (_confirmBtn == null) return;
            _confirmBtn.touchable = enabled;
            _confirmBtn.alpha = enabled ? 1f : 0.5f;
        }

        private int FirstVisibleZoneIndex()
        {
            var match = Tabs[_activeTab].Match;
            for (int i = 0; i < _app.Session.Zones.Count; i++)
                if (match(_app.Session.Zones[i])) return i;
            return _app.Session.Zones.Count > 0 ? 0 : -1;
        }

        private static string ZoneSubtitle(ServerListZone z)
        {
            string status = string.IsNullOrEmpty(z.status) ? "OPEN" : z.status;
            string load   = string.IsNullOrEmpty(z.load_level) ? "" : "  人气 " + z.load_level;
            return status + load;
        }

        private static Color ZoneBadgeColor(ServerListZone z) => z.status switch
        {
            "MAINTENANCE" => Theme.TextWarn,
            "CLOSED"      => Theme.TextWarn,
            "PREVIEW"     => Theme.Accent,
            _             => new Color(0.61f, 0.17f, 0.10f),
        };

        private static string Text(string key, string fallback) => QdaoUiText.Get(key, fallback);
    }
}
