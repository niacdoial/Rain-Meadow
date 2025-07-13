using System.Collections.Generic;
using System.Net;
using System;
using System.Linq;
using System.Diagnostics;

namespace RainMeadow {
    public class INetLobbyInfo : LobbyInfo {
        public MeadowPlayerId host;
        public ulong lobbyId = 0;  // TODO: maybe more separation than that?
        public INetLobbyInfo(MeadowPlayerId host, string name, string mode, int playerCount, bool hasPassword, int maxPlayerCount, string highImpactMods = "", string bannedMods = "") :
            base(name, mode, playerCount, hasPassword, maxPlayerCount, highImpactMods, bannedMods) {
            this.host = host;
        }

        public override string GetLobbyJoinCode(string? password = null)
        {
            if (host is LANPlayerId pHost) {
                if (password != null)
                    return $"+connect_lan_lobby {pHost.endPoint.Address.Address} {pHost.endPoint.Port} +lobby_password {password}";
                return $"+connect_lan_lobby {pHost.endPoint.Address.Address} {pHost.endPoint.Port}";
            } else if (host is RouterPlayerId rHost) {
                if (password != null)
                    return $"+connect_router_lobby {this.lobbyId} {rHost.RoutingId} +lobby_password {password}";
                return $"+connect_router_lobby {this.lobbyId} {rHost.RoutingId}";
            } else {
                throw new Exception("wrong lobby type");
            }
        }
    }

    public class RouterPlayerId : MeadowPlayerId {
        static readonly IPEndPoint BlackHole = new IPEndPoint(IPAddress.Parse("253.253.253.253"), 999);

        public ulong RoutingId = 0;
        public IPEndPoint endPoint;

        public RouterPlayerId() { endPoint = BlackHole; }
        public RouterPlayerId(ulong id = 0) { RoutingId = id; endPoint = BlackHole; }

        override public int GetHashCode() { unchecked { return (int)RoutingId; } }
#if !IS_SERVER // let's not bring serialisation in the server's code
        override public void CustomSerialize(Serializer serializer) {
            serializer.Serialize(ref RoutingId);
        }
#endif

        public bool isLoopback() {
            if (OnlineManager.mePlayer.id is RouterPlayerId mePlayerId)
               return mePlayerId == this;
            else
               return false;
        }

        public override bool Equals(MeadowPlayerId other) {
            if (other is RouterPlayerId other_router_id) {
                return RoutingId == other_router_id.RoutingId;
            }

            return false;
        }
    }


#if !IS_SERVER
    public class RouterMatchmakingManager : MatchmakingManager {

        public override void initializeMePlayer() {
            var meId = new RouterPlayerId();  // TODO: does CS use pass-by-copy or by-value already?
            OnlineManager.mePlayer = new OnlinePlayer( meId );
            if (RainMeadow.rainMeadowOptions.PlayerRoutingId.Value != 0) {
                meId.RoutingId = RainMeadow.rainMeadowOptions.PlayerRoutingId.Value;
            } else {
                System.Random random = new System.Random();
                // TODO: this returns a positive i64, so 63 bits!
                byte[] idBytes = new Byte[sizeof(ulong)];
                random.NextBytes(idBytes);
                meId.RoutingId = System.BitConverter.ToUInt64(idBytes,0);
            }
            // note: we don't know our endPoint yet, because of NAT

            if (RainMeadow.rainMeadowOptions.LanUserName.Value.Length > 0) {
                meId.name = RainMeadow.rainMeadowOptions.LanUserName.Value;
            }
        }


        public override void RequestLobbyList() {
            var packet = new RouterRequestLobbyPacket(CLIENT_VAL);
            ((RouterNetIO)NetIO.currentInstance).SendToServer(packet, NetIO.SendType.Reliable, true);
        }

        static List<INetLobbyInfo> lobbyinfo = new();
        public void addLobby(INetLobbyInfo lobby) {
            var updating_lobby = lobbyinfo.FirstOrDefault(x => x.host == lobby.host);
            if (updating_lobby is not null) {
                lobbyinfo.Remove(updating_lobby);
            }

            lobbyinfo.Add(lobby);
            OnLobbyListReceivedEvent(true, lobbyinfo.ToArray());
        }

        // private void LobbyListReceived(INetLobbyInfo[] lobbies, bool bIOFailure)
        // {
        //     try {
        //         OnLobbyListReceivedEvent(!bIOFailure, lobbies);
        //     } catch (System.Exception e) {
        //         RainMeadow.Error(e);
        //         throw;
        //     }
        // }

        public OnlinePlayer GetPlayerRouter(RouterPlayerId playerId) {
            OnlinePlayer candidate = OnlineManager.players.FirstOrDefault(p => {
                if (p.id is RouterPlayerId routid)
                    return (routid == playerId);
                return false;
            });
            RouterPlayerId candId = (RouterPlayerId)candidate.id;
            if (!UDPPeerManager.CompareIPEndpoints(candId.endPoint, playerId.endPoint)) {
                RainMeadow.Error("player IDs don't agree on endpoint: " + candId.endPoint.ToString() + " vs " + playerId.endPoint.ToString());
                throw new Exception("WHY");
            }
            return candidate;
        }
        public OnlinePlayer GetPlayerRouter(IPEndPoint endPoint) {
            OnlinePlayer candidate = OnlineManager.players.FirstOrDefault(p => {
                if (p.id is RouterPlayerId routid && routid.endPoint != null)
                    return UDPPeerManager.CompareIPEndpoints(routid.endPoint, endPoint);
                return false;
            });
            return candidate;
        }

        public override bool canSendChatMessages => true;
        public override void SendChatMessage(string message) {
            foreach (OnlinePlayer player in OnlineManager.players) {
                if (player.isMe) continue;
                ((RouterNetIO)NetIO.currentInstance).SendP2P(player, new ChatMessagePacket(message), NetIO.SendType.Reliable);
            }

            RecieveChatMessage(OnlineManager.mePlayer, message);
        }

        LobbyVisibility visibility;
        int? maxPlayerCount;
        public ulong lobbyId = 0;

        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount) {
            maxPlayerCount = maxPlayerCount ?? 4;
            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            ((RouterNetIO)NetIO.currentInstance).SendToServer(
                new RouterPublishLobbyPacket(
                    maxPlayerCount ?? 4,
                    OnlineManager.mePlayer.id.name,
                    password != null,
                    gameMode, 1,
                    RainMeadowModManager.ModArrayToString(OnlineManager.lobby.requiredmods),
                    RainMeadowModManager.ModArrayToString(OnlineManager.lobby.bannedmods)
                ),
                NetIO.SendType.Reliable,
                true
            );
        }

        public void OnLobbyPublished(ulong lobbyId) {
            this.lobbyId = lobbyId;
            MatchmakingManager.OnLobbyJoinedEvent(true, "");
        }

        public void LobbyAcknoledgedUs(OnlinePlayer owner)
        {
            RainMeadow.DebugMe();
            RainMeadow.Debug("Lobby has ack'd us, adding player list...");
            foreach (OnlinePlayer player in lobbyPlayers) {
                AcknoledgeRouterPlayer(player);
            }

            if (OnlineManager.lobby is null) {
                OnlineManager.lobby = new Lobby(
                    new OnlineGameMode.OnlineGameModeType(
                        OnlineManager.currentlyJoiningLobby.mode,
                        false
                    ),
                    owner,
                    lobbyPassword
                );
            }

            OnPlayerListReceivedEvent(playerList.ToArray());
        }

        public void AcknoledgeRouterPlayer(OnlinePlayer joiningPlayer)
        {
            var routid = joiningPlayer.id as RouterPlayerId;
            if (routid is null) return;
            if (routid.isLoopback()) return;


            RainMeadow.DebugMe();
            if (OnlineManager.players.Contains(joiningPlayer)) { return; }
            OnlineManager.players.Add(joiningPlayer);
            HandleJoin(joiningPlayer);
            // NAT piercing happens here (players are expected to send unprompted acks to each other)
            (NetIO.currentInstance as RouterNetIO)?.SendAcknoledgement(joiningPlayer);
            RainMeadow.Debug($"Added {joiningPlayer} to the lobby matchmaking player list");

            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner)
            {

                // Tell the other players to create this player
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (player.isMe || player == joiningPlayer)
                        continue;

                    ((RouterNetIO)NetIO.currentInstance).SendP2P(
                        player,
                        new RouterModifyPlayerListPacket(ModifyPlayerListPacketOperation.Add, new OnlinePlayer[] { joiningPlayer }),
                        NetIO.SendType.Reliable
                    );
                }

                // Tell joining peer to create everyone in the server
                // (safety precaution in case the server has out-of-date info or something)
                ((RouterNetIO)NetIO.currentInstance).SendP2P(
                    joiningPlayer,
                    new RouterModifyPlayerListPacket(
                        ModifyPlayerListPacketOperation.Add,
                        OnlineManager.players.Append(OnlineManager.mePlayer).ToArray()
                    ),
                    NetIO.SendType.Reliable
                );
            }
            OnPlayerListReceivedEvent(playerList.ToArray());
        }

        public void RemoveRouterPlayer(OnlinePlayer leavingPlayer)
        {
            StackTrace stackTrace = new();
            RainMeadow.Debug(stackTrace.ToString());

            if (leavingPlayer.isMe) return;
            if (!OnlineManager.players.Contains(leavingPlayer)) { return; }
            HandleDisconnect(leavingPlayer);
            if (OnlineManager.lobby is not null)
            if (OnlineManager.lobby.isOwner)
            {
                var packet = new RouterModifyPlayerListPacket(
                    ModifyPlayerListPacketOperation.Remove,
                    new OnlinePlayer[] { leavingPlayer }
                );

                // Tell the other players to remove this player
                ((RouterNetIO)NetIO.currentInstance).SendToServer(packet, NetIO.SendType.Reliable);
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (player.isMe)
                        continue;

                    ((RouterNetIO)NetIO.currentInstance).SendP2P(player, packet, NetIO.SendType.Reliable);
                }
            }
            NetIO.currentInstance.ForgetPlayer(leavingPlayer);
            OnPlayerListReceivedEvent(playerList.ToArray());
        }

        // note: this is phase 1, where we inform the server of our intent.
        string lobbyPassword = "";
        public override void RequestJoinLobby(LobbyInfo lobby, string? password) {
            RainMeadow.DebugMe();
            if (lobby is INetLobbyInfo lobbyInfo) {
                lobbyPassword = password ?? "";
                OnlineManager.currentlyJoiningLobby = lobby;
                if (lobbyInfo.host == null) { // TODO redo this line
                    RainMeadow.Error("Failed to join routed game (lobby doesn't list endpoint)...");
                    return;
                }
                this.lobbyId = lobbyInfo.lobbyId;

                RainMeadow.Debug("Sending Request to join lobby...");
                // NOTE: inform server to get a list of players,
                // then inform the host (doesn't need any info?)
                ((RouterNetIO)NetIO.currentInstance).SendToServer(
                    new RouterRequestJoinToServerPacket(lobbyInfo.lobbyId),
                    NetIO.SendType.Reliable,
                    true
                );
            } else {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        // phase 2 of joining a lobby: ask the host for permission
        OnlinePlayer[] lobbyPlayers;
        private void OnLobbyInfoSubmitted(OnlinePlayer[] players, bool success) {
            if (!success){
                RainMeadow.Error("Could not ask the server for the lobby");
                return;
            }
            lobbyPlayers = players;
            //OnPlayerListReceivedEvent(players);
            var lobbyInfo = (INetLobbyInfo)OnlineManager.currentlyJoiningLobby;
            if (lobbyInfo.host == null) {
                RainMeadow.Debug("Failed to join routed game (this should have been checked already)...");
                return;
            }

            OnlinePlayer host = null;
            foreach (OnlinePlayer player in players) {
                if (((RouterPlayerId)player.id) == lobbyInfo.host) {
                    host = player;
                    break;
                }
            }
            if (host == null) {
                RainMeadow.Error("Could not find host among player list");
                return;
            }

            RainMeadow.Debug("Sending Request to join lobby...");
            ((RouterNetIO)NetIO.currentInstance).SendP2P(
                host,
                new RouterRequestJoinPacket(OnlineManager.mePlayer.id, lobbyId),
                NetIO.SendType.Reliable,
                true
            );
        }

        // note: called not from the Matchmaking/NetIO layer but from the Lobby Event layer.
        // ...TODO: WHY is password checking this late in the process?
        public override void JoinLobby(bool success) {
            if (success)
            {
                RainMeadow.Debug("Joining lobby");
                OnLobbyJoinedEvent(true);
            }
            else
            {
                OnlineManager.LeaveLobby();
                RainMeadow.Debug("Failed to join local game. Wrong Password");
                OnLobbyJoinedEvent(false, Utils.Translate("Wrong password!"));
            }
        }

        public override void JoinLobbyUsingArgs(params string?[] args) {
            // if (args.Length >= 2 && long.TryParse(args[0], out var address) && int.TryParse(args[1], out var port))
            // {
            //     RainMeadow.Debug($"joining lobby with address {address} and port {port} from the command line");
            //     RequestJoinLobby(new INetLobbyInfo(new IPEndPoint(address, port), "", "", 0, false, 4), args.Length > 2 ? args[2] : null);
            // }
            // else
            //     RainMeadow.Error($"invalid address and port: {string.Join(" ", args)}");
        }

        public override void LeaveLobby() {

            if (OnlineManager.players is not null) {
                if (OnlineManager.players.Count > 1) {
                    foreach (OnlinePlayer p in OnlineManager.players) {
                        ((RouterNetIO)NetIO.currentInstance).SendP2P(p,
                            new SessionEndPacket(),
                                NetIO.SendType.Reliable);
                    }
                }
            }
            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner) {
                var packet = new RouterModifyPlayerListPacket(
                    ModifyPlayerListPacketOperation.Remove,
                    new OnlinePlayer[] { OnlineManager.mePlayer }
                );

                // Tell the other players to remove this player
                ((RouterNetIO)NetIO.currentInstance).SendToServer(packet, NetIO.SendType.Reliable);
            }
            NetIO.currentInstance.ForgetEverything();
        }


        public override OnlinePlayer GetLobbyOwner() {
            if (OnlineManager.lobby == null) return null;

            if (OnlineManager.lobby.owner.hasLeft == true) {
                // select a new owner.
                // The order of players should be
                // TODO implement full renegociation with server
            }

            return OnlineManager.lobby.owner;
        }

        public override MeadowPlayerId GetEmptyId() {
            return new RouterPlayerId(0);
        }

        public override string GetLobbyID() {
            if (OnlineManager.lobby != null) {
                return OnlineManager.lobby.owner.id.GetPersonaName() ?? Utils.Translate("Nobody");
            }

            return "Unknown Router Lobby";
        }


        public override bool canOpenInvitations => false;
    }
#endif  // !IS_SERVER
}
