using System;
using System.IO;
using System.Net;

namespace RainMeadow
{
    public class RouterPublishLobbyPacket : Packet
    {
        public override Type type => Type.RouterPublishLobby;
        // Role: H->S

        public IPEndPoint hostEndPoint;
        public ulong hostRoutingId;
        public int currentplayercount = default;
        public int maxplayers = default;
        public bool passwordprotected = default;
        public string name = "";
        public string mode = "";
        public string mods = "";
        public string bannedMods = "";

        public RouterPublishLobbyPacket(): base() {}
        public RouterPublishLobbyPacket(int maxplayers, string name, bool passwordprotected, string mode, int currentplayercount, string highImpactMods = "", string bannedMods = "")
        {
            this.currentplayercount = currentplayercount;
            this.mode = mode;
            this.maxplayers = maxplayers;
            this.name = name;
            this.passwordprotected = passwordprotected;
            this.mods = highImpactMods;
            this.bannedMods = bannedMods;
        }

        public RouterPublishLobbyPacket(INetLobbyInfo lobbyInfo)
        {
            this.currentplayercount = lobbyInfo.playerCount;
            this.mode = lobbyInfo.mode;
            this.maxplayers = lobbyInfo.maxPlayerCount;
            this.name = lobbyInfo.name;
            this.passwordprotected = lobbyInfo.hasPassword;
            this.mods = lobbyInfo.requiredMods;
            this.bannedMods = lobbyInfo.bannedMods;
        }

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
            UDPPeerManager.SerializeEndPoints(writer, new IPEndPoint[] {hostEndPoint}, LANPlayerId.BlackHole, false);
            writer.Write(hostRoutingId);
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
            IPEndPoint[] endPointList  = UDPPeerManager.DeserializeEndPoints(reader, LANPlayerId.BlackHole);
            hostEndPoint = endPointList[0];
            hostRoutingId = reader.ReadUInt64();
        }

        public override void Process()
        {
            throw new Exception("TODO server-side");
            // send RouterAcceptPublishPacket back
        }

        public INetLobbyInfo MakeLobbyInfo() {
            RouterPlayerId owner = new RouterPlayerId(hostRoutingId);
            owner.endPoint = hostEndPoint;
            owner.name = "LOBBY OWNER PLACEHOLDER NAME";
            return new INetLobbyInfo(
                owner,
                name, mode,
                currentplayercount, passwordprotected,
                maxplayers, mods, bannedMods
            );
        }
    }
}
