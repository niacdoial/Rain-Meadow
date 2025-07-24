using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
//using Kittehface.Framework20;
using RainMeadow.Shared;

namespace RainMeadow
{
    partial class NetIOPlatform {
        static partial void PlatformLanAvailable(ref bool val) {
            val = NetIOPlatform.PlatformUDPManager is not null;
        }
        public static LANNetIO? lanInstance { get => (LANNetIO)instances[MatchmakingManager.MatchMakingDomain.LAN]; }
    }

    public class LANNetIO : NonSteamNetIO {

        public override bool IsActive() {
            if (NetIOPlatform.PlatformUDPManager is null) {
                RainMeadow.Error("Cannot perform action without a functionning UDPPeerManager");
                return false;
            }
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) {
                RainMeadow.Error("Action performed on the wrong type of MatchMakingDomain");
                return false;
            }
            return true;
        }

        public LANNetIO() {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            NetIOPlatform.PlatformUDPManager.OnPeerForgotten += (peer) => {
                if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.LAN) {
                    return;
                }
                List<OnlinePlayer> playerstoRemove = new();
                foreach (OnlinePlayer player in OnlineManager.players) {
                    if (player.id is LANPlayerId lanid) {
                        if (lanid.endPoint is null) continue;
                        if (UDPPeerManager.CompareIPEndpoints(lanid.endPoint, peer)) {
                            if ((OnlineManager.lobby?.owner is OnlinePlayer owner && owner == player) ||
                                (OnlineManager.lobby?.isOwner ?? true)
                            ) {
                                playerstoRemove.Add(player);

                            }
                            break;  // only one player matches that if block
                        }
                    }
                }

                foreach (var player in playerstoRemove)
                    // this has a built-in test (checking if the player is in OnlineManager)
                    // to determine if the current closure is being called from a timeout or from a voluntary disconnect
                    ((LANMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.LAN]).RemoveLANPlayer(player);
            };
        }
        public override void SendSessionData(BasicOnlinePlayer toPlayer)
        {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            if (!(toPlayer is OnlinePlayer)) {
                RainMeadow.Error("Low-level network code ate half the OnlinePlayer object!");
                return;
            }
            try
            {
                OnlineManager.serializer.WriteData((OnlinePlayer)toPlayer);
                SendP2P(toPlayer, new SessionPacket(OnlineManager.serializer.buffer, (ushort)OnlineManager.serializer.Position), SendType.Unreliable);
                OnlineManager.serializer.EndWrite();
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                OnlineManager.serializer.EndWrite();
                throw;
            }

        }

        // "Target runtime doesn't support covariant return types in overrides." (sigh)
        public override BasicOnlinePlayer? GetPlayerFromEndPoint(IPEndPoint iPEndPoint) {
            var player = MatchmakingManager.lanInstance.GetPlayerLAN(iPEndPoint);
            if (player is null)
            {
                RainMeadow.Debug("Player not found! Instantiating new at: " + iPEndPoint.Port);
                var playerid = new LANPlayerId(iPEndPoint);
                player = new OnlinePlayer(playerid);
            }
            return player as BasicOnlinePlayer;
        }

        public override void ProcessPacket(Packet packet, BasicOnlinePlayer player) {
            if (player is OnlinePlayer plr) {
                PacketProcessing.ProcessPacketForLAN(packet, plr);
            } else {
                throw new Exception("Shared network code lost half the OnlinePlayer object");
            }
        }

        public virtual void SendBroadcast(Packet packet) {
            if (!IsActive()) return;

            RainMeadow.DebugMe();
            for (int broadcast_port = UDPPeerManager.DEFAULT_PORT;
                broadcast_port < (UDPPeerManager.FIND_PORT_ATTEMPTS + UDPPeerManager.DEFAULT_PORT);
                broadcast_port++) {
                IPEndPoint point = new(IPAddress.Broadcast, broadcast_port);

                var player = MatchmakingManager.lanInstance.GetPlayerLAN(point);  // TODO: there's no broadcast player, right?
                if (player == null)
                {
                    RainMeadow.Debug("Player not found! Instantiating new at: " + point);
                    var playerid = new LANPlayerId(point);
                    player = new OnlinePlayer(playerid);
                }

                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory)) {
                    Packet.Encode(packet, writer, player);

                    for (int i = 0; i < 4; i++)
                        NetIOPlatform.PlatformUDPManager.Send(
                            memory.GetBuffer(),
                            ((LANPlayerId)player.id).endPoint,
                            UDPPeerManager.PacketType.UnreliableBroadcast,
                            true
                        );
                }
            }
        }
    }
}
