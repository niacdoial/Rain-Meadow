using System;
using System.Net;

namespace RainMeadow
{
    // trimmed down version for listing lobbies in menus
    public abstract class LobbyInfo : IEquatable<LobbyInfo>
    {
        public abstract NetworkDomain.NetworkDomainType domain { get; }
        public string name;
        public string mode;
        public int playerCount;
        public bool hasPassword;
        public int maxPlayerCount;
        public string requiredMods;
        public string bannedMods;        
        public bool pinned;

        public LobbyInfo(string name, string mode, int playerCount, bool hasPassword, int? maxPlayerCount, string highImpactMods = "", string bannedMods = "")
        {
            this.name = name;
            this.mode = mode;
            this.playerCount = playerCount;
            this.hasPassword = hasPassword;
            this.maxPlayerCount = (int)maxPlayerCount;
            this.requiredMods = highImpactMods;
            this.bannedMods = bannedMods;
        }

        public abstract string directJoinCode { get; }
        public override bool Equals(object obj) => obj is LobbyInfo other ? this.Equals(other) : false;
        public abstract bool Equals(LobbyInfo other);
    }
}
