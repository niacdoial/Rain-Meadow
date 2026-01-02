using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using HarmonyLib;
using Menu;
using RainMeadow.Shared;

/// //////////////////////////////////////////
/// NetworkDomain describes the common interface for the middle part of the network stack
/// (or, for steam networking, the wrapper around the steam library): NetworkDomain.
///
/// This layer is responsible for keeping track of the player list (PeerID, name),
/// interpreting messages (packets of the RPC system, chat messages) between players,
/// and in general keeping up with joining/leaving/kicked players,
/// as well as setting up the info necessary to join an existing lobby
/// It is also somewhat responsible for preventing players to impersonate each other.
///
/// It is heavily used by the OnlineManager, which orchestrates the link between the network stack and the game's state changes.

namespace RainMeadow
{

    public abstract partial class NetworkDomain
    {
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
