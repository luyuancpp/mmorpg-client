using System.Collections;
using FairyGUI;
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
        private AppBootstrap _app;
        private GComponent _card;
        private GComponent _zoneList;
        private GTextField _statusLabel;
        private GComponent _confirmBtn;
        private bool _loading;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;
            var root = new GComponent();
            root.SetSize(GRoot.inst.width, GRoot.inst.height);

            const float CW = 720, CH = 560;
            _card = Theme.Card(CW, CH);
            _card.SetXY((root.width - CW) * 0.5f, (root.height - CH) * 0.5f);
            _card.AddRelation(root, RelationType.Center_Center);
            root.AddChild(_card);

            float x = 22, y = 18;
            var h1 = Theme.H1("选择服务器"); h1.SetXY(x, y); _card.AddChild(h1); y += 38;
            var sub = Theme.P("由 Java Gateway 实时下发的区服列表"); sub.SetXY(x, y); _card.AddChild(sub); y += 28;

            _zoneList = new GComponent();
            _zoneList.SetXY(x, y);
            _zoneList.SetSize(CW - x * 2, 360);
            _card.AddChild(_zoneList);
            y += 370;

            _statusLabel = Theme.P(""); _statusLabel.SetXY(x, y); _card.AddChild(_statusLabel); y += 24;

            _confirmBtn = Theme.PrimaryButton("确认进入选角", OnConfirm, 150);
            _confirmBtn.SetXY(x, y);
            _confirmBtn.touchable = false;
            _confirmBtn.alpha = 0.5f;
            _card.AddChild(_confirmBtn);

            var refreshBtn = Theme.GhostButton("刷新列表", () => _app.Run(LoadZones()));
            refreshBtn.SetXY(x + 160, y);
            _card.AddChild(refreshBtn);

            var backBtn = Theme.GhostButton("返回", () => _app.Router.Show<LoginScreen>());
            backBtn.SetXY(x + 280, y);
            _card.AddChild(backBtn);

            return root;
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
            _statusLabel.text = "正在拉取区服列表...";
            yield return _app.Gateway.GetServerList(
                resp =>
                {
                    _app.Session.Zones.Clear();
                    if (resp?.zones != null) _app.Session.Zones.AddRange(resp.zones);
                    if (_app.Session.SelectedZoneIndex < 0 && _app.Session.Zones.Count > 0)
                        _app.Session.SelectedZoneIndex = 0;
                    Rebuild();
                    _statusLabel.text = $"共 {_app.Session.Zones.Count} 个区服";
                },
                err => _statusLabel.text = "区服拉取失败: " + err);
            _loading = false;
        }

        private void Rebuild()
        {
            _zoneList.RemoveChildren(0, -1, true);
            float rowH = 56, gap = 6, w = _zoneList.width;
            for (int i = 0; i < _app.Session.Zones.Count; i++)
            {
                var z = _app.Session.Zones[i];
                int idx = i;
                bool selected = (idx == _app.Session.SelectedZoneIndex);
                var bgColor = selected ? Theme.ZoneSel : Theme.ZoneIdle;
                var row = Theme.FlatButton("", w, rowH, bgColor, Theme.TextPrim, () =>
                {
                    _app.Session.SelectedZoneIndex = idx;
                    Rebuild();
                });
                row.SetXY(0, i * (rowH + gap));

                var name = new GTextField();
                name.SetXY(12, 6);
                name.SetSize(w - 200, 24);
                name.text = $"#{z.zone_id}  {z.name}";
                name.textFormat = new TextFormat { color = Theme.TextPrim, size = 16, bold = true, align = AlignType.Left };
                row.AddChild(name);

                var status = new GTextField();
                status.SetXY(12, 30);
                status.SetSize(w - 200, 20);
                status.text = StatusText(z);
                status.textFormat = new TextFormat { color = StatusColor(z), size = 12 };
                row.AddChild(status);

                if (z.recommended)
                {
                    var badge = new GComponent();
                    badge.SetSize(48, 22);
                    badge.SetXY(w - 64, 8);
                    var bg = new GGraph();
                    bg.DrawRect(48, 22, 0, Color.clear, Theme.Accent);
                    badge.AddChild(bg);
                    var bt = new GTextField();
                    bt.SetSize(48, 22);
                    bt.text = "推荐";
                    bt.textFormat = new TextFormat { color = Color.black, size = 12, bold = true, align = AlignType.Center };
                    bt.verticalAlign = VertAlignType.Middle;
                    badge.AddChild(bt);
                    row.AddChild(badge);
                }
                _zoneList.AddChild(row);
            }
            bool can = _app.Session.SelectedZone != null;
            _confirmBtn.touchable = can;
            _confirmBtn.alpha = can ? 1f : 0.5f;
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
    }
}
