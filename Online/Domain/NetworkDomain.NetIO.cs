using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using HarmonyLib;
using Menu;

namespace RainMeadow
{

    public abstract partial class NetworkDomain
    {
        public abstract void SendSessionData(OnlinePlayer toPlayer);
        public abstract void RecieveData();

        public virtual void ForgetPlayer(OnlinePlayer player) { }
        public virtual void ForgetEverything() { }

    }
}
