# 獨立 A2DP 接收路線：離線可行性驗證

這不是可交給朋友直接收音的版本。沒有修改 USB 驅動、啟用介面、連接實體裝置或播放聲音。

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

此虛擬測試沒有驗證真實配對／認證、加密、音訊解碼、播放、延遲、斷線恢復或 Switch 2 相容性。虛擬 controller 的 Secure Connections Host Support command 目前會印出不支援警告；測試仍完成，但不能把這視為認證成功。

## 實機前置條件

Windows 的 Bumble USB 路線需以 WinUSB 控制一支獨立 USB 藍牙介面，不能與 Windows 原生藍牙堆疊同時共用同一介面。使用者目前的 USB 藍牙棒仍要連其他裝置，因此本研究沒有對它套用 WinUSB。

既有報告的 Realtek USB 硬體 ID 為 0BDA:A760。核對當時的上游 rtk.py，沒有找到這個 VID/PID 的明確表列；這不等於絕對不支援，但在確認控制器版本及韌體需求以前，不應只因為名稱含 Realtek 就更換驅動或載入猜測的韌體。

需先確認另一個可專用、支援 BR/EDR 且可由 Bumble 控制的 USB 介面，或先驗證既有 Intel 介面能承接其他裝置。現有報告 Intel 問題碼 22 只表示停用，不能視為已驗證可用，也不會自動啟用或搬移使用者裝置。

## 後續實作範圍

1. 專用硬體的控制器、韌體及復原方式確認。
2. 入站配對／認證、有限連線數、妥善儲存配對金鑰。
3. 僅提供 sink／SBC 的入站 AVDTP、狀態與拒絕碼紀錄。
4. 接收 SBC 後解碼成 PCM，經 Windows 共享音訊輸出混合播放。
5. 有界緩衝、斷線清理、日誌輪替；正式日誌不寫金鑰、音訊或位址。
6. Switch 2 實體驗證，包含成功連線、音訊播放、重連與長時間穩定性。

這些尚未完成，離線測試通過不能當成產品支援宣告。

## 來源

- [Bumble Windows：WinUSB 與專用 USB 控制](https://google.github.io/bumble/platforms/windows.html)
- [Bumble Realtek：支援及韌體需求依控制器而定](https://google.github.io/bumble/drivers/realtek.html)
- [Google A2DP sink 範例](https://github.com/google/bumble/blob/main/examples/run_a2dp_sink.py)
- [Microsoft AudioPlaybackConnection 的啟用與裝置條件](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/enable-remote-audio-playback)
