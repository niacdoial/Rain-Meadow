
using RainMeadow.Shared;

namespace RainMeadow
{
    public class SessionEndPacket : Packet
    {
        public override Type type => Type.SessionEnd;

        public override void Process()
        {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.LAN) return;
            var player = NetworkDomain.LAN?.GetPlayerLAN(processingEndpoint);
            if (player is not null) NetworkDomain.LAN?.ForgetPlayer(player);
        }
    }
}