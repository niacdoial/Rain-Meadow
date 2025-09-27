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

        public abstract void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, ushort size, UDPPeerManager.PacketType sendType);
        public virtual void RecieveCustomPacket(IPEndPoint endPoint, CustomPacket packet) {
            throw new InvalidProgrammerException("This domain should not process custom data this way");
        }
    }
}
