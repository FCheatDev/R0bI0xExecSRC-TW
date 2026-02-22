# first.md — WPF .NET 10 開發助手指令文檔

---

## 文檔說明

此文檔的功能是告訴你該做或是不該做。

---

## 專案資料來源定義

此專案分為兩種資料來源，路徑如下：

- `Docs\Internal`：使用 API 取得資料，規則遵循此 first.md 所有定義。
- `Docs\External`：使用偏移量（Offsets）直接讀取記憶體資料，偏移量定義來自 `offsets.h` / `offsets.json` / `offsets.py`。

### External 偏移量使用規則

1. 偏移量來源以 `offsets.h`（C++ 格式）、`offsets.json`、`offsets.py` 為準，三份內容一致。
2. 在 C# 中使用偏移量時，統一以 `ulong` 或 `nint` 型別定義常數，對應 `offsets.h` 的 `uintptr_t`。
3. 偏移量不可寫死在業務邏輯中，需集中定義在獨立的靜態類別（例如 `Offsets.cs`），結構對應 `offsets.h` 的 namespace 層級。
4. 版本號（`ROBLOX_VERSION`）需一併記錄在 `Offsets.cs` 頂部，方便日後更新對版本。
5. 偏移量若有更新，只改 `Offsets.cs`，業務邏輯不需動。

---

cmd.md / first.md 都需寫入記憶體，僅此專案生效。
此文檔不會再次丟給你，除非有新的內容。
遇到不懂的指令時，請來查閱 first.md，因為有可能是忘記給你。
所有縮寫指令定義可在 cmd.md 找到。

---

## 繼續執行指令

當收到以下任意回覆時，表示「繼續執行項目或未完成的需求」（對方打字打太快）：

- `c`
- `繼續`
- `際遇`
- `ru4mv4`

---

## 指令速覽（詳細定義請見 cmd.md）

| 指令            | 說明                                                      |
| --------------- | --------------------------------------------------------- |
| `{f:a_n}`       | 允許搜索並使用網路上的資料                                |
| `{f:lang}`      | 需要中英雙語，存入 en.json / zh.json，不可寫死字串        |
| `{f:skip_1}`    | 忽略 todo.md 第一點                                       |
| `{f:skip_2}`    | 忽略 todo.md 第二點                                       |
| `{e:仿execute}` | 模仿 editor page execute 按鈕動畫（同 BounceButtonStyle） |
| `{f:b_e-1}`     | 套用 BounceButtonStyle 按鈕參數（詳見 cmd.md）            |

---

## 基本行為規則

1. 不准使用 emoji。
2. 不懂的地方要問，不要盲目去做。
3. 收到指令後馬上分析並執行，不拖延。
4. 每次回應都直接針對用戶需求，不偏離主題。
5. 以繁體中文回答，不管對方回的是否為其他語言。
6. 如果你有什麼想法，可以隨時提出。
7. 如果你覺得有問題，可以隨時提出。
8. 100% 完成用戶提出的所有需求。
9. 請不要寫了結果還是錯的或跟原本結果一樣，請盡可能修復需求產生出來的問題。
10. 遵循現有程式碼風格，保持一致性。
11. 預先考慮可能出現的編譯問題。

---

## todo.md 使用規則

1. todo.md 是要你完成的選項。
2. 如果只丟給你 todo.md，你就完成上面的項目。
3. 完成需求後，在項目的 "1." 後面寫 "[✓]"，而不是馬上標記。
   - 例子: 1. [✓] 這個文檔的功能是告訴你該做或是不該做
4. 名稱定義：todo.md 裡面的 1./2./3.... 稱為需求/項目。
5. 有時候會忘記 [✓]，所以完成後要檢查一次程式碼是否真的有做到該需求，確認後再標記。

---

## 測試規則

1. 任何改動都需要跑一次測試。
2. 測試命名格式：`方法名稱_情境_預期結果`
   - 例子：`SaveCommand_WhenDataIsValid_ShouldCallRepository`
3. 每個 ViewModel 方法都附上測試範例（使用 xUnit）。

---

## 專案技術規範

### 基本環境

- 此項目是 WPF 編寫，.NET 版本是 10（TargetFramework: net10.0-windows）。
- 語言版本使用 C# 14（LangVersion: preview）。
- 啟用 Nullable 與 ImplicitUsings。

### .csproj 最低配置

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>preview</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.*" />
  </ItemGroup>
</Project>
```

### NuGet 套件

- 如果還需要其他 NuGet，自己去下載，不需等待確認。
- 優先使用 CommunityToolkit.Mvvm（`[ObservableProperty]`、`[RelayCommand]`）。

---

## MVVM 架構規則

- 嚴格遵守 Model / View / ViewModel 分離原則。
- View（XAML）：絕不寫業務邏輯，僅做 UI 呈現與資料綁定。
- ViewModel：不直接參考任何 WPF UI 控制項（不 using System.Windows.Controls）。
- Model：純資料與業務規則，不依賴 UI 層。
- 不在 ViewModel 中直接呼叫 `MessageBox.Show()` 或 `new Window()`，改用 IDialogService 介面抽象。

---

## 樣式規則

### 按鈕 Hover 禁止出現白色

- WPF 預設 Button Template 會在 IsMouseOver 時套用 Highlight/Brush，造成白色或亮色的底。
- 任何按鈕（包含像設定列表那種整行可點的 Button）在 Hover 時，背景與邊框顏色必須保持設計稿，不可變白。
- 做法（擇一或混用）：
  a. 設定 `FocusVisualStyle={x:Null}`，避免焦點框造成額外白邊。
  b. 自己寫 Button Style/ControlTemplate，並在 Triggers 裡把 IsMouseOver 的 Background/BorderBrush 明確指定成同一色。
  c. 若按鈕只是用來讓整行可點，可將 `Background=Transparent`、`BorderThickness=0`，並且仍需避免 Hover 套到預設 Template。
- 檢查點：
  a. 滑鼠移到按鈕上時，不應出現任何白色遮罩/白底/白邊。
  b. 按下按鈕時（Pressed）若有縮放動畫可以保留，但不可出現白色高亮。

---

## config.cfg 配置定義

- 如果前面有 "WaveUI" / "SynxUI" 的話，那個就是針對 Wave 的（以後可能還會改）。
- 如果前面沒有加上 "WaveUI" / "SynxUI" 的話，那就是全局配置。
- 例子：
  - `language=zh` → 全局
  - `theme=WaveUI-2025` → 全局
  - `discord_rpc=true` → 全局
  - `font=` → 全局
  - `opacity=0.8` → 全局
  - `topmost=true` → 全局
  - `WaveUI_skip_load_app=true` → Wave
  - `WaveUI_small_wave_effect=fade` → Wave
  - `WaveUI_small_wave_opacity=0.5` → Wave

---

## 備份規則

- 如果要備份檔案，不要放到 `Executor/Executor`，而是放到 `Executor/Backup`。
- 備份檔案需在最上面寫上備份日期與原因。
- 如果要還原，請刪掉備份日期內容。
- 記得把 Backup 資料夾不納入 git（加入 .gitignore）。

---

## 圖片資源規則

- 可以忽略圖片來源，因為已經放到 Output 資料夾。

---

## 專案結構範本

當建立新專案時，使用以下結構：

```
MyApp/
├── MyApp.sln
├── MyApp/
│   ├── MyApp.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── Models/
│   ├── ViewModels/
│   │   └── MainViewModel.cs
│   ├── Views/
│   │   └── MainWindow.xaml
│   ├── Services/
│   ├── Repositories/
│   ├── Helpers/
│   ├── Backup/                  （不納入 git）
│   └── Resources/
│       ├── Styles.xaml
│       └── Colors.xaml
└── MyApp.Tests/
    ├── MyApp.Tests.csproj
    ├── ViewModels/
    └── Services/
```

---

## 常見問題標準解法

| 問題       | 推薦解法                                       |
| ---------- | ---------------------------------------------- |
| 命令綁定   | `[RelayCommand]` (CommunityToolkit.Mvvm)       |
| 屬性通知   | `[ObservableProperty]` (CommunityToolkit.Mvvm) |
| 依賴注入   | Microsoft.Extensions.DependencyInjection       |
| 導覽       | 自訂 NavigationService 或 Messenger            |
| 對話框     | IDialogService 介面抽象                        |
| 設定檔     | System.Text.Json + IOptions<T>                 |
| 資料庫     | Entity Framework Core 10                       |
| 非同步載入 | LoadedAsync 模式 + IsBusy 屬性綁定             |
| 剪貼簿     | .NET 10 新 Clipboard JSON API                  |

---

## 禁止事項

- 不在 Code-Behind 寫業務邏輯。
- 不直接在 ViewModel 中呼叫 `MessageBox.Show()` 或 `new Window()`。
- 不使用 `BinaryFormatter`。
- 不在非 UI 執行緒直接更新 UI 元素。
- 不忽略 `IDisposable`（訂閱事件要記得取消訂閱）。
- 不使用 emoji。
- 不盲目執行不懂的指令，先詢問。
- 不把備份檔案放到 `Executor/Executor`。

---

_最後更新：2026 年 2 月_
_此文檔僅此專案生效_
