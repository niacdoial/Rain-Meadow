using System;
using System.IO;
using RainMeadow.Shared;

namespace RainMeadow
{
    public abstract partial class SecuredPeerNetworkDomain : NetworkDomain
    {
        public abstract SecuredPeerId? GetPeerIDFromPlayer(OnlinePlayer player);
        public abstract OnlinePlayer? GetPlayerFromPeerID(SecuredPeerId id);

        public override void ForgetPlayer(OnlinePlayer player)
        {
            if (PlatformPeerManager is null) return;
            SecuredPeerId? peerID = GetPeerIDFromPlayer(player);
            if (peerID is not null)
            {
                PlatformPeerManager.ForgetPeer(peerID);
            }
        }

        public override void ForgetEverything()
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.ForgetAllPeers();
        }
    }
}
