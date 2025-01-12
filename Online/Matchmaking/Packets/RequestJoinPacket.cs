namespace RainMeadow
{
    public class RequestJoinPacket : Packet
    {
        public override Type type => Type.RequestJoin;

        public override void Process()
        {
            RainMeadow.DebugMe();
            if (OnlineManager.lobby != null && MatchmakingManager.currentMatchMaker == MatchmakingManager.MatchMaker.LAN)
            {
                var matchmaker = (MatchmakingManager.instances[MatchmakingManager.MatchMaker.LAN] as LANMatchmakingManager);
                // Tell everyone else about them
                RainMeadow.Debug("Telling client they got in.");
                matchmaker.AcknoledgeLANPlayer(processingPlayer);

                // Tell them they are in
                OnlineManager.netIO.SendP2P(processingPlayer, new JoinLobbyPacket(
                    matchmaker.maxplayercount,
                    "LAN Lobby",
                    OnlineManager.lobby.hasPassword,
                    OnlineManager.lobby.gameModeType.value,
                    OnlineManager.players.Count
                ), NetIO.SendType.Reliable);

                



            }
        }
    }
}