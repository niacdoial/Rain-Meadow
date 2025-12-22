using RWCustom;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RainMeadow
{
    public partial class SteamNetworkDomain : NetworkDomain
    {
        public class SteamPlayerId : MeadowPlayerId
        {
            public CSteamID steamID;
            public SteamNetworkingIdentity oid;

            public SteamPlayerId() { }
            public SteamPlayerId(CSteamID steamID) : base(SteamFriends.GetFriendPersonaName(steamID) ?? string.Empty)
            {
                this.steamID = steamID;
                oid = new SteamNetworkingIdentity();
                oid.SetSteamID(steamID);
            }

            public override void CustomSerialize(Serializer serializer)
            {
                serializer.Serialize(ref steamID.m_SteamID);
                oid = new SteamNetworkingIdentity();
                oid.SetSteamID(steamID);
            }

            public override bool Equals(MeadowPlayerId other)
            {
                return other is SteamPlayerId otherS && steamID == otherS.steamID;
            }

            public override int GetHashCode()
            {
                return steamID.GetHashCode();
            }

            public override string GetPersonaName() {
                return UsernameGenerator.StreamerModeName(SteamFriends.GetFriendPersonaName(steamID));
            }

            public override bool canOpenProfileLink { get => true; }
            public override string DisplayName { get => UsernameGenerator.StreamerModeName(name); }
            public override void OpenProfileLink() {
                string url = $"https://steamcommunity.com/profiles/{steamID}";
                SteamFriends.ActivateGameOverlayToWebPage(url);
            }
        }

        public override MeadowPlayerId GetEmptyId()
        {
            return new SteamPlayerId();
        }
        public override OnlinePlayer CreateMePlayer()
        {
            return new OnlinePlayer(new SteamPlayerId(me)) { isMe = true };
        }

        public override OnlinePlayer GetPlayer(MeadowPlayerId id)
        {
            return OnlineManager.players.FirstOrDefault(p => (p.id as SteamPlayerId).steamID == (id as SteamPlayerId).steamID);
        }

        public override OnlinePlayer GetLobbyOwner()
        {
            return GetPlayer(new SteamPlayerId(SteamMatchmaking.GetLobbyOwner(lobbyID)));
        }

        public OnlinePlayer GetPlayerSteam(ulong steamID)
        {
            return OnlineManager.players.FirstOrDefault(p => (p.id as SteamPlayerId).steamID.m_SteamID == steamID);
        }

        private void PlayerJoined(CSteamID p)
        {
            RainMeadow.Debug($"PlayerJoined:{p} - {SteamFriends.GetFriendPersonaName(p)}");
            if (p == me) return;
            SteamFriends.RequestUserInformation(p, true);
            var player = new OnlinePlayer(new SteamPlayerId(p));
            OnlineManager.AddPlayer(player);
        }

        private void PlayerLeft(CSteamID p)
        {
            RainMeadow.Debug($"{p} - {SteamFriends.GetFriendPersonaName(p)}");

            if (OnlineManager.players.FirstOrDefault(op => (op.id as SteamPlayerId).steamID == p) is OnlinePlayer player)
            {
                OnlineManager.RemovePlayer(player);
            }
        }

        private void LobbyUpdated(LobbyDataUpdate_t param)
        {
            try
            {
                RainMeadow.Debug($"{param.m_ulSteamIDLobby} : {param.m_ulSteamIDMember} : {param.m_bSuccess}");
                if (OnlineManager.lobby == null)
                {
                    RainMeadow.Error("got lobby event with no lobby!");
                    return;
                }
                if ((CSteamID)param.m_ulSteamIDLobby != lobbyID)
                {
                    RainMeadow.Error("got lobby event for wrong lobby!");
                    return;
                }
                if (param.m_bSuccess > 0)
                {
                    if (OnlineManager.lobby != null && lobbyID == new CSteamID(param.m_ulSteamIDLobby))
                    {
                        // lobby event, check for possible changes
                        UpdatePlayersList();
                    }
                }
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        public void UpdatePlayersList()
        {
            try
            {
                RainMeadow.DebugMe();
                var oldplayers = OnlineManager.players.Select(p => (p.id as SteamPlayerId).steamID).ToArray();
                var n = SteamMatchmaking.GetNumLobbyMembers(lobbyID);
                var newplayers = new CSteamID[n];
                for (int i = 0; i < n; i++)
                {
                    newplayers[i] = SteamMatchmaking.GetLobbyMemberByIndex(lobbyID, i);
                }
                foreach (var p in oldplayers)
                {
                    if (!newplayers.Contains(p)) PlayerLeft(p);
                }
                foreach (var p in newplayers)
                {
                    if (!oldplayers.Contains(p)) PlayerJoined(p);
                }
                OnPlayerListReceivedEvent(OnlineManager.players.Select(x => x.id).ToArray());
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        public override void ForgetPlayer(OnlinePlayer player)
        {

        }

        public override void ForgetEverything()
        {

        }

    }
}
