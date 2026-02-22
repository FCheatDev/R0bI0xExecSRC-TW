# 第三方 API 整合與切換說明（Executor）

本文件說明 Executor 目前對第三方注入 API（例如 SpashAPI Velocity / Xeno）的整合方式，以及在軟體內「隨時切換」API 時的實際行為與限制。

## 1. 概念總覽

Executor 以「API Provider」的方式工作。

- API 清單來源：`version.json` 的 `APIs` 區塊
- API DLL 放置位置：`{AppDirectory}/APIs/{ApiName}/`
- 使用者選擇的 API：寫入 `config.cfg` 的 `selected_api`
- 實際呼叫：統一走 `Executor.API`（例如 `AttachAsync`、`ExecuteScriptAsync`、`IsAttached`）
- 反射呼叫：由 `SpashApiInvoker` 依目前選擇的 API 動態載入 DLL，並尋找對應類型/方法

## 2. 檔案與資料夾

### 2.1 AppDirectory

程式內會使用 `AppPaths.AppDirectory` 作為主要根目錄（Settings 的 UI 也會顯示/提供開啟）。

### 2.2 APIs 資料夾

每個 API Provider 應放在：

- `{AppDirectory}/APIs/{ApiName}/`

此資料夾內至少要有一個 `.dll`，且「主 DLL」的選擇規則大致如下（見 `ApiLoader.ChooseApiDllPath`）：

- 優先找檔名（不含副檔名）與資料夾名 `{ApiName}` 完全相同者
- 否則優先找 `SpashAPI*` 開頭
- 否則找包含 `{ApiName}` 的檔
- 再不行就退回選最短檔名（通常比較像主 DLL）

注意：若資料夾內同時存在多個依賴 DLL，請確保主 DLL 命名清楚，避免誤載。

## 3. version.json（API 清單來源）

Settings 內的 API 選擇清單是從 `version.json` 讀取。

程式會嘗試找到 `version.json`（見 `SettingsView.GetVersionJsonPath`）：

- 優先：`{AppDirectory}/../version.json`
- 其次：`{AppDirectory}/version.json`
- 最後：`{BaseDirectory}/../version.json`

`version.json` 必須包含 `APIs` 物件（Object）。範例（格式以程式實際讀取欄位為準）：

```json
{
  "APIs": {
    "SpashAPIVelocity": {
      "sUNC": "...",
      "UNC": "..."
    },
    "SpashAPIXeno": {
      "sUNC": "...",
      "UNC": "..."
    }
  }
}
```

- `APIs` 的 key 會被當成 API 名稱（ApiName）顯示在 UI，也會寫入 `selected_api`
- `sUNC` / `UNC` 目前僅用於 UI 顯示（Settings 內的每個 tile 會顯示一行或兩行資訊）

### 3.1 version.json 缺失/格式錯誤

如果 `version.json` 不存在、或 `APIs` 欄位不存在/不是物件，Settings 會顯示錯誤面板，並提供執行 `checker.exe` 的按鈕（僅在這兩種錯誤情況下出現）。

## 4. config.cfg（使用者選擇）

使用者選擇的 API Provider 會寫入：

- `selected_api={ApiName}`

Settings 啟動時會：

- 讀 `selected_api`
- 若為空，會預設使用 `version.json` 的第一個 API

## 5. Provider 判斷規則（Velocity / Xeno）

目前程式內對 Provider 的行為分歧，主要以「字串判斷」決定：

- 名稱包含 `xeno`（不分大小寫），或等於 `SpashAPIXeno` -> 視為 Xeno
- 名稱包含 `velocity`（不分大小寫），或等於 `SpashAPIVelocity` -> 視為 Velocity
- 其他情況目前會回落到 Velocity 行為

此判斷影響：

- `Executor.API` 在 `AttachAsync` / `ExecuteScriptAsync` 是否需要走 STA 執行緒（目前 Velocity 會走 STA）
- `SpashApiInvoker` 初始化/反射尋找 API Type 與方法的選擇策略

## 6. 切換 API 的實際行為（Settings 內）

在 Settings 的「API 選擇」視窗點選新的 API 時（見 `SettingsView.ApiTileButton_OnClick`），流程如下：

1. 如果新舊 API 名稱相同：只會關閉視窗，不做任何事
2. 若不同：會先嘗試判斷目前是否已 attach
   - 若 Roblox 未開啟，或 API 尚未初始化成功：視為未 attach
   - 若 Roblox 開啟且 API 已初始化：呼叫 `API.IsAttached()` 判斷
3. 若「切換當下已 attach」：
   - 顯示提示（Toast）告知會重啟 Roblox
   - 呼叫 `RobloxRuntime.KillRoblox()` 強制關閉 Roblox
4. 寫入 `config.cfg`：更新 `selected_api`
5. 呼叫 `SpashApiInvoker.ResetForApiChange()`，清空快取並讓下次呼叫重新初始化/載入
6. 若第 3 步有重啟 Roblox：會呼叫 `RobloxRuntime.TryLaunchRoblox()` 嘗試重新啟動
7. 最後關閉 API 選擇視窗

### 6.1 為什麼切換時要重啟 Roblox

目前的切換策略是「避免在已 attach 的狀態下換 DLL/換 provider」。

因為第三方注入 API 通常與目標進程（Roblox）有綁定狀態；在 attach 後直接換 provider，很容易造成：

- provider 內部狀態與實際注入狀態不一致
- IsAttached 判斷錯誤
- Execute/Attach 失敗或不穩定

所以程式選擇在 attach 狀態下切換時，先重啟 Roblox，確保環境乾淨。

## 7. 統一呼叫介面（Executor.API）

Executor 對 UI/其他模組提供統一 API：

- `bool API.IsRobloxOpen()`
- `bool API.IsAttached()`
- `void API.KillRoblox()`
- `Task<ApiResult> API.AttachAsync(CancellationToken ct)`
- `Task<ApiResult> API.ExecuteScriptAsync(string script, CancellationToken ct)`

其中：

- `AttachAsync` 會在內部輪詢 `IsAttached`（有 timeout / retry）直到成功或超時
- `ExecuteScriptAsync` 會在未 attach 時自動先 `AttachAsync`

回傳型別：

- `ApiResult` 包含 `Success`、`Provider`、`Code`、`Message`（以及可選的 `Exception`）

## 8. 常見問題

### 8.1 切換 API 後仍然像沒切換

請檢查：

- `config.cfg` 的 `selected_api` 是否已更新
- `version.json` 的 `APIs` key 是否與資料夾名/預期名稱一致
- `{AppDirectory}/APIs/{ApiName}/` 是否存在且含有可用 DLL

### 8.2 API 清單空白或顯示錯誤

請檢查：

- `version.json` 是否存在於程式實際搜尋到的位置（優先 `{AppDirectory}/../version.json`）
- `version.json` 是否包含 `APIs`，且 `APIs` 為物件（Object）

### 8.3 Execute 失敗（ProviderException）

通常表示第三方 DLL 反射呼叫失敗或 provider 自己拋錯。

請先確認：

- Roblox 是否已開啟
- `AttachAsync` 是否成功
- API DLL 與依賴 DLL 是否放在同一個 `{AppDirectory}/APIs/{ApiName}/`

