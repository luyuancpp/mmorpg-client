using System;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Role creation screen — pick an archetype + nickname, then enter the
    /// game (HudScreen). Returning goes back to ServerSelectScreen so the
    /// player can pick a different zone without quitting.
    ///
    /// FUI status: there is no published RoleCreateScreen component in the
    /// qdao package yet, so this screen builds its UI in code — that way
    /// the role flow is functional today and the visuals can be promoted
    /// to a published FUI screen later without touching this file's API.
    /// If a "RoleCreateScreen" component IS later added to qdao, the
    /// constructor automatically prefers it and falls back to code only
    /// when the package or component is missing.
    /// </summary>
    public sealed class RoleCreateScreen : IScreen
    {
        private static readonly (string label, string subtitle, Color tint)[] Archetypes =
        {
            ("剑修", "近战 · 高爆发", new Color(0.78f, 0.22f, 0.18f)),
            ("法修", "远程 · 元素",    new Color(0.30f, 0.45f, 0.85f)),
            ("丹修", "辅助 · 治疗",    new Color(0.32f, 0.68f, 0.36f)),
            ("体修", "坦克 · 抗性",    new Color(0.55f, 0.40f, 0.20f)),
        };

        private AppBootstrap _app;
        private GComponent _root;

        // Code-built widgets (only valid when the FUI fallback path is used)
        private GTextInput _inputNickname;
        private GTextField _txtStatus;
        private GButton[]  _archetypeButtons;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            // Prefer a published FUI component if/when one ships. Today
            // there isn't one, so we always end up in BuildCodeUi(). The
            // moment somebody publishes "RoleCreateScreen" with the same
            // child names (btnCreate / btnBack / inputNickname / txtStatus
            // / listArchetypes), this screen will pick it up automatically.
            _root = Theme.TryCreateFromPackage("RoleCreateScreen");
            if (_root == null)
            {
                _root = BuildCodeUi();
            }
            else
            {
                BindFuiVersion();
            }

            return _root;
        }

        public void OnEnter()
        {
            // Re-seed widgets from Session every entry so going back from
            // HudScreen and forward again shows the prior nickname/archetype.
            if (_app?.Session != null)
            {
                if (_inputNickname != null) _inputNickname.text = _app.Session.RoleNickname ?? "";
                ApplyArchetypeSelection(_app.Session.RoleArchetypeIndex);
            }
        }

        public void OnExit() { CommitToSession(); }
        public void Tick(float dt) { }

        // ── FUI-published path (future) ─────────────────────────────

        private void BindFuiVersion()
        {
            _inputNickname = (_root.GetChild("inputNicknameBox") as GComponent)?.GetChild("input") as GTextInput;
            _txtStatus     = Theme.Find<GTextField>(_root, "txtStatus");

            BindIfPresent("btnCreate", OnCreate);
            BindIfPresent("btnBack",   OnBack);

            if (_root.GetChild("listArchetypes") is GList list)
            {
                list.RemoveChildrenToPool();
                for (int i = 0; i < Archetypes.Length; i++)
                {
                    var item = UIPackage.CreateObjectFromURL("ui://qdao/QdaoServerCard") as GComponent;
                    if (item == null) continue;
                    list.AddChild(item);
                    if (item.GetChild("title")    is GTextField t) t.text = Archetypes[i].label;
                    if (item.GetChild("subtitle") is GTextField s) s.text = Archetypes[i].subtitle;
                }
                list.selectionMode = ListSelectionMode.Single;
                list.onClickItem.Set(ctx =>
                {
                    int idx = list.GetChildIndex(ctx.data as GObject);
                    ApplyArchetypeSelection(idx);
                });
            }
        }

        // ── Code-built path (current) ───────────────────────────────

        private GComponent BuildCodeUi()
        {
            var c = new GComponent();
            c.SetSize(Theme.Art.ReferenceWidth, Theme.Art.ReferenceHeight);

            // Backdrop — same scene art as the other screens for consistency
            // when no FUI overlay is available.
            var bgLoader = new GLoader();
            bgLoader.SetSize(c.width, c.height);
            bgLoader.fill = FillType.ScaleFree;
            bgLoader.url = "ui://qdao/qdao_scene_bg";
            c.AddChild(bgLoader);

            // Dim the backdrop so the form reads cleanly.
            var dim = new GGraph();
            dim.SetSize(c.width, c.height);
            dim.DrawRect(c.width, c.height, 0, Color.clear, new Color(0f, 0f, 0f, 0.45f));
            c.AddChild(dim);

            // Title
            var title = new GTextField();
            title.SetSize(c.width, 100);
            title.SetXY(0, 120);
            title.text = "创建角色";
            title.textFormat = new TextFormat
            {
                font = Theme.TitleFontName, color = new Color(1f, 0.94f, 0.74f),
                size = 64, align = AlignType.Center, bold = true,
            };
            title.verticalAlign = VertAlignType.Middle;
            c.AddChild(title);

            // Archetype card row — 4 cards across, centred horizontally
            const int cardW = 360, cardH = 480, gap = 32;
            int totalW = cardW * Archetypes.Length + gap * (Archetypes.Length - 1);
            float startX = (c.width - totalW) * 0.5f;
            float rowY   = 280f;

            _archetypeButtons = new GButton[Archetypes.Length];
            for (int i = 0; i < Archetypes.Length; i++)
            {
                int idx = i;
                var card = BuildArchetypeCard(Archetypes[i].label, Archetypes[i].subtitle, Archetypes[i].tint, cardW, cardH);
                card.SetXY(startX + (cardW + gap) * i, rowY);
                card.onClick.Set(_ => ApplyArchetypeSelection(idx));
                c.AddChild(card);
                _archetypeButtons[i] = card;
            }

            // Nickname row
            var nickLabel = new GTextField();
            nickLabel.SetSize(300, 60);
            nickLabel.SetXY(c.width * 0.5f - 480, rowY + cardH + 60);
            nickLabel.text = "昵称";
            nickLabel.textFormat = new TextFormat
            {
                font = Theme.BodyFontName, color = new Color(1f, 0.94f, 0.74f),
                size = 32, align = AlignType.Right,
            };
            nickLabel.verticalAlign = VertAlignType.Middle;
            c.AddChild(nickLabel);

            _inputNickname = new GTextInput();
            _inputNickname.SetSize(640, 60);
            _inputNickname.SetXY(c.width * 0.5f - 160, rowY + cardH + 60);
            _inputNickname.singleLine = true;
            _inputNickname.maxLength  = 12;
            _inputNickname.text = _app?.Session?.RoleNickname ?? "云行客";
            _inputNickname.textFormat = new TextFormat
            {
                font = Theme.BodyFontName, color = new Color(0.18f, 0.13f, 0.08f),
                size = 30,
            };
            // Light backdrop so the input is visible against the dimmed scene.
            var inputBg = new GGraph();
            inputBg.SetSize(_inputNickname.width + 20, _inputNickname.height + 20);
            inputBg.SetXY(_inputNickname.x - 10, _inputNickname.y - 10);
            inputBg.DrawRect(inputBg.width, inputBg.height, 2,
                new Color(1f, 0.94f, 0.74f, 0.85f),
                new Color(1f, 0.97f, 0.86f, 0.9f));
            c.AddChild(inputBg);
            c.AddChild(_inputNickname);

            // Action buttons
            var btnBack   = BuildPillButton("返回",    new Color(0.40f, 0.36f, 0.32f), 280, 84);
            var btnCreate = BuildPillButton("进入游戏", new Color(0.78f, 0.22f, 0.18f), 360, 92);
            btnBack.SetXY  (c.width * 0.5f - 380, rowY + cardH + 180);
            btnCreate.SetXY(c.width * 0.5f +  20, rowY + cardH + 176);
            btnBack.onClick.Set(_ => OnBack());
            btnCreate.onClick.Set(_ => OnCreate());
            c.AddChild(btnBack);
            c.AddChild(btnCreate);

            // Status line at the bottom
            _txtStatus = new GTextField();
            _txtStatus.SetSize(c.width, 36);
            _txtStatus.SetXY(0, c.height - 64);
            _txtStatus.text = "";
            _txtStatus.textFormat = new TextFormat
            {
                font = Theme.BodyFontName, color = new Color(1f, 0.94f, 0.74f),
                size = 22, align = AlignType.Center,
            };
            _txtStatus.verticalAlign = VertAlignType.Middle;
            c.AddChild(_txtStatus);

            ApplyArchetypeSelection(_app?.Session?.RoleArchetypeIndex ?? 0);
            return c;
        }

        private static GButton BuildArchetypeCard(string title, string subtitle, Color tint, int w, int h)
        {
            var btn = new GButton();
            btn.SetSize(w, h);

            var frame = new GGraph();
            frame.SetSize(w, h);
            // Two-stop "fill" approximation by drawing a translucent body
            // over a tinted base. FairyGUI's GGraph DrawRect doesn't do
            // gradients so we layer two rects.
            frame.DrawRect(w, h, 4, new Color(1f, 0.94f, 0.74f, 0.9f), new Color(tint.r, tint.g, tint.b, 0.20f));
            btn.AddChild(frame);

            var titleField = new GTextField();
            titleField.SetSize(w, 80);
            titleField.SetXY(0, 36);
            titleField.text = title;
            titleField.textFormat = new TextFormat
            {
                font = Theme.TitleFontName, color = new Color(1f, 0.94f, 0.74f),
                size = 56, align = AlignType.Center, bold = true,
            };
            titleField.verticalAlign = VertAlignType.Middle;
            btn.AddChild(titleField);

            var subField = new GTextField();
            subField.SetSize(w, 40);
            subField.SetXY(0, h - 80);
            subField.text = subtitle;
            subField.textFormat = new TextFormat
            {
                font = Theme.BodyFontName, color = new Color(0.95f, 0.88f, 0.70f),
                size = 26, align = AlignType.Center,
            };
            subField.verticalAlign = VertAlignType.Middle;
            btn.AddChild(subField);

            // Selection ring (drawn with a thicker border in red), hidden
            // until ApplyArchetypeSelection enables it.
            var ring = new GGraph { name = "selRing" };
            ring.SetSize(w, h);
            ring.DrawRect(w, h, 6, new Color(0.78f, 0.22f, 0.18f, 1f), Color.clear);
            ring.visible = false;
            btn.AddChild(ring);

            return btn;
        }

        private static GButton BuildPillButton(string label, Color tint, int w, int h)
        {
            var btn = new GButton();
            btn.SetSize(w, h);

            var bg = new GGraph();
            bg.SetSize(w, h);
            bg.DrawRect(w, h, 3, new Color(1f, 0.94f, 0.74f, 0.95f), tint);
            btn.AddChild(bg);

            var t = new GTextField();
            t.SetSize(w, h);
            t.text = label;
            t.textFormat = new TextFormat
            {
                font = Theme.BodyFontName, color = new Color(1f, 0.97f, 0.88f),
                size = 30, align = AlignType.Center, bold = true,
            };
            t.verticalAlign = VertAlignType.Middle;
            btn.AddChild(t);

            return btn;
        }

        // ── Selection state ─────────────────────────────────────────

        private void ApplyArchetypeSelection(int index)
        {
            if (Archetypes.Length == 0) return;
            index = Mathf.Clamp(index, 0, Archetypes.Length - 1);

            if (_archetypeButtons != null)
            {
                for (int i = 0; i < _archetypeButtons.Length; i++)
                {
                    var ring = _archetypeButtons[i]?.GetChild("selRing");
                    if (ring != null) ring.visible = (i == index);
                }
            }

            if (_app?.Session != null) _app.Session.RoleArchetypeIndex = index;
            SetStatus($"已选择: {Archetypes[index].label}");
        }

        private void CommitToSession()
        {
            if (_app?.Session == null) return;
            if (_inputNickname != null && !string.IsNullOrWhiteSpace(_inputNickname.text))
                _app.Session.RoleNickname = _inputNickname.text.Trim();
        }

        // ── Buttons ─────────────────────────────────────────────────

        private void BindIfPresent(string childName, Action handler)
        {
            var go = _root.GetChild(childName);
            if (go == null) return;
            go.onClick.Set(_ => handler?.Invoke());
        }

        private void OnCreate()
        {
            CommitToSession();
            if (_app?.Session == null)
            {
                SetStatus("会话未初始化");
                return;
            }
            if (string.IsNullOrWhiteSpace(_app.Session.RoleNickname))
            {
                SetStatus("请输入昵称");
                return;
            }
            Debug.Log($"[RoleCreateScreen] create: nick={_app.Session.RoleNickname}, archetype={_app.Session.RoleArchetypeIndex} ({Archetypes[Mathf.Clamp(_app.Session.RoleArchetypeIndex, 0, Archetypes.Length - 1)].label})");
            SetStatus("正在进入游戏…");
            _app.Router.Show<HudScreen>();
        }

        private void OnBack()
        {
            CommitToSession();
            _app.Router.Show<ServerSelectScreen>();
        }

        private void SetStatus(string text)
        {
            if (_txtStatus != null) _txtStatus.text = text ?? string.Empty;
        }
    }
}
