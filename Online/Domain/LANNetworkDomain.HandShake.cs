using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using HarmonyLib;
using Menu;
using RainMeadow.Shared;
using RainMeadow.Shared.Models;

namespace RainMeadow
{

    public partial class LANNetworkDomain : SecuredPeerNetworkDomain
    {

        private List<LANLobbyInfo> lobbies = [];
        public override void RequestLobbyList()
        {
            lobbies.Clear();
            // To create a proper list, we need to send a message to the broadcast endpoint.
            // and wait for responces from possible hosts.
            SendBroadcast(new RequestLobbyPacket());
        }

        public void AddLobby(LANLobbyInfo newlobby)
        {
            bool is_new = true;
            for (int i = 0; i < lobbies.Count; i++)
            {
                if (lobbies[i].Equals(newlobby))
                {
                    lobbies[i] = newlobby;
                    is_new = false;
                }
            }

            if (is_new) lobbies.Add(newlobby);

            OnLobbyListReceivedEvent(true, lobbies.ToArray());
        }


        public void SendLobbyInfo(SecuredPeerId endPoint)
        {
            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner)
            {
                var packet = new InformLobbyPacket(OnlineManager.lobby.participants.Count, GetLobbyParameters());
                for (int i = 0; i < 8; i++)
                {
                    SendPacket(endPoint, packet, PacketReliability.Unreliable, true);
                }
            }
        }

        public override bool canDirectConnect => true;
        public override LobbyInfo GenerateDCLobbyInfo(string connectstr)
        {
            var endpoint = SecuredPeerId.GetPeerIdByName(connectstr);
            if (endpoint != null)
            {
                return new LANLobbyInfo(endpoint, 1, new LobbyParameters());
            }
            else
            {
                throw new FormatException("IP Address format should be public_key@xxx.xxx.xxx.xxx:port");
            }
        }

        public int maxplayercount = 0;
        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount, bool pinned = false)
        {
            NetworkDomain.currentDomain = NetworkDomainType.LAN;
            OnlineManager.LeaveLobby();
            maxplayercount = maxPlayerCount ?? 0;
            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            NetworkDomain.OnLobbyJoinedEvent(true, "");
        }

        public void LobbyAcknoledgedUs(OnlinePlayer owner)
        {
            RainMeadow.DebugMe();
            if (OnlineManager.lobby is null)
            {
                OnlineManager.lobby = new Lobby(
                    new OnlineGameMode.OnlineGameModeType(OnlineManager.currentlyJoiningLobby.mode, false),
                    owner, lobbyPassword);
            }
        }

        string lobbyPassword = "";
        public override void RequestJoinLobby(LobbyInfo lobby, string? password)
        {
            NetworkDomain.currentDomain = NetworkDomainType.LAN;
            OnlineManager.LeaveLobby();
            RainMeadow.DebugMe();
            if (lobby is LANLobbyInfo lobbyinfo)
            {
                lobbyPassword = password ?? "";
                OnlineManager.currentlyJoiningLobby = lobby;
                var lobbyInfo = (LANLobbyInfo)lobby;
                if (lobbyInfo.endPoint == null)
                {
                    RainMeadow.Debug("Failed to join local game...");
                    return;
                }

                RainMeadow.Debug("Sending Request to join lobby...");
                SendP2P(new OnlinePlayer(new LANPlayerId(lobbyInfo.endPoint)),
                    new RequestJoinPacket(OnlineManager.mePlayer.id.name) {boxed=true}, PacketReliability.Reliable);
            }
            else
            {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        public override void HandleLeavingLobby()
        {
            ForgetEverything();
        }

        public override bool canOpenInvitations => false;
        public override void OpenInvitationOverlay()
        {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify(Utils.Translate("You cannot use this feature here."), OnlineManager.instance.manager, null));
        }

    }
}
