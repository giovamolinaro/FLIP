# FLIP for Windows

Convert a file without leaving Explorer. Drag it, press **Down Arrow**, and drop it on a format in the wheel. FLIP saves a new copy beside the original.

**FLIP** is the name of this project, this repository, and the published software. **FLIX** is the planned name of the finished architecture. FLIX is not a product you can download today.

These four lines are distinct. Publication details: [Publication status](docs/PUBLICATION-STATUS.md).

| State | What it is |
| --- | --- |
| **Latest upstream release** | [FLIP v0.6.1 AURA Edition](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.6.1) on [`thetinyfoundry/FLIP`](https://github.com/thetinyfoundry/FLIP). Latest verified upstream public release. Not a release of this fork, not this development branch, and not FLIX. |
| **Historical base** | [FLIP 0.5.1](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1), the documented package of this lineage. It uses the conversion wheel, the tray icon, and the interactions below. |
| **Development branch** | `sviluppo-personalizzato` at `e6cfcf049277a30f93f8842a5f5769f0af01d697`. Milestones M4–M5.4 are implemented. That work is **not** released. |
| **Planned architecture** | FLIX: select a file → **Converti con Flip** → choose a format → convert → progress window → result. Full implementation is still open. |

Context menus from milestones M5.2–M5.4 are not part of the historical 0.5.1 package.

![FLIP conversion wheel](docs/assets/wheel.png)

[Download the demo (MP4)](docs/assets/flip-demo.mp4) · [Italiano](README.it.md) · [Installation guide](docs/WINDOWS-INSTALL.md) · [Changelog](CHANGELOG.md) · [Publication status](docs/PUBLICATION-STATUS.md)

---

## Historical base — FLIP 0.5.1

The documented historical base of this lineage is **FLIP 0.5.1**. It is not the latest upstream public release. The instructions in this section describe that package. They do not describe later upstream AURA releases, and they do not describe the context-menu work on the development branch.

### Fixes in 0.5.1

- Fixed direct PowerShell installation from the extracted ZIP. Double-click `Install-FLIP.cmd` for the standard setup.
- Fixed FFmpeg detection in the documented per-user tools folder, so conversions such as MP4 to MP3 are available when FFmpeg is installed there.
- DOCX to PDF now defaults to A4.
- The multiple-conversion window explains how to open the wheel using the configured shortcut.

### What's new in v0.5.0-beta.1

- **Audio & Video conversion**: convert between video formats (MP4, MKV, WebM, AVI, MOV) and audio formats (MP3, WAV, FLAC, OGG, M4A, AAC), or extract audio tracks from video files on the fly with local FFmpeg.
- **Native structured-data conversions**: bidirectionally convert JSON, YAML, XML, and CSV, or export them to PDF, HTML, Markdown, and TXT with zero external dependencies.
- **Animated radial progress arc**: the wheel stays open during long conversions and displays an animated sweep line, a smooth circular progress arc, and a real-time percentage counter with a green completion checkmark.
- **In-app GitHub update checker**: right-click the tray icon and select *"Verifica aggiornamenti su GitHub…"* to check for new releases.
- **Reliable audio feedback**: restored tactile tick using modern WASAPI/Media Foundation audio playback.

### Download and install

**[Download FLIP 0.5.1 for Windows x64](https://github.com/thetinyfoundry/FLIP/releases/download/v0.5.1/Flip-0.5.1-win-x64.zip)** · [Release notes and checksum](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1)

1. Open the [v0.5.1 release page](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1), download the `Flip-0.5.1-win-x64.zip` archive and extract it.
2. Install the [.NET 10 Windows Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0) if not already installed.
3. Exit FLIP from its tray menu if it is running, then double-click `Install-FLIP.cmd` in the extracted folder. Open FLIP from the Desktop or Start menu shortcut. You do not need the SDK or a compiler to run the packaged app.

The 0.5.1 ZIP is a Windows x64 package. ARM64 and 32-bit packages are not provided. The app is framework-dependent: it needs the **.NET 10 Windows Desktop Runtime x64**, not only the base .NET runtime. See [docs/WINDOWS-INSTALL.md](docs/WINDOWS-INSTALL.md) and [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

Later upstream public releases, including [v0.6.1 AURA Edition](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.6.1), appear on the original repository [Releases](https://github.com/thetinyfoundry/FLIP/releases) page. They are not packages of this fork and are not the 0.5.1 historical base described here.

### Optional engines (0.5.1)

FFmpeg, Pandoc, and LibreOffice are **external** tools. They are **not** included in the 0.5.1 ZIP.

- **Audio & Video**: Place `ffmpeg.exe` in `%LOCALAPPDATA%\Flip\tools` and restart FLIP. FLIP also detects FFmpeg installed through WinGet or available on PATH.
- **Documents (Pandoc & LibreOffice)**: close FLIP and run `Install-DocumentEngine.ps1` from the package to download official Pandoc. LibreOffice provides document rendering.

Image conversion and structured-data conversion run without those engines.

### Build from source

Install the [.NET 10 SDK for Windows x64](https://dotnet.microsoft.com/download/dotnet/10.0), clone or download this repository, and open PowerShell in the project folder:

```powershell
.\scripts\Build.ps1 -DocumentEngine
.\scripts\Install.ps1
```

The installer copies FLIP to `%LOCALAPPDATA%\Programs\Flip`, creates Desktop and Start Menu shortcuts, and enables startup when you sign in to Windows. Use `-NoShortcuts` or `-NoStartup` to opt out.

These commands build the tree you have checked out. The documented binary of this historical base is the 0.5.1 ZIP above. Building the current development branch does **not** produce a new public release.

FLIP stays in the notification area near the clock. Right-click its icon to open Settings, choose a shortcut, change the language, test the wheel sound or check for GitHub updates.

### Use the wheel

1. Select one or more files in Explorer or on the Desktop.
2. Start dragging and press **Down Arrow** (or your custom shortcut). Release the key when the wheel appears.
3. Hover over an available output format and drop.
4. During conversion, watch the circular progress arc and percentage indicator. Press **Escape** at any time to cancel.

FLIP reserves the selected key during Explorer/Desktop mouse gestures. Outside that context it passes keyboard input through.

Output names include `.converted` and a number when needed. FLIP **never** overwrites the original or an existing output.

### Supported conversions (0.5.1 wheel)

These formats are those of the historical 0.5.1 wheel. They are not a list of Explorer context menus.

| Category | Input Formats | Available Outputs | Engine |
| --- | --- | --- | --- |
| **Images** | PNG, JPG/JPEG, WebP, BMP, TIFF, GIF | PNG, JPG, WebP, PDF, BMP, TIFF, GIF (excluding source) | Native SkiaSharp |
| **Structured Data** | JSON, YAML/YML, XML, CSV | JSON, YAML, XML, CSV, TXT, HTML, Markdown, PDF | Built-in |
| **Audio** | MP3, WAV, FLAC, OGG, M4A, AAC, WMA, OPUS | WAV, MP3, FLAC, OGG, M4A, AAC | Local FFmpeg |
| **Video** | MP4, MKV, WebM, AVI, MOV, WMV, M4V | MP4, MKV, WebM, AVI, MOV, plus MP3 and WAV audio extraction | Local FFmpeg |
| **Plain Text** | TXT | PDF, PNG, JPG, WebP (core); DOCX, EPUB, Markdown, HTML, ODT, RTF, FB2, RST (Pandoc) | Core / Pandoc |
| **Documents** | Markdown, HTML, DOCX, EPUB, ODT, RTF, FB2, RST | Other document formats with Pandoc; rendering to PDF/images requires LibreOffice | Pandoc / LibreOffice |
| **PDF** | PDF | PNG/JPG/WebP per page and extracted TXT; other document formats with Pandoc | Native / Pandoc |

### Notes and limits (0.5.1)

- Conversions run locally. There is no account and no cloud conversion service.
- Audio and video on the wheel need a local FFmpeg already present on the machine. The package does not ship FFmpeg.
- Advanced document conversions need Pandoc; rendering non-TXT documents to PDF or images needs LibreOffice. FLIP does not install LibreOffice automatically.
- Broader native drag-and-drop, listening, mixed-DPI, and clean-machine coverage remain testing limits of the 0.5.1 package. Drag integration with other file managers is not claimed.
- Treat untrusted documents with care. Process limits are not a complete security sandbox. See [SECURITY.md](SECURITY.md).

---

## Development branch

This section describes branch **`sviluppo-personalizzato`** at commit `e6cfcf049277a30f93f8842a5f5769f0af01d697`. It is in-progress work. It is **not** a new public release, and it is **not** the 0.5.1 ZIP.

On this branch, Explorer context menus are implemented through milestone **M5.4**. The historical 0.5.1 package does not include those menus.

| Milestone | What is in the branch |
| --- | --- |
| **M4** | Central progress window, implemented. |
| **M5.1** | PNG context-menu path, implemented. |
| **M5.2** | Image context menus, implemented. |
| **M5.3** | PDF, TXT, and data context menus, implemented. |
| **M5.4** | Audio and video context menus, implemented. |

### Audio and video menus (M5.4)

With FFmpeg detected, the branch plans **15** source extensions and **86** destination commands. Installation is refused before any registry write if FFmpeg is missing. Destination entries use dedicated premium icons.

That is not a claim that every one of the 86 conversions was run by hand.

Isolated real testing **M5.4.6** completed with a **PASS** in the perimeter recorded in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md):

- installation of the 15 menus and observation of the 86 destination commands;
- Explorer checks on WAV and MP4;
- real conversions **MP4 → MKV**, **MP4 → MP3**, and **WAV → MP3**, with source-integrity and output checks;
- removal of the 15 menus, then registry and visual checks.

Automated tests on this branch (engine matrices and in-memory input checks) are separate from those real conversions and from the Explorer/registry checks. M5.4.6 does not close the overall functional test in M5.6.

### What still runs together

The branch does **not** yet match the planned FLIX architecture. Today it still runs, in the same process:

- M5 context menus;
- the M4 progress window;
- the legacy conversion wheel;
- the tray icon;
- a resident host;
- mutexes;
- IPC over legacy named pipes.

After a context-menu conversion the progress window can close, but the process stays resident. Exiting after conversion is future **M6** work. This README does not define a new concurrency design.

Document context menus (**M5.5**) are not implemented yet. Do not treat the current branch as a finished product or as a replacement for 0.5.1.

Implementation detail lives in [ARCHITECTURE.md](ARCHITECTURE.md), not in this README.

---

## Planned architecture

**FLIX** is the planned name of the finished architecture. The software, repository, executable, CLI commands, and registry entries remain **FLIP** until a later phase changes them. This README does not rename anything.

The approved user path is:

1. Select a file.
2. **Converti con Flip**.
3. Choose an output format.
4. Convert.
5. Watch the M4 progress window.
6. Get the result.

That path is the destination. It is not fully implemented. Closing the process when the progress window finishes belongs to **M6**, which has not started.

Approved direction, phase order, and remaining requirements are in:

- [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md) — binding FLIX direction
- [ARCHITECTURE.md](ARCHITECTURE.md) — architecture actually implemented on this branch
- [ROADMAP.md](ROADMAP.md) — milestone sequence
- [TECHNICAL_DECISIONS.md](TECHNICAL_DECISIONS.md) — approved decisions

### Remaining work

Open milestones have **no** assigned public release version.

| Work | Status |
| --- | --- |
| **M5.5** — document context menus | To implement. Distinct from the PDF, TXT, and data menus in M5.3. |
| **M5.6** — overall functional testing | To complete. The M5.4.6 PASS does not close this phase. |
| **M6** — remove legacy components | Not started. Follows the close of M5. Removes the wheel, tray, resident host, mutexes, and legacy IPC, and binds process lifetime to a single conversion. |
| **M7** — FLIP Setup Manager | Not started. Follows the close of M6. |
| Global icon revision | Later dedicated phase. Not numbered. |

The approved sequence does not change: **M6 does not start before M5 closes. M7 does not start before M6 closes.**

---

## Requirements and dependencies

Do not mix these four layers.

**Historical base 0.5.1**

- Windows x64. Windows 11 is the documented acceptance target of that package.
- [.NET 10 Windows Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0) to run the ZIP. The SDK is not required for that package.
- FFmpeg, Pandoc, and LibreOffice are optional external engines. They are not in the ZIP.

**Development-branch code**

- Same Windows x64 and .NET 10 desktop platform (`net10.0-windows`).
- Direct project libraries include PDFtoImage and PdfPig; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
- Native image, text, and structured-data conversions do not require FFmpeg, Pandoc, or LibreOffice.
- Audio/video **context menus** on this branch install and run only when FFmpeg is detected. Pandoc remains optional for extra document targets. LibreOffice remains optional for Office/PDF rendering.

**Optional external tools**

| Tool | Role | Bundled? |
| --- | --- | --- |
| **FFmpeg** | Audio and video conversion (wheel in 0.5.1; context menus on the development branch) | No. FLIP searches for `ffmpeg.exe` on the machine. |
| **Pandoc** | Advanced document conversions | No. Optional; the package script can download the official build. |
| **LibreOffice** | Document rendering to PDF/images | No. Separate user installation. |

This README does not pin engine versions. Notices and licenses for components that *are* part of the project are in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and [LICENSE](LICENSE).

**Planned for FLIX**

- Windows 10 and Windows 11.
- Per-user installation (HKCU).
- Pandoc, LibreOffice, and FFmpeg remain optional. Native conversions must work without them. The menu should show only formats that are actually available.
- Any later distribution of external engines is future Setup Manager work (M7). It is not implemented.

---

## Privacy and local execution

All conversions run **locally** on your machine without third-party cloud services or account requirements. Converted files remain in their original folder.

Runtime diagnostic logs are stored under `%LOCALAPPDATA%\Flip\logs`. Review and redact logs before sharing. Avoid uploading sensitive inputs or unsanitized logs in public issues. See [SECURITY.md](SECURITY.md).

---

## License

The `LICENSE` file in this fork is the [MIT License](LICENSE). That is the text on this repository, including branch `sviluppo-personalizzato`, and at the historical `v0.5.1` tag. Third-party components retain their respective licenses. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). The upstream AURA `LICENSE` from v0.6.0 states GPL-3.0-only; that is not a relicensing of this fork. Details: [Publication status](docs/PUBLICATION-STATUS.md).

[Contributing](CONTRIBUTING.md) · [Changelog](CHANGELOG.md) · [Architecture](ARCHITECTURE.md) · [Security](SECURITY.md)
