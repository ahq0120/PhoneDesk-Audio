# Third-party components

The repository contains the PhoneDesk Windows Forms shell and a small process argument adapter. Third-party executables are downloaded at build time and are not committed.

- **iMirror v0.1.0-preview.1**: https://github.com/phh235/iphone-mirror-windows/tree/87aa5cb2175cb2637cc0dbe42a8d35f72266d542 ; upstream GPL-3.0 and accompanying notices. Its `iMirror.exe` is kept unchanged.
- **UxPlay**: https://github.com/FDH2/UxPlay ; GPL-3.0-or-later, with individual source notices retained. The receiver is rebuilt from the pinned iMirror source commit with `native/phonedesk-stable.patch`, and installed under the historical filename `UxPlay-original.exe`; it is no longer an unmodified upstream binary. Changes include LAN discovery selection, bounded audio queues, pipeline flush, frame validation/duration and live manual synchronization. `research/diagnostics.patch` is optional and not enabled by the default build. The argument adapter named `UxPlay.exe` enables shared WASAPI output when iMirror asks for `-as 0`. `-vs 0` suppresses video display; a mirroring connection still transmits video data. The build includes corresponding patched UxPlay source under `licenses/native/`.
- **GStreamer 1.28.7 / gst-plugins-bad and gst-plugins-good**: WASAPI and level elements, LGPL with component-specific notices; binary packages from the official MSYS2 UCRT64 repository. The upstream license directories are preserved in the build output.

Source archives for the added plugins:

- https://gstreamer.freedesktop.org/src/gst-plugins-bad/gst-plugins-bad-1.28.7.tar.xz
- https://gstreamer.freedesktop.org/src/gst-plugins-good/gst-plugins-good-1.28.7.tar.xz

The original iMirror release contains additional source references and notices. Preserve them when redistributing a build, and meet the applicable source-distribution obligations. A matching checksum confirms artifact identity; it is not a security audit. No third-party component is relicensed by this repository.
