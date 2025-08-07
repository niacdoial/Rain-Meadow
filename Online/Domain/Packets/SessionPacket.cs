using System;
using System.IO;
using RainMeadow.Shared;

namespace RainMeadow
{
    public class SessionPacket : Packet
    {
        public override Type type => Type.Session;

        private byte[] data;

        public SessionPacket() : base() { }
        public SessionPacket(byte[] data, ushort size) : base()
        {
            this.data = data;
            this.size = size;
        }

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
            if (OnlineManager.lobby is not null)
            {
                if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.LAN) return;
                Buffer.BlockCopy(data, 0, OnlineManager.serializer.buffer, 0, size);
                OnlineManager.serializer.ReadData(NetworkDomain.LAN?.GetPlayerLAN(processingEndpoint, true), size);
            }
        }
    }
}