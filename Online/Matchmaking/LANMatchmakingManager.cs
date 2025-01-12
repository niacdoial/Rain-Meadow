using System;
using System.Net;
using System.Linq;
using System.IO;
using Menu;
using System.Net.NetworkInformation;
using System.Security.Policy;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Collections.Generic;
using RWCustom;


namespace RainMeadow {
    

    public class LANMatchmakingManager : MatchmakingManager {
        public class LANLobbyInfo : LobbyInfo {
            public IPEndPoint endPoint;
            public LANLobbyInfo(IPEndPoint endPoint, string name, string mode, int playerCount, bool hasPassword, int maxPlayerCount) : 
                base(name, mode, playerCount, hasPassword, maxPlayerCount) {
                this.endPoint = endPoint;
            }
        }

        public class LANPlayerId : MeadowPlayerId
        {
            override public void OpenProfileLink() {
                OnlineManager.instance.manager.ShowDialog(new DialogNotify("This player does not have a profile.", OnlineManager.instance.manager, null));
            }

            public IPEndPoint? endPoint;

            public LANPlayerId() { }
            public LANPlayerId(IPEndPoint endPoint) : base(endPoint?.ToString() ?? "Unknown Enpoint")
            {
                if (endPoint == null) {
                    // System.Diagnostics.StackTrace t = new System.Diagnostics.StackTrace();
                    // RainMeadow.Debug(t.ToString());
                }

                this.endPoint = endPoint;
            }

            public void reset()
            {
                this.endPoint = default;
            }

            public override int GetHashCode() {
                return this.endPoint?.GetHashCode() ?? 0;
            }

            public override void CustomSerialize(Serializer serializer)
            {
                var endpointBytes = endPoint?.Address.GetAddressBytes() ?? new byte[0];
                var port = endPoint?.Port ?? 0;
                serializer.Serialize(ref endpointBytes);
                serializer.Serialize(ref port);

                if (serializer.IsReading) {
                    endPoint = new IPEndPoint(new IPAddress(endpointBytes), port);
                }
            }

            public bool isLoopback() {
                bool sameport = false;
                if (OnlineManager.netIO is LANNetIO netio) {
                    sameport = netio.manager.port == endPoint?.Port;
                }
                return (endPoint?.Address == IPAddress.Loopback || endPoint?.Address == IPAddress.IPv6Loopback) && sameport;
            }

            public override bool Equals(MeadowPlayerId other)
            {
                if (other is LANPlayerId lanid) {
                    if (endPoint != null && lanid.endPoint != null)
                        return UDPPeerManager.CompareIPEndpoints(endPoint, lanid.endPoint); 
                }
                return false;
            }
        }
        public override void initializeMePlayer() {
            if (OnlineManager.netIO is LANNetIO netio) {
                OnlineManager.mePlayer = new OnlinePlayer(new LANPlayerId(new IPEndPoint(IPAddress.Loopback, netio.manager.port))) { isMe = true };
            } 
            
        }

        
        static List<LANLobbyInfo> lobbyinfo = new();
        public override void RequestLobbyList() {
            
            // To create a proper list, we need to send a message to the broadcast endpoint.
            // and wait for responces from possible hosts.
            for (int i = 0; i < 8; i++) {
                if (OnlineManager.netIO is LANNetIO lanentio) {
                    using (MemoryStream memoryStream = new())
                    using (BinaryWriter writer = new(memoryStream)) {
                        (new RequestLobbyPacket()).Serialize(writer);
                        lanentio.manager.SendBroadcast(memoryStream.GetBuffer().Take((int)memoryStream.Position).ToArray());
                    }
                }

            }
        }

        public void addLobby(LANLobbyInfo lobby) {
            RainMeadow.Debug($"Added lobby {lobby}");
            lobbyinfo.Add(lobby);
            OnLobbyListReceivedEvent(true,  lobbyinfo.ToArray());
        }


        public void SendLobbyInfo(OnlinePlayer other) {
            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner) {
                if (OnlineManager.netIO is LANNetIO lannetio) {
                    var packet = new InformLobbyPacket(
                        maxplayercount, "LAN Lobby", OnlineManager.lobby.hasPassword,
                        OnlineManager.lobby.gameModeType.value, OnlineManager.players.Count);
                    OnlineManager.netIO.SendP2P(other, packet, NetIO.SendType.Reliable);
                }
            }
        }

        public OnlinePlayer GetPlayerLAN(IPEndPoint other) {
            return OnlineManager.players.FirstOrDefault(p => {
                if (p.id is LANPlayerId lanid)
                    if (lanid.endPoint != null)
                        return UDPPeerManager.CompareIPEndpoints(lanid.endPoint, other);
                return false;
            });
        }

        public int maxplayercount = 0;
        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount) {
            maxplayercount = maxPlayerCount ?? 0;
            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            MatchmakingManager.OnLobbyJoinedEvent(true, "");
        }

        public void LobbyAcknoledgedUs(OnlinePlayer owner)
        {
            RainMeadow.DebugMe();
            OnlineManager.lobby = new Lobby(
                new OnlineGameMode.OnlineGameModeType(OnlineManager.currentlyJoiningLobby.mode, false), 
                owner, lobbyPassword);
        }


        public void AcknoledgeLANPlayer(OnlinePlayer joiningPlayer)
        {
            var lanid = joiningPlayer.id as LANPlayerId;
            if (lanid is null) return;
            if (lanid.isLoopback()) return; 


            RainMeadow.DebugMe();
            if (OnlineManager.players.Contains(joiningPlayer)) { return; }
            OnlineManager.players.Add(joiningPlayer);
            ((LANNetIO)OnlineManager.netIO).SendAcknoledgement(joiningPlayer, false);
            RainMeadow.Debug($"Added {joiningPlayer} to the lobby matchmaking player list");

            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner)
            {
                
                // Tell the other players to create this player
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (player.isMe || player == joiningPlayer)
                        continue;

                    OnlineManager.netIO.SendP2P(player, new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Add, new OnlinePlayer[] { joiningPlayer }), 
                        NetIO.SendType.Reliable);
                }

                
                // Tell joining peer to create everyone in the server
                OnlineManager.netIO.SendP2P(joiningPlayer, new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Add, 
                    OnlineManager.players.Append(OnlineManager.mePlayer).ToArray()), 
                    NetIO.SendType.Reliable);
            }
            OnPlayerListReceivedEvent(playerList.ToArray());
        }

        public void RemoveLANPlayer(OnlinePlayer leavingPlayer)
        {
            if (leavingPlayer.isMe) return; 
            if (!OnlineManager.players.Contains(leavingPlayer)) { return; }
            OnPlayerListReceivedEvent(playerList.ToArray());

            HandleDisconnect(leavingPlayer);

            if (OnlineManager.lobby.isOwner)
            {
                // Tell the other players to remove this player
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (player.isMe)
                        continue;

                    OnlineManager.netIO.SendP2P(player, new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Remove, new OnlinePlayer[] { leavingPlayer }), 
                        NetIO.SendType.Reliable);
                }
            }
            OnlineManager.netIO.ForgetPlayer(leavingPlayer);
        }

        string lobbyPassword = "";
        public override void RequestJoinLobby(LobbyInfo lobby, string? password) {
            RainMeadow.DebugMe();
            if (lobby is LANLobbyInfo lobbyinfo) {
                lobbyPassword = password ?? "";
                OnlineManager.currentlyJoiningLobby = lobby;
                var lobbyInfo = (LANLobbyInfo)lobby;
                if (lobbyInfo.endPoint == null)
                {
                    RainMeadow.Debug("Failed to join local game...");
                    return;
                }
                
                RainMeadow.Debug("Sending Request to join lobby...");
                OnlineManager.netIO.SendP2P(new OnlinePlayer(new LANPlayerId(lobbyInfo.endPoint)), 
                    new RequestJoinPacket(), NetIO.SendType.Reliable, true);
            } else {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        public override void JoinLobby(bool success) {
            if (success)
            {
                RainMeadow.Debug("Joining lobby");
                OnLobbyJoinedEvent(true);
            }
            else
            {
                LeaveLobby();
                RainMeadow.Debug("Failed to join local game. Wrong Password");
                OnLobbyJoinedEvent(false, "Wrong password!");
            }
        }

        public override void LeaveLobby() {
            if (OnlineManager.lobby != null)
            {
                if (!OnlineManager.lobby.isOwner && GetLobbyOwner() is OnlinePlayer owner)
                {
                    OnlineManager.netIO.ForgetEverything();
                    OnlineManager.netIO.SendP2P(owner, 
                        new RequestLeavePacket(), NetIO.SendType.Reliable, true);
                } else if (OnlineManager.lobby.isOwner) {
                    foreach (OnlinePlayer p in  OnlineManager.players) {
                        OnlineManager.netIO.SendP2P(p, 
                            new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Remove, new OnlinePlayer[] { OnlineManager.mePlayer }), 
                                NetIO.SendType.Reliable);
                        OnlineManager.netIO.SendP2P(p, 
                            new SessionEndPacket(), 
                                NetIO.SendType.Reliable);
                    }
                }
            }
        }

        public override OnlinePlayer GetLobbyOwner() {
            if (OnlineManager.lobby.owner.hasLeft == true || OnlineManager.lobby == null) {
                // select a new owner. 
                // The order of players should be 
                return currentInstance.BestTransferCandidate(OnlineManager.lobby, OnlineManager.lobby.participants);
            }

            return OnlineManager.lobby.owner;
        }   

        public override MeadowPlayerId GetEmptyId() {
            return new LANPlayerId(null);
        }

        public override string GetLobbyID() {
            if (OnlineManager.lobby != null) {
                return (OnlineManager.lobby.owner.id as LANPlayerId).name + "'s Lobby";
            }

            return "unknown lan lobby";
        }
        public override void OpenInvitationOverlay() {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify("You cannot use this feature here.", OnlineManager.instance.manager, null));
        }
    }
}