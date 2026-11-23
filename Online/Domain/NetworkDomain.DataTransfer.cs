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
        public virtual bool SupportsBoxedEncryption => false;  // REVIEW: either use or discard
        public virtual bool CustomDataSupported => false;  // REVIEW: override this in the individual Domains
        public virtual void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, PacketReliability sendType, bool boxed = false) => throw new NotImplementedException();


        public virtual bool canSendChatMessages => false;
        public virtual void FilterMessage(ref string message) { }
        public virtual string FilterTeamName(string name)
        {
            return name;
        }
        public virtual void SendChatMessage(string message) { }
        public virtual void RecieveChatMessage(OnlinePlayer player, string message)
        {
            if (message.Length > ChatTextBox.textLimit)
           {
               RainMeadow.Error($"Error: {player} tried sending a chat message longer than what is allowed. Message will not be displayed. {message.Length}/{ChatTextBox.textLimit}");
               return;
           }
            ChatLogManager.LogMessage($"{player.id.GetPersonaName()}", $"{message}");
        }
    }
}
