# Technical decisions

This file keeps the earlier technical decisions and the approved direction of the final product. Each statement belongs to one of three layers.

1. **Documented historical behavior.** Platform and input, Dynamic visual feedback, Capabilities, Correctness and boundaries, and Build/publication record decisions of the wheel-based product. Those sections stay as that record.
2. **Legacy components still in the branch.** On `sviluppo-personalizzato` the wheel, the tray, conversion gestures and legacy drag-and-drop, wheel sounds, legacy Windows startup, the resident host, the legacy mutexes, and legacy IPC over named pipes are still in the code. This alignment does not remove them.
3. **Approved definitive architecture.** Definitive product architecture (approved), below, is the binding direction for the final product. The authoritative document is [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md). Phase order and status are in [ROADMAP.md](ROADMAP.md). **FLIX** is the planned name of that product. The full implementation is still open, and FLIX is not a product available or distributed. **FLIP** remains the name of the current software, of the repository, and of the existing commands. This file does not rename the executable, the CLI commands, or registry keys.

## Platform and input
.NET 10/WPF, x64, asInvoker, per-monitor DPI awareness. No Electron, Python runtime, account or cloud service.
A configurable single non-modifier key is handled by a scoped low-level keyboard hook, with Down Arrow as the default. Only the selected key is consumed during eligible Explorer/Desktop gestures. Raw Input handles Escape and mouse tracking; only OLE data validates a file payload. Both input orders are supported: drag first, or hold the key before starting a drag. A key pressed before dragging initially retains its normal action; once the gesture starts, repeats can be consumed while its matching key-up is still passed if the initial down was passed.
Ctrl activation was removed because it changes Explorer's native drag effect to copy. Unsupported saved choices fall back to the default; supported single-key preferences are retained. Compatible batches are required; no silent skipping. No keyboard events are injected and the hook callback performs no file I/O. Reference: [Microsoft low-level keyboard hook contract](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).
Hidden Ctrl+Alt global shortcuts were removed. The tray/picker remains an alternative.

This section is the documented historical behavior. The removal of Ctrl activation and of the hidden Ctrl+Alt shortcuts is already part of that history. The scoped hook, both drag orders, and the tray/picker are still implemented on the branch. They are legacy components. They are scheduled for removal in M6, which has not started. They are not the approved FLIX interface.

## Dynamic visual feedback
Every compatible output gets a rounded sector. A stable preference order is shared by keyboard navigation and visual placement. Geometry covers 1–12 sectors.
The outer disk is wider than the sectors and has alpha 214/255. Sector gradients are mildly translucent; this is not native backdrop blur.
The new original tic is 40 ms PCM, synthesized from a short transient/harmonics, rather than copied from Tangerine. Same-sector pointer motion stays silent; compatible-sector changes request playback.
The extra-formats button remains removed from the wheel.

This section is the documented historical behavior of the wheel, and that wheel is still in the branch, including its geometry and the synthesized tic. The extra-formats button stays removed, as already decided. Wheel drawing, wheel gestures, and wheel sounds are legacy. Their removal is part of M6, which has not started.

## Capabilities
TXT native PDF/images and PDF native TXT do not need Pandoc. Other text/document conversions use Pandoc. Document-to-PDF/images also use LibreOffice.
FB2 and RST were added only with actual conversion/readback tests. Pages and OCR are not advertised.
PDF-to-document is extracted text, and HTML is document conversion, not browser rendering.

The older sentence that grouped video and audio with Pages and OCR as "not advertised" is obsolete. Engine availability, menu implementation, and publication of a release stay separate. Pages and OCR remain unadvertised.

**Historical base FLIP 0.5.1.** FLIP 0.5.1 is the documented historical base of fork [`giovamolinaro/FLIP`](https://github.com/giovamolinaro/FLIP). It is the wheel-based package published upstream as [`v0.5.1`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1) on [`thetinyfoundry/FLIP`](https://github.com/thetinyfoundry/FLIP). It is not the latest upstream public release, FLIP v0.6.1 AURA Edition, and it is not a release of branch `sviluppo-personalizzato`. Its README and changelog describe audio and video conversion through a local FFmpeg already present on the machine (per-user tools folder, WinGet, or PATH). That package does not ship FFmpeg. It is distinct from the context-menu work on this branch. Manual coverage of audio and video on this branch is the M5.4.6 perimeter below. Provenance: [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

**Development branch, consolidated through M5.4.** Branch `sviluppo-personalizzato`, at `e6cfcf0`, implements these context menus:

- M5.1, the PNG menu;
- M5.2, the image menus;
- M5.3, the PDF, TXT, and data menus;
- M5.4, the audio and video menus.

M5.4 depends on FFmpeg. With FFmpeg available, the code plans 15 source extensions and 86 destinations in total: 42 audio and 44 video. The extensions are `.mp3`, `.wav`, `.flac`, `.ogg`, `.m4a`, `.aac`, `.wma`, `.opus`, `.mp4`, `.mkv`, `.webm`, `.avi`, `.mov`, `.wmv`, and `.m4v`. Without FFmpeg the plan is empty and installation is refused before any registry key is written. Pandoc and LibreOffice do not add audio or video entries. These menus are implementation on the development branch. They are not a new public release.

The isolated real run M5.4.6 is PASS inside the perimeter stated in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md), section 2. That run confirmed the 15 menus and the 86 destination commands. The conversions it executed are the ones listed there: MP4 to MKV, MP4 to MP3, and WAV to MP3, with source-integrity checks and output validation. Registry and Explorer checks in that run are separate from those conversions. M5.4.6 does not close M5.6. M5.5 and M5.6 remain open.

## Correctness and boundaries
Copies are saved beside the source using .converted/collision numbering. No overwrite, extension-only renaming or automatic Explorer navigation.
Limits: 128 files, 100 MiB source, 40 MP image, 2 MiB text, 200 PDF pages, 4096 package entries and 256 MiB expanded document containers.
Timeouts are distinct from user cancellation. Canceled/failed jobs clean their staging in normal execution; abrupt process failure can leave private remnants.
LibreOffice gets a dedicated short Windows temporary profile, independent of source-folder depth, with macro security at level 3 and cleanup on exit. A deep-folder regression covers the crash reproduced after upgrading the external engine to 26.8.1. External-tool errors include numeric/hex exit codes.

These bounds are the conversion behavior still implemented on the branch. Protection of the original files remains an approved requirement of the final product, as stated in Definitive product architecture (approved) and in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md).

## Build/publication
Versions are pinned in the lock file. Runtime identifiers keep RID restore consistent; deterministic source paths use a neutral mapping.
The published app uses its own optional tool, while only source-build output can use the development engine fallback.
The installer enables quiet per-user startup by default, without an administrator service/task. `-NoStartup` skips it; the tray toggle can disable it later.
Source exports include only committed reviewed files. Cache, evidence, backups, downloaded tools, runtime binaries and logs are excluded. MIT covers Flip's own assets/code; third-party obligations are separate.

This section is documented publication and installer behavior, and that behavior is still in the branch. Quiet per-user startup and the tray control that can disable it later are legacy. The same is true of the resident host: desktop mutex `Local\Flip.Desktop.v2`, named pipe `Flip.Local.Flip.v2`, menu mutex `Local\Flip.MenuConvert.v1`, and menu pipe `Flip.Local.Flip.v2.menu`. Removal of that infrastructure is M6, which has not started. The approved final product has no automatic Windows startup and no idle resident process. Installation of that product is managed by FLIP Setup Manager in the future M7. M7 has not started, and the Setup Manager is not implemented. This section does not change the approved Setup Manager requirements.

References: [Raw keyboard](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-rawkeyboard), [startup](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys), [SoundPlayer](https://learn.microsoft.com/en-us/dotnet/api/system.media.soundplayer?view=net-10.0), [LibreOffice security](https://www.libreoffice.org/security/).

---

## Definitive product architecture (approved)

**Authoritative document:** [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md)

**Sequencing:** [ROADMAP.md](ROADMAP.md). M6 does not start before M5 closes. M7 does not start before M6 closes. M6 and M7 have not started. On the development branch, M5.1 through M5.4 are implemented. M5.5 and M5.6 are still open. The M5.4.6 PASS does not close M5.6.

The sections above are the historical record. Where the code still contains the components they describe, those components are legacy and still present. This section, together with docs/FINAL-ARCHITECTURE.md, is the binding direction for the final product. FLIX is that planned product. It is not finished, available, or distributed. The current branch realizes the part already classified as implemented in section 2 of that document, and it still contains the legacy components in section 3.

Approved experience of the final product:

- Explorer context menus are the interface. Primary entry **Converti con Flip**, submenu of **actually available** output formats, premium icons, unchanged **M4** central conversion window. Conversion runs on request, then the M4 progress window. The approved premium-icon requirement stands. The global graphic revision of the icons remains a later dedicated phase. No milestone number is assigned to it in this file, in ROADMAP.md, or in docs/FINAL-ARCHITECTURE.md.
- **Original files** remain protected. Per-user **HKCU** installation. **Windows 10 and 11**.
- **FLIP Setup Manager** is the approved way to manage installation, in the future M7. It is not implemented. Menu `[1]`–`[6]`, `[0]`, with dynamic `[3]` for enable/disable context menu (or install prompt when not installed), refreshed in-session without restarting PowerShell. Those requirements are unchanged.
- **Uninstall** removes FLIP-owned shell entries, installed binaries/icons, and non-shared install state; does not blindly wipe `%LOCALAPPDATA%\Flip`; separate confirmation for logs/preferences; never deletes user conversions or shared external engines.
- **Update/rollback:** backup before update, preserve menu active/inactive state, restore FLIP registry keys, optional prior version restore, foreign-key collision checks, no deletion of personal files, no false atomicity claims.
- **Single PowerShell entry** to the Setup Manager; HTTPS download, Authenticode/trust and integrity checks, PS 5.1/7 compatibility; avoid `Invoke-Expression` on downloaded content—final bootstrap in **M7**. See [FLIP Setup Manager](docs/FINAL-ARCHITECTURE.md#flip-setup-manager) and [Bootstrap e distribuzione](docs/FINAL-ARCHITECTURE.md#bootstrap-e-distribuzione).
- **Pandoc, LibreOffice, FFmpeg** remain optional; native conversions must work without them; the menu shows only available targets. The audio and video menus already on this branch are the additional category that depends on FFmpeg. They do not make FFmpeg mandatory for native conversions, and they do not put FFmpeg in the public package.

Approved exclusions apply to the **final product**. The matching components are still on this branch and are destined for removal in M6: selection wheel, wheel sound, conversion gestures, legacy drag-and-drop, permanent tray, Windows startup registration, and an idle resident FLIP process.

**M6 — legacy removal.** M6 has not started. Its approved purpose is a non-resident process bound to the conversion: start when the user requests a conversion from the context menu, run the current M4 flow, and exit when that window finishes. The phase includes removal of:

- the wheel;
- the tray and its associated interface;
- conversion gestures and legacy drag-and-drop;
- wheel sounds;
- legacy automatic startup;
- the resident host that this process does not need;
- the legacy mutexes still in the branch (`Local\Flip.Desktop.v2` and `Local\Flip.MenuConvert.v1`);
- legacy IPC and the legacy named pipes still in the branch (`Flip.Local.Flip.v2` and `Flip.Local.Flip.v2.menu`).

Concurrency among simultaneous conversion requests remains a problem to solve in M6. No new protocol and no substitute for the legacy mutex are defined here. Current behavior stays as implemented. Dead-code removal and the full M6 test pass belong to that phase.

**M7** delivers the installable package and FLIP Setup Manager, after M6 closes. M7 has not started.
