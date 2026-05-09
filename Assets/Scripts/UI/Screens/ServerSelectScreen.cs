using System.Collections;
using FairyGUI;
using MmorpgClient.UI;
using MmorpgClient.Net;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Pulls the zone list from the Java gateway and lets the user pick one.
    /// On confirm, advances to <see cref="RoleCreateScreen"/>.
    /// </summary>
    public sealed class ServerSelectScreen : IScreen
    {
        private static readonly (string key, string fallback)[] ZoneTabs =
        {
            ("server.tab.mine", "我的角色"),
            ("server.tab.recommend", "推荐区服"),
            ("server.tab.preview", "预告专区"),
            ("server.tab.all", "全部区服"),
        };

        private AppBootstrap _app;
        private GComponent _tabList;
        private GComponent _zoneList;
        private GTextField _statusLabel;
        private GComponent _confirmBtn;
        private bool _loading;
        private int _activeTab = 1;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            var packagedRoot = BuildFromPackage(app);
            if (packagedRoot != null)
                return packagedRoot;

            var root = new GComponent();
            root.SetSize(GRoot.inst.width, GRoot.inst.height);

            _tabList = new GComponent();
            _tabList.SetSize(root.width, root.height);
            root.AddChild(_tabList);

            _zoneList = new GComponent();
            _zoneList.SetSize(root.width, root.height);
            root.AddChild(_zoneList);

            _statusLabel = Theme.ArtText("", Theme.TextDim, 18, false, AlignType.Center);
            Theme.SetArtRect(_statusLabel, root.width, root.height, 1120, 910, 520, 34);
            root.AddChild(_statusLabel);

            _confirmBtn = AddHitButton(root, 1722, 866, 154, 65, OnConfirm);
            _confirmBtn.touchable = false;
            _confirmBtn.alpha = 0.5f;

            AddHitButton(root, 1490, 872, 170, 50, () => _app.Run(LoadZones()));
            AddHitButton(root, 1300, 872, 150, 50, () => _app.Router.Show<LoginScreen>());

            return root;
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
            var root = Theme.TryCreateFromPackage(Theme.UiId.ServerRoot);
            if (root == null)
                return null;

            _zoneList = ReplacePackagedDynamicLayer(root, Theme.UiId.ServerList);
            _statusLabel = Theme.Find<GTextField>(root, Theme.UiId.ServerStatus);
            _confirmBtn = Theme.Find<GComponent>(root, Theme.UiId.ServerConfirmBtn);

            _tabList = new GComponent();
            _tabList.SetSize(root.width, root.height);
            root.AddChild(_tabList);

            var btnRefresh = Theme.Find<GButton>(root, Theme.UiId.ServerRefreshBtn);
            var btnBack = Theme.Find<GButton>(root, Theme.UiId.ServerBackBtn);

            if (_zoneList == null || _statusLabel == null || _confirmBtn == null || btnRefresh == null || btnBack == null)
            {
                Debug.LogWarning("[ServerSelectScreen] FairyGUI ServerSelectScreen component is missing required children; fallback to code UI.");
                root.Dispose();
                return null;
            }

            _confirmBtn.touchable = false;
            _confirmBtn.alpha = 0.5f;
            if (_confirmBtn is GButton confirmButton)
            {
                confirmButton.onClick.Add(_ => OnConfirm());
            }
            else
            {
                _confirmBtn.onClick.Add(_ => OnConfirm());
            }

            btnRefresh.onClick.Add(_ => _app.Run(LoadZones()));
            btnBack.onClick.Add(_ => _app.Router.Show<LoginScreen>());
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
            layer.touchable = true;
            root.AddChildAt(layer, index);
            return layer;
        }

        public void OnEnter()
        {
            if (_app.Session.Zones.Count == 0 && !_loading) _app.Run(LoadZones());
            else Rebuild();
        }

        public void OnExit() { }
        public void Tick(float dt) { }

        private void OnConfirm()
        {
            if (_app.Session.SelectedZone == null) return;
            _app.Router.Show<RoleCreateScreen>();
        }

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
                        _app.Session.SelectedZoneIndex = 0;
                    Rebuild();
                    _statusLabel.text = string.Format(Text("server.count", "共 {0} 个区服"), _app.Session.Zones.Count);
                },
                err => _statusLabel.text = string.Format(Text("server.error", "区服获取失败: {0}"), err));
            _loading = false;
        }

        private void Rebuild()
        {
            RebuildTabs();
            _zoneList.RemoveChildren(0, -1, true);

            var rowSlots = new (float x, float y)[]
            {
                (955, 315), (1455, 315),
                (955, 447), (1455, 447),
                (955, 580), (1455, 580),
                (955, 713), (1455, 713),
            };
            const float cellW = 467f, rowH = 97f;
            int visibleIndex = 0;
            for (int i = 0; i < _app.Session.Zones.Count; i++)
            {
                var z = _app.Session.Zones[i];
                if (!IsVisibleInActiveTab(z)) continue;
                if (visibleIndex >= rowSlots.Length) break;

                int idx = i;
                bool selected = (idx == _app.Session.SelectedZoneIndex);
                var slot = rowSlots[visibleIndex];
                var row = new GComponent();
                row.touchable = true;
                Theme.SetArtRect(row, _zoneList.width, _zoneList.height, slot.x, slot.y, cellW, rowH);
                row.onClick.Add(_ =>
                {
                    _app.Session.SelectedZoneIndex = idx;
                    Rebuild();
                });

                var rowBg = Theme.Image(visibleIndex % 2 == 0 ? Theme.Art.ServerRow : Theme.Art.ServerRowAlt, row.width, row.height);
                if (rowBg != null)
                    row.AddChild(rowBg);

                if (selected)
                {
                    var selectedGlow = new GGraph();
                    selectedGlow.DrawRect(row.width, row.height, 2, Theme.PanelEdge, new Color(0.18f, 0.62f, 0.52f, 0.18f));
                    row.AddChild(selectedGlow);
                }

                var name = new GTextField();
                name.SetXY(row.width * 0.16f, row.height * 0.24f);
                name.SetSize(row.width * 0.56f, row.height * 0.28f);
                name.text = z.name;
                name.textFormat = new TextFormat { font = Theme.BodyFontName, color = Theme.TextPrim, size = 15, bold = true, align = AlignType.Left };
                row.AddChild(name);

                var status = new GTextField();
                status.SetXY(row.width * 0.16f, row.height * 0.55f);
                status.SetSize(row.width * 0.56f, row.height * 0.24f);
                status.text = $"#{z.zone_id}  {StatusText(z)}";
                status.textFormat = new TextFormat { font = Theme.BodyFontName, color = StatusColor(z), size = 12 };
                row.AddChild(status);

                if (z.recommended)
                {
                    var badge = new GComponent();
                    badge.SetSize(row.width * 0.13f, row.height * 0.24f);
                    badge.SetXY(row.width * 0.78f, row.height * 0.12f);
                    var bg = new GGraph();
                    bg.DrawRect(badge.width, badge.height, 0, Color.clear, Theme.Accent);
                    badge.AddChild(bg);
                    var bt = new GTextField();
                    bt.SetSize(badge.width, badge.height);
                    bt.text = Text("server.recommended", "推荐");
                    bt.textFormat = new TextFormat { font = Theme.BodyFontName, color = Color.black, size = 12, bold = true, align = AlignType.Center };
                    bt.verticalAlign = VertAlignType.Middle;
                    badge.AddChild(bt);
                    row.AddChild(badge);
                }
                _zoneList.AddChild(row);
                visibleIndex++;
            }

            if (visibleIndex == 0)
            {
                var text = Theme.ArtText(Text("server.empty", "当前分类暂无区服"), Theme.TextDim, 18, false, AlignType.Center);
                Theme.SetArtRect(text, _zoneList.width, _zoneList.height, 1050, 500, 760, 48);
                _zoneList.AddChild(text);
            }
            bool can = _app.Session.SelectedZone != null && IsVisibleInActiveTab(_app.Session.SelectedZone);
            _confirmBtn.touchable = can;
            _confirmBtn.alpha = can ? 1f : 0.5f;
        }

        private void RebuildTabs()
        {
            _tabList.RemoveChildren(0, -1, true);
            for (int i = 0; i < ZoneTabs.Length; i++)
            {
                int idx = i;
                bool selected = _activeTab == idx;
                var tab = new GComponent();
                tab.touchable = true;
                Theme.SetArtRect(tab, _tabList.width, _tabList.height, 646, 363 + i * 74, 240, 68);
                tab.onClick.Add(_ =>
                {
                    _activeTab = idx;
                    Rebuild();
                });
                tab.alpha = selected ? 1f : 0.72f;

                var tabBg = new GGraph();
                tabBg.DrawRect(tab.width, tab.height, 1, Theme.PanelEdge, selected ? Theme.BtnPrim : Theme.BtnGhost);
                tab.AddChild(tabBg);

                var label = new GTextField();
                label.SetSize(tab.width, tab.height);
                label.text = Text(ZoneTabs[i].key, ZoneTabs[i].fallback);
                label.textFormat = new TextFormat { font = Theme.BodyFontName, color = selected ? Color.white : Theme.TextPrim, size = 14, bold = true, align = AlignType.Center };
                label.verticalAlign = VertAlignType.Middle;
                tab.AddChild(label);

                if (selected)
                {
                    var mark = new GGraph();
                    mark.SetXY(tab.width * 0.12f, tab.height * 0.31f);
                    mark.DrawEllipse(26, 26, Theme.Accent);
                    tab.AddChild(mark);
                }

                _tabList.AddChild(tab);
            }
        }

        private bool IsVisibleInActiveTab(ServerListZone z)
        {
            return _activeTab switch
            {
                1 => z.recommended,
                2 => z.status == "PREVIEW" || z.status == "MAINTENANCE",
                _ => true,
            };
        }

        private static string StatusText(ServerListZone z)
        {
            string s = string.IsNullOrEmpty(z.status) ? "OPEN" : z.status;
            string l = string.IsNullOrEmpty(z.load_level) ? "" : "·" + z.load_level;
            return s + l;
        }

        private static UnityEngine.Color StatusColor(ServerListZone z)
        {
            return z.status switch
            {
                "MAINTENANCE" => Theme.TextWarn,
                "CLOSED"      => Theme.TextWarn,
                "PREVIEW"     => Theme.Accent,
                _             => Theme.TextDim,
            };
        }

        private static string Text(string key, string fallback) => QdaoUiText.Get(key, fallback);
    }
}
