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

        public enum PacketReliability
        {
            Unreliable = 0,
            Reliable
        }
        public virtual bool SupportsBoxedEncryption => false;
        public virtual bool CustomDataSupported => false;
        public virtual void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, PacketReliability sendType, bool boxed = false) => throw new NotImplementedException();


        public virtual bool canSendChatMessages => false;
        public virtual void FilterMessage(ref string message) { }
        public virtual void SendChatMessage(string message) { }
        public virtual void RecieveChatMessage(OnlinePlayer player, string message)
        {
            ChatLogManager.LogMessage($"{player.id.GetPersonaName()}", $"{message}");
        }
    }
}
