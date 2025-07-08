using System.IO;
using System;

namespace RainMeadow
{
    public class RouterAcceptPublishPacket : Packet
    {
        public override Type type => Type.RouterAcceptPublish;
        // Role: S->H

        public ulong lobbyId;

        public RouterAcceptPublishPacket(): base() {}
        public RouterAcceptPublishPacket(ulong lobbyId)
        {
            throw new Exception("TODO: server-side code");
            this.lobbyId = lobbyId;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(lobbyId);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            lobbyId = reader.ReadUInt64();
        }

        public override void Process() {
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) return;
            if (OnlineManager.lobby != null) {
                if (OnlineManager.lobby.isOwner) {
                    RainMeadow.DebugMe();
                    RouterMatchmakingManager matchmaker = (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router] as RouterMatchmakingManager);
                    matchmaker.OnLobbyPublished(lobbyId);
                }
            }

        }
    }
}
