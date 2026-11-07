PhoneDesk Bluetooth v0.2／switch2 分支（Windows x64）

用途
將已配對的 Switch 或其他藍牙音訊來源送到電腦的預設耳機／喇叭，與電腦聲音混合。
這是獨立藍牙接收器，不需 PhoneDesk AirPlay、ESP32、手機 App 或 USB 線。

開啟
完整解壓縮，雙擊 PhoneDeskBluetooth.exe。保留旁邊的 .config。
需要 Windows 10 2004（build 19041）或更新版本／Windows 11、.NET Framework 4.8、可用的藍牙硬體與相容驅動。
一般使用不需要系統管理員權限。

第一次連線
1. 電腦開啟藍牙，將 Windows 預設聲音輸出設成想用的耳機。
2. 按「開啟藍牙設定」，完成 Windows 與 Switch 的配對。
   Switch 端：設定 → 藍牙音訊 → 新增裝置／配對裝置。
   電腦若沒有出現在 Switch：Windows 藍牙設定 → 更多藍牙設定，檢查「允許藍牙裝置尋找這部電腦」，並保持配對畫面開啟。
   某些組合需從 Windows「新增裝置 → 藍牙」找 Switch；Switch 同時維持配對搜尋。
   系統配對由 Windows 處理，本程式不會自動接受任何陌生裝置。
3. 回到本程式按「重新搜尋」，只列出 Windows 宣告可接收的已配對音訊來源。
4. 選擇 Switch，按「開始接收」。必要時在 Switch 的藍牙音訊清單選擇電腦連線。
5. Switch 播放遊戲聲音；Windows 音量混音器調整音量。電腦原本聲音可同時播放。
6. 本程式需保持開啟；停止接收或關閉視窗會釋放連線。

重要界線
目前完成 Windows API、沒有藍牙硬體時的處理、裝置搜尋及介面測試。
開發電腦沒有藍牙收發器，因此尚未實測 Switch 配對、音訊接收或延遲，不能保證所有藍牙驅動都可用。
有藍牙不一定代表能讓 Switch 成功建立 A2DP 音訊接收通道；若 Windows 配對失敗或無法列出來源，程式不能用軟體強制支援。
通道已開啟只代表 Windows 回報成功，不代表已量測到音訊。若沒聲音，請檢查來源播放、預設輸出、音量及靜音。
藍牙會有延遲，不承諾遊戲影音完全同步。
Switch 的藍牙音訊使用 A2DP／SBC，不支援藍牙麥克風；本機無線通訊可能停用藍牙音訊，無線控制器數量也有限制。

遇到問題
- 沒偵測到藍牙：檢查 Windows 藍牙開關、裝置管理員與原廠驅動。
- 沒有來源：先完成系統配對；關閉其他藍牙音訊接收程式，再重新搜尋。
- 連線逾時：從 Switch 選電腦連線，之後停止並重新接收。
- 沒聲音：確認 Windows 預設輸出和音量；暫停使用耳機麥克風，排除其他藍牙通道影響。
- 可從 Switch 移除已儲存的電腦配對，再由兩端重新配對。

隱私
不錄音、不上傳、不儲存裝置名稱或藍牙識別。只接收你手動選擇的單一來源。
Windows 本身的配對與裝置資料仍由作業系統管理。

診斷 Log
按「開啟 Log 資料夾」，把 logs/current.log 傳回即可查問題。
記錄啟動版本、Windows／.NET 版本、藍牙介面是否存在、搜尋數量、接收狀態、連線結果、錯誤類型與 HRESULT，以及 30 秒心跳。
不記錄音訊、裝置名稱、藍牙位址、Windows 使用者名稱或原始例外訊息。
固定只有一個 current.log：同一天重開接續寫入，換日後第一次寫入會覆蓋昨天內容。
程式開著時每 30 秒寫一次心跳，因此閒置跨日最遲約 30 秒更新；關閉時不會額外排程背景工作，下次開啟才換日。
使用執行電腦的本地日期，沒有持續累積的歷史檔。達 2 MiB 也會覆蓋，避免檔案無限增長。
若無法寫入，介面會提示；請解壓到一般可寫入資料夾，勿直接在 ZIP 內或受保護系統資料夾執行。

原始碼與建置
source/BluetoothReceiver.cs 是原生 Windows Forms + Windows AudioPlaybackConnection 實作。
在 Windows 執行 Build.ps1；首次建置會從官方 NuGet 下載固定版本的編譯用 SDK contracts，檢查 SHA-256，不執行套件腳本。
SDK contracts 只在建置使用，不需把 .build 資料夾給使用者。
--self-test 測試 API 與裝置列舉；--smoke-test 測試實際 UI 搜尋；兩者不代表 Switch 實機測試。
--log-test 測試同日接續、跨日覆蓋、並行寫入、容量上限與寫入失敗回報，使用獨立 .log-tests 測試目錄。
switch2 是開發分支名稱，Switch 2 藍牙收音仍需實機與驅動相容性驗證。

參考
Microsoft：https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/enable-remote-audio-playback
Nintendo：https://www.nintendo.com/my/support/switch/accessories/bluetooth_audio.html
