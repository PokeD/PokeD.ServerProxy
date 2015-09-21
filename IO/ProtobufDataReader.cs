using System;
using System.IO;
using System.Numerics;
using System.Text;

using PokeD.Core.Data;
using PokeD.Core.Interfaces;

using PokeD.Server.Exceptions;

namespace PokeD.Server.IO
{
    public sealed class ProtobufDataReader : IPacketDataReader
    {
        public bool IsServer => false;

        private Encoding Encoding { get; } = Encoding.UTF8;

        private readonly Stream _stream;
        
        public ProtobufDataReader(Stream stream)
        {
            _stream = stream;
        }

        public ProtobufDataReader(byte[] data)
        {
            _stream = new MemoryStream(data);
        }

        // -- String

        public string ReadString(int length = 0)
        {
            length = ReadVarInt();
            var stringBytes = ReadByteArray(length);

            return Encoding.GetString(stringBytes, 0, stringBytes.Length);
        }

        // -- VarInt

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
            return (int) result;
        }

        public VarInt ReadVarInt(out int length)
        {
            uint result = 0;
            length = 0;

            while (true)
            {
                var current = ReadByte();
                result |= (current & 0x7Fu) << length++ * 7;

                if (length > 5)
                    throw new ProtobufPlayerException("Reading error: VarInt may not be longer than 60 bits.");

                if ((current & 0x80) != 128)
                    break;
            }
            return (int) result;
        }


        // -- Boolean

        public bool ReadBoolean()
        {
            return Convert.ToBoolean(ReadByte());
        }

        // -- SByte & Byte

        public sbyte ReadSByte()
        {
            return unchecked((sbyte)ReadByte());
        }

        public byte ReadByte()
        {
            return (byte)_stream.ReadByte();
        }

        // -- Short & UShort

        public short ReadShort()
        {
            var bytes = ReadByteArray(2);
            Array.Reverse(bytes);

            return BitConverter.ToInt16(bytes, 0);
        }

        public ushort ReadUShort()
        {
            return (ushort)((ReadByte() << 8) | ReadByte());
        }

        // -- Int & UInt

        public int ReadInt()
        {
            var bytes = ReadByteArray(4);
            Array.Reverse(bytes);

            return BitConverter.ToInt32(bytes, 0);
        }

        public uint ReadUInt()
        {
            return (uint)(
                (ReadUShort() << 24) |
                (ReadUShort() << 16) |
                (ReadUShort() << 8) |
                 ReadUShort());
        }

        // -- Long & ULong

        public long ReadLong()
        {
            var bytes = ReadByteArray(8);
            Array.Reverse(bytes);

            return BitConverter.ToInt64(bytes, 0);
        }

        public ulong ReadULong()
        {
            return unchecked(
                   ((ulong)ReadUShort() << 56) |
                   ((ulong)ReadUShort() << 48) |
                   ((ulong)ReadUShort() << 40) |
                   ((ulong)ReadUShort() << 32) |
                   ((ulong)ReadUShort() << 24) |
                   ((ulong)ReadUShort() << 16) |
                   ((ulong)ReadUShort() << 8) |
                    (ulong)ReadUShort());
        }

        // -- BigInt & UBigInt

        public BigInteger ReadBigInteger()
        {
            var bytes = ReadByteArray(16);
            Array.Reverse(bytes);

            return new BigInteger(bytes);
        }

        public BigInteger ReadUBigInteger()
        {
            throw new NotImplementedException();
        }

        // -- Floats

        public float ReadFloat()
        {
            var bytes = ReadByteArray(4);
            Array.Reverse(bytes);

            return BitConverter.ToSingle(bytes, 0);
        }

        // -- Doubles

        public double ReadDouble()
        {
            var bytes = ReadByteArray(8);
            Array.Reverse(bytes);

            return BitConverter.ToDouble(bytes, 0);
        }


        // -- StringArray

        public string[] ReadStringArray(int value)
        {
            var myStrings = new string[value];

            for (var i = 0; i < value; i++)
                myStrings[i] = ReadString();

            return myStrings;
        }

        // -- VarIntArray

        public int[] ReadVarIntArray(int value)
        {
            var myInts = new int[value];

            for (var i = 0; i < value; i++)
                myInts[i] = ReadVarInt();

            return myInts;
        }

        // -- IntArray

        public int[] ReadIntArray(int value)
        {
            var myInts = new int[value];

            for (var i = 0; i < value; i++)
                myInts[i] = ReadInt();

            return myInts;
        }

        // -- ByteArray

        public byte[] ReadByteArray(int value)
        {
            var myBytes = new byte[value];

            var bytesRead = _stream.Read(myBytes, 0, myBytes.Length);

            while (true)
            {
                if (bytesRead != value)
                {
                    var newSize = value - bytesRead;
                    var bytesRead1 = _stream.Read(myBytes, bytesRead - 1, newSize);

                    if (bytesRead1 != newSize)
                    {
                        value = newSize;
                        bytesRead = bytesRead1;
                    }
                    else break;
                }
                else break;
            }

            return myBytes;
        }

        public int BytesLeft()
        {
            return (int)(_stream.Length - _stream.Position);
        }


        public void Dispose()
        {
            _stream?.Dispose();
        }
    }
}
