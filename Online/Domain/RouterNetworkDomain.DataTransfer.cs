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
    public partial class RouterNetworkDomain
    {

        public void HandleRouteSessionData(RouteSessionData packet)
        {
            var maybePlayer = GetValidatedSenderPlayer(packet, packet.fromRouterID);
            if (OnlineManager.lobby is not null && maybePlayer is OnlinePlayer player)
            {
                if (packet.toRouterID != ((RouterPlayerId)OnlineManager.mePlayer.id).routingID) {
                    RainMeadow.Error("mis-received a packet meant for " + packet.toRouterID.ToString());
                    return;
                }

                unsafe
                {
                    fixed (byte* data = packet.data.Array)
                    {
                        maybePlayer.UpdateSessionBuffer((IntPtr)(data + packet.data.Offset), packet.data.Count);
                    }
                }
            }
        }

        public void HandleChatMessage(RouterChatMessage packet) {
            var maybePlayer = GetValidatedSenderPlayer(packet, packet.fromRouterID);
            if (maybePlayer is OnlinePlayer player) {
                RecieveChatMessage(player, packet.message);
            }
        }

        public void HandleCustomData(RouterCustomPacket packet) {
            if (packet.key == "" || packet.data == null)
            {
                return;
            }
            if (packet.key.Length > 16 || packet.data.Count > 32768)
            {
                RainMeadow.Error($"Custom Packet was too large, the maximum size is 32768");
                return;
            }
            var maybePlayer = GetValidatedSenderPlayer(packet, packet.fromRouterID);
            if (maybePlayer is OnlinePlayer player) {
                if (packet.toRouterID != ((RouterPlayerId)OnlineManager.mePlayer.id).routingID) {
                    RainMeadow.Error("mis-received a packet meant for " + packet.toRouterID.ToString());
                    return;
                }
                // convert the RouterCustomPacket into a CustomPacket to process it further
                CustomManager.HandlePacket(player, new CustomPacket(packet.key, packet.data));
            }
        }

        SecuredPeerManager.RemotePeer? serverPeer = null;
        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            if (PlatformPeerManager is null) return;
            if (serverPeer is null) throw new InvalidProgrammerException("No lobby server");
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                var playerID = (RouterPlayerId)toPlayer.id;
                byte[] buffer = new byte[OnlineManager.serializer.Position];
                Buffer.BlockCopy(OnlineManager.serializer.buffer, 0, buffer, 0, (int)OnlineManager.serializer.Position);
                var myId = (RouterPlayerId)OnlineManager.mePlayer.id;
                var routerPacket = new RouteSessionData(
                    playerID.routingID,
                    myId.routingID,
                    new ArraySegment<byte>(buffer, 0, (int)OnlineManager.serializer.Position)
                );  // TODO: detect if personal data is passed through (lobby password, player names, etc.)

                SendPacket(playerID.endPoint is null? serverPeer.id : playerID.endPoint, routerPacket, PacketReliability.Unreliable);
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

        public override void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, PacketReliability sendType, bool boxed = false)
        {
            if (PlatformPeerManager is null) return;
            if (serverPeer is null) throw new InvalidProgrammerException("No lobby server");
            try
            {
                RouterPlayerId playerID = (RouterPlayerId)toPlayer.id;
                RouterPlayerId meID = (RouterPlayerId)OnlineManager.mePlayer.id;
                var packet = new RouterCustomPacket(playerID.routingID, meID.routingID, key, new ArraySegment<byte>(data, 0, data.Length));
                packet.boxed = boxed;
                SendPacket(playerID.endPoint is null? serverPeer.id : playerID.endPoint, packet, sendType);
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        public override bool canSendChatMessages => true;
        public override void SendChatMessage(string message)
        {
            if (serverPeer is null) throw new InvalidProgrammerException("serverPeer is null");
            var packet = new RouterChatMessage(
                ((RouterPlayerId)OnlineManager.mePlayer.id).routingID,
                message
            ) {boxed=true};

            SendPacket(serverPeer.id, packet, PacketReliability.Reliable);
            RecieveChatMessage(OnlineManager.mePlayer, message);
        }

    }
}
