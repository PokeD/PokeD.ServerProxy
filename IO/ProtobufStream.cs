using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text;

using ICSharpCode.SharpZipLib.Zip.Compression;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

using PokeD.Core.Data;
using PokeD.Core.Interfaces;
using PokeD.Core.Packets;
using PokeD.Core.Wrappers;

using PokeD.Server.Exceptions;

namespace PokeD.Server.IO
{
    public sealed class ProtobufStream : IPacketStream
    {
        #region Properties

        public bool IsServer => false;

        public bool Connected => _tcp != null && _tcp.Connected;
        public int DataAvailable => _tcp?.DataAvailable ?? 0;


        public bool EncryptionEnabled { get; private set; }

        public bool CompressionEnabled => CompressionThreshold > 0;
        public uint CompressionThreshold { get; private set; }

        private Encoding Encoding { get; } = Encoding.UTF8;

        #endregion

        private readonly INetworkTCPClient _tcp;

        private IAesStream _aesStream;
        private byte[] _buffer;

        public ProtobufStream(INetworkTCPClient tcp)
        {
            _tcp = tcp;
        }


        public void Connect(string ip, ushort port)
        {
            _tcp.Connect(ip, port);
        }
        public void Disconnect()
        {
            _tcp.Disconnect();
        }


        public void InitializeEncryption(byte[] key)
        {
            _aesStream = new BouncyCastleAES(_tcp, key);

            EncryptionEnabled = true;
        }

        public void SetCompression(uint threshold)
        {
            CompressionThreshold = threshold;
        }


        #region Vars

        // -- String

        public void WriteString(string value, int length = 0)
        {
            var lengthBytes = GetVarIntBytes(value.Length);
            var final = new byte[value.Length + lengthBytes.Length];
            
            Buffer.BlockCopy(lengthBytes, 0, final, 0, lengthBytes.Length);
            Buffer.BlockCopy(Encoding.GetBytes(value), 0, final, lengthBytes.Length, value.Length);
            
            WriteByteArray(final);
        }

        // -- VarInt

        public void WriteVarInt(VarInt value)
        {
            WriteByteArray(GetVarIntBytes(value));
        }

        // BUG: Is broken?
        public static byte[] GetVarIntBytes(int _value)
        {
            uint value = (uint)_value;

            var bytes = new List<byte>();
            while (true)
            {
                if ((value & 0xFFFFFF80u) == 0)
                {
                    bytes.Add((byte)value);
                    break;
                }
                bytes.Add((byte)(value & 0x7F | 0x80));
                value >>= 7;
            }

            return bytes.ToArray();
        }

        // -- Boolean

        public void WriteBoolean(bool value)
        {
            WriteByte(Convert.ToByte(value));
        }

        // -- SByte & Byte

        public void WriteSByte(sbyte value)
        {
            WriteByte(unchecked((byte)value));
        }

        public void WriteByte(byte value)
        {
            if (_buffer != null)
            {
                var tempBuff = new byte[_buffer.Length + 1];

                Buffer.BlockCopy(_buffer, 0, tempBuff, 0, _buffer.Length);
                tempBuff[_buffer.Length] = value;

                _buffer = tempBuff;
            }
            else
                _buffer = new byte[] { value };
        }

        // -- Short & UShort

        public void WriteShort(short value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        public void WriteUShort(ushort value)
        {
            WriteByteArray(new byte[]
            {
                (byte) ((value & 0xFF00) >> 8),
                (byte) (value & 0xFF)
            });
        }

        // -- Int & UInt

        public void WriteInt(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        public void WriteUInt(uint value)
        {
            WriteByteArray(new[]
            {
                (byte)((value & 0xFF000000) >> 24),
                (byte)((value & 0xFF0000) >> 16),
                (byte)((value & 0xFF00) >> 8),
                (byte)(value & 0xFF)
            });
        }

        // -- Long & ULong

        public void WriteLong(long value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        public void WriteULong(ulong value)
        {
            WriteByteArray(new[]
            {
                (byte)((value & 0xFF00000000000000) >> 56),
                (byte)((value & 0xFF000000000000) >> 48),
                (byte)((value & 0xFF0000000000) >> 40),
                (byte)((value & 0xFF00000000) >> 32),
                (byte)((value & 0xFF000000) >> 24),
                (byte)((value & 0xFF0000) >> 16),
                (byte)((value & 0xFF00) >> 8),
                (byte)(value & 0xFF)
            });
        }

        // -- BigInt & UBigInt

        public void WriteBigInteger(BigInteger value)
        {
            var bytes = value.ToByteArray();
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        public void WriteUBigInteger(BigInteger value)
        {
            throw new NotImplementedException();
        }

        // -- Float

        public void WriteFloat(float value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }

        // -- Double

        public void WriteDouble(double value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);

            WriteByteArray(bytes);
        }


        // -- StringArray

        public void WriteStringArray(params string[] value)
        {
            var length = value.Length;

            for (var i = 0; i < length; i++)
                WriteString(value[i]);
        }

        // -- VarIntArray

        public void WriteVarIntArray(params int[] value)
        {
            var length = value.Length;

            for (var i = 0; i < length; i++)
                WriteVarInt(value[i]);
        }

        // -- IntArray

        public void WriteIntArray(params int[] value)
        {
            var length = value.Length;

            for (var i = 0; i < length; i++)
                WriteInt(value[i]);
        }

        // -- ByteArray

        public void WriteByteArray(params byte[] value)
        {
            if (_buffer != null)
            {
                var tempLength = _buffer.Length + value.Length;
                var tempBuff = new byte[tempLength];

                Buffer.BlockCopy(_buffer, 0, tempBuff, 0, _buffer.Length);
                Buffer.BlockCopy(value, 0, tempBuff, _buffer.Length, value.Length);

                _buffer = tempBuff;
            }
            else
                _buffer = value;
        }

        #endregion Vars


        // -- Read methods

        public byte ReadByte()
        {
            var buffer = new byte[1];

            Receive(buffer, 0, buffer.Length);

            return buffer[0];
        }

        public VarInt ReadVarInt()
        {
            uint result = 0;
            int length = 0;

            while (true)
            {
                var current = ReadByte();
                result |= (current & 0x7Fu) << length++ * 7;

                if (length > 5)
                    throw new ProtobufPlayerException("Reading error: VarInt may not be longer than 28 bits.");

                if ((current & 0x80) != 128)
                    break;
            }
            return (int)result;
        }

        public byte[] ReadByteArray(int value)
        {
            var result = new byte[value];
            if (value == 0) return result;
            int n = value;
            while (true)
            {
                n -= Receive(result, value - n, n);
                if (n == 0)
                    break;
            }
            return result;
        }

        public string ReadLine()
        {
            throw new NotImplementedException();
        }

        // -- Read methods


        private void Send(byte[] buffer, int offset, int count)
        {
            if (EncryptionEnabled)
                _aesStream.Write(buffer, offset, count);
            else
                _tcp.Send(buffer, offset, count);
        }

        private int Receive(byte[] buffer, int offset, int count)
        {
            if (EncryptionEnabled)
                return _aesStream.Read(buffer, offset, count);
            else
                return _tcp.Receive(buffer, offset, count);
        }

        public void SendPacket(ref ProtobufPacket packet)
        {
            WriteVarInt(packet.ID);
            WriteVarInt(packet.Origin);
            packet.WritePacket(this);
            Purge();
        }
        public void SendPacket(ref P3DPacket packet)
        {
            throw new NotImplementedException();
        }


        #region Purge

        private void Purge()
        {
            if (CompressionEnabled)
                PurgeModernWithCompression();
            else
                PurgeModernWithoutCompression();
        }

        private void PurgeModernWithoutCompression()
        {
            var lenBytes = GetVarIntBytes(_buffer.Length);
            var tempBuff = new byte[_buffer.Length + lenBytes.Length];
            
            Buffer.BlockCopy(lenBytes, 0, tempBuff, 0, lenBytes.Length);
            Buffer.BlockCopy(_buffer, 0, tempBuff, lenBytes.Length, _buffer.Length);

            Send(tempBuff, 0, tempBuff.Length);

            _buffer = null;
        }

        private void PurgeModernWithCompression()
        {
            var packetLength = 0; // -- data.Length + GetVarIntBytes(data.Length).Length
            var dataLength = 0; // -- UncompressedData.Length
            var data = _buffer;

            packetLength = _buffer.Length + GetVarIntBytes(_buffer.Length).Length; // -- Get first Packet length

            if (packetLength >= CompressionThreshold) // -- if Packet length > threshold, compress
            {
                using (var outputStream = new MemoryStream())
                using (var inputStream = new DeflaterOutputStream(outputStream, new Deflater(0)))
                {
                    inputStream.Write(_buffer, 0, _buffer.Length);
                    inputStream.Finish();

                    data = outputStream.ToArray();
                }

                dataLength = data.Length;
                packetLength = dataLength + GetVarIntBytes(data.Length).Length; // -- Calculate new packet length
            }


            var packetLengthByteLength = GetVarIntBytes(packetLength);
            var dataLengthByteLength = GetVarIntBytes(dataLength);

            var tempBuff = new byte[data.Length + packetLengthByteLength.Length + dataLengthByteLength.Length];

            Buffer.BlockCopy(packetLengthByteLength, 0, tempBuff, 0, packetLengthByteLength.Length);
            Buffer.BlockCopy(dataLengthByteLength, 0, tempBuff, packetLengthByteLength.Length, dataLengthByteLength.Length);
            Buffer.BlockCopy(data, 0, tempBuff, packetLengthByteLength.Length + dataLengthByteLength.Length, data.Length);

            Send(tempBuff, 0, tempBuff.Length);

            _buffer = null;
        }

        #endregion

        public void Dispose()
        {
            _tcp?.Disconnect().Dispose();

            _aesStream?.Dispose();

            _buffer = null;
        }
    }
}
