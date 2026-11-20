# Technical decisions
## Platform and input
.NET 10/WPF, x64, asInvoker, per-monitor DPI awareness. No Electron, Python runtime, account or cloud service.
A configurable single non-modifier key is handled by a scoped low-level keyboard hook, with Down Arrow as the default. Only the selected key is consumed during eligible Explorer/Desktop gestures. Raw Input handles Escape and mouse tracking; only OLE data validates a file payload. Both input orders are supported: drag first, or hold the key before starting a drag. A key pressed before dragging initially retains its normal action; once the gesture starts, repeats can be consumed while its matching key-up is still passed if the initial down was passed.
Ctrl activation was removed because it changes Explorer's native drag effect to copy. Unsupported saved choices fall back to the default; supported single-key preferences are retained. Compatible batches are required; no silent skipping. No keyboard events are injected and the hook callback performs no file I/O. Reference: [Microsoft low-level keyboard hook contract](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).
Hidden Ctrl+Alt global shortcuts were removed. The tray/picker remains an alternative.

## Dynamic visual feedback
Every compatible output gets a rounded sector. A stable preference order is shared by keyboard navigation and visual placement. Geometry covers 1–12 sectors.
The outer disk is wider than the sectors and has alpha 214/255. Sector gradients are mildly translucent; this is not native backdrop blur.
The new original tic is 40 ms PCM, synthesized from a short transient/harmonics, rather than copied from Tangerine. Same-sector pointer motion stays silent; compatible-sector changes request playback.
The extra-formats button remains removed from the wheel.

## Capabilities
TXT native PDF/images and PDF native TXT do not need Pandoc. Other text/document conversions use Pandoc. Document-to-PDF/images also use LibreOffice.
FB2 and RST were added only with actual conversion/readback tests. Pages, video, audio conversion and OCR are not advertised.
PDF-to-document is extracted text, and HTML is document conversion, not browser rendering.

## Correctness and boundaries
Copies are saved beside the source using .converted/collision numbering. No overwrite, extension-only renaming or automatic Explorer navigation.
Limits: 128 files, 100 MiB source, 40 MP image, 2 MiB text, 200 PDF pages, 4096 package entries and 256 MiB expanded document containers.
Timeouts are distinct from user cancellation. Canceled/failed jobs clean their staging in normal execution; abrupt process failure can leave private remnants.
LibreOffice gets a dedicated short Windows temporary profile, independent of source-folder depth, with macro security at level 3 and cleanup on exit. A deep-folder regression covers the crash reproduced after upgrading the external engine to 26.8.1. External-tool errors include numeric/hex exit codes.

## Build/publication
Versions are pinned in the lock file. Runtime identifiers keep RID restore consistent; deterministic source paths use a neutral mapping.
The published app uses its own optional tool, while only source-build output can use the development engine fallback.
The installer enables quiet per-user startup by default, without an administrator service/task. `-NoStartup` skips it; the tray toggle can disable it later.
Source exports include only committed reviewed files. Cache, evidence, backups, downloaded tools, runtime binaries and logs are excluded. MIT covers Flip's own assets/code; third-party obligations are separate.

References: [Raw keyboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawkeyboard), [startup](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys), [SoundPlayer](https://learn.microsoft.com/en-us/dotnet/api/system.media.soundplayer?view=net-10.0), [LibreOffice security](https://www.libreoffice.org/security/).

---

## Definitive product architecture (approved)

**Authoritative document:** [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md)

The sections above (platform/input, dynamic visual feedback, build/publication references to tray/startup/hooks/wheel audio) describe **historical and in-tree behavior** retained for context and migration. For the **final shipped product**, the following decisions are binding:

- Context-menu-only delivery: primary entry **Converti con Flip**, submenu of **actually available** output formats, premium icons, unchanged **M4** central conversion window.
- **No** selection wheel, wheel sound, conversion gestures, wheel drag-and-drop, permanent tray, Windows startup registration, or idle resident FLIP process.
- **Ephemeral process:** start on conversion request; exit after M4 completes; concurrent requests handled per M6.
- **Original files** remain protected; per-user **HKCU** installation; **Windows 10 and 11**.
- **FLIP Setup Manager** menu `[1]`–`[6]`, `[0]`, with dynamic `[3]` for enable/disable context menu (or install prompt when not installed), refreshed in-session without restarting PowerShell.
- **Uninstall** removes FLIP-owned shell entries, installed binaries/icons, and non-shared install state; does not blindly wipe `%LOCALAPPDATA%\Flip`; separate confirmation for logs/preferences; never deletes user conversions or shared external engines.
- **Update/rollback:** backup before update, preserve menu active/inactive state, restore FLIP registry keys, optional prior version restore, foreign-key collision checks, no deletion of personal files, no false atomicity claims.
- **Single PowerShell entry** to the Setup Manager; HTTPS download, Authenticode/trust and integrity checks, PS 5.1/7 compatibility; avoid `Invoke-Expression` on downloaded content—final bootstrap in **M7**.
- **Pandoc, LibreOffice, FFmpeg** remain optional; native conversions must work without them; menu shows only available targets.

Implementation sequencing: [ROADMAP.md](ROADMAP.md) (M5.1–M7). M6 removes wheel/tray/startup/dead code; M7 delivers installable package and Setup Manager.
