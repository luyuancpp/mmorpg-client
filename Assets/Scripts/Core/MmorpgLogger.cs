using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace MmorpgClient.Core
{
    /// <summary>
    /// Lightweight leveled logger with a Unity console sink and a rolling
    /// file sink under <c>Application.persistentDataPath/logs/</c>.
    ///
    /// Production guidance:
    ///   * Set <see cref="MinLevel"/> to <see cref="LogLevel.Info"/> for
    ///     release builds; <see cref="LogLevel.Debug"/> for QA.
    ///   * Logs are flushed best-effort and capped at <see cref="MaxFileBytes"/>;
    ///     once exceeded, the file is rotated to <c>client.log.1</c>.
    ///   * Avoid logging PII (account name, token bytes); the network layer
    ///     should pass opaque request IDs instead.
    /// </summary>
    public static class MmorpgLogger
    {
        public enum LogLevel { Debug = 0, Info = 1, Warn = 2, Error = 3 }

        public static LogLevel MinLevel = LogLevel.Info;
        public static long     MaxFileBytes = 10 * 1024 * 1024; // 10 MiB
        public static bool     EchoToUnityConsole = true;

        private static readonly object _gate = new();
        private static StreamWriter _writer;
        private static string _logPath;

        public static string LogPath => _logPath;

        public static void Init()
        {
            if (_writer != null) return;
            try
            {
                var dir = Path.Combine(Application.persistentDataPath, "logs");
                Directory.CreateDirectory(dir);
                _logPath = Path.Combine(dir, "client.log");
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxFileBytes)
                {
                    var rotated = _logPath + ".1";
                    if (File.Exists(rotated)) File.Delete(rotated);
                    File.Move(_logPath, rotated);
                }
                _writer = new StreamWriter(new FileStream(_logPath,
                    FileMode.Append, FileAccess.Write, FileShare.Read), Encoding.UTF8)
                { AutoFlush = false };
                Application.quitting += Shutdown;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MmorpgLogger] file sink disabled: {ex.Message}");
            }
        }

        public static void Debug(string msg, string tag = null) => Write(LogLevel.Debug, tag, msg);
        public static void Info (string msg, string tag = null) => Write(LogLevel.Info,  tag, msg);
        public static void Warn (string msg, string tag = null) => Write(LogLevel.Warn,  tag, msg);
        public static void Error(string msg, string tag = null) => Write(LogLevel.Error, tag, msg);

        public static void Exception(Exception ex, string tag = null)
            => Write(LogLevel.Error, tag, ex == null ? "<null>" : ex.ToString());

        private static void Write(LogLevel level, string tag, string msg)
        {
            if (level < MinLevel) return;
            var ts = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var line = $"{ts} [{Lvl(level)}] {(string.IsNullOrEmpty(tag) ? "" : "[" + tag + "] ")}{msg}";

            if (EchoToUnityConsole)
            {
                switch (level)
                {
                    case LogLevel.Warn:  UnityEngine.Debug.LogWarning(line); break;
                    case LogLevel.Error: UnityEngine.Debug.LogError(line);   break;
                    default:             UnityEngine.Debug.Log(line);        break;
                }
            }

            lock (_gate)
            {
                if (_writer == null) return;
                try
                {
                    _writer.WriteLine(line);
                    if (level >= LogLevel.Warn) _writer.Flush();
                }
                catch { /* sink failure must never crash gameplay */ }
            }
        }

        private static string Lvl(LogLevel l) => l switch
        {
            LogLevel.Debug => "DBG",
            LogLevel.Info  => "INF",
            LogLevel.Warn  => "WRN",
            LogLevel.Error => "ERR",
            _              => "???",
        };

        private static void Shutdown()
        {
            lock (_gate)
            {
                try { _writer?.Flush(); _writer?.Dispose(); } catch { }
                _writer = null;
            }
        }
    }
}
