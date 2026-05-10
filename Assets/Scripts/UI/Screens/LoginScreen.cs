using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Placeholder while qdao art is being recut. Replace once new
    /// LoginScreen FUI component lands.
    /// </summary>
    public sealed class LoginScreen : IScreen
    {
        private AppBootstrap _app;
        private GComponent _root;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;
            _root = Theme.TryCreateFromPackage("LoginScreen") ?? BuildBlank();
            return _root;
        }

        public void OnEnter() { }
        public void OnExit() { }
        public void Tick(float dt) { }

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
            label.text = "LoginScreen — awaiting new art";
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
