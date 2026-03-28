using System;
using System.Net;
using System.Linq;
using System.IO;
using Menu;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using RainMeadow.Shared;
using RainMeadow.Shared.Models;

// TODO: delete this whole file once HTTP-based matchmaking exists
namespace RainMeadow
{
    public partial class RouterNetworkDomain
    {
        SecuredPeerId preloadedServerPeer = null;
        public void PreconfigureLobbyServerForCreation(string lobbyEndPointString)
        {
            preloadedServerPeer = SecuredPeerId.GetPeerIdByName(lobbyEndPointString);
            if (preloadedServerPeer != null)
            {
                RainMeadow.Debug("server set: " + preloadedServerPeer.ToString());
            }
            else
            {
                throw new FormatException("LobbyServer Address format should be public_key@xxx.xxx.xxx.xxx:port");
            }
        }

        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount, bool pinned = false)
        {
            currentDomain = NetworkDomainType.Router;
            if (preloadedServerPeer == null)
            {
                OnLobbyJoinedEvent(false, Utils.Translate("Global matchmaking is not yet implemented, you need to provide a lobby server override."));
                // FIXME: this is where we would insert the global-matchmaking-negociation to find our assigned lobby server
                return;
            }
            else
            {
                try
                {
                    serverPeer = PlatformPeerManager.GetRemotePeer(preloadedServerPeer, true);
                }
                catch (Exception exc)
                {
                    // there's pretty much only one reason why this can fail
                    throw new Exception("LobbyServer Address format should be public_key@xxx.xxx.xxx.xxx:port. Omitting the public key is only allowed when joining Local-domain lobbies");
                }
                preloadedServerPeer = null;
            }

            var maxplayercount = maxPlayerCount ?? 0;
            var lobbyParams = new LobbyParameters()
            {
                Mode = gameMode,
                MaxPlayers = maxplayercount,
                PasswordProtected = (password is string),
                Mods = RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                BannedMods = RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods()),
            };
            var lobbyInfo = new RouterLobbyInfo(serverPeer.id, "UNNAMED", 1, lobbyParams);

            OnlineManager.currentlyJoiningLobby = lobbyInfo;
            ((RouterPlayerId)OnlineManager.mePlayer.id).routingID = 1; // we have to be the first for us to send this

            string meName = RainMeadow.rainMeadowOptions.LanUserName.Value;
            if (string.IsNullOrWhiteSpace(meName)) meName = UsernameGenerator.GenerateRandomUsername(PlatformPeerManager.Me.GetHashCode());

            var lobbyPublishPacket = new PublishRouterLobby(
                lobbyInfo.name,
                lobbyParams,
                meName,
                RainMeadow.rainMeadowOptions.RouterExposeIP.Value
            ) {boxed = true};
            SendPacket(serverPeer.id, lobbyPublishPacket, PacketReliability.Reliable);

            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            OnLobbyJoinedEvent(true, "");
        }
    }
}
