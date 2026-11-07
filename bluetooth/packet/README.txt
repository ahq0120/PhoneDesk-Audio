Switch 2 HCI／L2CAP 封包追蹤 v1.1

1. 完整解壓縮，保留 PhoneDesk Bluetooth v0.4 開啟。
2. 停止手機接收，斷開不相關手機／耳機音訊；不要在追蹤期間輸入密碼或播放私人內容。
3. 右鍵 Run-Packets.cmd → 以系統管理員身分執行。
4. 等到 PACKET TRACE STARTED，再從 Switch 2 的「藍牙音訊」嘗試連線電腦。
5. 失敗可在這 60 秒內再試一次。保持視窗，直到 Completed。
6. 私下傳回 reports 裡最新的 ZIP，並提供 Switch 2 的錯誤文字。

不需要安裝 Wireshark／BTP；腳本只使用 Windows 內建 logman。
這次是 BTHPORT 的 HCI RAW（0x8000000000000000）、HCI（0x4000000000000000）與 L2CAP（0x2000000000000000），總遮罩 0xe000000000000000。
啟用 HCI RAW 事件來源以取得實際封包；不啟用額外的 Full Packet Logging 或 SSP debug 設定；不更改驅動、刪配對或上傳。
使用獨立 PhoneDeskSwitch2Packets 收集器；32 MB 循環上限，60 秒自動停止，正常結束會清除自己的收集器。
意外關閉視窗時，仍有排程停止；可右鍵 Stop-Packets.cmd → 以系統管理員執行，清除本工具留下的收集器。
原始 ETL 可能包含裝置位址、名稱、通訊內容與私人資料，沒有匿名化，請勿公開上傳 GitHub。
預設的封包遮蔽／截斷可能使部分內容缺失；必須實際分析，不能保證每次都有可用拒絕碼。
本機驗證為腳本、原生指令回傳與模擬生命週期；沒有管理員權限／實體 Switch 2，尚未完成真實 HCI 擷取測試。

分析端：用 Microsoft BTP 裡的 BTETLParse.exe -pcapng packets.pcapng input.etl 轉換，再以 Wireshark 分析。
不使用僅適用網路封包來源的 etl2pcapng／pktmon 轉檔指令，也不把一般 WPP 符號解碼當成協定解碼。
來源：
https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/testing-btp-tools-btetlparse
https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/testing-btp-tools-btvs
https://github.com/microsoft/busiotools/blob/master/bluetooth/tracing/BluetoothStack.wprp

1.1 修正：1.0 排除了事件 402 所屬的 HCIRAW 來源，因此沒有取得實際封包。HCIRAW 來源與 BTVS 額外的 Full Packet Logging 開關不同。不要重跑 1.0。
