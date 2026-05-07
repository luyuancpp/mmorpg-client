using System.Collections;
using MmorpgClient.Net;
using UnityEngine;
using UnityEngine.UIElements;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Pulls the zone list from the Java gateway and lets the user pick one.
    /// On confirm, advances to RoleCreateScreen (which actually drives the
    /// login -> assign-gate -> enter-game pipeline).
    /// </summary>
    public sealed class ServerSelectScreen : IScreen
    {
        private AppBootstrap _app;
        private ScrollView _zoneList;
        private Label _statusLabel;
        private Button _confirmBtn;
        private bool _loading;

        public VisualElement Build(AppBootstrap app)
        {
            _app = app;
            var root = new VisualElement();
            root.style.flexGrow = 1;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;

            var card = new VisualElement();
            card.style.width = 720;
            card.style.maxWidth = new Length(92, LengthUnit.Percent);
            Theme.StylePanel(card);
            root.Add(card);

            card.Add(Theme.H1("选择服务器"));
            card.Add(Theme.P("由 Java Gateway 实时下发的区服列表"));

            _zoneList = new ScrollView { mode = ScrollViewMode.Vertical };
            _zoneList.style.height = 320;
            _zoneList.style.marginTop = 12;
            card.Add(_zoneList);

            _statusLabel = Theme.P("");
            _statusLabel.style.marginTop = 6;
            card.Add(_statusLabel);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 12;
            _confirmBtn = Theme.PrimaryButton("确认进入选角", OnConfirm);
            _confirmBtn.SetEnabled(false);
            btnRow.Add(_confirmBtn);
            btnRow.Add(Theme.GhostButton("刷新列表", () => _app.Run(LoadZones())));
            btnRow.Add(Theme.GhostButton("返回", () => _app.Router.Show<LoginScreen>()));
            card.Add(btnRow);

            return root;
        }

        public void OnEnter()
        {
            if (_app.Session.Zones.Count == 0 && !_loading)
                _app.Run(LoadZones());
            else
                Rebuild();
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
            _statusLabel.style.color = Theme.TextDim;

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
                err =>
                {
                    _statusLabel.text = "区服拉取失败: " + err;
                    _statusLabel.style.color = Theme.TextWarn;
                });
            _loading = false;
        }

        private void Rebuild()
        {
            _zoneList.Clear();
            for (int i = 0; i < _app.Session.Zones.Count; i++)
            {
                var z = _app.Session.Zones[i];
                int idx = i;
                var row = new Button(() =>
                {
                    _app.Session.SelectedZoneIndex = idx;
                    Rebuild();
                });
                row.text = string.Empty;
                row.style.height = 56;
                row.style.marginBottom = 6;
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingLeft = row.style.paddingRight = 12;
                row.style.borderTopWidth = row.style.borderBottomWidth = row.style.borderLeftWidth = row.style.borderRightWidth = 0;
                row.style.borderTopLeftRadius = row.style.borderTopRightRadius = row.style.borderBottomLeftRadius = row.style.borderBottomRightRadius = 4;
                row.style.backgroundColor = (idx == _app.Session.SelectedZoneIndex) ? Theme.ZoneSel : Theme.ZoneIdle;

                var name = new Label($"#{z.zone_id}  {z.name}");
                name.style.color = Theme.TextPrim;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                name.style.fontSize = 16;
                name.style.flexGrow = 1;
                row.Add(name);

                var status = new Label(StatusText(z));
                status.style.color = StatusColor(z);
                status.style.marginRight = 12;
                row.Add(status);

                if (z.recommended)
                {
                    var badge = new Label("推荐");
                    badge.style.backgroundColor = Theme.Accent;
                    badge.style.color = Color.black;
                    badge.style.paddingLeft = badge.style.paddingRight = 6;
                    badge.style.paddingTop = badge.style.paddingBottom = 2;
                    badge.style.borderTopLeftRadius = badge.style.borderTopRightRadius = badge.style.borderBottomLeftRadius = badge.style.borderBottomRightRadius = 3;
                    row.Add(badge);
                }
                _zoneList.Add(row);
            }
            _confirmBtn.SetEnabled(_app.Session.SelectedZone != null);
        }

        private static string StatusText(ServerListZone z)
        {
            string s = string.IsNullOrEmpty(z.status) ? "OPEN" : z.status;
            string l = string.IsNullOrEmpty(z.load_level) ? "" : "·" + z.load_level;
            return s + l;
        }

        private static Color StatusColor(ServerListZone z)
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
