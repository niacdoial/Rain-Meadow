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
            if (OnlineManager.lobby != null && MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.LAN)
            {
                var matchmaker = (LANMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN];
                var processingPlayer = matchmaker.GetPlayerLAN(processingEndpoint, true);

                if (LanUserName.Length > 0)
                {
                    processingPlayer.id.name = LanUserName;
                }

                // Tell everyone else about them
                RainMeadow.Debug("Telling client they got in.");
                matchmaker.AcknoledgeLANPlayer(processingPlayer);

                // Tell them they are in
                ((LANNetIO)NetIO.instances[MatchmakingManager.MatchMakingDomain.LAN]).SendP2P(processingPlayer, new JoinLobbyPacket(
                    matchmaker.maxplayercount,
                    "LAN Lobby",
                    OnlineManager.lobby.hasPassword,
                    OnlineManager.lobby.gameModeType.value,
                    OnlineManager.players.Count,
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods())
                ), NetIO.SendType.Reliable);

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