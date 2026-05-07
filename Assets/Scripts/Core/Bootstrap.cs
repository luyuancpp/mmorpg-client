using MmorpgClient.Game;
using UnityEngine;

namespace MmorpgClient.Core
{
    /// <summary>
    /// Single persistent root for the running client. Survives scene loads
    /// via <see cref="Object.DontDestroyOnLoad"/> so the gate TCP connection
    /// is not torn down when the player crosses scene boundaries (login
    /// screen -> world -> dungeon).
    ///
    /// Wire-up:
    ///   1. Create a "Bootstrap" scene with one empty GameObject.
    ///   2. Add this component. It auto-creates the GameClient and starts
    ///      the login UI scene.
    ///   3. Other scenes pull <see cref="Instance"/> rather than newing up
    ///      <see cref="GameClient"/> themselves.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrap : MonoBehaviour
    {
        public static Bootstrap Instance { get; private set; }
        public GameClient Client { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            MmorpgLogger.Init();
            MmorpgLogger.MinLevel = ClientSettings.LogLevel;
            MmorpgLogger.Info($"client boot, log={MmorpgLogger.LogPath}", "boot");

            Client = new GameClient(ClientSettings.GatewayBaseUrl);
            Client.OnLog += s => MmorpgLogger.Info(s, "net");
            Application.targetFrameRate = 60;
        }

        private void Update() => Client?.Tick();

        private void OnApplicationQuit()
        {
            Client?.Disconnect();
            MmorpgLogger.Info("client quit", "boot");
        }
    }
}
