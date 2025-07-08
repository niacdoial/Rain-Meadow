using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using MonoMod.Utils;

namespace RainMeadow
{
    public enum ModifyPlayerListPacketOperation : byte
    {
        Add,
        Remove,
    }
    public class LANModifyPlayerListPacket : Packet
    {
        public override Type type => Type.LANModifyPlayerList;

        private ModifyPlayerListPacketOperation modifyOperation;
        private OnlinePlayer[] players;

        public LANModifyPlayerListPacket() : base() { }
        public LANModifyPlayerListPacket(ModifyPlayerListPacketOperation modifyOperation, OnlinePlayer[] players) : base()
        {
            this.modifyOperation = modifyOperation;
            this.players = players;
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)modifyOperation);
            var lanids = players.Select(x => (LANPlayerId)x.id);
            lanids = lanids.Where(x => x.endPoint != null);


            bool includeme = lanids.FirstOrDefault(x => x.isLoopback()) is not null;
            if (includeme)
                lanids = lanids.Where(x => !x.isLoopback());
            var processinglanid = (LANPlayerId)processingPlayer.id;
            UDPPeerManager.SerializeEndPoints(writer, lanids.Select(x => x.endPoint).ToArray(), processinglanid.endPoint, includeme);

            if (modifyOperation == ModifyPlayerListPacketOperation.Add) {
                if (includeme)
                    writer.WriteNullTerminatedString(OnlineManager.mePlayer.id.name);

                foreach (MeadowPlayerId id in lanids)
                    writer.WriteNullTerminatedString(id.name);
            }
        }

        public override void Deserialize(BinaryReader reader)
        {
            modifyOperation = (ModifyPlayerListPacketOperation)reader.ReadByte();
            var endpoints = UDPPeerManager.DeserializeEndPoints(reader, (processingPlayer.id as LANPlayerId).endPoint);


            var lanmatchmaker = (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN] as LANMatchmakingManager);

            if (modifyOperation == ModifyPlayerListPacketOperation.Add) {
                players = endpoints.Select(x => new OnlinePlayer(new LANPlayerId(x))).ToArray();
                for (int i = 0; i < players.Length; i++){
                    players[i].id.name = reader.ReadNullTerminatedString();
                }
            }

            else if (modifyOperation == ModifyPlayerListPacketOperation.Remove)
                players = endpoints.Select(x => lanmatchmaker.GetPlayerLAN(x)).ToArray();

        }

        public override void Process()
        {
            switch (modifyOperation)
            {
                case ModifyPlayerListPacketOperation.Add:
                    RainMeadow.Debug("Adding players...\n\t" + string.Join<OnlinePlayer>("\n\t", players));
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (((LANPlayerId)players[i].id).isLoopback()) {
                            // That's me
                            // Put me where I belong. (...at the end of the player list?)
                            OnlineManager.players.Remove(OnlineManager.mePlayer);
                            OnlineManager.players.Add(OnlineManager.mePlayer);
                            continue;
                        }

                        (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN] as LANMatchmakingManager).AcknoledgeLANPlayer(players[i]);
                    }
                    break;

                case ModifyPlayerListPacketOperation.Remove:
                    RainMeadow.Debug("Removing players...\n\t" + string.Join<OnlinePlayer>("\n\t", players));
                    for (int i = 0; i < players.Length; i++)
                    {
                        (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN] as LANMatchmakingManager).RemoveLANPlayer(players[i]);
                    }
                    break;
            }
        }
    }
}
