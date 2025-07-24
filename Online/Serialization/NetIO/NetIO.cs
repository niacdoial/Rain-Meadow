using System.Collections.Generic;
using RainMeadow.Shared;

namespace RainMeadow
{
    partial class NetIOPlatform: NetIOPlatform {
        static partial void PlatformSteamAvailable(ref bool val);
        static partial void PlatformLanAvailable(ref bool val);
        static partial void PlatformRouterAvailable(ref bool val);
        static partial void PlatformRouterServerSideAvailable(ref bool val);


        public static bool isSteamAvailable { get { bool val = false; PlatformSteamAvailable(ref val); return val; } }
        public static bool isLANAvailable { get { bool val = false; PlatformLanAvailable(ref val); return val; } }
        public static bool isRouterAvailable { get { bool val = false; PlatformRouterAvailable(ref val); return val; } }
        public static bool isRouterServerSideAvailable { get { bool val = false; PlatformRouterServerSideAvailable(ref val); return val; } }


        public static UDPPeerManager? PlatformUDPManager { get; } = new();

        public static Dictionary<MatchmakingManager.MatchMakingDomain, NetIO> instances = new();
        public static NetIO? currentInstance { get => instances[MatchmakingManager.currentDomain]; }

        public static void InitializesNetIO() {
            if (NetIOPlatform.isLANAvailable) instances.Add(MatchmakingManager.MatchMakingDomain.LAN, new LANNetIO());
            if (NetIOPlatform.isRouterAvailable) instances.Add(MatchmakingManager.MatchMakingDomain.Router, new RouterNetIO());
            if (NetIOPlatform.isSteamAvailable) instances.Add(MatchmakingManager.MatchMakingDomain.Steam, new SteamNetIO());

            heartbeatTime = (ulong)RainMeadow.rainMeadowOptions.UdpHeartbeat.Value;
            timeoutTime = (ulong)RainMeadow.rainMeadowOptions.UdpTimeout.Value;

        }
    }

    // public abstract class SerializableNetIO: NetIO {
    //     public abstract void SendSessionData(OnlinePlayer toPlayer);
    // }

    // public abstract class SerializablePlayerId: MeadowPlayerId, Serializer.ICustomSerializable {
    //     public abstract void CustomSerialize(Serializer serializer);

    //     public virtual void OpenProfileLink() {
    //         OnlineManager.instance.manager.ShowDialog(new DialogNotify(Utils.Translate("This player does not have a profile."), OnlineManager.instance.manager, null));
    //     }
    // }

}
