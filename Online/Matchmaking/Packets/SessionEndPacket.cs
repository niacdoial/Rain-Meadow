
using RainMeadow.Shared;

namespace RainMeadow
{
    public class SessionEndPacket : Packet
    {
        public override Type type => Type.SessionEnd;

        public override void Process()
        {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) return;
            var lanmatchmaker = (LANMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN];
            var player = lanmatchmaker.GetPlayerLAN(processingEndpoint);
            if (player is not null) NetIO.currentInstance?.ForgetPlayer(player);
        }
    }
}