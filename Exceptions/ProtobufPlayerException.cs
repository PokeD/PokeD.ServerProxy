using System;

namespace PokeD.ServerProxy.Exceptions
{
    public class ProtobufPlayerException : Exception
    {
        public ProtobufPlayerException() : base() { }

        public ProtobufPlayerException(string message) : base(message) { }

        public ProtobufPlayerException(string format, params object[] args) : base(string.Format(format, args)) { }

        public ProtobufPlayerException(string message, Exception innerException) : base(message, innerException) { }

        public ProtobufPlayerException(string format, Exception innerException, params object[] args) : base(string.Format(format, args), innerException) { }
    }
}
