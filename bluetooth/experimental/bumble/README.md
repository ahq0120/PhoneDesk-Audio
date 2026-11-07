# 獨立 A2DP 接收路線：離線可行性驗證

這是研究中的接收器核心，不是可交給朋友直接收音的 EXE。沒有修改 USB 驅動、啟用介面或連接實體裝置。

## 已實作的音訊核心

`audio_pipeline.py` 包含單一 AVDTP 來源接收、SBC RTP payload 長度／frame count 檢查、PyAV SBC 解碼、統一為 48 kHz stereo int16 PCM，以及 Windows WASAPI 共享輸出。

輸入佇列上限 6 個封包，PCM 緩衝預設上限 120 ms；滿載時丟棄舊資料，輸出不足補靜音。這些是程式緩衝上限，不是實測端到端延遲保證。停止、斷線、重新開始會清空緩衝；舊解碼工作完成後也不會寫入新連線。

日誌只有事件、數量與時間；固定 `current.log` 同日累加、跨日寫入輪替、約 2 MiB 上限，不記錄音訊、位址或金鑰。寫檔失敗不終止音訊流程，錯誤碼保留於 `last_error`。

目前只接收未分段的 SBC RTP payload；分段或格式錯誤的資料會丟棄並計數。尚無獨立 USB 啟動器、配對金鑰保護、GUI 或 Windows 發行包。

## 音訊驗證

```powershell
python -m pip install -r requirements-test.txt
python -m pytest test_audio_pipeline.py -q
```

2026-11-08 本機 14 項檢查通過：用 FFmpeg SBC encoder 產生合成音訊，經虛擬 AVDTP 傳送並解碼；兩次連線／關閉；佇列過載；丟棄重複／舊封包及停止前仍在解碼的音訊；日誌輪替與寫檔失敗；16／32／44.1／48 kHz mono/stereo 解碼與 resampling。

另在本機以靜音資料驗證 WASAPI 共享輸出開啟、執行、關閉，沒有錄音。未驗證實體 Switch 2 的聲音、延遲或與其他應用程式同時播放的聽感。

## 已完成的虛擬測試

在 Bumble 0.0.236，使用同一個 LocalLink 上的兩個虛擬 BR/EDR controller：

- 來源 SDP 刻意移除屬性 0x0000，保留 Audio Source 記錄。
- 接收端主動註冊 AVDTP listener 與 SBC sink，不先查詢來源 SDP。
- 來源連入，完成 Discover、Configure、Open、Start、Close。
- 整段測試有 15 秒逾時，沒有 USB 或 Windows AudioPlaybackConnection。

```powershell
python -m pip install -r requirements-research.txt
python listener_smoke.py
```

`listener_smoke.py` 只測通道協商；音訊核心由上述額外測試驗證。兩者都沒有驗證真實配對／認證、加密、USB 重連或 Switch 2 相容性。虛擬 controller 的 Secure Connections Host Support command 目前會印出不支援警告；測試仍完成，但不能把這視為認證成功。

## 實機前置條件

Windows 的 Bumble USB 路線需以 WinUSB 控制一支獨立 USB 藍牙介面，不能與 Windows 原生藍牙堆疊同時共用同一介面。使用者目前的 USB 藍牙棒仍要連其他裝置，因此本研究沒有對它套用 WinUSB。

既有報告的 Realtek USB 硬體 ID 為 0BDA:A760。核對當時的上游 rtk.py，沒有找到這個 VID/PID 的明確表列；這不等於絕對不支援，但在確認控制器版本及韌體需求以前，不應只因為名稱含 Realtek 就更換驅動或載入猜測的韌體。

需先確認另一個可專用、支援 BR/EDR 且可由 Bumble 控制的 USB 介面。既有 Intel 原先問題碼 22，使用者嘗試啟用後回報代碼 31，目前不能視為可用。由於使用者沒有有線滑鼠，不進行拔除 Realtek 的隔離測試，也不自動啟用、切換或搬移使用者裝置。

## 後續實作範圍

1. 專用硬體的控制器、韌體及復原方式確認。
2. 入站配對／認證、有限連線數、妥善儲存配對金鑰。
3. 將已驗證的 sink／SBC 核心接到實體 USB 傳輸，補入配對與協定拒絕碼診斷。
4. 真實 SBC 音訊與 Windows 共享輸出的播放、混音測試。
5. 斷線恢復、分段 SBC、GUI 與無需安裝 Python 的發行方式。
6. Switch 2 實體驗證，包含成功連線、音訊播放、重連與長時間穩定性。

上述實機與產品化工作尚未完成，離線測試通過不能當成產品支援宣告。不要要求一般使用者安裝 Python 重跑離線測試來證明實機相容性。

## 來源

- [Bumble Windows：WinUSB 與專用 USB 控制](https://google.github.io/bumble/platforms/windows.html)
- [Bumble Realtek：支援及韌體需求依控制器而定](https://google.github.io/bumble/drivers/realtek.html)
- [Google A2DP sink 範例](https://github.com/google/bumble/blob/main/examples/run_a2dp_sink.py)
- [Microsoft AudioPlaybackConnection 的啟用與裝置條件](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/enable-remote-audio-playback)
