# [ 重要 ]:

- 1.任何改動都需要跑一次測試
- 2.沒有經過我的允許,不准動其他道程式碼,只能動我說的
- 3.任何問題都可以提問
- 4.請把 "[ 重要 ]" 的內容加入到您的記憶體,但是還是要檢查一下有沒有多出項目
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

- 48.editor 的 execute/clear/save/open 圓角要跟 tab 的一樣
- 48-1.key system 的 login/get key 圓角也要要跟 tab 的一樣
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

- 51.移除 script 的自訂篩選條件,跟那個框,但是保留 key/mod/verfiy
- 52.任何設定的都要馬上生效,比如透明度只在進入 home page 才會生效,但我需要在 load app 就生效
- 53.setting page 的透明度的數字,雙擊數字可以更改數字並變成那個是數字的透明度,跟 editor page 的 tab 一樣
- 54.tab 雙擊變更名稱,當點到 monaco 的地方時會失效,修復這個問題
- 55.點擊 editor page 的 explorer 右邊的加號後,沒有跑出圖十的輸入框,請修復
- 56.editor page 的 explore 以儲存的腳本需要熱更新,不要重開才顯示
- 56-1.以儲存的腳本樣式為(已經下拉的樣式): {note.png} {腳本名稱}.{檔名},不需要框就這樣顯示
- 56-2.實現上面搜索框搜尋 workspace 名稱功能,使用者停頓的話,等待 0.5 秒直接搜索(不需要點擊右邊的 search.png)
