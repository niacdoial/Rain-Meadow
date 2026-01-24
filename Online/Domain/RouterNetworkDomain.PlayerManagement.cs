using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using Menu;
using RainMeadow.Shared;

namespace RainMeadow
{

    public partial class RouterNetworkDomain
    {
        public class RouterPlayerId : MeadowPlayerId
        {
            public ushort routingID;
            public SecuredPeerId? endPoint;
            public RouterPlayerId(ushort routingID) : base(
                    UsernameGenerator.GenerateRandomUsername(routingID))
            {
                this.routingID = routingID;
                endPoint = null;
            }

            public override void OpenProfileLink()
            {
                string dialogue = "RoutingID: " + routingID.ToString();
                if (routingID == (((RouterPlayerId)OnlineManager.mePlayer.id)?.routingID ?? 0)) {
                    dialogue = "my " + dialogue;
                }
                OnlineManager.instance.manager.ShowDialog(
                    new DialogNotify(dialogue, new Vector2(478.1f, 115.200005f * (1 + 0.2f * 8)),
                        OnlineManager.instance.manager, null));
            }

            public override int GetHashCode()
            {
                return routingID;
            }

            public override void CustomSerialize(Serializer serializer)
            {
                serializer.Serialize(ref routingID);
            }

            public override bool Equals(MeadowPlayerId other)
            {
                if (other is RouterPlayerId id)
                {
                    return routingID == id.routingID;
                }

                return false;
            }
        }

        public override MeadowPlayerId GetEmptyId()
        {
            return new RouterPlayerId(0);
        }

        public override OnlinePlayer CreateMePlayer()
        {
            return new OnlinePlayer(new RouterPlayerId(0)
                { name = RainMeadow.rainMeadowOptions.LanUserName.Value })
                { isMe = true };
            // note: we don't set our IP here, because it's not useful to anyone else (because NAT)
        }

        public override SecuredPeerId GetPeerIDFromPlayer(OnlinePlayer player)
        {
            throw new NotImplementedException();
        }

        public override OnlinePlayer? GetPlayerFromPeerID(SecuredPeerId id) => OnlineManager.players.FirstOrDefault(p =>
        {
            if (p.id is RouterPlayerId route) return route.endPoint?.CompareAndUpdate(id) ?? false;
            return false;
        });

        public OnlinePlayer? GetPlayerRouter(ushort routingID, bool create = false)
        {
            var player = OnlineManager.players.FirstOrDefault(p =>
            {
                if (p.id is RouterPlayerId route)
                    if (route.routingID != 0) return route.routingID == routingID;
                return false;
            });

            if (player is null && create)
            {
                RainMeadow.Debug($"Couldn't find player with routing ID {routingID}. Creating one...");
                player = new OnlinePlayer(new RouterPlayerId(routingID));
            }

            return player;
        }

        public void HandleModifyPlayerList(RouterModifyPlayerListPacket packet)
        {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.Router) return;
            if (!ValidateIsFromServer(packet)) return;

            switch (packet.operation)
            {
                case RouterModifyPlayerListPacket.Operation.Update:
                case RouterModifyPlayerListPacket.Operation.Add:
                    for (int i = 0; i < packet.routerIds.Count; i++)
                    {
                        RouterPlayerId playerID = new RouterPlayerId(packet.routerIds[i]);
                        if (!RainMeadow.rainMeadowOptions.RouterExposeIP.Value || packet.endPoints[i] == null)
                        {
                            playerID.endPoint = null;
                        }
                        else
                        {
                            playerID.endPoint = packet.endPoints[i];
                        }
                        playerID.name = packet.userNames[i];

                        OnlinePlayer? addedPlayer = GetPlayerRouter(packet.routerIds[i], false);
                        if (addedPlayer is OnlinePlayer existingPlayer)
                        {
                            if (packet.operation == RouterModifyPlayerListPacket.Operation.Update)
                            {
                                // FIXME: add checks once the PeerManager guarantees player identity
                                existingPlayer.id = playerID;
                                RainMeadow.Debug(String.Format("updating player: {0}, name {1}", playerID.routingID, playerID.name));
                            }
                            else
                            {
                                RainMeadow.Debug(String.Format("redundant add-player: {0}, 'name' {1}", playerID.routingID, playerID.name));
                            }
                            NATPierce(existingPlayer);  // just in case
                        }
                        else
                        {
                            addedPlayer = new OnlinePlayer(playerID);
                            NATPierce(addedPlayer);
                            RainMeadow.Debug(String.Format("new player to acknowledge: {0}, name {1}", playerID.routingID, playerID.name));
                            OnlineManager.AddPlayer(addedPlayer);
                            RainMeadow.Debug($"Added {addedPlayer} to the lobby matchmaking player list");
                        }
                    }
                    break;

                case RouterModifyPlayerListPacket.Operation.Remove:
                    for (int i = 0; i < packet.routerIds.Count; i++)
                    {
                        RemoveRouterPlayer(GetPlayerRouter(packet.routerIds[i], true));
                    }
                    break;
            }
            OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
        }

        public override void AcceptOrRejectPlayer(OnlinePlayer player, bool accept) {
            if (player.id is RouterPlayerId joiningId)
            if (GetLobbyOwner() == OnlineManager.mePlayer) {
                PlayerJoiningDecision.Decision decision = accept switch
                {
                    true => PlayerJoiningDecision.Decision.Accept,
                    false => PlayerJoiningDecision.Decision.Reject
                };

                SendPacket(
                    serverPeer,
                    new PlayerJoiningDecision(joiningId.routingID, decision),
                    PacketReliability.Reliable
                );
            }
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

        public void RemoveRouterPlayer(OnlinePlayer leavingPlayer)
        {
            if (leavingPlayer.isMe) return;
            if (!OnlineManager.players.Contains(leavingPlayer)) { return; }
            OnlineManager.RemovePlayer(leavingPlayer);
            ForgetPlayer(leavingPlayer);
            OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
        }

        public override void ForgetPlayer(OnlinePlayer player)
        {
            if (PlatformPeerManager is null) return;
            if (player.id is RouterNetworkDomain.RouterPlayerId routid)
            {
                if (routid.endPoint == serverPeer) return;   // do not forget the server accidentally!
                if (routid.endPoint is null) return;
                PlatformPeerManager.ForgetPeer(routid.endPoint);
            }
        }

        public override void ForgetEverything()
        {
            base.ForgetEverything();
            //serverPeer = null;  // do not reset server, it can be re-used in "knocking" lobby setup.
        }
    }
}
