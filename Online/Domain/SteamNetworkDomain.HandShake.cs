using RWCustom;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RainMeadow
{
    public partial class SteamNetworkDomain : NetworkDomain
    {

        public override void RequestLobbyList()
        {
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListStringFilter(CLIENT_KEY, CLIENT_VAL, ELobbyComparison.k_ELobbyComparisonEqual);
            m_RequestLobbyListCall.Set(SteamMatchmaking.RequestLobbyList());
        }

        private void LobbyListReceived(LobbyMatchList_t pCallback, bool bIOFailure)
        {
            try
            {
                RainMeadow.DebugMe();
                LobbyInfo[] lobbies = new LobbyInfo[pCallback.m_nLobbiesMatching];
                if (!bIOFailure)
                {
                    for (int i = 0; i < pCallback.m_nLobbiesMatching; i++)
                    {
                        CSteamID id = SteamMatchmaking.GetLobbyByIndex(i);
                        lobbies[i] = new SteamLobbyInfo(id);
                    }
                }

                OnLobbyListReceivedEvent(!bIOFailure, lobbies);
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount, bool pinned = false)
        {
            NetworkDomain.currentDomain = NetworkDomainType.Steam;
            OnlineManager.LeaveLobby();

            creatingWithMode = gameMode;
            creatingPinned = pinned;
            lobbyPassword = password;
            MAX_LOBBY = (int)maxPlayerCount;
            ELobbyType eLobbyTypeeLobbyType = visibility switch
            {
                LobbyVisibility.Private => ELobbyType.k_ELobbyTypePrivate,
                LobbyVisibility.Public => ELobbyType.k_ELobbyTypePublic,
                LobbyVisibility.FriendsOnly => ELobbyType.k_ELobbyTypeFriendsOnly,
                _ => throw new ArgumentException()
            };
            m_CreateLobbyCall.Set(SteamMatchmaking.CreateLobby(eLobbyTypeeLobbyType, 16));
        }

        public override bool canOpenInvitations => true;
        public override void OpenInvitationOverlay()
        {
            SteamFriends.ActivateGameOverlayInviteDialog(lobbyID);
        }

        public override bool canDirectConnect => true;
        public override LobbyInfo GenerateDCLobbyInfo(string connectstr)
        {
            if (ulong.TryParse(connectstr, out var result))
            {
                return new SteamLobbyInfo(new CSteamID(result));
            }
            else
            {
                throw new FormatException("Connection should use a valid SteamID64");
            }
        }

        public override void RequestJoinLobby(LobbyInfo lobby, string? password)
        {
            NetworkDomain.currentDomain = NetworkDomainType.Steam;
            OnlineManager.LeaveLobby();

            lobbyPassword = password;
            m_JoinLobbyCall.Set(SteamMatchmaking.JoinLobby((lobby as SteamLobbyInfo).iD));
        }

        private static string creatingWithMode;
        private static bool creatingPinned;
        private static string? lobbyPassword;
        private void LobbyCreated(LobbyCreated_t param, bool bIOFailure)
        {
            try
            {
                RainMeadow.DebugMe();
                if (!bIOFailure && param.m_eResult == EResult.k_EResultOK)
                {
                    RainMeadow.Debug("success");
                    lobbyID = new CSteamID(param.m_ulSteamIDLobby);
                    SteamMatchmaking.SetLobbyData(lobbyID, CLIENT_KEY, CLIENT_VAL);
                    SteamMatchmaking.SetLobbyData(lobbyID, NAME_KEY, SteamFriends.GetPersonaName());
                    SteamMatchmaking.SetLobbyData(lobbyID, MODE_KEY, creatingWithMode);
                    SteamMatchmaking.SetLobbyData(lobbyID, MODS_KEY, RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()));
                    SteamMatchmaking.SetLobbyData(lobbyID, BANNED_MODS_KEY, RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods()));
                    SteamMatchmaking.SetLobbyData(lobbyID, PASSWORD_KEY, lobbyPassword != null ? "true" : "false");
                    SteamMatchmaking.SetLobbyData(lobbyID, PINNED_KEY, creatingPinned? "true" : "false");
                    SteamMatchmaking.SetLobbyMemberLimit(lobbyID, MAX_LOBBY);
                    OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(creatingWithMode), OnlineManager.mePlayer, lobbyPassword);
                    SteamFriends.SetRichPresence("connect", lobbyID.ToString());
                    OnLobbyJoinedEvent(true);
                }
                else
                {
                    RainMeadow.Debug("failure, error code is " + param.m_eResult);
                    OnlineManager.lobby = null;
                    lobbyID = default;
                    OnLobbyJoinedEvent(false, param.m_eResult.ToString());
                }
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        private void LobbyConnected(LobbyEnter_t param, bool bIOFailure)
        {
            try
            {

                if (OnlineManager.currentlyJoiningLobby is not SteamLobbyInfo steamLobby)
                {
                    RainMeadow.Error("Cannot join lobby that does not");
                    OnlineManager.LeaveLobby();
                    return;
                }

                if (steamLobby.iD.m_SteamID != param.m_ulSteamIDLobby)
                {
                    RainMeadow.Error("Cannot join lobby that does not match the currently joined.");
                    OnlineManager.LeaveLobby();
                    return;
                }

                if (!bIOFailure)
                {
                    RainMeadow.Debug("success");
                    lobbyID = new CSteamID(param.m_ulSteamIDLobby);
                    UpdatePlayersList();
                    var mode = new OnlineGameMode.OnlineGameModeType(SteamMatchmaking.GetLobbyData(lobbyID, MODE_KEY));
                    var owner = GetLobbyOwner();
                    if (owner == OnlineManager.mePlayer)
                    {
                        SteamMatchmaking.SetLobbyData(lobbyID, CLIENT_KEY, CLIENT_VAL);
                        SteamMatchmaking.SetLobbyData(lobbyID, NAME_KEY, SteamFriends.GetPersonaName());
                    }
                    SteamFriends.SetRichPresence("connect", lobbyID.ToString());

                    OnlineManager.lobby = new Lobby(mode, owner, lobbyPassword);
                }
                else
                {
                    RainMeadow.Debug("failure");
                    OnlineManager.LeaveLobby();
                    OnLobbyJoinedEvent(false, ((EChatRoomEnterResponse)param.m_EChatRoomEnterResponse).ToString());
                }
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        private void SessionRequest(SteamNetworkingMessagesSessionRequest_t param)
        {
            try
            {
                var id = new CSteamID(param.m_identityRemote.GetSteamID64());
                RainMeadow.Debug("session request from " + id);
                if (OnlineManager.lobby != null)
                {
                    if (OnlineManager.players.FirstOrDefault(op => (op.id as SteamPlayerId).steamID == id) is OnlinePlayer p)
                    {
                        RainMeadow.Debug("accepted session from " + p.id.name);
                        SteamNetworkingMessages.AcceptSessionWithUser(ref param.m_identityRemote);
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        private void GameLobbyJoinRequested(GameLobbyJoinRequested_t param)
        {
            if (NetworkDomain.currentDomain != NetworkDomainType.Steam)
            {
                NetworkDomain.currentDomain = NetworkDomainType.Steam;
                OnlineManager.LeaveLobby();
            }

            try
            {
                if (param.m_steamIDLobby.m_SteamID == lobbyID.m_SteamID)
                {
                    RainMeadow.Debug("trying to rejoin same lobby, ignoring, id: " + param.m_steamIDLobby);
                    return;
                }

                RainMeadow.Debug("trying to join lobby from steam with id: " + param.m_steamIDLobby);

                if (lobbyID != default)
                {
                    HandleLeavingLobby();
                }

                OnlineManager.currentlyJoiningLobby = new SteamLobbyInfo(param.m_steamIDLobby);
                Custom.rainWorld.processManager.RequestMainProcessSwitch(RainMeadow.Ext_ProcessID.LobbySelectMenu);

                m_JoinLobbyCall.Set(SteamMatchmaking.JoinLobby(param.m_steamIDLobby));
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        public override void HandleLeavingLobby()
        {
            RainMeadow.DebugMe();
            if (lobbyID != default)
            {
                SteamMatchmaking.LeaveLobby(lobbyID);
            }
            lobbyID = default;
            SteamFriends.ClearRichPresence();
            ForgetEverything();
        }
    }
}
