using System;
using System.Net;
using System.Linq;
using System.IO;
using Menu;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using RainMeadow.Shared;
using System.Net.Sockets;

/// //////////////////////////////////////////////////
/// NetworkDomain describes the common interface for the middle part of the network stack
/// (or, for steam networking, the wrapper around the steam library): NetworkDomain.
/// This file describes the variant for Routed networking.
///
/// placeholder lobby creation process
/// host->server: BeginRouterSession
/// server->host: LobbyIsEmpty
/// server->host: RouterModifyPlayerListPacket (inform host of player ID)
///
/// handshake process:
/// player->server: BeginRouterSession
/// server->player: RouterModifyPlayerListPacket (inform of all player IDs/names/PeerIDs)
/// server->players: RouterModifyPlayerListPacket (inform of new player IDs/names/PeerIDs)
/// server->player: JoinRouterLobby (lobby data plus player's ID)
/// player sets up lobby object
/// player{RPC} -> host{RPC}: .RequestedLobby()
/// host checks the password
/// host{RPC} -> player{RPC}: .JoinLobby()

namespace RainMeadow
{

    public partial class RouterNetworkDomain : SecuredPeerNetworkDomain
    {
        public RouterNetworkDomain()
        {
            InitializePackets();
            if (PlatformPeerManager is null) throw new InvalidProgrammerException("no peer manager");
            PlatformPeerManager.OnPeerForgotten += (SecuredPeerManager.RemotePeer peer) => 
            {
                // ignore player removal / recursive call if we are leaving the lobby
                if (serverPeer == null) return;

                if (peer.id == serverPeer) 
                {
                    OnlineManager.QuitWithError("Connection Lost...");
                }

                // first, check if this endpoint is managed by the current NetworkDomain
                // then, check if the peer timed out or if we booted them already
                // if it's just a timeout, then fall back on proxied communication
                if (GetPlayerFromPeerID(peer.id) is OnlinePlayer player) 
                {
                    if (!OnlineManager.players.Contains(player)) { return; }
                    RouterPlayerId peerId = (RouterPlayerId)player.id;
                    RainMeadow.Error("Peer " + peerId.routingID.ToString() + " lost direct connection, falling back to proxied connection");
                    RainMeadow.Error("Note: some Reliable packets may have been lost. Enjoy the jank!");
                    peerId.endPoint = null;
                }
            };
        }




        public class RouterLobbyInfo : LobbyInfo
        {
            public override NetworkDomainType domain => NetworkDomainType.Router;
            public override string directJoinCode => endPoint.ToString();

            public SecuredPeerId endPoint;
            public RouterLobbyInfo(SecuredPeerId endPoint, string name, string mode, int playerCount, bool hasPassword, int maxPlayerCount, string highImpactMods = "", string bannedMods = "") :
                base(name, mode, playerCount, hasPassword, maxPlayerCount, highImpactMods, bannedMods)
            {
                this.endPoint = endPoint;
            }

            public override bool Equals(LobbyInfo other)
            {
                if (other is RouterLobbyInfo otherrouter) return (endPoint == otherrouter.endPoint);
                return false;
            }
        }

        public class RouterPlayerId : MeadowPlayerId
        {
            public ushort routingID;
            public SecuredPeerId? endPoint;
            public RouterPlayerId(ushort routingID) : base(
                    UsernameGenerator.GenerateRandomUsername(routingID))
            {
                this.routingID = routingID;
                endPoint = null;
            }

            public override void OpenProfileLink()
            {
                string dialogue = "RoutingID: " + routingID.ToString();
                if (routingID == (((RouterPlayerId)OnlineManager.mePlayer.id)?.routingID ?? 0)) {
                    dialogue = "my " + dialogue;
                }
                OnlineManager.instance.manager.ShowDialog(
                    new DialogNotify(dialogue, new Vector2(478.1f, 115.200005f * (1 + 0.2f * 8)),
                        OnlineManager.instance.manager, null));
            }

            public override int GetHashCode()
            {
                return routingID;
            }

            public override void CustomSerialize(Serializer serializer)
            {
                serializer.Serialize(ref routingID);
            }

            public override bool Equals(MeadowPlayerId other)
            {
                if (other is RouterPlayerId id)
                {
                    return routingID == id.routingID;
                }

                return false;
            }
        }

        public override OnlinePlayer CreateMePlayer()
        {
            return new OnlinePlayer(new RouterPlayerId(0)
                { name = RainMeadow.rainMeadowOptions.LanUserName.Value })
                { isMe = true };
            // note: we don't set our IP here, because it's not useful to anyone else (because NAT)
        }


        static List<RouterLobbyInfo> lobbyinfo = new();
        public override void RequestLobbyList()
        {
            lobbyinfo.Clear();
        }

        public override SecuredPeerId GetPeerIDFromPlayer(OnlinePlayer player)
        {
            throw new NotImplementedException();
        }

        public override OnlinePlayer? GetPlayerFromPeerID(SecuredPeerId id) => OnlineManager.players.FirstOrDefault(p =>
        {
            if (p.id is RouterPlayerId route) return route.endPoint?.CompareAndUpdate(id) ?? false;
            return false;
        });

        public OnlinePlayer? GetPlayerRouter(ushort routingID, bool create = false)
        {
            var player = OnlineManager.players.FirstOrDefault(p =>
            {
                if (p.id is RouterPlayerId route)
                    if (route.routingID != 0) return route.routingID == routingID;
                return false;
            });

            if (player is null && create)
            {
                RainMeadow.Debug($"Couldn't find player with routing ID {routingID}. Creating one...");
                player = new OnlinePlayer(new RouterPlayerId(routingID));
            }

            return player;
        }

        public override bool canSendChatMessages => true;
        public override void SendChatMessage(string message)
        {
            if (serverPeer is null) throw new InvalidProgrammerException("serverPeer is null");
            // bool needSendToServer = false;
            var packet = new RouterChatMessage(
                ((RouterPlayerId)OnlineManager.mePlayer.id).routingID,
                message
            );


            // since we're broadcasting this anyway, let's delegate the work to the server completey.
            // this way we don't get any duplicate messages.

            // foreach (OnlinePlayer player in OnlineManager.players)
            // {
            //     if (player.isMe) continue;
            //     RouterPlayerId playerId = (RouterPlayerId)player.id;
            //     if (playerId.endPoint == serverPeer) {
            //         needSendToServer = true;
            //     } else {
            //         Send(playerId.endPoint, packet, SecuredPeerId..PacketType.Reliable, false);
            //     }
            // }

            SendPacket(serverPeer, packet, PacketReliability.Reliable);
            RecieveChatMessage(OnlineManager.mePlayer, message);
        }

        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount, bool pinned)
        {
            NetworkDomain.OnLobbyJoinedEvent(false, "Create servers via the command line for now.");



            // maxplayercount = maxPlayerCount ?? 0;
            // OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            // NetworkDomain.OnLobbyJoinedEvent(true, "");
        }

        public void LobbyAcknoledgedUs(ushort mePlayerid)
        {
            RainMeadow.DebugMe();
            if (((RouterPlayerId)OnlineManager.mePlayer.id).routingID == 0)
            {
                OnlineManager.players.Remove(OnlineManager.mePlayer);
                OnlineManager.mePlayer = GetPlayerRouter(mePlayerid, false);
                if (OnlineManager.mePlayer is null)
                {
                    OnlineManager.QuitWithError("Recieved connection packets out of order:" +
                        "list of players (with just our player ID inside) should arrive before arrival ack", true);
                    return;
                }
                OnlineManager.mePlayer.id.name = RainMeadow.rainMeadowOptions.LanUserName.Value;
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
                if (joiningId.routingID != ((RouterPlayerId)OnlineManager.mePlayer.id).routingID) {
                    // RainMeadow.Debug("No NAT-piercing needed for self");
                } else if (joiningId.endPoint != null) {
                    RainMeadow.Debug("Piercing for peer " + joiningId.routingID.ToString() + " at " + joiningId.endPoint);
                    SendAcknoledgement(joiningId.endPoint);
                } else {
                    RainMeadow.Debug("peer " + joiningId.routingID.ToString() + " hidden by router");
                }
            }
        }

        public void RemoveRouterPlayer(OnlinePlayer leavingPlayer)
        {
            if (leavingPlayer.isMe) return;
            if (!OnlineManager.players.Contains(leavingPlayer)) { return; }
            OnlineManager.RemovePlayer(leavingPlayer);
            ForgetPlayer(leavingPlayer);
            OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
        }

        public override bool canDirectConnect => true;
        public override LobbyInfo GenerateDCLobbyInfo(string connectstr)
        {
            var endpoint = SecuredPeerId.GetPeerIdByName(connectstr);
            if (endpoint != null)
            {
                return new RouterLobbyInfo(endpoint, "Direct Connection", "Meadow", 0, true, 2);
            }
            else
            {
                if (PlatformPeerManager is SecuredPeerManager) {
                    throw new FormatException("IP Address format should be superLongStringThatIsTheServerPublicKey@xxx.xxx.xxx.xxx:port or xxx.xxx.xxx.xxx:port");
                } else {
                    throw new FormatException("IP Address format should be xxx.xxx.xxx.xxx:port");
                }
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
                serverPeer = routerLobbyInfo.endPoint;

                RainMeadow.Debug("Sending Request to join lobby...");
                string meName = OnlineManager.mePlayer.id.name;
                SendPacket(serverPeer, new BeginRouterSession(RainMeadow.rainMeadowOptions.RouterExposeIP.Value, meName), PacketReliability.Reliable);
            }
            else
            {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        public override void AcceptOrRejectPlayer(OnlinePlayer player, bool accept) {
            if (player.id is RouterPlayerId joiningId)
            if (GetLobbyOwner() == OnlineManager.mePlayer) {
                PlayerJoiningDecision.Decision decision = accept switch
                {
                    true => PlayerJoiningDecision.Decision.Accept,
                    false => PlayerJoiningDecision.Decision.Reject
                };

                SendPacket(
                    serverPeer,
                    new PlayerJoiningDecision(joiningId.routingID, decision),
                    PacketReliability.Reliable
                );
            }
        }

        public override void HandleLeavingLobby()
        {
            serverPeer = null;
            ForgetEverything();
        }

        public override OnlinePlayer? GetLobbyOwner()
        {
            if (OnlineManager.lobby == null) return null;
            if (OnlineManager.lobby.owner is null || OnlineManager.lobby.owner.hasLeft)
            {
                // select a new owner.
                // The order of players should be
                for (int i = 0; i < OnlineManager.players.Count; i++)
                {
                    OnlinePlayer onlinePlayer = OnlineManager.players[i];
                    if (onlinePlayer.hasLeft) continue;
                    return onlinePlayer;
                }

                return null;
            }

            return OnlineManager.lobby.owner;
        }

        public override MeadowPlayerId GetEmptyId()
        {
            return new RouterPlayerId(0);
        }

        public override void OpenInvitationOverlay()
        {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify(Utils.Translate("You cannot use this feature here."), OnlineManager.instance.manager, null));
        }

        public override bool canOpenInvitations => false;
    }
}
