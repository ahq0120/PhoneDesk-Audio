# Switch 2 原生藍牙連線調查

更新日期：2026-11-08。此文件區分觀測、平台條件與尚未證實的假設；不是已完成的修復宣告。

## 測試環境與既有結果

- 使用者確認 Switch 2 系統版本為 23.0.1。
- 目標電腦為 Windows build 26200，Realtek Bluetooth 6.0 Adapter 驅動 20.11.3038.3001。另一個 Intel 介面的問題碼為 22（停用）；不能將畫面中的兩個介面名稱當成同時運作衝突的證據。
- 控制手機在 Windows 建立 A2DP SNK 裝置，Service 為 `BthA2dp`、問題碼 0；對應 Microsoft A2DP Sink INF 為 `microsoft_bluetooth_a2dp_snk.inf`。接收 API 可成功 Open，使用者已回報能聽到手機音訊，穩定性問題暫不處理。
- Switch 2 有配對紀錄，但完整報告未出現對應 A2DP 接收裝置。其快取已啟用服務為 0。
- 即時 SDP 多次成功找到 110A（Audio Source）、110D（A2DP）、110C／110E（AVRCP）記錄。曾有單次 10108 與較長等待，不能把這些視為所有查詢均失敗。
- 對 Switch 2 呼叫 BluetoothSetServiceState 啟用 110A，Windows 回傳 1060，後續已啟用服務仍不含 110A。該實驗已停用，不建議繼續重跑。

這些結論不含使用者裝置名稱、藍牙位址或私人記錄；不是原始封包分析。

## 已核對的 Windows 接收條件

在開發機讀取 `AudioPlaybackConnection.GetDeviceSelector()`，回傳條件為：

```text
System.Devices.DevObjectType:=8
AND System.Devices.InterfaceClassGuid:="{6994AD04-93EF-11D0-A3CC-00A0C9223196}"
AND System.DeviceInterface.Bluetooth.ServiceGuid:="{0000110A-0000-1000-8000-00805F9B34FB}"
AND System.Devices.InterfaceEnabled:=System.StructuredQueryType.Boolean#True
```

也就是啟用中的音訊裝置介面，不是一般配對清單。單純將 Switch 2 加入清單、使用配對裝置 ID 或改裝置顯示分類，並不等於能建立有效的 AudioPlaybackConnection。

本機 Microsoft A2DP Sink INF 將遠端 110A 配對到 A2DP Sink profile；朋友的報告亦已存在這個驅動。這支持繼續查 Switch 特定服務解析／PnP 建立過程，而不是假設 Windows 完全未安裝接收能力。

Microsoft 對 BluetoothSetServiceState 的 1060 說明是指定的服務 GUID 不受支援。這是該次啟用要求的結果，不能推論整台電腦永遠不支援 110A，也不能靠建立同名 Windows 服務修復。

## 為何不能直接在現有程式強制改協商

目前使用的 AudioPlaybackConnection 公開介面提供連線啟用／開啟與狀態，沒有用來指定 AVDTP 回應、SBC-only 或本機 A2DP 角色集合的參數。Windows profile 驅動負責這一層。

Microsoft 文件記載的 AF_BTH Winsock 傳輸協定是 RFCOMM，並非可直接用來替換 AVDTP 的使用者模式 L2CAP 音訊連線。因此「自己用 socket 收 SBC」不是現有 WinForms 接收器可以直接補上的小改動。若要取代協定處理，需先證明可行的較底層傳輸／驅動途徑，不能只新增 UI 或 SDP 廣告就宣稱支援。

以上不代表所有 Windows 解法都不可能；它界定了目前 API 的能力與已觀測的斷點。

## 外部實測與版本核對

Nintendo 的 Switch 2 23.0.1 公告修正軟體啟動、暫停及關閉耗時問題，沒有列出本案的 Windows A2DP 相容性修正。不能據此聲稱此版本一定沒有其他改動，也不建議為本案盲目更新或降級。

BlueZ issue #2625 的作者回報 Linux 可透過限制 `a2dp_sink` 與 `sbc` 建立 Switch 2 串流，但仍有音量問題。作者沒有隔離是哪個設定產生效果；AVRCP 缺少 ServiceRecordHandle 的因果關係也未驗證。

這是有用的協商方向，不是可直接套進 Windows 的修復。朋友這台主機尚未取得可比對的 SDP 屬性／AVDTP 追蹤，不能斷言其缺少相同欄位。

## 下一個有效的調查點

1. 比較能被辨識的手機與 Switch 2 的 SDP 屬性結構，特別是服務類別、protocol descriptor、profile version 及記錄 handle 是否存在。
2. 對照 Windows 是否為 Switch 2 建立 110A 的服務子裝置／啟用音訊介面，及對應 PnP／SetupAPI 事件。
3. 若子裝置已建立但無法串流，才進入 AVDTP Discover、能力交換、SetConfiguration／Open／Start 的追蹤。
4. 原始 trace 可能包含位址或配對資料，收集時需採最小範圍；不要啟用含金鑰的 full packet／SSP debug 作為一般測試。

目前不重裝整套藍牙驅動、不強制把 A2DP 驅動綁到一般 Switch 配對父裝置、不改 bitpool／音訊緩衝、不反覆呼叫已失敗的啟用操作。尚未交付可宣稱修好 Switch 2 的版本。

## 來源

- [Nintendo Switch 2 更新說明](https://www.nintendo.com/en-gb/Support/Nintendo-Switch-2/System-Update-Information-for-Nintendo-Switch-2-2843738.html)
- [Microsoft：AudioPlaybackConnection](https://learn.microsoft.com/en-us/uwp/api/windows.media.audio.audioplaybackconnection)
- [Microsoft：TryCreateFromId](https://learn.microsoft.com/en-us/uwp/api/windows.media.audio.audioplaybackconnection.trycreatefromid)
- [Microsoft：BluetoothSetServiceState](https://learn.microsoft.com/en-us/windows/win32/api/bluetoothapis/nf-bluetoothapis-bluetoothsetservicestate)
- [Microsoft：Bluetooth driver stack 的裝置與 profile 建立](https://learn.microsoft.com/en-us/windows-hardware/drivers/bluetooth/using-the-bluetooth-driver-stack)
- [Microsoft：Bluetooth Winsock 支援協定](https://learn.microsoft.com/en-us/windows/win32/bluetooth/bluetooth-and-socket)
- [BlueZ：Switch 2 A2DP sink 實測](https://github.com/bluez/bluez/issues/2625)
