using System;
using System.Net;
using RainMeadow.Shared.Models;

namespace RainMeadow
{
    // trimmed down version for listing lobbies in menus
    public abstract class LobbyInfo : IEquatable<LobbyInfo>
    {
        public abstract NetworkDomain.NetworkDomainType domain { get; }
        public string name;

        public int playerCount;
        public LobbyParameters parameters;
        public string mode => parameters.Mode;
        public bool hasPassword => parameters.PasswordProtected;
        public int maxPlayerCount => parameters.MaxPlayers;
        public string requiredMods => parameters.Mods;
        public string bannedMods => parameters.BannedMods;
        public bool pinned => parameters.Pinned;

        public LobbyInfo(string name, int playerCount, LobbyParameters parameters)
        {
            this.name = name;
            this.playerCount = playerCount;
            this.parameters = parameters;
        }

        public abstract string directJoinCode { get; }
        public override bool Equals(object obj) => obj is LobbyInfo other ? this.Equals(other) : false;
        public abstract bool Equals(LobbyInfo other);
    }
}
