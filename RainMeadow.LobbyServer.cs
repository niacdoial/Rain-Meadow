using Menu;
using System;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;

using RainMeadow;

#if IS_SERVER
namespace RainMeadow
{
    class LobbyServer {
        public static RouterServerSideNetIO netIo;
        public static INetLobbyInfo lobby;
        public static List<OnlinePlayer> players;
        public static int maxPlayers = 4;

        public static OnlinePlayer GetPlayer(RouterPlayerId playerId) {
            OnlinePlayer candidate = LobbyServer.players.FirstOrDefault(p => {
                if (p.id is RouterPlayerId routid)
                    return (routid == playerId);
                return false;
            });
            RouterPlayerId candId = (RouterPlayerId)candidate.id;
            if (!UDPPeerManager.CompareIPEndpoints(candId.endPoint, playerId.endPoint)) {
                RainMeadow.Error("player IDs don't agree on endpoint: " + candId.endPoint.ToString() + " vs " + playerId.endPoint.ToString());
                throw new Exception("WHY");
            }
            return candidate;
        }
        public static OnlinePlayer GetPlayer(IPEndPoint endPoint) {
            OnlinePlayer candidate = LobbyServer.players.FirstOrDefault(p => {
                if (p.id is RouterPlayerId routid && routid.endPoint != null)
                    return UDPPeerManager.CompareIPEndpoints(routid.endPoint, endPoint);
                return false;
            });
            return candidate;
        }
    }
    partial class RainMeadow
    {
        static void Main()
        {
            LobbyServer.netIo = new RouterServerSideNetIO();
            LobbyServer.netIo.Mainloop();
        }
    }
}
#endif

// player ID is ulong, 0 is null, MAX is server
// zero-out the first 48 bits for "anonymous"? (could be useful for local tests, multiple clients from the same install)

// OPEN QUESTIONS:
// player sync and H/S disagreements
// password and co, if player list is given beforehand
// lobby ownership transfer
// server packet versioning?

// H->S RouterPublishLobbyPacket
//      (lobby desc, owner UID)(implicit: IP (check nonlocal!))
//      (TODO: IP-based and UID-based bans, UID-based friendlist)
// S->H RouterPublicationAcceptedPacket
//      (lobby ID for invites)
//      (TODO: expected hearbeat freq, missable heartbeats before autoclose)
//
// P->S RouterRequestLobbyPacket
//      (player ID, implicit IP (check nonlocal?))
// S->P RouterInformLobbyPacket
//      (TODO: inform player of apparent IP?)
// P->S RouterRequestJoinToServerPacket
//      (lobby ID, player UID, implicit IP)
// S->P RouterModifyPlayerListPacket
//      (give existing players)
//      TODO: remove? seems a little early, but host currently gives the same info before pwd check
// S->H RouterModifyPlayerListPacket (inform of player arrival)
//      (player ID, IP)
// H->P [ack] (pierce NAT)
// P->H RouterRequestJoinPacket
// H->P RouterAcceptJoinPacket
// H->S RouterModifyPlayerListPacket (ack's player joining)
// H->PP RouterModifyPlayerListPacket (inform other players)
// PP->P [ack] (pierce NAT)
// P->PP [ack] (pierce NAT)
// H<->P,PP [gaming]
//
// P->H leaving (or timed out)
// H->S RouterModifyPlayerListPacket  // (to update player list)
// H->PP RouterModifyPlayerListPacket
//
// H->S shutting down (or timed out)
// TODO
//
// --------------------
//
// H->S ownership transfer
// TODO
