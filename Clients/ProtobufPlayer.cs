﻿using System;
using System.Collections.Generic;

using Aragas.Core.Data;
using Aragas.Core.IO;
using Aragas.Core.Packets;
using Aragas.Core.Wrappers;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

using PokeD.Core;
using PokeD.Core.IO;
using PokeD.Core.Packets;
using PokeD.Core.Packets.Encryption;
using PokeD.Core.Packets.Server;



namespace PokeD.ServerProxy.Clients
{
    public class ProtobufPlayer
    {
        [Flags]
        enum JoinState
        {
            None                = 0x00,
            JoinGameSent        = 0x01,
            JoinedGame          = 0x02,
            QueryPacketsEmpty   = 0x04
        }


        public bool Connected => Stream.Connected;
        public string IP => Client.IP;


        ITCPClient Client { get; }
        PacketStream Stream { get; }

        JoinState State { get; set; }

        Queue<ProtobufPacket> ToSend = new Queue<ProtobufPacket>();

        readonly ServerProxy _proxy;

#if DEBUG
        // -- Debug -- //
        List<P3DPacket> FromOrigin { get; } = new List<P3DPacket>();
        List<ProtobufPacket> ToOrigin { get; } = new List<ProtobufPacket>();

        List<ProtobufPacket> FromServer { get; } = new List<ProtobufPacket>();
        List<ProtobufPacket> ToServer { get; } = new List<ProtobufPacket>();
        // -- Debug -- //
#endif

        public ProtobufPlayer(ITCPClient client, ServerProxy proxy)
        {
            Client = client;
            Stream = new ProtobufOriginStream(Client);
            _proxy = proxy;
        }


        public void Update()
        {
            if (!Stream.Connected)
            {
                _proxy.Disconnect();
                return;
            }

            if (Stream.DataAvailable > 0)
            {
                var dataLength = Stream.ReadVarInt();
                if (dataLength == 0)
                {
                    Logger.Log(LogType.GlobalError, $"Protobuf Reading Error: Packet Length size is 0. Disconnecting from server.");
                    SendPacket(new KickedPacket {Reason = $"Packet Length size is 0!"});
                    _proxy.Disconnect();
                    return;
                }

                var data = Stream.ReadByteArray(dataLength);

                HandleData(data);
            }
        }


        private void HandleData(byte[] data)
        {
            if (data == null)
            {
                Logger.Log(LogType.GlobalError, $"Protobuf Reading Error: Packet Data is null.");
                return;
            }

            using (var reader = new ProtobufDataReader(data))
            {
                var id = reader.Read<VarInt>();
                var origin = reader.Read<VarInt>();

                if (id >= GamePacketResponses.Packets.Length)
                {
                    Logger.Log(LogType.GlobalError, $"Protobuf Reading Error: Packet ID {id} is not correct, Packet Data: {data}. Disconnecting from server.");
                    SendPacket(new KickedPacket { Reason = $"Packet ID {id} is not correct!" });
                    _proxy.Disconnect();
                    return;
                }

                var packet = GamePacketResponses.Packets[id]().ReadPacket(reader) as ProtobufOriginPacket;
                packet.Origin = origin;


                HandlePacket(packet);

#if DEBUG
                FromServer.Add(packet);
#endif
            }
        }
        private void HandlePacket(ProtobufOriginPacket packet)
        {
            switch ((GamePacketTypes) (int) packet.ID)
            {
                case GamePacketTypes.JoiningGameResponse:
                    HandleJoiningGameResponse((JoiningGameResponsePacket) packet);
                    break;

                case GamePacketTypes.EncryptionRequest:
                    HandleEncryptionRequest((EncryptionRequestPacket) packet);
                    break;

                default:
                    _proxy.SendPacketToOrigin(packet);

#if DEBUG
                    ToOrigin.Add(packet);
#endif
                    break;
            }
        }
        private void HandleEncryptionRequest(EncryptionRequestPacket packet)
        {
            var generator = new CipherKeyGenerator();
            generator.Init(new KeyGenerationParameters(new SecureRandom(), 16 * 8));
            var sharedKey = generator.GenerateKey();

            var pkcs = new PKCS1Signer(packet.PublicKey);
            var signedSecret = pkcs.SignData(sharedKey);
            var signedVerify = pkcs.SignData(packet.VerificationToken);

            SendPacketDirect(new EncryptionResponsePacket { SharedSecret = signedSecret, VerificationToken = signedVerify });

            Stream.InitializeEncryption(sharedKey);

            State |= JoinState.JoinedGame;
        }
        private void HandleJoiningGameResponse(JoiningGameResponsePacket packet)
        {
            if(!packet.EncryptionEnabled)
                State |= JoinState.JoinedGame;
        }

        public void PacketFromOrigin(P3DPacket packet)
        {
#if DEBUG
            FromOrigin.Add(packet);
#endif

            SendPacket((ProtobufPacket) packet);
        }
        private void SendPacket(ProtobufPacket packet)
        {
            if (State.HasFlag(JoinState.QueryPacketsEmpty))
                goto SendPackets;

            if ((GamePacketTypes)(int) packet.ID == GamePacketTypes.ServerDataRequest)
            {
                SendPacketDirect(packet);
                return;
            }

            if (!State.HasFlag(JoinState.JoinGameSent))
            {
                SendPacketDirect(new JoiningGameRequestPacket());

                State |= JoinState.JoinGameSent;
            }

            if (!State.HasFlag(JoinState.JoinedGame))
            {
                ToSend.Enqueue(packet);
                return;
            }

            if (!State.HasFlag(JoinState.QueryPacketsEmpty))
            {
                while (ToSend.Count > 0)
                    SendPacketDirect(ToSend.Dequeue());

                State |= JoinState.QueryPacketsEmpty;
            }

        SendPackets:
            SendPacketDirect(packet);
        }
        private void SendPacketDirect(ProtobufPacket packet)
        {
            if (Stream.Connected)
            {
                Stream.SendPacket(ref packet);

#if DEBUG
                ToServer.Add(packet);
#endif
            }
        }


        public void Disconnect()
        {
            Stream?.Disconnect();
        }

        public void Dispose()
        {
            Stream?.Dispose();
        }
    }
}
