using System.Collections.Generic;
using MmorpgClient.Net;

namespace MmorpgClient.UI
{
    /// <summary>
    /// Cross-screen mutable state. Owned by <see cref="AppBootstrap"/>, passed
    /// into every screen so they can read selections from prior steps and
    /// stash their own outputs without a global singleton.
    /// </summary>
    public sealed class SessionModel
    {
        public string Account  = "demo";
        public string Password = "demo";
        public string GatewayBaseUrl = "http://127.0.0.1:8080";

        public readonly List<ServerListZone> Zones = new();
        public int SelectedZoneIndex = -1;
        public ServerListZone SelectedZone =>
            (SelectedZoneIndex >= 0 && SelectedZoneIndex < Zones.Count) ? Zones[SelectedZoneIndex] : null;

        public readonly List<AnnouncementItem> Announcements = new();

        public int    RoleArchetypeIndex = 0;
        public string RoleNickname = "云行客";

        public uint  StartSceneConfigId = 1;
        public ulong StartSceneId       = 0;
    }
}
