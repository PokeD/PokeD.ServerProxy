using System;

namespace PokeD.ServerProxy.Exceptions
{
    public class ProtobufReadingException : Exception
    {
        public ProtobufReadingException() : base() { }

        public ProtobufReadingException(string message) : base(message) { }

        public ProtobufReadingException(string format, params object[] args) : base(string.Format(format, args)) { }

        public ProtobufReadingException(string message, Exception innerException) : base(message, innerException) { }

        public ProtobufReadingException(string format, Exception innerException, params object[] args) : base(string.Format(format, args), innerException) { }
    }
}
