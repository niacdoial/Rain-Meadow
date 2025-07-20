using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
//using Kittehface.Framework20;

namespace RainMeadow
{
    partial class NetIOPlatform {
        static partial void PlatformRouterAvailable(ref bool val) {
            val = NetIOPlatform.PlatformUDPManager is not null;
        }
    }

#if !IS_SERVER
    public class RouterNetIO : NetIO {
        static MatchmakingManager.MatchMakingDomain MMDomain = MatchmakingManager.MatchMakingDomain.Router;

        private IPEndPoint serverEndPoint;  // TODO implement a way to update that without powercycling the game
        public const ulong serverRoutingId = 0xffff_ffff_ffff_ffff;
        public OnlinePlayer serverPlayer;
        public RouterNetIO() {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            NetIOPlatform.PlatformUDPManager.OnPeerForgotten += (peer) => {
                if (MatchmakingManager.currentDomain != MMDomain) {
                    return;
                }
                List<OnlinePlayer> playerstoRemove = new();
                foreach (OnlinePlayer player in OnlineManager.players) {
                    if (player.id is RouterPlayerId routid) {
                        if (routid.RoutingId is 0) continue;
                        if (UDPPeerManager.CompareIPEndpoints(routid.endPoint, peer)) {
                            if ((OnlineManager.lobby?.owner is OnlinePlayer owner && owner == player) ||
                                (OnlineManager.lobby?.isOwner ?? true)
                            ) {
                                playerstoRemove.Add(player);

                            }
                            break;  // only one player matches that if block
                        }
                    }
                }

                foreach (var player in playerstoRemove)
                    // this has a built-in test (checking if the player is in OnlineManager)
                    // to determine if the current closure is being called from a timeout or from a voluntary disconnect
                    ((RouterMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router]).RemoveRouterPlayer(player);
            };

            RainMeadow.Debug("what the settings say about the server's IP, *apparently*: " + RainMeadow.rainMeadowOptions.MatchmakingRouter.Value);
            serverEndPoint = UDPPeerManager.GetEndPointByName(
                RainMeadow.rainMeadowOptions.MatchmakingRouter.Value
                //"127.0.0.1:8710"
            );
            RouterPlayerId serverId = new RouterPlayerId(serverRoutingId);
            serverId.endPoint = serverEndPoint;
            serverId.name = "SERVER";
            serverPlayer = new OnlinePlayer(serverId);
        }
        bool BasicChecks() {
            if (NetIOPlatform.PlatformUDPManager is null) {
                RainMeadow.Error("Cannot perform action without a functionning UDPPeerManager");
                return false;
            }
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) {
                RainMeadow.Error("Action performed on the wrong type of MatchMakingDomain");
                return false;
            }
            return true;
        }

        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                SendP2P(toPlayer, new SessionPacket(OnlineManager.serializer.buffer, (ushort)OnlineManager.serializer.Position), SendType.Unreliable);
                OnlineManager.serializer.EndWrite();
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                OnlineManager.serializer.EndWrite();
                throw;
            }
        }

        public void SendBroadcast(Packet packet) {
            throw new NotImplementedException(); // RouterNetIO cannot send packets to Broatcast
        }

        // If using a domain requires you to start a conversation, then any packet sent before before starting a conversation is ignored.
        // otherwise, the parameter "start_conversation" is ignored.
        public void SendP2P(OnlinePlayer player, Packet packet, SendType sendType, bool start_conversation = false) {
            if (!BasicChecks()) return;

            if (player.id is RouterPlayerId routid) {
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory)) {
                    Packet.Encode(packet, writer, player);
                    NetIOPlatform.PlatformUDPManager.Send(
                        memory.GetBuffer(),
                        routid.endPoint,
                        sendType switch {
                            NetIO.SendType.Reliable => UDPPeerManager.PacketType.Reliable,
                            NetIO.SendType.Unreliable => UDPPeerManager.PacketType.Unreliable,
                            _ => UDPPeerManager.PacketType.Unreliable,
                        },
                        start_conversation
                    );
                }
            }
        }

        public void SendToServer(Packet packet, SendType sendType, bool start_conversation = false) {
            if (!BasicChecks()) {
                RainMeadow.Error("failed basic checks for netIO!");
                return;
            }
            RainMeadow.Debug("pkt server send " + packet.type.ToString());

            // TODO add code to make sure this peer is known by the UDPPeerManager as the server
            using (MemoryStream memory = new MemoryStream(128))
            using (BinaryWriter writer = new BinaryWriter(memory)) {
                Packet.Encode(packet, writer, this.serverPlayer);
                NetIOPlatform.PlatformUDPManager.Send(
                    memory.GetBuffer(),
                    this.serverEndPoint,
                    sendType switch {
                        NetIO.SendType.Reliable => UDPPeerManager.PacketType.Reliable,
                        NetIO.SendType.Unreliable => UDPPeerManager.PacketType.Unreliable,
                        _ => UDPPeerManager.PacketType.Unreliable,
                    },
                    start_conversation
                );
            }
        }


        public void SendAcknoledgement(OnlinePlayer player) {
            if (!BasicChecks()) return;

            if (player.id is RouterPlayerId routid) {
                NetIOPlatform.PlatformUDPManager.Send(
                    Array.Empty<byte>(),
                    routid.endPoint,
                    UDPPeerManager.PacketType.Reliable,
                    true
                );
            }
        }
        public void SendServerAcknoledgement() {
            if (!BasicChecks()) return;

            NetIOPlatform.PlatformUDPManager.Send(
                Array.Empty<byte>(),
                this.serverEndPoint,
                UDPPeerManager.PacketType.Reliable,
                true
            );
        }

        public override void ForgetPlayer(OnlinePlayer player) {
            if (!BasicChecks()) return;

            if (player.id is RouterPlayerId routid) {
                NetIOPlatform.PlatformUDPManager.ForgetPeer(routid.endPoint);
            }
        }

        public override void ForgetEverything() {
            if (!BasicChecks()) return;
            NetIOPlatform.PlatformUDPManager.ForgetAllPeers();
        }

        public override void Update()
        {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            NetIOPlatform.PlatformUDPManager.Update();
            base.Update();
        }

        public override void RecieveData()
        {
            if (!BasicChecks()) return;

            while (NetIOPlatform.PlatformUDPManager.IsPacketAvailable())
            {
                try
                {
                    //RainMeadow.Debug("To read: " + UdpPeer.debugClient.Available);
                    byte[]? data = NetIOPlatform.PlatformUDPManager.Recieve(out EndPoint? remoteEndpoint);
                    if (data == null) continue;
                    IPEndPoint? iPEndPoint = remoteEndpoint as IPEndPoint;
                    if (iPEndPoint is null) continue;
                    //, UDPPeerManager.CompareIPEndpoints(iPEndPoint, this.serverEndPoint)
                    using (MemoryStream netStream = new MemoryStream(data))
                    using (BinaryReader netReader = new BinaryReader(netStream)) {
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) continue; // nothing to read somehow?
                        var player = MatchmakingManager.routerInstance.GetPlayerRouter(iPEndPoint);
                        if (player is null) {
                            RainMeadow.Error("Routed player not found! Cannot instantiate new one without heads-up");
                            return;
                        }
                        Packet.Decode(netReader, player);
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
#endif  // !IS_SERVER
}
