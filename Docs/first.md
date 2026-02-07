1. 這個文檔的功能是告訴你該做或是不該做
2. 不准使用emoji
3. 不懂的地方要問,不要盲目去做
4. todo.md是要你完成的選項
5. 如果我只有丟給你 todo.md時,你就完成上面的項目
6. 如果todo.md項目完成,就在前面寫 [✓]
7. 如果你有什麼想法,可以隨時提出
8. 如果你覺得有問題,可以隨時提出
9. 此文檔(first.md)不會在丟給你,除非有新的內容
10. 隨時可以查閱文檔,但是遇到你不懂的指令,你就來看first.md,因為有可能是我忘記給你
11. 以繁體中文回答,不管我回你是否是其他語言
12. 指令可以在cmd.md找到
13. 任何改動都需要跑一次測試
14. 可以忽略圖片來源,因為已經放到Output資料夾
15. 如果還需要其他nuget,你自己去下載
16. 請不要寫了,結果還是錯的或是跟原本結果一樣,請盡可能的去修復需求產生出來的問題
17. 100% 完成用戶提出的所有需求
18. 收到指令後馬上分析並執行，不拖延
19. 每次回應都直接針對用戶需求，不偏離主題
20. 遵循現有程式碼風格，保持一致性
21. 預先考慮可能出現的編譯問題
22. 此項目是WPF編寫,net版本是10
23. 完成需求在 [✓] 而不是馬上
24. 名稱定義:
    24-1. todo.md裡面的1./2./3....稱為需求/項目25.如果你要備份檔案,不要放到Executor/Executor,而是放到Executor/Backup,然後要寫上備份日期/原因(允許在程式裡面最上面寫,如果要還原,請刪掉備份日期內容)
25. config.cfg配置定義:
    25-1. 如果前面有"WaveUI"/"SynxUI"的話,那個就是針對Wave的(以後可能還會改)
    25-2. 如果前面有沒有加上"WaveUI"/"SynxUI"的話,那就是全局配置
    25-3. 例子:
    - language=zh -> 全局
    - theme=WaveUI-2025 -> 全局
    - discord_rpc=true -> 全局
    - font= -> 全局
    - opacity=0.8 -> 全局
    - topmost=true -> 全局
    - WaveUI_skip_load_app=true -> Wave
    - WaveUI_small_wave_effect=fade -> Wave
    - WaveUI_small_wave_opacity=0.5 -> Wave
26. 樣式部分:
    26-1. 滑鼠懸浮到按鈕不要出現白色
    26-1-1. 原因: WPF 預設 Button Template 會在 IsMouseOver 時套用 Highlight/Brush, 造成白色或亮色的底
    26-1-2. 規則: 任何按鈕(包含像設定列表那種整行可點的 Button)在 Hover 時, 背景與邊框顏色必須保持設計稿, 不可變白
    26-1-3. 做法(擇一或混用):
    a. 設定 FocusVisualStyle={x:Null}, 避免焦點框造成額外白邊
    b. 自己寫 Button Style/ControlTemplate, 並在 Triggers 裡把 IsMouseOver 的 Background/BorderBrush 明確指定成同一色
    c. 若按鈕只是用來讓整行可點, 可將 Background=Transparent, BorderThickness=0, 並且仍需避免 Hover 套到預設 Template
    26-1-4. 檢查點:
    a. 滑鼠移到按鈕上時, 不應出現任何白色遮罩/白底/白邊
    b. 按下按鈕時(Pressed)若有縮放動畫可以保留, 但不可出現白色高亮
