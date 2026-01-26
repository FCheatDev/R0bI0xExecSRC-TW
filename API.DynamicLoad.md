# WPF .NET 10 動態載入外部 API 完整開發指南

## 📋 目錄

1. [專案概述](#專案概述)
2. [環境要求](#環境要求)
3. [架構設計](#架構設計)
4. [實作步驟](#實作步驟)
5. [完整程式碼範例](#完整程式碼範例)
6. [進階功能](#進階功能)
7. [最佳實踐](#最佳實踐)
8. [常見問題](#常見問題)

---

## 專案概述

本指南將教你如何在 .NET 10 的 WPF 應用程式中，實現動態載入外部 API (DLL)，無需在專案中添加參考，直接從指定路徑引用並使用。

### 核心優勢

- ✅ 不需要重新編譯主程式即可更新 API
- ✅ 支援插件式架構
- ✅ 降低主程式與 API 的耦合度
- ✅ 可動態卸載和重載 API
- ✅ 支援多版本 API 共存

---

## 環境要求

### 必要條件

- .NET 10 SDK
- Visual Studio 2022 (17.8 或更高版本)
- Windows 10/11

### 建議安裝

- NuGet Package Manager
- Git (版本控制)

---

## 架構設計

### 專案結構建議

```
Solution Root/
├── MyWpfApp/                          # WPF 主程式
│   ├── Services/
│   │   ├── ApiLoader.cs               # API 載入器
│   │   └── PluginManager.cs           # 插件管理器
│   ├── Models/
│   │   └── PluginConfig.cs            # 插件配置模型
│   ├── MainWindow.xaml
│   └── App.xaml
│
├── ApiContracts/                      # 介面契約專案
│   ├── IApiService.cs                 # 服務介面定義
│   ├── IPlugin.cs                     # 插件介面定義
│   └── DataModels/                    # 共用資料模型
│
├── ExternalApi/                       # 外部 API 實作專案
│   ├── Services/
│   │   └── MyApiService.cs            # API 實作
│   └── Plugin.cs                      # 插件進入點
│
└── PluginsOutput/                     # 編譯後的插件目錄
    ├── Plugin1/
    │   ├── ExternalApi.dll
    │   └── dependencies...
    └── Plugin2/
        └── ...
```

---

## 實作步驟

### 步驟 1: 建立介面契約專案

建立一個 .NET 10 類別庫專案 `ApiContracts`

#### IApiService.cs - 基礎服務介面

```csharp
namespace ApiContracts
{
    /// <summary>
    /// API 服務基礎介面
    /// </summary>
    public interface IApiService
    {
        /// <summary>
        /// 服務名稱
        /// </summary>
        string ServiceName { get; }

        /// <summary>
        /// 服務版本
        /// </summary>
        string Version { get; }

        /// <summary>
        /// 初始化服務
        /// </summary>
        /// <returns>初始化是否成功</returns>
        bool Initialize();

        /// <summary>
        /// 執行服務
        /// </summary>
        /// <param name="parameters">參數字典</param>
        /// <returns>執行結果</returns>
        object Execute(Dictionary<string, object> parameters);

        /// <summary>
        /// 釋放資源
        /// </summary>
        void Dispose();
    }
}
```

#### IPlugin.cs - 插件介面

```csharp
namespace ApiContracts
{
    /// <summary>
    /// 插件介面定義
    /// </summary>
    public interface IPlugin
    {
        /// <summary>
        /// 插件唯一識別碼
        /// </summary>
        Guid PluginId { get; }

        /// <summary>
        /// 插件名稱
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 插件描述
        /// </summary>
        string Description { get; }

        /// <summary>
        /// 插件作者
        /// </summary>
        string Author { get; }

        /// <summary>
        /// 插件版本
        /// </summary>
        Version PluginVersion { get; }

        /// <summary>
        /// 獲取插件提供的服務
        /// </summary>
        /// <returns>服務列表</returns>
        IEnumerable<IApiService> GetServices();

        /// <summary>
        /// 插件載入時觸發
        /// </summary>
        void OnLoad();

        /// <summary>
        /// 插件卸載時觸發
        /// </summary>
        void OnUnload();
    }
}
```

#### PluginMetadata.cs - 插件元數據

```csharp
namespace ApiContracts
{
    /// <summary>
    /// 插件元數據
    /// </summary>
    public class PluginMetadata
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Version { get; set; }
        public DateTime CreatedDate { get; set; }
        public List<string> Dependencies { get; set; } = new();
        public Dictionary<string, string> CustomProperties { get; set; } = new();
    }
}
```

---

### 步驟 2: 建立 WPF 主程式 - API 載入器

#### ApiLoader.cs - 基礎載入器

```csharp
using System;
using System.IO;
using System.Reflection;
using ApiContracts;

namespace MyWpfApp.Services
{
    /// <summary>
    /// API 動態載入器 - 基礎版本
    /// </summary>
    public class ApiLoader
    {
        /// <summary>
        /// 載入 DLL 並建立指定型別的實例
        /// </summary>
        /// <typeparam name="T">目標介面型別</typeparam>
        /// <param name="dllPath">DLL 完整路徑</param>
        /// <param name="typeName">完整的型別名稱 (包含命名空間)</param>
        /// <returns>型別實例</returns>
        public T LoadApi<T>(string dllPath, string typeName) where T : class
        {
            try
            {
                // 驗證檔案是否存在
                if (!File.Exists(dllPath))
                {
                    throw new FileNotFoundException($"找不到 DLL 檔案: {dllPath}");
                }

                // 載入組件
                Assembly assembly = Assembly.LoadFrom(dllPath);

                // 取得指定型別
                Type type = assembly.GetType(typeName);
                if (type == null)
                {
                    throw new TypeLoadException($"無法在組件中找到型別: {typeName}");
                }

                // 驗證型別是否實作目標介面
                if (!typeof(T).IsAssignableFrom(type))
                {
                    throw new InvalidCastException($"型別 {typeName} 未實作介面 {typeof(T).Name}");
                }

                // 建立實例
                object instance = Activator.CreateInstance(type);
                return instance as T;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"載入 API 失敗: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 動態調用方法
        /// </summary>
        /// <param name="instance">物件實例</param>
        /// <param name="methodName">方法名稱</param>
        /// <param name="parameters">參數陣列</param>
        /// <returns>方法回傳值</returns>
        public object InvokeMethod(object instance, string methodName, object[] parameters = null)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();
            MethodInfo method = type.GetMethod(methodName);

            if (method == null)
            {
                throw new MissingMethodException($"找不到方法: {methodName}");
            }

            return method.Invoke(instance, parameters);
        }

        /// <summary>
        /// 獲取屬性值
        /// </summary>
        public object GetPropertyValue(object instance, string propertyName)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(propertyName);

            if (property == null)
            {
                throw new MissingMemberException($"找不到屬性: {propertyName}");
            }

            return property.GetValue(instance);
        }

        /// <summary>
        /// 設定屬性值
        /// </summary>
        public void SetPropertyValue(object instance, string propertyName, object value)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(propertyName);

            if (property == null)
            {
                throw new MissingMemberException($"找不到屬性: {propertyName}");
            }

            property.SetValue(instance, value);
        }
    }
}
```

#### AdvancedApiLoader.cs - 進階載入器 (支援卸載)

```csharp
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using ApiContracts;

namespace MyWpfApp.Services
{
    /// <summary>
    /// 自訂組件載入上下文 - 支援卸載
    /// </summary>
    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            // 解析依賴項路徑
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 進階 API 載入器 - 支援卸載和隔離
    /// </summary>
    public class AdvancedApiLoader : IDisposable
    {
        private PluginLoadContext _loadContext;
        private WeakReference _instanceReference;

        /// <summary>
        /// 載入插件
        /// </summary>
        public T LoadPlugin<T>(string dllPath, string typeName) where T : class
        {
            try
            {
                if (!File.Exists(dllPath))
                {
                    throw new FileNotFoundException($"找不到 DLL: {dllPath}");
                }

                // 建立新的載入上下文
                _loadContext = new PluginLoadContext(dllPath);

                // 載入組件
                Assembly assembly = _loadContext.LoadFromAssemblyPath(dllPath);

                // 取得型別並建立實例
                Type type = assembly.GetType(typeName);
                if (type == null)
                {
                    throw new TypeLoadException($"找不到型別: {typeName}");
                }

                object instance = Activator.CreateInstance(type);
                _instanceReference = new WeakReference(instance);

                return instance as T;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"載入插件失敗: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 卸載插件
        /// </summary>
        public void Unload()
        {
            if (_loadContext != null)
            {
                _loadContext.Unload();
                _loadContext = null;
            }

            // 強制垃圾回收以完全卸載
            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        /// <summary>
        /// 檢查插件是否已卸載
        /// </summary>
        public bool IsUnloaded()
        {
            return _instanceReference != null && !_instanceReference.IsAlive;
        }

        public void Dispose()
        {
            Unload();
        }
    }
}
```

#### PluginManager.cs - 插件管理器

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ApiContracts;

namespace MyWpfApp.Services
{
    /// <summary>
    /// 插件管理器 - 管理多個插件的載入、卸載和生命週期
    /// </summary>
    public class PluginManager
    {
        private readonly Dictionary<Guid, PluginContainer> _loadedPlugins;
        private readonly string _pluginDirectory;

        public PluginManager(string pluginDirectory)
        {
            _pluginDirectory = pluginDirectory;
            _loadedPlugins = new Dictionary<Guid, PluginContainer>();
        }

        /// <summary>
        /// 掃描並載入所有插件
        /// </summary>
        public void LoadAllPlugins()
        {
            if (!Directory.Exists(_pluginDirectory))
            {
                Directory.CreateDirectory(_pluginDirectory);
                return;
            }

            // 掃描所有子目錄
            foreach (var directory in Directory.GetDirectories(_pluginDirectory))
            {
                LoadPluginFromDirectory(directory);
            }
        }

        /// <summary>
        /// 從指定目錄載入插件
        /// </summary>
        private void LoadPluginFromDirectory(string directory)
        {
            try
            {
                // 尋找 plugin.json 配置檔
                string configPath = Path.Combine(directory, "plugin.json");
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"目錄 {directory} 中找不到 plugin.json");
                    return;
                }

                // 讀取配置
                var config = System.Text.Json.JsonSerializer.Deserialize<PluginConfig>(
                    File.ReadAllText(configPath)
                );

                string dllPath = Path.Combine(directory, config.DllName);

                // 使用進階載入器
                var loader = new AdvancedApiLoader();
                var plugin = loader.LoadPlugin<IPlugin>(dllPath, config.TypeName);

                if (plugin != null)
                {
                    var container = new PluginContainer
                    {
                        Plugin = plugin,
                        Loader = loader,
                        Config = config,
                        LoadPath = dllPath
                    };

                    _loadedPlugins[plugin.PluginId] = container;
                    plugin.OnLoad();

                    Console.WriteLine($"成功載入插件: {plugin.Name} v{plugin.PluginVersion}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"載入插件失敗 ({directory}): {ex.Message}");
            }
        }

        /// <summary>
        /// 載入單一插件
        /// </summary>
        public IPlugin LoadPlugin(string dllPath, string typeName)
        {
            var loader = new AdvancedApiLoader();
            var plugin = loader.LoadPlugin<IPlugin>(dllPath, typeName);

            if (plugin != null)
            {
                var container = new PluginContainer
                {
                    Plugin = plugin,
                    Loader = loader,
                    LoadPath = dllPath
                };

                _loadedPlugins[plugin.PluginId] = container;
                plugin.OnLoad();
            }

            return plugin;
        }

        /// <summary>
        /// 卸載插件
        /// </summary>
        public void UnloadPlugin(Guid pluginId)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var container))
            {
                container.Plugin.OnUnload();
                container.Loader.Unload();
                _loadedPlugins.Remove(pluginId);

                Console.WriteLine($"已卸載插件: {container.Plugin.Name}");
            }
        }

        /// <summary>
        /// 重新載入插件
        /// </summary>
        public void ReloadPlugin(Guid pluginId)
        {
            if (_loadedPlugins.TryGetValue(pluginId, out var container))
            {
                string path = container.LoadPath;
                string typeName = container.Plugin.GetType().FullName;

                UnloadPlugin(pluginId);

                // 等待卸載完成
                System.Threading.Thread.Sleep(100);

                LoadPlugin(path, typeName);
            }
        }

        /// <summary>
        /// 獲取所有已載入的插件
        /// </summary>
        public IEnumerable<IPlugin> GetAllPlugins()
        {
            return _loadedPlugins.Values.Select(c => c.Plugin);
        }

        /// <summary>
        /// 根據 ID 獲取插件
        /// </summary>
        public IPlugin GetPlugin(Guid pluginId)
        {
            return _loadedPlugins.TryGetValue(pluginId, out var container)
                ? container.Plugin
                : null;
        }

        /// <summary>
        /// 獲取插件提供的服務
        /// </summary>
        public IEnumerable<IApiService> GetServices(Guid pluginId)
        {
            var plugin = GetPlugin(pluginId);
            return plugin?.GetServices() ?? Enumerable.Empty<IApiService>();
        }

        /// <summary>
        /// 卸載所有插件
        /// </summary>
        public void UnloadAll()
        {
            foreach (var pluginId in _loadedPlugins.Keys.ToList())
            {
                UnloadPlugin(pluginId);
            }
        }

        /// <summary>
        /// 插件容器 - 儲存插件相關資訊
        /// </summary>
        private class PluginContainer
        {
            public IPlugin Plugin { get; set; }
            public AdvancedApiLoader Loader { get; set; }
            public PluginConfig Config { get; set; }
            public string LoadPath { get; set; }
        }
    }

    /// <summary>
    /// 插件配置類別
    /// </summary>
    public class PluginConfig
    {
        public string Name { get; set; }
        public string DllName { get; set; }
        public string TypeName { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public bool AutoLoad { get; set; }
        public List<string> Dependencies { get; set; }
    }
}
```

---

### 步驟 3: 建立外部 API 專案

建立一個新的 .NET 10 類別庫專案 `ExternalApi`，並參考 `ApiContracts` 專案。

#### MyDataService.cs - 資料服務實作

```csharp
using System;
using System.Collections.Generic;
using ApiContracts;

namespace ExternalApi.Services
{
    /// <summary>
    /// 資料處理服務實作
    /// </summary>
    public class MyDataService : IApiService
    {
        private bool _isInitialized;

        public string ServiceName => "資料處理服務";
        public string Version => "1.0.0";

        public bool Initialize()
        {
            try
            {
                // 執行初始化邏輯
                Console.WriteLine($"{ServiceName} 正在初始化...");

                // 這裡可以進行資料庫連接、配置載入等操作

                _isInitialized = true;
                Console.WriteLine($"{ServiceName} 初始化完成");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"初始化失敗: {ex.Message}");
                return false;
            }
        }

        public object Execute(Dictionary<string, object> parameters)
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("服務尚未初始化");
            }

            // 解析參數
            if (!parameters.TryGetValue("action", out var actionObj))
            {
                throw new ArgumentException("缺少 'action' 參數");
            }

            string action = actionObj.ToString();

            // 根據不同的動作執行不同的邏輯
            return action switch
            {
                "process" => ProcessData(parameters),
                "calculate" => Calculate(parameters),
                "query" => QueryData(parameters),
                _ => throw new NotSupportedException($"不支援的動作: {action}")
            };
        }

        private object ProcessData(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("data", out var data))
            {
                string input = data.ToString();
                string result = $"已處理: {input.ToUpper()}";

                return new
                {
                    Success = true,
                    Message = "處理完成",
                    Result = result,
                    Timestamp = DateTime.Now
                };
            }

            return new { Success = false, Message = "缺少資料參數" };
        }

        private object Calculate(Dictionary<string, object> parameters)
        {
            if (parameters.TryGetValue("a", out var aObj) &&
                parameters.TryGetValue("b", out var bObj))
            {
                int a = Convert.ToInt32(aObj);
                int b = Convert.ToInt32(bObj);

                return new
                {
                    Success = true,
                    Sum = a + b,
                    Product = a * b,
                    Average = (a + b) / 2.0
                };
            }

            return new { Success = false, Message = "缺少計算參數" };
        }

        private object QueryData(Dictionary<string, object> parameters)
        {
            // 模擬資料查詢
            var results = new List<object>
            {
                new { Id = 1, Name = "項目A", Value = 100 },
                new { Id = 2, Name = "項目B", Value = 200 },
                new { Id = 3, Name = "項目C", Value = 300 }
            };

            return new
            {
                Success = true,
                Count = results.Count,
                Data = results
            };
        }

        public void Dispose()
        {
            // 釋放資源
            Console.WriteLine($"{ServiceName} 正在釋放資源...");
            _isInitialized = false;
        }
    }
}
```

#### MyCalculatorService.cs - 計算服務

```csharp
using System;
using System.Collections.Generic;
using ApiContracts;

namespace ExternalApi.Services
{
    public class MyCalculatorService : IApiService
    {
        public string ServiceName => "進階計算服務";
        public string Version => "2.0.0";

        public bool Initialize()
        {
            Console.WriteLine("計算服務已初始化");
            return true;
        }

        public object Execute(Dictionary<string, object> parameters)
        {
            if (!parameters.TryGetValue("operation", out var opObj))
            {
                throw new ArgumentException("缺少 'operation' 參數");
            }

            string operation = opObj.ToString();

            return operation switch
            {
                "add" => Add(parameters),
                "subtract" => Subtract(parameters),
                "multiply" => Multiply(parameters),
                "divide" => Divide(parameters),
                "power" => Power(parameters),
                "sqrt" => SquareRoot(parameters),
                _ => throw new NotSupportedException($"不支援的運算: {operation}")
            };
        }

        private object Add(Dictionary<string, object> parameters)
        {
            double a = Convert.ToDouble(parameters["x"]);
            double b = Convert.ToDouble(parameters["y"]);
            return new { Result = a + b, Operation = "加法" };
        }

        private object Subtract(Dictionary<string, object> parameters)
        {
            double a = Convert.ToDouble(parameters["x"]);
            double b = Convert.ToDouble(parameters["y"]);
            return new { Result = a - b, Operation = "減法" };
        }

        private object Multiply(Dictionary<string, object> parameters)
        {
            double a = Convert.ToDouble(parameters["x"]);
            double b = Convert.ToDouble(parameters["y"]);
            return new { Result = a * b, Operation = "乘法" };
        }

        private object Divide(Dictionary<string, object> parameters)
        {
            double a = Convert.ToDouble(parameters["x"]);
            double b = Convert.ToDouble(parameters["y"]);

            if (Math.Abs(b) < 0.0001)
            {
                return new { Success = false, Message = "除數不能為零" };
            }

            return new { Result = a / b, Operation = "除法" };
        }

        private object Power(Dictionary<string, object> parameters)
        {
            double baseNum = Convert.ToDouble(parameters["base"]);
            double exponent = Convert.ToDouble(parameters["exponent"]);
            return new { Result = Math.Pow(baseNum, exponent), Operation = "次方" };
        }

        private object SquareRoot(Dictionary<string, object> parameters)
        {
            double number = Convert.ToDouble(parameters["number"]);

            if (number < 0)
            {
                return new { Success = false, Message = "負數無法開平方根" };
            }

            return new { Result = Math.Sqrt(number), Operation = "平方根" };
        }

        public void Dispose()
        {
            Console.WriteLine("計算服務已釋放");
        }
    }
}
```

#### MyPlugin.cs - 插件進入點

```csharp
using System;
using System.Collections.Generic;
using ApiContracts;
using ExternalApi.Services;

namespace ExternalApi
{
    /// <summary>
    /// 插件主類別 - 作為插件的進入點
    /// </summary>
    public class MyPlugin : IPlugin
    {
        private readonly List<IApiService> _services;

        public Guid PluginId { get; } = Guid.Parse("12345678-1234-1234-1234-123456789012");
        public string Name => "我的第一個插件";
        public string Description => "這是一個示範插件，提供資料處理和計算服務";
        public string Author => "您的名字";
        public Version PluginVersion => new Version(1, 0, 0);

        public MyPlugin()
        {
            // 初始化服務列表
            _services = new List<IApiService>
            {
                new MyDataService(),
                new MyCalculatorService()
            };
        }

        public IEnumerable<IApiService> GetServices()
        {
            return _services;
        }

        public void OnLoad()
        {
            Console.WriteLine($"插件 '{Name}' 已載入");
            Console.WriteLine($"版本: {PluginVersion}");
            Console.WriteLine($"作者: {Author}");

            // 初始化所有服務
            foreach (var service in _services)
            {
                service.Initialize();
            }
        }

        public void OnUnload()
        {
            Console.WriteLine($"插件 '{Name}' 正在卸載...");

            // 釋放所有服務
            foreach (var service in _services)
            {
                service.Dispose();
            }
        }
    }
}
```

---

# 步驟 4: WPF 主視窗完整實作

## MainWindow.xaml - 完整 UI 介面

```xml
<Window x:Class="MyWpfApp.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="動態 API 載入示範" Height="700" Width="1000"
        WindowStartupLocation="CenterScreen"
        Loaded="Window_Loaded">

    <Window.Resources>
        <!-- 按鈕樣式 -->
        <Style x:Key="PrimaryButton" TargetType="Button">
            <Setter Property="Background" Value="#0078D4"/>
            <Setter Property="Foreground" Value="White"/>
            <Setter Property="Padding" Value="15,8"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border Background="{TemplateBinding Background}"
                                CornerRadius="4"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center"
                                            VerticalAlignment="Center"/>
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#106EBE"/>
                </Trigger>
                <Trigger Property="IsEnabled" Value="False">
                    <Setter Property="Background" Value="#CCCCCC"/>
                    <Setter Property="Foreground" Value="#666666"/>
                </Trigger>
            </Style.Triggers>
        </Style>

        <!-- 次要按鈕樣式 -->
        <Style x:Key="SecondaryButton" TargetType="Button" BasedOn="{StaticResource PrimaryButton}">
            <Setter Property="Background" Value="#F3F2F1"/>
            <Setter Property="Foreground" Value="#323130"/>
            <Style.Triggers>
                <Trigger Property="IsMouseOver" Value="True">
                    <Setter Property="Background" Value="#E1DFDD"/>
                </Trigger>
            </Style.Triggers>
        </Style>
    </Window.Resources>

    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="200"/>
        </Grid.RowDefinitions>

        <!-- ========== 標題區 ========== -->
        <StackPanel Grid.Row="0" Margin="0,0,0,20">
            <TextBlock Text="🔌 插件管理系統"
                       FontSize="28"
                       FontWeight="Bold"
                       Foreground="#323130"/>
            <TextBlock Text="動態載入和管理外部 API 插件"
                       FontSize="14"
                       Foreground="#605E5C"
                       Margin="0,5,0,0"/>
        </StackPanel>

        <!-- ========== 控制面板 ========== -->
        <Border Grid.Row="1"
                Background="#F3F2F1"
                CornerRadius="8"
                Padding="15"
                Margin="0,0,0,15">
            <StackPanel>
                <TextBlock Text="插件控制"
                           FontSize="16"
                           FontWeight="SemiBold"
                           Margin="0,0,0,10"/>

                <!-- 第一排按鈕 -->
                <WrapPanel Margin="0,0,0,10">
                    <Button Content="📂 載入所有插件"
                            Style="{StaticResource PrimaryButton}"
                            Width="140"
                            Margin="0,0,10,0"
                            Click="LoadAllPlugins_Click"/>

                    <Button Content="➕ 載入單一插件"
                            Style="{StaticResource SecondaryButton}"
                            Width="140"
                            Margin="0,0,10,0"
                            Click="LoadSinglePlugin_Click"/>

                    <Button Content="🔄 重新載入選定插件"
                            Style="{StaticResource SecondaryButton}"
                            Width="160"
                            Margin="0,0,10,0"
                            Click="ReloadPlugin_Click"
                            IsEnabled="{Binding ElementName=PluginListBox, Path=SelectedItem, Converter={StaticResource NullToBoolConverter}}"/>

                    <Button Content="❌ 卸載選定插件"
                            Style="{StaticResource SecondaryButton}"
                            Width="140"
                            Margin="0,0,10,0"
                            Click="UnloadPlugin_Click"
                            IsEnabled="{Binding ElementName=PluginListBox, Path=SelectedItem, Converter={StaticResource NullToBoolConverter}}"/>

                    <Button Content="🗑️ 卸載全部"
                            Style="{StaticResource SecondaryButton}"
                            Width="120"
                            Click="UnloadAllPlugins_Click"/>
                </WrapPanel>

                <!-- 第二排按鈕 -->
                <WrapPanel>
                    <Button Content="✅ 測試基礎載入器"
                            Style="{StaticResource PrimaryButton}"
                            Width="150"
                            Margin="0,0,10,0"
                            Click="TestBasicLoader_Click"/>

                    <Button Content="🚀 測試服務執行"
                            Style="{StaticResource PrimaryButton}"
                            Width="140"
                            Margin="0,0,10,0"
                            Click="TestServiceExecution_Click"
                            IsEnabled="{Binding ElementName=PluginListBox, Path=SelectedItem, Converter={StaticResource NullToBoolConverter}}"/>

                    <Button Content="📊 顯示插件資訊"
                            Style="{StaticResource SecondaryButton}"
                            Width="140"
                            Margin="0,0,10,0"
                            Click="ShowPluginInfo_Click"
                            IsEnabled="{Binding ElementName=PluginListBox, Path=SelectedItem, Converter={StaticResource NullToBoolConverter}}"/>

                    <Button Content="🧹 清除日誌"
                            Style="{StaticResource SecondaryButton}"
                            Width="120"
                            Click="ClearLog_Click"/>
                </WrapPanel>
            </StackPanel>
        </Border>

        <!-- ========== 主要內容區 ========== -->
        <Grid Grid.Row="2" Margin="0,0,0,15">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="350"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- 左側：插件列表 -->
            <Border Grid.Column="0"
                    Background="White"
                    BorderBrush="#E1DFDD"
                    BorderThickness="1"
                    CornerRadius="8"
                    Margin="0,0,10,0">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <!-- 列表標題 -->
                    <Border Grid.Row="0"
                            Background="#F3F2F1"
                            Padding="15,10"
                            CornerRadius="8,8,0,0">
                        <TextBlock Text="已載入的插件"
                                   FontSize="16"
                                   FontWeight="SemiBold"/>
                    </Border>

                    <!-- 插件列表 -->
                    <ListBox Grid.Row="1"
                             x:Name="PluginListBox"
                             BorderThickness="0"
                             SelectionChanged="PluginListBox_SelectionChanged">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <Border Padding="10"
                                        Margin="5"
                                        Background="#FAFAFA"
                                        CornerRadius="6"
                                        BorderBrush="#E1DFDD"
                                        BorderThickness="1">
                                    <StackPanel>
                                        <TextBlock Text="{Binding Name}"
                                                   FontWeight="SemiBold"
                                                   FontSize="14"/>
                                        <TextBlock Text="{Binding Description}"
                                                   FontSize="12"
                                                   Foreground="#605E5C"
                                                   TextWrapping="Wrap"
                                                   Margin="0,3,0,0"/>
                                        <StackPanel Orientation="Horizontal" Margin="0,5,0,0">
                                            <TextBlock Text="版本: " FontSize="11" Foreground="#8A8886"/>
                                            <TextBlock Text="{Binding PluginVersion}" FontSize="11" Foreground="#8A8886"/>
                                            <TextBlock Text=" | ID: " FontSize="11" Foreground="#8A8886" Margin="10,0,0,0"/>
                                            <TextBlock Text="{Binding PluginId}" FontSize="11" Foreground="#8A8886"/>
                                        </StackPanel>
                                    </StackPanel>
                                </Border>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </Grid>
            </Border>

            <!-- 右側：服務列表 -->
            <Border Grid.Column="1"
                    Background="White"
                    BorderBrush="#E1DFDD"
                    BorderThickness="1"
                    CornerRadius="8">
                <Grid>
                    <Grid.RowDefinitions>
                        <RowDefinition Height="Auto"/>
                        <RowDefinition Height="*"/>
                    </Grid.RowDefinitions>

                    <!-- 列表標題 -->
                    <Border Grid.Row="0"
                            Background="#F3F2F1"
                            Padding="15,10"
                            CornerRadius="8,8,0,0">
                        <TextBlock Text="插件提供的服務"
                                   FontSize="16"
                                   FontWeight="SemiBold"/>
                    </Border>

                    <!-- 服務列表 -->
                    <ListBox Grid.Row="1"
                             x:Name="ServiceListBox"
                             BorderThickness="0">
                        <ListBox.ItemTemplate>
                            <DataTemplate>
                                <Border Padding="12"
                                        Margin="5"
                                        Background="#FAFAFA"
                                        CornerRadius="6"
                                        BorderBrush="#E1DFDD"
                                        BorderThickness="1">
                                    <Grid>
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="*"/>
                                            <ColumnDefinition Width="Auto"/>
                                        </Grid.ColumnDefinitions>

                                        <StackPanel Grid.Column="0">
                                            <TextBlock Text="{Binding ServiceName}"
                                                       FontWeight="SemiBold"
                                                       FontSize="14"/>
                                            <TextBlock Text="{Binding Version, StringFormat='版本: {0}'}"
                                                       FontSize="11"
                                                       Foreground="#605E5C"
                                                       Margin="0,3,0,0"/>
                                        </StackPanel>

                                        <Button Grid.Column="1"
                                                Content="執行測試"
                                                Style="{StaticResource PrimaryButton}"
                                                Padding="10,5"
                                                Click="ExecuteService_Click"
                                                Tag="{Binding}"/>
                                    </Grid>
                                </Border>
                            </DataTemplate>
                        </ListBox.ItemTemplate>
                    </ListBox>
                </Grid>
            </Border>
        </Grid>

        <!-- ========== 日誌輸出區 ========== -->
        <Border Grid.Row="3"
                Background="White"
                BorderBrush="#E1DFDD"
                BorderThickness="1"
                CornerRadius="8">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>

                <!-- 日誌標題 -->
                <Border Grid.Row="0"
                        Background="#F3F2F1"
                        Padding="15,10"
                        CornerRadius="8,8,0,0">
                    <Grid>
                        <TextBlock Text="執行日誌"
                                   FontSize="16"
                                   FontWeight="SemiBold"/>
                        <TextBlock Text="{Binding ElementName=LogTextBox, Path=LineCount, StringFormat='共 {0} 行'}"
                                   FontSize="12"
                                   Foreground="#605E5C"
                                   HorizontalAlignment="Right"
                                   VerticalAlignment="Center"/>
                    </Grid>
                </Border>

                <!-- 日誌內容 -->
                <TextBox Grid.Row="1"
                         x:Name="LogTextBox"
                         IsReadOnly="True"
                         VerticalScrollBarVisibility="Auto"
                         HorizontalScrollBarVisibility="Auto"
                         BorderThickness="0"
                         Padding="10"
                         FontFamily="Consolas"
                         FontSize="12"
                         Background="#FAFAFA"
                         TextWrapping="Wrap"/>
            </Grid>
        </Border>
    </Grid>
</Window>
```

---

## MainWindow.xaml.cs - 完整後端程式碼

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ApiContracts;
using MyWpfApp.Services;

namespace MyWpfApp
{
    /// <summary>
    /// MainWindow.xaml 的互動邏輯
    /// </summary>
    public partial class MainWindow : Window
    {
        private PluginManager _pluginManager;
        private readonly string _pluginDirectory;

        public MainWindow()
        {
            InitializeComponent();

            // 設定插件目錄（可以從配置檔讀取）
            _pluginDirectory = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Plugins"
            );

            // 初始化插件管理器
            _pluginManager = new PluginManager(_pluginDirectory);

            // 重定向 Console 輸出到 UI
            Console.SetOut(new TextBoxWriter(LogTextBox));

            Log("=== 應用程式已啟動 ===");
            Log($"插件目錄: {_pluginDirectory}");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Log("視窗已載入，準備就緒");
        }

        // ==================== 插件管理功能 ====================

        /// <summary>
        /// 載入所有插件
        /// </summary>
        private void LoadAllPlugins_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("開始載入所有插件...");
                _pluginManager.LoadAllPlugins();

                RefreshPluginList();

                int count = _pluginManager.GetAllPlugins().Count();
                Log($"✓ 成功載入 {count} 個插件");

                MessageBox.Show(
                    $"成功載入 {count} 個插件",
                    "載入完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }
            catch (Exception ex)
            {
                Log($"✗ 載入插件失敗: {ex.Message}");
                MessageBox.Show(
                    $"載入失敗:\n{ex.Message}",
                    "錯誤",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 載入單一插件
        /// </summary>
        private void LoadSinglePlugin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "DLL 檔案 (*.dll)|*.dll|所有檔案 (*.*)|*.*",
                    Title = "選擇插件 DLL 檔案",
                    InitialDirectory = _pluginDirectory
                };

                if (dialog.ShowDialog() == true)
                {
                    string dllPath = dialog.FileName;
                    Log($"選擇的檔案: {dllPath}");

                    // 詢問型別名稱
                    var typeNameDialog = new InputDialog("請輸入完整的型別名稱", "型別名稱");
                    if (typeNameDialog.ShowDialog() == true)
                    {
                        string typeName = typeNameDialog.Answer;
                        Log($"嘗試載入型別: {typeName}");

                        var plugin = _pluginManager.LoadPlugin(dllPath, typeName);

                        if (plugin != null)
                        {
                            RefreshPluginList();
                            Log($"✓ 成功載入插件: {plugin.Name}");
                            MessageBox.Show(
                                $"成功載入插件:\n{plugin.Name} v{plugin.PluginVersion}",
                                "載入成功",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 載入插件失敗: {ex.Message}");
                MessageBox.Show(
                    $"載入失敗:\n{ex.Message}",
                    "錯誤",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 重新載入選定的插件
        /// </summary>
        private void ReloadPlugin_Click(object sender, RoutedEventArgs e)
        {
            if (PluginListBox.SelectedItem is IPlugin plugin)
            {
                try
                {
                    Log($"正在重新載入插件: {plugin.Name}...");
                    _pluginManager.ReloadPlugin(plugin.PluginId);

                    RefreshPluginList();
                    Log($"✓ 插件 {plugin.Name} 已重新載入");

                    MessageBox.Show(
                        $"插件已重新載入:\n{plugin.Name}",
                        "重載成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    Log($"✗ 重新載入失敗: {ex.Message}");
                    MessageBox.Show(
                        $"重新載入失敗:\n{ex.Message}",
                        "錯誤",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        /// <summary>
        /// 卸載選定的插件
        /// </summary>
        private void UnloadPlugin_Click(object sender, RoutedEventArgs e)
        {
            if (PluginListBox.SelectedItem is IPlugin plugin)
            {
                var result = MessageBox.Show(
                    $"確定要卸載插件 '{plugin.Name}' 嗎?",
                    "確認卸載",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question
                );

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        Log($"正在卸載插件: {plugin.Name}...");
                        _pluginManager.UnloadPlugin(plugin.PluginId);

                        RefreshPluginList();
                        ServiceListBox.ItemsSource = null;

                        Log($"✓ 插件 {plugin.Name} 已卸載");
                        MessageBox.Show(
                            "插件已卸載",
                            "卸載成功",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                    catch (Exception ex)
                    {
                        Log($"✗ 卸載失敗: {ex.Message}");
                        MessageBox.Show(
                            $"卸載失敗:\n{ex.Message}",
                            "錯誤",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error
                        );
                    }
                }
            }
        }

        /// <summary>
        /// 卸載所有插件
        /// </summary>
        private void UnloadAllPlugins_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "確定要卸載所有插件嗎?",
                "確認卸載",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Log("正在卸載所有插件...");
                    _pluginManager.UnloadAll();

                    RefreshPluginList();
                    ServiceListBox.ItemsSource = null;

                    Log("✓ 所有插件已卸載");
                    MessageBox.Show(
                        "所有插件已卸載",
                        "卸載完成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    Log($"✗ 卸載失敗: {ex.Message}");
                    MessageBox.Show(
                        $"卸載失敗:\n{ex.Message}",
                        "錯誤",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        // ==================== 測試功能 ====================

        /// <summary>
        /// 測試基礎載入器
        /// </summary>
        private void TestBasicLoader_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Log("========== 測試基礎載入器 ==========");

                var dialog = new OpenFileDialog
                {
                    Filter = "DLL 檔案 (*.dll)|*.dll",
                    Title = "選擇要測試的 DLL",
                    InitialDirectory = _pluginDirectory
                };

                if (dialog.ShowDialog() == true)
                {
                    string dllPath = dialog.FileName;
                    Log($"測試檔案: {dllPath}");

                    // 詢問型別名稱
                    var typeDialog = new InputDialog("請輸入完整的型別名稱", "型別名稱");
                    if (typeDialog.ShowDialog() == true)
                    {
                        string typeName = typeDialog.Answer;

                        // 使用基礎載入器
                        var loader = new ApiLoader();
                        var plugin = loader.LoadApi<IPlugin>(dllPath, typeName);

                        if (plugin != null)
                        {
                            Log($"✓ 載入成功!");
                            Log($"  插件名稱: {plugin.Name}");
                            Log($"  插件 ID: {plugin.PluginId}");
                            Log($"  版本: {plugin.PluginVersion}");
                            Log($"  作者: {plugin.Author}");
                            Log($"  描述: {plugin.Description}");

                            var services = plugin.GetServices().ToList();
                            Log($"  提供 {services.Count} 個服務:");
                            foreach (var service in services)
                            {
                                Log($"    - {service.ServiceName} (v{service.Version})");
                            }

                            MessageBox.Show(
                                $"基礎載入器測試成功!\n\n插件: {plugin.Name}\n版本: {plugin.PluginVersion}",
                                "測試成功",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"✗ 測試失敗: {ex.Message}");
                Log($"  堆疊追蹤: {ex.StackTrace}");
                MessageBox.Show(
                    $"測試失敗:\n{ex.Message}",
                    "錯誤",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 測試服務執行
        /// </summary>
        private void TestServiceExecution_Click(object sender, RoutedEventArgs e)
        {
            if (PluginListBox.SelectedItem is IPlugin plugin)
            {
                try
                {
                    Log($"========== 測試插件服務: {plugin.Name} ==========");

                    var services = plugin.GetServices().ToList();
                    Log($"插件提供 {services.Count} 個服務");

                    foreach (var service in services)
                    {
                        Log($"\n測試服務: {service.ServiceName}");

                        // 初始化服務
                        if (service.Initialize())
                        {
                            Log($"  ✓ 服務初始化成功");

                            // 執行測試
                            var parameters = new Dictionary<string, object>
                            {
                                { "action", "process" },
                                { "data", "測試資料" }
                            };

                            var result = service.Execute(parameters);
                            Log($"  執行結果: {JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}");
                        }
                        else
                        {
                            Log($"  ✗ 服務初始化失敗");
                        }
                    }

                    MessageBox.Show(
                        "服務測試完成，請查看日誌輸出",
                        "測試完成",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                catch (Exception ex)
                {
                    Log($"✗ 測試失敗: {ex.Message}");
                    MessageBox.Show(
                        $"測試失敗:\n{ex.Message}",
                        "錯誤",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        /// <summary>
        /// 執行選定的服務
        /// </summary>
        private void ExecuteService_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is IApiService service)
            {
                try
                {
                    Log($"========== 執行服務: {service.ServiceName} ==========");

                    // 顯示參數輸入對話框
                    var paramDialog = new ServiceParameterDialog(service.ServiceName);
                    if (paramDialog.ShowDialog() == true)
                    {
                        var parameters = paramDialog.Parameters;

                        Log($"執行參數: {JsonSerializer.Serialize(parameters)}");

                        // 初始化服務
                        if (!service.Initialize())
                        {
                            throw new Exception("服務初始化失敗");
                        }

                        // 執行服務
                        var result = service.Execute(parameters);

                        Log($"執行結果:\n{JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}");

                        // 釋放服務
                        service.Dispose();

                        MessageBox.Show(
                            $"服務執行成功!\n\n結果:\n{JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}",
                            "執行成功",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information
                        );
                    }
                }
                catch (Exception ex)
                {
                    Log($"✗ 執行失敗: {ex.Message}");
                    MessageBox.Show(
                        $"執行失敗:\n{ex.Message}",
                        "錯誤",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error
                    );
                }
            }
        }

        // ==================== UI 事件處理 ====================

        /// <summary>
        /// 插件選擇變更
        /// </summary>
        private void PluginListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PluginListBox.SelectedItem is IPlugin plugin)
            {
                Log($"選擇插件: {plugin.Name}");

                // 顯示插件提供的服務
                var services = plugin.GetServices().ToList();
                ServiceListBox.ItemsSource = services;

                Log($"  提供 {services.Count} 個服務");
            }
        }

        /// <summary>
        /// 顯示插件詳細資訊
        /// </summary>
        private void ShowPluginInfo_Click(object sender, RoutedEventArgs e)
        {
            if (PluginListBox.SelectedItem is IPlugin plugin)
            {
                var services = plugin.GetServices().ToList();

                string info = $"插件資訊\n" +
                             $"━━━━━━━━━━━━━━━━━━━━━━\n" +
                             $"名稱: {plugin.Name}\n" +
                             $"ID: {plugin.PluginId}\n" +
                             $"版本: {plugin.PluginVersion}\n" +
                             $"作者: {plugin.Author}\n" +
                             $"描述: {plugin.Description}\n\n" +
                             $"提供的服務 ({services.Count} 個):\n";

                foreach (var service in services)
                {
                    info += $"  • {service.ServiceName} (v{service.Version})\n";
                }

                MessageBox.Show(info, "插件資訊", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 清除日誌
        /// </summary>
        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogTextBox.Clear();
            Log("日誌已清除");
        }

        // ==================== 輔助方法 ====================

        /// <summary>
        /// 重新整理插件列表
        /// </summary>
        private void RefreshPluginList()
        {
            var plugins = _pluginManager.GetAllPlugins().ToList();
            PluginListBox.ItemsSource = null;
            PluginListBox.ItemsSource = plugins;

            Log($"插件列表已更新，共 {plugins.Count} 個插件");
        }

        /// <summary>
        /// 記錄日誌
        /// </summary>
        private void Log(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] {message}";

            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(logMessage + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });
        }
    }

    // ==================== 輔助類別 ====================

    /// <summary>
    /// TextBox 寫入器 - 將 Console 輸出重定向到 TextBox
    /// </summary>
    public class TextBoxWriter : System.IO.TextWriter
    {
        private readonly System.Windows.Controls.TextBox _textBox;

        public TextBoxWriter(System.Windows.Controls.TextBox textBox)
        {
            _textBox = textBox;
        }

        public override void Write(char value)
        {
            _textBox.Dispatcher.Invoke(() => _textBox.AppendText(value.ToString()));
        }

        public override void Write(string value)
        {
            _textBox.Dispatcher.Invoke(() => _textBox.AppendText(value));
        }

        public override void WriteLine(string value)
        {
            _textBox.Dispatcher.Invoke(() =>
            {
                _textBox.AppendText(value + Environment.NewLine);
                _textBox.ScrollToEnd();
            });
        }

        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}
```

---

## InputDialog.xaml - 輸入對話框

```xml
<Window x:Class="MyWpfApp.InputDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="{Binding Title}"
        Height="180"
        Width="450"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <TextBlock Grid.Row="0"
                   Text="{Binding Question}"
                   FontSize="14"
                   Margin="0,0,0,10"
                   TextWrapping="Wrap"/>

        <TextBox Grid.Row="1"
                 x:Name="AnswerTextBox"
                 Height="30"
                 Padding="5"
                 FontSize="13"/>

        <StackPanel Grid.Row="3"
                    Orientation="Horizontal"
                    HorizontalAlignment="Right"
                    Margin="0,15,0,0">
            <Button Content="確定"
                    Width="80"
                    Height="30"
                    Margin="0,0,10,0"
                    IsDefault="True"
                    Click="OK_Click"/>
            <Button Content="取消"
                    Width="80"
                    Height="30"
                    IsCancel="True"
                    Click="Cancel_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

## InputDialog.xaml.cs

```csharp
using System.Windows;

namespace MyWpfApp
{
    public partial class InputDialog : Window
    {
        public string Answer { get; private set; }
        public string Question { get; set; }
        public new string Title { get; set; }

        public InputDialog(string question, string title = "輸入")
        {
            InitializeComponent();

            Question = question;
            Title = title;
            DataContext = this;

            AnswerTextBox.Focus();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Answer = AnswerTextBox.Text;
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
```

---

## ServiceParameterDialog.xaml - 服務參數對話框

```xml
<Window x:Class="MyWpfApp.ServiceParameterDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="服務參數設定"
        Height="400"
        Width="500"
        WindowStartupLocation="CenterOwner"
        ResizeMode="NoResize">
    <Grid Margin="20">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" Margin="0,0,0,15">
            <TextBlock Text="{Binding ServiceName, StringFormat='設定服務參數: {0}'}"
                       FontSize="16"
                       FontWeight="SemiBold"/>
            <TextBlock Text="請輸入服務執行所需的參數 (JSON 格式)"
                       FontSize="12"
                       Foreground="#605E5C"
                       Margin="0,5,0,0"/>
        </StackPanel>

        <Border Grid.Row="1"
                BorderBrush="#E1DFDD"
                BorderThickness="1"
                CornerRadius="4">
            <TextBox x:Name="ParametersTextBox"
                     AcceptsReturn="True"
                     VerticalScrollBarVisibility="Auto"
                     FontFamily="Consolas"
                     FontSize="12"
                     Padding="10"
                     TextWrapping="Wrap">
{
  "action": "process",
  "data": "測試資料"
}
            </TextBox>
        </Border>

        <StackPanel Grid.Row="2"
                    Orientation="Horizontal"
                    HorizontalAlignment="Right"
                    Margin="0,15,0,0">
            <Button Content="執行"
                    Width="90"
                    Height="32"
                    Margin="0,0,10,0"
                    IsDefault="True"
                    Click="Execute_Click"/>
            <Button Content="取消"
                    Width="90"
                    Height="32"
                    IsCancel="True"
                    Click="Cancel_Click"/>
        </StackPanel>
    </Grid>
</Window>
```

## ServiceParameterDialog.xaml.cs

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows;

namespace MyWpfApp
{
    public partial class ServiceParameterDialog : Window
    {
        public Dictionary<string, object> Parameters { get; private set; }
        public string ServiceName { get; set; }

        public ServiceParameterDialog(string serviceName)
        {
            InitializeComponent();
            ServiceName = serviceName;
            DataContext = this;
        }

        private void Execute_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string json = ParametersTextBox.Text;

                // 解析 JSON
                Parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                DialogResult = true;
                Close();
            }
            catch (JsonException ex)
            {
                MessageBox.Show(
                    $"JSON 格式錯誤:\n{ex.Message}",
                    "錯誤",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
```

---

## App.xaml - 應用程式進入點

```xml
<Application x:Class="MyWpfApp.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="MainWindow.xaml">
    <Application.Resources>
        <!-- 全域資源 -->
    </Application.Resources>
</Application>
```

## App.xaml.cs

```csharp
using System.Windows;

namespace MyWpfApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 可在此處進行全域初始化
        }
    }
}
```

---

## 專案配置檔 (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ApiContracts\ApiContracts.csproj" />
  </ItemGroup>

  <!-- 自動複製插件到輸出目錄 -->
  <ItemGroup>
    <None Update="Plugins\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

---

## 使用說明

### 1. 啟動應用程式

- 執行 WPF 應用程式
- 主視窗會顯示插件管理介面

### 2. 載入插件

- 點擊「載入所有插件」自動載入 Plugins 目錄下的所有插件
- 或點擊「載入單一插件」手動選擇 DLL 檔案

### 3. 查看插件資訊

- 左側列表顯示已載入的插件
- 選擇插件後，右側顯示該插件提供的服務
- 點擊「顯示插件資訊」查看詳細資訊

### 4. 執行服務

- 選擇要執行的服務
- 點擊「執行測試」按鈕
- 在參數對話框中輸入 JSON 格式的參數
- 查看執行結果

### 5. 管理插件

- 重新載入：更新插件後重新載入
- 卸載插件：移除不需要的插件
- 查看日誌：所有操作都會記錄在下方日誌區

這樣你就有一個完整的 WPF 主視窗實作了!

---

# WPF 動態載入 API - 完整範例與進階指南

---

## 完整程式碼範例

### 範例 1: 最簡單的動態載入

```csharp
using System;
using System.Reflection;
using ApiContracts;

namespace SimpleLoadExample
{
    public class SimpleLoader
    {
        public static void Main()
        {
            // 1. 指定 DLL 路徑
            string dllPath = @"C:\Plugins\ExternalApi.dll";

            // 2. 載入組件
            Assembly assembly = Assembly.LoadFrom(dllPath);

            // 3. 取得型別（完整命名空間.類別名）
            Type pluginType = assembly.GetType("ExternalApi.MyPlugin");

            // 4. 建立實例
            IPlugin plugin = Activator.CreateInstance(pluginType) as IPlugin;

            // 5. 使用插件
            if (plugin != null)
            {
                Console.WriteLine($"載入插件: {plugin.Name}");
                Console.WriteLine($"版本: {plugin.PluginVersion}");

                plugin.OnLoad();

                // 取得服務
                foreach (var service in plugin.GetServices())
                {
                    Console.WriteLine($"  服務: {service.ServiceName}");
                }
            }
        }
    }
}
```

### 範例 2: 使用泛型載入器

```csharp
using System;
using System.IO;
using System.Reflection;
using ApiContracts;

namespace GenericLoaderExample
{
    /// <summary>
    /// 泛型 API 載入器
    /// </summary>
    public class ApiLoader
    {
        /// <summary>
        /// 載入並建立指定型別的實例
        /// </summary>
        public T Load<T>(string dllPath, string typeName) where T : class
        {
            if (!File.Exists(dllPath))
                throw new FileNotFoundException($"找不到檔案: {dllPath}");

            Assembly assembly = Assembly.LoadFrom(dllPath);
            Type type = assembly.GetType(typeName);

            if (type == null)
                throw new TypeLoadException($"找不到型別: {typeName}");

            if (!typeof(T).IsAssignableFrom(type))
                throw new InvalidCastException($"{typeName} 未實作 {typeof(T).Name}");

            return Activator.CreateInstance(type) as T;
        }
    }

    // 使用範例
    class Program
    {
        static void Main()
        {
            var loader = new ApiLoader();

            // 載入插件
            var plugin = loader.Load<IPlugin>(
                @"C:\Plugins\ExternalApi.dll",
                "ExternalApi.MyPlugin"
            );

            Console.WriteLine($"插件名稱: {plugin.Name}");

            // 載入特定服務
            var service = loader.Load<IApiService>(
                @"C:\Plugins\ExternalApi.dll",
                "ExternalApi.Services.MyDataService"
            );

            service.Initialize();
            var result = service.Execute(new Dictionary<string, object>
            {
                { "action", "process" },
                { "data", "Hello World" }
            });

            Console.WriteLine($"執行結果: {result}");
        }
    }
}
```

### 範例 3: 完整的插件管理系統

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ApiContracts;

namespace FullPluginSystemExample
{
    /// <summary>
    /// 插件資訊容器
    /// </summary>
    public class PluginInfo
    {
        public IPlugin Plugin { get; set; }
        public string LoadPath { get; set; }
        public DateTime LoadTime { get; set; }
        public PluginConfig Config { get; set; }
    }

    /// <summary>
    /// 完整的插件管理系統
    /// </summary>
    public class PluginSystem
    {
        private readonly Dictionary<Guid, PluginInfo> _plugins;
        private readonly string _pluginDirectory;
        private readonly ApiLoader _loader;

        public PluginSystem(string pluginDirectory)
        {
            _pluginDirectory = pluginDirectory;
            _plugins = new Dictionary<Guid, PluginInfo>();
            _loader = new ApiLoader();
        }

        /// <summary>
        /// 自動掃描並載入所有插件
        /// </summary>
        public void LoadAllPlugins()
        {
            if (!Directory.Exists(_pluginDirectory))
            {
                Directory.CreateDirectory(_pluginDirectory);
                return;
            }

            foreach (var dir in Directory.GetDirectories(_pluginDirectory))
            {
                TryLoadPluginFromDirectory(dir);
            }
        }

        /// <summary>
        /// 從目錄載入插件
        /// </summary>
        private void TryLoadPluginFromDirectory(string directory)
        {
            try
            {
                // 讀取配置檔
                string configPath = Path.Combine(directory, "plugin.json");
                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"[警告] {directory} 中沒有 plugin.json");
                    return;
                }

                var config = JsonSerializer.Deserialize<PluginConfig>(
                    File.ReadAllText(configPath)
                );

                if (!config.AutoLoad)
                {
                    Console.WriteLine($"[跳過] {config.Name} (AutoLoad=false)");
                    return;
                }

                // 載入插件
                string dllPath = Path.Combine(directory, config.DllName);
                var plugin = _loader.Load<IPlugin>(dllPath, config.TypeName);

                // 註冊插件
                var info = new PluginInfo
                {
                    Plugin = plugin,
                    LoadPath = dllPath,
                    LoadTime = DateTime.Now,
                    Config = config
                };

                _plugins[plugin.PluginId] = info;
                plugin.OnLoad();

                Console.WriteLine($"[成功] 載入插件: {plugin.Name} v{plugin.PluginVersion}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[錯誤] 載入 {directory} 失敗: {ex.Message}");
            }
        }

        /// <summary>
        /// 手動載入單一插件
        /// </summary>
        public IPlugin LoadPlugin(string dllPath, string typeName)
        {
            var plugin = _loader.Load<IPlugin>(dllPath, typeName);

            var info = new PluginInfo
            {
                Plugin = plugin,
                LoadPath = dllPath,
                LoadTime = DateTime.Now
            };

            _plugins[plugin.PluginId] = info;
            plugin.OnLoad();

            return plugin;
        }

        /// <summary>
        /// 卸載插件
        /// </summary>
        public void UnloadPlugin(Guid pluginId)
        {
            if (_plugins.TryGetValue(pluginId, out var info))
            {
                info.Plugin.OnUnload();
                _plugins.Remove(pluginId);
                Console.WriteLine($"[卸載] {info.Plugin.Name}");
            }
        }

        /// <summary>
        /// 獲取所有插件
        /// </summary>
        public IEnumerable<IPlugin> GetAllPlugins()
        {
            return _plugins.Values.Select(i => i.Plugin);
        }

        /// <summary>
        /// 根據 ID 獲取插件
        /// </summary>
        public IPlugin GetPlugin(Guid pluginId)
        {
            return _plugins.TryGetValue(pluginId, out var info) ? info.Plugin : null;
        }

        /// <summary>
        /// 執行服務
        /// </summary>
        public object ExecuteService(Guid pluginId, string serviceName, Dictionary<string, object> parameters)
        {
            var plugin = GetPlugin(pluginId);
            if (plugin == null)
                throw new Exception($"找不到插件 ID: {pluginId}");

            var service = plugin.GetServices()
                .FirstOrDefault(s => s.ServiceName == serviceName);

            if (service == null)
                throw new Exception($"找不到服務: {serviceName}");

            if (!service.Initialize())
                throw new Exception("服務初始化失敗");

            try
            {
                return service.Execute(parameters);
            }
            finally
            {
                service.Dispose();
            }
        }
    }

    /// <summary>
    /// 插件配置
    /// </summary>
    public class PluginConfig
    {
        public string Name { get; set; }
        public string DllName { get; set; }
        public string TypeName { get; set; }
        public bool AutoLoad { get; set; }
        public string Version { get; set; }
    }

    // ========== 使用示範 ==========

    class Program
    {
        static void Main()
        {
            Console.WriteLine("========== 插件系統示範 ==========\n");

            // 初始化插件系統
            var pluginSystem = new PluginSystem(@"C:\MyApp\Plugins");

            // 載入所有插件
            Console.WriteLine("載入所有插件...");
            pluginSystem.LoadAllPlugins();
            Console.WriteLine();

            // 列出所有插件
            Console.WriteLine("已載入的插件:");
            foreach (var plugin in pluginSystem.GetAllPlugins())
            {
                Console.WriteLine($"  • {plugin.Name} (v{plugin.PluginVersion})");
                Console.WriteLine($"    作者: {plugin.Author}");
                Console.WriteLine($"    ID: {plugin.PluginId}");

                var services = plugin.GetServices().ToList();
                Console.WriteLine($"    服務數量: {services.Count}");

                foreach (var service in services)
                {
                    Console.WriteLine($"      - {service.ServiceName} (v{service.Version})");
                }
                Console.WriteLine();
            }

            // 執行服務範例
            var firstPlugin = pluginSystem.GetAllPlugins().FirstOrDefault();
            if (firstPlugin != null)
            {
                Console.WriteLine($"執行插件 '{firstPlugin.Name}' 的服務...");

                var parameters = new Dictionary<string, object>
                {
                    { "action", "process" },
                    { "data", "範例資料" }
                };

                var result = pluginSystem.ExecuteService(
                    firstPlugin.PluginId,
                    "資料處理服務",
                    parameters
                );

                Console.WriteLine($"執行結果: {JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}");
            }

            Console.WriteLine("\n按任意鍵結束...");
            Console.ReadKey();
        }
    }
}
```

### 範例 4: WPF 中的實際應用

```csharp
using System;
using System.Collections.Generic;
using System.Windows;
using ApiContracts;

namespace WpfPluginExample
{
    public partial class MainWindow : Window
    {
        private PluginSystem _pluginSystem;

        public MainWindow()
        {
            InitializeComponent();
            InitializePluginSystem();
        }

        private void InitializePluginSystem()
        {
            _pluginSystem = new PluginSystem(@".\Plugins");
        }

        // 載入按鈕點擊事件
        private void LoadPlugins_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _pluginSystem.LoadAllPlugins();

                // 更新 UI 顯示插件列表
                PluginListBox.ItemsSource = _pluginSystem.GetAllPlugins();

                MessageBox.Show("插件載入完成!", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 執行服務按鈕點擊事件
        private void ExecuteService_Click(object sender, RoutedEventArgs e)
        {
            if (PluginListBox.SelectedItem is IPlugin selectedPlugin)
            {
                try
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "action", "calculate" },
                        { "x", 10 },
                        { "y", 20 }
                    };

                    var result = _pluginSystem.ExecuteService(
                        selectedPlugin.PluginId,
                        "進階計算服務",
                        parameters
                    );

                    MessageBox.Show($"執行結果: {result}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"執行失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
```

---

## 進階功能

### 5.1 熱重載（Hot Reload）

允許在不重啟應用程式的情況下更新插件：

```csharp
using System;
using System.IO;
using System.Runtime.Loader;
using System.Reflection;
using ApiContracts;

namespace AdvancedFeatures
{
    /// <summary>
    /// 支援卸載的載入上下文
    /// </summary>
    public class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        private AssemblyDependencyResolver _resolver;

        public CollectibleAssemblyLoadContext(string pluginPath)
            : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }

    /// <summary>
    /// 支援熱重載的插件容器
    /// </summary>
    public class HotReloadablePlugin : IDisposable
    {
        private CollectibleAssemblyLoadContext _loadContext;
        private WeakReference _pluginReference;
        private string _pluginPath;
        private string _typeName;

        public IPlugin Plugin => _pluginReference?.Target as IPlugin;

        public HotReloadablePlugin(string pluginPath, string typeName)
        {
            _pluginPath = pluginPath;
            _typeName = typeName;
            Load();
        }

        private void Load()
        {
            _loadContext = new CollectibleAssemblyLoadContext(_pluginPath);

            Assembly assembly = _loadContext.LoadFromAssemblyPath(_pluginPath);
            Type pluginType = assembly.GetType(_typeName);

            IPlugin plugin = Activator.CreateInstance(pluginType) as IPlugin;
            _pluginReference = new WeakReference(plugin);

            plugin?.OnLoad();
        }

        public void Reload()
        {
            // 卸載舊插件
            Unload();

            // 重新載入
            Load();
        }

        public void Unload()
        {
            Plugin?.OnUnload();

            _loadContext?.Unload();
            _loadContext = null;

            // 強制垃圾回收
            for (int i = 0; i < 10 && _pluginReference.IsAlive; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        public bool IsUnloaded => !_pluginReference.IsAlive;

        public void Dispose()
        {
            Unload();
        }
    }

    // ========== 使用範例 ==========

    class HotReloadExample
    {
        static void Main()
        {
            string pluginPath = @"C:\Plugins\MyPlugin.dll";
            string typeName = "MyPlugin.MainPlugin";

            using (var reloadablePlugin = new HotReloadablePlugin(pluginPath, typeName))
            {
                Console.WriteLine($"插件已載入: {reloadablePlugin.Plugin?.Name}");

                Console.WriteLine("按 R 重新載入插件，按 Q 退出");
                while (true)
                {
                    var key = Console.ReadKey(true);

                    if (key.Key == ConsoleKey.R)
                    {
                        Console.WriteLine("重新載入插件...");
                        reloadablePlugin.Reload();
                        Console.WriteLine($"插件已重新載入: {reloadablePlugin.Plugin?.Name}");
                    }
                    else if (key.Key == ConsoleKey.Q)
                    {
                        break;
                    }
                }
            }
        }
    }
}
```

### 5.2 檔案監視自動重載

```csharp
using System;
using System.IO;

namespace AdvancedFeatures
{
    /// <summary>
    /// 檔案監視器 - 自動偵測插件更新
    /// </summary>
    public class PluginFileWatcher : IDisposable
    {
        private FileSystemWatcher _watcher;
        private HotReloadablePlugin _plugin;
        private string _pluginPath;
        private string _typeName;

        public event EventHandler<string> PluginReloaded;

        public IPlugin Plugin => _plugin?.Plugin;

        public PluginFileWatcher(string pluginPath, string typeName)
        {
            _pluginPath = pluginPath;
            _typeName = typeName;

            // 初始載入
            _plugin = new HotReloadablePlugin(pluginPath, typeName);

            // 設置檔案監視
            SetupFileWatcher();
        }

        private void SetupFileWatcher()
        {
            string directory = Path.GetDirectoryName(_pluginPath);
            string fileName = Path.GetFileName(_pluginPath);

            _watcher = new FileSystemWatcher(directory)
            {
                Filter = fileName,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            _watcher.Changed += OnPluginFileChanged;
        }

        private void OnPluginFileChanged(object sender, FileSystemEventArgs e)
        {
            Console.WriteLine($"偵測到插件檔案變更: {e.FullPath}");

            // 延遲一下確保檔案寫入完成
            System.Threading.Thread.Sleep(500);

            try
            {
                _plugin.Reload();
                Console.WriteLine("插件已自動重新載入");

                PluginReloaded?.Invoke(this, _plugin.Plugin?.Name);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"自動重載失敗: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _watcher?.Dispose();
            _plugin?.Dispose();
        }
    }

    // ========== 使用範例 ==========

    class AutoReloadExample
    {
        static void Main()
        {
            using (var watcher = new PluginFileWatcher(
                @"C:\Plugins\MyPlugin.dll",
                "MyPlugin.MainPlugin"))
            {
                watcher.PluginReloaded += (s, name) =>
                {
                    Console.WriteLine($"[事件] 插件 '{name}' 已重新載入");
                };

                Console.WriteLine("檔案監視已啟動，等待插件更新...");
                Console.WriteLine("按任意鍵退出");
                Console.ReadKey();
            }
        }
    }
}
```

### 5.3 依賴注入整合

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using ApiContracts;

namespace AdvancedFeatures
{
    /// <summary>
    /// 支援依賴注入的插件容器
    /// </summary>
    public class DIPluginContainer
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<Guid, IPlugin> _plugins;

        public DIPluginContainer(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _plugins = new Dictionary<Guid, IPlugin>();
        }

        /// <summary>
        /// 載入插件並注入依賴
        /// </summary>
        public IPlugin LoadPlugin(string dllPath, string typeName)
        {
            var loader = new ApiLoader();
            var pluginType = loader.LoadType(dllPath, typeName);

            // 使用 DI 容器建立實例
            var plugin = ActivatorUtilities.CreateInstance(
                _serviceProvider,
                pluginType
            ) as IPlugin;

            if (plugin != null)
            {
                _plugins[plugin.PluginId] = plugin;
                plugin.OnLoad();
            }

            return plugin;
        }
    }

    // ========== 使用範例 ==========

    class DIExample
    {
        static void Main()
        {
            // 設置 DI 容器
            var services = new ServiceCollection();

            // 註冊服務
            services.AddSingleton<ILogger, ConsoleLogger>();
            services.AddTransient<IDataService, DataService>();

            var serviceProvider = services.BuildServiceProvider();

            // 使用 DI 載入插件
            var container = new DIPluginContainer(serviceProvider);
            var plugin = container.LoadPlugin(
                @"C:\Plugins\MyPlugin.dll",
                "MyPlugin.MainPlugin"
            );

            Console.WriteLine($"插件已載入: {plugin?.Name}");
        }
    }

    // 範例服務介面
    public interface ILogger
    {
        void Log(string message);
    }

    public class ConsoleLogger : ILogger
    {
        public void Log(string message)
        {
            Console.WriteLine($"[LOG] {message}");
        }
    }

    public interface IDataService
    {
        string GetData();
    }

    public class DataService : IDataService
    {
        public string GetData() => "Sample Data";
    }
}
```

### 5.4 事件系統與插件通訊

```csharp
using System;
using System.Collections.Generic;
using ApiContracts;

namespace AdvancedFeatures
{
    /// <summary>
    /// 插件事件參數
    /// </summary>
    public class PluginEventArgs : EventArgs
    {
        public Guid PluginId { get; set; }
        public string EventName { get; set; }
        public Dictionary<string, object> Data { get; set; }
    }

    /// <summary>
    /// 事件匯流排 - 插件間通訊
    /// </summary>
    public class PluginEventBus
    {
        private readonly Dictionary<string, List<Action<PluginEventArgs>>> _handlers;

        public PluginEventBus()
        {
            _handlers = new Dictionary<string, List<Action<PluginEventArgs>>>();
        }

        /// <summary>
        /// 訂閱事件
        /// </summary>
        public void Subscribe(string eventName, Action<PluginEventArgs> handler)
        {
            if (!_handlers.ContainsKey(eventName))
            {
                _handlers[eventName] = new List<Action<PluginEventArgs>>();
            }

            _handlers[eventName].Add(handler);
        }

        /// <summary>
        /// 取消訂閱
        /// </summary>
        public void Unsubscribe(string eventName, Action<PluginEventArgs> handler)
        {
            if (_handlers.ContainsKey(eventName))
            {
                _handlers[eventName].Remove(handler);
            }
        }

        /// <summary>
        /// 發布事件
        /// </summary>
        public void Publish(string eventName, PluginEventArgs args)
        {
            if (_handlers.ContainsKey(eventName))
            {
                foreach (var handler in _handlers[eventName])
                {
                    try
                    {
                        handler(args);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"事件處理器錯誤: {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// 支援事件的插件管理器
    /// </summary>
    public class EventDrivenPluginManager
    {
        private readonly Dictionary<Guid, IPlugin> _plugins;
        private readonly PluginEventBus _eventBus;

        public PluginEventBus EventBus => _eventBus;

        public EventDrivenPluginManager()
        {
            _plugins = new Dictionary<Guid, IPlugin>();
            _eventBus = new PluginEventBus();
        }

        public void LoadPlugin(IPlugin plugin)
        {
            _plugins[plugin.PluginId] = plugin;
            plugin.OnLoad();

            // 發布插件載入事件
            _eventBus.Publish("PluginLoaded", new PluginEventArgs
            {
                PluginId = plugin.PluginId,
                EventName = "PluginLoaded",
                Data = new Dictionary<string, object>
                {
                    { "PluginName", plugin.Name },
                    { "Version", plugin.PluginVersion.ToString() }
                }
            });
        }

        public void UnloadPlugin(Guid pluginId)
        {
            if (_plugins.TryGetValue(pluginId, out var plugin))
            {
                plugin.OnUnload();
                _plugins.Remove(pluginId);

                // 發布插件卸載事件
                _eventBus.Publish("PluginUnloaded", new PluginEventArgs
                {
                    PluginId = pluginId,
                    EventName = "PluginUnloaded"
                });
            }
        }
    }

    // ========== 使用範例 ==========

    class EventBusExample
    {
        static void Main()
        {
            var manager = new EventDrivenPluginManager();

            // 訂閱插件載入事件
            manager.EventBus.Subscribe("PluginLoaded", args =>
            {
                Console.WriteLine($"[事件] 插件已載入: {args.Data["PluginName"]}");
            });

            // 訂閱插件卸載事件
            manager.EventBus.Subscribe("PluginUnloaded", args =>
            {
                Console.WriteLine($"[事件] 插件已卸載: {args.PluginId}");
            });

            // 訂閱自訂事件
            manager.EventBus.Subscribe("DataProcessed", args =>
            {
                Console.WriteLine($"[事件] 資料處理完成: {args.Data["Result"]}");
            });

            // 載入插件（會觸發事件）
            var loader = new ApiLoader();
            var plugin = loader.Load<IPlugin>(
                @"C:\Plugins\MyPlugin.dll",
                "MyPlugin.MainPlugin"
            );

            manager.LoadPlugin(plugin);

            // 插件內部可以發布事件
            manager.EventBus.Publish("DataProcessed", new PluginEventArgs
            {
                PluginId = plugin.PluginId,
                EventName = "DataProcessed",
                Data = new Dictionary<string, object>
                {
                    { "Result", "Success" },
                    { "ProcessedItems", 100 }
                }
            });
        }
    }
}
```

### 5.5 插件版本管理與相容性檢查

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using ApiContracts;

namespace AdvancedFeatures
{
    /// <summary>
    /// 版本資訊
    /// </summary>
    public class VersionInfo
    {
        public Version PluginVersion { get; set; }
        public Version MinHostVersion { get; set; }
        public Version MaxHostVersion { get; set; }
        public List<string> RequiredFeatures { get; set; }
    }

    /// <summary>
    /// 版本管理器
    /// </summary>
    public class VersionManager
    {
        private readonly Version _hostVersion;
        private readonly HashSet<string> _availableFeatures;

        public VersionManager(Version hostVersion, params string[] features)
        {
            _hostVersion = hostVersion;
            _availableFeatures = new HashSet<string>(features);
        }

        /// <summary>
        /// 檢查插件相容性
        /// </summary>
        public bool IsCompatible(VersionInfo pluginVersion, out string reason)
        {
            // 檢查最小版本
            if (pluginVersion.MinHostVersion != null &&
                _hostVersion < pluginVersion.MinHostVersion)
            {
                reason = $"主程式版本過舊 (需要 >= {pluginVersion.MinHostVersion})";
                return false;
            }

            // 檢查最大版本
            if (pluginVersion.MaxHostVersion != null &&
                _hostVersion > pluginVersion.MaxHostVersion)
            {
                reason = $"主程式版本過新 (需要 <= {pluginVersion.MaxHostVersion})";
                return false;
            }

            // 檢查必要功能
            if (pluginVersion.RequiredFeatures != null)
            {
                var missingFeatures = pluginVersion.RequiredFeatures
                    .Where(f => !_availableFeatures.Contains(f))
                    .ToList();

                if (missingFeatures.Any())
                {
                    reason = $"缺少必要功能: {string.Join(", ", missingFeatures)}";
                    return false;
                }
            }

            reason = "相容";
            return true;
        }
    }

    // ========== 使用範例 ==========

    class VersionCheckExample
    {
        static void Main()
        {
            // 設定主程式版本和支援的功能
            var versionManager = new VersionManager(
                new Version(1, 5, 0),
                "FileAccess", "NetworkAccess", "DatabaseAccess"
            );

            // 插件版本資訊
            var pluginVersion = new VersionInfo
            {
                PluginVersion = new Version(2, 0, 0),
                MinHostVersion = new Version(1, 0, 0),
                MaxHostVersion = new Version(2, 0, 0),
                RequiredFeatures = new List<string> { "FileAccess", "NetworkAccess" }
            };

            // 檢查相容性
            if (versionManager.IsCompatible(pluginVersion, out string reason))
            {
                Console.WriteLine("✓ 插件相容，可以載入");
            }
            else
            {
                Console.WriteLine($"✗ 插件不相容: {reason}");
            }
        }
    }
}
```

### 5.6 插件沙箱與權限控制

```csharp
using System;
using System.Collections.Generic;
using System.Security;
using ApiContracts;

namespace AdvancedFeatures
{
    /// <summary>
    /// 插件權限
    /// </summary>
    [Flags]
    public enum PluginPermissions
    {
        None = 0,
        FileRead = 1,
        FileWrite = 2,
        NetworkAccess = 4,
        DatabaseAccess = 8,
        SystemAccess = 16,
        All = FileRead | FileWrite | NetworkAccess | DatabaseAccess | SystemAccess
    }

    /// <summary>
    /// 權限管理器
    /// </summary>
    public class PluginSecurityManager
    {
        private readonly Dictionary<Guid, PluginPermissions> _pluginPermissions;

        public PluginSecurityManager()
        {
            _pluginPermissions = new Dictionary<Guid, PluginPermissions>();
        }

        /// <summary>
        /// 授予權限
        /// </summary>
        public void GrantPermission(Guid pluginId, PluginPermissions permissions)
        {
            if (!_pluginPermissions.ContainsKey(pluginId))
            {
                _pluginPermissions[pluginId] = PluginPermissions.None;
            }

            _pluginPermissions[pluginId] |= permissions;
        }

        /// <summary>
        /// 撤銷權限
        /// </summary>
        public void RevokePermission(Guid pluginId, PluginPermissions permissions)
        {
            if (_pluginPermissions.ContainsKey(pluginId))
            {
                _pluginPermissions[pluginId] &= ~permissions;
            }
        }

        /// <summary>
        /// 檢查權限
        /// </summary>
        public bool HasPermission(Guid pluginId, PluginPermissions permission)
        {
            if (!_pluginPermissions.TryGetValue(pluginId, out var granted))
            {
                return false;
            }

            return (granted & permission) == permission;
        }

        /// <summary>
        /// 驗證操作權限
        /// </summary>
        public void ValidatePermission(Guid pluginId, PluginPermissions required, string operation)
        {
            if (!HasPermission(pluginId, required))
            {
                throw new SecurityException(
                    $"插件 {pluginId} 沒有執行 '{operation}' 的權限 (需要: {required})"
                );
            }
        }
    }

    /// <summary>
    /// 受保護的插件包裝器
    /// </summary>
    public class SecurePluginWrapper
    {
        private readonly IPlugin _plugin;
        private readonly PluginSecurityManager _securityManager;

        public Guid PluginId => _plugin.PluginId;

        public SecurePluginWrapper(IPlugin plugin, PluginSecurityManager securityManager)
        {
            _plugin = plugin;
            _securityManager = securityManager;
        }

        /// <summary>
        /// 安全執行服務
        /// </summary>
        public object ExecuteService(string serviceName, Dictionary<string, object> parameters)
        {
            // 根據參數判斷需要的權限
            var requiredPermissions = DetermineRequiredPermissions(parameters);

            // 驗證權限
            _securityManager.ValidatePermission(_plugin.PluginId, requiredPermissions, serviceName);

            // 執行服務
            var service = GetService(serviceName);
            service.Initialize();

            try
            {
                return service.Execute(parameters);
            }
            finally
            {
                service.Dispose();
            }
        }

        private IApiService GetService(string serviceName)
        {
            foreach (var service in _plugin.GetServices())
            {
                if (service.ServiceName == serviceName)
                {
                    return service;
                }
            }

            throw new Exception($"找不到服務: {serviceName}");
        }

        private PluginPermissions DetermineRequiredPermissions(Dictionary<string, object> parameters)
        {
            var permissions = PluginPermissions.None;

            if (parameters.ContainsKey("filePath"))
                permissions |= PluginPermissions.FileRead;

            if (parameters.ContainsKey("saveFile"))
                permissions |= PluginPermissions.FileWrite;

            if (parameters.ContainsKey("url"))
                permissions |= PluginPermissions.NetworkAccess;

            if (parameters.ContainsKey("query"))
                permissions |= PluginPermissions.DatabaseAccess;

            return permissions;
        }
    }

    // ========== 使用範例 ==========

    class SecurityExample
    {
        static void Main()
        {
            var securityManager = new PluginSecurityManager();
            var loader = new ApiLoader();

            // 載入插件
            var plugin = loader.Load<IPlugin>(
                @"C:\Plugins\MyPlugin.dll",
                "MyPlugin.MainPlugin"
            );

            // 授予權限
            securityManager.GrantPermission(plugin.PluginId, PluginPermissions.FileRead);
            securityManager.GrantPermission(plugin.PluginId, PluginPermissions.NetworkAccess);

            // 建立安全包裝器
            var securePlugin = new SecurePluginWrapper(plugin, securityManager);

            try
            {
                // 這個操作需要 FileRead 權限 - 會成功
                var result1 = securePlugin.ExecuteService("ReadData", new Dictionary<string, object>
                {
                    { "filePath", @"C:\data.txt" }
                });
                Console.WriteLine("✓ 讀取檔案成功");

                // 這個操作需要 FileWrite 權限 - 會失敗
                var result2 = securePlugin.ExecuteService("WriteData", new Dictionary<string, object>
                {
                    { "saveFile", @"C:\output.txt" }
                });
            }
            catch (SecurityException ex)
            {
                Console.WriteLine($"✗ 權限錯誤: {ex.Message}");
            }
        }
    }
}
```

---

## 最佳實踐

### 6.1 錯誤處理與日誌記錄

```csharp
using System;
using System.IO;
using System.Text;

namespace BestPractices
{
    /// <summary>
    /// 日誌等級
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// 日誌記錄器
    /// </summary>
    public class Logger
    {
        private readonly string _logFilePath;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new object();

        public Logger(string logDirectory, LogLevel minLevel = LogLevel.Info)
        {
            _minLevel = minLevel;

            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            string fileName = $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
            _logFilePath = Path.Combine(logDirectory, fileName);
        }

        public void Log(LogLevel level, string message, Exception ex = null)
        {
            if (level < _minLevel)
                return;

            lock (_lock)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}");

                if (ex != null)
                {
                    sb.AppendLine($"例外類型: {ex.GetType().Name}");
                    sb.AppendLine($"例外訊息: {ex.Message}");
                    sb.AppendLine($"堆疊追蹤:\n{ex.StackTrace}");

                    if (ex.InnerException != null)
                    {
                        sb.AppendLine($"內部例外: {ex.InnerException.Message}");
                    }
                }

                string logEntry = sb.ToString();

                // 寫入檔案
                try
                {
                    File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
                }
                catch
                {
                    // 忽略日誌寫入失敗
                }

                // 同時輸出到控制台
                Console.Write(logEntry);
            }
        }

        public void Debug(string message) => Log(LogLevel.Debug, message);
        public void Info(string message) => Log(LogLevel.Info, message);
        public void Warning(string message) => Log(LogLevel.Warning, message);
        public void Error(string message, Exception ex = null) => Log(LogLevel.Error, message, ex);
        public void Fatal(string message, Exception ex = null) => Log(LogLevel.Fatal, message, ex);
    }

    /// <summary>
    /// 帶完整錯誤處理的插件管理器
    /// </summary>
    public class RobustPluginManager
    {
        private readonly Logger _logger;
        private readonly Dictionary<Guid, IPlugin> _plugins;

        public RobustPluginManager(Logger logger)
        {
            _logger = logger;
            _plugins = new Dictionary<Guid, IPlugin>();
        }

        /// <summary>
        /// 安全載入插件
        /// </summary>
        public bool TryLoadPlugin(string dllPath, string typeName, out IPlugin plugin, out string error)
        {
            plugin = null;
            error = null;

            try
            {
                _logger.Info($"嘗試載入插件: {dllPath}");

                // 驗證檔案
                if (!File.Exists(dllPath))
                {
                    error = $"檔案不存在: {dllPath}";
                    _logger.Warning(error);
                    return false;
                }

                // 載入插件
                var loader = new ApiLoader();
                plugin = loader.Load<IPlugin>(dllPath, typeName);

                if (plugin == null)
                {
                    error = "插件載入失敗，返回 null";
                    _logger.Error(error);
                    return false;
                }

                // 註冊插件
                _plugins[plugin.PluginId] = plugin;
                plugin.OnLoad();

                _logger.Info($"✓ 插件載入成功: {plugin.Name} v{plugin.PluginVersion}");
                return true;
            }
            catch (FileNotFoundException ex)
            {
                error = $"找不到檔案: {ex.FileName}";
                _logger.Error(error, ex);
                return false;
            }
            catch (TypeLoadException ex)
            {
                error = $"無法載入型別: {ex.TypeName}";
                _logger.Error(error, ex);
                return false;
            }
            catch (Exception ex)
            {
                error = $"載入失敗: {ex.Message}";
                _logger.Error(error, ex);
                return false;
            }
        }

        /// <summary>
        /// 安全執行服務
        /// </summary>
        public bool TryExecuteService(
            Guid pluginId,
            string serviceName,
            Dictionary<string, object> parameters,
            out object result,
            out string error)
        {
            result = null;
            error = null;

            try
            {
                _logger.Debug($"執行服務: {serviceName} (插件: {pluginId})");

                if (!_plugins.TryGetValue(pluginId, out var plugin))
                {
                    error = $"找不到插件: {pluginId}";
                    _logger.Warning(error);
                    return false;
                }

                IApiService service = null;
                foreach (var s in plugin.GetServices())
                {
                    if (s.ServiceName == serviceName)
                    {
                        service = s;
                        break;
                    }
                }

                if (service == null)
                {
                    error = $"找不到服務: {serviceName}";
                    _logger.Warning(error);
                    return false;
                }

                // 初始化
                if (!service.Initialize())
                {
                    error = "服務初始化失敗";
                    _logger.Error(error);
                    return false;
                }

                try
                {
                    // 執行
                    result = service.Execute(parameters);
                    _logger.Info($"✓ 服務執行成功: {serviceName}");
                    return true;
                }
                finally
                {
                    service.Dispose();
                }
            }
            catch (ArgumentException ex)
            {
                error = $"參數錯誤: {ex.Message}";
                _logger.Error(error, ex);
                return false;
            }
            catch (InvalidOperationException ex)
            {
                error = $"操作無效: {ex.Message}";
                _logger.Error(error, ex);
                return false;
            }
            catch (Exception ex)
            {
                error = $"執行失敗: {ex.Message}";
                _logger.Error(error, ex);
                return false;
            }
        }
    }
}
```

### 6.2 效能優化

```csharp
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;

namespace BestPractices
{
    /// <summary>
    /// 效能監視器
    /// </summary>
    public class PerformanceMonitor
    {
        private readonly ConcurrentDictionary<string, List<long>> _metrics;

        public PerformanceMonitor()
        {
            _metrics = new ConcurrentDictionary<string, List<long>>();
        }

        /// <summary>
        /// 測量執行時間
        /// </summary>
        public T Measure<T>(string operationName, Func<T> operation)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return operation();
            }
            finally
            {
                sw.Stop();
                RecordMetric(operationName, sw.ElapsedMilliseconds);
            }
        }

        public void Measure(string operationName, Action operation)
        {
            Measure<object>(operationName, () =>
            {
                operation();
                return null;
            });
        }

        private void RecordMetric(string name, long milliseconds)
        {
            _metrics.AddOrUpdate(
                name,
                new List<long> { milliseconds },
                (key, list) =>
                {
                    list.Add(milliseconds);
                    return list;
                }
            );
        }

        /// <summary>
        /// 獲取統計資訊
        /// </summary>
        public void PrintStatistics()
        {
            Console.WriteLine("\n========== 效能統計 ==========");
            foreach (var kvp in _metrics)
            {
                var values = kvp.Value;
                var avg = values.Average();
                var min = values.Min();
                var max = values.Max();

                Console.WriteLine($"{kvp.Key}:");
                Console.WriteLine($"  呼叫次數: {values.Count}");
                Console.WriteLine($"  平均: {avg:F2}ms");
                Console.WriteLine($"  最小: {min}ms");
                Console.WriteLine($"  最大: {max}ms");
            }
        }
    }

    /// <summary>
    /// 快取載入器 - 避免重複載入
    /// </summary>
    public class CachedPluginLoader
    {
        private readonly ConcurrentDictionary<string, Assembly> _assemblyCache;
        private readonly ConcurrentDictionary<string, Type> _typeCache;
        private readonly PerformanceMonitor _monitor;

        public CachedPluginLoader(PerformanceMonitor monitor = null)
        {
            _assemblyCache = new ConcurrentDictionary<string, Assembly>();
            _typeCache = new ConcurrentDictionary<string, Type>();
            _monitor = monitor ?? new PerformanceMonitor();
        }

        /// <summary>
        /// 載入組件（帶快取）
        /// </summary>
        public Assembly LoadAssembly(string dllPath)
        {
            return _monitor.Measure($"LoadAssembly:{Path.GetFileName(dllPath)}", () =>
            {
                return _assemblyCache.GetOrAdd(dllPath, path =>
                {
                    Console.WriteLine($"[快取未命中] 載入組件: {path}");
                    return Assembly.LoadFrom(path);
                });
            });
        }

        /// <summary>
        /// 載入型別（帶快取）
        /// </summary>
        public Type LoadType(string dllPath, string typeName)
        {
            string cacheKey = $"{dllPath}:{typeName}";

            return _monitor.Measure($"LoadType:{typeName}", () =>
            {
                return _typeCache.GetOrAdd(cacheKey, key =>
                {
                    Console.WriteLine($"[快取未命中] 載入型別: {typeName}");
                    var assembly = LoadAssembly(dllPath);
                    return assembly.GetType(typeName);
                });
            });
        }

        /// <summary>
        /// 建立實例
        /// </summary>
        public T CreateInstance<T>(string dllPath, string typeName) where T : class
        {
            return _monitor.Measure($"CreateInstance:{typeName}", () =>
            {
                var type = LoadType(dllPath, typeName);
                return Activator.CreateInstance(type) as T;
            });
        }

        /// <summary>
        /// 清除快取
        /// </summary>
        public void ClearCache()
        {
            _assemblyCache.Clear();
            _typeCache.Clear();
            Console.WriteLine("快取已清除");
        }

        /// <summary>
        /// 顯示快取統計
        /// </summary>
        public void PrintCacheStatistics()
        {
            Console.WriteLine("\n========== 快取統計 ==========");
            Console.WriteLine($"已快取組件: {_assemblyCache.Count}");
            Console.WriteLine($"已快取型別: {_typeCache.Count}");
            _monitor.PrintStatistics();
        }
    }
}
```

### 6.3 設計模式建議

```csharp
namespace BestPractices
{
    /// <summary>
    /// 單例模式 - 插件管理器
    /// </summary>
    public sealed class PluginManagerSingleton
    {
        private static readonly Lazy<PluginManagerSingleton> _instance =
            new Lazy<PluginManagerSingleton>(() => new PluginManagerSingleton());

        public static PluginManagerSingleton Instance => _instance.Value;

        private readonly Dictionary<Guid, IPlugin> _plugins;

        private PluginManagerSingleton()
        {
            _plugins = new Dictionary<Guid, IPlugin>();
        }

        public void RegisterPlugin(IPlugin plugin)
        {
            _plugins[plugin.PluginId] = plugin;
        }

        public IPlugin GetPlugin(Guid id)
        {
            return _plugins.TryGetValue(id, out var plugin) ? plugin : null;
        }
    }

    /// <summary>
    /// 工廠模式 - 插件工廠
    /// </summary>
    public interface IPluginFactory
    {
        IPlugin CreatePlugin(string dllPath, string typeName);
    }

    public class StandardPluginFactory : IPluginFactory
    {
        private readonly CachedPluginLoader _loader;

        public StandardPluginFactory(CachedPluginLoader loader)
        {
            _loader = loader;
        }

        public IPlugin CreatePlugin(string dllPath, string typeName)
        {
            return _loader.CreateInstance<IPlugin>(dllPath, typeName);
        }
    }

    /// <summary>
    /// 觀察者模式 - 插件事件通知
    /// </summary>
    public interface IPluginObserver
    {
        void OnPluginLoaded(IPlugin plugin);
        void OnPluginUnloaded(Guid pluginId);
        void OnServiceExecuted(Guid pluginId, string serviceName, object result);
    }

    public class PluginSubject
    {
        private readonly List<IPluginObserver> _observers = new List<IPluginObserver>();

        public void Attach(IPluginObserver observer)
        {
            _observers.Add(observer);
        }

        public void Detach(IPluginObserver observer)
        {
            _observers.Remove(observer);
        }

        protected void NotifyPluginLoaded(IPlugin plugin)
        {
            foreach (var observer in _observers)
            {
                observer.OnPluginLoaded(plugin);
            }
        }

        protected void NotifyPluginUnloaded(Guid pluginId)
        {
            foreach (var observer in _observers)
            {
                observer.OnPluginUnloaded(pluginId);
            }
        }

        protected void NotifyServiceExecuted(Guid pluginId, string serviceName, object result)
        {
            foreach (var observer in _observers)
            {
                observer.OnServiceExecuted(pluginId, serviceName, result);
            }
        }
    }

    /// <summary>
    /// 策略模式 - 載入策略
    /// </summary>
    public interface ILoadStrategy
    {
        IPlugin Load(string dllPath, string typeName);
    }

    public class SimpleLoadStrategy : ILoadStrategy
    {
        public IPlugin Load(string dllPath, string typeName)
        {
            var assembly = Assembly.LoadFrom(dllPath);
            var type = assembly.GetType(typeName);
            return Activator.CreateInstance(type) as IPlugin;
        }
    }

    public class CachedLoadStrategy : ILoadStrategy
    {
        private readonly CachedPluginLoader _loader;

        public CachedLoadStrategy(CachedPluginLoader loader)
        {
            _loader = loader;
        }

        public IPlugin Load(string dllPath, string typeName)
        {
            return _loader.CreateInstance<IPlugin>(dllPath, typeName);
        }
    }

    public class PluginLoaderContext
    {
        private ILoadStrategy _strategy;

        public PluginLoaderContext(ILoadStrategy strategy)
        {
            _strategy = strategy;
        }

        public void SetStrategy(ILoadStrategy strategy)
        {
            _strategy = strategy;
        }

        public IPlugin LoadPlugin(string dllPath, string typeName)
        {
            return _strategy.Load(dllPath, typeName);
        }
    }
}
```

---

## 常見問題

### 問題 1: 無法載入組件

**問題描述**:

```
System.IO.FileNotFoundException: Could not load file or assembly
```

**解決方案**:

```csharp
// 1. 檢查檔案是否存在
if (!File.Exists(dllPath))
{
    throw new FileNotFoundException($"DLL 檔案不存在: {dllPath}");
}

// 2. 檢查依賴項
// 將所有依賴的 DLL 放在同一目錄下

// 3. 使用 AssemblyResolve 事件處理依賴項
AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
{
    string assemblyName = new AssemblyName(args.Name).Name;
    string dllPath = Path.Combine(pluginDirectory, $"{assemblyName}.dll");

    if (File.Exists(dllPath))
    {
        return Assembly.LoadFrom(dllPath);
    }

    return null;
};

// 4. 使用 AssemblyLoadContext（.NET Core/5+）
public class PluginLoadContext : AssemblyLoadContext
{
    private AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath) : base(isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }
}
```

### 問題 2: 找不到型別

**問題描述**:

```
assembly.GetType(typeName) 返回 null
```

**解決方案**:

```csharp
// 1. 確認型別名稱包含完整命名空間
string typeName = "MyNamespace.MyPlugin";  // ✓ 正確
string typeName = "MyPlugin";              // ✗ 錯誤

// 2. 列出組件中所有型別
Assembly assembly = Assembly.LoadFrom(dllPath);
Console.WriteLine("組件中的所有型別:");
foreach (var type in assembly.GetTypes())
{
    Console.WriteLine($"  - {type.FullName}");
}

// 3. 使用反射查找
Type FindType(Assembly assembly, string typeName)
{
    // 精確匹配
    var type = assembly.GetType(typeName);
    if (type != null) return type;

    // 模糊匹配（不區分大小寫）
    return assembly.GetTypes()
        .FirstOrDefault(t => t.FullName.Equals(typeName, StringComparison.OrdinalIgnoreCase));
}
```

### 問題 3: 插件無法卸載

**問題描述**:

```
插件載入後無法完全卸載，記憶體未釋放
```

**解決方案**:

```csharp
// 使用 AssemblyLoadContext 的 isCollectible 參數
public class UnloadablePlugin
{
    private AssemblyLoadContext _loadContext;
    private WeakReference _pluginRef;

    public void Load(string dllPath, string typeName)
    {
        _loadContext = new AssemblyLoadContext("PluginContext", isCollectible: true);

        var assembly = _loadContext.LoadFromAssemblyPath(dllPath);
        var type = assembly.GetType(typeName);
        var plugin = Activator.CreateInstance(type);

        _pluginRef = new WeakReference(plugin);
    }

    public void Unload()
    {
        _loadContext?.Unload();
        _loadContext = null;

        // 強制垃圾回收
        for (int i = 0; i < 10; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            if (!_pluginRef.IsAlive)
            {
                Console.WriteLine("插件已成功卸載");
                break;
            }
        }
    }
}
```

### 問題 4: 版本衝突

**問題描述**:

```
主程式和插件使用不同版本的相同 DLL
```

**解決方案**:

```csharp
// 1. 使用 Binding Redirect (app.config)
<configuration>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="Newtonsoft.Json"
                          publicKeyToken="30ad4fe6b2a6aeed"
                          culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-13.0.0.0"
                         newVersion="13.0.3.0" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>

// 2. 使用獨立的 AssemblyLoadContext
public class IsolatedPluginLoader
{
    public IPlugin LoadIsolated(string dllPath, string typeName)
    {
        // 每個插件使用獨立的載入上下文
        var context = new AssemblyLoadContext($"Plugin_{Guid.NewGuid()}", isCollectible: true);

        var assembly = context.LoadFromAssemblyPath(dllPath);
        var type = assembly.GetType(typeName);

        return Activator.CreateInstance(type) as IPlugin;
    }
}

// 3. 使用介面隔離
// 定義共用介面專案，主程式和插件都參考同一版本
```

### 問題 5: 跨平台相容性

**問題描述**:

```
Windows 開發的插件無法在 Linux/Mac 上執行
```

**解決方案**:

```csharp
// 1. 使用 .NET 標準 API，避免平台特定代碼
public class CrossPlatformPlugin : IPlugin
{
    public void Initialize()
    {
        // ❌ 錯誤：使用 Windows 特定 API
        // var key = Microsoft.Win32.Registry.CurrentUser;

        // ✅ 正確：使用跨平台方式
        string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MyApp", "config.json"
        );
    }
}

// 2. 檢測運行平台
public class PlatformDetector
{
    public static string GetCurrentPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "Linux";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "macOS";

        return "Unknown";
    }

    public static string GetPluginPath()
    {
        string basePath = AppDomain.CurrentDomain.BaseDirectory;

        // 根據平台選擇不同的插件目錄
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? Path.Combine(basePath, "Plugins", "Windows")
            : Path.Combine(basePath, "Plugins", "Unix");
    }
}

// 3. 條件編譯
#if WINDOWS
    using Windows.UI.Notifications;
#elif LINUX
    using Linux.Notifications;
#endif

public class NotificationService
{
    public void ShowNotification(string message)
    {
#if WINDOWS
        // Windows 特定實現
        var notifier = ToastNotificationManager.CreateToastNotifier();
#elif LINUX
        // Linux 特定實現
        var notifier = new LinuxNotifier();
#else
        // 通用實現
        Console.WriteLine(message);
#endif
    }
}
// 2. 使用獨立的 AssemblyLoadContext
public class IsolatedPluginLoader
{
    private readonly Dictionary<Guid, AssemblyLoadContext> _contexts;

    public IsolatedPluginLoader()
    {
        _contexts = new Dictionary<Guid, AssemblyLoadContext>();
    }

    public IPlugin LoadIsolated(string dllPath, string typeName)
    {
        // 每個插件使用獨立的載入上下文
        var contextName = $"Plugin_{Guid.NewGuid()}";
        var context = new AssemblyLoadContext(contextName, isCollectible: true);

        var assembly = context.LoadFromAssemblyPath(dllPath);
        var type = assembly.GetType(typeName);

        var plugin = Activator.CreateInstance(type) as IPlugin;

        if (plugin != null)
        {
            _contexts[plugin.PluginId] = context;
        }

        return plugin;
    }

    public void UnloadPlugin(Guid pluginId)
    {
        if (_contexts.TryGetValue(pluginId, out var context))
        {
            context.Unload();
            _contexts.Remove(pluginId);

            // 強制垃圾回收
            for (int i = 0; i < 10; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
    }
}

// 3. 使用介面隔離
// 定義共用介面專案，主程式和插件都參考同一版本

// ApiContracts 專案 (.NET Standard 2.0 - 最大相容性)
// 這個專案應該:
// - 使用 .NET Standard 2.0
// - 不依賴任何第三方套件
// - 只定義介面和基本數據模型

// 範例:
namespace ApiContracts
{
    // 純介面定義，無實作
    public interface IVersionIndependentService
    {
        string GetData();
        void ProcessData(string data);
    }

    // 基本數據傳輸對象
    public class DataTransferObject
    {
        public string Id { get; set; }
        public string Value { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }
}

// 4. 版本隔離包裝器
public class VersionIsolationWrapper
{
    private readonly IPlugin _plugin;
    private readonly Version _pluginVersion;
    private readonly Version _hostVersion;

    public VersionIsolationWrapper(IPlugin plugin, Version hostVersion)
    {
        _plugin = plugin;
        _hostVersion = hostVersion;
        _pluginVersion = plugin.PluginVersion;
    }

    public object ExecuteWithCompatibility(string serviceName, Dictionary<string, object> parameters)
    {
        try
        {
            // 檢查版本相容性
            if (!IsCompatible())
            {
                throw new InvalidOperationException(
                    $"版本不相容: Host={_hostVersion}, Plugin={_pluginVersion}"
                );
            }

            // 執行服務
            var service = GetService(serviceName);
            return service.Execute(parameters);
        }
        catch (MissingMethodException ex)
        {
            // 方法不存在 - 可能是版本問題
            throw new InvalidOperationException(
                $"方法不存在，可能是版本不相容: {ex.Message}", ex
            );
        }
        catch (TypeLoadException ex)
        {
            // 類型載入失敗 - 可能是依賴版本問題
            throw new InvalidOperationException(
                $"類型載入失敗，請檢查依賴版本: {ex.Message}", ex
            );
        }
    }

    private bool IsCompatible()
    {
        // 主版本必須相同
        return _pluginVersion.Major == _hostVersion.Major;
    }

    private IApiService GetService(string serviceName)
    {
        return _plugin.GetServices()
            .FirstOrDefault(s => s.ServiceName == serviceName);
    }
}
```

### 問題 6: 效能問題

**問題描述**:

```
插件載入和執行效能低下
```

**解決方案**:

```csharp
// 1. 延遲載入（Lazy Loading）
public class LazyPluginManager
{
    private readonly Dictionary<Guid, Lazy<IPlugin>> _lazyPlugins;
    private readonly ApiLoader _loader;

    public LazyPluginManager()
    {
        _lazyPlugins = new Dictionary<Guid, Lazy<IPlugin>>();
        _loader = new ApiLoader();
    }

    public void RegisterPlugin(Guid id, string dllPath, string typeName)
    {
        _lazyPlugins[id] = new Lazy<IPlugin>(() =>
        {
            Console.WriteLine($"實際載入插件: {dllPath}");
            return _loader.Load<IPlugin>(dllPath, typeName);
        });
    }

    public IPlugin GetPlugin(Guid id)
    {
        if (_lazyPlugins.TryGetValue(id, out var lazy))
        {
            // 只有在首次訪問時才真正載入
            return lazy.Value;
        }
        return null;
    }

    public bool IsPluginLoaded(Guid id)
    {
        return _lazyPlugins.TryGetValue(id, out var lazy) && lazy.IsValueCreated;
    }

    public IEnumerable<Guid> GetRegisteredPluginIds()
    {
        return _lazyPlugins.Keys;
    }

    public IEnumerable<IPlugin> GetLoadedPlugins()
    {
        return _lazyPlugins.Values
            .Where(l => l.IsValueCreated)
            .Select(l => l.Value);
    }
}

// 2. 並行載入
public class ParallelPluginLoader
{
    private readonly ConcurrentDictionary<Guid, IPlugin> _plugins;
    private readonly SemaphoreSlim _semaphore;

    public ParallelPluginLoader(int maxConcurrency = 4)
    {
        _plugins = new ConcurrentDictionary<Guid, IPlugin>();
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
    }

    public async Task LoadPluginsParallelAsync(string[] pluginPaths)
    {
        var loadTasks = pluginPaths.Select(async path =>
        {
            await _semaphore.WaitAsync();
            try
            {
                return await LoadPluginAsync(path);
            }
            finally
            {
                _semaphore.Release();
            }
        });

        var plugins = await Task.WhenAll(loadTasks);

        foreach (var plugin in plugins.Where(p => p != null))
        {
            _plugins[plugin.PluginId] = plugin;
            plugin.OnLoad();
        }
    }

    private async Task<IPlugin> LoadPluginAsync(string dllPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var configPath = Path.Combine(
                    Path.GetDirectoryName(dllPath),
                    "plugin.json"
                );

                if (!File.Exists(configPath))
                {
                    Console.WriteLine($"找不到配置: {configPath}");
                    return null;
                }

                var config = JsonSerializer.Deserialize<PluginConfig>(
                    File.ReadAllText(configPath)
                );

                var loader = new ApiLoader();
                var plugin = loader.Load<IPlugin>(dllPath, config.TypeName);

                Console.WriteLine($"✓ 已載入: {plugin.Name}");
                return plugin;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ 載入失敗 ({dllPath}): {ex.Message}");
                return null;
            }
        });
    }

    public IEnumerable<IPlugin> GetAllPlugins()
    {
        return _plugins.Values;
    }
}

// 3. 預加載元數據
public class PluginMetadataCache
{
    private readonly Dictionary<string, PluginMetadata> _cache;
    private readonly string _pluginDirectory;

    public PluginMetadataCache(string pluginDirectory)
    {
        _cache = new Dictionary<string, PluginMetadata>();
        _pluginDirectory = pluginDirectory;
    }

    public void PreloadMetadata()
    {
        if (!Directory.Exists(_pluginDirectory))
            return;

        Console.WriteLine("預載入插件元數據...");

        // 只讀取 plugin.json，不載入 DLL
        foreach (var dir in Directory.GetDirectories(_pluginDirectory))
        {
            var configPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(configPath))
                continue;

            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<PluginConfig>(json);

                var metadata = new PluginMetadata
                {
                    Name = config.Name,
                    Version = config.Version,
                    Description = config.Description,
                    Author = config.Author,
                    DllPath = Path.Combine(dir, config.DllName),
                    TypeName = config.TypeName,
                    AutoLoad = config.AutoLoad,
                    Dependencies = config.Dependencies ?? new List<string>()
                };

                _cache[dir] = metadata;
                Console.WriteLine($"  ✓ {metadata.Name} v{metadata.Version}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 讀取元數據失敗 ({dir}): {ex.Message}");
            }
        }

        Console.WriteLine($"共找到 {_cache.Count} 個插件");
    }

    public bool ShouldLoadPlugin(string pluginPath)
    {
        // 根據元數據決定是否載入
        if (_cache.TryGetValue(pluginPath, out var metadata))
        {
            return metadata.AutoLoad;
        }
        return false;
    }

    public IEnumerable<PluginMetadata> GetAllMetadata()
    {
        return _cache.Values;
    }

    public PluginMetadata GetMetadata(string pluginPath)
    {
        _cache.TryGetValue(pluginPath, out var metadata);
        return metadata;
    }

    public IEnumerable<PluginMetadata> GetMetadataByAuthor(string author)
    {
        return _cache.Values.Where(m => m.Author == author);
    }
}

public class PluginMetadata
{
    public string Name { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public string Author { get; set; }
    public string DllPath { get; set; }
    public string TypeName { get; set; }
    public bool AutoLoad { get; set; }
    public List<string> Dependencies { get; set; }
}

// 4. 智能載入管理器 - 結合以上所有優化
public class SmartPluginLoader
{
    private readonly PluginMetadataCache _metadataCache;
    private readonly LazyPluginManager _lazyManager;
    private readonly ParallelPluginLoader _parallelLoader;
    private readonly PerformanceMonitor _monitor;

    public SmartPluginLoader(string pluginDirectory)
    {
        _metadataCache = new PluginMetadataCache(pluginDirectory);
        _lazyManager = new LazyPluginManager();
        _parallelLoader = new ParallelPluginLoader();
        _monitor = new PerformanceMonitor();
    }

    public async Task InitializeAsync()
    {
        // 步驟 1: 快速預載入元數據
        _monitor.Measure("PreloadMetadata", () =>
        {
            _metadataCache.PreloadMetadata();
        });

        // 步驟 2: 註冊需要延遲載入的插件
        var lazyPlugins = _metadataCache.GetAllMetadata()
            .Where(m => !m.AutoLoad)
            .ToList();

        foreach (var metadata in lazyPlugins)
        {
            var pluginId = Guid.NewGuid(); // 實際應從元數據獲取
            _lazyManager.RegisterPlugin(pluginId, metadata.DllPath, metadata.TypeName);
        }

        Console.WriteLine($"已註冊 {lazyPlugins.Count} 個延遲載入插件");

        // 步驟 3: 並行載入需要自動載入的插件
        var autoLoadPlugins = _metadataCache.GetAllMetadata()
            .Where(m => m.AutoLoad)
            .Select(m => m.DllPath)
            .ToArray();

        if (autoLoadPlugins.Length > 0)
        {
            await _monitor.MeasureAsync("ParallelLoad", async () =>
            {
                await _parallelLoader.LoadPluginsParallelAsync(autoLoadPlugins);
            });
        }

        Console.WriteLine($"已自動載入 {autoLoadPlugins.Length} 個插件");
        _monitor.PrintStatistics();
    }

    public IPlugin GetPlugin(Guid id)
    {
        // 先從已載入的查找
        var loaded = _parallelLoader.GetAllPlugins()
            .FirstOrDefault(p => p.PluginId == id);

        if (loaded != null)
            return loaded;

        // 再從延遲載入查找
        return _lazyManager.GetPlugin(id);
    }

    public IEnumerable<IPlugin> GetAllLoadedPlugins()
    {
        var parallel = _parallelLoader.GetAllPlugins();
        var lazy = _lazyManager.GetLoadedPlugins();
        return parallel.Concat(lazy);
    }
}

// 使用範例
public class PerformanceExample
{
    static async Task Main()
    {
        var loader = new SmartPluginLoader(@"C:\MyApp\Plugins");

        Console.WriteLine("========== 智能插件載入 ==========\n");

        // 快速初始化 - 元數據預載入 + 並行載入
        await loader.InitializeAsync();

        Console.WriteLine("\n應用程式已就緒！");
        Console.WriteLine("延遲載入的插件將在首次訪問時載入");
    }
}
```

### 問題 7: 記憶體洩漏

**問題描述**:

```
長時間運行後記憶體持續增長

```

```csharp
namespace BestPractices
{
    // 1. 實現 IDisposable 模式
    public class PluginContainer : IDisposable
    {
        private IPlugin _plugin;
        private AssemblyLoadContext _loadContext;
        private bool _disposed = false;

        public IPlugin Plugin => _plugin;
        public bool IsDisposed => _disposed;

        public PluginContainer(IPlugin plugin, AssemblyLoadContext loadContext)
        {
            _plugin = plugin;
            _loadContext = loadContext;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // 釋放受控資源
                    try
                    {
                        _plugin?.OnUnload();
                        Console.WriteLine($"已卸載插件: {_plugin?.Name}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"卸載插件時發生錯誤: {ex.Message}");
                    }
                    finally
                    {
                        _plugin = null;
                    }

                    // 卸載程序集
                    try
                    {
                        _loadContext?.Unload();
                        Console.WriteLine("已卸載 AssemblyLoadContext");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"卸載上下文時發生錯誤: {ex.Message}");
                    }
                    finally
                    {
                        _loadContext = null;
                    }
                }

                // 釋放非受控資源（如果有）
                _disposed = true;
            }
        }

        ~PluginContainer()
        {
            Dispose(false);
        }
    }

    // 2. 取消事件訂閱
    public class EventAwarePlugin : IPlugin
    {
        private PluginEventBus _eventBus;
        private Action<PluginEventArgs> _dataHandler;
        private Action<PluginEventArgs> _statusHandler;
        private readonly List<(string eventName, Action<PluginEventArgs> handler)> _subscriptions;

        public Guid PluginId { get; } = Guid.NewGuid();
        public string Name => "事件感知插件";
        public string Description => "示範正確的事件訂閱管理";
        public string Author => "Demo";
        public Version PluginVersion => new Version(1, 0, 0);

        public EventAwarePlugin()
        {
            _subscriptions = new List<(string, Action<PluginEventArgs>)>();
        }

        public void Initialize(PluginEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void OnLoad()
        {
            Console.WriteLine($"插件 {Name} 正在載入...");

            _dataHandler = HandleDataEvent;
            _statusHandler = HandleStatusEvent;

            Subscribe("DataProcessed", _dataHandler);
            Subscribe("StatusChanged", _statusHandler);

            Console.WriteLine($"已訂閱 {_subscriptions.Count} 個事件");
        }

        public void OnUnload()
        {
            Console.WriteLine($"插件 {Name} 正在卸載...");

            // ⚠️ 重要：取消所有訂閱以避免記憶體洩漏
            UnsubscribeAll();

            _dataHandler = null;
            _statusHandler = null;
            _eventBus = null;

            Console.WriteLine("已清理所有事件訂閱");
        }

        private void Subscribe(string eventName, Action<PluginEventArgs> handler)
        {
            _eventBus.Subscribe(eventName, handler);
            _subscriptions.Add((eventName, handler));
        }

        private void UnsubscribeAll()
        {
            foreach (var (eventName, handler) in _subscriptions)
            {
                _eventBus.Unsubscribe(eventName, handler);
                Console.WriteLine($"  取消訂閱: {eventName}");
            }
            _subscriptions.Clear();
        }

        private void HandleDataEvent(PluginEventArgs args)
        {
            Console.WriteLine($"[DataEvent] {args.Data}");
        }

        private void HandleStatusEvent(PluginEventArgs args)
        {
            Console.WriteLine($"[StatusEvent] {args.Data}");
        }

        public IEnumerable<IApiService> GetServices()
        {
            return Enumerable.Empty<IApiService>();
        }
    }

    // 3. 使用 WeakReference
    public class WeakPluginReference
    {
        private WeakReference<IPlugin> _pluginRef;
        private readonly Guid _pluginId;
        private readonly string _pluginName;

        public Guid PluginId => _pluginId;
        public string PluginName => _pluginName;
        public bool IsAlive => _pluginRef != null && _pluginRef.TryGetTarget(out _);

        public WeakPluginReference(IPlugin plugin)
        {
            _pluginRef = new WeakReference<IPlugin>(plugin);
            _pluginId = plugin.PluginId;
            _pluginName = plugin.Name;
        }

        public bool TryGetPlugin(out IPlugin plugin)
        {
            plugin = null;
            return _pluginRef != null && _pluginRef.TryGetTarget(out plugin);
        }

        public void Clear()
        {
            _pluginRef = null;
        }
    }

    public class WeakPluginCache
    {
        private readonly List<WeakPluginReference> _cache;
        private readonly object _lock = new object();

        public WeakPluginCache()
        {
            _cache = new List<WeakPluginReference>();
        }

        public void Add(IPlugin plugin)
        {
            lock (_lock)
            {
                _cache.Add(new WeakPluginReference(plugin));
            }
        }

        public IPlugin GetPlugin(Guid pluginId)
        {
            lock (_lock)
            {
                var weakRef = _cache.FirstOrDefault(w => w.PluginId == pluginId);
                if (weakRef != null && weakRef.TryGetPlugin(out var plugin))
                {
                    return plugin;
                }
                return null;
            }
        }

        public void Cleanup()
        {
            lock (_lock)
            {
                var deadRefs = _cache.Where(w => !w.IsAlive).ToList();
                foreach (var deadRef in deadRefs)
                {
                    _cache.Remove(deadRef);
                    Console.WriteLine($"清理已回收的插件引用: {deadRef.PluginName}");
                }
            }
        }

        public int GetAliveCount()
        {
            lock (_lock)
            {
                return _cache.Count(w => w.IsAlive);
            }
        }

        public int GetTotalCount()
        {
            lock (_lock)
            {
                return _cache.Count;
            }
        }
    }

    // 4. 定期清理
    public class PluginMemoryManager : IDisposable
    {
        private readonly WeakPluginCache _pluginCache;
        private readonly Dictionary<Guid, PluginContainer> _activePlugins;
        private readonly Timer _cleanupTimer;
        private readonly TimeSpan _cleanupInterval;
        private readonly TimeSpan _inactivityThreshold;
        private readonly Dictionary<Guid, DateTime> _lastAccessTimes;
        private readonly object _lock = new object();
        private bool _disposed = false;

        public PluginMemoryManager(
            TimeSpan? cleanupInterval = null,
            TimeSpan? inactivityThreshold = null)
        {
            _pluginCache = new WeakPluginCache();
            _activePlugins = new Dictionary<Guid, PluginContainer>();
            _lastAccessTimes = new Dictionary<Guid, DateTime>();

            _cleanupInterval = cleanupInterval ?? TimeSpan.FromMinutes(5);
            _inactivityThreshold = inactivityThreshold ?? TimeSpan.FromHours(1);

            _cleanupTimer = new Timer(
                callback: _ => PerformCleanup(),
                state: null,
                dueTime: _cleanupInterval,
                period: _cleanupInterval
            );

            Console.WriteLine($"記憶體管理器已啟動 (清理間隔: {_cleanupInterval.TotalMinutes}分鐘)");
        }

        public void RegisterPlugin(IPlugin plugin, AssemblyLoadContext loadContext)
        {
            lock (_lock)
            {
                var container = new PluginContainer(plugin, loadContext);
                _activePlugins[plugin.PluginId] = container;
                _lastAccessTimes[plugin.PluginId] = DateTime.Now;
                _pluginCache.Add(plugin);

                Console.WriteLine($"註冊插件: {plugin.Name}");
            }
        }

        public void AccessPlugin(Guid pluginId)
        {
            lock (_lock)
            {
                if (_lastAccessTimes.ContainsKey(pluginId))
                {
                    _lastAccessTimes[pluginId] = DateTime.Now;
                }
            }
        }

        public void UnloadPlugin(Guid pluginId)
        {
            lock (_lock)
            {
                if (_activePlugins.TryGetValue(pluginId, out var container))
                {
                    container.Dispose();
                    _activePlugins.Remove(pluginId);
                    _lastAccessTimes.Remove(pluginId);

                    Console.WriteLine($"已卸載插件 ID: {pluginId}");
                }
            }
        }

        private void PerformCleanup()
        {
            try
            {
                Console.WriteLine("\n========== 開始記憶體清理 ==========");
                Console.WriteLine($"時間: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

                lock (_lock)
                {
                    var now = DateTime.Now;
                    var inactivePlugins = _lastAccessTimes
                        .Where(kvp => now - kvp.Value > _inactivityThreshold)
                        .Select(kvp => kvp.Key)
                        .ToList();

                    Console.WriteLine($"找到 {inactivePlugins.Count} 個閒置插件");

                    foreach (var pluginId in inactivePlugins)
                    {
                        if (_activePlugins.TryGetValue(pluginId, out var container))
                        {
                            var inactiveTime = now - _lastAccessTimes[pluginId];
                            Console.WriteLine($"  卸載閒置插件: {container.Plugin?.Name} (閒置時間: {inactiveTime.TotalMinutes:F1}分鐘)");

                            UnloadPlugin(pluginId);
                        }
                    }

                    _pluginCache.Cleanup();
                    Console.WriteLine($"弱引用緩存: 存活={_pluginCache.GetAliveCount()}, 總計={_pluginCache.GetTotalCount()}");
                    Console.WriteLine($"當前活動插件數: {_activePlugins.Count}");

                    var memoryBefore = GC.GetTotalMemory(false);

                    Console.WriteLine("執行垃圾回收...");
                    GC.Collect(2, GCCollectionMode.Aggressive);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Aggressive);

                    var memoryAfter = GC.GetTotalMemory(false);
                    var memoryFreed = memoryBefore - memoryAfter;

                    Console.WriteLine($"記憶體回收: {FormatBytes(memoryFreed)} (之前: {FormatBytes(memoryBefore)}, 之後: {FormatBytes(memoryAfter)})");
                }

                Console.WriteLine("========== 記憶體清理完成 ==========\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"記憶體清理時發生錯誤: {ex.Message}");
            }
        }

        public void ForceCleanup()
        {
            Console.WriteLine("執行強制清理...");
            PerformCleanup();
        }

        public MemoryStatistics GetMemoryStatistics()
        {
            lock (_lock)
            {
                return new MemoryStatistics
                {
                    ActivePluginCount = _activePlugins.Count,
                    WeakReferencesAlive = _pluginCache.GetAliveCount(),
                    WeakReferencesTotal = _pluginCache.GetTotalCount(),
                    TotalMemory = GC.GetTotalMemory(false),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                };
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cleanupTimer?.Dispose();

                lock (_lock)
                {
                    foreach (var container in _activePlugins.Values)
                    {
                        container.Dispose();
                    }
                    _activePlugins.Clear();
                    _lastAccessTimes.Clear();
                }

                _disposed = true;
                Console.WriteLine("記憶體管理器已釋放");
            }
        }
    }

    public class MemoryStatistics
    {
        public int ActivePluginCount { get; set; }
        public int WeakReferencesAlive { get; set; }
        public int WeakReferencesTotal { get; set; }
        public long TotalMemory { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }

        public override string ToString()
        {
            return $@"
記憶體統計:
  活動插件數: {ActivePluginCount}
  弱引用 (存活/總計): {WeakReferencesAlive}/{WeakReferencesTotal}
  總記憶體: {FormatBytes(TotalMemory)}
  GC 次數 - Gen0: {Gen0Collections}, Gen1: {Gen1Collections}, Gen2: {Gen2Collections}
";
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
```

## 7. Executor 專案落地（Spash API 動態載入）

> 本段落針對 Executor 實際流程，補齊「動態載入 API」在專案中的落地方式。

### 7.1 載入流程概要 (Runtime Flow)

1. `SettingsView` 讀取 `../version.json` 的 `APIs` 節點，建立可選 API 清單。
2. 使用者在設定中選擇 API → `ConfigManager` 寫入 `selected_api`。
3. `SpashApiInvoker.EnsureInitialized()` 觸發載入：
   - 以 `AppPaths.AppDirectory\APIs` 為基準尋找 API 資料夾。
   - 依 `BuildApiTypeCandidates()` 建立 DLL 名稱候選（例：`SpashAPI{ApiName}`）。
   - 找到 DLL 後載入 `Assembly`，同時安裝 `AssemblyResolve` 與 Native DLL 搜尋路徑。
4. `API.cs` 透過 `SpashApiInvoker` 反射呼叫 `ExecuteScript / AttachAPI / IsAttached` 等方法。

### 7.2 目錄與檔名規則 (Folder & Naming)

- 根目錄：`AppDirectory\APIs`
- 資料夾候選：
  - `APIs\{API名稱}`
  - `APIs\SpashAPI - {API名稱}`
- DLL 檔名建議：`SpashAPI{API名稱}.dll`
- 若找不到對應名稱，會退回 `SpashAPI*.dll` 的第一個符合檔。

**範例結構**

```
AppDirectory/
└── APIs/
    ├── SpashAPI - Velocity/
    │   ├── SpashAPIVelocity.dll
    │   ├── SpashAPIVelocity.pdb
    │   └── Dependencies.dll
    └── SpashAPI - Xeno/
        ├── SpashAPIXeno.dll
        └── Dependencies.dll
```

### 7.3 version.json 內容 (API List)

`version.json` 的 `APIs` 節點目前用於 UI 顯示與 API 清單來源，`sUNC / UNC` 為顯示資訊。

```json
{
  "AppVersion": "1.0.1",
  "APIs": {
    "Velocity": { "sUNC": "92%", "UNC": "92%" },
    "Xeno": { "sUNC": "86%", "UNC": "86%" }
  }
}
```

#### 7.3.1 路徑與命名對應 (對照 112-1)

`APIs` 節點的**鍵名**就是 API 名稱來源，對應的目錄會使用這個名稱或包含這個名稱的資料夾。

| version.json API 名稱 | 實際資料夾路徑                                  | 主 DLL 名稱            |
| --------------------- | ----------------------------------------------- | ---------------------- |
| `SpashAPI - Velocity` | `APIs\SpashAPI - Velocity\`                     | `SpashAPIVelocity.dll` |
| `SpashAPI - Solara`   | `APIs\SpashAPI - Solara\`                       | `SpashAPISolara.dll`   |
| `Velocity`            | `APIs\Velocity\` 或 `APIs\SpashAPI - Velocity\` | `SpashAPIVelocity.dll` |

> 參考：`SpashApiInvoker.BuildApiTypeCandidates()` 會把 API 名稱轉為 `SpashAPI{API名稱}` 的 DLL 候選。

### 7.4 新增/更新 API 步驟 (Step-by-step)

1. 準備 API DLL 與所有相依 DLL（建議 x64）。
2. 放入 `AppDirectory\APIs\SpashAPI - {API名稱}`。
3. 主 DLL 以 `SpashAPI{API名稱}.dll` 命名。
4. 更新 `version.json` 的 `APIs` 節點。
5. 重新開啟 Wave 或重開 API 選單以刷新清單。
6. 切換 API 時若 Roblox 已附加，程式會提示並重啟 Roblox。

### 7.5 Executor 常見錯誤 (Troubleshooting)

- `APIs folder not found`: `AppDirectory\APIs` 不存在或路徑錯誤。
- `No SpashAPI DLL found`: 資料夾內沒有符合命名規則的 DLL。
- `MSB3270`: API DLL 架構與主程式不一致（MSIL vs AMD64）。
- `AssemblyResolve not found`: 依賴 DLL 未放在同目錄/子目錄。

### 7.6 檢查清單 (Checklist)

- [ ] `APIs` 目錄存在
- [ ] API 資料夾命名正確
- [ ] 主 DLL 名稱符合 `SpashAPI{API名稱}.dll`
- [ ] 相依 DLL 已放同目錄或 `bin/` 內
- [ ] `version.json` 有對應 API 條目
- [ ] `config.cfg` 的 `selected_api` 值與 API 名稱相符
- [ ] DLL 架構與主程式一致 (x64 建議)

## 8. 附錄

### 8.1 完整的 plugin.json 配置範例

```json
{
  "name": "我的示範插件",
  "dllName": "ExternalApi.dll",
  "typeName": "ExternalApi.MyPlugin",
  "version": "1.0.0",
  "description": "這是一個示範插件，提供資料處理和計算服務",
  "author": "您的名字",
  "email": "your.email@example.com",
  "website": "https://example.com",
  "license": "MIT",

  "autoLoad": true,
  "enabled": true,
  "priority": 100,

  "dependencies": ["Newtonsoft.Json >= 13.0.0", "System.Text.Json >= 8.0.0"],

  "permissions": ["FileRead", "FileWrite", "NetworkAccess"],

  "compatibility": {
    "minHostVersion": "1.0.0",
    "maxHostVersion": "2.0.0",
    "targetFramework": "net10.0",
    "requiredFeatures": ["Database", "Logging", "EventBus"]
  },

  "settings": {
    "timeout": 30000,
    "retryCount": 3,
    "enableCache": true,
    "cacheSize": 100,
    "logLevel": "Info"
  },

  "services": [
    {
      "name": "資料處理服務",
      "version": "1.0.0",
      "description": "提供資料處理功能",
      "endpoints": ["process", "calculate", "query"]
    },
    {
      "name": "進階計算服務",
      "version": "2.0.0",
      "description": "提供數學計算功能",
      "endpoints": ["add", "subtract", "multiply", "divide"]
    }
  ],

  "localization": {
    "defaultLanguage": "zh-TW",
    "supportedLanguages": ["zh-TW", "en-US", "ja-JP"]
  },

  "metadata": {
    "tags": ["data", "processing", "calculation"],
    "category": "Utility",
    "icon": "plugin-icon.png",
    "screenshots": ["screenshot1.png", "screenshot2.png"]
  },

  "customProperties": {
    "maxConcurrentRequests": 10,
    "dataSourceType": "SQL",
    "connectionString": "Server=localhost;Database=MyDb;"
  }
}
```

### 8.2 完整專案結構範例

```
MyWpfApplication/
│
├── Solution Items/
│   ├── README.md
│   ├── LICENSE
│   └── .gitignore
│
├── MyWpfApp/                          # WPF 主程式
│   ├── App.xaml
│   ├── App.xaml.cs
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   ├── MyWpfApp.csproj
│   │
│   ├── Services/
│   │   ├── ApiLoader.cs
│   │   ├── AdvancedApiLoader.cs
│   │   ├── PluginManager.cs
│   │   ├── CachedPluginLoader.cs
│   │   ├── PluginMemoryManager.cs
│   │   └── PluginSecurityManager.cs
│   │
│   ├── Models/
│   │   ├── PluginConfig.cs
│   │   ├── PluginInfo.cs
│   │   ├── PluginMetadata.cs
│   │   └── MemoryStatistics.cs
│   │
│   ├── Views/
│   │   ├── InputDialog.xaml
│   │   ├── InputDialog.xaml.cs
│   │   ├── ServiceParameterDialog.xaml
│   │   ├── ServiceParameterDialog.xaml.cs
│   │   ├── PluginDetailsWindow.xaml
│   │   └── PluginDetailsWindow.xaml.cs
│   │
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── PluginViewModel.cs
│   │   └── ServiceViewModel.cs
│   │
│   ├── Utils/
│   │   ├── Logger.cs
│   │   ├── PerformanceMonitor.cs
│   │   ├── TextBoxWriter.cs
│   │   └── PlatformDetector.cs
│   │
│   ├── Converters/
│   │   ├── NullToBoolConverter.cs
│   │   ├── BoolToVisibilityConverter.cs
│   │   └── BytesToStringConverter.cs
│   │
│   ├── Resources/
│   │   ├── Icons/
│   │   ├── Images/
│   │   └── Styles/
│   │       ├── ButtonStyles.xaml
│   │       ├── TextBlockStyles.xaml
│   │       └── Colors.xaml
│   │
│   └── Configuration/
│       ├── appsettings.json
│       └── app.config
│
├── ApiContracts/                      # 接口契約專案
│   ├── ApiContracts.csproj
│   ├── IApiService.cs
│   ├── IPlugin.cs
│   ├── PluginMetadata.cs
│   │
│   ├── Interfaces/
│   │   ├── ILogger.cs
│   │   ├── IEventBus.cs
│   │   └── ISecurityManager.cs
│   │
│   ├── DataModels/
│   │   ├── ServiceRequest.cs
│   │   ├── ServiceResponse.cs
│   │   ├── PluginEventArgs.cs
│   │   └── ErrorInfo.cs
│   │
│   ├── Enums/
│   │   ├── PluginStatus.cs
│   │   ├── ServiceStatus.cs
│   │   └── LogLevel.cs
│   │
│   └── Attributes/
│       ├── PluginAttribute.cs
│       ├── ServiceAttribute.cs
│       └── PermissionAttribute.cs
│
├── ExternalApi/                       # 外部 API 實作專案
│   ├── ExternalApi.csproj
│   ├── MyPlugin.cs
│   ├── plugin.json
│   │
│   ├── Services/
│   │   ├── MyDataService.cs
│   │   ├── MyCalculatorService.cs
│   │   ├── FileProcessingService.cs
│   │   └── NetworkService.cs
│   │
│   ├── Helpers/
│   │   ├── DataValidator.cs
│   │   ├── CacheManager.cs
│   │   └── ConfigReader.cs
│   │
│   └── Resources/
│       ├── Localization/
│       │   ├── zh-TW.json
│       │   └── en-US.json
│       └── Templates/
│
├── AnotherPlugin/                     # 另一個插件專案範例
│   ├── AnotherPlugin.csproj
│   ├── AnotherPlugin.cs
│   ├── plugin.json
│   └── Services/
│       └── CustomService.cs
│
├── PluginsOutput/                     # 編譯後的插件目錄
│   ├── Plugin1/
│   │   ├── ExternalApi.dll
│   │   ├── ExternalApi.pdb
│   │   ├── ApiContracts.dll
│   │   ├── plugin.json
│   │   ├── plugin-icon.png
│   │   └── Dependencies/
│   │       ├── Newtonsoft.Json.dll
│   │       └── Other.dll
│   │
│   └── Plugin2/
│       ├── AnotherPlugin.dll
│       ├── AnotherPlugin.pdb
│       ├── ApiContracts.dll
│       ├── plugin.json
│       └── Dependencies/
│
├── Tests/                             # 測試專案
│   ├── Tests.csproj
│   │
│   ├── Unit/
│   │   ├── ApiLoaderTests.cs
│   │   ├── PluginManagerTests.cs
│   │   ├── MemoryManagerTests.cs
│   │   └── SecurityManagerTests.cs
│   │
│   ├── Integration/
│   │   ├── PluginLoadingTests.cs
│   │   ├── ServiceExecutionTests.cs
│   │   └── EventBusTests.cs
│   │
│   └── Fixtures/
│       ├── TestPlugin.cs
│       └── TestData/
│
├── Benchmarks/                        # 效能測試
│   ├── Benchmarks.csproj
│   ├── LoadingBenchmarks.cs
│   └── MemoryBenchmarks.cs
│
└── Documentation/                     # 文檔
    ├── API.DynamicLoad.md
    ├── PluginDevelopmentGuide.md
    ├── Troubleshooting.md
    ├── BestPractices.md
    ├── API-Reference.md
    └── Images/
        ├── architecture-diagram.png
        └── ui-screenshot.png
```

### 8.3 NuGet 套件建議

```csharp
<!-- MyWpfApp.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <!-- JSON 序列化 -->
    <PackageReference Include="System.Text.Json" Version="8.0.0" />
    <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />

    <!-- 依賴注入 -->
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />

    <!-- 日誌 -->
    <PackageReference Include="Serilog" Version="3.1.1" />
    <PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
    <PackageReference Include="Serilog.Extensions.Logging" Version="8.0.0" />

    <!-- 配置 -->
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="8.0.0" />

    <!-- MVVM -->
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />

    <!-- UI 增強 -->
    <PackageReference Include="MaterialDesignThemes" Version="4.9.0" />
    <PackageReference Include="MaterialDesignColors" Version="2.1.4" />

    <!-- 反射優化 -->
    <PackageReference Include="System.Reflection.Metadata" Version="8.0.0" />

    <!-- 測試 -->
    <PackageReference Include="xunit" Version="2.6.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.4" />
    <PackageReference Include="Moq" Version="4.20.70" />
    <PackageReference Include="FluentAssertions" Version="6.12.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\ApiContracts\ApiContracts.csproj" />
  </ItemGroup>

  <!-- 自動複製插件到輸出目錄 -->
  <ItemGroup>
    <None Update="Plugins\**\*">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>
```

```csharp
<!-- ApiContracts.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- 最小依賴，保持最大相容性 -->
    <PackageReference Include="System.ComponentModel.Annotations" Version="5.0.0" />
  </ItemGroup>

</Project>
```

```csharp
<!-- ApiContracts.csproj -->
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <!-- 最小依賴，保持最大相容性 -->
    <PackageReference Include="System.ComponentModel.Annotations" Version="5.0.0" />
  </ItemGroup>

</Project>
```
