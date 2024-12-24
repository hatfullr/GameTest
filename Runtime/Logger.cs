using System.Collections.Generic;
using UnityEngine;

namespace GameTest
{
    public class Logger
    {
        /// <summary>
        /// When true, debug messages are printed to Console.
        /// </summary>
        public static DebugMode debug = DebugMode.Log | DebugMode.LogWarning | DebugMode.LogError;

        [System.Flags]
        public enum DebugMode
        {
            Log = 1 << 0,
            LogWarning = 1 << 1,
            LogError = 1 << 2,
        }

        public static string debugTag { get => "[" + nameof(GameTest) +"]"; }

        private static HashSet<string> loggedExceptions = new HashSet<string>();

        public static string ColorString(string text, string color)
        {
            if (string.IsNullOrEmpty(text)) return null;
            return "<color=" + color + ">" + text + "</color>";
        }

        [HideInCallstack]
        private static string GetLogString(string message, string color = null, bool hideMessage = true)
        {
            string tag = "<size=10>" + debugTag + "</size>";
            if (!string.IsNullOrEmpty(color)) tag = ColorString(tag, color);
            return string.Join(' ', tag, message) + "\n<size=10>(Disable these messages with the debug toolbar button)</size>";
        }


        /// <summary>
        /// Print a log message to the console, intended for debug messages.
        /// </summary>
        [HideInCallstack] public static void Log(string message, Object context, string color, bool hideMessage = true) { if (debug.HasFlag(DebugMode.Log)) Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, context, "{0}", GetLogString(message, color, hideMessage)); }
        [HideInCallstack] public static void Log(string message, Object context) => Log(message, context, null);
        [HideInCallstack] public static void Log(string message, string color) => Log(message, null, color);
        [HideInCallstack] public static void Log(string message) => Log(message, null, null);

        /// <summary>
        /// Print a warning message to the console.
        /// </summary>
        [HideInCallstack] public static void LogWarning(string message, Object context, string color, bool hideMessage = true) { if (debug.HasFlag(DebugMode.LogWarning)) Debug.LogWarning(GetLogString(message, color, hideMessage), context); }
        [HideInCallstack] public static void LogWarning(string message, Object context) => LogWarning(message, context, null);
        [HideInCallstack] public static void LogWarning(string message, string color) => LogWarning(message, null, color);
        [HideInCallstack] public static void LogWarning(string message) => LogWarning(message, null, null);


        /// <summary>
        /// Print a warning message to the console.
        /// </summary>
        [HideInCallstack] public static void LogError(string message, Object context, string color, bool hideMessage = true) { if (debug.HasFlag(DebugMode.LogError)) Debug.LogError(GetLogString(message, color, hideMessage), context); }
        [HideInCallstack] public static void LogError(string message, Object context) => LogError(message, context, null);
        [HideInCallstack] public static void LogError(string message, string color) => LogError(message, null, color);
        [HideInCallstack] public static void LogError(string message) => LogError(message, null, null);

        /// <summary>
        /// Print an exception to the console. The color cannot be changed. To ensure that an exception is logged only a single time
        /// per assembly reload, set "once" to true. Only the exception's message is checked when "once" is true.
        /// </summary>
        [HideInCallstack]
        public static void LogException(System.Exception exception, Object context, bool once = false)
        {
            if (once && loggedExceptions.Contains(exception.Message)) return;
            Debug.LogException(exception, context);
            loggedExceptions.Add(exception.Message);
        }
        [HideInCallstack]
        public static void LogException(System.Exception exception, bool once = false)
        {
            if (once && loggedExceptions.Contains(exception.Message)) return;
            Debug.LogException(exception);
            loggedExceptions.Add(exception.Message);
        }
    }
}