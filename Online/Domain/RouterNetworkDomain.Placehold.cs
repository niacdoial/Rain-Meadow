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
        public void PreconfigureLobbyServerForCreation(string lobbyEndPointString)
        {
            var lobbyEndpoint = SecuredPeerId.GetPeerIdByName(lobbyEndPointString);
            if (lobbyEndpoint != null)
            {
                serverPeer = PlatformPeerManager.GetRemotePeer(lobbyEndpoint, true);
            }
            else
            {
                throw new FormatException("IP Address format should be public_key@xxx.xxx.xxx.xxx:port");
            }
        }

        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount, bool pinned = false)
        {
            currentDomain = NetworkDomainType.Router;
            if (serverPeer == null)
            {
                OnLobbyJoinedEvent(false, Utils.Translate("Global matchmaking is not yet implemented, you need to provide a lobby server override."));
                // FIXME: this is where we would insert the global-matchmaking-negociation to find our assigned lobby server
                return;
            }

            var maxplayercount = maxPlayerCount ?? 0;
            var lobbyInfo = new RouterLobbyInfo(
                serverPeer.id,
                "UNNAMED", gameMode,
                1, (password is string), maxplayercount,
                RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods())
            );
            OnlineManager.currentlyJoiningLobby = lobbyInfo;
            ((RouterPlayerId)OnlineManager.mePlayer.id).routingID = 1; // we have to be the first for us to send this

            var lobbyPublishPacket = new PublishRouterLobby(
                lobbyInfo.name,
                lobbyInfo.GetParameters(),
                RainMeadow.rainMeadowOptions.RouterExposeIP.Value
            );
            SendPacket(serverPeer.id, lobbyPublishPacket, PacketReliability.Reliable);

            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            OnLobbyJoinedEvent(true, "");
        }
    }
}
