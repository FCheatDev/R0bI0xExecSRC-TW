using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Executor
{
    /// <summary>
    /// 動態 API 管理器，負責管理載入的 API 實例
    /// </summary>
    public static class DynamicApiManager
    {
        private static readonly Dictionary<string, Assembly> _loadedAssemblies = new();
        private static readonly Dictionary<string, object> _apiInstances = new();
        private static string? _currentApiName;

        /// <summary>
        /// 初始化並載入所有可用的 API
        /// </summary>
        public static void Initialize()
        {
            _loadedAssemblies.Clear();
            _apiInstances.Clear();
            
            var loadedApis = ApiLoader.LoadApisFromVersionJson();
            foreach (var kvp in loadedApis)
            {
                _loadedAssemblies[kvp.Key] = kvp.Value;
            }
        }

        /// <summary>
        /// 設定當前使用的 API
        /// </summary>
        /// <param name="apiName">API 名稱</param>
        /// <returns>是否設定成功</returns>
        public static bool SetCurrentApi(string apiName)
        {
            if (!_loadedAssemblies.ContainsKey(apiName))
            {
                System.Diagnostics.Debug.WriteLine($"API not loaded: {apiName}");
                return false;
            }

            try
            {
                // 如果已有實例，先清理
                if (!string.IsNullOrWhiteSpace(_currentApiName) && _apiInstances.ContainsKey(_currentApiName))
                {
                    CleanupApiInstance(_currentApiName);
                }

                // 創建新的 API 實例
                var assembly = _loadedAssemblies[apiName];
                var apiInstance = CreateApiInstance(assembly);
                
                if (apiInstance != null)
                {
                    _apiInstances[apiName] = apiInstance;
                    _currentApiName = apiName;
                    
                    System.Diagnostics.Debug.WriteLine($"Successfully set current API: {apiName}");
                    return true;
                }

                // 有些第三方 API 只提供 static 類（無法 Activator.CreateInstance）。
                // 這種情況仍允許切換成功：由呼叫端走反射 fallback（SpashApiInvoker 的原始反射路徑）。
                _currentApiName = apiName;
                System.Diagnostics.Debug.WriteLine($"Set current API without instance (static-only API?): {apiName}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set current API {apiName}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 取得當前 API 實例
        /// </summary>
        /// <returns>當前 API 實例，失敗返回 null</returns>
        public static object? GetCurrentApiInstance()
        {
            if (string.IsNullOrEmpty(_currentApiName) || !_apiInstances.ContainsKey(_currentApiName))
            {
                return null;
            }

            return _apiInstances[_currentApiName];
        }

        /// <summary>
        /// 取得當前 API 名稱
        /// </summary>
        /// <returns>當前 API 名稱</returns>
        public static string? GetCurrentApiName()
        {
            return _currentApiName;
        }

        /// <summary>
        /// 取得所有已載入的 API 名稱
        /// </summary>
        /// <returns>API 名稱清單</returns>
        public static string[] GetLoadedApiNames()
        {
            return _loadedAssemblies.Keys.ToArray();
        }

        /// <summary>
        /// 檢查 API 是否已載入
        /// </summary>
        /// <param name="apiName">API 名稱</param>
        /// <returns>是否已載入</returns>
        public static bool IsApiLoaded(string apiName)
        {
            return _loadedAssemblies.ContainsKey(apiName);
        }

        /// <summary>
        /// 重新載入指定 API
        /// </summary>
        /// <param name="apiName">API 名稱</param>
        /// <returns>是否重新載入成功</returns>
        public static bool ReloadApi(string apiName)
        {
            try
            {
                // 清理舊實例
                if (_apiInstances.ContainsKey(apiName))
                {
                    CleanupApiInstance(apiName);
                }

                // 重新載入 Assembly
                var assembly = ApiLoader.LoadApiFromFolder(apiName);
                if (assembly != null)
                {
                    _loadedAssemblies[apiName] = assembly;
                    
                    // 如果是當前 API，重新創建實例
                    if (_currentApiName == apiName)
                    {
                        var apiInstance = CreateApiInstance(assembly);
                        if (apiInstance != null)
                        {
                            _apiInstances[apiName] = apiInstance;
                        }
                    }
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to reload API {apiName}: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 清理所有 API 實例
        /// </summary>
        public static void Cleanup()
        {
            foreach (var apiName in _apiInstances.Keys.ToList())
            {
                CleanupApiInstance(apiName);
            }
            
            _apiInstances.Clear();
            _loadedAssemblies.Clear();
            _currentApiName = null;
        }

        /// <summary>
        /// 創建 API 實例
        /// </summary>
        /// <param name="assembly">API Assembly</param>
        /// <returns>API 實例，失敗返回 null</returns>
        private static object? CreateApiInstance(Assembly assembly)
        {
            try
            {
                // 嘗試找到符合條件的類型
                var types = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract)
                    .Where(t => t.Name.Contains("API") || t.Name.Contains("Spash"))
                    .ToArray();

                foreach (var type in types)
                {
                    try
                    {
                        var instance = Activator.CreateInstance(type);
                        if (instance != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Created API instance: {type.FullName}");
                            return instance;
                        }
                    }
                    catch
                    {
                        // 繼續嘗試下一個類型
                        continue;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"No suitable API type found in assembly");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create API instance: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 清理 API 實例
        /// </summary>
        /// <param name="apiName">API 名稱</param>
        private static void CleanupApiInstance(string apiName)
        {
            if (_apiInstances.ContainsKey(apiName))
            {
                try
                {
                    var instance = _apiInstances[apiName];
                    
                    // 如果實例有實作 IDisposable，呼叫 Dispose
                    if (instance is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error cleaning up API instance {apiName}: {ex.Message}");
                }
                finally
                {
                    _apiInstances.Remove(apiName);
                }
            }
        }
    }
}
