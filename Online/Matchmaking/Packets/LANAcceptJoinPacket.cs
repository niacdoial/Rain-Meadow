using System;

namespace RainMeadow
{
    public class LANAcceptJoinPacket : LANInformLobbyPacket
    {
        public override Type type => Type.LANAcceptJoin;

        public LANAcceptJoinPacket() : base() { }
#if !IS_SERVER
        public LANAcceptJoinPacket(int maxplayers, string name, bool passwordprotected, string mode, int currentplayercount, string highImpactMods = "", string bannedMods = "") : base(maxplayers, name, passwordprotected, mode, currentplayercount, highImpactMods, bannedMods) { }
#endif
        public override void Process()
        {
#if IS_SERVER
            throw new Exception("This function must only be called player-side");
#else
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) return;
            var newLobbyInfo = MakeLobbyInfo();

            // If we don't have a lobby and we a currently joining a lobby
            if (OnlineManager.lobby is null && OnlineManager.currentlyJoiningLobby is not null) {
                // If the lobby we want to join is a lan lobby
                if (OnlineManager.currentlyJoiningLobby is INetLobbyInfo oldLobbyInfo) {
                    // If the lobby we want to join is the lobby that allowed us to join.
                    if (oldLobbyInfo.host is LANPlayerId oldHost && newLobbyInfo.host is LANPlayerId newHost)
                        if (UDPPeerManager.CompareIPEndpoints(oldHost.endPoint, newHost.endPoint)) {
                            OnlineManager.currentlyJoiningLobby = newLobbyInfo;
                            LANMatchmakingManager matchMaker = (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN] as LANMatchmakingManager);
                            matchMaker.maxplayercount = newLobbyInfo.maxPlayerCount;
                            matchMaker.LobbyAcknoledgedUs(processingPlayer);
                        }
                }
            }
#endif
        }
    }
}
