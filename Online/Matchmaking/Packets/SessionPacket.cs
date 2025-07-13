using System;
using System.IO;

namespace RainMeadow
{
    public class SessionPacket : Packet
    {
        public override Type type => Type.Session;

        private byte[] data;

        public SessionPacket() : base() { }
#if !IS_SERVER
        public SessionPacket(byte[] data, ushort size) : base()
        {
            this.data = data;
            this.size = size;
        }
#endif
        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(data, 0, size);
        }

        public override void Deserialize(BinaryReader reader)
        {
            data = reader.ReadBytes(size);
        }

        public override void Process()
        {
#if IS_SERVER
            throw new Exception("This function must only be called player-side");
#else
            if (OnlineManager.lobby is not null) {
                Buffer.BlockCopy(data, 0, OnlineManager.serializer.buffer, 0, size);
                OnlineManager.serializer.ReadData(processingPlayer, size);
            }
#endif
        }
    }
}
