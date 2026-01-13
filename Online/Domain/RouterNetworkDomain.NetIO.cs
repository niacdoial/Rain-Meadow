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
        static partial void PlatformRouterAvailable(ref bool val) { val = NetworkDomain.PlatformPeerManager is not null; }
    }

    public partial class RouterNetworkDomain
    {

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
            if (packet.processingEndpoint != serverPeer)
            {
                RainMeadow.Error($"Recieved from-server packet from {packet.processingEndpoint}, not server: {serverPeer}");
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
                if (packet.processingEndpoint == senderID.endPoint) {
                    return player;
                } else if (packet.processingEndpoint == serverPeer) {
                    // we also tolerate players switching to server-proxying halfway
                    return player;
                } else {
                    RainMeadow.Error($"Possible impersonation: player {fromRouterID} can't come from endpoint {packet.processingEndpoint}");
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
                    fixed (byte* data = packet.data)
                    {
                        maybePlayer.UpdateSessionBuffer((IntPtr)data, packet.data.Length);
                    }
                }
            }
        }

        public void HandleModifyPlayerList(RouterModifyPlayerListPacket packet)
        {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.Router) return;
            if (!ValidateIsFromServer(packet)) return;

            switch (packet.operation)
            {
                case RouterModifyPlayerListPacket.Operation.Update:
                case RouterModifyPlayerListPacket.Operation.Add:
                    for (int i = 0; i < packet.routerIds.Count; i++)
                    {
                        RouterPlayerId playerID = new RouterPlayerId(packet.routerIds[i]);
                        if (!RainMeadow.rainMeadowOptions.RouterExposeIP.Value || packet.endPoints[i] == null)
                        {
                            playerID.endPoint = null;
                        } 
                        else 
                        {
                            playerID.endPoint = packet.endPoints[i];
                        }
                        playerID.name = packet.userNames[i];

                        OnlinePlayer? addedPlayer = GetPlayerRouter(packet.routerIds[i], false);
                        if (addedPlayer is OnlinePlayer existingPlayer) 
                        {
                            if (packet.operation == RouterModifyPlayerListPacket.Operation.Update) 
                            {
                                // FIXME: add checks once the PeerManager guarantees player identity
                                existingPlayer.id = playerID;
                                RainMeadow.Debug(String.Format("updating player: {0}, name {1}", playerID.routingID, playerID.name));
                            } 
                            else 
                            {
                                RainMeadow.Debug(String.Format("redundant add-player: {0}, 'name' {1}", playerID.routingID, playerID.name));
                            }
                            NATPierce(existingPlayer);  // just in case
                        } 
                        else 
                        {
                            addedPlayer = new OnlinePlayer(playerID);
                            NATPierce(addedPlayer);
                            RainMeadow.Debug(String.Format("new player to acknowledge: {0}, name {1}", playerID.routingID, playerID.name));
                            OnlineManager.AddPlayer(addedPlayer);
                            RainMeadow.Debug($"Added {addedPlayer} to the lobby matchmaking player list");
                        }
                    }
                    break;

                case RouterModifyPlayerListPacket.Operation.Remove:
                    for (int i = 0; i < packet.routerIds.Count; i++)
                    {
                        RemoveRouterPlayer(GetPlayerRouter(packet.routerIds[i], true));
                    }
                    break;
            }
            OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
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
            if (packet.key.Length > 16 || packet.data.Length > 32768)
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
                CustomManager.HandlePacket(player, new CustomPacket(packet.key, packet.data, (ushort)packet.data.Length));
            }
        }

        SecuredPeerId? serverPeer = null;
        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            if (PlatformPeerManager is null) return;
            if (serverPeer is null) throw new InvalidProgrammerException("No lobby server");
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                var playerID = (RouterPlayerId)toPlayer.id;
                var myId = (RouterPlayerId)OnlineManager.mePlayer.id;
                var routerPacket = new RouteSessionData(
                    playerID.routingID,
                    myId.routingID,
                    OnlineManager.serializer.buffer,
                    (ushort)OnlineManager.serializer.Position
                );

                SendPacket(playerID.endPoint is null? serverPeer : playerID.endPoint, routerPacket, PacketReliability.Unreliable);
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
                var packet = new RouterCustomPacket(playerID.routingID, meID.routingID, key, data, (ushort)data.Length);
                packet.boxed = boxed;
                SendPacket(playerID.endPoint is null? serverPeer : playerID.endPoint, packet, sendType);
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        public override void RecieveData()
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.Update();

            int packetlimit = 4; // TODO: Add to remix menu
            for (int i = 0; (i < packetlimit) && PlatformPeerManager.IsPacketAvailable(); i++)
            {
                try
                {
                    byte[]? data = PlatformPeerManager.Receive(out SecuredPeerId? remoteEndpoint, out bool boxed);
                    if (data == null) continue;
                    if (remoteEndpoint is null) continue;
                    serverPeer?.CompareAndUpdate(remoteEndpoint);  // the server might need to be updated on how to be contacted

                    using (MemoryStream netStream = new MemoryStream(data))
                    using (BinaryReader netReader = new BinaryReader(netStream))
                    {
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) continue; // nothing to read somehow?
                        Packet.Decode(netReader, remoteEndpoint, boxed);
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
            if (PlatformPeerManager is null) return;
            if (player.id is RouterNetworkDomain.RouterPlayerId routid)
            {
                if (routid.endPoint == serverPeer) return;   // do not forget the server accidentally!
                if (routid.endPoint is null) return;
                PlatformPeerManager.ForgetPeer(routid.endPoint);
            }
        }

        public override void ForgetEverything()
        {
            base.ForgetEverything();
            //serverPeer = null;  // do not reset server, it can be re-used in "knocking" lobby setup.
        }
    }
}
