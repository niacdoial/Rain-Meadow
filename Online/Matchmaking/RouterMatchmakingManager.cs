



using System.Collections.Generic;
using System.Net;

namespace RainMeadow {
    class RouterPlayerId : MeadowPlayerId {
        public ulong RoutingId = 0;

        public RouterPlayerId() {}
        public RouterPlayerId(ulong id = 0) { RoutingId = id; }

        override public int GetHashCode() { unchecked { return (int)RoutingId; } }
        override public void CustomSerialize(Serializer serializer) {
            serializer.Serialize(ref RoutingId);
        }

        public override bool Equals(MeadowPlayerId other) {
            if (other is RouterPlayerId other_router_id) {
                return RoutingId == other_router_id.RoutingId;
            }

            return false;
        }
    }



    class ServerRouterPlayerId : RouterPlayerId {
        public IPEndPoint endPoint;

        public ServerRouterPlayerId() {}
        public ServerRouterPlayerId(ulong id, IPEndPoint endPoint) { RoutingId = id; this.endPoint = endPoint; }
    }

    public class RouterMatchmakingManager : MatchmakingManager {

        public override void initializeMePlayer() {
            OnlineManager.mePlayer = new OnlinePlayer( new RouterPlayerId());
            OnlineManager.players = new List<OnlinePlayer>{ OnlineManager.mePlayer };
        }

        public override void RequestLobbyList() {

        }

        LobbyVisibility visibility;
        int? maxPlayerCount;
        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount) {
            maxPlayerCount = maxPlayerCount ?? 4;
            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            MatchmakingManager.OnLobbyJoinedEvent(true, "");
        }

        public override void RequestJoinLobby(LobbyInfo lobby, string? password) {

        }

        public override void JoinLobby(bool success) {

        }

        public override void JoinLobbyUsingArgs(params string?[] args) {
            if (args.Length >= 2 && long.TryParse(args[0], out var address) && int.TryParse(args[1], out var port))
            {
                RainMeadow.Debug($"joining lobby with address {address} and port {port} from the command line");
                RequestJoinLobby(new INetLobbyInfo(new IPEndPoint(address, port), "", "", 0, false, 4), args.Length > 2 ? args[2] : null);
            }
            else
                RainMeadow.Error($"invalid address and port: {string.Join(" ", args)}");
        }

        public override void LeaveLobby() {

        }

        public override OnlinePlayer GetLobbyOwner() {
            return null;
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
