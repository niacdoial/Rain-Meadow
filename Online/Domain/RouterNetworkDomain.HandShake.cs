using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using Menu;
using RainMeadow.Shared;
using RainMeadow.Shared.Models;

namespace RainMeadow
{

    public partial class RouterNetworkDomain
    {

        static List<RouterLobbyInfo> lobbyinfo = new();
        public override void RequestLobbyList()
        {
            lobbyinfo.Clear();
        }

        // public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount, bool pinned)
        // {
        //     NetworkDomain.OnLobbyJoinedEvent(false, "Create servers via the command line for now.");

        //     // maxplayercount = maxPlayerCount ?? 0;
        //     // OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
        //     // NetworkDomain.OnLobbyJoinedEvent(true, "");
        // }

        public override bool canOpenInvitations => false;
        public override void OpenInvitationOverlay()
        {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify(Utils.Translate("You cannot use this feature here."), OnlineManager.instance.manager, null));
        }

        public override bool canDirectConnect => true;
        public override LobbyInfo GenerateDCLobbyInfo(string connectstr)
        {
            var endpoint = SecuredPeerId.GetPeerIdByName(connectstr);
            if (endpoint != null)
            {
                return new RouterLobbyInfo(endpoint, "Router Lobby", 1, new LobbyParameters());
            }
            else
            {
                throw new FormatException("IP Address format should be public_key@xxx.xxx.xxx.xxx:port");
            }
        }

        string lobbyPassword = "";
        public override void RequestJoinLobby(LobbyInfo lobby, string? password)
        {
            NetworkDomain.currentDomain = NetworkDomainType.Router;
            OnlineManager.LeaveLobby();

            if (lobby is RouterLobbyInfo routerLobbyInfo)
            {
                lobbyPassword = password ?? "";
                OnlineManager.currentlyJoiningLobby = lobby;
                if (routerLobbyInfo.endPoint == null)
                {
                    RainMeadow.Debug("Failed to join local game...");
                    return;
                }
                serverPeer = PlatformPeerManager.GetRemotePeer(routerLobbyInfo.endPoint, true);

                RainMeadow.Debug("Sending Request to join lobby...");
                string meName = RainMeadow.rainMeadowOptions.LanUserName.Value;
                if (string.IsNullOrWhiteSpace(meName)) meName = UsernameGenerator.GenerateRandomUsername(PlatformPeerManager.Me.GetHashCode());
                var packet = new BeginRouterSession(RainMeadow.rainMeadowOptions.RouterExposeIP.Value, meName, null) {boxed=true};
                SendPacket(serverPeer.id, packet, PacketReliability.Reliable);
            }
            else
            {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        public void HandleJoinRouterLobby(JoinRouterLobby packet)
        {
            RainMeadow.DebugMe();
            if (!ValidateIsFromServer(packet)) return;

            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.Router) return;
            var newLobbyInfo = new RouterLobbyInfo(packet.processingPeer, packet.name, 0, packet.lobbyParameters);
            // If we don't have a lobby and we a currently joining a lobby
            if (OnlineManager.lobby is null && OnlineManager.currentlyJoiningLobby is not null)
            {
                // If the lobby we want to join is a router lobby
                if (OnlineManager.currentlyJoiningLobby is RouterNetworkDomain.RouterLobbyInfo oldLobbyInfo)
                {
                    // If the lobby we want to join is the lobby that allowed us to join.
                    if (oldLobbyInfo.endPoint.CompareAndUpdate(newLobbyInfo.endPoint))
                    {
                        OnlineManager.currentlyJoiningLobby = newLobbyInfo;
                        LobbyAcknoledgedUs(packet.assignedRoutingID);
                    }
                }
            }
        }

        public void LobbyAcknoledgedUs(ushort mePlayerid)
        {
            RainMeadow.DebugMe();
            if (((RouterPlayerId)OnlineManager.mePlayer.id).routingID == 0)
            {
                OnlineManager.players.Remove(OnlineManager.mePlayer);
                OnlineManager.mePlayer = GetPlayerRouter(mePlayerid);
                if (OnlineManager.mePlayer is null)
                {
                    OnlineManager.QuitWithError("Recieved connection packets out of order:" +
                        "list of players (with just our player ID inside) should arrive before arrival ack", true);
                    return;
                }
                OnlineManager.mePlayer.isMe = true;
            }

            foreach (var player in OnlineManager.players)
            {
                RainMeadow.Debug($"{player}, {((RouterPlayerId)player.id).routingID}");
            }

            var owner = OnlineManager.players.First();
            if (OnlineManager.lobby is null)
            {
                OnlineManager.lobby = new Lobby(
                    new OnlineGameMode.OnlineGameModeType(OnlineManager.currentlyJoiningLobby.mode, false),
                    owner, lobbyPassword);
            }
        }

        public void NATPierce(OnlinePlayer joiningPlayer)
        {
            if (joiningPlayer.id is RouterPlayerId joiningId) {
                // if all two players send unprompted packets to their respective endpoints, it should pierce NAT layers on both sides,
                // allowing them to communicate.
                if (joiningId.routingID == ((RouterPlayerId)OnlineManager.mePlayer.id).routingID) {
                    // RainMeadow.Debug("No NAT-piercing needed for self");
                } else if (joiningId.endPoint != null) {
                    RainMeadow.Debug("Piercing for peer " + joiningId.routingID.ToString() + " at " + joiningId.endPoint);
                    SendAcknoledgement(joiningId.endPoint);
                } else {
                    RainMeadow.Debug("peer " + joiningId.routingID.ToString() + " hidden by router");
                }
            }
        }

        public override void HandleLeavingLobby()
        {
            serverPeer = null;
            ForgetEverything();
        }

    }
}
