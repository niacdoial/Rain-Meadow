using System;
using System.Net;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Menu;

using RainMeadow.Shared;

namespace RainMeadow
{
    public abstract partial class NetworkDomain
    {
        public abstract OnlinePlayer CreateMePlayer();
        public virtual void ForgetPlayer(OnlinePlayer player) { }
        public virtual void ForgetEverything() { }

        public abstract OnlinePlayer? GetLobbyOwner();
        public abstract MeadowPlayerId GetEmptyId();

        public virtual bool IsDev(MeadowPlayerId player) => false;
        public virtual bool IsTrustedCommunity(MeadowPlayerId player) => false;

        public virtual OnlinePlayer GetPlayer(MeadowPlayerId id)
        {
            return OnlineManager.players.FirstOrDefault(p => p.id == id);
        }

        // the idea here was to decide by ping some day
        public virtual OnlinePlayer BestTransferCandidate(OnlineResource onlineResource, List<OnlinePlayer> subscribers)
        {
            if (onlineResource.isAvailable && onlineResource.isActive && subscribers.Contains(OnlineManager.mePlayer) && !OnlineManager.mePlayer.isActuallySpectating) return OnlineManager.mePlayer;
            if (subscribers.Count < 1) return null;
            return subscribers.FirstOrDefault(p => !p.hasLeft && OnlineManager.lobby.gameMode.PlayerCanOwnResource(p, onlineResource));
        }
    }
}
