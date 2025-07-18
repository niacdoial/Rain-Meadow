using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using MonoMod.Utils;

namespace RainMeadow
{
    public class RouterGenericFailurePacket : Packet
    {
        public override Type type => Type.RouterGenericFailure;
        // Roles: any

        private string message = "";

        public RouterGenericFailurePacket() : base() { }
        public RouterGenericFailurePacket(string message) : base() {
            this.message = message;
        }

        public override void Serialize(BinaryWriter writer) {
            writer.WriteNullTerminatedString(message);
        }

        public override void Deserialize(BinaryReader reader) {
            message = reader.ReadNullTerminatedString();
        }

        public override void Process() {
#if IS_SERVER
            throw new Exception("This function must only be called player-side");
#else
            MatchmakingManager.routerInstance.OnError(this.message);
#endif
        }
    }
}
