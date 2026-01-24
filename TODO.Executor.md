# [ 重要 ]:

- 1.任何改動都需要跑一次測試
- 2.沒有經過我的允許,不准動其他道程式碼,只能動我說的
- 3.任何問題都可以提問
- 4.請把 "[ 重要 ]" 的內容加入到您的記憶體
- 5.如果你已經完成我提出的要求,請修改此 TODO.md 的[ 需求 ] ,比如你已經完成第一點,就寫: - [ DONE ]1.{需求},並列出那時候需求產生出來的錯誤...等,產生出來的錯誤格式("5-1."這只是本 todo.md 編號,無需寫進去 ):
- 5-1 範例: [ 錯誤{第幾個錯誤數字(比如第一個就寫第一個 1-1....1-9999999)} ]: {問題}
- 5-2 範例: - [ DONE ] 1.SETTING 的快速前往,點擊任意一個按鈕,如果離太近,
-             會一直跳,然後加亮的框會顯示錯誤,而且還有機會會先跳到 app 在跳到其他的
-           - [ 錯誤1-1 ]: 開啟軟體後閃退
-           - [ 錯誤1-2 ]: sdassfgerERR
- 6.如果我沒有再 [ 需求 ]添加需求而是再跟你聊天事裡面寫的話,請您跟第 5 點一樣,自己添加編號,還有是否完成
- 7.TODO.md 是寫給你看的
- 8.可以提出不合理的地方與任何不懂的問題/事情
- 9.請不要寫了,結果還是錯的或是跟原本結果一樣,請盡可能的去修復需求產生出來的問題
- 10.所有檔案你可以讀取
- 11.我知道我描述的很不完全,你可以告訴我,但是不要用那麼專業的字跟我說,我也看不懂
- 12 如果我跟你說"繼續"你就繼續完成 [ 需求 ] 新的項目
- 13.回復我的訊息請以繁體回答
- 14.可以忽略圖片來源,因為已經放到Output資料夾
- 15.如果還需要其他nuget,你自己去下載
- 16.{}規則,這是一個函數,比如上面寫"{換行}",那個文字就必須換行...等

# [ 需求 ]:

- [ DONE ] 1.SETTING 的快速前往,點擊任意一個按鈕,如果離太近,會一直跳,然後加亮的框會顯示錯誤,而且還有機會會先跳到 app 在跳到其他的
  - [ 錯誤1-1 ]: 修復時發現「舊的滾動動畫結束」會把「新的點擊目標」狀態清掉,造成跳動/加亮錯亂,需要加上請求序號避免互相覆蓋

- [ DONE ] 2."\n"這個換行功能失敗,請修復,還有所有文字不能超出軟體的最大框,只允許換行
  - [ 錯誤 2-1 ]: WPF 的 XAML 內, Text="...\n..." 不會自動換行,會直接顯示 \n,需要改成可換行的寫法(例如 &#10;)
  - [ 錯誤 2-2 ]: json 文字有時候會出現多層跳脫(\\n),需要在字串顯示前做統一的換行正規化

- [ DONE ] 3.檢查沒有漢化的地方與沒有切換字的語言的功能,你列出來就好,我會告訴你哪個要漢化
  - [ 錯誤 3-1 ]: 無法用單一 regex 一次掃完整個專案(工具限制),改成分段搜尋/逐檔確認

- [ DONE ] 4.Velocity 的 Executescript 還是不能使用,但可以 attach,以下是我覺得的問題,請告訴我最有可能的問題:
  - [ 錯誤 4-1 ]: 從日誌看得出來 SpashAPIVelocity.ExecuteScript 回傳 Executed,且 VelocityStatus 顯示 Attached,所以「切換 API 機制(4-4)」與「傳入格式完全不符合(4-1)」的機率很低
  - [ 錯誤 4-2 ]: 最有可能是 Velocity 本身在某些狀態下會回 Executed,但腳本沒有真的在遊戲裡執行(偏向你說的 4-2)
  - [ 錯誤 4-3 ]: 我們這邊送進去的是原始 script 字串,沒有做加密/打亂,所以 4-3 機率較低

- [ DONE ] 5.修改日誌系統內容:
  - 5-1. 這是原本的:
    - 5-1-1. [2026-01-13 16:27:34.726] [INFO][Logger] ProcessId: 28892
    - 5-1-2. [2026-01-13 16:27:34.727] [INFO][Logger] OS: Microsoft Windows NT 10.0.26100.0
    - 5-1-3. [2026-01-13 16:27:34.727] [INFO][Logger] Framework: .NET 10.0.1
  - 5-2. 可以改成:
    - 5-2-1. [{時間}] [{狀態資訊}][{使用的物件名稱}] ProcessId: 28892
    - 5-2-2. [{時間}] [{狀態資訊}][{使用的物件名稱}] OS: Microsoft Windows NT 10.0.26100.0
    - 5-2-3. [{時間}] [{狀態資訊}][{使用的物件名稱}] Framework: .NET 10.0.1
    - 5-2-4. 解釋: {時間}: 就是時間
    - 5-2-4. 解釋: {狀態資訊}: 比如 ERROR/INFO/WARING
    - 5-2-4. 解釋: {使用的物件名稱}: 比如 APIs / Debug / Attach 時的資訊 / ExecuteScript 時的資訊
  - [ 錯誤 5-1 ]: 原本 Warn/Exception 的狀態文字不是你列的格式(例如 WARN/EX),已統一成 WARING/ERROR

- [ DONE ] 6.日誌可以改成 SUCCESS/WARING/INFO/ERROR 顯示相對應的狀態

- [ DONE ] 7.修復錯誤 1-1 他現在會亂跳,不過比以前好,假如我在 Data 那邊,切到 API,他會先跳到 App 在跳到 API,修復此問題

- [ DONE ] 8.設定內容的眶(Topmost/monaco 之類的)都改成使用一條線,並移除所有框,就像 isattached 跟 isrobloxopen 那樣的樣式,所以最後會變成全部的框變成線,來區分類別

- [ DONE ] 9.將 setting 裡面的 reload/open 按鈕改成跟 editor page 的 killroblox 一樣有底線,細細的間隔大一點(特效動畫字體都要),移除框

- [ DONE ] 10.info 進度條的顏色是 rgb(94, 161, 222),進度條背景為 rgb(35, 53, 73),可以參考 waveUI 資料夾裡的圖九

- [ DONE ] 11.在 setting 的 debug 添加 Skip Load App 功能,可以勾選,然後 load app 跟 key system 會出現 Skip 的按鈕(有底線,跟 killroblox 一樣的動畫字體效果,沒有框,位置在 load app 的進度條下面,key system 則在 log in 跟 Get Key 中間下面,顏色為淺灰色,移到上面變成白色)就可以跳過 Load app 跟 key system 直接進入 home page,如果取消勾選,Skip 按鈕會消失,需要漢化
  - [ 錯誤 11-1 ]: LoadView.xaml 把 SkipButton 放在 Grid.RowDefinitions 前面會造成 XAML 編譯錯誤(MC3088),需要移到 RowDefinitions 後

- [ DONE ] 12.editor page 裡面的 execute/clear/save 之間的距離短一點,但是 Execute 按鈕不要動要保持在原位置

- [ DONE ] 13.setting 的 debug 添加測試提示框,點擊後,可以測試提示框是否有正常發送,需要漢化,動畫樣式跟第 9.一樣

- [ DONE ] 14.如果我開啟 topmost,attach 後,api 會彈出 msg 對話框,會關閉不了,因為會被凍結 topmost 一直在上方,msg 會凍結

- [ DONE ] 15.skip 按鈕是在右下角,而不是右上角

- [ DONE ] 16.setting 的跳過載入畫面,因該放到 app 類別裡面的置頂與開機啟動的中間

- [ DONE ] 17.setting 的 debug 添加前往 waveUI 開機啟動的地方(在測試提示框的下面)
  - [ 錯誤 17-1 ]: 實作時不小心把 GoBootButton_OnClick 插到內部 class(ApiEntry) 裡,導致結構不正確,需要移回 SettingsView 類別本體

- [ DONE ] 18.選擇 APIs 的下面的線,因該要跟除錯類別有一定的距離,就像變更介面主題與資料類別一樣的距離

- [ DONE ] 19.setting 快速前往類別還是有問題,可以做成,點擊一次前往之後,高亮那個類別,然後就不要動他

- [ DONE ] 20.setting 的快速前往還是有問題,會跳到 app 類別在跳到使用者選擇的類別
  - [ 錯誤 20-1 ]: 無

- [ DONE ] 21.setting 外觀那兩個下拉選單,空隙太大,不需要縮小,但是要改成那個下拉選單的框,裡面都能點擊並觸發下拉選單,而不是點擊字才觸發下拉選單
  - [ 錯誤 21-1 ]: 無

- [ DONE ] 22.刪除 setting 的 debug 裡面前往開機啟動
  - [ 錯誤 22-1 ]: 無

- [ DONE ] 23.修復 velocity 的 executescript,我發現點了 Execute 按鈕,提示框會有卡頓,說明他執行了,但是失敗
  - [ 錯誤 23-1 ]: Velocity ExecuteScript 可能回傳 Task/void/null,原本會等待 Task 完成導致 UI/提示框卡頓
  - [ 錯誤 23-2 ]: ExecuteScript 回傳 null/void 時,容易被誤判為成功,需要用 Velocity internal 狀態(error/status/message)判斷是否失敗

- [ DONE ] 24.Skip 按紐字再大一點 16px 吧
  - [ 錯誤 24-1 ]: 無

- [ DONE ] 25.SpashAPI 的 velocity 已更新,以下是他的接口:
  SpashAPIVelocity.API.AttachAPI();
  SpashAPIVelocity.API.ExecuteScript(richTextBox1.Text);
  SpashAPIVelocity.API.KillRoblox();
  SpashAPIVelocity.API.IsRobloxOpen();
  SpashAPIVelocity.API.IsAttached();
  SpashAPIVelocity.API.AutoAttachAPI(true);
  SpashAPIVelocity.Custom.UserAgent("example");
  SpashAPIVelocity.Custom.IdentifyExecutor("example", "1.0");
  - [ 錯誤 25-1 ]: 新版 Velocity API 類型可能是 SpashAPIVelocity+API / SpashAPIVelocity.API,需要同時支援兩種命名
  - [ 錯誤 25-2 ]: Custom.UserAgent/IdentifyExecutor 可能是 method 或 static member,需要兼容處理
  - [ 錯誤 25-3 ]: 缺少 VelocityCustomTypeCandidates 造成 build error,已補回

  - 更新功能:
  * Auto Update
  * Custom API

- [ DONE ] 26.分析 RKOAPI-Velocity 的 切換 API 功能,看能不能找出 spashapi 的 velocity 無法 executescript 問題,代碼:
  - [ 錯誤 26-1 ]: RKO 的 Execute() 在 injected_pids=0 時直接回 NotAttached,不會假裝成功；我們這邊需要把 NotAttached 類型/字串視為失敗
  - [ 錯誤 26-2 ]: RKO 的 NamedPipes 會先檢查 pipe 是否存在,不存在就不送；我們這邊只能透過 API 回傳值/狀態來判斷是否真的有注入
  - [ 錯誤 26-3 ]: 我們的 Velocity ExecuteScript 若回傳 Task,等待會造成 UI/提示框卡頓,需要避免阻塞

  - 結論:
  1. ExecuteScript 回傳 string 時不能一律當成功,需要把 not attached/not injected/fail/error/denied 當作失敗
  2. 新版 Velocity API 類型可能是 SpashAPIVelocity.API/SpashAPIVelocity+API,需要兼容
  3. 若 ExecuteScript 回傳未完成 Task,先不要等待(避免卡頓),改用後續/狀態判斷
  - tAPI/rkoAPI-velocity1.txt
  - tAPI/rkoAPI-velocity2.txt

- [ DONE ] 27.SETTING 的透明度不要有框,就是旁邊條整透明的線
  - [ 錯誤 27-1 ]: 無

- [ DONE ] 28.data 類別的程式目錄/Monaco/waveUI 資源(漢化的 waveUI 圖片改成 waveUI 資源)...後面,有一個 copy.png,用來複製路徑,且滑鼠懸浮在上面的時候要顯示 copy 字樣,動畫是,從 copy.png 上面往上滑淡入一個小框(沒有圓角),滑鼠需懸浮此 copy.png 0.5 秒才顯示提示 copy 框,需要漢化,英文也要添加
  - [ 錯誤 28-1 ]: 無

- [ DONE ] 29.程式目錄下方添加開啟 APIs 資料夾(樣式/按鈕都跟程式目錄一樣,也要有 copy(需求的第 28 點))
  - [ 錯誤 29-1 ]: 無

- [ DONE ] 30.移除 Velocity 的 AutoAttachAPI 與 Custom.UserAgent/IdentifyExecutor 設定
  - [ 錯誤 30-1 ]: AutoAttachAPI/Custom UA 可能觸發外部 HTTP 行為(403)導致 attach 異常,依需求先移除

- [ DONE ] 31.setting 的 copy.png 不是放到開啟的旁邊,是放到"\..."旁邊,要有 2px 距離,範例:
- 31-1.D:\data_Files\Project\AzureaX\RH\Executor\Outputs\Executor\Assets\.... {copy.png}
- 31-2.D:\data_Files\Project\AzureaX\RH\Executor\Outputs\Executor\Assets\.... {copy.png}
- 31-3.僅程式目錄/Monaco/WaveUI 資源需要
  - [ 錯誤 31-1 ]: 無

- [ DONE ] 32.WaveUI 資源複製的路徑不需要 "\*.png"
  - [ 錯誤 32-1 ]: 無

- [ DONE ] 33.複製成功時,copy.png 上面的文字顯示已複製
  - [ 錯誤 33-1 ]: 無

- [ DONE ] 34.修復英文版的 execute/save/clear/killroblox,沒有英文完全
  - [ 錯誤 34-1 ]: 無

- [ DONE ] 35.scripts 那邊,點任意一個 script 後,旁邊的 SCRIPTS 會顯示該 script 狀態,比如 已驗證 /鑰匙 /模式等可以參考 API.scriptblox.md 順便優化變成 markdown(無需漢化)
  - [ 錯誤 35-1 ]: 無

- [ DONE ] 36.軟體邊框可以像軟體邊框上方一樣多出一條白色的線(左右下沒有多出需要改),我怕軟體黑色與其他黑色重疊
  - [ 錯誤 36-1 ]: 無

- [ DONE ] 37.是文檔變成 markdown,不是軟體
  - [ 錯誤 37-1 ]: 無

- [ DONE ] 38.移除軟體邊框的白色線
  - [ 錯誤 38-1 ]: 無

- [ DONE ] 39.script page 不能點擊 script 查看內容
  - [ 錯誤 39-1 ]: 無

- [ DONE ] 40.script page 可點擊並顯示內容
  - [ 錯誤 40-1 ]: 無

- [ DONE ] 41.setting 是一複製就顯示已複製
  - [ 錯誤 41-1 ]: 無

- [ DONE ] 42.script page 的 script 點了沒反應,旁邊沒有變化
  - [ 錯誤 42-1 ]: 無

- [ DONE ] 43.告訴我 setting 旁邊那個快速前往類別功能的專有名詞
  - [ 錯誤 43-1 ]: 無

- [ DONE ] 44.告訴我除了 41 點以上之外還沒有完成的需求
  - [ 錯誤 44-1 ]: 無

- [ DONE ] 45.setting 調整透明度,不是點中間的地方就跑到 0.1 或是 1,而是點了跑到相對應的透明度
  - [ 錯誤 45-1 ]: 無

- [ DONE ] 46.Anchor Navigation 可以跟網路上的一樣嗎?我不要那種點了跳到 app 又跳到我點的錨點,而是點了直接跳到我點的錨點,可以嗎?
  - [ 錯誤 46-1 ]: 無

- [ DONE ] 47.editor page 的旁邊 explorer 的已儲存腳本,的旁邊按鈕,可以點擊,並且點擊後要旋轉(就像點了下方會有儲存的腳本,在點一次就儲存的腳本內容會消失(上下滑動),需要讀取 workspace 資料夾來展示.luau/.txt,點了後,創建新的 tab,名稱是點選的.luau/.txt 名稱,內容是點選的腳本內容)
  - [ 錯誤 47-1 ]: 無

- [ DONE ] 47-1.已儲存腳本旁邊的加號點擊後會顯示圖十的 ui(軟體中間顯示,可以一動,有淡出淡入動畫)創建後,會自動儲存到 workspace 資料夾,儲存的名稱是輸入框的名稱,儲存的內容是輸入框的內容,並且 tab 也要自動創建(敘述的有點差,不懂/未定義的地方可以告訴我)
  - [ 錯誤 47-1-1 ]: 無

- [ DONE ] 48.editor 的 execute/clear/save/open 圓角要跟 tab 的一樣
  - [ 錯誤 48-1 ]: 無
- [ DONE ] 48-1.key system 的 login/get key 圓角也要要跟 tab 的一樣
  - [ 錯誤 48-1-1 ]: 無
- [ DONE ] 49.設定的 Anchor Navigation 還原到我還沒叫你改的時候(第 46.)
  - [ 錯誤 49-1 ]: 無

- [ DONE ] 50.script page 的 script 點了不是顯示腳本,而是顯示腳本狀態,已驗證 /鑰匙 /模式等可以參考 API.scriptblox.md
  - [ 錯誤 50-1 ]: Run.Text 預設 TwoWay 綁定會造成 XamlParseException(唯讀屬性),需改為 OneWay

- [ DONE ] 50-1.格式為: Key = {需要或不需要(字體為粗體)}(要漢化也要有英文版本)
  - [ 錯誤 50-1-1 ]: 無

- [ DONE ] 50-2.格式為: Mode = {免費或付費(字體為粗體)}(要漢化也要有英文版本)
  - [ 錯誤 50-2-1 ]: 無

- [ DONE ] 50-3.格式為: Verify = {已驗證或未驗證(字體為粗體)}(要漢化也要有英文版本)
  - [ 錯誤 50-3-1 ]: 無

- [ DONE ] 51.移除 script 的自訂篩選條件,跟那個框,但是保留 key/mod/verfiy
  - [ 錯誤 51-1 ]: 無

- [ DONE ] 52.任何設定的都要馬上生效,比如透明度只在進入 home page 才會生效,但我需要在 load app 就生效
  - [ 錯誤 52-1 ]: 無

- [ DONE ] 53.setting page 的透明度的數字,雙擊數字可以更改數字並變成那個是數字的透明度,跟 editor page 的 tab 一樣
  - [ 錯誤 53-1 ]: 無

- [ DONE ] 54.tab 雙擊變更名稱,當點到 monaco 的地方時會失效,修復這個問題
  - [ 錯誤 54-1 ]: 無

- [ DONE ] 55.點擊 editor page 的 explorer 右邊的加號後,沒有跑出圖十的輸入框,請修復
  - [ 錯誤 55-1 ]: 無

- [ DONE ] 56.editor page 的 explore 以儲存的腳本需要熱更新,不要重開才顯示
  - [ 錯誤 56-1 ]: 無

- [ DONE ] 56-1.以儲存的腳本樣式為(已經下拉的樣式): {note.png} {腳本名稱}.{檔名},不需要框就這樣顯示
  - [ 錯誤 56-1-1 ]: 無

- [ DONE ] 56-2.實現上面搜索框搜尋 workspace 名稱功能,使用者停頓的話,等待 0.5 秒直接搜索(不需要點擊右邊的 search.png)
  - [ 錯誤 56-2-1 ]: 無

- [ DONE ] 57.editor page 的 explorer 的搜索框裡面打字需要白色一條在閃爍,讓使用者知道要輸入
  - [ 錯誤 57-1 ]: 無

- [ DONE ] 57-1.以儲存的腳本旁邊加號,滑鼠移到上面不需要白色的效果,要有圓角 5px
  - [ 錯誤 57-1-1 ]: 無

- [ DONE ] 57-2.點擊加號後沒有跑出圖十的輸入框,請修復
  - [ 錯誤 57-2-1 ]: Monaco WebView2 層級會遮住彈窗

- [ DONE ] 57-3.以儲存的腳本裡面的腳本名稱要粗體字,腳本與腳本之間要有間隔,並且會有一條白色線來區分(上面腳本與線的距離為 3px,下面也是),還要添加滾輪功能
  - [ 錯誤 57-3-1 ]: 無

- [ DONE ] 58.editor pag的explorer以儲存腳本,線是淺色的,腳本與線的距離改5px
  - [ 錯誤 58-1 ]: 無

- [ DONE ] 59.editor page的explorer創建新的script,要跟圖十完全一樣,圓角/內容/字體/大小/顏色
  - [ 錯誤 59-1 ]: 無

- [ DONE ] 60.移除script page的右邊script內容key..等的最外框,只顯示key/mode/verify
  - [ 錯誤 60-1 ]: 無

- [ DONE ] 61.editor page的tab右鍵添加"重新命名","關閉所有分頁",順序為: 釘選 → 重新命名 →{淺灰色的分隔線} → 關閉所有分頁(只關閉沒有釘選的),樣式都跟釘選一樣,圖案位置也是
  - [ 錯誤 61-1 ]: 無
- [ DONE ] 61-1.重新命名的圖案: rename.png
  - [ 錯誤 61-1-1 ]: 專案內未找到 rename.png，請提供檔案或確認路徑
- [ DONE ] 61-2.關閉所有分頁的圖案: remove.png
  - [ 錯誤 61-2-1 ]: 專案內未找到 remove.png，請提供檔案或確認路徑

- [ DONE ] 62.EDITOR page的explorer如果當前分頁已經存在那個script,就跳到那個分頁
  - [ 錯誤 62-1 ]: 無

- [ DONE ] 62-1.tab分頁關閉需要動畫(符合添加tab的動畫)
  - [ 錯誤 62-1-1 ]: 無

- [ DONE ] 63-2.當超出可顯示的tab時,那個加號會錯位,請固定他不要亂跑
  - [ 錯誤 63-2-1 ]: 無

- [ DONE ] 64.explorer的savedscript也可以點擊,也會顯示以儲存的腳本
  - [ 錯誤 64-1 ]: 無

- [ DONE ] 64-1.滾輪太粗,要跟tab的那邊一樣
  - [ 錯誤 64-1-1 ]: 無

- [ DONE ] 64-2.選單要到最底(留10px),才會有滾輪
  - [ 錯誤 64-2-1 ]: 無

- [ DONE ] 65.需求的57-3.改成rgb(79, 79, 79)
  - [ 錯誤 65-1 ]: 無

- [ DONE ] 66.移除setting page的錨點功能的點擊後高亮藍色(功能要,但是不需要變藍色)
  - [ 錯誤 66-1 ]: 無

- [ DONE ] 67.editor page的tab滾輪顏色改成淺灰色rgb(0, 0, 0)
  - [ 錯誤 67-1 ]: 無

- [ DONE ] 68.我能不重啟的情況下來變更字體嗎?比如那個字體的粗體不支援中文只支援英文,但是我想要都是粗體
  - [ 錯誤 68-1 ]: 無

- [ DONE ] 69.將editor page的explorer裡面以儲存腳本能展示的框拉到最底並保留10px空隙(約拉到killroblox按鈕一樣的高度)
  - [ 錯誤 69-1 ]: 無

- [ DONE ] 70.editor page所有滾輪用白色
  - [ 錯誤 70-1 ]: 無

- [ DONE ] 71.editor page的重新命名會導致添加tab的圖案位置錯位
  - [ 錯誤 71-1 ]: 無
- [ DONE ] 71-1.滾輪沒有改成白色
  - [ 錯誤 71-1-1 ]: 無
- [ DONE ] 71-2.explorer的創建新腳本,enter script name改成粗體,其他不變
  - [ 錯誤 71-2-1 ]: 無

- [ DONE ] 72.editor page的explorer下拉以儲存的腳本的"沒有以儲存的腳本"因該顯示在"以儲存的腳本"的下面一點
  - [ 錯誤 72-1 ]: 無

- [ DONE ] 73.setting的開啟version.json下方添加開啟ax-log日誌資料夾(顯示名字就寫Log資料夾不需要加"ax-"),樣式都跟上面一樣(開啟version.json)
  - [ 錯誤 73-1 ]: 無

- [ DONE ] 74.editor page的tab,我希望點擊到explorer的以儲存的腳本時,是會自動切到那個腳本對吧,再添加tab列表也要移動到那個腳本,如果只有切換到那個腳本,使用者還要自己滑到那個腳本,很不方便
  - [ 錯誤 74-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)
  - [ DONE ] 74-1."以儲存的腳本"也要可以點擊,功能跟左邊下拉箭頭一樣
    - [ 錯誤 74-1-1 ]: 無
  - [ DONE ] 74-2.下拉顯示以儲存的腳本的動畫是從上面往下,如果是關閉顯示以儲存的腳本就是從下面往上
    - [ 錯誤 74-2-1 ]: 無
  - [ DONE ] 74-3.explorer已經下拉顯示以儲存的腳本,裡面的"沒有已儲存的腳本",這個字因該放到"示以儲存的腳本"這個字下面一點,而不是下拉後最底下的位置
    - [ 錯誤 74-3-1 ]: 無

- [ DONE ] 75.小Wave(浮動圓球)功能
  - 75-1.最小化按鈕
    - 75-1-1.視窗關閉按鈕左側新增 minimize_wave.png
    - 75-1-2.點擊後縮成小Wave(wave_logo.png)
  - 75-2.小Wave 視覺與位置
    - 75-2-1.命名: 縮小後的浮動圓球統一稱為「小Wave」
    - 75-2-2.預設位置: 螢幕右下角(非工作列區域)
    - 75-2-3.外觀: 圓形圖案(wave_logo.png),無黑色外框
  - 75-3.小Wave 互動
    - 75-3-1.雙擊(短按2下)開啟主程式,顯示點擊指標
    - 75-3-2.長按拖曳才可移動,顯示移動專用滑鼠指標
    - 75-3-3.拖曳時帶脈衝動畫效果
    - 75-3-4.拖曳移動為拋擲感(不即時跟隨滑鼠,放開後有拋擲慣性)
  - 75-4.小Wave 右鍵選單(樣式同 Editor Page 的 Tab 右鍵選單)
    - 75-4-1.{maximize_wave.png} 打開軟體
    - 75-4-2.{淺灰色分隔線}
    - 75-4-3.{close.png} 關閉軟體(同時關閉小Wave,要淡出)
    - 75-4-4.選單顯示/隱藏動畫
  - 75-5.動畫流程(必須序列執行)
  - 75-5-1.主視窗先淡出(0.5秒)
  - 75-5-2.淡出完成後,小Wave淡入(0.5秒)
  - 75-5-3.小Wave先淡出(0.5秒)
  - 75-5-4.淡出完成後,主視窗淡入(0.5秒)

- [ DONE ] 76.小wave右鍵菜單需要有英文跟中文
  - 76-1.脈衝不是小wave一跳一跳的,要類似從小wave向外噴一個圓形,0.5秒後消失
  - 76-2.都說了不要跟著滑鼠移動
  - 76-3.小wave的脈衝不是點擊就脈衝而是一直脈衝直到開啟主程式
  - [ 錯誤 76-1 ]: dotnet build 警告 NU1903 (SkiaSharp 2.88.0 高嚴重性弱點)
  - [ 錯誤 76-2 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 77.小wave被點擊,如果小wave有空白處會沒反應,需要改成只要在那張圖片裡面,都能觸發,並且背景不變
  - [ 錯誤 77-1 ]: 無
- [ DONE ] 77-1.需要有被丟出去的效果,就是使用者移動不是會放開嗎?放開後,小wave要自己被往前丟,碰到螢幕邊緣反彈其他地方
  - [ 錯誤 77-1-1 ]: 無
- [ DONE ] 77-2.脈衝頻率改成1s一次脈衝
  - [ 錯誤 77-2-1 ]: 無
- [ DONE ] 77-3.右鍵菜單需要中英文
  - [ 錯誤 77-3-1 ]: 無
- [ DONE ] 77-4.主程式顯示後,透明度沒有變成使用者調的,會變成透明度1
  - [ 錯誤 77-4-1 ]: 無
- [ DONE ] 77-5.setting添加懸浮球設定(小wave,顯示名稱使用Wave懸浮球),錨點位置在應用程式下方
  - [ 錯誤 77-5-1 ]: 無
- [ DONE ] 77-5-1.內容有可以更改懸浮球透明度(與主程式不同),還有勾選,可以當啟動wave後,直接顯示小wave,而不是主程式
  - [ 錯誤 77-5-1-1 ]: 無
- [ DONE ] 77-6.丟出去的效果不要等使用者長按一秒,而是不管長按多少秒,移動並放開就直接丟出去,如果是移動並放開且到目的地沒有繼續移動,就不需丟出去的動畫
  - [ 錯誤 77-6-1 ]: 無
- [ DONE ] 77-6-1.丟出去效果卡卡的,優化他,不要一偵一偵
- [ DONE ] 78.小wave需要記住上次關閉的位置座標(可寫入config.cfg)
  - [ 錯誤 78-1 ]: 無
- [ DONE ] 79.Settng page的懸浮球選項在透明度下方可以多出更改特效的下拉選單,樣式要跟外觀的"更改語言"一樣,比如可以切換脈衝/淡出淡入(1s)特效
- [ DONE ] 80.'SettingsView' 未包含 'FontCombo_OnSelectionChanged' 的定義，也找不到可接受類型 'SettingsView' 第一個引數的可存取擴充方法 'FontCombo_OnSelectionChanged' (是否遺漏 using 指示詞或組件參考?) 修復
  - [ 錯誤 80-1 ]: 無

- [ DONE ] 81.修復小Wave設定/特效相關的編譯錯誤與未使用欄位
  - [ 錯誤 81-1 ]: SettingsView 缺少 SmallWaveOpacityValueEditBox_OnLostFocus/SmallWaveOpacitySlider_OnValueChanged/SmallWaveOpacityValueText_OnMouseLeftButtonDown/SmallWaveOpacityValueEditBox_OnKeyDown
  - [ 錯誤 81-2 ]: SettingsView 缺少 SmallWaveStartupCheckBox_OnChanged
  - [ 錯誤 81-3 ]: WaveMinimizeWindow 缺少 EffectPulse/ApplyEffect
  - [ 錯誤 81-4 ]: WaveMinimizeWindow.\_isLongPressActive 與 SettingsView.\_isSmallWaveOpacityEditing 未使用

- [ DONE ] 82.小wave圖片有空隙會點不到
  - [ DONE ] 82-1.是碰到牆壁立刻反彈
  - [ DONE ] 82-2.被丟出去的效果是依據移動距離與時間計算速度,然後依照速度與時間計最後依照移動距離計算移動時間算移動距離,
  - [ DONE ] 82-3.wave懸浮球的setting需要你幫我寫英中語言
    - [ DONE ] 82-3-1. start in small wave是啟動軟體後,變成小wave
    - [ DONE ] 82-3-2.effect功能沒有成功
    - [ DONE ] 82-3-3.移除effect下面的線(細的,不是粗的那條)
    - [ DONE ] 82-3-4.錨點導航需要有懸浮球(不要寫Wave懸浮球)
  - [ DONE ] 82-4.右鍵沒有中英語言,需要支援
- [ DONE ] 83.setting的切換字體,我希望能直接讀取windows/fonts的,然後選擇字體的樣式下拉選單,不要顯示預設的字型,而是改成那個字體的樣式,就像visual studio切換字體一樣的功能

- [ DONE ] 84.小wave還是會一偵一偵的移動,我要他穩定60偵
  - [ 錯誤 84-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)
- [ DONE ] 85.移除setting的啟動後顯示懸浮球功能
  - [ 錯誤 85-1 ]: 無
- [ DONE ] 86.setting的debug添加可以更改小wave的偵數,樣式一樣
  - [ 錯誤 86-1 ]: 無
- [ DONE ] 87.小wave的圖片,如果是透明的,會點擊不到,我需要不管圖片裡面是否透明,都要點擊的到(不是小wave透明度,而是懸浮球的圖片)
  - [ 錯誤 87-1 ]: 無
- [ DONE ] 88.setting錨點那邊,的"自訂Wave的設定"旁邊添加搜索框,用於搜索setting,然後如果搜索到的setting裡面有相關字,就那個字高亮藍色
  - [ 錯誤 88-1 ]: 無

- [ DONE ] 89.自訂字體那邊,下拉後也要有搜索框(第一個是default,然後搜索框在default上方,樣式跟editor page的explorer 搜索框一樣,search圖案,閃爍I讓使用者知道,之類的都要一樣)
  - [ 錯誤 89-1 ]: 無

- [ DONE ] 90.小wave的脈衝卡卡的優化他,效能像淡出淡入一樣,不要太卡
  - [ 錯誤 90-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)
- [ DONE ] 91.setting搜索功能,當找到有相關字,立馬滑到那個相關字(滑動到字在中間,滑到第一個從上往下的最近相關字)
  - [ 錯誤 91-1 ]: 無

- [ DONE ] 92.editor page的explorer下拉,新增腳本因該是從上到下,而不是從下到上,且"沒有已儲存腳本"要放在最上面
  - [ 錯誤 92-1 ]: 無
  - [ DONE ] 92-1.搜索後不是顯示"沒有已儲存腳本",而是顯示"沒有符合的腳本",需要漢化
    - [ 錯誤 92-1-1 ]: 無
- [ DONE ] 93.在setting page的debug 懸浮球偵數上方添加"軟體同時開啟",預設是"不允許",他類似開關,樣式要字定義符合setting page,這功能是避免使用者開啟一次又多開一次軟體,造成不必要的錯誤,需要漢化
  - [ 錯誤 93-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 94.軟體同時開啟的控制改成 switch,不是勾選
  - [ 錯誤 94-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 95.軟體同時開啟的 switch 需要切換動畫,往右滑跟往左滑的動畫
  - [ 錯誤 95-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 96.小wave的特效(已調成淡出淡入),關閉軟體後再開啟還是會變成脈衝,修復此問題,讓他的特效變成關閉前的樣子
  - [ 錯誤 96-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 97.editor page的explorer已儲存腳本,顯示多一點腳本,不是只顯示兩個,而是顯示到底部,但底部留10px
  - [ 錯誤 97-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 98.editor page的explorer新增腳本,如果使用者輸入"1.0."會顯示"只允許luau...."我不要,輸入腳本就輸入腳本,不需要寫副檔名
  - [ 錯誤 98-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 98-1.輸入腳本名稱提示框的"取消按鈕",他的最左邊添加切換儲存方式".luau"/".lua"/".txt",預設是luau,是個下拉選單,樣式就跟setting page的下拉選單一樣(比如選擇語言的),功能要實現
  - [ 錯誤 98-1-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 99.setting的debug添加"解除分頁上限",功能是將editor的tab上限30設為無限,是勾選功能,默認不勾選,要中英語言,樣式一樣(setting的置頂...等),需要寫入config.cfg,且要有記憶性
  - [ 錯誤 99-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 100.全螢幕後,透明度會從使用者條的(0.1)變成1,請修復此問題
  - [ 錯誤 100-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 101.此app不會在後台運行(目前),如果使用者關閉的話,軟體直接強制關閉自己,避免後台殘留
  - [ 錯誤 101-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 102.寫入config.cfg的("theme="/"language="/"font(從ui_font改成font)"/"allow_multi_instance"/"opacity="不要)
  key=
  minimize_window_left=
  minimize_window_top=
  skip_load_app=
  small_wave_effect=
  small_wave_fps=
  small_wave_opacity=
  start_on_boot=
  最前面都加上"WaveUI",變成:
  WaveUI_key=
  WaveUI_minimize_window_left=
  WaveUI_minimize_window_top=
  WaveUI_skip_load_app=
  WaveUI_small_wave_effect=
  WaveUI_small_wave_fps=
  WaveUI_small_wave_opacity=
  WaveUI_start_on_boot=
  - [ 錯誤 102-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 103.editor page的tab被關閉後,在打開,我希望能記住被關閉前的分頁,這樣就不用一直點
- [ DONE ] 103-1.當那個tab被關閉時(僅檢測到裡面有輸入文字),就彈出自訂義窗口(類似msgbox,樣式符合軟體),提示說,是否關閉,裡面還有一個勾選的功能,就是"記住我的選擇",需要中英語言,寫入config.cfg保存
  - [ 錯誤 103-2 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 104.我希望軟體同時開啟(不允許的狀態),偵測到的話,就彈出自訂義窗口(類似msgbox,樣式符合軟體),提示說"當前不可開啟多個Wave {換行} 可以在setting的debug更改"
  - [ 錯誤 104-1 ]: dotnet build 警告 MSB3270 (MSIL 與 SpashAPIXeno AMD64 不相符)

- [ DONE ] 105.editor,explorer新增腳本的選擇副檔名錯位了,我要放在提示框最左邊,右下角

- [ 已修正 103-1 ]: ClosePrompt WaveCheckBox 缺少 WaveBlue 資源導致 BorderBrush UnsetValue

- 104.setting的選擇api,當檢測到"../version.json"被刪除時,裡面的對話框顯示說,"version.json丟失,請重新執行Checker.exe"(用紅字顯示),然後下面有一個按鈕就是執行Checker.exe(按鈕放在右下角,按鈕只有在"version.json"被刪除時才顯示,並且點擊後會關閉當前軟體執行Checker.exe),需要中英語言
- 104-1.Checker.exe路徑是寫死的,在"../Checker.exe",需要中英語言
- 104-1-1.如果未找到Checker.exe提示"未找到Checker.exe {換行} 請重新下載Checker.exe"並跳轉到"https://axproject.dpdns.org/",需要中英語言

- 105.設定的重新載入下面添加當前軟體版本,目前是1.0.1,樣式一樣也需要線,需要中英語言,然後寫入"../version.json"的AppVersion

- 106.設定的重新載入上面添加禁用提示框,旁邊是按鈕,字是"篩選",點開後,會像選擇api時點開一樣,樣式一樣,需要中英語言,寫入config.cfg(前面要加上"WaveUI"),要有動畫
- 106-1.篩選資訊:
  - 格式為(類似表格): {勾選框} {名稱} {空白} {switch}
  - switch為: {啟用/禁用}
  - 勾選框預設是沒有然後最上面有個勾選框是"全部",點的話,所有勾選框變成已勾選
  - 此功能用來調整 Executed / 儲存 / KillRoblox ...等(有用到提示框的東西全部都列出來),的提示框(debug的測試提示框可以忽略,並且在debug的測試提示框旁邊寫"不受禁用與啟用提示框影響")

- 107.script page的搜索框消失了,請放回去,我要搜索框與功能
