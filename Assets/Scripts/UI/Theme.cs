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
        public static readonly Color BgTop    = new Color(0.05f, 0.07f, 0.10f);
        public static readonly Color BgBottom = new Color(0.20f, 0.16f, 0.10f);
        public static readonly Color Panel    = new Color(0.07f, 0.09f, 0.12f, 0.94f);
        public static readonly Color PanelEdge= new Color(0.78f, 0.62f, 0.31f, 0.85f);
        public static readonly Color TextPrim = new Color(0.96f, 0.94f, 0.86f);
        public static readonly Color TextDim  = new Color(0.74f, 0.78f, 0.74f);
        public static readonly Color TextWarn = new Color(0.95f, 0.55f, 0.40f);
        public static readonly Color BtnPrim  = new Color(0.20f, 0.55f, 0.42f);
        public static readonly Color BtnGhost = new Color(0.16f, 0.18f, 0.22f, 0.92f);
        public static readonly Color ZoneIdle = new Color(0.13f, 0.15f, 0.19f, 0.92f);
        public static readonly Color ZoneSel  = new Color(0.22f, 0.36f, 0.30f, 0.98f);
        public static readonly Color Accent   = new Color(0.95f, 0.78f, 0.36f);

        // ── Builders (placeholder skin until .fui packages are authored) ──

        /// <summary>Card panel: solid fill + 1px gold edge, no .fui asset required.</summary>
        public static GComponent Card(float w, float h)
        {
            var c = new GComponent();
            c.SetSize(w, h);
            c.opaque = true;

            var bg = new GGraph();
            bg.SetSize(w, h);
            bg.DrawRect(1, PanelEdge, Panel);
            c.AddChild(bg);
            return c;
        }

        public static GTextField H1(string text)
        {
            var t = new GTextField();
            t.text = text;
            t.textFormat = new TextFormat { color = Accent, size = 28, bold = true, align = AlignType.Left };
            t.ApplyFormat();
            t.autoSize = AutoSizeType.Both;
            return t;
        }

        public static GTextField H2(string text)
        {
            var t = new GTextField();
            t.text = text;
            t.textFormat = new TextFormat { color = TextPrim, size = 18, bold = true, align = AlignType.Left };
            t.ApplyFormat();
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
                size  = 13,
                align = AlignType.Left,
            };
            t.ApplyFormat();
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
            bg.SetSize(w, h);
            bg.DrawRect(0, Color.clear, baseCol);
            btn.AddChild(bg);

            var label = new GTextField();
            label.SetSize(w, h);
            label.text = text;
            label.textFormat = new TextFormat
            {
                color = textCol, size = 14, bold = true,
                align = AlignType.Center, verticalAlign = VertAlignType.Middle,
            };
            label.ApplyFormat();
            btn.AddChild(label);

            var hover = new Color(Mathf.Min(1, baseCol.r * 1.18f), Mathf.Min(1, baseCol.g * 1.18f), Mathf.Min(1, baseCol.b * 1.18f), baseCol.a);
            btn.onRollOver.Add(() => bg.DrawRect(0, Color.clear, hover));
            btn.onRollOut.Add(() => bg.DrawRect(0, Color.clear, baseCol));
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
            row.SetSize(rowW, 28);

            var l = new GTextField();
            l.SetPosition(0, 4);
            l.SetSize(110, 24);
            l.text = label;
            l.textFormat = new TextFormat { color = TextDim, size = 13, align = AlignType.Left };
            l.ApplyFormat();
            row.AddChild(l);

            var bg = new GGraph();
            bg.SetPosition(112, 0);
            bg.SetSize(rowW - 112, 28);
            bg.DrawRect(1, new Color(1, 1, 1, 0.18f), new Color(1, 1, 1, 0.06f));
            row.AddChild(bg);

            var input = new GTextInput();
            input.SetPosition(120, 2);
            input.SetSize(rowW - 132, 24);
            input.text = value;
            input.displayAsPassword = isPassword;
            input.textFormat = new TextFormat { color = TextPrim, size = 14, align = AlignType.Left };
            input.ApplyFormat();
            row.AddChild(input);

            return (row, input);
        }
    }
}
