# Publication status — 12 November 2026

This file records four different lines. They must not be read as one:

| Line | Meaning |
| --- | --- |
| **Latest upstream public release** | **FLIP v0.6.1 — AURA Edition** on [`thetinyfoundry/FLIP`](https://github.com/thetinyfoundry/FLIP), published 2026-11-20T14:16:08Z. A public release, not a prerelease. Not a release of this fork, not branch `sviluppo-personalizzato`, and not FLIX. |
| **Historical base of this fork** | **FLIP 0.5.1**, the documented package of this lineage. Local validation record: 12 November 2026. Upstream publication of that release: 2026-11-11T23:45:12Z. |
| **Unreleased development branch** | Branch `sviluppo-personalizzato` at `e6cfcf049277a30f93f8842a5f5769f0af01d697`, through milestone M5.4. Consolidated on the remote development branch. Not a GitHub Release. |
| **Working tree** | **FLIX** in this working tree: `Flix.exe` and **Converti con FLIX**. Not a downloadable release. M7 has not started. |

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

The Windows download of that package was built with `Package.ps1` as it existed in the 0.5.1 package. That script is not in this working tree. The copy removed from this tree is archived outside the repository. The package includes the executable, installer scripts, dependency notices and SHA256 checksums. It requires the .NET 10 Windows Desktop Runtime x64. The package does not include debug symbols, local logs, personal documents or development caches.

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
- Installation: `Install-FLIP.cmd` inside the extracted 0.5.1 ZIP. That file is part of the published package. It is not a script of this working tree.

The [Windows package v0.5.1](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1) includes the Windows x64 ZIP and its SHA-256 checksum. Local validation recorded on 12 November 2026 (upstream publication 2026-11-11T23:45:12Z) covered the extracted installer, 100 input checks, 117 conversion checks and the wheel shortcut hint in the multiple-conversion window. Those counts belong to the 0.5.1 package. They are not results of upstream v0.6.1 AURA Edition, they are not the later M5.4.5 suite figures, and they do not certify every Windows keyboard, display or audio device.

Earlier tags on the upstream product line include [`v0.4.0-beta.3`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.4.0-beta.3) (recorded 23 October 2026) and [`v0.5.0-beta.1`](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.0-beta.1) (recorded 7 November 2026). They are previous publications. They are not the 0.5.1 package of this lineage and they are not the latest upstream release.

Earlier audits, including the 22 October 2026 review now archived outside the repository as `FLIX-AUDIT-ARCHIVE-20261124/PUBLIC_RELEASE_REVIEW.md`, recorded beta gates that blocked an executable at that date. Those statements are historical. They do not withdraw the already published 0.5.1 package. Remaining hardware, listening and clean-machine limits of that package stay as recorded above. The Windows install narrative of that package is archived as `FLIX-AUDIT-ARCHIVE-20261124/docs/WINDOWS-INSTALL.md`.

The 0.5.1 asset linked above remains the published package. This repository's README describes FLIX. See [CHANGELOG.md](../CHANGELOG.md) for the 0.5.1 notes, kept as the record of that package.

## Development branch — not released

This section describes branch **`sviluppo-personalizzato`** at commit `e6cfcf049277a30f93f8842a5f5769f0af01d697`. At that commit the product name in the tree was still FLIP. It is not a public release, not an installable end-user package, and not an approved product version. The working tree after that commit is FLIX. The counts below stay with that commit. They are not reattributed.

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
- registration protections at that commit (`FlipOwner` checks and best-effort backup/rollback);
- isolated real testing **M5.4.6 PASS** in the perimeter recorded in [docs/FINAL-ARCHITECTURE.md](FINAL-ARCHITECTURE.md).

M5.4 is not a public release. It has no approved distribution version. This file does not document an end-user procedure for installing those menus.

## Current working tree — FLIX

**FLIX** is the product identity in this working tree: `src/Flix/Flix.csproj`, namespace `Flix`, executable `Flix.exe`, Explorer command **Converti con FLIX**. It is not a downloadable release. The remote repository is still named FLIP. The historical package and the upstream releases keep the names they were published under.

The wheel, tray, resident host, mutexes and named pipes are not in the running process. Their removal is **M6**, present in this working tree and not a public release. **M7**, the Setup Manager, has not started. There is no FLIX installer in this tree.

Binding direction: [docs/FINAL-ARCHITECTURE.md](FINAL-ARCHITECTURE.md). Implemented architecture: [ARCHITECTURE.md](../ARCHITECTURE.md). Sequence: [ROADMAP.md](../ROADMAP.md).

## Open milestones

| Work | Status |
| --- | --- |
| **M5.5** | Document context menus, present in the working tree. Not a public release. |
| **M5.6** | The M6 Explorer run covered five conversions, cancellation, interprocess concurrency and menu removal. A hand run of every destination is still open. |
| **M6** | Legacy infrastructure removed from the running process in the working tree. Not a public release. |
| **M7** | Setup Manager. Not started. |
| Global icon revision | The parent uses the approved XF monogram. Destination icons are the existing premium artwork under `flix-menu-*`. |

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
