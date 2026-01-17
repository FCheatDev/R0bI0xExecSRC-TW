using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace Executor
{
    internal static class Logger
    {
        private static readonly object LockObj = new();
        private static bool _initialized;
        private static string? _logFilePath;
        private static StreamWriter? _writer;

        internal static string? LogFilePath => _logFilePath;

        internal static void Initialize()
        {
            lock (LockObj)
            {
                if (_initialized)
                {
                    return;
                }

                _initialized = true;

                try
                {
                    var baseDir = AppPaths.AppDirectory;
                    var logDir = ResolveLogDirectory(baseDir);

                    var now = DateTime.Now;
                    var pid = Environment.ProcessId;
                    var fileName = $"{now:yyyy-MM-dd_HH-mm-ss_fff}.log";
                    _logFilePath = Path.Combine(logDir, fileName);

                    var fs = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
                    _writer = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                    {
                        AutoFlush = true,
                    };

                    var listener = new TextWriterTraceListener(_writer);
                    Trace.Listeners.Add(listener);
                    Trace.AutoFlush = true;

                    Info("Logger", "Initialized");
                    Info("Logger", $"AppDirectory: {baseDir}");
                    Info("Logger", $"LogDirectory: {logDir}");
                    Info("Logger", $"LogFile: {_logFilePath}");
                    Info("Logger", $"ProcessId: {pid}");
                    Info("Logger", $"OS: {Environment.OSVersion}");
                    Info("Logger", $"Framework: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
                }
                catch
                {
                }
            }
        }

        private static string ResolveLogDirectory(string baseDir)
        {
            var candidates = new[]
            {
                Path.Combine(baseDir, "ax-log"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Executor", "ax-log"),
            };

            foreach (var dir in candidates)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    var testPath = Path.Combine(dir, ".write_test");
                    File.WriteAllText(testPath, "ok");
                    File.Delete(testPath);
                    return dir;
                }
                catch
                {
                }
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ax-log");
        }

        internal static void Info(string source, string message)
        {
            Write("INFO", source, message, null);
        }

        internal static void Success(string source, string message)
        {
            Write("SUCCESS", source, message, null);
        }

        internal static void Warn(string source, string message)
        {
            Write("WARING", source, message, null);
        }

        internal static void Error(string source, string message)
        {
            Write("ERROR", source, message, null);
        }

        internal static void Exception(string source, Exception ex, string? message = null)
        {
            Write("ERROR", source, message ?? ex.Message, ex);
        }

        private static void Write(string level, string source, string message, Exception? ex)
        {
            try
            {
                if (!_initialized)
                {
                    Initialize();
                }

                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}][{source}] {message}";

                lock (LockObj)
                {
                    try
                    {
                        _writer?.WriteLine(line);
                        if (ex != null)
                        {
                            _writer?.WriteLine(ex.ToString());
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
    }
}
