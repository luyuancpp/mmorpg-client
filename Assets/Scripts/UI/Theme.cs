using UnityEngine;
using UnityEngine.UIElements;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Centralised colour / spacing constants for the in-code UI Toolkit screens.
    /// Replace with a real <c>StyleSheet</c> (USS) when the art team ships final visuals.
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
        public static readonly Color BtnPrimH = new Color(0.27f, 0.66f, 0.50f);
        public static readonly Color BtnGhost = new Color(0.16f, 0.18f, 0.22f, 0.92f);
        public static readonly Color BtnGhostH= new Color(0.22f, 0.26f, 0.30f, 0.96f);
        public static readonly Color ZoneIdle = new Color(0.13f, 0.15f, 0.19f, 0.92f);
        public static readonly Color ZoneSel  = new Color(0.22f, 0.36f, 0.30f, 0.98f);
        public static readonly Color Accent   = new Color(0.95f, 0.78f, 0.36f);

        public static void StylePanel(VisualElement e)
        {
            e.style.backgroundColor = Panel;
            e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = 1;
            e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = PanelEdge;
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = 8;
            e.style.paddingTop = e.style.paddingBottom = 18;
            e.style.paddingLeft = e.style.paddingRight = 22;
        }

        public static Label H1(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 30;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = Accent;
            l.style.marginBottom = 4;
            return l;
        }

        public static Label H2(string text)
        {
            var l = new Label(text);
            l.style.fontSize = 18;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = TextPrim;
            l.style.marginTop = 8;
            l.style.marginBottom = 4;
            return l;
        }

        public static Label P(string text, bool dim = true)
        {
            var l = new Label(text);
            l.style.fontSize = 13;
            l.style.color = dim ? TextDim : TextPrim;
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        public static Button PrimaryButton(string text, System.Action click)
        {
            var b = new Button(click) { text = text };
            b.style.backgroundColor = BtnPrim;
            b.style.color = Color.white;
            b.style.unityFontStyleAndWeight = FontStyle.Bold;
            b.style.height = 36;
            b.style.minWidth = 120;
            b.style.marginRight = 6;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 4;
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = BtnPrimH);
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = BtnPrim);
            return b;
        }

        public static Button GhostButton(string text, System.Action click)
        {
            var b = new Button(click) { text = text };
            b.style.backgroundColor = BtnGhost;
            b.style.color = TextPrim;
            b.style.height = 32;
            b.style.minWidth = 90;
            b.style.marginRight = 6;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 4;
            b.style.borderTopWidth = b.style.borderBottomWidth = b.style.borderLeftWidth = b.style.borderRightWidth = 0;
            b.RegisterCallback<MouseEnterEvent>(_ => b.style.backgroundColor = BtnGhostH);
            b.RegisterCallback<MouseLeaveEvent>(_ => b.style.backgroundColor = BtnGhost);
            return b;
        }

        public static TextField LabeledField(string label, string value, bool isPassword = false)
        {
            var f = new TextField(label) { value = value, isPasswordField = isPassword };
            f.style.marginBottom = 4;
            f.labelElement.style.minWidth = 90;
            f.labelElement.style.color = TextDim;
            return f;
        }
    }
}
