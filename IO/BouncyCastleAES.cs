using System;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using PokeD.Core.Interfaces;
using PokeD.Core.Wrappers;

namespace PokeD.ServerProxy.IO
{
    public sealed class BouncyCastleAES : IAesStream
    {
        private readonly INetworkTCPClient _tcp;

        private readonly BufferedBlockCipher _decryptCipher;
        private readonly BufferedBlockCipher _encryptCipher;

        public BouncyCastleAES(INetworkTCPClient tcp, byte[] key)
        {
            _tcp = tcp;

            _encryptCipher = new BufferedBlockCipher(new CfbBlockCipher(new AesFastEngine(), 8));
            _encryptCipher.Init(true, new ParametersWithIV(new KeyParameter(key), key, 0, 16));

            _decryptCipher = new BufferedBlockCipher(new CfbBlockCipher(new AesFastEngine(), 8));
            _decryptCipher.Init(false, new ParametersWithIV(new KeyParameter(key), key, 0, 16));
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            var length = _tcp.Receive(buffer, offset, count);
            var decrypted = _decryptCipher.ProcessBytes(buffer, offset, length);
            Buffer.BlockCopy(decrypted, 0, buffer, offset, decrypted.Length);
            return length;
        }

        public void Write(byte[] buffer, int offset, int count)
        {
            var encrypted = _encryptCipher.ProcessBytes(buffer, offset, count);
            _tcp.Send(encrypted, 0, encrypted.Length);
        }

        public void Dispose()
        {
            _decryptCipher?.Reset();

            _encryptCipher?.Reset();
        }
    }
}