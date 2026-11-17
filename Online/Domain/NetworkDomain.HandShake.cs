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
        public static void AbortJoinLobby(string error = "")
        {
            if (OnlineManager.currentlyJoiningLobby == null) return;
            if (OnlineManager.lobby != null) OnlineManager.LeaveLobby();
            OnlineManager.currentlyJoiningLobby = null!;
            OnLobbyJoined?.Invoke(false, error);
            return;
        }

        public abstract void RequestLobbyList(); // todo custom filters?

        public abstract void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount, bool pinned = false);

        public virtual bool canDirectConnect => false;
        public virtual LobbyInfo GenerateDCLobbyInfo(string connectstr) // throws FormatException or NotImplementedException
        {
            throw new NotImplementedException();
        }

        public abstract void RequestJoinLobby(LobbyInfo lobby, string? password);
        public virtual void AcceptOrRejectPlayer(OnlinePlayer player, bool accept) {}
        public void JoinLobby(bool success, string error = "")
        {
            if (success)
            {
                RainMeadow.Debug("Joining lobby");
                OnLobbyJoinedEvent(true);
            }
            else
            {
                OnlineManager.LeaveLobby();
                RainMeadow.Debug($"Failed to join local game. {error}");
                OnLobbyJoinedEvent(false, Utils.Translate(error));
            }
        }
        public abstract void HandleLeavingLobby();

        public abstract bool canOpenInvitations { get; }
        public virtual void OpenInvitationOverlay()
        {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify("You cannot use this feature here.", OnlineManager.instance.manager, null));
        }
    }
}
