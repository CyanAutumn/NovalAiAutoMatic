using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using log4net;

namespace AutoNai3Tools.utils {
    [Flags]
    internal enum LogSinkCapabilities {
        None = 0,
        Ui = 1,
        Persistent = 2,
    }

    internal enum LogLevel {
        Debug,
        Info,
        Warn,
        Error,
        Fatal,
    }

    internal interface ILogSink {
        LogSinkCapabilities Capabilities { get; }
        bool IsEnabled(LogLevel level);
        void Write(LogEntry entry);
    }

    internal interface ILogSpacerSink {
        void InsertSpacer();
    }
    /// <summary>由前端实现：接收「当前图片信息」那一行的内容。</summary>
    internal interface IPicInfoSink {
        void UpdatePicInfo(string message);
    }

    internal sealed class LogEntry {
        public LogEntry(LogLevel level, string message, string category, Exception exception = null,
            IDictionary<string, object> context = null) {
            Timestamp = DateTime.Now;
            Level = level;
            Message = message ?? string.Empty;
            Category = category;
            Exception = exception;
            if (context != null && context.Count > 0)
                Context = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(context));
            else
                Context = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
        }

        public DateTime Timestamp { get; }
        public LogLevel Level { get; }
        public string Message { get; }
        public string Category { get; }
        public Exception Exception { get; }
        public IReadOnlyDictionary<string, object> Context { get; }
    }

    internal static class Logger {
        private static readonly object SyncRoot = new object();
        private static readonly List<ILogSink> Sinks = new List<ILogSink>();
        private static ILogSink uiSink;
        private static bool initialized;

        public static void Initialize(ILogSink sink) {
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            lock (SyncRoot) {
                Sinks.Clear();
                uiSink = sink;
                Sinks.Add(new Log4NetSink());
                Sinks.Add(sink);
                initialized = true;
            }
        }

        public static void Info(string message, bool showToTextBox = true, bool saveToLocal = true,
            IDictionary<string, object> context = null) =>
            Log(LogLevel.Info, message, showToTextBox, saveToLocal, null, context);

        public static void Debug(string message, bool showToTextBox = true, bool saveToLocal = true,
            IDictionary<string, object> context = null) =>
            Log(LogLevel.Debug, message, showToTextBox, saveToLocal, null, context);

        public static void Warn(string message, bool showToTextBox = true, bool saveToLocal = true,
            IDictionary<string, object> context = null) =>
            Log(LogLevel.Warn, message, showToTextBox, saveToLocal, null, context);

        public static void Error(string message, bool showToTextBox = true, bool saveToLocal = true,
            Exception exception = null, IDictionary<string, object> context = null) =>
            Log(LogLevel.Error, message, showToTextBox, saveToLocal, exception, context);

        public static void Fatal(string message, bool showToTextBox = true, bool saveToLocal = true,
            Exception exception = null, IDictionary<string, object> context = null) =>
            Log(LogLevel.Fatal, message, showToTextBox, saveToLocal, exception, context);

        public static IDictionary<string, object> Context(params (string Key, object Value)[] pairs) {
            var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (pairs == null)
                return dict;

            foreach (var (key, value) in pairs) {
                if (string.IsNullOrWhiteSpace(key))
                    continue;
                dict[key] = value;
            }

            return dict;
        }

        private static void Log(LogLevel level, string message, bool showToTextBox, bool saveToLocal,
            Exception exception, IDictionary<string, object> context) {
            if (!initialized)
                return;

            string category = GetCallerCategory();
            var entry = new LogEntry(level, message, category, exception, context);

            List<ILogSink> snapshot;
            lock (SyncRoot) {
                snapshot = Sinks.ToList();
            }

            foreach (var sink in snapshot) {
                if (!sink.IsEnabled(level))
                    continue;

                if (!showToTextBox && sink.Capabilities.HasFlag(LogSinkCapabilities.Ui))
                    continue;

                if (!saveToLocal && sink.Capabilities.HasFlag(LogSinkCapabilities.Persistent))
                    continue;

                sink.Write(entry);
            }
        }

        private static string GetCallerCategory() {
            var frames = new StackTrace().GetFrames();
            if (frames == null)
                return "Unknown";

            foreach (var frame in frames) {
                var method = frame.GetMethod();
                if (method?.DeclaringType == null)
                    continue;

                if (method.DeclaringType == typeof(Logger))
                    continue;

                return method.DeclaringType.Name;
            }

            return "Unknown";
        }

        internal static string RenderEntry(LogEntry entry, bool includeContext = true) {
            var builder = new StringBuilder();
            builder.Append('[').Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            builder.Append("] [").Append(entry.Level).Append("] ");
            if (!string.IsNullOrWhiteSpace(entry.Category))
                builder.Append(entry.Category).Append(": ");
            builder.Append(entry.Message);

            if (includeContext && entry.Context?.Count > 0) {
                builder.Append(" | ");
                builder.Append(string.Join(", ", entry.Context.Select(kvp => $"{kvp.Key}={kvp.Value}")));
            }

            if (entry.Exception != null)
                builder.Append(" | ").Append(entry.Exception);

            return builder.ToString();
        }

        public static void PicInfo(string message) {
            (uiSink as IPicInfoSink)?.UpdatePicInfo(message);
        }

        public static void Spacer() {
            if (!initialized)
                return;

            List<ILogSink> snapshot;
            lock (SyncRoot) {
                snapshot = Sinks.ToList();
            }

            foreach (var sink in snapshot) {
                if (sink is ILogSpacerSink spacerSink)
                    spacerSink.InsertSpacer();
            }
        }

        private class Log4NetSink : ILogSink, ILogSpacerSink {
            private readonly ILog internalLogger = LogManager.GetLogger(typeof(Logger));

            public LogSinkCapabilities Capabilities => LogSinkCapabilities.Persistent;

            public bool IsEnabled(LogLevel level) => true;

            public void Write(LogEntry entry) {
                string formatted = RenderEntry(entry);
                switch (entry.Level) {
                    case LogLevel.Debug:
                        internalLogger.Debug(formatted);
                        break;
                    case LogLevel.Info:
                        internalLogger.Info(formatted);
                        break;
                    case LogLevel.Warn:
                        internalLogger.Warn(formatted);
                        break;
                    case LogLevel.Error:
                        if (entry.Exception != null)
                            internalLogger.Error(formatted, entry.Exception);
                        else
                            internalLogger.Error(formatted);
                        break;
                    case LogLevel.Fatal:
                        if (entry.Exception != null)
                            internalLogger.Fatal(formatted, entry.Exception);
                        else
                            internalLogger.Fatal(formatted);
                        break;
                }
            }
            public void InsertSpacer() {
                internalLogger.Info(string.Empty);
            }
        }

    }
}
