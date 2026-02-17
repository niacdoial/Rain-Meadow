using RainMeadow.Shared;
using RainMeadow.Shared.Models;
namespace RainMeadow
{
    public class JoinLobbyPacket : InformLobbyPacket
    {
        public JoinLobbyPacket() : base() { }
        public JoinLobbyPacket(int currentPlayers, LobbyParameters parameters) : base(currentPlayers, parameters) { }
        public override Type type => Type.JoinLobby;

        public override void Process()
        {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.LAN) return;
            var newLobbyInfo = MakeLobbyInfo();

            // If we don't have a lobby and we a currently joining a lobby
            if (OnlineManager.lobby is null && OnlineManager.currentlyJoiningLobby is not null)
            {
                // If the lobby we want to join is a lan lobby
                if (OnlineManager.currentlyJoiningLobby is LANNetworkDomain.LANLobbyInfo oldLobbyInfo)
                {
                    // If the lobby we want to join is the lobby that allowed us to join.
                    if (oldLobbyInfo.endPoint == newLobbyInfo.endPoint)
                    {
                        OnlineManager.currentlyJoiningLobby = newLobbyInfo;
                        var processingPlayer = NetworkDomain.LAN.GetPlayerLAN(processingPeer, true);
                        NetworkDomain.LAN.maxplayercount = newLobbyInfo.maxPlayerCount;
                        NetworkDomain.LAN.LobbyAcknoledgedUs(processingPlayer);
                    }
                }
            }
        }
    }
}
