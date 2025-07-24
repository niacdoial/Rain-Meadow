using System;
using System.IO;
using RainMeadow.Shared;

namespace RainMeadow {
    class PacketProcessing {
        public static void ProcessPacketForRouter(Packet packet, OnlinePlayer player) {
            switch (packet.type) {
                // most common first (if switch is closer to a bunch of "if"s than a lookup table like in C)
                case Packet.Type.Session: ProcessSession((SessionPacket)packet); break;
                case Packet.Type.ChatMessage: ProcessChatMessage((ChatMessagePacket)packet); break;
                case Packet.Type.SessionEnd: ProcessSessionEnd((SessionEndPacket)packet); break;
                case Packet.Type.LANModifyPlayerList: RainMeadow.Error("Cannot receive LAN packet in router mode"); break;
                case Packet.Type.LANAcceptJoin: RainMeadow.Error("Cannot receive LAN packet in router mode"); break;
                case Packet.Type.LANRequestJoin: RainMeadow.Error("Cannot receive LAN packet in router mode"); break;
                case Packet.Type.LANRequestLobby: RainMeadow.Error("Cannot receive LAN packet in router mode"); break;
                case Packet.Type.LANInformLobby: RainMeadow.Error("Cannot receive LAN packet in router mode"); break;
                case Packet.Type.RouterModifyPlayerList: ProcessPlayerModifyRouter((RouterModifyPlayerListPacket)packet); break;
                case Packet.Type.RouterAcceptJoin: ProcessAcceptJoinRouter((RouterAcceptJoinPacket)packet); break;
                case Packet.Type.RouterRequestJoinToServer: RainMeadow.Error("Cannot process this packet player-side"); break;
                case Packet.Type.RouterRequestJoin: ProcessRequestJoinRouter((RouterRequestJoinPacket)packet); break;
                case Packet.Type.RouterRequestLobby: RainMeadow.Error("Cannot process this packet player-side"); break;
                case Packet.Type.RouterInformLobby: ProcessInformLobbyRouter((RouterInformLobbyPacket)packet); break;
                case Packet.Type.RouterPublishLobby: RainMeadow.Error("Cannot process this packet player-side"); break;
                case Packet.Type.RouterAcceptPublish: ProcessAcceptPublishRouter((RouterAcceptPublishPacket)packet); break;
                case Packet.Type.RouterGenericFailure: ProcessGenericFailureRouter((RouterGenericFailurePacket)packet); break;
                default: RainMeadow.Error("Unknown packet type: " + packet.type.ToString()); break;
            };
        }

        public static void ProcessPacketForLAN(Packet packet, OnlinePlayer player) {
            switch (packet.type) {
                // most common first (if switch is closer to a bunch of "if"s than a lookup table like in C)
                case Packet.Type.Session: ProcessSession((SessionPacket)packet); break;
                case Packet.Type.ChatMessage: ProcessChatMessage((ChatMessagePacket)packet); break;
                case Packet.Type.SessionEnd: ProcessSessionEnd((SessionEndPacket)packet); break;
                case Packet.Type.LANModifyPlayerList: ProcessPlayerModifyLAN((LANModifyPlayerListPacket)packet); break;
                case Packet.Type.LANAcceptJoin: ProcessAcceptJoinLAN((LANAcceptJoinPacket)packet); break;
                case Packet.Type.LANRequestJoin: ProcessRequestJoinLAN((LANRequestJoinPacket)packet); break;
                case Packet.Type.LANRequestLobby: ProcessRequestLobbyLAN((LANRequestLobbyPacket)packet); break;
                case Packet.Type.LANInformLobby: ProcessInformLobbyLAN((LANInformLobbyPacket)packet); break;
                case Packet.Type.RouterModifyPlayerList: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                case Packet.Type.RouterAcceptJoin: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                case Packet.Type.RouterRequestJoinToServer: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                case Packet.Type.RouterRequestJoin: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                case Packet.Type.RouterRequestLobby: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                case Packet.Type.RouterInformLobby: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                case Packet.Type.RouterPublishLobby: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                case Packet.Type.RouterAcceptPublish: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                case Packet.Type.RouterGenericFailure: RainMeadow.Error("Cannot receive Router packet in LAN mode"); break;
                default: RainMeadow.Error("Unknown packet type: " + packet.type.ToString()); break;
            };
        }

        static void ProcessSession(SessionPacket packet) {
            if (OnlineManager.lobby is not null) {
                Buffer.BlockCopy(packet.data, 0, OnlineManager.serializer.buffer, 0, packet.size);
                OnlineManager.serializer.ReadData(packet.processingPlayer, packet.size);
            }
        }
        static void ProcessChatMessage(ChatMessagePacket packet) {
            MatchmakingManager.currentInstance.RecieveChatMessage(packet.processingPlayer, packet.message);
        }
        static void ProcessSessionEnd(SessionEndPacket packet) {
            if (MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.LAN) {}
            else if (MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.Router) {}
            else return;
            NetIOPlatform.currentInstance.ForgetPlayer(packet.processingPlayer);
        }


        static void ProcessPlayerModifyLAN(LANModifyPlayerListPacket packet) {
            switch (packet.modifyOperation) {
                case ModifyPlayerListPacketOperation.Add:
                    RainMeadow.Debug("Adding players...\n\t" + string.Join<LANPlayerId>("\n\t", packet.players));
                    for (int i = 0; i < packet.players.Length; i++) {
                        if ((packet.players[i]).isLoopback()) {
                            // That's me
                            // Put me where I belong. (...at the end of the player list?)
                            OnlineManager.players.Remove(OnlineManager.mePlayer);
                            OnlineManager.players.Add(OnlineManager.mePlayer);
                            continue;
                        }

                        MatchmakingManager.lanInstance.AcknoledgeLANPlayer(new OnlinePlayer(packet.players[i]));
                    }
                    break;

                case ModifyPlayerListPacketOperation.Remove:
                    RainMeadow.Debug("Removing players...\n\t" + string.Join<LANPlayerId>("\n\t", packet.players));
                    for (int i = 0; i < packet.players.Length; i++) {
                        var player = MatchmakingManager.lanInstance.GetPlayerLAN(packet.players[i]);
                        if (player is BasicOnlinePlayer plr) {
                            MatchmakingManager.lanInstance.RemoveLANPlayer(plr);
                        }
                    }
                    break;
            }
        }
        static void ProcessAcceptJoinLAN(LANAcceptJoinPacket packet) {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) return;
            var newLobbyInfo = packet.MakeLobbyInfo();

            // If we don't have a lobby and we a currently joining a lobby
            if (OnlineManager.lobby is null && OnlineManager.currentlyJoiningLobby is not null) {
                // If the lobby we want to join is a lan lobby
                if (OnlineManager.currentlyJoiningLobby is INetLobbyInfo oldLobbyInfo) {
                    // If the lobby we want to join is the lobby that allowed us to join.
                    if (oldLobbyInfo.host is LANPlayerId oldHost && newLobbyInfo.host is LANPlayerId newHost)
                        if (UDPPeerManager.CompareIPEndpoints(oldHost.endPoint, newHost.endPoint)) {
                            OnlineManager.currentlyJoiningLobby = newLobbyInfo;
                            LANMatchmakingManager matchMaker = MatchmakingManager.lanInstance;
                            matchMaker.maxplayercount = newLobbyInfo.maxPlayerCount;
                            matchMaker.LobbyAcknoledgedUs(processingPlayer);
                        }
                }
            }
        }
        static void ProcessRequestJoinLAN(LANRequestJoinPacket packet) {
            RainMeadow.DebugMe();
            if (OnlineManager.lobby != null && MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.LAN)
            {
                var matchmaker = MatchmakingManager.lanInstance;

                if (packet.LanUserName.Length > 0) {
                    packet.processingPlayer.id.name = packet.LanUserName;
                }

                // Tell everyone else about them
                RainMeadow.Debug("Telling client they got in.");
                matchmaker.AcknoledgeLANPlayer(packet.processingPlayer);

                // Tell them they are in
                NetIOPlatform.lanInstance.SendP2P(packet.processingPlayer, new LANAcceptJoinPacket(
                    matchmaker.maxplayercount,
                    "LAN Lobby",
                    OnlineManager.lobby.hasPassword,
                    OnlineManager.lobby.gameModeType.value,
                    OnlineManager.players.Count,
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods())
                ), NetIO.SendType.Reliable);

            }
        }
        static void ProcessRequestLobbyLAN(LANRequestLobbyPacket packet) {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) return;
            if (OnlineManager.lobby != null) {
                RainMeadow.DebugMe();
                MatchmakingManager.lanInstance.SendLobbyInfo(packet.processingPlayer);
            }
        }
        static void ProcessInformLobbyLAN(LANInformLobbyPacket packet) {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) return;
            if (OnlineManager.instance != null && OnlineManager.lobby == null) {
                RainMeadow.DebugMe();
                var lobbyinfo = packet.MakeLobbyInfo();
                MatchmakingManager.lanInstance.addLobby(lobbyinfo);
            }
        }


        static void ProcessPlayerModifyRouter(RouterModifyPlayerListPacket packet) {
            switch (packet.modifyOperation)
            {
                case ModifyPlayerListPacketOperation.Add:
                    if (((RouterPlayerId)packet.processingPlayer.id).isServer()) {
                        if (OnlineManager.lobby?.isOwner ?? false) {
                            // in the future, maybe ensure incoming players have talked to the matchmaking server first
                            // (in other words: ensure this codepath is hit before they arrive)
                            foreach (RouterPlayerId playerId in packet.players) {
                                OnlinePlayer player = new OnlinePlayer(playerId);
                                NetIOPlatform.routerInstance.SendAcknoledgement(player); // pierce NAT
                                MatchmakingManager.routerInstance.AcknoledgeRouterPlayer(player);  // TODO: maybe this is a little early to announce this to everyone;
                            }
                        } else {
                            var players = packet.players.Select(x => new OnlinePlayer(x)).ToArray();
                            MatchmakingManager.routerInstance.OnLobbyInfoSubmitted(players);
                        }
                    } else {
                        if (OnlineManager.lobby == null) {
                            return;
                        }
                        if (OnlineManager.lobby.isOwner || ((RouterPlayerId)OnlineManager.lobby.owner.id) != ((RouterPlayerId)packet.processingPlayer.id)) {
                            RainMeadow.Error("cheeky user is trying to force-introduce themselves!");
                            return;
                        }
                        for (int i = 0; i < packet.players.Length; i++)
                        {
                            if (packet.players[i].isLoopback()) {
                                // That's me
                                // move the existing "me" entry there rather than adding a new "me" entry without isMe flag.
                                OnlineManager.players.Remove(OnlineManager.mePlayer);
                                OnlineManager.players.Add(OnlineManager.mePlayer);
                                continue;
                            }

                            MatchmakingManager.routerInstance.AcknoledgeRouterPlayer(new OnlinePlayer(packet.players[i]));
                        }
                    }
                    break;

                case ModifyPlayerListPacketOperation.Remove:
                    if (((RouterPlayerId)packet.processingPlayer.id).isServer()) {
                        RainMeadow.Error("received player removal notice from server: this isn't supposed to happen");
                    }
                    RainMeadow.Debug("Removing players...\n\t" + string.Join<RouterPlayerId>("\n\t", packet.players));
                    for (int i = 0; i < packet.players.Length; i++)
                    {
                        BasicOnlinePlayer? player = MatchmakingManager.routerInstance.GetPlayerRouter(packet.players[i]);
                        if (player is OnlinePlayer plr) {
                            MatchmakingManager.routerInstance.RemoveRouterPlayer(plr);
                        }
                    }
                    break;
            }
        }
        static void ProcessAcceptJoinRouter(RouterAcceptJoinPacket packet) {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) return;
            var newLobbyInfo = packet.MakeLobbyInfo();

            // If we don't have a lobby and we a currently joining a lobby
            if (OnlineManager.lobby is null && OnlineManager.currentlyJoiningLobby is not null) {
                // If the lobby we want to join is a lan lobby
                if (OnlineManager.currentlyJoiningLobby is INetLobbyInfo oldLobbyInfo) {
                    // If the lobby we want to join is the lobby that allowed us to join.
                    if (oldLobbyInfo.host == newLobbyInfo.host) {
                        OnlineManager.currentlyJoiningLobby = newLobbyInfo;
                        RouterMatchmakingManager matchmaker = (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router] as RouterMatchmakingManager);
                        matchmaker.MAX_LOBBY = newLobbyInfo.maxPlayerCount;
                        matchmaker.LobbyAcknoledgedUs(packet.processingPlayer);
                    }
                }
            }
        }
        static void ProcessRequestJoinRouter(RouterRequestJoinPacket packet) {
            RainMeadow.DebugMe();
            if (OnlineManager.lobby != null && MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.Router)
            {
                RouterMatchmakingManager matchmaker = MatchmakingManager.routerInstance;

                // TODO IP check?
                if (packet.senderUserName.Length > 0) {
                    packet.processingPlayer.id.name = packet.senderUserName;
                }
                if (packet.lobbyId != matchmaker.lobbyId) {
                    RainMeadow.Error("Received a request to join for the wrong lobby ID! %"+packet.lobbyId.ToString()+" vs %"+matchmaker.lobbyId.ToString());
                    NetIOPlatform.routerInstance.SendP2P(
                        packet.processingPlayer,
                        new RouterGenericFailurePacket("Contacted host is hosting another lobby!"),
                        NetIO.SendType.Reliable
                    );
                    return;
                }

                // Tell everyone else about them
                RainMeadow.Debug("Telling client they got in.");
                matchmaker.AcknoledgeRouterPlayer(packet.processingPlayer);

                // Tell them they are in
                NetIOPlatform.routerInstance.SendP2P(packet.processingPlayer, new RouterAcceptJoinPacket(
                    packet.lobbyId,
                    matchmaker.MAX_LOBBY,
                    OnlineManager.mePlayer.id.name,
                    OnlineManager.lobby.hasPassword,
                    OnlineManager.lobby.gameModeType.value,
                    OnlineManager.players.Count,
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods())
                ), NetIO.SendType.Reliable);
            }
        }

        static void ProcessInformLobbyRouter(RouterInformLobbyPacket packet) {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) return;
            if (OnlineManager.instance != null && OnlineManager.lobby == null) {
                RainMeadow.DebugMe();
                var lobbyinfo = packet.MakeLobbyInfo();
                lobbyinfo.lobbyId = packet.lobbyId;
                MatchmakingManager.routerInstance.addLobby(lobbyinfo);
            }
        }

        static void ProcessAcceptPublishRouter(RouterAcceptPublishPacket packet) {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) return;
            if (OnlineManager.lobby != null) {
                if (OnlineManager.lobby.isOwner) {
                    RainMeadow.DebugMe();
                    RouterMatchmakingManager matchmaker = MatchmakingManager.routerInstance;
                    matchmaker.OnLobbyPublished(packet.lobbyId);
                }
            }
        }

        static void ProcessGenericFailureRouter(RouterGenericFailurePacket packet) {
            MatchmakingManager.routerInstance.OnError(packet.message);
        }

    }
}
