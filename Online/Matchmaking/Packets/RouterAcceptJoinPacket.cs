namespace RainMeadow
{
    public class RouterAcceptJoinPacket : RouterInformLobbyPacket
    {
        public override Type type => Type.RouterAcceptJoin;
        // roles: H->P

        public RouterAcceptJoinPacket() : base() { }
        public RouterAcceptJoinPacket(ulong lobbyId, int maxplayers, string name, bool passwordprotected, string mode, int currentplayercount, string highImpactMods = "", string bannedMods = "") :
            base(lobbyId, maxplayers, name, passwordprotected, mode, currentplayercount, highImpactMods, bannedMods) { }

        public override void Process()
        {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) return;
            var newLobbyInfo = MakeLobbyInfo();

            // If we don't have a lobby and we a currently joining a lobby
            if (OnlineManager.lobby is null && OnlineManager.currentlyJoiningLobby is not null) {
                // If the lobby we want to join is a lan lobby
                if (OnlineManager.currentlyJoiningLobby is INetLobbyInfo oldLobbyInfo) {
                    // If the lobby we want to join is the lobby that allowed us to join.
                    if (oldLobbyInfo.host == newLobbyInfo.host) {
                        OnlineManager.currentlyJoiningLobby = newLobbyInfo;
                        RouterMatchmakingManager matchmaker = (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router] as RouterMatchmakingManager);
                        matchmaker.MAX_LOBBY = newLobbyInfo.maxPlayerCount;
                        matchmaker.LobbyAcknoledgedUs(processingPlayer);
                    }
                }
            }
        }
    }
}
