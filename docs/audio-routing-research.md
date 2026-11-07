# 音訊路徑實測摘要

2026-11-07，單一手機／電腦／妮姬片段的探索性測試；未以麥克風或高速攝影量測絕對延遲。

| 路徑／實驗 | 觀察 |
|---|---|
| AirPlay Audio／ALAC，操作→動畫→操作 | 時序相似，audioMode 無新切換；快速播放時動畫聲音早到 |
| 延遲回報改為整數 0 或 250ms | 設定確實生效，使用者聽感未改善 |
| AirPlay Mirror／AAC-ELD，電腦不顯示畫面 | 動畫同步、操作反應與原本差不多；曾短暫斷音，後續約 30 秒播放穩定 |

鏡像串流確認 codec=8、screen=1；協定同步等待約 169ms，先前 ALAC 約 1749ms。這些不是耳機端延遲的實測值。

一次 renderer 輸入空窗約 4.18 秒後出現資料補回；可能涉及內容停頓、手機、網路或接收器排程。使用者回報該次要求的播放觀察沒有可聞斷音，不能將空窗直接當成根因；queue underrun 計數也不等於可聞中斷次數。

目前建議鏡像音訊，不啟用猜測式自動切換，仍需更多片段、長時間與其他裝置驗證。

## 診斷版本

正式建置後，可執行 `scripts/Build-Native.ps1 -Research -SdkRoot <相同SDK>`，在正式 native patch 後追加診斷 patch。須重新產生 helper 雜湊 manifest，再以 `research/PhoneDeskResearch.cs` 代替正式介面編譯成獨立應用。使用獨立資料夾，不要覆蓋使用中的正式版本。

診斷版使用 PhoneDesk Research 名稱、不同 MAC／連接埠與互斥鎖。A 是原始回報，B/C/D 是整數 0/80/250ms；改選後須重啟接收器並重新連線。C 只有本機封包驗證，未做手機實測。

只記錄白名單模式、相對時序與數量，不錄音或保存畫面。記錄位於應用程式旁 `logs/audio-health.log`，寫入失敗會顯示。不要公開個人診斷記錄。本儲存庫只包含人工整理、匿名的結論。

## 其他方向

- Windows `AudioPlaybackConnection` 支援 Bluetooth A2DP Sink，但本機未偵測到藍牙硬體，未實測；版本號不保證低延遲。
- USB-C DAC → Line-in／硬體混音可避開 AirPlay，仍須考量介面與監聽延遲。
- ReplayKit 自製串流需要手機 App 與廣播授權，不能保證等同手機喇叭最終輸出。
- 完整 AirPlay 2 buffered 尚未實作，不能承諾改善遊戲延遲。

來源：[UxPlay](https://github.com/FDH2/UxPlay)、[Microsoft A2DP Sink](https://learn.microsoft.com/en-us/windows/apps/develop/media-playback/enable-remote-audio-playback)、[Apple ReplayKit](https://developer.apple.com/documentation/replaykit)、[Apple AirPlay buffering](https://developer.apple.com/videos/play/wwdc2023/10238/)。
