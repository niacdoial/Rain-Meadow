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
        static partial void PlatformRouterAvailable(ref bool val) {
            val = NetIOPlatform.PlatformUDPManager is not null;
        }
        public static RouterNetIO? routerInstance { get => (RouterNetIO)instances[MatchmakingManager.MatchMakingDomain.Router]; }
    }

    public class RouterNetIO : NonSteamNetIO {
        private IPEndPoint serverEndPoint;  // TODO implement a way to update that without powercycling the game
        public const ulong serverRoutingId = 0xffff_ffff_ffff_ffff;  // TODO make shared constant
        public OnlinePlayer serverPlayer;

        public override bool IsActive() {
            if (NetIOPlatform.PlatformUDPManager is null) {
                RainMeadow.Error("Cannot perform action without a functionning UDPPeerManager");
                return false;
            }
            if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) {
                RainMeadow.Error("Action performed on the wrong type of MatchMakingDomain");
                return false;
            }
            return true;
        }

        public RouterNetIO() {
            if (NetIOPlatform.PlatformUDPManager is null) return;
            NetIOPlatform.PlatformUDPManager.OnPeerForgotten += (peer) => {
                if (MatchmakingManager.currentDomain != MatchmakingManager.MatchMakingDomain.Router) {
                    return;
                }
                List<OnlinePlayer> playerstoRemove = new();
                foreach (OnlinePlayer player in OnlineManager.players) {
                    if (player.id is RouterPlayerId routid) {
                        if (routid.RoutingId is 0) continue;
                        if (UDPPeerManager.CompareIPEndpoints(routid.endPoint, peer)) {
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
                    ((RouterMatchmakingManager)MatchmakingManager.instances[MatchmakingManager.MatchMakingDomain.Router]).RemoveRouterPlayer(player);
            };

            RainMeadow.Debug("what the settings say about the server's IP, *apparently*: " + RainMeadow.rainMeadowOptions.MatchmakingRouter.Value);
            serverEndPoint = UDPPeerManager.GetEndPointByName(
                //RainMeadow.rainMeadowOptions.MatchmakingRouter.Value
                "127.0.0.1:8720"
            );
            RouterPlayerId serverId = new RouterPlayerId(serverRoutingId);
            serverId.endPoint = serverEndPoint;
            serverId.name = "SERVER";
            serverPlayer = new OnlinePlayer(serverId);
        }

        public override void SendSessionData(BasicOnlinePlayer toPlayer)
        {
            if (NetIOPlatform.PlatformUDPManager is null) return;
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

        // public override void SendBroadcast(Packet packet) {
        //     throw new NotImplementedException(); // RouterNetIO cannot send packets to Broatcast
        // }

        // "Target runtime doesn't support covariant return types in overrides." (sigh)
        public override BasicOnlinePlayer? GetPlayerFromEndPoint(IPEndPoint iPEndPoint) {
            var player = MatchmakingManager.routerInstance.GetPlayerRouter(iPEndPoint);
            if (player is null) {
                RainMeadow.Error("Routed player not found! Cannot instantiate new one without heads-up");
            }
            return player as BasicOnlinePlayer;
        }

        public override void ProcessPacket(Packet packet, BasicOnlinePlayer player) {
            if (player is OnlinePlayer plr) {
                PacketProcessing.ProcessPacketForRouter(packet, plr);
            } else {
                throw new Exception("Shared network code lost half the OnlinePlayer object");
            }
        }
    }
}
