using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using HarmonyLib;
using Menu;
using RainMeadow.Shared;

namespace RainMeadow
{
    public partial class NetworkDomain
    {
        static partial void PlatformRouterAvailable(ref bool val) { val = NetworkDomain.PlatformUDPManager is not null; }
    }

    public partial class RouterNetworkDomain
    {

        public void InitializePackets()
        {
            Packet.packetFactory += Packet.RouterFactory;
            JoinRouterLobby.ProcessAction += HandleJoinRouterLobby;
            LobbyIsEmpty.ProcessAction += OnLobbyServerEmpty;
            RouteSessionData.ProcessAction += HandleRouteSessionData;
            RouterModifyPlayerListPacket.ProcessAction += HandleModifyPlayerList;
        }

        bool ValidateIsFromServer(Packet packet) {
            if (serverPeer is null) return false;
            if (!UDPPeerManager.CompareIPEndpoints(packet.processingEndpoint, serverPeer))
            {
                RainMeadow.Error($"Recieved host packet not from server: {packet.processingEndpoint}, server: {serverPeer}");
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
                if (UDPPeerManager.CompareIPEndpoints(packet.processingEndpoint, senderID.endPoint)) {
                    return player;
                } else if (UDPPeerManager.CompareIPEndpoints(packet.processingEndpoint, serverPeer)) {
                    // we also tolerate players switching to server-proxying halfway
                    return player;
                } else {
                    RainMeadow.Error(
                        "Possible impersonation: player " + fromRouterID.ToString()
                        + " can't come from endpoint " + packet.processingEndpoint.ToString()
                    );
                    return null;
                }
            } else {
                return null;
            }
        }


        public void HandleJoinRouterLobby(JoinRouterLobby packet)
        {
            RainMeadow.DebugMe();
            if (!ValidateIsFromServer(packet)) return;

            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.Router) return;
            var newLobbyInfo = new RouterLobbyInfo(packet.processingEndpoint, packet.name, packet.mode, 1, packet.passwordprotected, packet.maxplayers, packet.mods, packet.bannedMods);
            // If we don't have a lobby and we a currently joining a lobby
            if (OnlineManager.lobby is null && OnlineManager.currentlyJoiningLobby is not null)
            {
                // If the lobby we want to join is a lan lobby
                if (OnlineManager.currentlyJoiningLobby is RouterNetworkDomain.RouterLobbyInfo oldLobbyInfo)
                {
                    // If the lobby we want to join is the lobby that allowed us to join.
                    if (UDPPeerManager.CompareIPEndpoints(oldLobbyInfo.endPoint, newLobbyInfo.endPoint))
                    {
                        OnlineManager.currentlyJoiningLobby = newLobbyInfo;
                        LobbyAcknoledgedUs(packet.assignedRoutingID);
                    }
                }
            }

        }

        public void HandleRouteSessionData(RouteSessionData packet)
        {
            var maybePlayer = GetValidatedSenderPlayer(packet, packet.fromRouterID);
            if (OnlineManager.lobby is not null && maybePlayer is OnlinePlayer player)
            {
                if (packet.toRouterID != ((RouterPlayerId)OnlineManager.mePlayer.id).routingID) {
                    RainMeadow.Error("mis-received a packet meant for " + packet.toRouterID.ToString());
                    return;
                }

                var size = packet.size;
                Buffer.BlockCopy(packet.data, 0, OnlineManager.serializer.buffer, 0, size);
                OnlineManager.serializer.ReadData(player, size);
            }
        }

        public void HandleModifyPlayerList(RouterModifyPlayerListPacket packet)
        {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.Router) return;
            if (!ValidateIsFromServer(packet)) return;

            switch (packet.operation)
            {
                case RouterModifyPlayerListPacket.Operation.Add:
                    for (int i = 0; i < packet.routerIds.Count; i++)
                    {
                        OnlinePlayer player = NetworkDomain.Router.GetPlayerRouter(packet.routerIds[i], true);
                        RouterPlayerId playerID = (RouterPlayerId)player.id;
                        if (UDPPeerManager.CompareIPEndpoints(packet.endPoints[i], SharedPlatform.BlackHole)) {
                            playerID.endPoint = serverPeer;
                        } else {
                            playerID.endPoint = packet.endPoints[i];
                        }
                        playerID.name = packet.userNames[i];
                        NetworkDomain.Router.AcknoledgeRouterPlayer(player);
                    }
                    break;

                case RouterModifyPlayerListPacket.Operation.Remove:
                    for (int i = 0; i < packet.routerIds.Count; i++)
                    {
                        NetworkDomain.Router.RemoveRouterPlayer(NetworkDomain.Router.GetPlayerRouter(packet.routerIds[i], true));
                    }
                    break;
            }
        }


        IPEndPoint? serverPeer = null;
        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            if (PlatformUDPManager is null) return;
            if (serverPeer is null) throw new InvalidProgrammerException("No lobby server");
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                var playerID = (RouterPlayerId)toPlayer.id;
                var myId = (RouterPlayerId)OnlineManager.mePlayer.id;
                Send(playerID.endPoint, new RouteSessionData(
                    playerID.routingID,
                    myId.routingID,
                    OnlineManager.serializer.buffer,
                    (ushort)OnlineManager.serializer.Position
                ), UDPPeerManager.PacketType.Unreliable);
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
            finally
            {
                OnlineManager.serializer.EndWrite();
            }
        }

        public void Send(IPEndPoint endPoint, Packet packet, UDPPeerManager.PacketType sendType, bool start_conversation = false)
        {
            if (PlatformUDPManager is null) return;
            using (MemoryStream memory = new MemoryStream(128))
            using (BinaryWriter writer = new BinaryWriter(memory))
            {
                Packet.Encode(packet, writer, endPoint);
                PlatformUDPManager.Send(memory.GetBuffer(), endPoint, sendType, start_conversation);
            }
        }

        public void SendEmptyPacket(IPEndPoint endPoint, UDPPeerManager.PacketType sendType, bool start_conversation = false)
        {
            if (PlatformUDPManager is null) return;
            PlatformUDPManager.Send(Array.Empty<byte>(), endPoint, sendType, start_conversation);
        }

        public override void RecieveData()
        {
            if (PlatformUDPManager is null) return;
            PlatformUDPManager.Update();

            int packetlimit = 4; // TODO: Add to remix menu
            for (int i = 0; (i < packetlimit) && PlatformUDPManager.IsPacketAvailable(); i++)
            {
                try
                {
                    byte[]? data = PlatformUDPManager.Recieve(out EndPoint? remoteEndpoint);
                    if (data == null) continue;
                    IPEndPoint? iPEndPoint = remoteEndpoint as IPEndPoint;
                    if (iPEndPoint is null) continue;


                    using (MemoryStream netStream = new MemoryStream(data))
                    using (BinaryReader netReader = new BinaryReader(netStream))
                    {
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) continue; // nothing to read somehow?
                        Packet.Decode(netReader, iPEndPoint);
                    }
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                    OnlineManager.serializer.EndRead();
                }
            }
        }

        public override void ForgetPlayer(OnlinePlayer player)
        {
            if (PlatformUDPManager is null) return;
            if (player.id is RouterNetworkDomain.RouterPlayerId routid)
            {
                if (routid.endPoint == serverPeer) { return; }  // do not forget the server accidentally!
                PlatformUDPManager.ForgetPeer(routid.endPoint);
            }
        }

        public override void ForgetEverything()
        {
            if (PlatformUDPManager is null) return;
            PlatformUDPManager.ForgetAllPeers();
            serverPeer = null;
        }
    }
}
