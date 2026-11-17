using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using Menu;
using RainMeadow.Shared;

/// //////////////////////////////////////////
/// NetworkDomain describes the common interface for the middle part of the network stack
/// (or, for steam networking, the wrapper around the steam library): NetworkDomain.
///
/// This layer is responsible for keeping track of the player list (PeerID, name),
/// interpreting messages (packets of the RPC system, chat messages) between players,
/// and in general keeping up with joining/leaving/kicked players,
/// as well as setting up the info necessary to join an existing lobby
/// It is also somewhat responsible for preventing players to impersonate each other.
///
/// It is heavily used by the OnlineManager, which orchestrates the link between the network stack and the game's state changes.

namespace RainMeadow
{

    public abstract partial class NetworkDomain
    {
        public class NetworkDomainType : ExtEnum<NetworkDomainType>
        {
            public NetworkDomainType(string name, bool register) : base(name, register) { }

            public static NetworkDomainType LAN = new NetworkDomainType("Local", true);
            public static NetworkDomainType Router = new NetworkDomainType("Router", true);
            public static NetworkDomainType Steam = new NetworkDomainType("Steam", true);
        };

        static partial void PlatformSteamAvailable(ref bool val);
        static partial void PlatformLanAvailable(ref bool val);
        static partial void PlatformRouterAvailable(ref bool val);


        public static bool isSteamAvailable { get { bool val = false; PlatformSteamAvailable(ref val); return val; } }
        public static bool isLANAvailable { get { bool val = false; PlatformLanAvailable(ref val); return val; } }
        public static bool isRouterAvailable { get { bool val = false; PlatformRouterAvailable(ref val); return val; } }
        public static SecuredPeerManager? PlatformPeerManager { get; private set; }


        public static event LobbyListReceived_t OnLobbyListReceived = delegate { };
        public static event PlayerListReceived_t OnPlayerListReceived = delegate { };
        public static event LobbyJoined_t OnLobbyJoined = delegate { };

        protected static void OnLobbyJoinedEvent(bool ok, string error = "") => OnLobbyJoined?.Invoke(ok, error);
        protected static void OnPlayerListReceivedEvent(MeadowPlayerId[] players) => OnPlayerListReceived?.Invoke(players);
        protected static void OnLobbyListReceivedEvent(bool ok, LobbyInfo[] lobbies) => OnLobbyListReceived?.Invoke(ok, lobbies);

        public static event ChangedMatchMakingDomain_t changedMatchMaker = delegate { };
        public delegate void ChangedMatchMakingDomain_t(NetworkDomainType last, NetworkDomainType current);

        private static NetworkDomainType _Domain = NetworkDomainType.LAN;

        public static NetworkDomainType currentDomain
        {
            get { return _Domain; }
            set
            {
                var last = _Domain;
                _Domain = value;
                changedMatchMaker.Invoke(last, _Domain);
            }
        }
        public static NetworkDomain currentInstance { get => instances[currentDomain]; }
        public static Dictionary<NetworkDomainType, NetworkDomain> instances = new Dictionary<NetworkDomainType, NetworkDomain>();


        public static string CLIENT_KEY = "client";
        public static string CLIENT_VAL = "Meadow_" + RainMeadow.MeadowVersionStr + "routerincompat1";
        public static string NAME_KEY = "name";
        public static string MODE_KEY = "mode";
        public static string MODS_KEY = "mods";
        public static string BANNED_MODS_KEY = "banned_mods";
        public static string PASSWORD_KEY = "password";
        public static string PINNED_KEY = "pinned";
        public static int MAX_LOBBY = 4;

        static public readonly List<NetworkDomainType> supportedDomains = new();
        static public RouterNetworkDomain? Router => instances.GetValueSafe(NetworkDomainType.Router) as RouterNetworkDomain;
        static public LANNetworkDomain? LAN => instances.GetValueSafe(NetworkDomainType.LAN) as LANNetworkDomain;
        static public SteamNetworkDomain? Steam => instances.GetValueSafe(NetworkDomainType.Steam) as SteamNetworkDomain;

        public static void Initialize()
        {
            supportedDomains.Clear();
            instances.Clear();

            try
            {
                PlatformPeerManager = new SecuredPeerManager();
            }
            catch (Exception except)
            {
                RainMeadow.Error(except);
            }


            if (isLANAvailable)
            {
                supportedDomains.Add(NetworkDomainType.LAN);
                instances.Add(NetworkDomainType.LAN, new LANNetworkDomain());
                currentDomain = NetworkDomainType.LAN;
                changedMatchMaker += (last, current) =>
                {
                    // only allow peerIDs without keys for early-handshake direct-connection to LAN lobby hosts
                    PlatformPeerManager.allowKeylessPeerIDs = (current == NetworkDomainType.LAN);
                };
            }

            if (isRouterAvailable) {
                supportedDomains.Add(NetworkDomainType.Router);
                instances.Add(NetworkDomainType.Router, new RouterNetworkDomain());
                currentDomain = NetworkDomainType.Router;
            }

            if (isSteamAvailable)
            {
                instances.Add(NetworkDomainType.Steam, new SteamNetworkDomain());
                supportedDomains.Add(NetworkDomainType.Steam);
                currentDomain = NetworkDomainType.Steam;
            }

            if (!supportedDomains.Any()) throw new Exception("No supported networking domains.");
            OnlineManager.LeaveLobby();
            changedMatchMaker += (last, current) =>
            {
                OnlineManager.LeaveLobby();
            };
        }

        public enum LobbyVisibility
        {
            [Description("Public")]
            Public = 1,
            [Description("Friends Only")]
            FriendsOnly,
            [Description("Invite Only")]
            Private
        }

        public delegate void LobbyListReceived_t(bool ok, LobbyInfo[] lobbies);
        public delegate void PlayerListReceived_t(MeadowPlayerId[] players);
        public delegate void LobbyJoined_t(bool ok, string error = "");

    }
}
