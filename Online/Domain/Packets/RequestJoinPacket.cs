using System.IO;
using MonoMod.Utils;
using RainMeadow.Shared;

namespace RainMeadow
{
    public class RequestJoinPacket : Packet
    {
        public string LanUserName = "";
        public override Type type => Type.RequestJoin;

        public RequestJoinPacket() {}
        public RequestJoinPacket(string name) {
            LanUserName = name;
        }

        public override void Process()
        {
            RainMeadow.DebugMe();
            if (OnlineManager.lobby != null && NetworkDomain.currentDomain == NetworkDomain.NetworkDomainType.LAN)
            {
                var processingPlayer = NetworkDomain.LAN?.GetPlayerLAN(processingPeer, true);

                if (LanUserName.Length > 0)
                {
                    processingPlayer.id.name = LanUserName;
                }

                // Tell everyone else about them
                RainMeadow.Debug("Telling client they got in.");
                NetworkDomain.LAN?.AcknoledgeLANPlayer(processingPlayer);

                // Tell them they are in
                NetworkDomain.LAN?.SendP2P(processingPlayer, new JoinLobbyPacket(
                    NetworkDomain.LAN.maxplayercount,
                    "LAN Lobby",
                    OnlineManager.lobby.hasPassword,
                    OnlineManager.lobby.gameModeType.value,
                    OnlineManager.players.Count,
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods())
                ), NetworkDomain.PacketReliability.Reliable);

            }
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.WriteNullTerminatedString(LanUserName);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            LanUserName = reader.ReadNullTerminatedString();
        }
    }
}
