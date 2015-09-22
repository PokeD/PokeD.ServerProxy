using System.Collections.Generic;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

using PokeD.Core;
using PokeD.Core.Interfaces;
using PokeD.Core.Packets;
using PokeD.Core.Packets.Encryption;
using PokeD.Core.Wrappers;

using PokeD.ServerProxy.Exceptions;
using PokeD.ServerProxy.IO;

namespace PokeD.ServerProxy.Clients
{
    public class ProtobufPlayer
    {
        public bool Connected => Stream.Connected;

        public string IP => Client.IP;


        INetworkTCPClient Client { get; }
        IPacketStream Stream { get; }


        readonly ServerProxy _proxy;

#if DEBUG
        // -- Debug -- //
        List<ProtobufPacket> ToOrigin { get; } = new List<ProtobufPacket>();

        List<ProtobufPacket> FromServer { get; } = new List<ProtobufPacket>();
        List<ProtobufPacket> ToServer { get; } = new List<ProtobufPacket>();
        // -- Debug -- //
#endif

        public ProtobufPlayer(INetworkTCPClient client, ServerProxy proxy)
        {
            Client = client;
            Stream = new ProtobufStream(Client);
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
                try
                {
                    var dataLength = Stream.ReadVarInt();
                    if (dataLength == 0)
                    {
                        Logger.Log(LogType.GlobalError, $"Protobuf Reading Error: Packet Length size is 0. Disconnecting from server.");
                        _proxy.Disconnect();
                        return;
                    }

                    var data = Stream.ReadByteArray(dataLength);

                    HandleData(data);
                }
                catch (ProtobufReadingException ex) { Logger.Log(LogType.GlobalError, $"Protobuf Reading Exeption: {ex.Message}. Disconnecting from server."); _proxy.Disconnect(); }
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
                var id = reader.ReadVarInt();
                var origin = reader.ReadVarInt();

                if (id >= PlayerResponse.Packets.Length)
                {
                    Logger.Log(LogType.GlobalError, $"Protobuf Reading Error: Packet ID {id} is not correct, Packet Data: {data}. Disconnecting from server.");
                    _proxy.Disconnect();
                    return;
                }

                var packet = PlayerResponse.Packets[id]().ReadPacket(reader);
                packet.Origin = origin;

                if (id == (int) PlayerPacketTypes.EncryptionRequest)
                    HandleEncryption((EncryptionRequestPacket) packet);
                else
                    HandlePacket(packet);

#if DEBUG
                FromServer.Add(packet);
#endif
            }
        }
        private void HandlePacket(ProtobufPacket packet)
        {
            _proxy.SendPacketToOrigin(packet);

#if DEBUG
            ToOrigin.Add(packet);
#endif
        }
        private void HandleEncryption(EncryptionRequestPacket packet)
        {
            var generator = new CipherKeyGenerator();
            generator.Init(new KeyGenerationParameters(new SecureRandom(), 16 * 8));
            var sharedKey = generator.GenerateKey();

            var pkcs = new PKCS1Signer(packet.PublicKey);
            var signedSecret = pkcs.SignData(sharedKey);
            var signedVerify = pkcs.SignData(packet.VerificationToken);

            SendPacket(new EncryptionResponsePacket { SharedSecret = signedSecret, VerificationToken = signedVerify });

            Stream.InitializeEncryption(sharedKey);
        }


        public void SendPacket(ProtobufPacket packet)
        {
            if (Stream.Connected)
            {
                Stream.SendPacket(ref packet);

#if DEBUG
                ToServer.Add(packet);
#endif
            }
        }


        public void Dispose()
        {
            Stream?.Dispose();
        }
    }
}
