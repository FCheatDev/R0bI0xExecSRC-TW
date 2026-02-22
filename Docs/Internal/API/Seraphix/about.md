WaveHexAPI 有三種實作（對應 `..\Outputs\Executor\APIs` 內的三個資料夾）：

- `WaveHexAPI - Xeno`
- `WaveHexAPI - Velocity`
- `WaveHexAPI - WaveHex`

下方整理每一種的檔案依賴與公開成員（以 1~5.png / 實際輸出檔案為準）。

## Xeno（WaveHexAPI - Xeno）

### 檔案（需放在同一資料夾）

- `WaveHax.dll`
- `Xeno.dll`
- `libcrypto-3-x64.dll`
- `libssl-3-x64.dll`
- `libcurl.dll`
- `zlib1.dll`

### WaveHax.dll 公開方法（Object Browser：3.png）

- `DisableInjectionNotification()`
- `Execute(string)`
- `GetAutoInject()`
- `GetClientsList()`
- `Inject()`
- `IsInjected()`
- `IsRobloxOpen()`
- `KillRoblox()`
- `SetAutoInject(bool)`
- `SetCustomInjectionNotification(string, string, string, string)`
- `SetCustomNameExecutor(string, string)`
- `SetCustomUserAgent(string)`

## Velocity（WaveHexAPI - Velocity）

### 檔案

- `WaveHaxAPI.dll`

### VelocityAPI.VelAPI（Object Browser：1.png）

- `VelAPI()`
- `Attach(int)`
- `Execute(string)`
- `IsAttached(int)`
- `StartCommunication()`
- `StopCommunication()`
- `Base64Decode(string)`
- `Base64Encode(string)`
- `injected_pids`
- `VelocityStatus`

### VelocityAPI.VelocityStates（Object Browser：2.png）

- `Attached`
- `Attaching`
- `Error`
- `Executed`
- `NoProcessFound`
- `NotAttached`
- `TamperDetected`

### WaveHax.Api（Object Browser：4.png）

- `Execute(string)`
- `GetClientsList()`
- `Inject()`
- `InjectFull()`
- `IsInjected()`
- `IsRobloxOpen()`
- `KillRoblox()`
- `SetCustomInjectionNotification(string, string, string, string)`
- `SetCustomNameExecutor(string, string)`
- `SetCustomUserAgent(string)`
- `EnableConsole`
- `WaveHax.Api.ClientInfo`

## WaveHex（WaveHexAPI - WaveHex）

### 檔案
