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

        public const string BodyFontName  = "Microsoft YaHei UI,Microsoft YaHei,SimHei";
        public const string TitleFontName = "STKaiti,KaiTi,STXingkai,Microsoft YaHei UI,Microsoft YaHei";

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
