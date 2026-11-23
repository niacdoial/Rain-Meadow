using System;
using System.Collections.Generic;
using System.IO;
using RainMeadow.Shared;

namespace RainMeadow
{
    public class CustomPacket : Packet
    {
        public string key = "";
        public ArraySegment<byte> data;
        public override Type type => Type.CustomPacket;

        public CustomPacket() { }
        public CustomPacket(string key, ArraySegment<byte> data)
        {
            this.key = key;
            this.data = data;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(this.key);
            writer.Write(this.data.Array, this.data.Offset, this.data.Count);
        }

        public override void Deserialize(BinaryReader reader)
        {
            long orig = reader.BaseStream.Position;
            base.Deserialize(reader);
            this.key = reader.ReadString();
            this.data = new ArraySegment<byte>(reader.ReadBytes((int)(size-(reader.BaseStream.Position-orig))));
        }

        public override void Process()
        {
            if (key == "" || data == null)
            {
                return;
            }
            if (key.Length > 16 || data.Count > 32768)
            {
                RainMeadow.Error($"Custom Packet was too large, the maximum size is 32768");
                return;
            }

            if (NetworkDomain.currentInstance.CustomDataSupported && NetworkDomain.currentInstance is SecuredPeerNetworkDomain domain)
            {
                if (domain.GetPlayerFromPeerID(processingPeer!) is OnlinePlayer player)
                {
                    CustomManager.HandlePacket(player, this);
                }
                else
                {
                    RainMeadow.Error($"Recieved custom packet from unknown player {processingPeer}");
                }

            }

        }

        public void SteamEncode(MemoryStream ms, BinaryWriter writer)
        {
            writer.Write(this.key);
            writer.Write((ushort)this.data.Count);
            writer.Write(this.data.Array, this.data.Offset, this.data.Count);
        }
    }
}
