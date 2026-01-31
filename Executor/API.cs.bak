using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Executor
{
    internal static class API
    {
        public sealed record ApiResult(
            bool Success,
            string Provider,
            string Code,
            string Message,
            Exception? Exception = null);

        private const string SelectedApiKey = "selected_api";
        private const string VelocityKey = "velocity";
        private const string XenoKey = "xeno";

        private static readonly ConcurrentDictionary<string, byte> _pidDiagLogged = new();

        private static readonly object AttachLock = new();
        private static Task<ApiResult>? _inflightAttach;
        private static int _inflightAttachPid;
        private static int _lastAttachedPid;

        private static readonly object StaLock = new();
        private static StaThreadWorker? _staWorker;

        private sealed class StaThreadWorker
        {
            private readonly Thread _thread;
            private readonly TaskCompletionSource<Dispatcher> _dispatcherTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public StaThreadWorker()
            {
                _thread = new Thread(() =>
                {
                    try
                    {
                        var dispatcher = Dispatcher.CurrentDispatcher;
                        try
                        {
                            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
                        }
                        catch
                        {
                        }
                        _dispatcherTcs.TrySetResult(dispatcher);
                        Dispatcher.Run();
                    }
                    catch (Exception ex)
                    {
                        _dispatcherTcs.TrySetException(ex);
                    }
                });

                _thread.IsBackground = true;
                try { _thread.SetApartmentState(ApartmentState.STA); } catch { }
                _thread.Start();
            }

            public Task<T> Run<T>(Func<T> func, CancellationToken ct)
            {
                var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

                if (ct.IsCancellationRequested)
                {
                    tcs.TrySetCanceled(ct);
                    return tcs.Task;
                }

                Dispatcher dispatcher;
                try
                {
                    dispatcher = _dispatcherTcs.Task.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                    return tcs.Task;
                }

                var reg = ct.Register(() => tcs.TrySetCanceled(ct));
                dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (ct.IsCancellationRequested)
                        {
                            tcs.TrySetCanceled(ct);
                            return;
                        }

                        var result = func();
                        tcs.TrySetResult(result);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                    finally
                    {
                        reg.Dispose();
                    }
                }));

                return tcs.Task;
            }
        }

        private static string GetProviderName()
        {
            string? selectedApi = null;
            try
            {
                var cfg = ConfigManager.ReadConfig();
                selectedApi = ConfigManager.Get(cfg, SelectedApiKey);
            }
            catch
            {
            }

            var v = (selectedApi ?? string.Empty).Trim();
            if (v.Length == 0)
            {
                return "Velocity";
            }

            var lower = v.ToLowerInvariant();
            if (lower.Contains(XenoKey, StringComparison.OrdinalIgnoreCase) || string.Equals(v, "SpashAPIXeno", StringComparison.OrdinalIgnoreCase))
            {
                return "Xeno";
            }

            if (lower.Contains(VelocityKey, StringComparison.OrdinalIgnoreCase) || string.Equals(v, "SpashAPIVelocity", StringComparison.OrdinalIgnoreCase))
            {
                return "Velocity";
            }

            return "Velocity";
        }

        internal static void ResetAttachState()
        {
            ResetAttachState(clearLastAttachedPid: true);
        }

        internal static void ResetAttachState(bool clearLastAttachedPid)
        {
            lock (AttachLock)
            {
                _inflightAttach = null;
                _inflightAttachPid = 0;
                if (clearLastAttachedPid)
                {
                    _lastAttachedPid = 0;
                }
            }
        }

        private static bool IsVelocityProvider()
        {
            return string.Equals(GetProviderName(), "Velocity", StringComparison.OrdinalIgnoreCase);
        }

        private static Task<T> RunOnStaThreadAsync<T>(Func<T> func, CancellationToken ct)
        {
            lock (StaLock)
            {
                _staWorker ??= new StaThreadWorker();
                return _staWorker.Run(func, ct);
            }
        }

        private static ApiResult Ok(string code, string message)
        {
            return new ApiResult(true, GetProviderName(), code, message);
        }

        private static ApiResult Fail(string code, string message, Exception? ex = null)
        {
            return new ApiResult(false, GetProviderName(), code, message, ex);
        }

        private sealed class TopmostSuspendScope : IDisposable
        {
            private readonly bool _previous;
            private readonly bool _changed;

            private TopmostSuspendScope(bool previous, bool changed)
            {
                _previous = previous;
                _changed = changed;
            }

            public static TopmostSuspendScope? TryEnter()
            {
                try
                {
                    var app = Application.Current;
                    if (app == null)
                    {
                        return null;
                    }

                    bool previous = false;
                    var dispatcher = app.Dispatcher;
                    if (dispatcher == null)
                    {
                        return null;
                    }

                    var hasMain = dispatcher.Invoke(() =>
                    {
                        var w = app.MainWindow;
                        if (w == null)
                        {
                            return false;
                        }

                        previous = w.Topmost;
                        if (w.Topmost)
                        {
                            w.Topmost = false;
                            return true;
                        }

                        return true;
                    });

                    if (!hasMain)
                    {
                        return null;
                    }

                    return new TopmostSuspendScope(previous, changed: previous);
                }
                catch
                {
                    return null;
                }
            }

            public void Dispose()
            {
                if (!_changed)
                {
                    return;
                }

                try
                {
                    var app = Application.Current;
                    if (app == null)
                    {
                        return;
                    }

                    var dispatcher = app.Dispatcher;
                    if (dispatcher == null)
                    {
                        return;
                    }

                    dispatcher.Invoke(() =>
                    {
                        try
                        {
                            var w = app.MainWindow;
                            if (w != null)
                            {
                                w.Topmost = _previous;
                            }
                        }
                        catch
                        {
                        }
                    });
                }
                catch
                {
                }
            }
        }

        private static string GetApiLogTag()
        {
            return string.Equals(GetProviderName(), "Xeno", StringComparison.OrdinalIgnoreCase) ? "XENO" : "VELOCITY";
        }

        private static void LogPidDiag(string phase, int pid)
        {
            if (pid <= 0)
            {
                return;
            }

            var key = phase + ":" + pid;
            if (!_pidDiagLogged.TryAdd(key, 0))
            {
                return;
            }

            try
            {
                using var p = Process.GetProcessById(pid);
                string title;
                try { title = p.MainWindowTitle ?? string.Empty; } catch { title = string.Empty; }
                string name;
                try { name = p.ProcessName ?? string.Empty; } catch { name = string.Empty; }
                Logger.Info(GetApiLogTag(), $"PID diag ({phase}): pid={pid}, name={name}, title={title}");
            }
            catch
            {
                try
                {
                    Logger.Info(GetApiLogTag(), $"PID diag ({phase}): pid={pid}");
                }
                catch
                {
                }
            }
        }

        public static bool IsAttached()
        {
            if (!SpashApiInvoker.IsRobloxProcessRunning())
            {
                return false;
            }

            var pid = 0;
            try
            {
                _ = RobloxRuntime.TryGetRobloxProcessId(out pid);
            }
            catch
            {
                pid = 0;
            }

            LogPidDiag("attach", pid);

            lock (AttachLock)
            {
                if (pid == 0 && _lastAttachedPid != 0)
                {
                    return false;
                }

                if (pid != 0 && _lastAttachedPid != 0 && pid != _lastAttachedPid)
                {
                    return false;
                }
            }

            var attached = SpashApiInvoker.TryIsAttached(out var ok, out _) && ok;
            if (attached && pid != 0)
            {
                lock (AttachLock)
                {
                    _lastAttachedPid = pid;
                }
            }

            return attached;
        }

        public static bool IsRobloxOpen()
        {
            return SpashApiInvoker.IsRobloxProcessRunning();
        }

        public static void KillRoblox()
        {
            _ = SpashApiInvoker.TryKillRoblox(out _);
        }

        public static async Task<ApiResult> AttachAsync(CancellationToken ct)
        {
            if (!SpashApiInvoker.IsRobloxProcessRunning())
            {
                return Fail("NoProcessFound", "Roblox is not open.");
            }

            var pid = 0;
            try
            {
                _ = RobloxRuntime.TryGetRobloxProcessId(out pid);
            }
            catch
            {
                pid = 0;
            }

            Task<ApiResult>? inflight;
            lock (AttachLock)
            {
                if (_inflightAttach != null && !_inflightAttach.IsCompleted && pid == _inflightAttachPid)
                {
                    inflight = _inflightAttach;
                }
                else
                {
                    _inflightAttachPid = pid;
                    _inflightAttach = AttachCoreAsync(pid);
                    inflight = _inflightAttach;
                }
            }

            return await inflight.WaitAsync(ct);
        }

        private static async Task<ApiResult> AttachCoreAsync(int pid)
        {
            if (IsAttached())
            {
                return Ok("Attached", "Already attached.");
            }

            if (IsVelocityProvider())
            {
                return await RunOnStaThreadAsync(() => AttachCoreSync(pid, CancellationToken.None), CancellationToken.None);
            }

            return await Task.Run(() => AttachCoreSync(pid, CancellationToken.None));
        }

        private static ApiResult AttachCoreSync(int pid, CancellationToken ct)
        {
            using var topmostScope = TopmostSuspendScope.TryEnter();

            if (!SpashApiInvoker.IsRobloxProcessRunning())
            {
                return Fail("NoProcessFound", "Roblox is not open.");
            }

            if (pid != 0)
            {
                try
                {
                    _ = RobloxRuntime.TryGetRobloxProcessId(out var currentPid);
                    if (currentPid != 0 && currentPid != pid)
                    {
                        ResetAttachState(clearLastAttachedPid: false);
                        pid = currentPid;
                    }
                }
                catch
                {
                }
            }

            if (IsAttached())
            {
                return Ok("Attached", "Already attached.");
            }

            string? err;
            var okAttach = SpashApiInvoker.TryAttach(out err);
            if (!okAttach)
            {
                return Fail("ProviderException", err ?? "Attach failed.");
            }

            var timeout = TimeSpan.FromSeconds(15);
            var poll = TimeSpan.FromMilliseconds(200);
            var reattachInterval = TimeSpan.FromSeconds(2);
            var sw = Stopwatch.StartNew();
            var nextReattach = reattachInterval;

            while (sw.Elapsed < timeout)
            {
                if (ct.IsCancellationRequested)
                {
                    return Fail("Canceled", "Canceled.");
                }

                if (!SpashApiInvoker.IsRobloxProcessRunning())
                {
                    return Fail("NoProcessFound", "Roblox is not open.");
                }

                if (!SpashApiInvoker.TryIsAttached(out var ok, out var isAttachedError))
                {
                    return Fail("ProviderException", isAttachedError ?? "IsAttached failed.");
                }

                if (ok)
                {
                    var resolvedPid = pid;
                    if (resolvedPid == 0)
                    {
                        try
                        {
                            _ = RobloxRuntime.TryGetRobloxProcessId(out resolvedPid);
                        }
                        catch
                        {
                            resolvedPid = 0;
                        }
                    }

                    if (resolvedPid != 0)
                    {
                        lock (AttachLock)
                        {
                            _lastAttachedPid = resolvedPid;
                        }
                    }
                    return Ok("Attached", "Attached.");
                }

                if (sw.Elapsed >= nextReattach)
                {
                    okAttach = SpashApiInvoker.TryAttach(out err);
                    if (!okAttach)
                    {
                        return Fail("ProviderException", err ?? "Attach failed.");
                    }

                    nextReattach += reattachInterval;
                }

                Thread.Sleep(poll);
            }

            return Fail("AttachTimeout", "Attach timeout.");
        }

        public static async Task<ApiResult> ExecuteScriptAsync(string script, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(script))
            {
                return Fail("Unknown", "Script is empty.");
            }

            if (!SpashApiInvoker.IsRobloxProcessRunning())
            {
                return Fail("NoProcessFound", "Roblox is not open.");
            }

            if (!IsAttached())
            {
                var attachResult = await AttachAsync(ct);
                if (!attachResult.Success)
                {
                    return attachResult;
                }
            }

            try
            {
                if (RobloxRuntime.TryGetRobloxProcessId(out var pid))
                {
                    LogPidDiag("execute", pid);
                }
            }
            catch
            {
            }

            (bool ok, string? err) execAttempt;
            if (IsVelocityProvider())
            {
                execAttempt = await RunOnStaThreadAsync(() =>
                {
                    using var topmostScope = TopmostSuspendScope.TryEnter();
                    string? err;
                    var ok = SpashApiInvoker.TryExecuteScript(script, out err);
                    return (ok, err);
                }, ct);
            }
            else
            {
                execAttempt = await Task.Run(() =>
                {
                    using var topmostScope = TopmostSuspendScope.TryEnter();
                    string? err;
                    var ok = SpashApiInvoker.TryExecuteScript(script, out err);
                    return (ok, err);
                }, ct);
            }
            if (!execAttempt.ok)
            {
                return Fail("ProviderException", execAttempt.err ?? "Execute failed.");
            }

            return Ok("Executed", "Executed.");
        }
    }
}
