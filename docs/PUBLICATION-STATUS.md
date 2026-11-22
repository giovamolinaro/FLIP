# Publication status — 12 November 2026

This file records four different lines. They must not be read as one:

| Line | Meaning |
| --- | --- |
| **Latest upstream public release** | **FLIP v0.6.1 — AURA Edition** on [`thetinyfoundry/FLIP`](https://github.com/thetinyfoundry/FLIP), published 2026-11-20T14:16:08Z. A public release, not a prerelease. Not a release of this fork, not branch `sviluppo-personalizzato`, and not FLIX. |
| **Historical base of this fork** | **FLIP 0.5.1**, the documented package of this lineage. Local validation record: 12 November 2026. Upstream publication of that release: 2026-11-11T23:45:12Z. |
| **Unreleased development branch** | Branch `sviluppo-personalizzato` at `e6cfcf049277a30f93f8842a5f5769f0af01d697`, through milestone M5.4. Consolidated on the remote development branch. Not a GitHub Release. |
| **Planned FLIX architecture** | **FLIX**, the approved destination. Not implemented in full and not distributed. |

The 12 November 2026 date is the local record of the 0.5.1 package, the calendar day in Italy of the publication at 2026-11-11T23:45:12Z. Later sections describe unpublished development work at a later commit. They do not replace the 0.5.1 package and they do not adopt the upstream AURA releases. A commit or push on the development branch is not a GitHub Release.

## Latest upstream public release

Public GitHub Releases verified on [`thetinyfoundry/FLIP`](https://github.com/thetinyfoundry/FLIP/releases):

- [`v0.5.1`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1), FLIP 0.5.1 for Windows, published 2026-11-11T23:45:12Z.
- [`v0.6.0`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.6.0), FLIP v0.6.0 — AURA Edition, published 2026-11-19T22:44:58Z.
- [`v0.6.1`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.6.1), FLIP v0.6.1 — AURA Edition, published 2026-11-20T14:16:08Z.

v0.6.1 is the latest upstream public release verified in this note. It is not a release of fork [`giovamolinaro/FLIP`](https://github.com/giovamolinaro/FLIP), not a release of `sviluppo-personalizzato`, and not FLIX. AURA functionality does not enter this branch or FLIX automatically. This file does not describe those upstream functions as implemented here.

The fork has no GitHub Releases and no tags. Fork `main` is `8133a4ded576659763fb274cdba06fb423030f83`. Branch `sviluppo-personalizzato` on that fork is `e6cfcf049277a30f93f8842a5f5769f0af01d697`.

The git tag `v0.5.1` points to `610de131ba150b17ab66ab7570ee8021f0bfa199`. The v0.5.1 GitHub Release field `target_commitish` is `8133a4ded576659763fb274cdba06fb423030f83`, the same object as this fork's `main`. Those two commits are not the same object. The GitHub compare API reports no common ancestor between them. This file does not identify which of those commits produced the 0.5.1 binary.

`LICENSE` in this fork is the MIT license text, on `main` and on `sviluppo-personalizzato`. The upstream `LICENSE` file at tags `v0.6.0` and `v0.6.1` states GPL-3.0-only. The `LICENSE` file at tag `v0.5.1` states MIT. This paragraph records those file texts. It does not modify `LICENSE` and it does not declare a FLIX relicensing.

## Historical base of this fork — FLIP 0.5.1

The 0.5.1 package recorded in this section contains the application source, build and installation scripts, assets, MIT license, component notices and documentation.

On the 0.5.1 line, the Windows workflow passes locked restore, source/history pattern checks, compilation and input/policy/audio-logic tests. These automated checks do not replace physical drag/drop, listening or clean-machine testing.

The Windows download is built with `scripts/Package.ps1`. It includes the executable, installer scripts, dependency notices and SHA256 checksums. It requires the .NET 10 Windows Desktop Runtime x64. The package does not include debug symbols, local logs, personal documents or development caches.

Pandoc and FFmpeg are optional engines:
- FFmpeg enables audio and video format conversions and audio track extraction.
- Pandoc enables advanced document conversions.
- LibreOffice is a separate optional installation for Office/PDF rendering.
- The built-in core supports images and structured data (JSON, YAML, XML, CSV) with zero external tools.

The historical base package of this fork is **0.5.1**. That package still uses the conversion wheel and the tray icon. Context menus from milestones M5.2–M5.4 are not part of it. The checks recorded for this package are not results of upstream v0.6.1 AURA Edition.

Documented distribution of this package:

- Tag: [`v0.5.1`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1)
- Asset: [`Flip-0.5.1-win-x64.zip`](https://github.com/thetinyfoundry/FLIP/releases/download/v0.5.1/Flip-0.5.1-win-x64.zip)
- Checksum: published with the release as `Flip-0.5.1-win-x64.zip.sha256`
- Installation: `Install-FLIP.cmd` from the extracted ZIP

The [Windows package v0.5.1](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1) includes the Windows x64 ZIP and its SHA-256 checksum. Local validation recorded on 12 November 2026 (upstream publication 2026-11-11T23:45:12Z) covered the extracted installer, 100 input checks, 117 conversion checks and the wheel shortcut hint in the multiple-conversion window. Those counts belong to the 0.5.1 package. They are not results of upstream v0.6.1 AURA Edition, they are not the later M5.4.5 suite figures, and they do not certify every Windows keyboard, display or audio device.

Earlier tags on the upstream product line include [`v0.4.0-beta.3`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.4.0-beta.3) (recorded 23 October 2026) and [`v0.5.0-beta.1`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.0-beta.1) (recorded 7 November 2026). They are previous publications. They are not the 0.5.1 package of this lineage and they are not the latest upstream release.

Earlier audits such as [PUBLIC_RELEASE_REVIEW.md](../PUBLIC_RELEASE_REVIEW.md) (22 October 2026) recorded beta gates that blocked an executable at that date. Those statements are historical. They do not withdraw the already published 0.5.1 package. Remaining hardware, listening and clean-machine limits of that package stay as recorded above and in [docs/WINDOWS-INSTALL.md](WINDOWS-INSTALL.md).

See [README.md](../README.md) for the 0.5.1 download and wheel instructions. See [CHANGELOG.md](../CHANGELOG.md) for the 0.5.1 notes.

## Development branch — not released

This section describes branch **`sviluppo-personalizzato`** at commit `e6cfcf049277a30f93f8842a5f5769f0af01d697`. It is FLIP development work. It is not a public release, not an installable end-user package, and not an approved product version.

Implemented on this branch:

| Milestone | Status on the branch |
| --- | --- |
| **M4** | Central progress window, implemented. |
| **M5.1** | PNG context menu, implemented. |
| **M5.2** | Image context menus, implemented. |
| **M5.3** | PDF, TXT and data context menus, implemented. |
| **M5.4** | Audio and video context menus, implemented and consolidated on the remote development branch. |

M5.4, as implemented on this branch, comprises:

- 15 source extensions;
- 86 destination commands;
- a local FFmpeg dependency (installation is refused before any registry write if FFmpeg is missing);
- registration protections (`FlipOwner` checks and best-effort backup/rollback);
- isolated real testing **M5.4.6 PASS** in the perimeter recorded in [docs/FINAL-ARCHITECTURE.md](FINAL-ARCHITECTURE.md).

M5.4 is not a public release. It has no approved distribution version. This file does not document an end-user procedure for installing those menus.

## Planned architecture — FLIX

**FLIP** remains the current name of the software, the repository, the CLI, the upstream public releases and the menus implemented on this branch.

**FLIX** is the planned name of the finished product. The approved experience is Explorer context menus: select a file → **Converti con Flip** → choose a format → convert → M4 progress window → result. That architecture is not implemented in full and is not distributed. Upstream AURA releases do not become part of FLIX automatically. This file does not assign FLIX a release date.

The development branch still contains the legacy wheel, tray, resident host, mutexes and IPC/named pipes. Their removal belongs to **M6**, which has not started. **FLIP Setup Manager** belongs to **M7**, which has not started and is not available.

Binding direction: [docs/FINAL-ARCHITECTURE.md](FINAL-ARCHITECTURE.md). Implemented architecture: [ARCHITECTURE.md](../ARCHITECTURE.md). Sequence: [ROADMAP.md](../ROADMAP.md).

## Open milestones

None of the following is complete. None has a public release version.

| Work | Status |
| --- | --- |
| **M5.5** | Document context menus, still to implement. Distinct from the PDF, TXT and data menus in M5.3. |
| **M5.6** | Overall functional testing, still to run or complete. The M5.4.6 PASS does not close this phase. |
| **M6** | Removal of the legacy infrastructure. Not started. Follows the close of M5. |
| **M7** | FLIP Setup Manager. Not started. Follows the close of M6. |
| Global icon revision | Later dedicated phase. Not numbered. |

## Verification sources

[TEST_RESULTS.md](../TEST_RESULTS.md) separates automated tests, in-memory registry checks, reported build results, the real Windows laboratory, and checks that were not executed. That file is the detail record. This section does not copy it.

**0.5.1 package (12 November 2026; publication 2026-11-11T23:45:12Z).** Local validation of the published ZIP: extracted installer, 100 input checks, 117 conversion checks, and the wheel shortcut hint. Those figures stay with 0.5.1. They are not results of upstream v0.6.1 AURA Edition.

**M5.4.5 automated suites.** Reported engine-matrix and in-memory input results for the development branch. They are not a new publication, and they are not the 0.5.1 counts. [TEST_RESULTS.md](../TEST_RESULTS.md) records them as reported, not re-run in the later documentary pass.

**M5.4.6 isolated laboratory (FlipLab).** Recorded as **PASS** inside the documented perimeter:

- installation of 15 menus;
- observation of 86 destination commands;
- Explorer checks on WAV and MP4;
- three real conversions (MP4 → MKV, MP4 → MP3, WAV → MP3);
- source-integrity checks on those originals;
- removal of the 15 menus;
- post-removal registry and visual checks;
- final audit PASS.

That run did not exercise all 86 conversions by hand. Registry and Explorer observations are distinct from the three conversions. See [TEST_RESULTS.md](../TEST_RESULTS.md) for the laboratory detail, including the unresolved post-removal measurement (`MENU_RESIDUI`) whose cause was not determined.

Windows 10 compatibility is an approved FLIX target. It is not recorded here as a completed test of 0.5.1 or of this branch.
