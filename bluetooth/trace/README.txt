Switch 2 服務／A2DP ETW 追蹤 v1.0

1. 完整解壓縮。保留 PhoneDesk Bluetooth v0.4，停止手機音訊接收。
2. 右鍵 Run-Trace.cmd → 以系統管理員身分執行。
3. 等到綠字 TRACE STARTED，立即在 Switch 2 嘗試連線電腦；失敗可再試一次。
4. 保留視窗，60 秒後自動停止，最後顯示 Completed 與 ZIP 路徑。
5. 私下傳回 reports 裡的 ZIP，以及 Switch 2 的錯誤文字／截圖。

Windows ETW 需要系統管理員權限；不是要求關閉防毒或更改配對。
不安裝軟體、不下載元件、不上傳、不改永久事件通道、不中止其他人的 WPR／ETW。
只建立 PhoneDeskSwitch2Trace，32 MB 循環上限與 60 秒排程停止；一般結束會刪除此收集器。
若視窗被強制關閉，收集器仍有到時停止設定；可右鍵 Stop-Trace.cmd → 以系統管理員身分執行，清除此工具自己的收集器。
追蹤來源取自 Microsoft 的 BluetoothStack.wprp，僅選服務、列舉與 A2DP；沒有 HCI RAW／完整封包來源，不開 SSP debug 或金鑰記錄。
ETL 不是匿名資料，可能含裝置名稱、藍牙位址與私人中繼資料。不要公開上傳到 GitHub。
WPP 記錄可能需要相符符號／TMF 解碼，無法保證這次一定能定位問題。
本機沒有管理員權限及 Switch 2，僅能驗證腳本、來源與命令流程，尚未完成實際 ETW 收音連線驗證。

來源：https://github.com/microsoft/busiotools/blob/master/bluetooth/tracing/BluetoothStack.wprp
logman：https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/logman-create-trace
