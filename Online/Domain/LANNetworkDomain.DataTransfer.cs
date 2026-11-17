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
        public void SendP2P(OnlinePlayer player, Packet packet, PacketReliability sendType)
        {
            if (PlatformPeerManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                SendPacket(lanid.endPoint, packet, sendType, false);
            }
        }

        public override bool canSendChatMessages => true;
        public override void SendChatMessage(string message)
        {
            foreach (OnlinePlayer player in OnlineManager.players)
            {
                if (player.isMe) continue;
                SendP2P(player, new ChatMessagePacket(message){boxed=true}, PacketReliability.Reliable);
            }

            RecieveChatMessage(OnlineManager.mePlayer, message);
        }
    }
}
