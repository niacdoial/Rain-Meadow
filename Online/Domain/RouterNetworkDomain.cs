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

    public partial class RouterNetworkDomain : NetworkDomain
    {
        public RouterNetworkDomain()
        {
            InitializePackets();
        }




        public class RouterLobbyInfo : LobbyInfo
        {
            public IPEndPoint endPoint;
            public RouterLobbyInfo(IPEndPoint endPoint, string name, string mode, int playerCount, bool hasPassword, int maxPlayerCount, string highImpactMods = "", string bannedMods = "") :
                base(name, mode, playerCount, hasPassword, maxPlayerCount, highImpactMods, bannedMods)
            {
                this.endPoint = endPoint;
            }
            public override string GetLobbyJoinCode(string? password = null)
            {
                if (password != null)
                    return $"+connect_router_lobby {endPoint.Address.Address} {endPoint.Port} +lobby_password {password}";
                return $"+connect_router_lobby {endPoint.Address.Address} {endPoint.Port}";
            }
        }

        public class RouterPlayerId : MeadowPlayerId
        {
            // TODO IPEndpoint and NAT stuff
            public ushort routingID;
            public RouterPlayerId(ushort routingID) : base(
                    UsernameGenerator.GenerateRandomUsername(routingID))
            {
                this.routingID = routingID;
            }

            public override void OpenProfileLink()
            {
                string dialogue = "RoutingID: " + routingID.ToString();
                OnlineManager.instance.manager.ShowDialog(
                    new DialogNotify(dialogue, new Vector2(478.1f, 115.200005f * (1 + 0.2f * UDPPeerManager.getInterfaceAddresses().Length)),
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
        
        public override OnlinePlayer CreateMePlayer()
        {
            return new OnlinePlayer(new RouterPlayerId(0)
                { name = RainMeadow.rainMeadowOptions.LanUserName.Value })
                { isMe = true };
        }


        static List<RouterLobbyInfo> lobbyinfo = new();
        public override void RequestLobbyList()
        {
            lobbyinfo.Clear();
        }


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
                RainMeadow.Debug($"Couldn't find player with endpoint {routingID}. Creating one...");
                player = new OnlinePlayer(new RouterPlayerId(routingID));
            }

            return player;
        }

        public override bool canSendChatMessages => false; // TODO: Chat Messages in router domain
        public override void SendChatMessage(string message)
        {
            return;
        }

        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount)
        {
            // maxplayercount = maxPlayerCount ?? 0;
            // OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            // NetworkDomain.OnLobbyJoinedEvent(true, "");
        }

        public void LobbyAcknoledgedUs(ushort mePlayerid)
        {
            RainMeadow.DebugMe();
            if (((RouterPlayerId)OnlineManager.mePlayer.id).routingID == 0)
            {
                OnlineManager.players.Remove(OnlineManager.mePlayer);
                OnlineManager.mePlayer = GetPlayerRouter(mePlayerid, false);
                if (OnlineManager.mePlayer is null)
                {
                    OnlineManager.QuitWithError("Recieved connection packets out of order");
                }

                OnlineManager.mePlayer.isMe = true;
            }

            foreach (var player in OnlineManager.players)
            {
                RainMeadow.Debug($"{player}, {((RouterPlayerId)player.id).routingID}");
            }

            var owner = OnlineManager.players.First();
            if (OnlineManager.lobby is null)
            {
                OnlineManager.lobby = new Lobby(
                    new OnlineGameMode.OnlineGameModeType(OnlineManager.currentlyJoiningLobby.mode, false),
                    owner, lobbyPassword);
            }

        }


        public void AcknoledgeRouterPlayer(OnlinePlayer joiningPlayer)
        {
            RainMeadow.DebugMe();
            if (OnlineManager.players.Contains(joiningPlayer)) { return; }
            OnlineManager.AddPlayer(joiningPlayer);
            RainMeadow.Debug($"Added {joiningPlayer} to the lobby matchmaking player list");
            OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
        }

        public void RemoveRouterPlayer(OnlinePlayer leavingPlayer)
        {
            StackTrace stackTrace = new();
            RainMeadow.Debug(stackTrace.ToString());


            if (leavingPlayer.isMe) return;
            if (!OnlineManager.players.Contains(leavingPlayer)) { return; }
            OnlineManager.RemovePlayer(leavingPlayer);
            ForgetPlayer(leavingPlayer);
            OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
        }

        public override LobbyInfo GenerateDCLobbyInfo(string connectstr)
        {
            var endpoint = UDPPeerManager.GetEndPointByName(connectstr);
            if (endpoint != null)
            {
                return new RouterLobbyInfo(endpoint, "Direct Connection", "Meadow", 0, true, 2);
            }
            else
            {
                throw new FormatException("IP Address format should be xxx.xxx.xxx.xxx:port");
            }
        }

        string lobbyPassword = "";
        public override void RequestJoinLobby(LobbyInfo lobby, string? password)
        {
            RainMeadow.DebugMe();
            if (lobby is RouterLobbyInfo routerLobbyInfo)
            {
                lobbyPassword = password ?? "";
                OnlineManager.currentlyJoiningLobby = lobby;
                hostPeer = routerLobbyInfo.endPoint;
                if (routerLobbyInfo.endPoint == null)
                {
                    RainMeadow.Debug("Failed to join local game...");
                    return;
                }

                RainMeadow.Debug("Sending Request to join lobby...");
                Send(hostPeer, new BeginRouterSession(false), UDPPeerManager.PacketType.Reliable, true);
            }
            else
            {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        public override void JoinLobby(bool success)
        {
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

        public override void JoinLobbyUsingArgs(params string?[] args)
        {
            if (args.Length >= 2 && long.TryParse(args[0], out var address) && int.TryParse(args[1], out var port))
            {
                RainMeadow.Debug($"joining lobby with address {address} and port {port} from the command line");
                RequestJoinLobby(new RouterLobbyInfo(new IPEndPoint(address, port), "", "", 0, false, 4), args.Length > 2 ? args[2] : null);
            }
            else
                RainMeadow.Error($"invalid address and port: {string.Join(" ", args)}");
        }

        public override void HandleLeavingLobby()
        {
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
            return new RouterPlayerId(0);
        }

        public override string GetLobbyID()
        {
            if (OnlineManager.lobby != null)
            {
                return (OnlineManager.lobby.owner.id as RouterPlayerId)?.GetPersonaName() ?? Utils.Translate("Nobody");
            }

            return "Unknown Lan Lobby";
        }
        public override void OpenInvitationOverlay()
        {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify(Utils.Translate("You cannot use this feature here."), OnlineManager.instance.manager, null));
        }

        public override bool canOpenInvitations => false;
    }
}
