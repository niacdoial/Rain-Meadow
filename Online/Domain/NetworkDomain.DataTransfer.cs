using System;
using System.Net;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using HarmonyLib;
using Menu;

using RainMeadow.Shared;

namespace RainMeadow
{

    public abstract partial class NetworkDomain
    {
        public abstract void SendSessionData(OnlinePlayer toPlayer);
        public abstract void RecieveData();

        public abstract void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, ushort size, BasePeerManager.PacketType sendType);
        public virtual void RecieveCustomPacket(PeerId endPoint, CustomPacket packet) {
            throw new InvalidProgrammerException("This domain should not process custom data this way");
        }

        public virtual bool canSendChatMessages => false;
        public virtual void FilterMessage(ref string message) { }
        public virtual void SendChatMessage(string message) { }
        public virtual void RecieveChatMessage(OnlinePlayer player, string message)
        {
            ChatLogManager.LogMessage($"{player.id.GetPersonaName()}", $"{message}");
        }
    }
}
