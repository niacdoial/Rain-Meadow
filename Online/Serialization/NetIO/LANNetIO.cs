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
        static partial void PlatformLanAvailable(ref bool val) {
            val = NetIOPlatform.PlatformUDPManager is not null;
        }
    }

    public class LANNetIO : NetIO {
        static MatchmakingManager.MatchMakingDomain MMDomain = MatchmakingManager.MatchMakingDomain.LAN;
        public LANNetIO() {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            NetIOPlatform.PlatformUDPManager.OnPeerForgotten += (peer) => {
                if (MatchmakingManager.currentDomain != MMDomain) {
                    return;
                }
                List<OnlinePlayer> playerstoRemove = new();
                foreach (OnlinePlayer player in OnlineManager.players) {
                    if (player.id is LANPlayerId lanid) {
                        if (lanid.endPoint is null) continue;
                        if (UDPPeerManager.CompareIPEndpoints(lanid.endPoint, peer)) {
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
                    ((LANMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN]).RemoveLANPlayer(player);
            };
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
            if (NetIOPlatform.PlatformUDPManager is null) return;
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) {
                return;
            }
            RainMeadow.DebugMe();
            for (int broadcast_port = UDPPeerManager.DEFAULT_PORT;
                broadcast_port < (UDPPeerManager.FIND_PORT_ATTEMPTS + UDPPeerManager.DEFAULT_PORT);
                broadcast_port++) {
                IPEndPoint point = new(IPAddress.Broadcast, broadcast_port);

                var player = (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN] as LANMatchmakingManager).GetPlayerLAN(point);
                if (player == null)
                {
                    RainMeadow.Debug("Player not found! Instantiating new at: " + point);
                    var playerid = new LANPlayerId(point);
                    player = new OnlinePlayer(playerid);
                }

                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory)) {
                    Packet.Encode(packet, writer, player);

                    for (int i = 0; i < 4; i++)
                        NetIOPlatform.PlatformUDPManager.Send(
                            memory.GetBuffer(),
                            ((LANPlayerId)player.id).endPoint,
                            UDPPeerManager.PacketType.UnreliableBroadcast,
                            true
                        );
                }
            }
        }

        // If using a domain requires you to start a conversation, then any packet sent before before starting a conversation is ignored.
        // otherwise, the parameter "start_conversation" is ignored.
        public void SendP2P(OnlinePlayer player, Packet packet, SendType sendType, bool start_conversation = false) {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) {
                return;
            }
            if (player.id is LANPlayerId lanid) {
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory)) {
                    Packet.Encode(packet, writer, player);
                    NetIOPlatform.PlatformUDPManager.Send(
                        memory.GetBuffer(),
                        lanid.endPoint,
                        sendType switch {
                            NetIO.SendType.Reliable => UDPPeerManager.PacketType.Reliable,
                            NetIO.SendType.Unreliable => start_conversation? UDPPeerManager.PacketType.UnreliableBroadcast : UDPPeerManager.PacketType.Unreliable,
                            _ => UDPPeerManager.PacketType.Unreliable,
                        },
                        start_conversation
                    );
                }
            }
        }


        public void SendAcknoledgement(OnlinePlayer player) {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) {
                return;
            }

            if (player.id is LANPlayerId lanid) {
                NetIOPlatform.PlatformUDPManager.Send(Array.Empty<byte>(), lanid.endPoint,
                    UDPPeerManager.PacketType.Reliable, true);
            }
        }

        public override void ForgetPlayer(OnlinePlayer player) {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) {
                return;
            }
            if (NetIOPlatform.PlatformUDPManager is null) return;

            if (player.id is LANPlayerId lanid) {
                NetIOPlatform.PlatformUDPManager.ForgetPeer(lanid.endPoint);
            }
        }

        public override void ForgetEverything() {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) {
                return;
            }
            if (NetIOPlatform.PlatformUDPManager is null) return;
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
            if (NetIOPlatform.PlatformUDPManager is null) return;
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) {
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

                    using (MemoryStream netStream = new MemoryStream(data))
                    using (BinaryReader netReader = new BinaryReader(netStream)) {
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) continue; // nothing to read somehow?
                        var player = ((LANMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN]).GetPlayerLAN(iPEndPoint);
                        if (player is null)
                        {
                            RainMeadow.Debug("Player not found! Instantiating new at: " + iPEndPoint.Port);
                            var playerid = new LANPlayerId(iPEndPoint);
                            player = new OnlinePlayer(playerid);
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
}
