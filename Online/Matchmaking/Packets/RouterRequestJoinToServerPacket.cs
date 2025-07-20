using System;
using System.IO;
using System.Collections.Generic;
using MonoMod.Utils;

namespace RainMeadow
{
    public class RouterRequestJoinToServerPacket : Packet
    {
        public override Type type => Type.RouterRequestJoinToServer;
        // roles: P->S

        public ulong lobbyId = 0;

        public RouterRequestJoinToServerPacket() {}
#if !IS_SERVER
        public RouterRequestJoinToServerPacket(ulong lobbyId) {
            this.lobbyId = lobbyId;
        }
#endif

        public override void Process()
        {
#if IS_SERVER
            var packet = new RouterModifyPlayerListPacket(
                ModifyPlayerListPacketOperation.Add,
                LobbyServer.lobbyPlayers.ToArray()
            );
            LobbyServer.netIo.SendP2P(processingPlayer, packet, NetIO.SendType.Reliable);
            packet = new RouterModifyPlayerListPacket(
                ModifyPlayerListPacketOperation.Add,
                new OnlinePlayer[] {processingPlayer}
            );
            LobbyServer.netIo.SendP2P(LobbyServer.GetLobbyPlayer((RouterPlayerId)LobbyServer.lobby.host), packet, NetIO.SendType.Reliable);
#else
            throw new Exception("This function must only be called server-side");
#endif
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
    }
}
