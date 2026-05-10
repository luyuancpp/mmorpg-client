using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI.Screens
{
    /// <summary>
    /// Placeholder while qdao art is being recut. Boots a blank canvas with a
    /// status label so AppBootstrap can route here without NRE. Replace this
    /// file once the new SceneScreen FUI component lands.
    /// </summary>
    public sealed class SceneScreen : IScreen
    {
        private AppBootstrap _app;
        private GComponent _root;

        public GComponent Build(AppBootstrap app)
        {
            _app = app;

            // Try to load the placeholder published from the qdao FairyGUI
            // package. If it isn't there yet (no .bytes published) we
            // fall back to a code-built blank canvas.
            _root = Theme.TryCreateFromPackage("SceneScreen") ?? BuildBlank();
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
            label.text = "SceneScreen — awaiting new art";
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
