[✓] 1. 幫我在選擇語言跟theme那邊,告訴我ui可以怎樣設計{f:a_n}
[✓] 2. 在 SetupWindow 實作主題選擇方案 B（左側清單 + 右側預覽）並維持現有風格/行為
[✓] 3. SetupWindow大改內容:
3-1. 移除theme選擇ui的W2..等的圖案,只保留字
3-1-1. 盡量與theme-option-b-mock.svg 的選擇ui一樣(不含preview)
3-2. 圓角不能太圓,最多3px
3-3. 移除Preview跟選擇theme的框
3-4. 移除preview展示圖片的黑色背景
3-5. 下一步/完成/返回的按鈕不要有背景,只要字
3-5-1. 點擊下一步後,要有淡出動畫(含字),不是瞬間切到選擇theme
3-6. 軟體標題的"Wave設定"改成DropUI
3-7. 切換theme後,preview要淡出淡入
3-8. 選擇主題的字(WaveUI-2025/WaveUI-2026/2)刪除"UI"這兩個字,然後字體變成粗體
[✓] 4. 我打算theme讀取png都是直接在WaveUI裡面(會有2025/2026版,Monaco也是)
4-1.Monaco的資料夾名稱先改成2025(因為現在也只有2025版),我以後會創建2026的monaco
4-2.目前monaco資料夾位置: Assets/WaveUI/Monaco
4-3.希望改成的路徑(2025版跟2026/2(目前日期未知)版): Assets/WaveUI/Monaco/2025
4-3-1.你只需把2025版monaco路徑改成Assets/WaveUI/Monaco/2025,我已經把檔案移動到2025資料夾裡面
4-3-2.2025版的setting開啟monaco路徑也要改
4-4.注意:Monaco與png都是絕對路徑,不可能會出現monaco1之類的

[✓] 5.setup的preview圖片使用Assets/Setup裡面的Wave2025.png等

[✓] 6. 我好奇,我把WaveUI資料夾裡面的檔案移動到WaveUI/2025,為什麼還是可以讀到,比如:
6-1. WaveMinimizeWindow.xaml.cs
6-2. WaveShell.xaml
6-3. WaveAssets.cs
6-4. 之類的檔案,可以在Executor\WaveUI\2025 看到
6-5. 幫我檢查這樣改會不會有其他問題

[✓] 7. 把key system page也{f:lang},目前是寫死的,我不要這樣
[✓] 8. 你告訴我,config.cfg的"WaveUI"我現在有2025版跟2026版,我要區分他們嗎?
[✓] 8. 將config.cfg的WaveUI_key改成全局"key"

[✓]9. 在setting page的app列表裡面的透明度上方添加變更Key功能,樣式同setting(不含按鈕),他是個按鈕,點擊後,會彈出窗口,窗口內容:
9-1. 樣式同editor page的explorer添加腳本的窗口差不多
9-2. 有取消與變更按鈕,還有輸入key的框,然後會顯示上次的key
9-3. {f:lang}
9-4. 取消與變更按鈕樣式動畫是editor page的executor
9-5. 我希望字跟上面的一樣靠左邊,然後按鈕靠右邊,按鈕樣式跟setting一樣,然後不要key圖案,只要字
9-6. 需要有字體切換功能(變更key彈窗)

[✓]10. 所有page的字體都要有字體切換功能
[✓]11. 將setting page的debug 懸浮球偵數移動到懸浮球透明度上方
[✓]12. 將變更key移動到重新載入上方
[✓]13. 告訴我setting的所有專有名詞
