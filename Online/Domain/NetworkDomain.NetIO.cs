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

        public virtual void ForgetPlayer(OnlinePlayer player) { }
        public virtual void ForgetEverything() { }


        public enum PacketReliability
        {
            Unreliable = 0,
            Reliable
        }
        
        public virtual bool CustomDataSupported => false;
        public virtual bool SupportsBoxedEncryption => false;
        public virtual void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, PacketReliability sendType, bool boxed = false) => throw new NotImplementedException();
    }
}
