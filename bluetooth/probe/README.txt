Switch 2 即時音訊服務查詢 v1.0

目的
先前接收器只確認 Windows 快取中沒有 Switch 2 已啟用的服務；不能據此判定主機實際不支援音訊。
此工具使用 Windows Sockets Bluetooth SDP 遠端服務查詢（LUP_FLUSHCACHE），對已記住、名稱符合 Nintendo Switch 的主機查詢 Audio Source、A2DP、兩個 AVRCP 標準服務 UUID。
不修改配對、驅動、登錄或音量；不錄音、不上傳。查詢會嘗試建立暫時的藍牙服務探索連線，因此不等同離線讀取。

操作
1. 完整解壓縮。
2. PhoneDesk 停止接收手機，避免混淆測試。
3. Switch 2 保持開機、靠近電腦，停在「設定 → 藍牙音訊」畫面，保留現有電腦配對。
4. 雙擊 Run-Probe.cmd，等待完成，最長約 45 秒。
5. 傳回 logs/switch2-service-probe/current.log。
若沒查到，可讓 Switch 2 停在「配對裝置」搜尋畫面再跑一次；不需要先刪除配對。

記錄
只保存匿名目標編號、標準服務 UUID、回應數、錯誤碼與耗時，不保存裝置名稱、藍牙位址、原始 SDP 資料或音訊。
這是獨立 log 目錄，不覆蓋 PhoneDesk 接收器的記錄。同日接續、跨日覆蓋，沿用 2 MiB 上限。
沒有找到記住的主機、逾時、查詢失敗，都會顯示並留下記錄。

界線
此版本查的是服務探索，不是假裝已修好音訊。查到服務也不代表後續 AVDTP 音訊協商一定成功；查不到也可能是主機狀態、介面卡／Windows 查詢或連線問題。
本機完成 x64 ABI 版型與編譯測試、無主機時的流程測試；開發電腦沒有藍牙硬體，尚未完成遠端 Switch 2 實測。
不要用本工具結果直接判定需購買硬體或重裝系統。

建置
在原始碼儲存庫執行 bluetooth/probe/Build.ps1。使用 Windows .NET Framework 編譯器，不下載套件。

研究來源
Microsoft 即時 SDP 查詢：https://learn.microsoft.com/en-us/windows/win32/bluetooth/bluetooth-and-wsalookupservicebegin-for-service-discovery
Microsoft WSAQUERYSET：https://learn.microsoft.com/en-us/windows/win32/bluetooth/bluetooth-and-wsaqueryset-for-service-inquiry
AudioPlaybackConnection 只接受可串流裝置：https://learn.microsoft.com/en-us/uwp/api/windows.media.audio.audioplaybackconnection.trycreatefromid
BlueZ Switch 2 實測問題：https://github.com/bluez/bluez/issues/2625
BlueZ issue 是他人的 Linux 環境結果，不是本機 Windows 故障原因的證明。
