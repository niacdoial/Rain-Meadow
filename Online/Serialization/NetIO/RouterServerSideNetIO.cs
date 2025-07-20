using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using RainMeadow;

namespace RainMeadow {
    partial class NetIOPlatform {
        static partial void PlatformRouterServerSideAvailable(ref bool val) {
            val = NetIOPlatform.PlatformUDPManager is not null;
        }
    }

    public class RouterServerSideNetIO : NetIO {

        public OnlinePlayer serverPlayer;
        public RouterServerSideNetIO() {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            NetIOPlatform.PlatformUDPManager.OnPeerForgotten += (peer) => {
                LobbyServer.RemoveContactPlayer(peer);
            };

            RouterPlayerId serverId = new RouterPlayerId(0xffff_ffff_ffff_ffff);
            serverId.endPoint = LANPlayerId.BlackHole;  // by definition we don't know our public IP
            serverId.name = "SERVER";
            serverPlayer = new OnlinePlayer(serverId) {isMe = true};
        }
        bool BasicChecks() {
            if (NetIOPlatform.PlatformUDPManager is null) {
                RainMeadow.Error("Cannot perform action without a functionning UDPPeerManager");
                return false;
            }
            // if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) {
            //     RainMeadow.Error("Action performed on the wrong type of MatchMakingDomain");
            //     return false;
            // }
            return true;
        }

        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            throw new NotImplementedException(); // server-side NetIO has no session data to send
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
            if (!BasicChecks()){
                RainMeadow.Error("basic NetIO checks failed");
                return;
            }

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
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) {
                            // nothing to read, for example as a result of SendAck
                            continue;
                        }
#if IS_SERVER
                        OnlinePlayer player = LobbyServer.GetContactPlayer(iPEndPoint, true);
#else
                        OnlinePlayer player = ((RouterMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router]).GetPlayerRouter(iPEndPoint);
#endif
                        if (player is null)
                        {
                            RainMeadow.Error("Routed player not found! Cannot instantiate new one without heads-up");
                            return;
                        }
                        Packet.Decode(netReader, player);
                    }
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                }
            }
        }

        public void Mainloop() {
            while (true) {
                RecieveData();
                Thread.Sleep(20);
            }
        }

    }
}
