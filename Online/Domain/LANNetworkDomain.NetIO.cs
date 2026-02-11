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
                    // Packet.Type.SessionEnd => new SessionEndPacket(),
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

        public void SendP2P(OnlinePlayer player, Packet packet, PacketReliability sendType)
        {
            if (PlatformPeerManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                SendPacket(lanid.endPoint, packet, sendType, false);
            }
        }
    }
}
