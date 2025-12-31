using System;
using System.IO;
using RainMeadow.Shared;

namespace RainMeadow
{
    public abstract class SecuredPeerNetworkDomain : NetworkDomain
    {

        public abstract SecuredPeerId? GetPeerIDFromPlayer(OnlinePlayer player);
        public abstract OnlinePlayer? GetPlayerFromPeerID(SecuredPeerId id);
        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            if (PlatformPeerManager is null) return;
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                SecuredPeerId? peerID = GetPeerIDFromPlayer(toPlayer);
                if (peerID is null) throw new InvalidProgrammerException("no peerid");
                SendPacket(peerID, new SessionPacket(OnlineManager.serializer.buffer, (ushort)OnlineManager.serializer.Position), PacketReliability.Reliable, true);
                OnlineManager.serializer.EndWrite();
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                OnlineManager.serializer.EndWrite();
                throw;
            }
        }

        public override void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, PacketReliability sendType, bool boxed = false)
        {
            try
            {
                SecuredPeerId? peerID = GetPeerIDFromPlayer(toPlayer);
                if (peerID is null) throw new InvalidProgrammerException("no peerid");
                SendPacket(peerID, new CustomPacket(key, data, (ushort)data.Length), sendType, boxed);
            }

            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
        }


        public void SendBroadcast(Packet packet)
        {
            if (PlatformPeerManager is null) return;
            RainMeadow.DebugMe();
            SecuredPeerId[] broadcastables = PlatformPeerManager.GetBroadcastPeerIDs();
            foreach(SecuredPeerId broadId in broadcastables)
            {
                using (MemoryStream memory = new MemoryStream(128))
                using (BinaryWriter writer = new BinaryWriter(memory))
                {
                    Packet.Encode(packet, writer, broadId);

                    for (int i = 0; i < 4; i++)
                        PlatformPeerManager.Send(memory.GetBuffer(), broadId,
                            SecuredPeerManager.PacketFlags.Broadcast, false);
                }
            }
        }

        public void SendPacket(SecuredPeerId peer, Packet packet, PacketReliability sendType, bool boxed)
        {
            if (PlatformPeerManager is null) return;
            using (MemoryStream memory = new MemoryStream(128))
            using (BinaryWriter writer = new BinaryWriter(memory))
            {
                Packet.Encode(packet, writer, peer);
                PlatformPeerManager.Send(memory.GetBuffer(), peer, 
                    sendType switch  
                    {
                        PacketReliability.Reliable => SecuredPeerManager.PacketFlags.Reliable,
                        _ => SecuredPeerManager.PacketFlags.Unreliable,
                    },
                    boxed);
            }
        }

        public override void RecieveData()
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.Update();

            int packetlimit = 4; // TODO: Add to remix menu
            for (int i = 0; (i < packetlimit) && PlatformPeerManager.IsPacketAvailable(); i++)
            {
                try
                {
                    byte[]? data = PlatformPeerManager.Receive(out SecuredPeerId? remoteEndpoint);
                    if (data == null) continue;
                    if (remoteEndpoint is null) continue;

                    using (MemoryStream netStream = new MemoryStream(data))
                    using (BinaryReader netReader = new BinaryReader(netStream))
                    {
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) continue; // nothing to read somehow?
                        Packet.Decode(netReader, remoteEndpoint);
                    }
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                }
            }
        }

        public void SendAcknoledgement(SecuredPeerId id)
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.Send(Array.Empty<byte>(), id, SecuredPeerManager.PacketFlags.Reliable);
        }

        public override void ForgetPlayer(OnlinePlayer player)
        {
            if (PlatformPeerManager is null) return;
            SecuredPeerId? peerID = GetPeerIDFromPlayer(player);
            if (peerID is not null)
            {
                PlatformPeerManager.GetRemotePeer(peerID)?.Terminate();
            }
        }

        public override void ForgetEverything()
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.TerminateAllPeers();
            while (PlatformPeerManager.AnyPendingTermination())
            {
                PlatformPeerManager.Update();
            }
        }
    }
}