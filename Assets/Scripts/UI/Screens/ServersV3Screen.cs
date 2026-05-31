using System;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// V3 server selection screen.
    ///
    /// Layout contract with assets/qdao/ServersV3.xml:
    ///   banner (V3Banner), ornTL/ornTR (GImage)
    ///   tabs    (GList, defaultItem=V3Tab,  Single selection)
    ///   servers (GList, defaultItem=V3Card, Single selection, pagination 2x4)
    ///   btnBack / btnRefresh / btnEnter
    ///   status  (GTextField)
    ///
    /// Selection contract:
    ///   V3Card has a "checked" controller (0=idle, 1=active). We drive it
    ///   manually on click rather than relying on GList's built-in selection
    ///   highlight, because:
    ///     - GList only flips one controller value at a time, and
    ///     - re-population via RemoveChildrenToPool loses prior state.
    ///   See ApplyServerSelection() for the single source of truth.
    /// </summary>
    public sealed class ServersV3Screen : IScreen
    {
        private const string PkgComponent = "ServersV3";
        private const string TabUrl  = "ui://qdao/V3Tab";
        private const string CardUrl = "ui://qdao/V3Card";

        // Placeholder tab + server data. Replaced by SessionModel.Zones when
        // the /api/server-list fetch is wired into this screen.
        private static readonly string[] TabLabels =
        {
            "近期", "推荐", "全部", "新服", "官方", "海外"
        };

        // (name, subtitle, status:0=ok 1=busy 2=maintenance)
        private static readonly (string name, string subtitle, int status)[] Servers =
        {
            ("云海宗", "畅通  人气旺", 0),
            ("清风谷", "畅通  人气旺", 0),
            ("碧落渊", "拥挤",         1),
            ("紫霄峰", "畅通  人气旺", 0),
            ("沧浪洲", "畅通  人气旺", 0),
            ("昆仑墟", "拥挤",         1),
            ("蓬莱岛", "畅通  人气旺", 0),
            ("瀛洲海", "维护中",       2)
        };

        private AppBootstrap _app;
        private GComponent _root;
        private GList _listTabs;
        private GList _listServers;
        private GTextField _status;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            _root = Theme.TryCreateFromPackage(PkgComponent);
            if (_root == null) { _root = BuildMissingPackagePlaceholder(); return _root; }

            _listTabs    = Theme.Find<GList>(_root, "tabs");
            _listServers = Theme.Find<GList>(_root, "servers");
            _status      = Theme.Find<GTextField>(_root, "status");

            FillTabs();
            FillServers();

            BindIfPresent("tabRecent",    () => OnTopTabClicked(0));
            BindIfPresent("tabRecommend", () => OnTopTabClicked(1));
            BindIfPresent("tabAll",       () => OnTopTabClicked(2));
            BindIfPresent("btnBack",    () => _app.Router.Show<LoginV3Screen>());
            BindIfPresent("btnRefresh", OnRefresh);
            BindIfPresent("btnEnter",   OnEnterClicked);

            SetStatus("");
            return _root;
        }

        public void OnEnter()
        {
            int idx = Mathf.Clamp(_app?.Session?.SelectedZoneIndex ?? 0, 0, Servers.Length - 1);
            ApplyServerSelection(idx);
        }
        public void OnExit() { }
        public void Tick(float _) { }

        // ── Population ────────────────────────────────────────────────

        private void FillTabs()
        {
            if (_listTabs == null) return;
            _listTabs.RemoveChildrenToPool();
            for (int i = 0; i < TabLabels.Length; i++)
            {
                var item = UIPackage.CreateObjectFromURL(TabUrl) as GComponent;
                if (item == null) continue;
                _listTabs.AddChild(item);
                if (item.GetChild("title") is GTextField t) t.text = TabLabels[i];

                var styleCtrl = item.GetController("style");
                if (styleCtrl != null && styleCtrl.pageCount > 0)
                    styleCtrl.selectedIndex = Mathf.Clamp(i, 0, styleCtrl.pageCount - 1);
            }
            _listTabs.selectionMode = ListSelectionMode.Single;
            _listTabs.selectedIndex = 1; // start on "推荐"
            _listTabs.onClickItem.Set(OnTabClicked);

            if (_listTabs.scrollPane != null)
            {
                _listTabs.scrollPane.touchEffect = false;
                _listTabs.scrollPane.bouncebackEffect = false;
            }
        }

        private void FillServers()
        {
            if (_listServers == null) return;
            _listServers.RemoveChildrenToPool();
            for (int i = 0; i < Servers.Length; i++)
            {
                var card = UIPackage.CreateObjectFromURL(CardUrl) as GComponent;
                if (card == null) continue;
                _listServers.AddChild(card);
                FillServerCard(card, i);
            }
            _listServers.selectionMode = ListSelectionMode.Single;
            _listServers.onClickItem.Set(OnServerClicked);
        }

        private static void FillServerCard(GComponent card, int index)
        {
            var (name, subtitle, status) = Servers[index];
            if (card.GetChild("title")    is GTextField t) t.text = name;
            if (card.GetChild("subtitle") is GTextField s) s.text = subtitle;

            var statusCtrl = card.GetController("status");
            if (statusCtrl != null) statusCtrl.selectedIndex = Mathf.Clamp(status, 0, 2);

            var badgeCtrl = card.GetController("badge");
            if (badgeCtrl != null && badgeCtrl.pageCount > 0)
                badgeCtrl.selectedIndex = index % badgeCtrl.pageCount;

            // Always reset checked when re-populating — pooled cards may
            // come back already selected from a previous fill.
            var checkedCtrl = card.GetController("checked");
            if (checkedCtrl != null) checkedCtrl.selectedIndex = 0;
        }

        // ── Click handlers ────────────────────────────────────────────

        private void OnTabClicked(EventContext ctx)
        {
            int idx = _listTabs.GetChildIndex(ctx.data as GObject);
            if (idx < 0 || idx >= TabLabels.Length) return;
            Debug.Log($"[ServersV3] tab: {TabLabels[idx]}");
            // Real filter wires up here once Session.Zones is fed by the API.
        }

        private void OnTopTabClicked(int index)
        {
            if (_listTabs != null)
                _listTabs.selectedIndex = Mathf.Clamp(index, 0, _listTabs.numChildren - 1);
            Debug.Log($"[ServersV3] top tab: {index}");
            SetStatus(index == 0 ? "最近登录" : index == 1 ? "推荐服务器" : "全部区服");
        }

        private void OnServerClicked(EventContext ctx)
        {
            int idx = _listServers.GetChildIndex(ctx.data as GObject);
            if (idx < 0 || idx >= Servers.Length) return;
            ApplyServerSelection(idx);
        }

        private void ApplyServerSelection(int index)
        {
            if (_listServers == null) return;
            if (index < 0 || index >= _listServers.numChildren) return;

            for (int i = 0; i < _listServers.numChildren; i++)
            {
                if (_listServers.GetChildAt(i) is GComponent card)
                {
                    var c = card.GetController("checked");
                    if (c != null) c.selectedIndex = (i == index) ? 1 : 0;
                }
            }
            _listServers.selectedIndex = index;
            if (_app?.Session != null) _app.Session.SelectedZoneIndex = index;
            SetStatus($"已选择: {Servers[index].name}");
        }

        private void OnRefresh()
        {
            Debug.Log("[ServersV3] refresh");
            FillServers();
            ApplyServerSelection(Mathf.Clamp(_app?.Session?.SelectedZoneIndex ?? 0, 0, Servers.Length - 1));
        }

        private void OnEnterClicked()
        {
            int idx = _app?.Session?.SelectedZoneIndex ?? -1;
            if (idx < 0 || idx >= Servers.Length) { SetStatus("请先选择一个服务器"); return; }
            Debug.Log($"[ServersV3] enter zone: {Servers[idx].name}");
            _app.Router.Show<SceneV3Screen>();
        }

        // ── shared utilities ──────────────────────────────────────────

        private void BindIfPresent(string childName, Action handler)
        {
            var go = _root.GetChild(childName);
            if (go == null) return;
            go.onClick.Set(_ => handler?.Invoke());
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
