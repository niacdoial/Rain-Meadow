using System;
using System.IO;

namespace RainMeadow
{
    public class ChatMessagePacket : Packet
    {
        public string message = "";

        public ChatMessagePacket(): base() {}
#if !IS_SERVER
        public ChatMessagePacket(string message)
        {
            this.message = message;
        }
#endif

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
#if IS_SERVER
            throw new Exception("This function must only be called player-side");
#else
            MatchmakingManager.currentInstance.RecieveChatMessage(processingPlayer, message);
#endif
        }
    }
}
