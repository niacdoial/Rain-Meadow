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

        public override void ForgetPlayer(OnlinePlayer player)
        {
            if (PlatformPeerManager is null) return;
            if (player.id is LANNetworkDomain.LANPlayerId lanid)
            {
                PlatformPeerManager.ForgetPeer(lanid.endPoint);
            }
        }

        public override void ForgetEverything()
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.ForgetAllPeers();
        }
    }
}
