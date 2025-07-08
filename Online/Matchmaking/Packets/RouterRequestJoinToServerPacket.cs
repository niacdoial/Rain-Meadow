using System.IO;
using System;
using MonoMod.Utils;

namespace RainMeadow
{
    public class RouterRequestJoinToServerPacket : Packet
    {
        public override Type type => Type.RouterRequestJoinToServer;
        // roles: P->S

        public ulong lobbyId = 0;

        public RouterRequestJoinToServerPacket() {}
        public RouterRequestJoinToServerPacket(ulong lobbyId) {
            this.lobbyId = lobbyId;
        }

        public override void Process()
        {
            throw new Exception("TODO: make sure only the server");
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
