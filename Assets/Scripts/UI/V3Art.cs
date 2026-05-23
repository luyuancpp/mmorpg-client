using System.Collections.Generic;
using FairyGUI;
using UnityEngine;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Bridge that exposes the qdao v3 atlases (redrawn UI bg pieces, 22
    /// character portraits, 124 weapon icons) to runtime code WITHOUT going
    /// through the published <c>qdao_fui.bytes</c>. The FairyGUI free edition
    /// can't CLI-publish, so the v3 entries injected into package.xml only
    /// exist in source until someone presses F8 in the editor. Meanwhile,
    /// the same PNGs are mirrored under <c>Assets/Resources/UI/qdao_v3/</c>
    /// and loaded here as Unity <see cref="Texture2D"/> wrapped in
    /// <see cref="NTexture"/> so they can drop straight into a
    /// <see cref="GLoader"/> via <see cref="GLoader.texture"/>.
    ///
    /// Once the package is republished, callers can swap to
    /// <c>ui://qdao/&lt;name&gt;</c> URLs and this helper can retire.
    /// </summary>
    public static class V3Art
    {
        public const string ResourceRoot = "UI/qdao_v3";

        private static readonly Dictionary<string, NTexture> _cache = new();

        // ── Canonical names (without .png) ──────────────────────────

        // UI background slices
        public const string UiTabActive       = "ui/tab_button_green_active_v3";
        public const string UiListRowIdle     = "ui/list_row_idle_v3";
        public const string UiListRowActive   = "ui/list_row_active_v3";
        public const string UiCardWideIdle    = "ui/server_card_wide_idle_v3";
        public const string UiCardWideActive  = "ui/server_card_wide_active_v3";
        public const string UiCardMedIdle     = "ui/server_card_med_idle_v3";
        public const string UiCardMedActive   = "ui/server_card_med_active_v3";
        public const string UiBottomBar       = "ui/bottom_bar_v3";
        public const string UiSearchBox       = "ui/search_box_with_icon_v3";
        public const string UiStatusRedDot    = "ui/status_red_dot_v3";
        public const string UiOrnamentFlower  = "ui/ornament_gold_flower_v3";

        /// <summary>22 character portrait names, in display order.</summary>
        public static readonly string[] CharacterNames =
        {
            "01_ice_sword_girl_v3",
            "02_fire_talisman_boy_v3",
            "03_lotus_healer_girl_v3",
            "04_mountain_guardian_boy_v3",
            "05_celestial_musician_girl_v3",
            "06_thunder_caster_boy_v3",
            "07_moon_shadow_assassin_girl_v3",
            "08_alchemy_prodigy_boy_v3",
            "09_bamboo_archer_girl_v3",
            "10_crimson_spear_girl_v3",
            "11_jade_fist_flat_top_boy_v3",
            "12_iron_saber_flat_top_boy_v3",
            "13_short_hair_wind_blade_girl_v3",
            "14_short_hair_snow_summoner_girl_v3",
            "15_water_dragon_scholar_boy_v3",
            "16_golden_bell_dancer_girl_v3",
            "17_ghost_script_calligrapher_boy_v3",
            "18_desert_sun_monk_girl_v3",
            "19_spirit_beast_tamer_boy_v3",
            "20_star_formation_master_girl_v3",
            "21_lidazui_hair_cook_boy_v3",
            "22_lidazui_hair_waiter_saber_boy_v3",
        };

        /// <summary>
        /// Short Chinese display label per character (mirrors the file name's
        /// English slug). Same length and ordering as <see cref="CharacterNames"/>.
        /// </summary>
        public static readonly string[] CharacterLabels =
        {
            "冰剑娘", "火符少", "莲心师", "山岳卫",
            "天音姬", "雷诀生", "月影刺", "炼丹郎",
            "竹影射", "赤缨枪", "玉拳郎", "铁刀客",
            "风刃娘", "雪召娘", "水龙生", "金铃舞",
            "鬼篆生", "炽阳尼", "灵兽童", "星阵姬",
            "厨修生", "刀肆郎",
        };

        /// <summary>
        /// 22 starter-weapon icon names, picked from the 124-icon pack
        /// to roughly match each character's flavour (sword/talisman/staff
        /// /spear/etc.). Keep in step with <see cref="CharacterNames"/>.
        /// </summary>
        public static readonly string[] StarterWeaponNames =
        {
            "icons_weapon/007_flying_sword",          // 01 ice sword girl
            "icons_weapon/016_talisman_dagger",       // 02 fire talisman boy
            "icons_weapon/010_lotus_seat",            // 03 lotus healer girl
            "icons_weapon/008_holy_hammer",           // 04 mountain guardian boy
            "icons_weapon/005_folding_fan",           // 05 celestial musician girl
            "icons_weapon/014_golden_bell_hammer",    // 06 thunder caster boy
            "icons_weapon/024_moon_sickle",           // 07 moon shadow assassin girl
            "icons_weapon/018_medicine_gourd_flask",  // 08 alchemy prodigy boy
            "icons_weapon/002_spear",                 // 09 bamboo archer girl
            "icons_weapon/002_spear",                 // 10 crimson spear girl
            "icons_weapon/006_iron_claw",             // 11 jade fist boy
            "icons_weapon/001_daoist_saber",          // 12 iron saber boy
            "icons_weapon/021_cloud_fan",             // 13 short hair wind blade girl
            "icons_weapon/019_lotus_lamp",            // 14 short hair snow summoner girl
            "icons_weapon/022_dragon_claw",           // 15 water dragon scholar boy
            "icons_weapon/014_golden_bell_hammer",    // 16 golden bell dancer girl
            "icons_weapon/013_daoist_whisk",          // 17 ghost script calligrapher boy
            "icons_weapon/025_sun_mace",              // 18 desert sun monk girl
            "icons_weapon/023_tiger_claw",            // 19 spirit beast tamer boy
            "icons_weapon/017_jade_ruyi",             // 20 star formation master girl
            "icons_weapon/008_holy_hammer",           // 21 cook boy (cleaver substitute)
            "icons_weapon/001_daoist_saber",          // 22 waiter saber boy
        };

        // ── Loaders ─────────────────────────────────────────────────

        /// <summary>
        /// Load (and cache) a v3 texture by relative path (no extension).
        /// Returns <c>null</c> + warns if missing.
        /// </summary>
        public static NTexture Get(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath)) return null;
            if (_cache.TryGetValue(relativePath, out var cached) && cached != null && cached.nativeTexture != null)
                return cached;

            var tex = Resources.Load<Texture2D>(ResourceRoot + "/" + relativePath);
            if (tex == null)
            {
                Debug.LogWarning($"[V3Art] missing texture: Resources/{ResourceRoot}/{relativePath}");
                return null;
            }
            var nt = new NTexture(tex);
            _cache[relativePath] = nt;
            return nt;
        }

        /// <summary>
        /// Apply a v3 texture to a <see cref="GLoader"/>. Sets fill to ScaleFree
        /// so 9-grid-style backgrounds stretch over the loader's box.
        /// </summary>
        public static bool Apply(GLoader loader, string relativePath, FillType fill = FillType.ScaleFree)
        {
            if (loader == null) return false;
            var nt = Get(relativePath);
            if (nt == null) return false;
            loader.fill = fill;
            loader.texture = nt;
            return true;
        }

        /// <summary>
        /// Build a <see cref="GLoader"/> filled with a v3 texture, sized and
        /// positioned at the given rect.
        /// </summary>
        public static GLoader MakeLoader(string relativePath, float x, float y, float w, float h,
                                         FillType fill = FillType.ScaleFree)
        {
            var l = new GLoader();
            l.SetSize(w, h);
            l.SetXY(x, y);
            l.fill = fill;
            Apply(l, relativePath, fill);
            return l;
        }
    }
}
