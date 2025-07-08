using System;
using System.IO;

namespace RainMeadow
{
    public abstract class Packet
    {
        public enum Type : byte
        {
            None,

            LANModifyPlayerList,
            LANRequestJoin,
            LANAcceptJoin,
            LANRequestLobby,
            LANInformLobby,

            RouterModifyPlayerList,
            RouterRequestJoin,
            RouterRequestJoinToServer,
            RouterAcceptJoin,
            RouterRequestLobby,
            RouterInformLobby,
            RouterPublishLobby,
            RouterAcceptPublish,

            Session,
            SessionEnd,
            ChatMessage,
        }

        public abstract Type type { get; }
        public ushort size = 0;
        public ulong routingFrom = 0;
        public ulong routingTo = 0;

        public virtual void Serialize(BinaryWriter writer) { } // Write into bytes
        public virtual void Deserialize(BinaryReader reader) { } // Read from bytes
        public virtual void Process() { } // Do the payload

        public static OnlinePlayer processingPlayer;
        public static void Encode(Packet packet, BinaryWriter writer, OnlinePlayer toPlayer)
        {
            processingPlayer = toPlayer;

            if (MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.Router) {
                if (OnlineManager.mePlayer.id is RouterPlayerId meId && toPlayer.id is RouterPlayerId toId) {
                    packet.routingFrom = meId.RoutingId;
                    packet.routingTo = toId.RoutingId;
                    writer.Write(packet.routingTo);
                    writer.Write(packet.routingFrom);
                }

            }

            writer.Write((byte)packet.type);
            long payloadPos = writer.Seek(2, SeekOrigin.Current);


            packet.Serialize(writer);
            packet.size = (ushort)(writer.BaseStream.Position - payloadPos);

            writer.Seek((int)payloadPos - 2, SeekOrigin.Begin);
            writer.Write(packet.size);
            writer.Seek(packet.size, SeekOrigin.Current);
        }

        public static void Decode(BinaryReader reader, OnlinePlayer fromPlayer)
        {
            processingPlayer = fromPlayer;

            Type type = (Type)reader.ReadByte();
            // RainMeadow.Debug($"Recieved {type}");
            //RainMeadow.Debug("Got packet type: " + type);

            ulong routingTo = 0;
            ulong routingFrom = 0;
            if (MatchmakingManager.currentDomain == MatchmakingManager.MatchMakingDomain.Router) {
                routingTo = reader.ReadUInt64();
                routingFrom = reader.ReadUInt64();
                if (routingTo != ((RouterPlayerId)OnlineManager.mePlayer.id).RoutingId) {
                    RainMeadow.Error("BAD ROUTING: received a packet of type " + type.ToString() + " destined to user " + routingTo.ToString());
                    return;
                }
                if (routingFrom != ((RouterPlayerId)fromPlayer.id).RoutingId) {
                    RainMeadow.Error(
                        "BAD ROUTING: received a packet from "
                        + ((RouterPlayerId)fromPlayer.id).RoutingId +
                        " but sender field reads " + routingTo.ToString()
                    );
                    return;
                }
            }

            Packet? packet = type switch
            {
                // most common first (if switch is closer to a bunch of "if"s than a lookup table like in C)
                Type.Session => new SessionPacket(),
                Type.ChatMessage => new ChatMessagePacket(),
                Type.SessionEnd => new SessionEndPacket(),
                Type.LANModifyPlayerList => new LANModifyPlayerListPacket(),
                Type.LANAcceptJoin => new LANAcceptJoinPacket(),
                Type.LANRequestJoin => new LANRequestJoinPacket(),
                Type.LANRequestLobby => new LANRequestLobbyPacket(),
                Type.LANInformLobby => new LANInformLobbyPacket(),
                Type.RouterModifyPlayerList => new RouterModifyPlayerListPacket(),
                Type.RouterAcceptJoin => new RouterAcceptJoinPacket(),
                Type.RouterRequestJoinToServer => new RouterRequestJoinToServerPacket(),
                Type.RouterRequestJoin => new RouterRequestJoinPacket(),
                Type.RouterRequestLobby => new RouterRequestLobbyPacket(),
                Type.RouterInformLobby => new RouterInformLobbyPacket(),
                Type.RouterPublishLobby => new RouterPublishLobbyPacket(),
                Type.RouterAcceptPublish => new RouterAcceptPublishPacket(),

                _ => null
            };

            if (packet == null) {
                // throw new Exception($"Undetermined packet type ({type}) received");
                RainMeadow.Error("Bad Packet Type Recieved");
                return;
            }
            packet.routingTo = routingTo;
            packet.routingFrom = routingFrom;

            packet.size = reader.ReadUInt16();

            var startingPos = reader.BaseStream.Position;
            try
            {
                packet.Deserialize(reader);
                var readLength = reader.BaseStream.Position - startingPos;

                if (readLength != packet.size) throw new Exception($"Payload size mismatch, expected {packet.size} but read {readLength}");

                packet.Process();
            }
            finally
            {
                // Move stream position to next part of packet
                reader.BaseStream.Position = startingPos + packet.size;
            }
        }
    }
}
