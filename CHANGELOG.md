# Changelog

## Unreleased

Not yet released. Development branch `sviluppo-personalizzato` at `e6cfcf049277a30f93f8842a5f5769f0af01d697`. This work is on FLIP and is distinct from the public 0.5.1 package. The software, repository, CLI, and implemented context menus remain FLIP. FLIX is the planned name of the finished architecture and is not available.

Unreleased is the `sviluppo-personalizzato` line of this fork. Its documented historical base is FLIP 0.5.1. Upstream AURA releases v0.6.0 and v0.6.1 are not releases of this fork and not FLIX. See [Publication status](docs/PUBLICATION-STATUS.md).

- M4 (`8c4b758`): Add a central progress window for conversions started from context menus.
- M5.1 (`b0bb71e`): Add the initial PNG context-menu path.
- M5.2 (`61c2666`): Add context menus for the image formats, with dedicated tests and protections.
- M5.3 (`2a44518`): Add PDF, TXT, and data context menus, with dedicated registration and tests.
- M5.4 (`e6cfcf0`): Add audio and video context menus: 8 audio extensions, 7 video extensions, 15 menus in total, and 86 destination commands. Installation depends on a detected local FFmpeg. Destination entries use premium icons. Registry writes use FlipOwner checks and best-effort backup/rollback. Add the dedicated test suite and conversion matrix.
- Isolated real testing of M5.4 (M5.4.6) in the FlipLab Windows account passed in the recorded perimeter: installation of 15 menus; registration of 86 destination commands; WAV and MP4 menus shown; conversions MP4 → MKV, MP4 → MP3, and WAV → MP3; source integrity; output validation and decode; menu removal; absence of the 15 keys after removal; post-removal visual check. That run did not exercise all 86 conversions by hand.

Still open on this branch, and not introduced here: M5.5 document context menus; M5.6 overall functional testing; M6 legacy removal; M7 Setup Manager; a later global icon revision.

## 0.5.1

- Fix `Install.ps1` when run directly from the extracted ZIP. After installing the .NET 10 Windows Desktop Runtime x64, extract the entire ZIP, double-click `Install-FLIP.cmd`, then start FLIP from the Desktop or Start menu shortcut.
- Detect FFmpeg placed in `%LOCALAPPDATA%\Flip\tools`. Restart FLIP after copying `ffmpeg.exe` to that folder to enable audio and video conversions.
- Default to A4 when converting DOCX to PDF through Pandoc and LibreOffice.
- Explain how to open the wheel in the multiple-conversion window, using the configured shortcut.

## 0.5.0-beta.1

- Add native structured-data conversions for JSON, YAML, XML and CSV (into JSON, YAML, XML, CSV, TXT, HTML, Markdown and PDF) without external dependencies.
- Add audio conversions (MP3, WAV, FLAC, OGG, M4A, AAC) and video conversions/audio extraction (MP4, MKV, WebM, AVI, MOV, plus MP3/WAV audio extraction) powered by local FFmpeg.
- Keep the radial wheel open during active conversions with an animated circular progress arc, radial sweep line, live percentage counter and Escape cancellation.
- Add a GitHub release update check in the tray context menu (`Verifica aggiornamenti su GitHub…`).

## 0.4.0-beta.3

- Add tray Settings with arrow presets and custom single-key capture; default to Down Arrow on new installations.
- Reserve the chosen key during Explorer/Desktop gestures so it does not also reach Explorer; preserve normal keyboard use and pair captured key-up/repeat events.
- Support dragging before the key press and holding the key before starting the drag; cancel stale ordering state and keep the wheel open after key release.
- Keep the original wheel tick and its mute control. Do not change Windows system sounds.
- Create Desktop/Start shortcuts and enable per-user startup by default in the installer, with `-NoShortcuts` and `-NoStartup` opt-outs.
## 0.4.0-beta.2

- Replace Ctrl drag activation with `<>`; migrate old saved choices to `<>`.
- Play the original tick directly from pinned memory; remove the hover debounce and per-sector disk logging.
- Clean LibreOffice read-only lock files only inside validated owned job directories.
- Validate missing CLI option values and stop file lists before following options.
- Add a per-user installer with backup/checksum checks and an explicit-region screen recorder.
- Expand repository audit to historical filenames, new source files and commit metadata; document installation, release gates and reference products.

## 0.4.0-beta.1
- Show every compatible format in a dynamic wheel, including RTF/ODT; add tested FB2/RST document conversions.
- Restore core TXT-to-PDF/images and PDF-to-TXT without Pandoc.
- Slightly more translucent backing, larger outer margin and an original 40 ms tic.
- Harden container/batch bounds, timeout reporting, busy UI and private log rotation.
- Keep isolated LibreOffice profiles short and clean them on exit; add deep-folder conversion coverage and external-tool exit codes after validating LibreOffice 26.8.1.
- Scope development-engine fallback, normalize source paths and remove hidden global shortcuts.
- Prepare clean publication helpers, Italian README and release review; public binary release remains blocked.

## 0.3.0
- Add a tray sound-test command and bounded audio-request diagnostics; retain the existing sound player after intermittent user reports.
- Remove the extra-formats button from the wheel and center the filename/status area.
- Optional startup at Windows sign-in, controllable from the tray menu.
- Desktop and Start-menu shortcut installation/removal commands.
- Quiet background startup without opening a panel or spawning duplicate hosts.
- Original 120 ms wheel sound with mute and persistent preferences.
- Denser charcoal wheel to reduce visual interference from the folder underneath.
- Prevent child-control drag-leave transitions from repeatedly clearing the selected sector.
- Source publication preparation: ignore private files, document dependencies and add build CI.

## 0.2.0
- Real image, PDF and document conversions with collision-safe output beside the source.
- Batch selection and multiple output formats.
- Right/left Ctrl gesture and swapped mouse button support.
- Explanation for incompatible batches.
