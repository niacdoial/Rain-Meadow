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
        static partial void PlatformLanAvailable(ref bool val) { val = NetworkDomain.PlatformPeerManager is not null; }
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
            if (PlatformPeerManager is null) return;
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                SendP2P(toPlayer, new SessionPacket(OnlineManager.serializer.buffer, (ushort)OnlineManager.serializer.Position), BasePeerManager.PacketType.Unreliable);
                OnlineManager.serializer.EndWrite();
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                OnlineManager.serializer.EndWrite();
                throw;
            }

        }

        public override void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, ushort size, BasePeerManager.PacketType sendType)
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
            if (PlatformPeerManager is null) return;
            RainMeadow.DebugMe();
            PeerId[] broadcastables = PlatformPeerManager.GetBroadcastPeerIDs();
            foreach(PeerId broadId in broadcastables)
            {
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    Packet.Encode(packet, writer, broadId);

                    for (int i = 0; i < 4; i++)
                        PlatformPeerManager.Send(memory.GetBuffer(), broadId,
                            BasePeerManager.PacketType.UnreliableBroadcast, false);
                }
            }
        }

        // If using a domain requires you to start a conversation, then any packet sent before before starting a conversation is ignored.
        // otherwise, the parameter "start_conversation" is ignored.
        public void SendP2P(OnlinePlayer player, Packet packet, BasePeerManager.PacketType sendType, bool start_conversation = false)
        {
            if (PlatformPeerManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    Packet.Encode(packet, writer, lanid.endPoint);
                    PlatformPeerManager.Send(memory.GetBuffer(), lanid.endPoint, sendType, start_conversation);
                }
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
                    byte[]? data = PlatformPeerManager.Receive(out PeerId? remoteEndpoint);
                    if (data == null) continue;
                    if (remoteEndpoint is null) continue;

                    using (MemoryStream netStream = new MemoryStream(data))
                    using (BinaryReader netReader = new BinaryReader(netStream))
                    {
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) continue; // nothing to read somehow?
                        Packet.Decode(netReader, remoteEndpoint);
                    }
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                }
            }
        }

        public override void RecieveCustomPacket(PeerId endPoint, CustomPacket packet)
        {
            var maybePlayer = GetPlayerLAN(endPoint);
            if (maybePlayer is OnlinePlayer player) {
                CustomManager.HandlePacket(player, packet);
            }
        }

        public void SendAcknoledgement(OnlinePlayer player)
        {
            if (PlatformPeerManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                PlatformPeerManager.Send(Array.Empty<byte>(), lanid.endPoint,
                    BasePeerManager.PacketType.Reliable, true);
            }
        }

        public override void ForgetPlayer(OnlinePlayer player)
        {
            if (PlatformPeerManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                PlatformPeerManager.ForgetPeer(lanid.endPoint);
            }
        }

        public override void ForgetEverything()
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.ForgetAllPeers();
        }

    }
}
