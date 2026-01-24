using RWCustom;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RainMeadow
{
    public partial class NetworkDomain
    {
        static partial void PlatformSteamAvailable(ref bool val) { val = SteamManager.Initialized && SteamUser.BLoggedOn(); }
    }

    public partial class SteamNetworkDomain : NetworkDomain
    {
        public class SteamLobbyInfo : LobbyInfo
        {
            public override NetworkDomainType domain => NetworkDomainType.Steam;
            public override string directJoinCode => iD.m_SteamID.ToString();

            public CSteamID iD;
            public SteamLobbyInfo(CSteamID id, string name, string mode, int playerCount, bool hasPassword, int? maxPlayerCount, string highImpactMods = "", string bannedMods = "") :
                base(name, mode, playerCount, hasPassword, maxPlayerCount, highImpactMods, bannedMods)
            {
                iD = id;
                // REVIEW: missing dev detection?
            }
            public override bool Equals(LobbyInfo other)
            {
                if (other is SteamLobbyInfo othersteam) return iD == othersteam.iD;
                return false;
            }
        }

#pragma warning disable IDE0052 // Remove unread private members
        private CallResult<LobbyMatchList_t> m_RequestLobbyListCall;
        private CallResult<LobbyCreated_t> m_CreateLobbyCall;
        private CallResult<LobbyEnter_t> m_JoinLobbyCall;
        private Callback<LobbyDataUpdate_t> m_LobbyDataUpdate;
        private Callback<LobbyChatUpdate_t> m_LobbyChatUpdate;
        private Callback<SteamNetworkingMessagesSessionRequest_t> m_SessionRequest;
        private Callback<GameLobbyJoinRequested_t> m_GameLobbyJoinRequested;
        private Callback<LobbyChatMsg_t> m_LobbyChatMsgCall;
#pragma warning restore IDE0052 // Remove unread private members

        private CSteamID me;
        public CSteamID lobbyID { get; private set; }

        public SteamNetworkDomain()
        {
            SteamNetworkingUtils.InitRelayNetworkAccess();
            RainMeadow.DebugMe();
            m_RequestLobbyListCall = CallResult<LobbyMatchList_t>.Create(LobbyListReceived);
            m_CreateLobbyCall = CallResult<LobbyCreated_t>.Create(LobbyCreated);
            m_JoinLobbyCall = CallResult<LobbyEnter_t>.Create(LobbyConnected);
            m_LobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(LobbyUpdated);
            m_LobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(LobbyChatUpdated);
            m_SessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(SessionRequest);
            m_GameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(GameLobbyJoinRequested);
            m_LobbyChatMsgCall = Callback<LobbyChatMsg_t>.Create(LobbyChatMessageReceived);

            filteringAvailable = SteamUtils.InitFilterText();

            me = SteamUser.GetSteamID();
        }

    }
}
