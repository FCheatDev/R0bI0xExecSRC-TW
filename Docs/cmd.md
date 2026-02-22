# cmd.md — 指令定義文檔

---

## 文檔說明

此文檔定義所有在對話或 todo.md 中使用的縮寫指令。
遇到不認識的指令時，請來此文檔查閱。

---

## 繼續執行指令

當收到以下任意回覆時，表示「繼續執行項目或未完成的需求」（對方打字打太快）：

- `c`
- `繼續`
- `際遇`
- `ru4mv4`

---

## 功能指令（{f:...}）

| 指令         | 說明                                                       |
| ------------ | ---------------------------------------------------------- |
| `{f:a_n}`    | 允許搜索並使用網路上的資料                                 |
| `{f:lang}`   | 需要中英雙語支援，內容存入 en.json / zh.json，不可寫死字串 |
| `{f:skip_1}` | 可以忽略 todo.md 的第一點                                  |
| `{f:skip_2}` | 可以忽略 todo.md 的第二點                                  |

---

## 效果指令（{e:...}）

| 指令            | 說明                                                               |
| --------------- | ------------------------------------------------------------------ |
| `{e:仿execute}` | 模仿 editor page 的 execute 按鈕動畫，參數見下方 BounceButtonStyle |

---

## 按鈕指令（{f:b_e-1}）

按鈕套用以下參數：

- Style Key: `BounceButtonStyle`
- RenderTransformOrigin: `0.5, 0.5`
- RenderTransform: `ScaleTransform (ScaleX=1, ScaleY=1)`
- 觸發條件: `Trigger IsPressed=True`
- 按下縮小: Scale `1 -> 0.94`，Duration `0:0:0.06`
- 放開回彈: Scale `-> 1`，Duration `0:0:0.18`
  - Easing: `BackEase`，EasingMode=`EaseOut`，Amplitude=`0.6`

備注：`{e:仿execute}` 與 `{f:b_e-1}` 效果相同，兩者都指向 BounceButtonStyle。

---

## SKILL 文檔

如果有需要，可以查閱以下路徑的技術指引：

- `SKILL/README.md`
- `SKILL/SKILL.md`

---

_最後更新：2026 年 2 月_
_此文檔僅此專案生效_
