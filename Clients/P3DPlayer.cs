using System;
using System.Collections.Generic;

using PokeD.Core.Interfaces;
using PokeD.Core.Packets;
using PokeD.Core.Wrappers;

using PokeD.ServerProxy.IO;

namespace PokeD.ServerProxy.Clients
{
    public class P3DPlayer
    {
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
            {
                _proxy.Disconnect();
                return;
            }

            if (Stream.Connected && Stream.DataAvailable > 0)
            {
                var data = Stream.ReadLine();

                HandleData(data);
            }
        }


        private void HandleData(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                Logger.Log(LogType.GlobalError, $"P3D Reading Error: Packet Data is null or empty.");
                return;
            }


            int id;
            if (P3DPacket.TryParseID(data, out id))
            {
                if (id >= PlayerResponse.Packets.Length)
                {
                    Logger.Log(LogType.GlobalError, $"P3D Reading Error: Packet ID {id} is not correct, Packet Data: {data}.");
                    return;
                }

                var packet = PlayerResponse.Packets[id]();
                if (packet == null)
                {
                    Logger.Log(LogType.GlobalError, $"P3D Reading Error: Packet is null. Packet ID {id}, Packet Data: {data}.");
                    return;
                }

                //packet = PlayerResponse.Packets[id]();
                if (packet.TryParseData(data))
                {
                    HandlePacket(packet);
#if DEBUG
                    FromGame.Add(packet);
#endif
                }
                else
                    Logger.Log(LogType.GlobalError, $"P3D Reading Error: Packet TryParseData error. Packet ID {id}, Packet Data: {data}.");
            }
            else
                Logger.Log(LogType.GlobalError, $"P3D Reading Error: Packet TryParseID error. Packet Data: {data}.");
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