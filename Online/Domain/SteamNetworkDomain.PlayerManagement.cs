using RWCustom;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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

        private static HashSet<string> devSteamIdHashes = new HashSet<string>()
        {
            "AOOTy8PrB9DWbAxExg9BbhiLBbRqAgmsRLoAHnIGXOU=",
            "dlWUAGjYBtAdypcmLwbDnZ73akq624OiSNIQ//ecsms=",
            "ApNKog4MYwp7nfkyC6lIPtD+/sBJfBArnSPiy6yo7VU=",
            "YIczH+KncjxdHf3MrnhumDUJ1QVAyBsy9ME6k0bZyPc=",
            "P5S1c63jYWl3Ce73H0k99BeIMSAmxa/BbvkEiyTs9mM=",
            "iJFBCXhwwaHxbJ5uXfmZsK7Ad9a7vZgT1ZwiofO0aMg=",
            "ATe23LFNxITCICTkw+2Bs67cNZ5N/nRBMfziGhIn11s=",
            "oz6hibRdEiJow7IWhn+T7Ij+agHeNqmxyHO34YMOla4=",
            "E5mtN6Hh2vyAuOgBZ5iiTH36j2pAJ8urOgEZKZsciSo=",
            "TA9uZQ7Z7MkVUm7D32EB0gpuQBrhE9cAZWB2UXBuqtg=",
            "tXLLHFXRXKzi285CSDIko+gmRrLChLb3k3K1pV0GUq4=",
            "AkQKwH5S6zj//MRsnrjaTp2HGe7Ln9ZB057MP5xLk2M=",
            "tMoAaCdZejjuWCF0MsXcOUr+D4eok0b2c46B8PTM0kg=",
            "095dLJgw4Nc1zbdUIdxL7d7nmyKxcj7hekNx8EQlXGY=",
            "GpdPaLhUEEkwjCbkSLjXN7lZy0iXa5YlFErMi9V+hXI=",
            "PwcZS6t8kETyBdrPiR2ple35lpLMfEw6TP/VyHVD4z4=",
            "wZ2+Phw6EOBLv9bZKdSGV+3lWhNxiT2KHwCluqhLdzo=",
            "Hr8BfOHHTBRGgSmQoj4qQdlHqaY6d4DHFbF7wCNFI1U=",
            "cOL0sHXOvRyn7y5S+3VXWmuyZE1KvQXdfBgcHrph2kE=",
            "3aA5+Ga/lMY848/EcCZLBnO93TS1RhPfSMgAGtf7MQY=",
            "5eD7MQy+i6B6862JCgkjFXRevE7UFU+kvvBGPXJ4hGQ=",
            "iJFBCXhwwaHxbJ5uXfmZsK7Ad9a7vZgT1ZwiofO0aMg="
        };

        public override bool IsDev(MeadowPlayerId player)
        {
            if (player is SteamPlayerId steamid)
            {
                ulong steamID = steamid.oid.GetSteamID64();
                SHA256 Sha = SHA256.Create();
                var steamIDHash = Convert.ToBase64String(Sha.ComputeHash(Encoding.ASCII.GetBytes(steamID.ToString())));

                if (devSteamIdHashes.Contains(steamIDHash))
                {
                    return true;
                }
            }
            return false;
        }

        public override bool IsTrustedCommunity(MeadowPlayerId player)
        {
            if (player is SteamPlayerId steamid)
            {
                ulong steamID = steamid.oid.GetSteamID64();
                SHA256 Sha = SHA256.Create();
                var steamIDHash = Convert.ToBase64String(Sha.ComputeHash(Encoding.ASCII.GetBytes(steamID.ToString())));

                if (devSteamIdHashes.Contains(steamIDHash))
                {
                    return true;
                }
            }
            return false;
        }

    }
}
