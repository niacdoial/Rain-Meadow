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
        public static List<OnlinePlayer> lobbyPlayers = new List<OnlinePlayer> {};  // players in the one lobby
        public static List<OnlinePlayer> contactPlayers = new List<OnlinePlayer> {};  // players that are in contact with the server
        public static int maxPlayers = 4;

        public static OnlinePlayer GetLobbyPlayer(RouterPlayerId playerId) {
            OnlinePlayer candidate = LobbyServer.lobbyPlayers.FirstOrDefault(p => {
                if (p.id is RouterPlayerId routid)
                    return (routid == playerId);
                return false;
            });
            if (candidate == null) {
                RainMeadow.Error("attempting to remove nonexistant LobbyPlayer " + playerId.name + " %" + playerId.RoutingId.ToString());
                return candidate;
            }
            RouterPlayerId candId = (RouterPlayerId)candidate.id;
            if (!UDPPeerManager.CompareIPEndpoints(candId.endPoint, playerId.endPoint)) {
                RainMeadow.Error("player IDs don't agree on endpoint: " + candId.endPoint.ToString() + " vs " + playerId.endPoint.ToString());
                throw new Exception("WHY");
            }
            return candidate;
        }
        public static OnlinePlayer GetContactPlayer(IPEndPoint endPoint, bool allowCreate = false) {
            OnlinePlayer candidate = LobbyServer.contactPlayers.FirstOrDefault(p => {
                if (p.id is RouterPlayerId routid)
                    return UDPPeerManager.CompareIPEndpoints(routid.endPoint, endPoint);
                return false;
            });
            if (candidate == null && allowCreate) {
                // create player with ID 0, it will be set immediately by packet decoding
                RouterPlayerId newId = new RouterPlayerId(0) {endPoint = endPoint};
                candidate = new OnlinePlayer(newId);
                LobbyServer.contactPlayers.Add(candidate);
            }
            return candidate;
        }
        public static void RemoveContactPlayer(IPEndPoint endPoint) {
            int player_index = LobbyServer.contactPlayers.FindIndex(p => {
                if (p.id is RouterPlayerId routid)
                    return UDPPeerManager.CompareIPEndpoints(routid.endPoint, endPoint);
                return false;
            });
            if (player_index != -1) {
                RouterPlayerId leavingId = (RouterPlayerId)LobbyServer.contactPlayers[player_index].id;
                LobbyServer.contactPlayers.RemoveAt(player_index);
                if (leavingId == (RouterPlayerId)LobbyServer.lobby.host) {
                    LobbyServer.lobby = null;
                    LobbyServer.lobbyPlayers.Clear();
                }
            } else {
                RainMeadow.Error("Peer cannot leave if not there to begin with");
            }
        }
    }
    partial class RainMeadow
    {
        static void Main()
        {
            LobbyServer.netIo = new RouterServerSideNetIO();
            RainMeadow.Debug("Entering mainloop");
            RainMeadow.Debug("┌╨─╨─╨─╨─╨─╨─╨─╨─╨┐");
            RainMeadow.Debug("╡╔═══════════════╗╞");
            RainMeadow.Debug("╡║ ═──╦─────╦──═ ║╞");
            RainMeadow.Debug("╡║    │     │    ║╞");
            RainMeadow.Debug("╡║    │     │    ║╞");
            RainMeadow.Debug("╡║ ═──╣     ╠──═ ║╞");
            RainMeadow.Debug("╡║    │     │    ║╞");
            RainMeadow.Debug("╡║    │     │    ║╞");
            RainMeadow.Debug("╡║ ═──╝     ╚──═ ║╞");
            RainMeadow.Debug("╡╚═══════════════╝╞");
            RainMeadow.Debug("└╥─╥─╥─╥─╥─╥─╥─╥─╥┘");
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
