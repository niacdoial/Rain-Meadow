using System;
using System.Net;
using System.Linq;
using System.IO;
using Menu;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using RainMeadow.Shared;


namespace RainMeadow
{

    public partial class LANNetworkDomain : NetworkDomain
    {
        public LANNetworkDomain()
        {
            InitializePackets();
            NetworkDomain.PlatformPeerManager.OnPeerForgotten += (PeerId endPoint) => {
                // first, check if this endpoint is managed by the current NetworkDomain
                // then, check if the peer timed out or if we booted them already (done in the callee)
                OnlinePlayer? maybePeer = GetPlayerLAN(endPoint);
                if (maybePeer is OnlinePlayer peer) {
                    RemoveLANPlayer(peer);
                }
            };
        }

        public class LANLobbyInfo : LobbyInfo
        {
            public override NetworkDomainType domain => NetworkDomainType.LAN;

            public override string directJoinCode => endPoint.ToString();

            public PeerId endPoint;
            public LANLobbyInfo(PeerId endPoint, string name, string mode, int playerCount, bool hasPassword, int maxPlayerCount, string highImpactMods = "", string bannedMods = "") :
                base(name, mode, playerCount, hasPassword, maxPlayerCount, highImpactMods, bannedMods)
            {
                this.endPoint = endPoint;
            }
            public override bool Equals(LobbyInfo other)
            {
                if (other is LANLobbyInfo otherlan) return (endPoint == otherlan.endPoint);
                return false;
            }
        }

        public class LANPlayerId : MeadowPlayerId
        {
            public PeerId endPoint;
            public LANPlayerId(PeerId? endPoint) : base(
                    UsernameGenerator.GenerateRandomUsername(endPoint?.GetHashCode() ?? 0))
            {
                this.endPoint = endPoint ?? PlatformPeerManager.BlackHole;
            }

            public override void OpenProfileLink()
            {
                string dialogue = "";
                bool isMe = isLoopback();
                if (isMe)
                {
                    dialogue += Utils.Translate("Your network interface(s) are");
                    foreach (var ip in UDPPeerManager.getInterfaceAddresses())
                    {
                        dialogue += Environment.NewLine + ip.ToString() + ":" + PlatformPeerManager.port.ToString();
                    }
                }
                else dialogue += Utils.Translate("<NAME> network interface is ").Replace("<NAME>", name) + endPoint.ToString();
                if (OnlineManager.lobby?.owner?.id?.Equals(this) ?? false)
                {
                    string isMe0 = isMe ? "You are" : "This player is";
                    string isMe1 = isMe ? "your" : "their";
                    dialogue += Environment.NewLine + Utils.Translate($"{isMe0} the owner of the lobby.");
                    dialogue += Environment.NewLine + Utils.Translate($"Players can “Direct Connect” to this lobby through {isMe1} interface(s).");
                }
                OnlineManager.instance.manager.ShowDialog(
                    new DialogNotify(dialogue, new Vector2(478.1f, 115.200005f * (1 + 0.2f * UDPPeerManager.getInterfaceAddresses().Length)),
                        OnlineManager.instance.manager, null));
            }

            public void reset()
            {
                this.endPoint = PlatformPeerManager.BlackHole;
            }

            public override int GetHashCode()
            {
                return this.endPoint?.GetHashCode() ?? 0;
            }

            public override void CustomSerialize(Serializer serializer)
            {

                if (serializer.IsWriting)
                {
                    if (this.isLoopback())
                    {
                        serializer.writer.Write(true);
                    }
                    else
                    {
                        serializer.writer.Write(false);
                        NetworkDomain.PlatformPeerManager.SerializePeerId(serializer.writer, this.endPoint);
                    }
                }
                else if (serializer.IsReading)
                {
                    bool issender = serializer.reader.ReadBoolean();
                    if (issender)
                    {
                        this.endPoint = (serializer.currPlayer.id as LANPlayerId)?.endPoint ?? NetworkDomain.PlatformPeerManager.BlackHole;
                    }
                    else
                    {
                        this.endPoint = NetworkDomain.PlatformPeerManager.DeserializePeerId(serializer.reader);
                    }
                }

                ;
            }

            public bool isLoopback()
            {
                if (endPoint is null) return false;
                return endPoint.isLoopback();
            }

            public override bool Equals(MeadowPlayerId other)
            {

                if (other is LANPlayerId lanid)
                {
                    return endPoint == lanid.endPoint;
                }
                return false;
            }
        }

        public override OnlinePlayer CreateMePlayer()
        {
            var op = new OnlinePlayer(new LANPlayerId(PlatformPeerManager.GetSelf()))
            { isMe = true };

            if (!string.IsNullOrWhiteSpace(RainMeadow.rainMeadowOptions.LanUserName.Value))
            {
                op.id.name = RainMeadow.rainMeadowOptions.LanUserName.Value;
            }

            return op;
        }


        public override void RequestLobbyList()
        {
            // To create a proper list, we need to send a message to the broadcast endpoint.
            // and wait for responces from possible hosts.
            for (int i = 0; i < 8; i++)
            {
                using (MemoryStream memoryStream = new())
                using (BinaryWriter writer = new(memoryStream))
                {
                    SendBroadcast(new RequestLobbyPacket());
                }
            }
        }

        public void AddLobby(LANLobbyInfo lobby)
        {
            OnLobbyListReceivedEvent(true, [ lobby ]);
        }


        public void SendLobbyInfo(PeerId endPoint)
        {
            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner)
            {
                var packet = new InformLobbyPacket(
                    maxplayercount, Utils.Translate("LAN Lobby"), OnlineManager.lobby.hasPassword,
                    OnlineManager.lobby.gameModeType.value, OnlineManager.players.Count,
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()), RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods()));
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    Packet.Encode(packet, writer, endPoint);
                    PlatformPeerManager.Send(memory.GetBuffer(), endPoint, BasePeerManager.PacketType.UnreliableBroadcast, false);
                }
            }
        }

        public override bool canDirectConnect => true;
        public override LobbyInfo GenerateDCLobbyInfo(string connectstr)
        {
            var endpoint = PlatformPeerManager.GetPeerIdByName(connectstr);
            if (endpoint != null)
            {
                return new LANNetworkDomain.LANLobbyInfo(endpoint, "Direct Connection", "Meadow", 0, true, 2);
            }
            else
            {
                throw new FormatException("IP Address format should be xxx.xxx.xxx.xxx:port");
            }
        }

        public OnlinePlayer? GetPlayerLAN(PeerId other, bool create = false)
        {
            var player = OnlineManager.players.FirstOrDefault(p =>
            {
                if (p.id is LANPlayerId lanid)
                    if (lanid.endPoint != null)
                        return lanid.endPoint == other;
                return false;
            });

            if (player is null && create)
            {
                RainMeadow.Debug($"Couldn't find player with endpoint {other}. Creating one...");
                player = new OnlinePlayer(new LANPlayerId(other));
            }

            return player;
        }

        public override bool canSendChatMessages => true;
        public override void SendChatMessage(string message)
        {
            foreach (OnlinePlayer player in OnlineManager.players)
            {
                if (player.isMe) continue;
                SendP2P(player, new ChatMessagePacket(message), BasePeerManager.PacketType.Reliable);
            }

            RecieveChatMessage(OnlineManager.mePlayer, message);
        }

        public int maxplayercount = 0;
        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount)
        {
            NetworkDomain.currentDomain = NetworkDomainType.LAN;
            maxplayercount = maxPlayerCount ?? 0;
            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            NetworkDomain.OnLobbyJoinedEvent(true, "");
        }

        public void LobbyAcknoledgedUs(OnlinePlayer owner)
        {
            RainMeadow.DebugMe();
            if (OnlineManager.lobby is null)
            {
                OnlineManager.lobby = new Lobby(
                    new OnlineGameMode.OnlineGameModeType(OnlineManager.currentlyJoiningLobby.mode, false),
                    owner, lobbyPassword);
            }

        }


        public void AcknoledgeLANPlayer(OnlinePlayer joiningPlayer)
        {
            var lanid = joiningPlayer.id as LANPlayerId;
            if (lanid is null) return;
            if (lanid.isLoopback()) return;


            RainMeadow.DebugMe();
            if (OnlineManager.players.Contains(joiningPlayer)) { return; }
            OnlineManager.AddPlayer(joiningPlayer);
            SendAcknoledgement(joiningPlayer);
            RainMeadow.Debug($"Added {joiningPlayer} to the lobby matchmaking player list");

            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner)
            {

                // Tell the other players to create this player
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (player.isMe || player == joiningPlayer)
                        continue;

                    SendP2P(player, new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Add, new OnlinePlayer[] { joiningPlayer }),
                        BasePeerManager.PacketType.Reliable);
                }

                // Tell joining peer to create everyone in the server
                SendP2P(joiningPlayer, new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Add,
                    OnlineManager.players.Append(OnlineManager.mePlayer).ToArray()),
                    BasePeerManager.PacketType.Reliable);
            }

            OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
        }

        public void RemoveLANPlayer(OnlinePlayer leavingPlayer)
        {
            StackTrace stackTrace = new();
            RainMeadow.Debug(stackTrace.ToString());


            if (leavingPlayer.isMe) return;
            if (!OnlineManager.players.Contains(leavingPlayer)) { return; }
            OnlineManager.RemovePlayer(leavingPlayer);
            if (OnlineManager.lobby is not null)
                if (OnlineManager.lobby.isOwner)
                {
                    // Tell the other players to remove this player
                    foreach (OnlinePlayer player in OnlineManager.players)
                    {
                        if (player.isMe)
                            continue;

                        SendP2P(player, new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Remove, new OnlinePlayer[] { leavingPlayer }),
                            BasePeerManager.PacketType.Reliable);
                    }
                }
            ForgetPlayer(leavingPlayer);
            OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
        }
        string lobbyPassword = "";
        public override void RequestJoinLobby(LobbyInfo lobby, string? password)
        {
            NetworkDomain.currentDomain = NetworkDomainType.LAN;
            RainMeadow.DebugMe();
            if (lobby is LANLobbyInfo lobbyinfo)
            {
                lobbyPassword = password ?? "";
                OnlineManager.currentlyJoiningLobby = lobby;
                var lobbyInfo = (LANLobbyInfo)lobby;
                if (lobbyInfo.endPoint == null)
                {
                    RainMeadow.Debug("Failed to join local game...");
                    return;
                }

                RainMeadow.Debug("Sending Request to join lobby...");
                SendP2P(new OnlinePlayer(new LANPlayerId(lobbyInfo.endPoint)),
                    new RequestJoinPacket(OnlineManager.mePlayer.id.name), BasePeerManager.PacketType.Reliable, true);
            }
            else
            {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        public override void HandleLeavingLobby()
        {
            if (OnlineManager.players is not null)
            {
                if (OnlineManager.players.Count > 1)
                {
                    foreach (OnlinePlayer p in OnlineManager.players)
                    {
                        SendP2P(p,
                            new SessionEndPacket(),
                                BasePeerManager.PacketType.Unreliable);
                    }
                }
            }
            ForgetEverything();
        }

        public override OnlinePlayer? GetLobbyOwner()
        {
            if (OnlineManager.lobby == null) return null;
            if (OnlineManager.lobby.owner is null || OnlineManager.lobby.owner.hasLeft)
            {
                // select a new owner.
                // The order of players should be
                for (int i = 0; i < OnlineManager.players.Count; i++)
                {
                    OnlinePlayer onlinePlayer = OnlineManager.players[i];
                    if (onlinePlayer.hasLeft) continue;
                    return onlinePlayer;
                }

                return null;
            }

            return OnlineManager.lobby.owner;
        }

        public override MeadowPlayerId GetEmptyId()
        {
            return new LANPlayerId(null);
        }


        public override void OpenInvitationOverlay()
        {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify(Utils.Translate("You cannot use this feature here."), OnlineManager.instance.manager, null));
        }

        public override bool canOpenInvitations => false;
    }
}
