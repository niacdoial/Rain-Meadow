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
        static partial void PlatformLanAvailable(ref bool val) { val = NetworkDomain.PlatformUDPManager is not null; }
    }

    public partial class LANNetworkDomain
    {
        void PacketFactory(Packet.Type type, ref Packet? packet)
        {
            if (packet is null)
            {
                packet = type switch
                {
                    Packet.Type.RequestJoin => new RequestJoinPacket(),
                    Packet.Type.ModifyPlayerList => new ModifyPlayerListPacket(),
                    Packet.Type.JoinLobby => new JoinLobbyPacket(),
                    Packet.Type.Session => new SessionPacket(),
                    Packet.Type.SessionEnd => new SessionEndPacket(),
                    Packet.Type.RequestLobby => new RequestLobbyPacket(),
                    Packet.Type.InformLobby => new InformLobbyPacket(),
                    Packet.Type.ChatMessage => new ChatMessagePacket(),
                    Packet.Type.CustomPacket => new CustomPacket(),

                    _ => null
                };
            }
        }

        public void InitializePackets() {
            Packet.packetFactory += PacketFactory;
        }

        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            if (PlatformUDPManager is null) return;
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                SendP2P(toPlayer, new SessionPacket(OnlineManager.serializer.buffer, (ushort)OnlineManager.serializer.Position), UDPPeerManager.PacketType.Unreliable);
                OnlineManager.serializer.EndWrite();
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                OnlineManager.serializer.EndWrite();
                throw;
            }

        }

        public override void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, ushort size, UDPPeerManager.PacketType sendType)
        {
            try
            {
                SendP2P(toPlayer, new CustomPacket(key, data, size), sendType);
            }

            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }


        public void SendBroadcast(Packet packet)
        {
            if (PlatformUDPManager is null) return;
            RainMeadow.DebugMe();
            for (int broadcast_port = UDPPeerManager.DEFAULT_PORT;
                broadcast_port < (UDPPeerManager.FIND_PORT_ATTEMPTS + UDPPeerManager.DEFAULT_PORT);
                broadcast_port++)
            {
                IPEndPoint point = new(IPAddress.Broadcast, broadcast_port);

                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    Packet.Encode(packet, writer, point);

                    for (int i = 0; i < 4; i++)
                        PlatformUDPManager.Send(memory.GetBuffer(), point,
                            UDPPeerManager.PacketType.UnreliableBroadcast, false);
                }
            }
        }

        // If using a domain requires you to start a conversation, then any packet sent before before starting a conversation is ignored.
        // otherwise, the parameter "start_conversation" is ignored.
        public void SendP2P(OnlinePlayer player, Packet packet, UDPPeerManager.PacketType sendType, bool start_conversation = false)
        {
            if (PlatformUDPManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    Packet.Encode(packet, writer, lanid.endPoint);
                    PlatformUDPManager.Send(memory.GetBuffer(), lanid.endPoint, sendType, start_conversation);
                }
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
                }
            }
        }

        public override void RecieveCustomPacket(IPEndPoint endPoint, CustomPacket packet)
        {
            var maybePlayer = GetPlayerLAN(endPoint);
            if (maybePlayer is OnlinePlayer player) {
                CustomManager.HandlePacket(player, packet);
            }
        }

        public void SendAcknoledgement(OnlinePlayer player)
        {
            if (PlatformUDPManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                PlatformUDPManager.Send(Array.Empty<byte>(), lanid.endPoint,
                    UDPPeerManager.PacketType.Reliable, true);
            }
        }

        public override void ForgetPlayer(OnlinePlayer player)
        {
            if (PlatformUDPManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                PlatformUDPManager.ForgetPeer(lanid.endPoint);
            }
        }

        public override void ForgetEverything()
        {
            if (PlatformUDPManager is null) return;
            PlatformUDPManager.ForgetAllPeers();
        }

    }
}
