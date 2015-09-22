using System;

using PokeD.Core.Wrappers;

namespace PokeD.ServerProxy
{
    /// <summary>
    /// Message Log Type
    /// </summary>
    public enum LogType
    {
        /// <summary>
        /// General Log Type.
        /// </summary>
        Info,

        /// <summary>
        /// Error Log Type.
        /// </summary>
        Warning,

        /// <summary>
        /// Debug Log Type.
        /// </summary>
        Debug,

        /// <summary>
        /// Chat Log Type.
        /// </summary>
        Chat,

        /// <summary>
        /// PM Log Type.
        /// </summary>
        PM,

        /// <summary>
        /// Server Chat Log Type.
        /// </summary>
        Server,

        /// <summary>
        /// Trade Log Type.
        /// </summary>
        Trade,

        /// <summary>
        /// PvP Log Type.
        /// </summary>
        PvP,

        /// <summary>
        /// Command Log Type.
        /// </summary>
        Command,

        /// <summary>
        /// Should be reported.
        /// </summary>
        GlobalError
    }

    public static class Logger
    {
        public static void Log(LogType type, string message)
        {
            InputWrapper.LogWriteLine($"[{DateTime.Now:yyyy-MM-dd_HH:mm:ss}]_[{type}]:{message}");
        }

        public static void LogChatMessage(string player, string message)
        {
            InputWrapper.LogWriteLine($"[{DateTime.Now:yyyy-MM-dd_HH:mm:ss}]_<{player}>_{message}");
        }
    }
}
