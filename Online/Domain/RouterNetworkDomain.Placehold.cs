using System;
using System.Net;
using System.Linq;
using System.IO;
using Menu;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using RainMeadow.Shared;

// TODO: delete this whole file once HTTP-based matchmaking exists
namespace RainMeadow
{
    public partial class RouterNetworkDomain
    {
        void OnLobbyServerEmpty(LobbyIsEmpty packet)
        {
            RainMeadow.Debug("Received LobbyEmpty");
            if (!ValidateIsFromServer(packet)) return;
            // serverPeer has been set by the original RequestJoinLobby
            // we have been chosen to be the lobby host, set our routingID to 1 now so that we don't get confused by the ModifyPlayerList packet
            ((RouterPlayerId)OnlineManager.mePlayer.id).routingID = 1;

            // the user can create the lobby now, and it will be published to the server.
            OnLobbyJoinedEvent(false, Utils.Translate("Connection successful! You can now use the \"create lobby\" menu to start playing."));
            //OnlineManager.instance.manager.ShowDialog(new DialogNotify("No lobby in this server: you can create one.", OnlineManager.instance.manager, null));
        }

        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount)
        {
            if (serverPeer != null && !UDPPeerManager.CompareIPEndpoints(serverPeer, SharedPlatform.BlackHole)) {
                var maxplayercount = maxPlayerCount ?? 0;
                var lobbyInfo = new RouterLobbyInfo(
                    serverPeer,
                    "UNNAMED", gameMode,
                    1, (password is string), maxplayercount,
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods())
                );
                RequestPublishLobby(lobbyInfo);

                OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
                OnLobbyJoinedEvent(true, "");
            } else {
                OnLobbyJoinedEvent(false, Utils.Translate("You need attempt a direct-connect to the lobby server first, and if it is empty you can create the lobby."));
            }
        }

        void RequestPublishLobby(RouterLobbyInfo lobby)
        {
            OnlineManager.currentlyJoiningLobby = lobby;

            RainMeadow.Debug("Sending Request to join lobby...");
            string meName = OnlineManager.mePlayer.id.name;
            Send(
                serverPeer,
                new PublishRouterLobby(
                    lobby.maxPlayerCount, lobby.name,  lobby.mode, lobby.hasPassword,
                    lobby.requiredMods, lobby.bannedMods
                ),
                UDPPeerManager.PacketType.Reliable,
                false
            );
            ((RouterPlayerId)OnlineManager.mePlayer.id).routingID = 1; // we have to be the first for us to send this
        }
    }
}
