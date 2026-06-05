using System;
using System.IO;
using RainMeadow.Shared;

namespace RainMeadow
{
    public abstract partial class SecuredPeerNetworkDomain : NetworkDomain
    {

        public override void SendSessionData(OnlinePlayer toPlayer)
        {
            if (PlatformPeerManager is null) return;
            try
            {
                OnlineManager.serializer.WriteData(toPlayer);
                SecuredPeerId? peerID = GetPeerIDFromPlayer(toPlayer);
                if (peerID is null) throw new InvalidProgrammerException("no peerid");
                var packet = new SessionPacket(new ArraySegment<byte>(OnlineManager.serializer.buffer, 0, (int)OnlineManager.serializer.Position));

                SendPacket(peerID, packet, PacketReliability.Unreliable);
            }
            catch (Exception e)
            {
                RainMeadow.Error(e);
                throw;
            }
            finally
            {
                OnlineManager.serializer.EndWrite();
            }
        }

        public override void SendCustomData(OnlinePlayer toPlayer, string key, byte[] data, PacketReliability sendType, bool boxed = false)
        {
            try
            {
                SecuredPeerId? peerID = GetPeerIDFromPlayer(toPlayer);
                if (peerID is null) throw new InvalidProgrammerException("no peerid");
                SendPacket(peerID, new CustomPacket(key, new ArraySegment<byte>(data, 0, data.Length)) {boxed = boxed}, sendType);
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
            if (packet.boxed)
            {
                RainMeadow.Error("Cannot broadcast boxed packet.");
                return;
            }
            RainMeadow.DebugMe();
            foreach(SecuredPeerId broadId in PlatformPeerManager.GetBroadcastPeerIDs())
            {
                SendPacket(broadId, packet, PacketReliability.Unreliable, true);
            }
        }

        public void SendPacket(SecuredPeerId peer, Packet packet, PacketReliability sendType, bool broadcast = false)
        {
            if (PlatformPeerManager is null) return;
            using (MemoryStream memory = new MemoryStream(128))  // REVIEW: what is this size?
            using (BinaryWriter writer = new BinaryWriter(memory))
            {
                Packet.Encode(packet, writer, peer, PlatformPeerManager.Me);
                PlatformPeerManager.Send(memory.GetBuffer(), peer,
                    sendType switch
                    {
                        PacketReliability.Reliable => SecuredPeerManager.PacketFlags.Reliable,
                        _ => broadcast? SecuredPeerManager.PacketFlags.Broadcast : SecuredPeerManager.PacketFlags.Unreliable,
                    },
                    packet.boxed);
            }
        }

        public override void RecieveData()
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.Update();

            int packetlimit = RainMeadow.rainMeadowOptions.UdpMaxPacketsPerUpdate.Value;
            for (int i = 0; (i < packetlimit) && PlatformPeerManager.IsPacketAvailable(); i++)
            {
                try
                {
                    byte[]? data = PlatformPeerManager.Receive(out SecuredPeerId? remoteEndpoint, out bool boxed);
                    if (data == null) continue;
                    if (remoteEndpoint is null) continue;

                    using (MemoryStream netStream = new MemoryStream(data))
                    using (BinaryReader netReader = new BinaryReader(netStream))
                    {
                        if (netReader.BaseStream.Position == ((MemoryStream)netReader.BaseStream).Length) continue; // nothing to read somehow?
                        Packet.Decode(netReader, remoteEndpoint, PlatformPeerManager.Me, boxed);
                    }
                }
                catch (Exception e)
                {
                    RainMeadow.Error(e);
                    OnlineManager.serializer.EndRead();
                }
            }
        }

        public void SendAcknoledgement(SecuredPeerId id)
        {
            if (PlatformPeerManager is null) return;
            PlatformPeerManager.Send(Array.Empty<byte>(), id, SecuredPeerManager.PacketFlags.Reliable);
        }

    }
}
