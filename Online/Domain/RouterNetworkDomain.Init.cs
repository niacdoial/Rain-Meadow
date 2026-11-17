using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;

using HarmonyLib;
using Menu;
using UnityEngine;
using RainMeadow.Shared;
using RainMeadow.Shared.Models;


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
    public partial class NetworkDomain
    {
        static partial void PlatformRouterAvailable(ref bool val) { val = NetworkDomain.PlatformPeerManager is not null; }
    }

    public partial class RouterNetworkDomain : SecuredPeerNetworkDomain
    {

        public RouterNetworkDomain()
        {
            InitializePackets();
            if (PlatformPeerManager is null) throw new InvalidProgrammerException("no peer manager");
            PlatformPeerManager.OnPeerForgotten += (SecuredPeerManager.RemotePeer peer, string reason) =>
            {
                // ignore player removal / recursive call if we are leaving the lobby
                if (serverPeer == null) return;

                if (object.ReferenceEquals(peer, serverPeer!))
                {
                    try
                    {
                        if (reason.Length > 0)
                        {
                            OnlineManager.QuitWithError("Connection failure. The server sent this error message:\n" + reason, true);
                        }
                        else
                        {
                            OnlineManager.QuitWithError("Connection Lost...", true);
                        }
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
                    if (reason.Length > 0)
                    {
                        RainMeadow.Error("It send the following error: " + reason);
                    }
                    RainMeadow.Error("Note: some Reliable packets may have been lost. Enjoy the jank!");
                    peerId.endPoint = null;
                }
            };
        }

        public class RouterLobbyInfo : LobbyInfo
        {
            public override NetworkDomainType domain => NetworkDomainType.Router;
            public override string directJoinCode => endPoint.ToString(false);

            public SecuredPeerId endPoint;
            public RouterLobbyInfo(SecuredPeerId endPoint, string name, int playerCount, LobbyParameters parameters) :
                base(name, playerCount, parameters)
            {
                this.endPoint = endPoint;
            }

            //public RouterLobbyInfo(SecuredPeerId endPoint, string name, LobbyParameters parameters) :
            //    base(name, parameters.Mode, 1, parameters.PasswordProtected, parameters.MaxPlayers, parameters.Mods, parameters.BannedMods)
            //{
            //    this.endPoint = endPoint;
            //}

            //public LobbyParameters GetParameters()
            //{
            //    return new LobbyParameters()
            //    {
            //        Mode = mode,
            //        MaxPlayers = maxPlayerCount,
            //        PasswordProtected = hasPassword,
            //        Mods = requiredMods,
            //        BannedMods = bannedMods,
            //        Pinned = false,
            //    };
            //}

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
            RouteSessionData.ProcessAction += HandleRouteSessionData;
            RouterModifyPlayerListPacket.ProcessAction += HandleModifyPlayerList;
            RouterChatMessage.ProcessAction += HandleChatMessage;
            RouterCustomPacket.ProcessAction += HandleCustomData;
            PublishRouterLobby.ProcessAction += NotAServer;
            BeginRouterSession.ProcessAction += NotAServer;
        }

        void NotAServer(Packet packet) {
            var peer = PlatformPeerManager.GetRemotePeer(packet.processingPeer);
            if (peer != null) {
                PlatformPeerManager.TerminatePeer(peer, "Please connect to a server instead");
            }
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

            if (GetPlayerRouter(fromRouterID) is OnlinePlayer player
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



        public override void RecieveData()
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.Update();
            int packetlimit = RainMeadow.rainMeadowOptions.UdpMaxPacketsPerUpdate.Value;
            for (int i = 0; (i < packetlimit) && PlatformPeerManager.IsPacketAvailable(); i++)
            {
                try
                {
                    byte[]? data = PlatformPeerManager.Receive(out SecuredPeerId? remoteEndpoint, out bool boxed);
                    if (data == null) continue;
                    if (remoteEndpoint is null) continue;

                    using (MemoryStream netStream = new MemoryStream(data))
                    using (BinaryReader netReader = new BinaryReader(netStream))
                    {
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) continue; // nothing to read somehow?
                        Packet.Decode(netReader, remoteEndpoint, PlatformPeerManager.Me, boxed);
                    }
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                    OnlineManager.serializer.EndRead();
                }
            }
        }
    }
}
