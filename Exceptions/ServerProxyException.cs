using System;

namespace PokeD.ServerProxy.Exceptions
{
    public class ServerProxyException : Exception
    {
        public ServerProxyException() : base() { }

        public ServerProxyException(string message) : base(message) { }

        public ServerProxyException(string format, params object[] args) : base(string.Format(format, args)) { }

        public ServerProxyException(string message, Exception innerException) : base(message, innerException) { }

        public ServerProxyException(string format, Exception innerException, params object[] args) : base(string.Format(format, args), innerException) { }
    }
}
