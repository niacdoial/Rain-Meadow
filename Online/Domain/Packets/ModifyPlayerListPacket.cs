using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using MonoMod.Utils;
using RainMeadow.Shared;

namespace RainMeadow
{
    public class ModifyPlayerListPacket : Packet
    {
        public override Type type => Type.ModifyPlayerList;

        public enum Operation : byte
        {
            Add,
            Remove,
        }

        private Operation modifyOperation;
        private OnlinePlayer[] players;

        public ModifyPlayerListPacket() : base() { }
        public ModifyPlayerListPacket(Operation modifyOperation, OnlinePlayer[] players) : base()
        {
            this.modifyOperation = modifyOperation;
            this.players = players;
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)modifyOperation);
            var lanids = players.Select(x => (LANNetworkDomain.LANPlayerId)x.id).Where(x => x.endPoint != null);
            SecuredPeerId.SerializeArray(writer, lanids.Select(x => x.endPoint).OfType<SecuredPeerId>().ToArray(), processingPeer, mePeer);

            if (modifyOperation == Operation.Add) {
                foreach (LANNetworkDomain.LANPlayerId lanid in lanids){
                    writer.Write(lanid.name);
                }
            }

        }

        public override void Deserialize(BinaryReader reader)
        {
            modifyOperation = (Operation)reader.ReadByte();
            SecuredPeerId[] ids = SecuredPeerId.DeserializeArray(reader, processingPeer, mePeer);

            if (modifyOperation == Operation.Add) {
                players = ids.Select(x => NetworkDomain.LAN.GetPlayerLAN(x, true)).ToArray();
                for (int i = 0; i < players.Length; i++){
                    players[i].id.name = reader.ReadString();
                }
            }

            else if (modifyOperation == Operation.Remove)
                players = ids.Select(x => NetworkDomain.LAN.GetPlayerLAN(x)).OfType<OnlinePlayer>().ToArray();

        }

        public override void Process()
        {
            if (NetworkDomain.currentDomain != NetworkDomain.NetworkDomainType.LAN) return;
            switch (modifyOperation)
            {
                case Operation.Add:
                    RainMeadow.Debug("Adding players...\n\t" + string.Join<OnlinePlayer>("\n\t", players));
                    for (int i = 0; i < players.Length; i++)
                    {
                        if (((LANNetworkDomain.LANPlayerId)players[i].id).IsMe())
                        {
                            // That's me
                            // move me in the list, instead of creating a new me from scratch
                            OnlineManager.players.Remove(OnlineManager.mePlayer);
                            OnlineManager.players.Add(OnlineManager.mePlayer);
                            continue;
                        }

                        NetworkDomain.LAN.AcknoledgeLANPlayer(players[i]);
                    }
                    break;

                case Operation.Remove:
                    RainMeadow.Debug("Removing players...\n\t" + string.Join<OnlinePlayer>("\n\t", players));
                    for (int i = 0; i < players.Length; i++)
                    {
                        NetworkDomain.LAN.RemoveLANPlayer(players[i]);
                    }
                    break;
            }

        }
    }
}
