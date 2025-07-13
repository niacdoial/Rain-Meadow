using System;

namespace RainMeadow
{
    public class SessionEndPacket : Packet
    {
        public override Type type => Type.SessionEnd;

        public override void Process()
        {
#if IS_SERVER
            throw new Exception("this function must be called from the player side");
#else
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) return;
            NetIO.currentInstance.ForgetPlayer(processingPlayer);
#endif
        }
    }
}
