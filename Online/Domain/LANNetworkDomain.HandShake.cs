using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using HarmonyLib;
using Menu;
using RainMeadow.Shared;

namespace RainMeadow
{

    public partial class LANNetworkDomain
    {
        public override void RequestLobbyList()
        {
            // To create a proper list, we need to send a message to the broadcast endpoint.
            // and wait for responces from possible hosts.
            for (int i = 0; i < 8; i++)
            {
                using (MemoryStream memoryStream = new())
                using (BinaryWriter writer = new(memoryStream))
                {
                    SendBroadcast(new RequestLobbyPacket());
                }
            }
        }

        public void AddLobby(LANLobbyInfo lobby)
        {
            OnLobbyListReceivedEvent(true, [ lobby ]);
        }


        public void SendLobbyInfo(PeerId endPoint)
        {
            if (OnlineManager.lobby != null && OnlineManager.lobby.isOwner)
            {
                var packet = new InformLobbyPacket(
                    maxplayercount, Utils.Translate("LAN Lobby"), OnlineManager.lobby.hasPassword,
                    OnlineManager.lobby.gameModeType.value, OnlineManager.players.Count,
                    RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetRequiredMods()), RainMeadowModManager.ModArrayToString(RainMeadowModManager.GetBannedMods()));
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    Packet.Encode(packet, writer, endPoint);
                    PlatformPeerManager.Send(memory.GetBuffer(), endPoint, BasePeerManager.PacketType.UnreliableBroadcast, false);
                }
            }
        }

        public override bool canDirectConnect => true;
        public override LobbyInfo GenerateDCLobbyInfo(string connectstr)
        {
            var endpoint = PlatformPeerManager.GetPeerIdByName(connectstr);
            if (endpoint != null)
            {
                return new LANNetworkDomain.LANLobbyInfo(endpoint, "Direct Connection", "Meadow", 0, true, 2);
            }
            else
            {
                throw new FormatException("IP Address format should be xxx.xxx.xxx.xxx:port");
            }
        }

        public int maxplayercount = 0;
        public override void CreateLobby(LobbyVisibility visibility, string gameMode, string? password, int? maxPlayerCount)
        {
            NetworkDomain.currentDomain = NetworkDomainType.LAN;
            maxplayercount = maxPlayerCount ?? 0;
            OnlineManager.lobby = new Lobby(new OnlineGameMode.OnlineGameModeType(gameMode), OnlineManager.mePlayer, password);
            NetworkDomain.OnLobbyJoinedEvent(true, "");
        }

        public void LobbyAcknoledgedUs(OnlinePlayer owner)
        {
            RainMeadow.DebugMe();
            if (OnlineManager.lobby is null)
            {
                OnlineManager.lobby = new Lobby(
                    new OnlineGameMode.OnlineGameModeType(OnlineManager.currentlyJoiningLobby.mode, false),
                    owner, lobbyPassword);
            }
        }

        string lobbyPassword = "";
        public override void RequestJoinLobby(LobbyInfo lobby, string? password)
        {
            NetworkDomain.currentDomain = NetworkDomainType.LAN;
            RainMeadow.DebugMe();
            if (lobby is LANLobbyInfo lobbyinfo)
            {
                lobbyPassword = password ?? "";
                OnlineManager.currentlyJoiningLobby = lobby;
                var lobbyInfo = (LANLobbyInfo)lobby;
                if (lobbyInfo.endPoint == null)
                {
                    RainMeadow.Debug("Failed to join local game...");
                    return;
                }

                RainMeadow.Debug("Sending Request to join lobby...");
                SendP2P(new OnlinePlayer(new LANPlayerId(lobbyInfo.endPoint)),
                    new RequestJoinPacket(OnlineManager.mePlayer.id.name), BasePeerManager.PacketType.Reliable, true);
            }
            else
            {
                RainMeadow.Error("Invalid lobby type");
            }
        }

        public override void HandleLeavingLobby()
        {
            if (OnlineManager.players is not null)
            {
                if (OnlineManager.players.Count > 1)
                {
                    foreach (OnlinePlayer p in OnlineManager.players)
                    {
                        SendP2P(p,
                            new SessionEndPacket(),
                                BasePeerManager.PacketType.Unreliable);
                    }
                }
            }
            ForgetEverything();
        }

        public override void OpenInvitationOverlay()
        {
            OnlineManager.instance.manager.ShowDialog(new DialogNotify(Utils.Translate("You cannot use this feature here."), OnlineManager.instance.manager, null));
        }

        public override bool canOpenInvitations => false;

    }
}
