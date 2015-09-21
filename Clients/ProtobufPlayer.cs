using System.Collections.Generic;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Security;

using PokeD.Core;
using PokeD.Core.Interfaces;
using PokeD.Core.Packets;
using PokeD.Core.Packets.Encryption;
using PokeD.Core.Wrappers;

using PokeD.Server.Exceptions;
using PokeD.Server.IO;

namespace PokeD.Server.Clients
{
    public class ProtobufPlayer
    {
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
                _proxy.Disconnect();

            if (Stream.Connected && Stream.DataAvailable > 0)
            {
                var dataLength = Stream.ReadVarInt();
                if (dataLength == 0)
                    throw new ProtobufPlayerException("Reading error: Packet Length size is 0");

                var data = Stream.ReadByteArray(dataLength);

                HandleData(data);
            }
        }


        private void HandleData(byte[] data)
        {
            if (data == null)
                return;

            using (var reader = new ProtobufDataReader(data))
            {
                var id = reader.ReadVarInt();
                var origin = reader.ReadVarInt();
                var packet = PlayerResponse.Packets[id]().ReadPacket(reader);
                packet.Origin = origin;


                if (id == (int) PlayerPacketTypes.EncryptionResponse)
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
