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
                        NetworkDomain.Router.LobbyAcknoledgedUs(packet.assignedRoutingID);
                    }
                }
            }

        }

        public void HandleRouteSessionData(RouteSessionData packet)
        {
            if (!ValidateIsFromServer(packet)) return;  // TODO: change once P2P is enabled

            if (packet.toRouterID != ((RouterPlayerId)OnlineManager.mePlayer.id).routingID) {
                RainMeadow.Error("mis-received a packet meant for " + packet.toRouterID.ToString());
                return;
            }
            if (OnlineManager.lobby is not null)
            {

                if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.Router) return;
                if (NetworkDomain.Router?.GetPlayerRouter(packet.fromRouterID, false) is OnlinePlayer p)
                {
                    var size = packet.size;
                    Buffer.BlockCopy(packet.data, 0, OnlineManager.serializer.buffer, 0, size);
                    OnlineManager.serializer.ReadData(p, size);
                }
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
                        NetworkDomain.Router.AcknoledgeRouterPlayer(NetworkDomain.Router.GetPlayerRouter(packet.routerIds[i], true));
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
                // todo nat stuff
                var playerID = (RouterPlayerId)toPlayer.id;
                var myId = (RouterPlayerId)OnlineManager.mePlayer.id;
                Send(serverPeer, new RouteSessionData(
                    playerID.routingID,
                    myId.routingID,
                    OnlineManager.serializer.buffer,
                    (ushort)OnlineManager.serializer.Position
                ), UDPPeerManager.PacketType.Unreliable);
                OnlineManager.serializer.EndWrite();
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                OnlineManager.serializer.EndWrite();
                throw;
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
            // todo NAT stuff
        }

        public override void ForgetEverything()
        {
            if (PlatformUDPManager is null) return;
            PlatformUDPManager.ForgetAllPeers();
            serverPeer = null;
        }
    }
}
