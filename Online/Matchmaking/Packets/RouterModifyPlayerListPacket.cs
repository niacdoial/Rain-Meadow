using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using MonoMod.Utils;

namespace RainMeadow
{
    public class RouterModifyPlayerListPacket : Packet
    {
        public override Type type => Type.RouterModifyPlayerList;
        // Roles: any

        private ModifyPlayerListPacketOperation modifyOperation;
        private OnlinePlayer[] players;

        public RouterModifyPlayerListPacket() : base() { }
        public RouterModifyPlayerListPacket(ModifyPlayerListPacketOperation modifyOperation, OnlinePlayer[] players) : base()
        {
            this.modifyOperation = modifyOperation;
            this.players = players;
        }

        public override void Serialize(BinaryWriter writer)
        {
            writer.Write((byte)modifyOperation);

            var lanids = players.Select(x => (RouterPlayerId)x.id);
            lanids = lanids.Where(x => x.endPoint != null && x.RoutingId != 0);

            bool includeme = lanids.FirstOrDefault(x => x.isLoopback()) is not null;
            if (includeme)
                lanids = lanids.Where(x => !x.isLoopback());
            var processinglanid = (RouterPlayerId)processingPlayer.id;
            UDPPeerManager.SerializeEndPoints(writer, lanids.Select(x => x.endPoint).ToArray(), processinglanid.endPoint, includeme);

            if (includeme)
                writer.Write(
                    ((RouterPlayerId) OnlineManager.mePlayer.id).RoutingId
                );
            foreach (RouterPlayerId id in lanids)
                writer.Write(id.RoutingId);

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
            var endpoints = UDPPeerManager.DeserializeEndPoints(reader, (processingPlayer.id as RouterPlayerId).endPoint);

#if !IS_SERVER
            var matchmaker = (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router] as RouterMatchmakingManager);
#endif

            List<RouterPlayerId> routids = default;
            foreach (IPEndPoint end in endpoints) {
                RouterPlayerId id = new RouterPlayerId();
                id.endPoint = end;
                id.RoutingId = reader.ReadUInt64();
                routids.Add(id);
            }

            if (modifyOperation == ModifyPlayerListPacketOperation.Add) {
                players = routids.Select(x => new OnlinePlayer(x)).ToArray();
                for (int i = 0; i < players.Length; i++){
                    players[i].id.name = reader.ReadNullTerminatedString();
                }
            }

            else if (modifyOperation == ModifyPlayerListPacketOperation.Remove) {
#if IS_SERVER
                players = routids.Select(x => LobbyServer.GetPlayer(x)).ToArray();
#else
                players = routids.Select(x => matchmaker.GetPlayerRouter(x)).ToArray();
#endif
            }
        }

        public override void Process()
        {
            switch (modifyOperation)
            {
                case ModifyPlayerListPacketOperation.Add:
                    RainMeadow.Debug("Adding players...\n\t" + string.Join<OnlinePlayer>("\n\t", players));
                    for (int i = 0; i < players.Length; i++)
                    {
#if IS_SERVER
                        LobbyServer.players.Add((OnlinePlayer) players[i]);
#else
                        if (((RouterPlayerId)players[i].id).isLoopback()) {
                            // That's me
                            // Put me where I belong. (...at the end of the player list?)
                            OnlineManager.players.Remove(OnlineManager.mePlayer);
                            OnlineManager.players.Add(OnlineManager.mePlayer);
                            continue;
                        }

                        (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router] as RouterMatchmakingManager).AcknoledgeRouterPlayer(players[i]);
#endif
                    }
                    break;

                case ModifyPlayerListPacketOperation.Remove:
                    RainMeadow.Debug("Removing players...\n\t" + string.Join<OnlinePlayer>("\n\t", players));
                    for (int i = 0; i < players.Length; i++)
                    {
#if IS_SERVER
                        LobbyServer.players.Remove((OnlinePlayer)players[i]);
#else
                        (MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router] as RouterMatchmakingManager).RemoveRouterPlayer(players[i]);
#endif
                    }
                    break;
            }
        }
    }
}
