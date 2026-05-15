using UnityEngine;
using FairyGUI;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Minimal helpers used by AppBootstrap and screen placeholders while the
    /// qdao FairyGUI art is being recut. Keep this file intentionally lean —
    /// no bespoke colours, no per-screen UiId tables, no list helpers. Once
    /// the new package lands, populate UiId / ItemUrl with the real names.
    /// </summary>
    public static class Theme
    {
        public const string UiPackageName = "qdao";
        public const string UiPackagePath = "UI/qdao/qdao";

        // Font stack. SimKai is now bundled in Assets/Resources/Fonts/SimKai.ttf
        // so it works on any platform without depending on the user's installed
        // fonts. FairyGUI looks up font names by:
        //   1. Resources.Load<Font>("Fonts/<name>") — picks up our bundled .ttf
        //   2. Font.CreateDynamicFontFromOSFont(name) — falls back to OS fonts
        // The first name that resolves wins; later names are graceful fallbacks
        // for environments where the bundled font fails to load.
        //
        // Reference comp (q_daoist_login_clear_2560x1080.png) uses a brush-style
        // kaishu for both body and title — SimKai is a clean kaishu that ships
        // with Windows and reads well at all the qdao font sizes.
        public const string BodyFontName  = "SimKai,KaiTi,Microsoft YaHei UI,Microsoft YaHei,SimHei";
        public const string TitleFontName = "SimKai,KaiTi,STXingkai,Microsoft YaHei UI,Microsoft YaHei";

        public static class Art
        {
            public const float ReferenceWidth  = 2560f;
            public const float ReferenceHeight = 1080f;
        }

        /// <summary>
        /// Try to instantiate a published component from the qdao package.
        /// Returns null if the package isn't loaded or the component is
        /// missing — callers should provide a code-built fallback.
        /// </summary>
        public static GComponent TryCreateFromPackage(string componentName)
        {
            if (UIPackage.GetByName(UiPackageName) == null) return null;
            return UIPackage.CreateObject(UiPackageName, componentName) as GComponent;
        }

        public static T Find<T>(GComponent root, string childName) where T : GObject
        {
            if (root == null || string.IsNullOrEmpty(childName)) return null;
            return root.GetChild(childName) as T;
        }
    }
}
