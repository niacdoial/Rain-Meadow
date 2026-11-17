using RainMeadow.Shared;

namespace RainMeadow
{
    public class RequestLobbyPacket : Packet
    {
        public override Type type => Type.RequestLobby;
        public override bool requireBoxed => false;

        public override void Process()
        {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.LAN) return;
            if (OnlineManager.lobby != null)
            {
                RainMeadow.DebugMe();
                NetworkDomain.LAN?.SendLobbyInfo(processingPeer);
            }

        }
    }
}
