namespace RainMeadow
{
    public class LANRequestLobbyPacket : Packet
    {
        public override Type type => Type.LANRequestLobby;

        public override void Process() {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) return;
            if (OnlineManager.lobby != null) {
                RainMeadow.DebugMe();
                (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN] as LANMatchmakingManager).SendLobbyInfo(processingPlayer);
            }

        }
    }
}
