﻿using System;
using System.Diagnostics;

using Aragas.Core.Interfaces;
using Aragas.Core.Wrappers;

using Newtonsoft.Json;

using PokeD.Core.Packets;

using PokeD.ServerProxy.Clients;

namespace PokeD.ServerProxy
{
    public class ServerProxy : IUpdatable, IDisposable
    {
        public const string FileName = "ServerProxy.json";

        [JsonProperty("ConnectionPort")]
        public ushort ConnectionPort { get; private set; } = 15100;

        [JsonProperty("ServerIP", NullValueHandling = NullValueHandling.Ignore)]
        public string ServerIP { get; private set; } = "127.0.0.1";

        [JsonProperty("ServerPort")]
        public ushort ServerPort { get; private set; } = 15125;

        [JsonIgnore]
        public bool IsDisposing { get; private set; }


        ITCPListener P3DListener { get; set; }
        IThread ListenToConnectionsThread { get; set; }


        P3DPlayer OriginPlayer { get; set; }
        ProtobufPlayer ProxyPlayer { get; set; }


        public ServerProxy() { }

        public ServerProxy(string serverIp, ushort serverPort = 15125) : this()
        {
            ServerIP = serverIp;
            ServerPort = serverPort;
        }


        public bool Start()
        {
            var status = FileSystemWrapper.LoadSettings(FileName, this);
            if (!status)
                Logger.Log(LogType.Warning, "Failed to load ServerProxy settings!");

            ListenToConnectionsThread = ThreadWrapper.CreateThread(ListenToConnectionsCycle);
            ListenToConnectionsThread.IsBackground = true;
            ListenToConnectionsThread.Name = "ListenToConnectionsThread";
            ListenToConnectionsThread.Start();

            return status;
        }
        public bool Stop()
        {
            var status = FileSystemWrapper.SaveSettings(FileName, this);
            if (!status)
                Logger.Log(LogType.Warning, "Failed to save ServerProxy settings!");

            if (ListenToConnectionsThread.IsRunning)
                ListenToConnectionsThread.Abort();

            Dispose();

            return status;
        }


        public static long ClientConnectionsThreadTime { get; private set; }
        private void ListenToConnectionsCycle()
        {
            P3DListener = TCPListenerWrapper.CreateTCPListener(ConnectionPort);
            P3DListener.Start();

            var watch = Stopwatch.StartNew();
            while (!IsDisposing)
            {
                if (P3DListener.AvailableClients)
                {
                    var client = TCPClientWrapper.CreateTCPClient().Connect(ServerIP, ServerPort);
                    ProxyPlayer?.Dispose();
                    ProxyPlayer = new ProtobufPlayer(client, this);

                    OriginPlayer?.Dispose();
                    OriginPlayer = new P3DPlayer(P3DListener.AcceptTCPClient(), this);
                }



                if (watch.ElapsedMilliseconds < 250)
                {
                    ClientConnectionsThreadTime = watch.ElapsedMilliseconds;

                    var time = (int)(250 - watch.ElapsedMilliseconds);
                    if (time < 0) time = 0;
                    ThreadWrapper.Sleep(time);
                }

                watch.Reset();
                watch.Start();
            }
        }


        public void Update()
        {
            if (OriginPlayer != null && ProxyPlayer != null)
            {
                OriginPlayer.Update();
                ProxyPlayer.Update();
            }
        }


        public void SendPacketToOrigin(ProtobufOriginPacket packet)
        {
            OriginPlayer.PacketFromProxy(packet);
        }
        public void SendPacketToProxy(P3DPacket packet)
        {
            ProxyPlayer.PacketFromOrigin(packet);
        }


        public void Disconnect()
        {
            OriginPlayer.Disconnect();
            ProxyPlayer.Disconnect();
        }


        public void Dispose()
        {
            IsDisposing = true;

            OriginPlayer.Dispose();
            ProxyPlayer.Dispose();
        }
    }
}
