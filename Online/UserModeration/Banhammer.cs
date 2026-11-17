using Menu;
using System;


namespace RainMeadow
{
    public class BanHammer
    {
        public static void BanUser(OnlinePlayer steamUser)
        {
            steamUser.InvokeRPC(RPCs.KickToLobby);
            if (OnlineManager.lobby.bannedUsers == null)
            {
                OnlineManager.lobby.bannedUsers = new();
            }
            if (!OnlineManager.lobby.bannedUsers.list.Contains(steamUser.id))
            {
                OnlineManager.lobby.bannedUsers.list.Add(steamUser.id);
            }
        }
    }
}
