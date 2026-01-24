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
    public partial class LANNetworkDomain
    {
        // REVIEW: is this method redundant?
        public void SendP2P(OnlinePlayer player, Packet packet, PacketReliability sendType, bool boxed = false)
        {
            if (PlatformPeerManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    Packet.Encode(packet, writer, lanid.endPoint);
                    PlatformPeerManager.Send(memory.GetBuffer(), lanid.endPoint,
                        sendType switch
                        {
                            PacketReliability.Reliable => SecuredPeerManager.PacketFlags.Reliable,
                            _ => SecuredPeerManager.PacketFlags.Unreliable,
                        },

                        boxed);
                }
            }
        }

        public override bool canSendChatMessages => true;
        public override void SendChatMessage(string message)
        {
            foreach (OnlinePlayer player in OnlineManager.players)
            {
                if (player.isMe) continue;
                SendP2P(player, new ChatMessagePacket(message), PacketReliability.Reliable, true);
            }

            RecieveChatMessage(OnlineManager.mePlayer, message);
        }
    }
}
