using UnityEngine;

namespace MmorpgClient.Core
{
    /// <summary>
    /// PlayerPrefs-backed user-facing settings (gateway URL, last account,
    /// preferred zone). All keys are namespaced under "mmorpg." to avoid
    /// collisions with other Unity scripts.
    ///
    /// Anything secret (refresh tokens, passwords) MUST NOT be stored here:
    /// PlayerPrefs is plain-text on every supported platform. Use OS keystore
    /// integrations (Keychain on iOS, EncryptedSharedPreferences on Android,
    /// DPAPI on Windows) for credentials -- see <c>SecureStore</c> stub
    /// follow-up in the production roadmap.
    /// </summary>
    public static class ClientSettings
    {
        private const string K_Gateway  = "mmorpg.gateway";
        private const string K_Account  = "mmorpg.account";
        private const string K_Zone     = "mmorpg.zone";
        private const string K_LogLevel = "mmorpg.loglevel";

        public static string GatewayBaseUrl
        {
            get => PlayerPrefs.GetString(K_Gateway, "http://127.0.0.1:8080");
            set { PlayerPrefs.SetString(K_Gateway, value ?? ""); PlayerPrefs.Save(); }
        }

        public static string LastAccount
        {
            get => PlayerPrefs.GetString(K_Account, "");
            set { PlayerPrefs.SetString(K_Account, value ?? ""); PlayerPrefs.Save(); }
        }

        public static uint ZoneId
        {
            get => (uint)PlayerPrefs.GetInt(K_Zone, 1);
            set { PlayerPrefs.SetInt(K_Zone, (int)value); PlayerPrefs.Save(); }
        }

        public static MmorpgLogger.LogLevel LogLevel
        {
            get => (MmorpgLogger.LogLevel)PlayerPrefs.GetInt(K_LogLevel,
                Debug.isDebugBuild ? (int)MmorpgLogger.LogLevel.Debug : (int)MmorpgLogger.LogLevel.Info);
            set { PlayerPrefs.SetInt(K_LogLevel, (int)value); PlayerPrefs.Save(); }
        }
    }
}
