# Third-party components

The repository contains the PhoneDesk Windows Forms shell and a small process argument adapter. Third-party executables are downloaded at build time and are not committed.

- **iMirror v0.1.0-preview.1**: https://github.com/phh235/iphone-mirror-windows/tree/87aa5cb2175cb2637cc0dbe42a8d35f72266d542 ; upstream GPL-3.0 and accompanying notices. Its `iMirror.exe` is kept unchanged.
- **UxPlay**: bundled by the pinned iMirror release; https://github.com/FDH2/UxPlay ; GPL-3.0-or-later. Its binary is preserved as `UxPlay-original.exe`. The PhoneDesk adapter named `UxPlay.exe` changes only the explicit `-as 0` argument to `wasapisink exclusive=false low-latency=true`, then invokes the unchanged original. Pure audio mode invokes the original with `-vs 0`.
- **GStreamer 1.28.7 / gst-plugins-bad and gst-plugins-good**: WASAPI and level elements, LGPL with component-specific notices; binary packages from the official MSYS2 UCRT64 repository. The upstream license directories are preserved in the build output.

Source archives for the added plugins:

- https://gstreamer.freedesktop.org/src/gst-plugins-bad/gst-plugins-bad-1.28.7.tar.xz
- https://gstreamer.freedesktop.org/src/gst-plugins-good/gst-plugins-good-1.28.7.tar.xz

The original iMirror release contains additional source references and notices. Preserve them when redistributing a build, and meet the applicable source-distribution obligations. A matching checksum confirms artifact identity; it is not a security audit. No third-party component is relicensed by this repository.
