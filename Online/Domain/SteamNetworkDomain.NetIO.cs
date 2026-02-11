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

        public override void ForgetPlayer(OnlinePlayer player)
        {

        }

        public override void ForgetEverything()
        {

        }

    }
}
