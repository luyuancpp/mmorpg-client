using System;
using UnityEngine;

namespace MmorpgClient.UI
{
    public static class QdaoUiText
    {
        private const string ResourcePath = "UI/qdao/qdao_ui_text";
        private static TextTable _table;

        [Serializable]
        private sealed class TextTable
        {
            public TextEntry[] entries;
        }

        [Serializable]
        private sealed class TextEntry
        {
            public string key;
            public string value;
        }

        public static string Get(string key, string fallback)
        {
            EnsureLoaded();
            if (_table?.entries == null)
                return fallback;

            foreach (var entry in _table.entries)
            {
                if (entry != null && entry.key == key)
                    return string.IsNullOrEmpty(entry.value) ? fallback : entry.value;
            }
            return fallback;
        }

        private static void EnsureLoaded()
        {
            if (_table != null)
                return;

            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogWarning($"[QdaoUiText] Text config not found: Resources/{ResourcePath}.json");
                _table = new TextTable { entries = Array.Empty<TextEntry>() };
                return;
            }

            try
            {
                _table = JsonUtility.FromJson<TextTable>(asset.text) ?? new TextTable { entries = Array.Empty<TextEntry>() };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[QdaoUiText] Failed to parse text config: {ex.Message}");
                _table = new TextTable { entries = Array.Empty<TextEntry>() };
            }
        }
    }
}