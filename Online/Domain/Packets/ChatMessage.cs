using System.IO;
using RainMeadow.Shared;

namespace RainMeadow
{
    public class ChatMessagePacket : Packet
    {
        public string message = "";

        public ChatMessagePacket(): base() {}
        public ChatMessagePacket(string message)
        {
            this.message = message;
        }

        public override void Serialize(BinaryWriter writer)
        {
            base.Serialize(writer);
            writer.Write(message);
        }

        public override void Deserialize(BinaryReader reader)
        {
            base.Deserialize(reader);
            message = reader.ReadString();
        }


        public override Type type => Type.ChatMessage;

        public override void Process() {
            var matchmaker = (LANNetworkDomain)NetworkDomain.instances[NetworkDomain.NetworkDomainType.LAN];
            var processingPlayer = matchmaker.GetPlayerLAN(processingPeer, true);
            NetworkDomain.currentInstance.RecieveChatMessage(processingPlayer, message);
        }
    }
}
