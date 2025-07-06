using System.Collections.Generic;
using System.Net;
using System;
using System.Linq;
using System.Diagnostics;

namespace RainMeadow {
    class RouterPlayerId : MeadowPlayerId {
        static readonly IPEndPoint BlackHole = new IPEndPoint(IPAddress.Parse("253.253.253.253"), 999);

        public ulong RoutingId = 0;
        public IPEndPoint endPoint;

        public RouterPlayerId() { endPoint = BlackHole; }
        public RouterPlayerId(ulong id = 0) { RoutingId = id; endPoint = BlackHole; }

        override public int GetHashCode() { unchecked { return (int)RoutingId; } }
        override public void CustomSerialize(Serializer serializer) {
            serializer.Serialize(ref RoutingId);
        }

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
            var packet = new RequestLobbyListPacket(
                CLIENT_VAL,
                OnlineManager.mePlayer.id
            );
            ((RouterNetIO)NetIO.currentInstance).SendToServer(packet, NetIO.SendType.Reliable, true);
        }

        private void LobbyListReceived(INetLobbyInfo[] lobbies, bool bIOFailure)
        {
            try {
                OnLobbyListReceivedEvent(!bIOFailure, lobbies);
            } catch (System.Exception e) {
                RainMeadow.Error(e);
                throw;
            }
        }

        public OnlinePlayer GetPlayerRouter(IPEndPoint other) {
            return OnlineManager.players.FirstOrDefault(p => {
                if (p.id is RouterPlayerId routid)
                    if (routid.endPoint != null)
                        return UDPPeerManager.CompareIPEndpoints(routid.endPoint, other);
                return false;
            });
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
        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount) {
            maxPlayerCount = maxPlayerCount ?? 4;
            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            ((RouterNetIO)NetIO.currentInstance).SendToServer(
                new PublishLobbyPacket(OnlineManager.lobby),
                NetIO.SendType.Reliable,
                true
            );
            MatchmakingManager.OnLobbyJoinedEvent(true, "");
        }

        public void LobbyAcknoledgedUs(OnlinePlayer owner)
        {
            RainMeadow.DebugMe();
            RainMeadow.Debug("Lobby has ack'd us, adding player list...");
            foreach (RouterPlayerId playerId in lobbyPlayerIds) {
                OnlineManager.players.Add(new OnlinePlayer(playerId));
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
            (NetIO.currentInstance as RouterNetIO)?.SendAcknoledgement(joiningPlayer);
            RainMeadow.Debug($"Added {joiningPlayer} to the lobby matchmaking player list");

            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner)
            {

                // Tell the other players to create this player
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (player.isMe || player == joiningPlayer)
                        continue;

                    ((RouterNetIO)NetIO.currentInstance).SendP2P(player, new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Add, new OnlinePlayer[] { joiningPlayer }),
                        NetIO.SendType.Reliable);
                }

                // Tell joining peer to create everyone in the server
                // (safety precaution in case the server has out-of-date info or something)
                ((RouterNetIO)NetIO.currentInstance).SendP2P(
                    joiningPlayer,
                    new ModifyPlayerListPacket(
                        ModifyPlayerListPacket.Operation.Add,
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
                var packet = new ModifyPlayerListPacket(
                    ModifyPlayerListPacket.Operation.Remove,
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
            if (lobby is INetLobbyInfo lobbyinfo) {
                lobbyPassword = password ?? "";
                OnlineManager.currentlyJoiningLobby = lobby;
                if (lobbyinfo.endPoint == null) {
                    RainMeadow.Error("Failed to join routed game (lobby doesn't list endpoint)...");
                    return;
                }

                //var host = OnlineManager.mePlayer = new OnlinePlayer( new RouterPlayerId());
                //host.id.endPoint = lobbyInfo.endPoint;
                // routingId and name will be set when we receive a SelfAnnouncementPacket from them

                RainMeadow.Debug("Sending Request to join lobby...");
                // NOTE: inform server to get a list of players,
                // then inform the host (doesn't need any info?)
                ((RouterNetIO)NetIO.currentInstance).SendToServer(
                    new RequestJoinToServerPacket(
                        lobbyinfo.name,
                        lobbyinfo.endPoint,
                        OnlineManager.mePlayer.id
                    ),
                    NetIO.SendType.Reliable,
                    true
                );
            } else {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        // phase 2 of joining a lobby: ask the host for permission
        RouterPlayerId[] lobbyPlayerIds;
        private void OnLobbyInfoSubmitted(RouterPlayerId[] players, bool success) {
            if (!success){
                RainMeadow.Error("Could not ask the server for the lobby");
                return;
            }
            lobbyPlayerIds = players;
            //OnPlayerListReceivedEvent(players);
            var lobbyinfo = (INetLobbyInfo)OnlineManager.currentlyJoiningLobby;
            if (lobbyinfo.endPoint == null) {
                RainMeadow.Debug("Failed to join routed game (this should have been checked already)...");
                return;
            }

            OnlinePlayer host = null;
            foreach (RouterPlayerId playerId in players) {
                if (playerId.endPoint is null) continue;
                if (UDPPeerManager.CompareIPEndpoints(playerId.endPoint, lobbyinfo.endPoint)) {
                    host = new OnlinePlayer(playerId);
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
                new RequestJoinPacket(OnlineManager.mePlayer.id.name),
                NetIO.SendType.Reliable,
                true
            );
        }

        // note: called not from the Matchmaking/NetIO layer but from the Lobby Event layer.
        // ...WHY is password checking this late in the process?
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
                    foreach (OnlinePlayer p in  OnlineManager.players) {
                        ((RouterNetIO)NetIO.currentInstance).SendP2P(p,
                            new SessionEndPacket(),
                                NetIO.SendType.Reliable);
                    }
                }
            }
            if (OnlineManager.lobby.isOwner) {
                var packet = new ModifyPlayerListPacket(
                    ModifyPlayerListPacket.Operation.Remove,
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
}
