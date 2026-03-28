using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using HarmonyLib;
using Menu;
using Steamworks;

using RainMeadow.Shared;

namespace RainMeadow
{

    public partial class SteamNetworkDomain
    {
        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                var steamNetId = (toPlayer.id as SteamNetworkDomain.SteamPlayerId).oid;
                unsafe
                {
                    fixed (byte* dataPointer = OnlineManager.serializer.buffer)
                    {
                        SteamNetworkingMessages.SendMessageToUser(ref steamNetId, (IntPtr)dataPointer, (uint)OnlineManager.serializer.Position, Constants.k_nSteamNetworkingSend_UnreliableNoDelay, 0);
                    }
                }
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                OnlineManager.serializer.EndWrite();
                throw;
            }
        }

        public override void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, PacketReliability sendType, bool boxed)
        {
            if (NetworkDomain.currentDomain == NetworkDomain.NetworkDomainType.Steam)
            {
                try
                {
                    byte[] buffer = null;
                    using (MemoryStream ms = new MemoryStream())
                    using (BinaryWriter writer = new BinaryWriter(ms))
                    {
                        var customPacket = new CustomPacket(key, new ArraySegment<byte>(data, 0, data.Length));
                        customPacket.SteamEncode(ms, writer);
                        writer.Flush();
                        buffer = ms.ToArray();
                    }
                    if (buffer == null)
                    {
                        RainMeadow.Error("There was an error writing the Custom Data buffer.");
                        return;
                    }
                    var steamNetId = (toPlayer.id as SteamPlayerId).oid;
                    unsafe
                    {
                        fixed (byte* dataPointer = buffer)
                        {
                            SteamNetworkingMessages.SendMessageToUser(ref steamNetId, (IntPtr)dataPointer, (uint)buffer.Length,
                                sendType switch
                                {
                                    PacketReliability.Reliable => Constants.k_nSteamNetworkingSend_Reliable,
                                    _ => Constants.k_nSteamNetworkingSend_UnreliableNoDelay
                                }, 1);
                        }
                    }
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                    throw;
                }
            }
        }

        public override void RecieveData()
        {
            SteamAPI.RunCallbacks();
            lock (OnlineManager.serializer)
            {
                int n; // Session
                int c; // Custom
                IntPtr[] messages = new IntPtr[32];
                do // process in batches
                {
                    n = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, messages, messages.Length);
                    for (int i = 0; i < n; i++)
                    {
                        var message = SteamNetworkingMessage_t.FromIntPtr(messages[i]);
                        try
                        {
                            if (OnlineManager.lobby != null)
                            {

                                var fromPlayer = NetworkDomain.Steam?.GetPlayerSteam(message.m_identityPeer.GetSteamID().m_SteamID);
                                if (fromPlayer is null)
                                {
                                    RainMeadow.Error("player not found: " + message.m_identityPeer + " " + message.m_identityPeer.GetSteamID());
                                    continue;
                                }


                                fromPlayer.UpdateSessionBuffer(message.m_pData, message.m_cbSize);
                            }
                        }
                        catch (Exception e)
                        {
                            RainMeadow.Error("Error reading packet from player : " + message.m_identityPeer.GetSteamID());
                            RainMeadow.Error(e);
                            OnlineManager.serializer.EndRead();
                            //throw;
                        }
                        finally
                        {
                            SteamNetworkingMessage_t.Release(messages[i]);
                        }
                    }
                }
                while (n > 0);
                // Custom Data
                do
                {
                    c = SteamNetworkingMessages.ReceiveMessagesOnChannel(1, messages, messages.Length);
                    for (int i = 0; i < c; i++)
                    {
                        var message = SteamNetworkingMessage_t.FromIntPtr(messages[i]);
                        try
                        {
                            if (OnlineManager.lobby != null)
                            {

                                var fromPlayer = GetPlayerSteam(message.m_identityPeer.GetSteamID().m_SteamID);
                                if (fromPlayer == null)
                                {
                                    RainMeadow.Error("player not found: " + message.m_identityPeer + " " + message.m_identityPeer.GetSteamID());
                                    continue;
                                }
                                //RainMeadow.Debug($"Receiving message from {fromPlayer}");
                                byte[] data = new byte[message.m_cbSize];
                                Marshal.Copy(message.m_pData, data, 0, message.m_cbSize);
                                CustomManager.ReadCustom(fromPlayer, data);
                            }
                        }
                        catch (Exception e)
                        {
                            RainMeadow.Error("Error reading custom packet from player : " + message.m_identityPeer.GetSteamID());
                            RainMeadow.Error(e);
                            //throw;
                        }
                        finally
                        {
                            SteamNetworkingMessage_t.Release(messages[i]);
                        }
                    }
                } while (c > 0);
            }
        }

        public override bool canSendChatMessages => true;
        public override void SendChatMessage(string message)
        {
            byte[] msgBytes = System.Text.Encoding.UTF8.GetBytes(message);
            bool outputted = SteamMatchmaking.SendLobbyChatMsg(lobbyID, msgBytes, msgBytes.Length);

            if (!outputted) RainMeadow.Debug($"Failed to send message: {msgBytes} {msgBytes.Length}");
        }

        private void LobbyChatMessageReceived(LobbyChatMsg_t callback)
        {
            CSteamID senderID;
            byte[] msgData = new byte[1024];
            int msgDataLength = SteamMatchmaking.GetLobbyChatEntry((CSteamID)callback.m_ulSteamIDLobby, (int)callback.m_iChatID, out senderID, msgData, msgData.Length, out EChatEntryType _);

            string message = System.Text.Encoding.UTF8.GetString(msgData, 0, msgDataLength);
            RainMeadow.Debug($"Message from {SteamFriends.GetFriendPersonaName(senderID)}: {message}");
            RecieveChatMessage(GetPlayerSteam(senderID.m_SteamID), message);
        }


        private void LobbyChatUpdated(LobbyChatUpdate_t param)
        {
            try
            {
                RainMeadow.Debug($"{param.m_ulSteamIDLobby} : {param.m_ulSteamIDUserChanged} : {param.m_ulSteamIDMakingChange} : {param.m_rgfChatMemberStateChange}");
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

                UpdatePlayersList();
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }

        public bool filteringAvailable;
        /// <summary>
        /// Filters a message using Steam if ProfanityFilter is enabled in Remix options.
        /// </summary>
        /// <param name="message"></param>
        public override void FilterMessage(ref string message)
        {
            if (!filteringAvailable || !RainMeadow.rainMeadowOptions.ProfanityFilter.Value || OnlineManager.lobby == null) return;
            if (SteamUtils.FilterText(ETextFilteringContext.k_ETextFilteringContextChat, CSteamID.Nil, message, out string pchOutFilteredText, (uint)(message.Length * 2 + 1)) > 0)
            {
                message = pchOutFilteredText;
            }
        }

        /// <summary>
        /// Filters a team name using Steam if ProfanityFilter is enabled in Remix options.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public override string FilterTeamName(string name)
        {
            if (!filteringAvailable || !RainMeadow.rainMeadowOptions.ProfanityFilter.Value || OnlineManager.lobby == null) return name;
            if (SteamUtils.FilterText(ETextFilteringContext.k_ETextFilteringContextName, CSteamID.Nil, name, out string pchOutFilteredText, (uint)(name.Length * 2 + 1)) > 0)
            {
                return pchOutFilteredText;
            }
            return name;
        }

    }
}
