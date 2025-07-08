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
        public RouterRequestLobbyPacket(string version) {
            meadowVersion = version;
        }

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

        public override void Process() {
            throw new Exception("TODO: make server-side code");
        }
    }
}
