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
        /// <summary>
        /// 從 version.json 取得 API 清單並載入對應的 DLL
        /// </summary>
        /// <returns>載入成功的 API 清單</returns>
        public static Dictionary<string, Assembly> LoadApisFromVersionJson()
        {
            var loadedApis = new Dictionary<string, Assembly>();
            
            try
            {
                var versionPath = Path.Combine(AppPaths.AppDirectory, "version.json");
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
                var dllFiles = Directory.GetFiles(apiFolder, "*.dll");
                if (dllFiles.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"No DLL files found in: {apiFolder}");
                    return null;
                }

                // 嘗試載入第一個找到的 DLL
                var dllPath = dllFiles[0];
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

                var dllFiles = Directory.GetFiles(apiFolder, "*.dll");
                return dllFiles.Length > 0 ? dllFiles[0] : null;
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
