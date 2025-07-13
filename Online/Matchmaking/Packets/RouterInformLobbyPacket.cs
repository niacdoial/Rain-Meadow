using System;
using System.IO;
using System.Net;

namespace RainMeadow
{
    public class RouterInformLobbyPacket : RouterPublishLobbyPacket
    {
        public override Type type => Type.RouterInformLobby;
        // Role: S->P

        public ulong lobbyId;

        public RouterInformLobbyPacket(): base() {}
// note: no static removal of constructors because types inherit this
        public RouterInformLobbyPacket(ulong lobbyId, int maxplayers, string name, bool passwordprotected, string mode, int currentplayercount, string highImpactMods = "", string bannedMods = "") :
            base(maxplayers, name, passwordprotected, mode, currentplayercount, highImpactMods, bannedMods)
        {
            this.lobbyId = lobbyId;
        }

        public RouterInformLobbyPacket(INetLobbyInfo lobbyInfo) : base(lobbyInfo)
        {
            throw new Exception("TODO: server-side code");
            this.lobbyId = lobbyInfo.lobbyId;
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

        public override void Process()
        {
#if IS_SERVER
            throw new Exception("This function must only be called player-side");
#else
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) return;
            if (OnlineManager.instance != null && OnlineManager.lobby != null) {
                if (OnlineManager.lobby.isOwner) {
                    RainMeadow.DebugMe();
                    var lobbyinfo = MakeLobbyInfo();
                    (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router] as RouterMatchmakingManager).addLobby(lobbyinfo);
                }
            }
#endif
        }

    }
}
