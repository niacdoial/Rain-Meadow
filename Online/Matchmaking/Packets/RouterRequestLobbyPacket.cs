using System;
using System.IO;


namespace RainMeadow
{
    public class RouterRequestLobbyPacket : Packet
    {
        public override Type type => Type.RouterRequestLobby;
        // Role: P->S

        public string meadowVersion = "";

        public RouterRequestLobbyPacket() {}
#if !IS_SERVER
        public RouterRequestLobbyPacket(string version) {
            meadowVersion = version;
        }
#endif

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(meadowVersion);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            meadowVersion = reader.ReadString();
        }

        // // NOTE: these two functions MUST be kept in sync
        // // with ModManager/RainMeadowModManager.cs
        // static string ModArrayToString(string[] requiredMods) {
        //     return string.Join("\n", requiredMods);
        // }
        // static string[] ModStringToArray(string requiredMods) {
        //     return requiredMods.Split('\n');
        // }

        public override void Process() {
#if IS_SERVER
            if (UDPPeerManager.isEndpointLocal(((RouterPlayerId)processingPlayer.id).endPoint)) {
                LobbyServer.netIo.SendP2P(
                    processingPlayer,
                    new RouterGenericFailurePacket("Cannot route players with local-network addresses!"),
                    NetIO.SendType.Reliable
                );
            } else {
                LobbyServer.netIo.SendP2P(processingPlayer, new RouterInformLobbyPacket(
                    (ulong)1,
                    LobbyServer.maxPlayers,
                    ((RouterPlayerId)LobbyServer.lobby.host).name,
                    LobbyServer.lobby.hasPassword,
                    LobbyServer.lobby.mode,
                    LobbyServer.players.Count,
                    LobbyServer.lobby.requiredMods,
                    LobbyServer.lobby.bannedMods
                ), NetIO.SendType.Reliable);
            }
#else
            throw new Exception("This function must only be called server-side");
#endif
        }
    }
}
