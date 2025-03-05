using System.Collections.Generic;
using RainMeadow.Shared;

namespace RainMeadow
{
    partial class NetIOPlatform {
        static partial void PlatformSteamAvailable(ref bool val);
        static partial void PlatformLanAvailable(ref bool val);
        static partial void PlatformRouterAvailable(ref bool val);


        public static bool isSteamAvailable { get { bool val = false; PlatformSteamAvailable(ref val); return val; } }
        public static bool isLANAvailable { get { bool val = false; PlatformLanAvailable(ref val); return val; } }
        public static bool isRouterAvailable { get { bool val = false; PlatformRouterAvailable(ref val); return val; } }


        public static UDPPeerManager? PlatformUDPManager { get; } = new();
    }
    public abstract class NetIO
    {
        public static NetIO? currentInstance { get => instances[MatchmakingManager.currentDomain]; }
        public static Dictionary<MatchmakingManager.MatchMakingDomain, NetIO> instances = new();

        public enum SendType : byte
        {
            Reliable,
            Unreliable,
        }

        public static void InitializesNetIO() {
            if (NetIOPlatform.isLANAvailable) instances.Add(MatchmakingManager.MatchMakingDomain.LAN, new LANNetIO());
            // if (NetIOPlatform.isRouterAvailable) instances.Add(MatchmakingManager.MatchMakingDomain.Router, new RouterNetIO());
            if (NetIOPlatform.isSteamAvailable) instances.Add(MatchmakingManager.MatchMakingDomain.Steam, new SteamNetIO());   
        }

        public virtual void SendSessionData(OnlinePlayer toPlayer) {}
        public virtual void ForgetPlayer(OnlinePlayer player) {}
        public virtual void ForgetEverything() {}
        public virtual void Update() {}
    }
}
