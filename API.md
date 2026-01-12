# SpashAPI Integration / Unified API Facade Spec

## Purpose / 文件目的

This document defines a **single stable API contract** for the app to call, regardless of which upstream provider is selected.

- **Audience**: developers (UI/ViewModel/business logic authors)
- **Goal**: unified behavior + unified error/reporting model (Result-based)

## Terminology / 名詞

- **Facade**: the app's unified entrypoint (`API`)
- **Provider**: a concrete adapter for a specific upstream (Velocity / Xeno / others)
- **Upstream**: 3rd-party implementation shipped as `.dll` or `.exe`

## Upstream APIs (3rd-party)

### SpashAPI Velocity v3.2

Example calls:

- `SpashAPIVelocity.AttachAPI();`
- `SpashAPIVelocity.ExecuteScript(richTextBox1.Text);`
- `SpashAPIVelocity.KillRoblox();`
- `SpashAPIVelocity.IsRobloxOpen();`
- `SpashAPIVelocity.IsAttached();`

### SpashAPI Xeno v4.4

Example calls:

- `SpashAPIXeno.AttachAPI();`
- `SpashAPIXeno.ExecuteScript(richTextBox1.Text);`
- `SpashAPIXeno.KillRoblox();`
- `SpashAPIXeno.IsRobloxOpen();`
- `SpashAPIXeno.IsAttached();`

### Notes

- Velocity is an `.dll`.
- Xeno is an `.exe`.
- Files are located under `Bin/` (runtime output folder).

## Runtime Requirements / Constraints

- **Platform**: x64 required.
- **Framework**: .NET (Windows) required.
  - Project currently builds as `net10.0-windows`.
  - If you hit runtime load errors, verify the target framework + native dependencies.
- **Known issue**: some upstream builds may fail under .NET 8.0 (may trigger `<Module>` error).
- **Debug run**: upstream developer suggests using `Ctrl+F5` when starting from Visual Studio.

Architecture note:

- If you see a build warning like **MSIL project referencing AMD64 assembly** (e.g. `SpashAPIXeno`), set the project's target platform/processor architecture to **x64** to avoid runtime failures.

## Finalized Decisions / 已定案行為

- **Result-based API**: facade methods return a Result object (not `bool`) for actionable UI decisions.
- **AttachAsync completion rule**: `AttachAsync` must only complete when:
  - Attached is confirmed (`IsAttached()==true`), or
  - It fails / times out.
- **Xeno prompt policy**: when provider is Xeno, upstream may already show its own custom prompt; **facade must not show another prompt/messagebox**.

## Goal: APIs 資源集中處理（Unified Facade）

Make a single unified API entry that hides provider differences (Velocity/Xeno/others).

External callers should only call:

- `API.IsAttached()`
- `API.IsRobloxOpen()`
- `API.KillRoblox()`
- `API.ExecuteScriptAsync(script, ct)`
- `API.AttachAsync(ct)`

## Architecture / 層級（Layering）

Expected structure:

`UI / Buttons / ViewModel`
-> `API (Facade)`
-> `SpashAPI Velocity` (provider)
-> `SpashAPI Xeno` (provider)
-> `...other API` (provider)

Responsibilities:

- **API (Facade)**
  - Expose a single stable interface (the contract) to the rest of the app.
  - Handle cross-cutting behaviors: auto-attach on execute, provider selection, and error normalization.
- **Provider (Velocity/Xeno/others)**
  - Thin adapter layer mapping unified calls to the specific upstream implementation.
  - No UI responsibilities.

## Provider Switching Strategy (Manual)

- Switching is **manual** via **Settings -> APIs** (e.g. dropdown).
- Only **one active provider** is exposed by `API` at a time.

## API Contract（對外行為規格 / Behavior Contract）

The “contract” means: for each method, we define **what it guarantees**, **what it returns**, and **how failures are reported**, so UI/business code can rely on stable behavior even if the underlying provider changes.

### Result object (return type)

Facade methods return a **Result object** instead of `bool`, so callers can decide UX (toast/log/etc.) and we can preserve provider-specific failure reasons.

Recommended shape (concept):

- `Success: bool`
- `Provider: string` (e.g. `Velocity`, `Xeno`)
- `Code: string` (normalized outcome / failure reason)
- `Message: string` (short user-facing summary)
- `Exception: Exception?` (optional, for logging)

Recommended C# shape (example):

```csharp
public sealed record ApiResult(
    bool Success,
    string Provider,
    string Code,
    string Message,
    Exception? Exception = null);
```

Rules:

- `Message` should be short and safe to display, but UI decides whether to display.
- `Exception` is for logging/diagnostics only; do not show raw exception to users.

### Standardized `Code` values

`ApiResult.Code` is a stable string set that UI/business logic can switch on.

- `Executed`
- `Attached`
- `AttachTimeout`
- `Attaching`
- `NotAttached`
- `NoProcessFound`
- `TamperDetected`
- `ProviderError`
- `ProviderException`
- `Unknown`

Suggested meaning (stable semantics):

- `Executed`: provider accepted script execution.
- `Attached`: attached confirmed.
- `Attaching`: provider started attach but not yet confirmed.
- `AttachTimeout`: facade waited for `IsAttached()` but timed out.
- `NotAttached`: attach did not occur / provider reports not attached.
- `NoProcessFound`: Roblox process not found.
- `TamperDetected`: provider indicates tamper/anti-cheat condition.
- `ProviderError`: provider returned an error outcome but did not throw.
- `ProviderException`: provider threw an exception.
- `Unknown`: anything not mapped.

### Mapping rules

- Velocity:
  - `Executed` -> `Executed`
  - `Attached` -> `Attached`
  - `Attaching` -> `Attaching` (note: facade still waits until attached or timeout)
  - `NotAttached` -> `NotAttached`
  - `NoProcessFound` -> `NoProcessFound`
  - `TamperDetected` -> `TamperDetected`
  - `Error` -> `ProviderError`
- Facade:
  - Attach wait timeout -> `AttachTimeout`
  - Any unexpected/unknown outcome -> `Unknown`
- Xeno:
  - Any thrown exception -> `ProviderException`
  - Notes: provider already shows its own custom prompt; facade must not show another prompt.

### Error policy

- For `AttachAsync()` / `ExecuteScriptAsync()`: **do not show message boxes**; return a Result object and let UI decide.
- Exception: when provider is **Xeno**, upstream may already show a custom prompt; facade must **not** add any additional prompt.
- For other methods (`IsAttached()` / `IsRobloxOpen()` / `KillRoblox()`): prefer “safe/no-throw + best-effort”.

### Configuration / 可配置項

- **Selected provider**: managed by Settings -> APIs (e.g. dropdown)
  - Facade must expose only **one active provider** at a time.
- **Attach timeout**: configurable; default recommendation: **5 seconds**.
- **Attach polling interval**: configurable; recommended: **100ms - 250ms**.

### Methods

- `Task<ApiResult> AttachAsync(CancellationToken ct)`

  - **Behavior**: try to attach/inject using the currently selected provider.
  - **Guarantee**: the task only completes when either:
    - Attached is confirmed, or
    - Attach failed / timed out.
  - **Waiting rule**: if provider returns/enters `Attaching`, facade must wait until `IsAttached()==true` (poll) or timeout.
  - **Timeout**: should be configurable; default recommendation: **5 seconds**.
  - **Return**:
    - `Success=true` if attached is confirmed before timeout.
    - `Success=false` otherwise; `Code/Message` should explain the reason.

  Notes:

  - If `ct` is cancelled, return `Success=false` with a meaningful `Code` (either a dedicated cancellation code, or `Unknown` if you keep the list fixed) and set `Exception` to `OperationCanceledException` for logging.

- `Task<ApiResult> ExecuteScriptAsync(string script, CancellationToken ct)`

  - **Behavior**:
    - If not attached, **auto-call `AttachAsync(ct)`** first.
    - If attach fails, do not execute.
    - If attached, execute script via current provider.
  - **Return**:
    - `Success=true` when execution is accepted/confirmed by provider.
      - Velocity: `Code=Executed`.
      - Xeno: no exception thrown.
    - `Success=false` when attach/execution failed; `Code` should capture provider outcome (`NotAttached`, `NoProcessFound`, `TamperDetected`, `Error`, etc.).

  Notes:

  - Empty/whitespace script should return `Success=false` with a clear `Message` and a stable `Code` (recommended: `Unknown` unless you introduce a dedicated validation code).

- `bool IsAttached()`

  - **Behavior**: query current provider attached status.
  - **Return**: `true`/`false`.

- `bool IsRobloxOpen()`

  - **Behavior**: query whether Roblox is running (provider handles detection).
  - **Return**: `true`/`false`.

- `void KillRoblox()`
  - **Behavior**: kill Roblox process (best-effort).
  - **Notes**: if Roblox is not running, no action required.

## Troubleshooting: "The type initializer for '<Module>' threw an exception"

This usually means the module initialization failed (often due to missing dependencies, wrong architecture, or a native load failure).

What to capture / check:

- Check the **real root cause**: read `Exception.InnerException` (and its message/stack trace).
- Verify the app is running as **x64**.
- Verify the required `.dll` / `.exe` and their dependencies exist under the output folder (e.g. `Bin/`).
- If it only happens on some machines, it can be missing runtime dependencies (e.g. VC++ runtime) or blocked files.

Practical tips:

- If the upstream binaries were downloaded from the internet, Windows may mark them as blocked; try **Unblock** in file properties.
- Compare the runtime output folder contents between a working machine and a failing machine.
