using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace Executor
{
    internal static class SpashApiInvoker
    {
        private const string SelectedApiKey = "selected_api";
        private const string VelocityKey = "velocity";
        private const string XenoKey = "xeno";

        private static readonly object ResolverLock = new();
        private static bool _resolverInstalled;
        private static string? _resolverApiFolder;
        private static readonly List<IntPtr> _nativeSearchCookies = new();

        private static readonly ConcurrentDictionary<string, byte> _resolveLookupLogged = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> _resolveNotFoundLogged = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> _apiTypeFoundLogged = new(StringComparer.OrdinalIgnoreCase);

        private static readonly ConcurrentDictionary<string, Type> _apiTypeCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, MethodInfo> _apiMethodCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, InvocationLogState> _invokeLogStates = new(StringComparer.OrdinalIgnoreCase);

        private sealed class InvocationLogState
        {
            public long LastLogTicks;
            public string? LastValue;
        }

        private static readonly TimeSpan ChattyLogInterval = TimeSpan.FromSeconds(2);

        private static bool _initialized;
        private static bool _dllLoaded;
        private static Exception? _initializationError;
        private static readonly object _initLock = new();

        private enum SelectedApi
        {
            Velocity,
            Xeno,
        }

        internal static bool IsInitializedForDebug()
        {
            lock (_initLock)
            {
                return _initialized && _initializationError == null;
            }
        }

        internal static void ResetForApiChange()
        {
            lock (_initLock)
            {
                _initialized = false;
                _dllLoaded = false;
                _initializationError = null;
            }

            try
            {
                _apiTypeCache.Clear();
                _apiMethodCache.Clear();
                _apiTypeFoundLogged.Clear();
                _invokeLogStates.Clear();
            }
            catch
            {
            }
        }

        private static string GetApiLogTag(SelectedApi api)
        {
            return api == SelectedApi.Xeno ? "XENO" : "VELOCITY";
        }

        private static string GetApiLogTagFromSelectedString(string? selected)
        {
            var s = (selected ?? string.Empty).Trim();
            if (s.Length == 0)
            {
                return "VELOCITY";
            }

            if (s.Contains(XenoKey, StringComparison.OrdinalIgnoreCase))
            {
                return "XENO";
            }

            if (s.Contains(VelocityKey, StringComparison.OrdinalIgnoreCase))
            {
                return "VELOCITY";
            }

            return "API";
        }

        private static void LogInfo(string apiTag, string message)
        {
            try { Logger.Info(apiTag, message); } catch { }
        }

        private static void LogWarn(string apiTag, string message)
        {
            try { Logger.Warn(apiTag, message); } catch { }
        }

        private static void LogError(string apiTag, string message)
        {
            try { Logger.Error(apiTag, message); } catch { }
        }

        private static SelectedApi GetSelectedApi()
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
                return SelectedApi.Velocity;
            }

            if (v.Contains(XenoKey, StringComparison.OrdinalIgnoreCase) || string.Equals(v, "SpashAPIXeno", StringComparison.OrdinalIgnoreCase))
            {
                return SelectedApi.Xeno;
            }

            return SelectedApi.Velocity;
        }

        private static bool EnsureInitialized(out string? error)
        {
            error = null;
            lock (_initLock)
            {
                if (_initialized)
                {
                    if (_initializationError != null)
                    {
                        error = _initializationError.Message;
                        return false;
                    }
                    return true;
                }

                try
                {
                    if (!_dllLoaded)
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

                        if (string.IsNullOrWhiteSpace(selectedApi))
                        {
                            selectedApi = "Velocity";
                        }

                        var tag = GetApiLogTagFromSelectedString(selectedApi);
                        LogInfo(tag, $"Loading API: {selectedApi}");

                        if (TryLoadSelectedApiAssembly(selectedApi, out var asm, out var dllPath, out var loadError))
                        {
                            if (asm != null)
                            {
                                LogInfo(tag, $"Successfully loaded: {dllPath}");
                                _dllLoaded = true;
                            }
                        }
                        else
                        {
                            _initializationError = new Exception(loadError ?? "Load failed.");
                            error = loadError;
                            return false;
                        }
                    }

                    _initialized = true;
                    return true;
                }
                catch (Exception ex)
                {
                    _initializationError = ex;
                    error = FormatExceptionMessage(ex);
                    LogError("API", $"Initialization error: {error}");
                    return false;
                }
            }
        }

        internal static bool IsRobloxProcessRunning()
        {
            try
            {
                RobloxRuntime.Initialize();
                RobloxRuntime.RefreshRunningState();
            }
            catch
            {
            }

            try
            {
                return RobloxRuntime.DetectRobloxProcessRunning();
            }
            catch
            {
                try
                {
                    return RobloxRuntime.IsRobloxRunning;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal static bool TryExecuteScript(string script, out string? error)
        {
            if (!EnsureInitialized(out error))
            {
                return false;
            }

            error = null;
            try
            {
                var api = GetSelectedApi();
                var typeName = api == SelectedApi.Xeno ? "SpashAPIXeno" : "SpashAPIVelocity";

                if (!TryInvokeApiMethod(typeName, "ExecuteScript", new object[] { script }, out var result, out error))
                {
                    return false;
                }

                if (!TryUnwrapTaskResult(result, out var final, out error))
                {
                    return false;
                }

                if (api == SelectedApi.Velocity)
                {
                    return TryInterpretVelocityExecuteScriptResult(final, out error);
                }

                if (final is bool ok && !ok)
                {
                    error = "ExecuteScript returned false";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                return false;
            }
        }

        internal static bool TryAttach(out string? error)
        {
            if (!EnsureInitialized(out error))
            {
                return false;
            }

            error = null;
            try
            {
                var api = GetSelectedApi();
                var typeName = api == SelectedApi.Xeno ? "SpashAPIXeno" : "SpashAPIVelocity";

                if (!TryInvokeApiMethod(typeName, "AttachAPI", Array.Empty<object>(), out var result, out error))
                {
                    return false;
                }

                if (!TryUnwrapTaskResult(result, out var final, out error))
                {
                    return false;
                }

                if (final is bool ok && !ok)
                {
                    error = "AttachAPI returned false";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                return false;
            }
        }

        internal static bool TryIsAttached(out bool result, out string? error)
        {
            result = false;

            if (!IsRobloxProcessRunning())
            {
                error = null;
                return true;
            }

            if (!EnsureInitialized(out error))
            {
                return false;
            }

            error = null;
            try
            {
                var api = GetSelectedApi();
                var typeName = api == SelectedApi.Xeno ? "SpashAPIXeno" : "SpashAPIVelocity";

                if (!TryInvokeApiMethod(typeName, "IsAttached", Array.Empty<object>(), out var obj, out error))
                {
                    return false;
                }

                if (!TryUnwrapTaskResult(obj, out var final, out error))
                {
                    return false;
                }

                if (final is bool b)
                {
                    result = b;
                    return true;
                }

                error = "Invalid result type";
                return false;
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                return false;
            }
        }

        internal static bool TryIsRobloxOpen(out bool result, out string? error)
        {
            result = false;

            if (!EnsureInitialized(out error))
            {
                return false;
            }

            error = null;
            try
            {
                var api = GetSelectedApi();
                var typeName = api == SelectedApi.Xeno ? "SpashAPIXeno" : "SpashAPIVelocity";

                if (!TryInvokeApiMethod(typeName, "IsRobloxOpen", Array.Empty<object>(), out var obj, out error))
                {
                    return false;
                }

                if (!TryUnwrapTaskResult(obj, out var final, out error))
                {
                    return false;
                }

                if (final is bool b)
                {
                    result = b;
                    return true;
                }

                error = "Invalid result type";
                return false;
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                return false;
            }
        }

        internal static bool TryKillRoblox(out string? error)
        {
            if (!EnsureInitialized(out error))
            {
                return false;
            }

            error = null;
            try
            {
                var api = GetSelectedApi();
                var typeName = api == SelectedApi.Xeno ? "SpashAPIXeno" : "SpashAPIVelocity";
                return TryInvokeApiMethod(typeName, "KillRoblox", Array.Empty<object>(), out error);
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                return false;
            }
        }

        private static bool TryInvokeApiMethod(string typeName, string methodName, object[] args, out string? error)
        {
            return TryInvokeApiMethod(typeName, methodName, args, out _, out error);
        }

        private static bool TryInvokeApiMethod(string typeName, string methodName, object[] args, out object? result, out string? error)
        {
            result = null;
            error = null;
            var apiTag = GetApiLogTag(GetSelectedApi());

            try
            {
                if (!_apiTypeCache.TryGetValue(typeName, out var apiType))
                {
                    apiType = null;
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            apiType = asm.GetType(typeName, false, true);
                            if (apiType != null)
                            {
                                _apiTypeCache[typeName] = apiType;
                                if (_apiTypeFoundLogged.TryAdd(typeName, 0))
                                {
                                    LogInfo(apiTag, $"Found type {typeName} in {asm.GetName().Name}");
                                }
                                break;
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                if (apiType == null)
                {
                    error = $"API type '{typeName}' not found.";
                    LogError(apiTag, error);
                    return false;
                }

                var methodCacheKey = (apiType.FullName ?? typeName) + "." + methodName;
                if (!_apiMethodCache.TryGetValue(methodCacheKey, out var method))
                {
                    method = apiType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        _apiMethodCache[methodCacheKey] = method;
                    }
                }

                if (method == null)
                {
                    error = $"Method '{methodName}' not found in {typeName}.";
                    LogError(apiTag, error);
                    return false;
                }

                string? prevDir = null;
                var sw = Stopwatch.StartNew();

                lock (ResolverLock)
                {
                    try
                    {
                        try { prevDir = Environment.CurrentDirectory; } catch { prevDir = null; }

                        try
                        {
                            if (!string.IsNullOrWhiteSpace(_resolverApiFolder) && Directory.Exists(_resolverApiFolder))
                            {
                                Environment.CurrentDirectory = _resolverApiFolder;
                            }
                        }
                        catch
                        {
                        }

                        result = method.Invoke(null, args);
                    }
                    finally
                    {
                        sw.Stop();
                        if (!string.IsNullOrWhiteSpace(prevDir))
                        {
                            try { Environment.CurrentDirectory = prevDir; } catch { }
                        }
                    }
                }

                WriteInvocationLog(apiTag, typeName, methodName, method.ReturnType.Name, result, sw.ElapsedMilliseconds);
                return true;
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                LogError(apiTag, $"Invoke error: {error}");
                return false;
            }
        }

        private static void WriteInvocationLog(string apiTag, string typeName, string methodName, string returnTypeName, object? result, long elapsedMs)
        {
            try
            {
                var formatted = FormatResultForLog(result);
                var key = typeName + "." + methodName;
                var line = $"{typeName}.{methodName} => {returnTypeName}: {formatted} ({elapsedMs}ms)";

                if (!string.Equals(methodName, "IsAttached", StringComparison.Ordinal))
                {
                    LogInfo(apiTag, line);
                    return;
                }

                var state = _invokeLogStates.GetOrAdd(key, _ => new InvocationLogState());
                lock (state)
                {
                    var now = DateTime.UtcNow.Ticks;
                    var changed = !string.Equals(state.LastValue, formatted, StringComparison.Ordinal);
                    var intervalOk = state.LastLogTicks == 0 || new TimeSpan(now - state.LastLogTicks) >= ChattyLogInterval;

                    if (changed || intervalOk)
                    {
                        state.LastLogTicks = now;
                        state.LastValue = formatted;
                        LogInfo(apiTag, line);
                    }
                }
            }
            catch
            {
            }
        }

        private static string FormatResultForLog(object? result)
        {
            if (result == null)
            {
                return "<null>";
            }

            try
            {
                if (result is string s)
                {
                    return s.Length <= 200 ? "\"" + s + "\"" : "\"" + s.Substring(0, 200) + "...\"";
                }

                if (result is bool b)
                {
                    return b ? "true" : "false";
                }

                var t = result.GetType();
                var text = string.Empty;
                try { text = result.ToString() ?? string.Empty; } catch { text = string.Empty; }
                if (text.Length > 200)
                {
                    text = text.Substring(0, 200) + "...";
                }
                if (text.Length == 0)
                {
                    return "<" + t.Name + ">";
                }
                return "<" + t.Name + "> " + text;
            }
            catch (Exception ex)
            {
                return $"<unprintable: {ex.Message}>";
            }
        }

        private static bool TryUnwrapTaskResult(object? value, out object? result, out string? error)
        {
            result = value;
            error = null;

            if (value is not Task task)
            {
                return true;
            }

            try
            {
                if (!task.Wait(TimeSpan.FromSeconds(12)))
                {
                    error = "Operation timed out.";
                    return false;
                }

                if (task.IsFaulted)
                {
                    var ex = task.Exception?.GetBaseException();
                    error = ex?.Message ?? "Task faulted.";
                    return false;
                }

                if (task.IsCanceled)
                {
                    error = "Operation canceled.";
                    return false;
                }

                var type = task.GetType();
                if (type.IsGenericType)
                {
                    var prop = type.GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
                    if (prop != null)
                    {
                        result = prop.GetValue(task);
                        return true;
                    }
                }

                result = null;
                return true;
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                return false;
            }
        }

        private static bool TryInterpretVelocityExecuteScriptResult(object? value, out string? error)
        {
            error = null;
            if (value == null)
            {
                return true;
            }

            if (value is bool ok)
            {
                if (!ok)
                {
                    error = "ExecuteScript returned false";
                    return false;
                }
                return true;
            }

            if (value is string)
            {
                return true;
            }

            try
            {
                var vt = value.GetType();
                if (vt.IsEnum)
                {
                    var name = value.ToString() ?? string.Empty;
                    if (name.Length == 0)
                    {
                        return true;
                    }

                    if (string.Equals(name, "Executed", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "Success", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "Succeeded", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(name, "Ok", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (name.Contains("fail", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("error", StringComparison.OrdinalIgnoreCase)
                        || name.Contains("denied", StringComparison.OrdinalIgnoreCase))
                    {
                        error = $"ExecuteScript returned {vt.Name}: {name}";
                        return false;
                    }

                    return true;
                }
            }
            catch
            {
            }

            try
            {
                object? invokeResult = null;

                static object?[] BuildDefaultArguments(ParameterInfo[] ps)
                {
                    if (ps.Length == 0)
                    {
                        return Array.Empty<object?>();
                    }

                    var args = new object?[ps.Length];
                    for (var i = 0; i < ps.Length; i++)
                    {
                        var p = ps[i];
                        if (p.GetCustomAttributes(typeof(ParamArrayAttribute), inherit: false).Any())
                        {
                            var et = p.ParameterType.IsArray ? p.ParameterType.GetElementType() : null;
                            args[i] = et == null ? Array.Empty<object>() : Array.CreateInstance(et, 0);
                            continue;
                        }

                        if (p.HasDefaultValue)
                        {
                            var dv = p.DefaultValue;
                            args[i] = dv == DBNull.Value ? null : dv;
                            continue;
                        }

                        var pt = p.ParameterType;
                        if (pt.IsValueType)
                        {
                            try { args[i] = Activator.CreateInstance(pt); } catch { args[i] = null; }
                        }
                        else
                        {
                            args[i] = null;
                        }
                    }

                    return args;
                }

                if (value is Delegate del)
                {
                    try
                    {
                        invokeResult = del.DynamicInvoke();
                    }
                    catch (TargetParameterCountException)
                    {
                        invokeResult = del.DynamicInvoke(BuildDefaultArguments(del.Method.GetParameters()));
                    }
                }
                else
                {
                    var t = value.GetType();

                    var common = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    {
                        "invoke",
                        "execute",
                        "run",
                        "call",
                        "fire",
                        "start",
                        "perform",
                        "dispatch",
                    };

                    var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                        .Where(m => !m.IsSpecialName)
                        .ToArray();

                    var method = methods
                        .Where(m => common.Contains(m.Name))
                        .OrderBy(m => m.GetParameters().Length)
                        .FirstOrDefault();

                    if (method == null)
                    {
                        Delegate? found = null;
                        try
                        {
                            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                                .Where(p => typeof(Delegate).IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0)
                                .ToArray();
                            if (props.Length == 1)
                            {
                                found = props[0].GetValue(value) as Delegate;
                            }

                            if (found == null)
                            {
                                var fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                    .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType))
                                    .ToArray();
                                if (fields.Length == 1)
                                {
                                    found = fields[0].GetValue(value) as Delegate;
                                }
                            }
                        }
                        catch
                        {
                        }

                        if (found != null)
                        {
                            try
                            {
                                invokeResult = found.DynamicInvoke();
                            }
                            catch (TargetParameterCountException)
                            {
                                invokeResult = found.DynamicInvoke(BuildDefaultArguments(found.Method.GetParameters()));
                            }
                        }
                        else
                        {
                            try
                            {
                                var apiTag = GetApiLogTag(GetSelectedApi());
                                var methodNames = t
                                    .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                                    .Where(m => !m.IsSpecialName)
                                    .Select(m => m.Name + "(" + m.GetParameters().Length + ")")
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                                    .Take(40)
                                    .ToArray();

                                LogError(apiTag, $"ExecuteScript returned {t.Name} but no invokable method was found. Public methods: {string.Join(", ", methodNames)}");
                            }
                            catch
                            {
                            }

                            error = $"ExecuteScript returned {t.Name} but no invokable method was found.";
                            return false;
                        }
                    }

                    if (method != null)
                    {
                        invokeResult = method.Invoke(value, BuildDefaultArguments(method.GetParameters()));
                    }
                }

                if (!TryUnwrapTaskResult(invokeResult, out var final, out error))
                {
                    return false;
                }

                if (final is bool finalOk && !finalOk)
                {
                    error = "ExecuteScript invoke returned false";
                    return false;
                }

                return true;
            }
            catch (TargetInvocationException tie)
            {
                error = FormatExceptionMessage(tie.GetBaseException() ?? tie);
                return false;
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                return false;
            }
        }

        private static bool TryLoadSelectedApiAssembly(string? selectedApi, out Assembly? assembly, out string? dllPath, out string? error)
        {
            assembly = null;
            dllPath = null;
            error = null;

            var apiTag = GetApiLogTagFromSelectedString(selectedApi);
            var apiName = (selectedApi ?? string.Empty).Trim();
            if (apiName.Length == 0)
            {
                apiName = "Velocity";
            }

            try
            {
                var baseDir = AppPaths.AppDirectory;
                var apisBaseDir = Path.Combine(baseDir, "APIs");

                LogInfo(apiTag, $"Searching for API: {apiName}");
                LogInfo(apiTag, $"APIs base dir: {apisBaseDir}");

                if (!Directory.Exists(apisBaseDir))
                {
                    error = $"APIs folder not found: {apisBaseDir}";
                    LogError(apiTag, error);
                    return false;
                }

                var folderCandidates = new List<string>();
                folderCandidates.Add(Path.Combine(apisBaseDir, apiName));
                if (!apiName.StartsWith("SpashAPI", StringComparison.OrdinalIgnoreCase))
                {
                    folderCandidates.Add(Path.Combine(apisBaseDir, $"SpashAPI - {apiName}"));
                }

                try
                {
                    foreach (var dir in Directory.GetDirectories(apisBaseDir))
                    {
                        var dirName = Path.GetFileName(dir);
                        if (dirName.Contains(apiName, StringComparison.OrdinalIgnoreCase) || apiName.Contains(dirName, StringComparison.OrdinalIgnoreCase))
                        {
                            folderCandidates.Add(dir);
                        }
                    }
                }
                catch
                {
                }

                string? apiFolder = null;
                foreach (var candidate in folderCandidates.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    LogInfo(apiTag, $"Checking folder: {candidate}");
                    if (Directory.Exists(candidate))
                    {
                        apiFolder = candidate;
                        LogInfo(apiTag, $"Found API folder: {apiFolder}");
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(apiFolder))
                {
                    error = $"API folder not found for '{apiName}'.";
                    LogError(apiTag, error);
                    return false;
                }

                var candidates = BuildApiTypeCandidates(selectedApi);
                LogInfo(apiTag, $"Looking for DLL matching: {string.Join(", ", candidates)}");

                foreach (var file in Directory.EnumerateFiles(apiFolder, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    var stem = Path.GetFileNameWithoutExtension(file);
                    if (candidates.Any(c => string.Equals(stem, c, StringComparison.OrdinalIgnoreCase)))
                    {
                        dllPath = file;
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(dllPath))
                {
                    dllPath = Directory.EnumerateFiles(apiFolder, "SpashAPI*.dll", SearchOption.TopDirectoryOnly).FirstOrDefault();
                }

                if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                {
                    error = $"No SpashAPI DLL found in: {apiFolder}";
                    LogError(apiTag, error);
                    return false;
                }

                EnsureDependencyResolverInstalled(apiFolder);
                EnsureNativeDependencySearchPathInstalled(apiFolder);

                var expectedName = string.Empty;
                try { expectedName = Path.GetFileNameWithoutExtension(dllPath) ?? string.Empty; } catch { expectedName = string.Empty; }

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(asm.Location) && string.Equals(Path.GetFullPath(asm.Location), Path.GetFullPath(dllPath), StringComparison.OrdinalIgnoreCase))
                        {
                            assembly = asm;
                            return true;
                        }
                    }
                    catch
                    {
                    }

                    try
                    {
                        if (string.IsNullOrWhiteSpace(asm.Location) && !string.IsNullOrWhiteSpace(expectedName))
                        {
                            var n = asm.GetName().Name ?? string.Empty;
                            if (string.Equals(n, expectedName, StringComparison.OrdinalIgnoreCase))
                            {
                                assembly = asm;
                                return true;
                            }
                        }
                    }
                    catch
                    {
                    }
                }

                LogInfo(apiTag, $"Loading assembly from: {dllPath}");
                assembly = Assembly.LoadFrom(dllPath);
                LogInfo(apiTag, $"Successfully loaded assembly: {assembly.GetName().Name}");
                return true;
            }
            catch (Exception ex)
            {
                error = FormatExceptionMessage(ex);
                LogError(apiTag, $"Load error: {error}");
                return false;
            }
        }

        private static void EnsureDependencyResolverInstalled(string apiFolder)
        {
            lock (ResolverLock)
            {
                _resolverApiFolder = apiFolder;
                if (_resolverInstalled)
                {
                    return;
                }

                _resolverInstalled = true;
                LogInfo(GetApiLogTag(GetSelectedApi()), $"Installing dependency resolver for: {apiFolder}");

                AppDomain.CurrentDomain.AssemblyResolve += (_, args) =>
                {
                    try
                    {
                        var name = new AssemblyName(args.Name).Name ?? string.Empty;
                        if (name.Length == 0)
                        {
                            return null;
                        }

                        if (_resolveLookupLogged.TryAdd(name, 0))
                        {
                            LogInfo(GetApiLogTag(GetSelectedApi()), $"AssemblyResolve looking for: {name}");
                        }

                        var fileName = name + ".dll";
                        var candidates = new[]
                        {
                            apiFolder,
                            Path.Combine(apiFolder, "Bin"),
                            Path.Combine(apiFolder, "bin"),
                        };

                        foreach (var dir in candidates)
                        {
                            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                            {
                                continue;
                            }

                            var p = Path.Combine(dir, fileName);
                            if (File.Exists(p))
                            {
                                return Assembly.LoadFrom(p);
                            }
                        }

                        if (_resolveNotFoundLogged.TryAdd(name, 0))
                        {
                            LogWarn(GetApiLogTag(GetSelectedApi()), $"AssemblyResolve not found: {name}");
                        }
                        return null;
                    }
                    catch (Exception ex)
                    {
                        LogWarn(GetApiLogTag(GetSelectedApi()), $"AssemblyResolve error: {ex.Message}");
                        return null;
                    }
                };
            }
        }

        private static void EnsureNativeDependencySearchPathInstalled(string apiFolder)
        {
            lock (ResolverLock)
            {
                try
                {
                    var candidates = new[]
                    {
                        apiFolder,
                        Path.Combine(apiFolder, "Bin"),
                        Path.Combine(apiFolder, "bin"),
                    };

                    SetDefaultDllDirectoriesSafe();
                    foreach (var dir in candidates)
                    {
                        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                        {
                            continue;
                        }

                        var cookie = AddDllDirectorySafe(dir);
                        if (cookie != IntPtr.Zero)
                        {
                            _nativeSearchCookies.Add(cookie);
                        }
                    }

                    if (_nativeSearchCookies.Count == 0)
                    {
                        var bin = candidates.FirstOrDefault(Directory.Exists);
                        if (!string.IsNullOrWhiteSpace(bin))
                        {
                            SetDllDirectorySafe(bin);
                        }
                    }
                }
                catch
                {
                }
            }
        }

        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint directoryFlags);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr AddDllDirectory(string newDirectory);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SetDllDirectory(string? lpPathName);

        private static void SetDefaultDllDirectoriesSafe()
        {
            try { SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS); } catch { }
        }

        private static IntPtr AddDllDirectorySafe(string dir)
        {
            try { return AddDllDirectory(dir); } catch { return IntPtr.Zero; }
        }

        private static void SetDllDirectorySafe(string dir)
        {
            try { SetDllDirectory(dir); } catch { }
        }

        private static string[] BuildApiTypeCandidates(string? selectedApi)
        {
            var list = new List<string>();
            var raw = (selectedApi ?? string.Empty).Trim();
            if (raw.Length > 0)
            {
                var namePart = raw;
                if (namePart.StartsWith("SpashAPI", StringComparison.OrdinalIgnoreCase))
                {
                    namePart = namePart.Substring(8).Trim();
                    if (namePart.StartsWith("-"))
                    {
                        namePart = namePart.Substring(1).Trim();
                    }
                }

                var dashIdx = raw.LastIndexOf('-');
                if (dashIdx >= 0 && dashIdx + 1 < raw.Length)
                {
                    var afterDash = raw[(dashIdx + 1)..].Trim();
                    if (!string.IsNullOrWhiteSpace(afterDash))
                    {
                        namePart = afterDash;
                    }
                }

                var collapsed = new string(namePart.Where(c => char.IsLetterOrDigit(c)).ToArray());
                if (collapsed.Length > 0)
                {
                    list.Add("SpashAPI" + collapsed);
                    list.Add(collapsed);
                }
            }

            list.Add("SpashAPIVelocity");
            list.Add("SpashAPIXeno");

            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string FormatExceptionMessage(Exception ex)
        {
            try
            {
                Exception root = ex;
                if (root is TargetInvocationException tie && tie.InnerException != null)
                {
                    root = tie.InnerException;
                }
                var baseEx = root.GetBaseException();
                if (baseEx != null)
                {
                    root = baseEx;
                }

                return root.GetType().Name + ": " + root.Message;
            }
            catch
            {
                return ex.Message;
            }
        }
    }
}