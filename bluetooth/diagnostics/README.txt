Windows 藍牙／音訊完整診斷 v1.0

怎麼用
1. 完整解壓縮此工具包，保留所有 .ps1 與 .cs 檔案。
2. 保持 PhoneDesk Bluetooth 開啟、顯示通道已開啟。
3. 手機播放音樂，音訊輸出選電腦；暫停電腦其他音樂／影片，避免干擾音量活動觀察。
4. 雙擊 Run-Diagnostics.cmd，準備好後按 Enter。通常約 1～3 分鐘，速度依裝置與事件記錄而異。
5. 等畫面顯示 Completed，把 reports 資料夾內本次產生的 ZIP 傳回。
若雙擊未執行，可在 Windows PowerShell 輸入：
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Collect-AudioDiagnostics.ps1

這是唯讀檢查
不修改預設輸出、音量、登錄、服務、驅動、配對或防火牆，不下載／安裝東西，不連網上傳，不錄音。
ExecutionPolicy Bypass 僅用於這次 PowerShell 程序，不變更系統的永久執行原則。
一般權限先跑即可；沒有權限讀取的記錄會列在報告，不要求一定用系統管理員。

檢查範圍
- Windows 版本、架構、PowerShell、權限與時區。
- 音訊／藍牙相關服務狀態與啟動類型。
- 目前存在的藍牙、音效與音訊端點，裝置問題碼、驅動提供者、版本、日期、簽章。
- 播放／錄音端點狀態，主控台、媒體與通訊預設端點，音量、靜音。
- 播放端點的應用程式音訊工作階段：處理程序名稱、狀態、音量與靜音。
- 約 10 秒的端點音量峰值／活動統計。只讀 Windows 音量表，不保存音訊內容。
- Windows 已快取的藍牙配對及標準音訊／HID 服務旗標。
- 相關政策中的數值設定，以及最近 6 小時可讀取的藍牙／音訊／PnP／應用程式錯誤事件。
- 可找到時附上正在執行的 PhoneDesk 的 current.log。

報告界線
音量表會包含電腦的其他聲音，不能直接證明手機音訊已進來。
應用程式音量是列舉當下的快照；音量活動是接下來十秒的統計。
快取服務不是完整即時 SDP 探測；缺少服務不等於硬體永久不支援。
報告無法替沒有建立的音訊通道補出聲音，也不保證跑完就修好。

分享前檢查
會遮蔽目前 Windows 帳戶名、電腦名、常見藍牙位址與帳戶 SID；PnP／端點 ID 以雜湊代碼表示。
裝置／應用程式顯示名稱與 Windows 事件文字仍可能含個人資訊，請先查看 SUMMARY.txt、diagnostics.clixml（XML 文字檔）再分享。
reports 內只打包這次產生的報告，不會把整台電腦或其他資料夾一起壓縮。

工具包內的 AudioProbe.cs、BluetoothDiagnostics.cs、DailyLog.cs 都是可閱讀的原始碼，會由 Windows 的 Add-Type 在本機編譯使用。
