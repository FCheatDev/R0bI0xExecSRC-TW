1. [✓]幫我在選擇語言跟theme那邊,告訴我ui可以怎樣設計{f:a_n}
2. [✓]在 SetupWindow 實作主題選擇方案 B（左側清單 + 右側預覽）並維持現有風格/行為
3. [✓]SetupWindow大改內容:
   - 移除theme選擇ui的W2..等的圖案,只保留字
   - 盡量與theme-option-b-mock.svg 的選擇ui一樣(不含preview)
   - 圓角不能太圓,最多3px
   - 移除Preview跟選擇theme的框
   - 移除preview展示圖片的黑色背景
   - 下一步/完成/返回的按鈕不要有背景,只要字
   - 點擊下一步後,要有淡出動畫(含字),不是瞬間切到選擇theme
   - 軟體標題的"Wave設定"改成DropUI
   - 切換theme後,preview要淡出淡入
   - 選擇主題的字(WaveUI-2025/WaveUI-2026/2)刪除"UI"這兩個字,然後字體變成粗體
4. [✓]我打算theme讀取png都是直接在WaveUI裡面(會有2025/2026版,Monaco也是)
   - Monaco的資料夾名稱先改成2025(因為現在也只有2025版),我以後會創建2026的monaco
   - 目前monaco資料夾位置: Assets/WaveUI/Monaco
   - 希望改成的路徑(2025版跟2026/2(目前日期未知)版): Assets/WaveUI/Monaco/2025
     - 你只需把2025版monaco路徑改成Assets/WaveUI/Monaco/2025,我已經把檔案移動到2025資料夾裡面
     - 2025版的setting開啟monaco路徑也要改
     - 注意:Monaco與png都是絕對路徑,不可能會出現monaco1之類的

5. [✓]setup的preview圖片使用Assets/Setup裡面的Wave2025.png等

6. [✓]我好奇,我把WaveUI資料夾裡面的檔案移動到WaveUI/2025,為什麼還是可以讀到,比如:
   - WaveMinimizeWindow.xaml.cs
   - WaveShell.xaml
   - WaveAssets.cs
   - 之類的檔案,可以在Executor\WaveUI\2025 看到
   - 幫我檢查這樣改會不會有其他問題

7. [✓]把key system page也{f:lang},目前是寫死的,我不要這樣
8. [✓]你告訴我,config.cfg的"WaveUI"我現在有2025版跟2026版,我要區分他們嗎?
9. [✓]將config.cfg的WaveUI_key改成全局"key"

10. [✓]在setting page的app列表裡面的透明度上方添加變更Key功能,樣式同setting(不含按鈕),他是個按鈕,點擊後,會彈出窗口,窗口內容:
    - 樣式同editor page的explorer添加腳本的窗口差不多
    - 有取消與變更按鈕,還有輸入key的框,然後會顯示上次的key
    - {f:lang}
    - 取消與變更按鈕樣式動畫是editor page的executor
    - 我希望字跟上面的一樣靠左邊,然後按鈕靠右邊,按鈕樣式跟setting一樣,然後不要key圖案,只要字
    - 需要有字體切換功能(變更key彈窗)

11. [✓]所有page的字體都要有字體切換功能
12. [✓]將setting page的debug 懸浮球偵數移動到懸浮球透明度上方
13. [✓]將變更key移動到重新載入上方
14. [✓]告訴我setting的所有專有名詞

15. [✓]editor的explorer創建腳本的加號,點擊後的動畫要回彈(跟execute一樣),彈窗的取消/確定也要
16. [✓]setting page的api選擇,當../version.json被刪除後,彈窗裡面的內容會變成,未找到version.json
    - 右下角會有紅色按鈕,點了會執行../checker.exe (僅version.json找不到或是version.json內容沒有)
    - 點擊後,關閉executor然後執行checker.exe
    - 按鈕位置在右下角
    - 滑鼠懸浮到上面,滑鼠圖標會是手的圖案
    - {f:lang} {e:仿execute}
17. [✓]可以的話,請告訴我editor page的execute按鈕動畫資訊/參數
    - Style Key: BounceButtonStyle
    - RenderTransformOrigin: 0.5,0.5
    - RenderTransform: ScaleTransform (ScaleX=1, ScaleY=1)
    - 觸發條件: Trigger IsPressed=True
    - 按下縮小: Scale 1 -> 0.94, Duration 0:0:0.06
    - 放開回彈: Scale -> 1, Duration 0:0:0.18
      - Easing: BackEase, EasingMode=EaseOut, Amplitude=0.6
18. [✓]home page的 網站/商店按鈕動畫改{e:仿execute}
19. [✓] 如果我變更字體,setting的資料類別路徑會寫很長,修復此問題,列子:
    - D:\data_Files\Project\AzureaX\RH\Executor\Executor\Executor\bin\Release\net10.0-windows\win-x64
    - 我希望在D:\data_Files\Project\AzureaX\RH\Executor\Executor\Executor\bin之後就開始使用"..."表示
20. [✓]添加第三方API整合/切換的說明文檔(Docs/API/API.Integration.Switching.md)
21. [✓]幫我把wave 2025版軟體邊框做成圓角
22. [✓]以 version.json 的 Executor_Type 取代 selected_api 字串 contains，修正 Provider 判斷（SpashApiInvoker + API.cs）並完成 dotnet build 驗證
23. [✓]需求21.沒有成功,軟體邊框沒有出現圓角3px
24. [✓]把wave2025版的視窗按鈕改掉,將打叉變成#ababab(滑鼠移到上面),然後沒有移到上面的時候是#6c6c6c,且全螢幕/縮小也是,不要按鈕框
    - 然後距離靠近(因為按鈕框距離已經調整,含軟體邊與按鈕的距離)
25. [✓]key system的左上角logo大小在大一點
    - 輸入框還是沒有點擊框之後,還是沒高亮(參考圖一)
26. [✓]logo不要白色,而是使用assets/waveUI/wave.png
27. [✓]topbar那邊,editor與script之間添加Console類別
    - 用於檢查輸出日誌
    - 樣式一樣
    - 有顏色,比如: WARING為黃色/ERROR為紅色/INFO為白色
      - 有下拉選單來篩選想看到的資訊,需自訂下拉選單/動畫
      - 只有INFO/WARING...等,這種警訊才要變顏色,不然預設顏色是白色,參考如下:
        - 範例日誌: [2026-02-09 11:19:46.700] [INFO][Logger] Initialized
        - [2026-02-09 11:19:46.700] 變色
        - [INFO] 變色
        - [Logger] 不要變色
        - Initialized 不要變色
    - 下拉選單右側有搜索框(樣式參考之前的)
    - 要跟隨自訂字型
    - {f:lang}
    - 排版為:
      - 左邊檢視日誌,右側為console內容

28. [✓]進入home page後,旁邊不是有logo嗎?把他向右移動,因為他太靠近軟體邊了,不好看
29. [✓]setting旁邊搜索框,把他移動到應用程式上方,就是錨點往下移動,這樣比較好看(不是放到右邊喔)
30. [✓]需求30的搜索框寬度要改,跟錨點差度多長
31. [✓]重做需求28.這是依據你做28後的新變更:
    - topbar的地方,當切換到console時,顏色要跟topbar其他的一樣會變顏色
    - 日誌是最新的,不是之前的(當軟體關閉後,存入ax-log,存入功能已經做好)
    - 沒有框,是用顏色來區分區域的,同editor page(指右側類似client的右側)
    - {f:a_n} 上網查相關日誌顯示方式
    - 移除篩選顯示/搜索,表示全部都是日誌
    - "Console"右側是篩選顯示/搜索框,還有可以調整字體大小
    - 日誌內的文字可以選取
    - 日誌有捲動條
    - {f:lang}
    - 移除左邊的那個東西,我不要他,我是說我要把每條日誌放在console裡面,然後一直向下延伸
    - 調整字體大小的那個要跟setting page的調整透明度一樣樣式
    - 請寫入語言,不要有函數出現
    - 下拉選單改成按鈕,樣式設定如下:
      - {f:b_e-1} 錯誤/資訊/警告/全部按鈕
      - 點擊後按鈕變#1da1f2
      - 動畫要淡入淡出
    - 日誌與日誌之間有一些間隔
    - 調整字體大小滑動的時候會顯示當前字體大小,比如12/14/16之類(動畫樣式類似setting page的data類別複製路徑)
    - 按鈕沒有回彈動畫,修復
    - 顯字字體大小,是跟隨那個圓圈,不是固定,位置錯了,而且是在下方不是上方
    - waring按鈕大小不要太擠,可以把按鈕變寬一點
    - 顯字字體大小,顯示位置在下方,不是上方
    - 滾輪太粗,要跟setting page的滾輪一樣
    - 不強制畫面往最底下,但是使用者往上滑日誌的時候,中間下方會寫滑到最底部,的按鈕(無圓角/淡出淡入)
    - 滾輪還是很粗阿,而且滑到最底部背景是#1da1f2
    - 滑到最底部是個按紐,預設顏色是#1da1f2
      - 且他是固定的不會一直刷新(指不要隨著滾輪動就一起刷新)
      - 滑到最底部要有滑動的動畫
    - 在住空台下方添加一條細的白色線,來區分日誌與按鈕
    - 日誌那邊,為什麼我滑到中間的時候,會被強制往上滑到頂?
32. [✓]軟體有嚴重的卡頓/記憶體用太多/GPU使用率高,請優化他,尤其是切到console page會卡頓
33. [✓]clients managr是怎麼做出來的?就是可以偵測並選擇開啟的roblox,然後可以選擇注入哪個client
34. [✓]修復軟體在visual studio debug時,未找到api會卡頓
35. [✓]你剛剛完成需求的分隔線,不是在滑到最底部上面添加,刪掉他
36. [✓]clients page那邊,可以像clientsManager.png 嗎?
    - 能自己檢測有多少個roblox開啟,並顯示在上面
    - 顯示pid等之類的跟圖片一樣
    - 有兩個按鈕,一個是選擇注入{f:b_e-1},一個是選擇client (勾選框,樣式同setting page開機自啟動)
    - {f:lang}
37. [✓]修復:
    - System.InvalidOperationException
      HResult=0x80131509
      Message='{DependencyProperty.UnsetValue}' 對屬性 'BorderBrush' 來說，並非有效的值。
      Source=<無法評估例外狀況來源>
      StackTrace:
      <無法評估例外狀況堆疊追蹤>
38. [✓]我的clients page是要有:
    - 能顯示那個robloxbeta的使用著頭像
    - 添加UserName然後刪除status
    - 然後字體/語言不是固定的,而是{f:lang},如果不懂,可以參考其他page是怎麼做的,預設字體的PID:{PID}/UserName:{UserName}是粗體喔
    - 不要顯示robloxbeta字樣,要跟clientManager.png差不多
    - client page所有字需要{f:lang}喔!!!
39. [✓]你client page的函式顯示出來了,請你修改,就是寫入zh.json/en.json,PID/UserName也要{f:lang}

40. [✓]修復 clients page：en.json 同步 Clients 多語言 + PID/UserName 標籤改用 {f:lang} + Inject/全選按鈕回彈動畫

41. [✓]修復 clients page：PidLabelText/UserNameLabelText 的 Run.Text 繫結被視為 TwoWay，改成 Mode=OneWay 避免 InvalidOperationException
42. [✓]你注入語言沒有跟著變,名稱要{f:lang},而且你只要說Internal/External ,不要"Internal(DLL Injection)"跟"External(Memory Read/Write)"
    - External （顯示 ValexUI,但是還有其他的）
43. [✓]移除選擇主題的框,不要軟體->框->主題 ,而是: 軟體->主題
44. [✓]注入模式必須選擇一個,默認是內部
45. [✓]setupWindow的返回淡出淡入要跟下一步按鈕的淡出淡入動畫一樣秒數
46. [✓]外部與內部是不同theme,修復他,內部wave的,外部valex的
47. [✓]還原選擇主題,我是說,他不是有分兩邊嗎?他下面有灰色背景對吧?我不要他
    - 右邊的預覽呢?
    - 注入模式的External/Injection不是說要{f:lang}嗎?
    - 下方的三個點要固定在中間不能動,我要的是位置不變,但是裡面三個點會隨著設定以一點一點設定完變色 #606060
      - 比如:
        - 選擇語言是只有一個 #606060
        - 注入模式是兩個 #606060
        - 第三個同上
