using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Extensions.Logging;

namespace DocExtractor.UI.Logging
{
    internal class UiFileLoggerProvider : ILoggerProvider
    {
        private readonly Action<string, string> _uiSink;
        private readonly string _logFilePath;

        public UiFileLoggerProvider(Action<string, string> uiSink, string logDir)
        {
            _uiSink = uiSink;
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, "docextractor-" + DateTime.Now.ToString("yyyyMMdd") + ".log");
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new UiFileLogger(categoryName, _uiSink, _logFilePath);
        }

        public void Dispose()
        {
        }

        private class UiFileLogger : ILogger
        {
            private static readonly object FileLock = new object();
            private readonly string _category;
            private readonly Action<string, string> _uiSink;
            private readonly string _logFilePath;

            public UiFileLogger(string category, Action<string, string> uiSink, string logFilePath)
            {
                _category = category;
                _uiSink = uiSink;
                _logFilePath = logFilePath;
            }

            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;

                string timestamp = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                string level = ToShortLevel(logLevel);
                string message = formatter != null ? formatter(state, exception) : state?.ToString() ?? string.Empty;
                string structured = FormatStructuredState(state);
                string exText = exception != null ? " | ex=" + exception.Message : string.Empty;

                string line = $"[{timestamp} {level}] [{_category}] {message}{structured}{exText}";

                lock (FileLock)
                {
                    File.AppendAllText(_logFilePath, line + Environment.NewLine);
                }

                _uiSink(_category, line);
            }

            private static string FormatStructuredState<TState>(TState state)
            {
                if (!(state is IEnumerable<KeyValuePair<string, object>> kvps))
                    return string.Empty;

                var parts = new List<string>();
                foreach (var kv in kvps)
                {
                    if (kv.Key == "{OriginalFormat}") continue;
                    string value = kv.Value == null ? string.Empty : kv.Value.ToString();
                    parts.Add($"{kv.Key}={value}");
                }
                return parts.Count == 0 ? string.Empty : " | " + string.Join(" ", parts);
            }

            private static string ToShortLevel(LogLevel level)
            {
                return level switch
                {
                    LogLevel.Trace => "TRC",
                    LogLevel.Debug => "DBG",
                    LogLevel.Information => "INF",
                    LogLevel.Warning => "WRN",
                    LogLevel.Error => "ERR",
                    LogLevel.Critical => "CRT",
                    _ => "NON"
                };
            }
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new NullScope();
            public void Dispose() { }
        }
    }
}
