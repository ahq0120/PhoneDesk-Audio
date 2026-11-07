Switch 2 服務描述比對 v1.2（唯讀）

目前確定的結果
即時 SDP 多次找到 Switch 2 的 Audio Source 與 A2DP 服務，但 Windows 的 BluetoothSetServiceState 回傳 1060，服務仍未啟用。
這不等於 Switch 2 沒有音訊功能，也不代表能靠補裝某個名稱相同的服務修好。
v1.1 的啟用實驗已停用，不要重複執行。v1.2 不呼叫服務啟用 API。

使用
1. PhoneDesk 停止接收，手機藍牙保持開啟並保留電腦配對。
2. Switch 2 保持開機、靠近電腦，停在「設定 → 藍牙音訊」，保留現有配對。
3. 雙擊 Run-Compare.cmd，等待完成，最多約 45 秒。
4. 傳回 logs/switch2-service-probe/current.log。
不用刪配對、重裝驅動或調整音訊緩衝。
一次只開一個查詢程式。若只想查 Switch，Run-Probe.cmd 仍可使用。

比對方式
目標 0／1 是已記住的 Nintendo Switch；目標 100 是控制手機。
手機控制組必須是單一已配對、Phone 類別且 Windows 快取有啟用 Audio Source 的來源；零個或多個時跳過並記錄原因。
工具對標準 Audio Source、A2DP、AVRCP 服務作遠端查詢，略過快取。
每筆服務描述只保存：記錄 handle 是否存在（數值遮蔽）、服務 UUID、協定數值、版本、標準功能旗標與解析結果。
不保存原始 SDP、名稱、位址、音訊或其他文字欄位。這些查詢會暫時連接已配對裝置的 SDP 服務；不掃描陌生裝置，不改設定，不上傳。

限制
記錄比對可找出資料差異，不代表該差異就是 Windows 拒絕建立接收來源的原因。
parse_ok=False 也可能是 Windows 回傳資料格式不符合單一 SDP 記錄，不能單憑此判斷 Switch 回覆損壞。
目前只通過合成 SDP 的正常／缺欄位／損壞／隱私測試與無硬體流程。真實回覆解析和手機對照仍待目標電腦實測。
log 同日接續、跨日覆蓋，上限 2 MiB。請傳本次執行後的記錄。

研究來源
https://learn.microsoft.com/en-us/windows/win32/bluetooth/bluetooth-and-wsalookupservicebegin-for-service-discovery
https://learn.microsoft.com/en-us/windows/win32/api/bluetoothapis/nf-bluetoothapis-bluetoothsdpenumattributes
https://learn.microsoft.com/en-us/windows/win32/api/bluetoothapis/nf-bluetoothapis-bluetoothsetservicestate
https://github.com/bluez/bluez/issues/2625
BlueZ 是其他人的 Linux 實測，不能直接當成這台 Windows 的故障原因。

原始碼建置：在儲存庫執行 bluetooth/probe/Build.ps1。沒有第三方套件下載。
