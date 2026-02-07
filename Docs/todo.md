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

6. 我好奇,我把WaveUI資料夾裡面的檔案移動到WaveUI/2025,為什麼還是可以讀到,比如:
   - WaveMinimizeWindow.xaml.cs
   - WaveShell.xaml
   - WaveAssets.cs
   - 之類的檔案,可以在Executor\WaveUI\2025 看到
   - 幫我檢查這樣改會不會有其他問題

7. 把key system page也{f:lang},目前是寫死的,我不要這樣
8. 你告訴我,config.cfg的"WaveUI"我現在有2025版跟2026版,我要區分他們嗎?
