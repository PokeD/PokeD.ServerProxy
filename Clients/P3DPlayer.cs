using System;
using PokeD.Core.Interfaces;
using PokeD.Core.Packets;
using PokeD.Core.Wrappers;
using PokeD.ServerProxy.IO;

namespace PokeD.ServerProxy.Clients
{
    public class P3DPlayer
    {
        public float P3DProtocolVersion => 0.5f;

        public string IP => Client.IP;

        public DateTime ConnectionTime { get; } = DateTime.Now;


        INetworkTCPClient Client { get; }
        IPacketStream Stream { get; }


        readonly ServerProxy _proxy;

#if DEBUG
        // -- Debug -- //
        List<P3DPacket> ToProxy { get; } = new List<P3DPacket>();

        List<P3DPacket> FromGame { get; } = new List<P3DPacket>();
        List<P3DPacket> ToGame { get; } = new List<P3DPacket>();
        // -- Debug -- //
#endif


        public P3DPlayer(INetworkTCPClient client, ServerProxy proxy)
        {
            Client = client;
            Stream = new P3DStream(Client);
            _proxy = proxy;
        }


        public void Update()
        {
            if (!Stream.Connected)
                _proxy.Disconnect();

            if (Stream.Connected && Stream.DataAvailable > 0)
            {
                var data = Stream.ReadLine();

                HandleData(data);
            }
        }


        private void HandleData(string data)
        {
            if (string.IsNullOrEmpty(data))
                return;
            

            int id;
            if (P3DPacket.TryParseID(data, out id))
            {
                P3DPacket packet = null;
                try { packet = PlayerResponse.Packets[id](); }
                catch (Exception) { }

                if (packet == null)
                    return;

                //packet = PlayerResponse.Packets[id]();
                if (packet.TryParseData(data))
                {
                    HandlePacket(packet);
#if DEBUG
                    FromGame.Add(packet);
#endif
                }
            }
        }

        private void HandlePacket(P3DPacket packet)
        {
            _proxy.SendPacketToProxy(packet);

#if DEBUG
            ToProxy.Add(packet);
#endif
        }


        public void SendPacket(P3DPacket packet)
        {
            if (Stream.Connected)
            {
                packet.ProtocolVersion = P3DProtocolVersion;

                Stream.SendPacket(ref packet);

#if DEBUG
                ToGame.Add(packet);
#endif
            }
        }


        public void Dispose()
        {
            Stream?.Dispose();
        }
    }
}