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
            var lanids = players.Select(x => (LANNetworkDomain.LANPlayerId)x.id);
            lanids = lanids.Where(x => x.endPoint != null);

            bool includeme = lanids.FirstOrDefault(x => x.isLoopback()) is not null;

            lanids = lanids.Where(x => !x.isLoopback());
            UDPPeerManager.SerializeEndPoints(writer, lanids.Select(x => x.endPoint).ToArray(), processingEndpoint, includeme);

            if (modifyOperation == Operation.Add) {
                if (includeme) {
                    writer.WriteNullTerminatedString(OnlineManager.mePlayer.id.name);
                }

                foreach (LANNetworkDomain.LANPlayerId lanid in lanids){
                    writer.WriteNullTerminatedString(lanid.name);
                }
            }

        }

        public override void Deserialize(BinaryReader reader)
        {
            modifyOperation = (Operation)reader.ReadByte();
            var endpoints = UDPPeerManager.DeserializeEndPoints(reader, processingEndpoint);

            if (modifyOperation == Operation.Add) {
                players = endpoints.Select(x => new OnlinePlayer(new LANNetworkDomain.LANPlayerId(x))).ToArray();
                for (int i = 0; i < players.Length; i++){
                    players[i].id.name = reader.ReadNullTerminatedString();
                }
            }
                
            else if (modifyOperation == Operation.Remove)
                players = endpoints.Select(x => NetworkDomain.LAN.GetPlayerLAN(x)).OfType<OnlinePlayer>().ToArray();

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
                        if (((LANNetworkDomain.LANPlayerId)players[i].id).isLoopback()) {
                            // That's me
                            // Put me where I belong.
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