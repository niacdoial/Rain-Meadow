using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Menu;
using RainMeadow.Shared;
using RainMeadow.Shared.Models;

namespace RainMeadow
{
    public partial class NetworkDomain
    {
        static partial void PlatformLanAvailable(ref bool val) { val = NetworkDomain.PlatformPeerManager is not null; }
    }

    public partial class LANNetworkDomain : SecuredPeerNetworkDomain
    {
        void PacketFactory(Packet.Type type, ref Packet? packet)
        {
            if (packet is null)
            {
                packet = type switch
                {
                    Packet.Type.RequestJoin => new RequestJoinPacket(),
                    Packet.Type.ModifyPlayerList => new ModifyPlayerListPacket(),
                    Packet.Type.JoinLobby => new JoinLobbyPacket(),
                    Packet.Type.Session => new SessionPacket(),
                    // Packet.Type.SessionEnd => new SessionEndPacket(),
                    Packet.Type.RequestLobby => new RequestLobbyPacket(),
                    Packet.Type.InformLobby => new InformLobbyPacket(),
                    Packet.Type.ChatMessage => new ChatMessagePacket(),
                    Packet.Type.CustomPacket => new CustomPacket(),

                    _ => null
                };
            }
        }

        public void InitializePackets() {
            Packet.packetFactory += PacketFactory;
        }

        public LANNetworkDomain()
        {
            InitializePackets();
            NetworkDomain.PlatformPeerManager.OnPeerForgotten += (SecuredPeerManager.RemotePeer endPoint, string reason) => {
                // first, check if this endpoint is managed by the current NetworkDomain
                // then, check if the peer timed out or if we booted them already (done in the callee)

                if (NetworkDomain.currentDomain == NetworkDomainType.LAN)
                {
                    if (GetPlayerLAN(endPoint.id) is OnlinePlayer player)
                    {
                        RemoveLANPlayer(player);
                    }

                    if (OnlineManager.currentlyJoiningLobby is LANLobbyInfo lobbyInfo)
                    {
                        if (endPoint.id.Equals(lobbyInfo.endPoint))
                        {
                            // REVIEW: are we giving up on rotating who hosts?
                            OnlineManager.QuitWithError("Connection Lost...");
                        }
                    }

                }
            };
        }

        public class LANLobbyInfo : LobbyInfo
        {
            public override NetworkDomainType domain => NetworkDomainType.LAN;

            public override string directJoinCode => endPoint.ToString();

            public SecuredPeerId endPoint;
            public LANLobbyInfo(SecuredPeerId endPoint, int playerCount, LobbyParameters parameters) :
                base("LAN Lobby", playerCount, parameters)
            {
                string dnsName = endPoint.endPoint.Address.ToString();
                try
                {
                    dnsName = Dns.GetHostEntry(endPoint.endPoint.Address).HostName;
                }
                catch (Exception except)
                {
                    RainMeadow.Error(except);
                }
                name = dnsName + ":" + endPoint.endPoint.Port;
                this.endPoint = endPoint;
            }
            public override bool Equals(LobbyInfo other)
            {
                if (other is LANLobbyInfo otherlan) return endPoint.Equals(otherlan.endPoint);
                return false;
            }
        }

        public LobbyParameters GetLobbyParameters()
        {
            return new LobbyParameters()
            {
                MaxPlayers = maxplayercount,
                PasswordProtected = OnlineManager.lobby.hasPassword,
                Mode = OnlineManager.lobby.gameModeType.value,
                Mods = RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()),
                BannedMods = RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods())
            };
        }
    }
}
