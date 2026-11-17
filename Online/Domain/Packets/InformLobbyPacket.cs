using System.IO;
using RainMeadow.Shared;
using RainMeadow.Shared.Models;


namespace RainMeadow
{
    public class InformLobbyPacket : Packet
    {
        public int currentplayercount = default;
        public LobbyParameters parameters;
        public InformLobbyPacket() : base() { }
        public InformLobbyPacket(int currentplayercount, LobbyParameters parameters)
        {
            this.currentplayercount = currentplayercount;
            this.parameters = parameters;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            parameters.Serialize(writer);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            this.parameters = new LobbyParameters(reader);
        }

        public override Type type => Type.InformLobby;
        public override bool requireBoxed => false;  // TODO: is this needed? does it do anything?

        public override void Process()
        {
            RainMeadow.DebugMe();
            var lobbyinfo = MakeLobbyInfo();
            NetworkDomain.LAN?.AddLobby(lobbyinfo);
        }

        public LANNetworkDomain.LANLobbyInfo MakeLobbyInfo()
        {
            return new LANNetworkDomain.LANLobbyInfo(processingPeer, currentplayercount, parameters);
        }

    }
}
