using System.IO;
using System;
using MonoMod.Utils;

namespace RainMeadow
{
    public class RouterRequestJoinPacket : Packet
    {
        public override Type type => Type.RouterRequestJoin;
        // roles: P->H

        public string senderUserName = "";
        public ulong lobbyId = 0;

        public RouterRequestJoinPacket() {}
#if !IS_SERVER
        public RouterRequestJoinPacket(MeadowPlayerId player, ulong lobbyId) {
            if (player is RouterPlayerId rPlayer) {
                senderUserName = rPlayer.name;
                this.lobbyId = lobbyId;
            }
        }
#endif
        public override void Process()
        {
#if IS_SERVER
            throw new Exception("This function must only be called player-side");
#else
            RainMeadow.DebugMe();
            if (OnlineManager.lobby != null && MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.Router)
            {
                RouterMatchmakingManager matchmaker = (RouterMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router];

                if (senderUserName.Length > 0) {
                    processingPlayer.id.name = senderUserName;
                }
                if (lobbyId != matchmaker.lobbyId) {
                    RainMeadow.Error("Received a request to join for the wrong lobby ID!");
                    return;
                }

                // Tell everyone else about them
                RainMeadow.Debug("Telling client they got in.");
                matchmaker.AcknoledgeRouterPlayer(processingPlayer);

                // Tell them they are in
                ((RouterNetIO)NetIO.currentInstance).SendP2P(processingPlayer, new RouterAcceptJoinPacket(
                    lobbyId,
                    matchmaker.MAX_LOBBY,
                    OnlineManager.mePlayer.id.name,
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
            writer.Write(lobbyId);
            writer.WriteNullTerminatedString(senderUserName);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            lobbyId = reader.ReadUInt64();
            senderUserName = reader.ReadNullTerminatedString();
        }
    }
}
