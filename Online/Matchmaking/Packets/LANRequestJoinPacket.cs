using System;
using System.IO;
using MonoMod.Utils;

namespace RainMeadow
{
    public class LANRequestJoinPacket : Packet
    {
        public override Type type => Type.LANRequestJoin;

        public string LanUserName = "";

        public LANRequestJoinPacket() {}
#if !IS_SERVER
        public LANRequestJoinPacket(LANPlayerId player) {
            LanUserName = player.name;
        }
#endif
        public override void Process()
        {
#if IS_SERVER
            throw new Exception("This function must only be called player-side");
#else
            RainMeadow.DebugMe();
            if (OnlineManager.lobby != null && MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.LAN)
            {
                var matchmaker = (LANMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN];

                if (LanUserName.Length > 0) {
                    processingPlayer.id.name = LanUserName;
                }

                // Tell everyone else about them
                RainMeadow.Debug("Telling client they got in.");
                matchmaker.AcknoledgeLANPlayer(processingPlayer);

                // Tell them they are in
                ((LANNetIO)NetIO.currentInstance).SendP2P(processingPlayer, new LANAcceptJoinPacket(
                    matchmaker.maxplayercount,
                    "LAN Lobby",
                    OnlineManager.lobby.hasPassword,
                    OnlineManager.lobby.gameModeType.value,
                    OnlineManager.players.Count,
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods())
                ), NetIO.SendType.Reliable);

            }
#endif
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
