using System;
using System.IO;

namespace RainMeadow
{
    public class LANInformLobbyPacket : Packet
    {
        public override Type type => Type.LANInformLobby;

        public int currentplayercount = default;
        public int maxplayers = default;
        public bool passwordprotected = default;
        public string name = "";
        public string mode = "";
        public string mods = "";
        public string bannedMods = "";

        public LANInformLobbyPacket(): base() {}
#if !IS_SERVER
        public LANInformLobbyPacket(int maxplayers, string name, bool passwordprotected, string mode, int currentplayercount, string highImpactMods = "", string bannedMods = "")
        {
            this.currentplayercount = currentplayercount;
            this.mode = mode;
            this.maxplayers = maxplayers;
            this.name = name;
            this.passwordprotected = passwordprotected;
            this.mods = highImpactMods;
            this.bannedMods = bannedMods;
        }
#endif

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(maxplayers);
            writer.Write(currentplayercount);
            writer.Write(passwordprotected);
            writer.Write(name);
            writer.Write(mode);
            writer.Write(mods);
            writer.Write(bannedMods);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            maxplayers = reader.ReadInt32();
            currentplayercount = reader.ReadInt32();
            passwordprotected = reader.ReadBoolean();
            name = reader.ReadString();
            mode = reader.ReadString();
            mods = reader.ReadString();
            bannedMods = reader.ReadString();
        }

        public override void Process()
        {
#if IS_SERVER
            throw new Exception("This function must only be called player-side");
#else
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) return;
            if (OnlineManager.instance != null && OnlineManager.lobby != null) {
                if (OnlineManager.lobby.isOwner) {
                    RainMeadow.DebugMe();
                    var lobbyinfo = MakeLobbyInfo();
                    (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN] as LANMatchmakingManager).addLobby(lobbyinfo);
                }
            }
#endif
        }

        public INetLobbyInfo MakeLobbyInfo() {
            return new INetLobbyInfo(
                processingPlayer.id,
                name, mode,
                currentplayercount, passwordprotected,
                maxplayers,
                mods, bannedMods
            );
        }

    }
}
