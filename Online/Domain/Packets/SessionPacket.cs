using System;
using System.IO;
using RainMeadow.Shared;

namespace RainMeadow
{
    public class SessionPacket : Packet
    {
        public override Type type => Type.Session;
        public override bool requireBoxed => false;
        private ArraySegment<byte> data;

        public SessionPacket() : base() { }
        public SessionPacket(ArraySegment<byte> data) : base()
        {
            this.data = data;
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write(data.Array, data.Offset, data.Count);
        }

        public override void Deserialize(BinaryReader reader)
        {
            long orig = reader.BaseStream.Position;
            base.Deserialize(reader);
            data = new ArraySegment<byte>(reader.ReadBytes((int)(size-(reader.BaseStream.Position-orig))));
        }

        public override void Process()
        {
            if (OnlineManager.lobby is not null)
            {
                if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.LAN) return;
                var player = NetworkDomain.LAN?.GetPlayerLAN(processingPeer, true)!;
                unsafe
                {
                    fixed (byte* pdata = data.Array)
                    {
                        player.UpdateSessionBuffer((IntPtr)(pdata + data.Offset), data.Count);
                    }
                }

            }
        }
    }
}
