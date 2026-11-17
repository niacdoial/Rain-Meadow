using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Menu;
using BepInEx;
using RainMeadow.Shared;
using RainMeadow.Shared.Models;
using UnityEngine;  // for Vector2

namespace RainMeadow
{

    public partial class RouterNetworkDomain : SecuredPeerNetworkDomain
    {
        public class RouterPlayerId : MeadowPlayerId
        {
            public ushort routingID;
            public SecuredPeerId? endPoint;
            public PlayerInfo? info;
            public RouterPlayerId(ushort routingID, PlayerInfo? info) : base(
                    UsernameGenerator.GenerateRandomUsername(routingID))
            {
                this.info = info;
                if (info is not null)
                {
                    this.name = info.username;
                }

                this.routingID = routingID;
                endPoint = null;
            }

            public override void OpenProfileLink()
            {
                StringBuilder dialogue = new StringBuilder();
                if (routingID == (((RouterPlayerId)OnlineManager.mePlayer.id)?.routingID ?? 0)) {
                    dialogue.Append("My ");
                }
                dialogue.Append("RoutingID: ");
                dialogue.Append(routingID.ToString());
                dialogue.Append(Environment.NewLine);
                dialogue.Append(Utils.Translate("Players can “Direct Connect” to this lobby through the LobbyServer's address:"));
                dialogue.Append(Environment.NewLine);
                dialogue.Append(NetworkDomain.Router.serverPeer.id.ToString(false));

                OnlineManager.instance.manager.ShowDialog(
                    new DialogNotify(dialogue.ToString(), new Vector2(478.1f, 115.200005f * (1 + 0.2f * 8)),
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
            return new RouterPlayerId(0, null);
        }

        public override OnlinePlayer CreateMePlayer()
        {
            return new OnlinePlayer(new RouterPlayerId(0, new PlayerInfo() { username = RainMeadow.rainMeadowOptions.LanUserName.Value })
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

        public OnlinePlayer? GetPlayerRouter(ushort routingID)
        {
            var player = OnlineManager.players.FirstOrDefault(p =>
            {
                if (p.id is RouterPlayerId route)
                    if (route.routingID != 0) return route.routingID == routingID;
                return false;
            });

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
                        RouterPlayerId playerID = new RouterPlayerId(packet.routerIds[i], packet.userData[i]);
                        OnlinePlayer? addedPlayer = GetPlayerRouter(packet.routerIds[i]);
                        if (addedPlayer is OnlinePlayer existingPlayer)
                        {
                            existingPlayer.id = playerID;
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
                        if (GetPlayerRouter(packet.routerIds[i]) is OnlinePlayer p)
                        {
                            RemoveRouterPlayer(p);
                        }

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
                    serverPeer.id,
                    new PlayerJoiningDecision(joiningId.routingID, decision){boxed=true},
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
                if (routid.endPoint == serverPeer?.id) return;   // do not forget the server accidentally!
                if (routid.endPoint is null) return;
                PlatformPeerManager.ForgetPeer(routid.endPoint);
            }
        }

        public override void ForgetEverything()
        {
            base.ForgetEverything();
            serverPeer = null;
        }
    }
}
