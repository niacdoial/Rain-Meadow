using System;
using System.Net;
using System.Linq;
using System.IO;
using Menu;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using RainMeadow.Shared;
using RainMeadow.Shared.Models;
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

                if (object.ReferenceEquals(peer, serverPeer!))
                {
                    try
                    {
                        OnlineManager.QuitWithError("Connection Lost...", true);
                    }
                    catch (Exception except) // who decided that QuitWithError was responsible for throwing a error?
                        {}
                    serverPeer = null;
                    return;
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

            public RouterLobbyInfo(SecuredPeerId endPoint, string name, LobbyParameters parameters) :
                base(name, parameters.Mode, 1, parameters.PasswordProtected, parameters.MaxPlayers, parameters.Mods, parameters.BannedMods)
            {
                this.endPoint = endPoint;
            }

            public LobbyParameters GetParameters()
            {
                return new LobbyParameters()
                {
                    Mode = mode,
                    MaxPlayers = maxPlayerCount,
                    PasswordProtected = hasPassword,
                    Mods = requiredMods,
                    BannedMods = bannedMods,
                    Pinned = false,
                };
            }

            public override bool Equals(LobbyInfo other)
            {
                if (other is RouterLobbyInfo otherrouter) return (endPoint == otherrouter.endPoint);
                return false;
            }
        }

        public void InitializePackets()
        {
            Packet.packetFactory += Packet.RouterFactory;
            JoinRouterLobby.ProcessAction += HandleJoinRouterLobby;
            // LobbyIsEmpty.ProcessAction += OnLobbyServerEmpty;
            RouteSessionData.ProcessAction += HandleRouteSessionData;
            RouterModifyPlayerListPacket.ProcessAction += HandleModifyPlayerList;
            RouterChatMessage.ProcessAction += HandleChatMessage;
            RouterCustomPacket.ProcessAction += HandleCustomData;
        }

        bool ValidateIsFromServer(Packet packet) {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.Router) {
                throw new Exception("subtle failure inbound: inconsistant currentDomain");
            }
            if (serverPeer is null) {
                RainMeadow.Error($"serverPeer is null, cannot check that the packet is from the right peer");
                return false;
            }
            if (packet.processingPeer != serverPeer.id)
            {
                RainMeadow.Error($"Recieved from-server packet from {packet.processingPeer}, not server: {serverPeer.id}");
                return false;
            }
            return true;
        }

        public OnlinePlayer? GetValidatedSenderPlayer(Packet packet, ushort fromRouterID)
        {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.Router) return null;

            if (GetPlayerRouter(fromRouterID, false) is OnlinePlayer player
                && player.id is RouterPlayerId senderID
            ) {
                if (packet.processingPeer == senderID.endPoint) {
                    return player;
                } else if (packet.processingPeer == serverPeer?.id) {
                    // we also tolerate players switching to server-proxying halfway
                    return player;
                } else {
                    RainMeadow.Error($"Possible impersonation: player {fromRouterID} can't come from endpoint {packet.processingPeer}");
                    return null;
                }
            } else {
                return null;
            }
        }

    }
}
