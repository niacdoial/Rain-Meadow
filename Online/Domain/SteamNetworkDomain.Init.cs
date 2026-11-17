using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Steamworks;
using RWCustom;
using RainMeadow.Shared.Models;

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
            public SteamLobbyInfo(CSteamID id) : base("", 0, new LobbyParameters())
            {
                iD = id;
                try
                {
                    name = Utils.GetTranslatedLobbyName(SteamMatchmaking.GetLobbyData(id, NAME_KEY));
                    playerCount = SteamMatchmaking.GetNumLobbyMembers(id);
                    parameters.Mode = SteamMatchmaking.GetLobbyData(id, MODE_KEY);
                    parameters.PasswordProtected = bool.TryParse(SteamMatchmaking.GetLobbyData(id, PASSWORD_KEY), out var hasPass) && hasPass;
                    parameters.MaxPlayers = SteamMatchmaking.GetLobbyMemberLimit(id);
                    parameters.Mods = SteamMatchmaking.GetLobbyData(id, MODS_KEY);
                    parameters.BannedMods = SteamMatchmaking.GetLobbyData(id, BANNED_MODS_KEY);

                    // REVIEW: is the following block something that has been removed upstream, or something that was not re-added to the RouterDomain branch?
                    if (NetworkDomain.instances[NetworkDomain.NetworkDomainType.Steam].IsTrustedCommunity(new SteamPlayerId(SteamMatchmaking.GetLobbyOwner(iD))))
                    {
                        if (bool.TryParse(SteamMatchmaking.GetLobbyData(iD, PINNED_KEY), out parameters.Pinned))
                        {
                            RainMeadow.Debug("Successfully read pinned lobby data");
                        }
                    }
                }
                catch (Exception except)
                {
                    RainMeadow.Error(except);
                }
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
