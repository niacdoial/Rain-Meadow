using System;
// using System.Net;
using RainMeadow.Shared.Models;
namespace RainMeadow;

// abstracted for displaying lobby data
public abstract class LobbyInfo (
    string name,
    int playerCount,
    LobbyParameters parameters
)// : IEquatable<LobbyInfo>
{
    public string name = name;

    public int playerCount = playerCount;
    public LobbyParameters parameters = parameters;
    public string mode => parameters.Mode;
    public bool hasPassword => parameters.PasswordProtected;
    public int maxPlayerCount => parameters.MaxPlayers;
    public string requiredMods => parameters.Mods;
    public string bannedMods => parameters.BannedMods;
    // public bool pinned => parameters.Pinned;
    public bool pinned = false;  // FIXME does ANYTHING write to this?
    public string activeTimeline => parameters.ActiveTimeline;
    public bool whitelistMode => parameters.WhitelistMode;

    
    public abstract NetworkDomain.NetworkDomainType domain { get; }
    public abstract string directJoinCode { get; }

    public string GetLobbyJoinCode(string? password = null)
        => NetworkDomain.instances[domain].FormatJoinCode(password, directJoinCode);
}
