using System;

namespace PokeD.Server.Exceptions
{
    public class P3DPlayerException : Exception
    {
        public P3DPlayerException() : base() { }

        public P3DPlayerException(string message) : base(message) { }

        public P3DPlayerException(string format, params object[] args) : base(string.Format(format, args)) { }

        public P3DPlayerException(string message, Exception innerException) : base(message, innerException) { }

        public P3DPlayerException(string format, Exception innerException, params object[] args) : base(string.Format(format, args), innerException) { }
    }
}
