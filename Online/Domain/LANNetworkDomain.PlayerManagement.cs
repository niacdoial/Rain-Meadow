using System;
using System.Net;
using System.Linq;
using System.IO;
using System.Text;
using Menu;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using RainMeadow.Shared;


namespace RainMeadow
{
    public partial class LANNetworkDomain : SecuredPeerNetworkDomain
    {

        public class LANPlayerId : MeadowPlayerId
        {
            public SecuredPeerId? endPoint;
            public LANPlayerId(SecuredPeerId? endPoint) : base(
                    UsernameGenerator.GenerateRandomUsername(endPoint?.GetHashCode() ?? 0))
            {
                this.endPoint = endPoint;
            }

            public override void OpenProfileLink()
            {
                StringBuilder dialogue = new StringBuilder();
                bool isMe = IsMe();
                if (isMe)
                {
                    dialogue.Append(Utils.Translate("Your network interface(s) are"));
                    foreach (var ip in SharedPlatform.InterfaceAddresses)
                    {
                        dialogue.Append(Environment.NewLine);
                        dialogue.Append(PlatformPeerManager.Me.publicKeyStr);
                        dialogue.Append("@");
                        dialogue.Append(ip.ToString());
                        dialogue.Append(":");
                        dialogue.Append(PlatformPeerManager.Me.endPoint.Port.ToString());
                    }
                }
                else
                {
                    dialogue.Append(Utils.Translate("<NAME>'s network interface is ").Replace("<NAME>", name));
                    dialogue.Append(endPoint.ToString());
                }
                if (OnlineManager.lobby?.owner?.id?.Equals(this) ?? false)
                {
                    dialogue.Append(Environment.NewLine);
                    string isMe0 = isMe ? "You are" : "This player is";
                    string isMe1 = isMe ? "your" : "their";
                    dialogue.Append(Utils.Translate($"{isMe0} the owner of the lobby."));
                    dialogue.Append(Environment.NewLine);
                    dialogue.Append(Utils.Translate($"Players can “Direct Connect” to this lobby through {isMe1} interface(s)."));
                }
                OnlineManager.instance.manager.ShowDialog(
                    new DialogNotify(dialogue.ToString(), new Vector2(478.1f, 115.200005f * (1 + 0.2f * SharedPlatform.InterfaceAddresses.Count)),
                        OnlineManager.instance.manager, null));
            }

            public override int GetHashCode()
            {
                return this.endPoint?.GetHashCode() ?? 0;
            }

            public override void CustomSerialize(Serializer serializer)
            {
                if (PlatformPeerManager is null) throw new Exception("Peermanager is null");
                if (serializer.IsWriting)
                {
                    if (endPoint is null) throw new Exception("Can't serialize null endpoint");
                    if (serializer.currPlayer?.id is not LANPlayerId to) throw new Exception("Can't serialize to non LAN player");
                    if (to.endPoint is null) throw new Exception("Can't serialize to null endpoint");
                    endPoint.Serialize(serializer.writer, to.endPoint, PlatformPeerManager.Me);
                }
                else if (serializer.IsReading)
                {
                    if (NetworkDomain.PlatformPeerManager is null) throw new Exception("Peermanager is null");
                    if (serializer.currPlayer?.id is not LANPlayerId from) throw new Exception("Can't serialize from non LAN player");
                    if (from.endPoint is null) throw new Exception("Can't serialize from null endpoint");
                    endPoint = SecuredPeerId.Deserialize(serializer.reader, from.endPoint, PlatformPeerManager.Me);
                }
            }

            public bool IsMe()
            {
                if (endPoint is null) return false;
                return endPoint.IsLoopback() && endPoint.endPoint.Port == PlatformPeerManager?.port;
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
            if (PlatformPeerManager is null) throw new InvalidProgrammerException("no peer manager");
            var op = new OnlinePlayer(new LANPlayerId(PlatformPeerManager.Me))
            { isMe = true };

            if (!string.IsNullOrWhiteSpace(RainMeadow.rainMeadowOptions.LanUserName.Value))
            {
                op.id.name = RainMeadow.rainMeadowOptions.LanUserName.Value;
            }

            return op;
        }

        public OnlinePlayer? GetPlayerLAN(SecuredPeerId other, bool create = false)
        {
            var player = OnlineManager.players.FirstOrDefault(p =>
            {
                if (p.id is LANPlayerId lanid)
                    if (lanid.endPoint != null)
                        return lanid.endPoint.Equals(other);
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
            if (lanid.IsMe()) return;


            RainMeadow.DebugMe();
            if (OnlineManager.players.Contains(joiningPlayer)) { return; }
            OnlineManager.AddPlayer(joiningPlayer);
            SendAcknoledgement(lanid.endPoint);
            RainMeadow.Debug($"Added {joiningPlayer} to the lobby matchmaking player list");

            if (OnlineManager.lobby is not null && OnlineManager.lobby.isOwner)
            {

                // Tell the other players to create this player
                var newPlayerPacket = new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Add, new OnlinePlayer[] { joiningPlayer }) {boxed=true};
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (player.isMe || player == joiningPlayer)
                        continue;

                    SendP2P(player, newPlayerPacket, PacketReliability.Reliable);
                }

                // Tell joining peer to create everyone in the server
                SendP2P(joiningPlayer, new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Add,
                    OnlineManager.players.ToArray()){boxed=true},
                    PacketReliability.Reliable);

                // tell them they're in
                SendP2P(joiningPlayer, new JoinLobbyPacket(OnlineManager.lobby.participants.Count, GetLobbyParameters()), NetworkDomain.PacketReliability.Reliable);
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
            if (OnlineManager.lobby is not null && OnlineManager.lobby.isOwner)
              {
                // Tell the other players to remove this player
                var removalPacket = new ModifyPlayerListPacket(ModifyPlayerListPacket.Operation.Remove, new OnlinePlayer[] { leavingPlayer }) {boxed=true};
                foreach (OnlinePlayer player in OnlineManager.players)
                {
                    if (player.isMe)
                        continue;

                    SendP2P(player, removalPacket, PacketReliability.Reliable);
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
                // The order of players should be consistant across players, so everyone should agree on this
                for (int i = 0; i < OnlineManager.players.Count; i++)
                {
                    OnlinePlayer onlinePlayer = OnlineManager.players[i];
                    if (onlinePlayer.hasLeft) continue;
                    return onlinePlayer;
                }
                RainMeadow.Error("All players (including us!) seem to have left the lobby.");
                return null;
            }

            return OnlineManager.lobby.owner;
        }

        public override MeadowPlayerId GetEmptyId()
        {
            return new LANPlayerId(null);
        }

        public override SecuredPeerId? GetPeerIDFromPlayer(OnlinePlayer player) => (player.id as LANPlayerId)?.endPoint;
        public override OnlinePlayer? GetPlayerFromPeerID(SecuredPeerId id) => GetPlayerLAN(id);
    }
}
