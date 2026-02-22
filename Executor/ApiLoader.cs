using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace Executor
{
    /// <summary>
    /// API 載入器，負責掃描並動態載入 API DLL
    /// </summary>
    public static class ApiLoader
    {
        private static string GetVersionJsonPath()
        {
            var preferred = Path.GetFullPath(Path.Combine(AppPaths.AppDirectory, "..", "version.json"));
            if (File.Exists(preferred))
            {
                return preferred;
            }

            var appLocal = Path.Combine(AppPaths.AppDirectory, "version.json");
            if (File.Exists(appLocal))
            {
                return appLocal;
            }

            return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "version.json"));
        }

        /// <summary>
        /// 從 version.json 取得 API 清單並載入對應的 DLL
        /// </summary>
        /// <returns>載入成功的 API 清單</returns>
        public static Dictionary<string, Assembly> LoadApisFromVersionJson()
        {
            var loadedApis = new Dictionary<string, Assembly>();
            
            try
            {
                var versionPath = GetVersionJsonPath();
                if (!File.Exists(versionPath))
                {
                    return loadedApis;
                }

                var json = File.ReadAllText(versionPath);
                using var doc = JsonDocument.Parse(json);
                
                if (!doc.RootElement.TryGetProperty("APIs", out var apisElement) || apisElement.ValueKind != JsonValueKind.Object)
                {
                    return loadedApis;
                }

                foreach (var apiProp in apisElement.EnumerateObject())
                {
                    var apiName = apiProp.Name;
                    var assembly = LoadApiFromFolder(apiName);
                    
                    if (assembly != null)
                    {
                        loadedApis[apiName] = assembly;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading APIs from version.json: {ex.Message}");
            }

            return loadedApis;
        }

        /// <summary>
        /// 從指定 API 資料夾載入 DLL
        /// </summary>
        /// <param name="apiName">API 名稱</param>
        /// <returns>載入的 Assembly，失敗返回 null</returns>
        public static Assembly? LoadApiFromFolder(string apiName)
        {
            try
            {
                var apiFolder = Path.Combine(AppPaths.AppDirectory, "APIs", apiName);
                if (!Directory.Exists(apiFolder))
                {
                    System.Diagnostics.Debug.WriteLine($"API folder not found: {apiFolder}");
                    return null;
                }

                // 搜尋資料夾中的 DLL 檔案
                var dllFiles = Directory.GetFiles(apiFolder, "*.dll", SearchOption.TopDirectoryOnly);
                if (dllFiles.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"No DLL files found in: {apiFolder}");
                    return null;
                }

                // 優先挑選可能是主 API 的 DLL（避免誤載依賴 DLL）
                var dllPath = ChooseApiDllPath(apiName, apiFolder, dllFiles);
                if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
                {
                    System.Diagnostics.Debug.WriteLine($"No suitable API DLL found in: {apiFolder}");
                    return null;
                }

                // 安裝依賴/Native DLL 解析（與 SpashApiInvoker 同策略）
                try
                {
                    SpashApiInvoker.EnsureDependencyResolverInstalled(apiFolder);
                    SpashApiInvoker.EnsureNativeDependencySearchPathInstalled(apiFolder);
                }
                catch
                {
                    // 忽略，讓後續 Assembly.LoadFrom 自己拋錯並由外層處理
                }

                var assembly = Assembly.LoadFrom(dllPath);
                
                System.Diagnostics.Debug.WriteLine($"Successfully loaded API: {apiName} from {dllPath}");
                return assembly;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load API {apiName}: {ex.Message}");
                return null;
            }
        }

        private static string? ChooseApiDllPath(string apiName, string apiFolder, string[] dllFiles)
        {
            // 常見情況：資料夾內包含多個依賴 DLL，主 DLL 通常以 SpashAPI* 命名
            try
            {
                var ordered = dllFiles
                    .Select(p => new
                    {
                        Path = p,
                        Name = System.IO.Path.GetFileNameWithoutExtension(p) ?? string.Empty,
                    })
                    .OrderBy(x => x.Name.Length)
                    .ToList();

                string? Pick(Func<string, bool> predicate)
                {
                    foreach (var item in ordered)
                    {
                        if (predicate(item.Name))
                        {
                            return item.Path;
                        }
                    }
                    return null;
                }

                var api = (apiName ?? string.Empty).Trim();

                // 1) 精準匹配資料夾名
                var exact = Pick(n => string.Equals(n, api, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(exact)) return exact;

                // 2) SpashAPI 開頭
                var spash = Pick(n => n.StartsWith("SpashAPI", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(spash)) return spash;

                // 3) 包含 apiName
                var contains = Pick(n => n.Contains(api, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(contains)) return contains;

                // 4) 退而求其次：選最短檔名（較像主 DLL）
                return ordered.FirstOrDefault()?.Path;
            }
            catch
            {
                return dllFiles.FirstOrDefault();
            }
        }

        /// <summary>
        /// 取得 API 資料夾中的 DLL 檔案路徑
        /// </summary>
        /// <param name="apiName">API 名稱</param>
        /// <returns>DLL 檔案路徑，找不到返回 null</returns>
        public static string? GetApiDllPath(string apiName)
        {
            try
            {
                var apiFolder = Path.Combine(AppPaths.AppDirectory, "APIs", apiName);
                if (!Directory.Exists(apiFolder))
                {
                    return null;
                }

                var dllFiles = Directory.GetFiles(apiFolder, "*.dll", SearchOption.TopDirectoryOnly);
                if (dllFiles.Length == 0)
                {
                    return null;
                }

                return ChooseApiDllPath(apiName, apiFolder, dllFiles);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 檢查 API 是否存在且可載入
        /// </summary>
        /// <param name="apiName">API 名稱</param>
        /// <returns>是否存在</returns>
        public static bool IsApiAvailable(string apiName)
        {
            var dllPath = GetApiDllPath(apiName);
            return !string.IsNullOrEmpty(dllPath) && File.Exists(dllPath);
        }
    }
}
