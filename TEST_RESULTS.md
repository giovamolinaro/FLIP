# Validation

The sections from “Current: 0.4.0-beta.3” through “Release gate” are historical records. Each heading names the candidate, and the date where one was written. Those results are kept as recorded. “Current” in the first heading means current when that record was produced. It does not describe branch `sviluppo-personalizzato`, and this file does not declare a new public release. File names, project paths and registry value names inside those records are the names of the build that produced them. The current working tree is FLIX (`src/Flix/Flix.csproj`). **FLIP 0.5.1** is the documented historical base of this fork. It is not the latest upstream public release. See [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

## Current: 0.4.0-beta.3

Custom single-key shortcuts and both held-key/drag input orders are covered by **94 passing input checks**; the conversion engine has **99 passing checks**. The extended record is archived outside the repository as `FLIX-AUDIT-ARCHIVE-20261124/docs/CUSTOM-SHORTCUTS.md`. The counts above stay as recorded. The earlier results below describe previous candidates.

## 0.4.0-beta.2 audit, 23 October 2026

The extended audit is archived outside the repository as `FLIX-AUDIT-ARCHIVE-20261124/docs/AUDIT-2026-10-23.md`. The counts in that record stay as recorded. The gesture of that candidate is `<>`, not Ctrl. The historical results below describe beta.1 and do not certify native behavior of beta.2.

## Historical: 0.4.0-beta.1
Date: 22 October 2026. Raw evidence stays under ignored _local/evidence and _local/review; it is not exported.

## Automated results
- **99 conversion checks passed, 0 failed**, covering **57 conversion cases**, with LibreOffice 26.8.1.1 before the installer-requested Windows restart.
- **45 input/policy/layout/audio/bounds checks passed, 0 failed**.
- Release build and publish: zero compiler warnings/errors.
- Locked restore with NuGet audit enabled, all dependencies, and audit-connection errors treated as errors: succeeded. No NuGet advisories were reported for the resolved graph at this check. An earlier sandbox-only attempt could not reach the audit feed; it was not counted as a successful security check.
- PowerShell helper syntax parsed without errors. Script execution follows the host policy; no policy bypass was used. The prepared GitHub workflow has not run remotely.

Conversion checks include signatures/decoding, white JPEG alpha, EXIF orientation, PDF pages, Italian text, DOCX/EPUB structure, RTF/ODT/FB2/RST text readback, collision concurrency, cancellation, corrupt images, source hashes and no staging residue. A synthetic compressed document expanding past 256 MiB was rejected before conversion.
The initial FB2 readback failure was corrected and the full suite rerun successfully; failed runs were retained as private evidence.
The authorized LibreOffice upgrade initially exposed five failures (90 passed, 5 failed), reproduced on a second run. A short-path control succeeded. Moving the isolated Office profile out of the source-adjacent staging folder to the Windows temporary directory fixed the tested failures; a deep-folder PDF regression and temporary-profile cleanup checks now pass. These failed runs are retained privately.

Input/layout checks cover both Ctrl sides/orderings, swapped primary buttons, all TXT outputs represented in the wheel, missing-engine capabilities, unsupported batches, empty/oversized batches, radial geometry at 1/2/3/6/8/12 sectors, log rotation, preference parsing and original PCM audio with no clipping/faded endpoints.

## Native Windows flow
The real optional panel displayed twelve TXT output choices. UI actions selected PDF/DOCX/HTML/JPG and clicked Convert.
It reported four outputs and zero errors. Files were present beside the synthetic source; an independent SHA256 comparison confirmed the original remained unchanged. No job directory remained.
This is native panel E2E evidence, not proof of the complete Ctrl+Explorer drag on the new dynamic layout.

A separate runtime copy without Pandoc converted TXT to PDF/JPG and PDF to TXT. The installed module no longer falls back silently to ancestor development tools.

Earlier user tests established Ctrl-before and Ctrl-during wheel activation, swapped-button Explorer dragging and actual conversions. Physical mixed-DPI monitors, Desktop-origin drag and the complete cancellation matrix remain incomplete.

## Measured idle resources
One current candidate run, Windows 11 x64, .NET 10. After three seconds settling:
- Observation: 9.9916179 s.
- CPU time delta: 0 ms at the observed process-counter resolution.
- Working set: 107171840 bytes (**102.21 MiB**).
- Private bytes: 70864896 (**67.58 MiB**).
- Threads: 25. Handles: 672.
- Mouse listener off, no job/panel/wheel open.

This is a single observation, not proof of zero CPU use over time.
The local app folder is about 334.65 MiB including Pandoc and debug symbols; a small source archive is not a small runtime bundle.

## Visual and audio evidence
WPF renders show the slightly translucent backing, larger rim margin, dynamic compatible sectors and no extra-formats button. Native panel appearance and behavior were inspected.
The sound changed from a low tone to an original **40 ms tic**. Its waveform passed programmatic checks; subjective/device acceptance of this new sound is still open. Playback-request logs are not acoustic proof.

## Release gate
LibreOffice was upgraded from 24.2.5.2 to 26.8.1.1 with explicit authorization. The official installer SHA256 matched its published checksum and Windows SDK SignTool verified the Authenticode signature by The Document Foundation with zero errors/warnings. The personal profile was backed up privately. Installation returned 3010 (restart required); no automatic restart was performed.
The new engine passed the full corrected suite above. This is pre-restart evidence on synthetic documents, not qualification of arbitrary untrusted documents. Executable release remains blocked pending post-restart checks, broader native acceptance and redistribution work. The review named here is archived outside the repository as `FLIX-AUDIT-ARCHIVE-20261124/PUBLIC_RELEASE_REVIEW.md`.

## Development branch test status

Branch `sviluppo-personalizzato`, commit `e6cfcf049277a30f93f8842a5f5769f0af01d697`. This is unreleased development work on FLIP after milestone M5.4. Commit `e6cfcf0` is present on the remote development branch, without a GitHub Release. **FLIP 0.5.1** is the documented historical base of this fork. See [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

The laboratory date below applies only to M5.4.6: 22 November 2026. No date is recorded here for the M5.4.5 automated run.

DOC-01.5-B did not rebuild, did not run FLIP, and did not re-run the suites. No original log, `input-tests.json`, matrix `summary.txt`, sample file, `.reg` backup, or checksum manifest from these runs is stored in the repository. Reading the suite source shows what a run would count. It is not evidence that the run happened.

| Label | Meaning |
|-------|---------|
| A | Present in the repository: source, or a document already committed or already edited on this branch. |
| B | Attested by the operational record used for this update. |
| C | Not reproduced during DOC-01.5-B. |

### 1. Automated engine tests — M5.4.5, reported

The operational record reports a Release build with zero compiler errors and zero warnings, and the suite results in the table. There is no build log or suite log in the repository (B, C).

The same number 86 appears in two different measurements further below: conversion cells inside `MediaMatrixTests`, and destination commands observed in the M5.4.6 laboratory. They are not added together, and neither one is 86 hand tests in File Explorer.

| Reported result | What the source counts | Reconciliation | Evidence |
|-----------------|------------------------|----------------|----------|
| Release build: zero errors, zero warnings | `scripts/Build.ps1` publishes with `dotnet publish -c Release`. CI at M5.4.5 time built with `dotnet build src/Flip/Flip.csproj -c Release` (historical FLIP path; current tree: `src/Flix/Flix.csproj`). The record does not say which invocation was used. | Reported outcome only. No log in the tree. | B, C |
| `InputTests`: 234/234 | `--test-input` calls `InputTests.Run` unless a matrix switch is also present. A static list of the names that `Run` puts in its dictionary is 234 distinct checks: 111 in the body of `Run`, 121 `Check` registrations, and 2 IPC checks. | The reported 234/234 matches that dictionary size. The 234 include legacy gesture, wheel, and policy checks together with the in-memory menu checks in section 2. They are not 234 M5.4 conversions. | B for the pass count; A for the name count; C for execution |
| Audio/video matrix: 86/86 | `--test-input --media-matrix-only` calls `MediaMatrixTests.Run`. With FFmpeg, the suite’s own success condition requires 42 audio cells and 44 video cells. | 42 + 44 = 86 conversion cells of `ConversionEngine`. Not menu registration and not Explorer clicks. | B for the pass count; A for the 42/44 split; C for execution |
| Negative tests: 55 | On that same FFmpeg-present path, `MediaMatrixTests` records 55 negative and boundary rows: 40 policy refusals, 4 unreadable files, 5 silent-video cases, 2 spaced paths, 2 naming cases, 1 simulated FFmpeg absence, and 1 residual `.flip-job` check. | The reported 55 matches that row count. These rows are separate from the 86 conversion cells. The record states 55; it does not state a separate pass fraction beyond that figure. | B for the figure; A for the row count; C for execution |
| Image matrix: 48/48 | `--test-input --image-matrix-only` calls `ImageMatrixTests.Run`. `Policy.Images` has 8 extensions, and the image menu plans 6 destinations each. | 8 × 6 = 48 matrix cells. The suite also records two naming rows. Those two rows are outside the 48, and this update has no separate outcome for them. | B for 48/48; A for the 8 × 6 shape; C for execution |
| Native content tests: 43/43 | `--test-input --content-matrix-only` calls `ContentMatrixTests.Run`. The source expects 43 native cells: PDF 4, TXT 4, and 7 each for JSON, YAML, YML, XML, and CSV. | The reported 43/43 matches that native set. | B for the pass count; A for the split; C for execution |
| 16 optional Pandoc conversions not executed | The same content suite expects 16 further cells, 8 from PDF and 8 from TXT. If Pandoc is absent they are marked not executable and are not installed. | Consistent with the record: not executed because the engine was absent. A plan size of 59 with Pandoc (43 + 16) is a catalog figure in `content_native_43_and_pandoc_59`. It is not a count of executed conversions. | B for “not executed”; A for the 16; C for execution |

`ContentMatrixTests` also records negative and boundary rows. Their count and outcome are not in the operational figures, so they are not marked passed or failed here. `ImageMatrixTests` naming rows are in the same position. `--test-conversions` (`ConversionTests.Run`) is a separate engine suite. No M5.4.5 result for it is in the figures used here.

### 2. Menu tests with an in-memory registry

These checks are inside the 234 `InputTests` checks. They are not a second run and they are not added to 234.

`InputTests` calls `ApplyImages`, `ApplyContent`, and `ApplyMedia` with an injected `IRegistryStore`. The stores used in those checks are `MemoryRegistryStore`, `TouchRegistry`, `FailAfterWriteStore`, and `FailAfterDeleteStore` (`InputTests.cs`, `ContextMenuPreview.cs`). `MemoryRegistryStore` is a dictionary. It does not open `Registry.CurrentUser`. A passing in-memory check is not a result on real HKCU.

The production commands (`--install-context-menu-images`, `--install-context-menu-pdf-text-data`, `--install-context-menu-audio-video`, and the matching `--remove-context-menu-*` commands) use `CurrentUserRegistryStore` when they are launched for real. The tests do not.

The source comment on the menu commands states that HKCU is not transactional, that restore is an attempt, and that a failed restore can leave the registry partial. Exit code 0 means the requested procedure finished. By itself it does not prove that every planned key is absent. Check `next_image_remove_restore_is_not_atomic` records that limit in the image suite. The media and content checks also drive stores that fail after a write or a delete, including a restore that does not match the snapshot.

Within that in-memory perimeter, the named checks cover:

| Area | What the named checks exercise |
|------|--------------------------------|
| Registration plans | Image: 8 extensions, 6 destinations, JPEG/TIFF aliases. PNG plan: 6 destinations. Content: 7 extensions, 43 native destinations, 59 when Pandoc is included, YAML/YML sharing one command tree. Media: 15 menus, 42 audio and 44 video destinations, 86 commands, no aliases. An empty media plan when FFmpeg is absent. Pandoc and LibreOffice do not change the media plan. |
| Engine prerequisites | Media install writes nothing when FFmpeg is absent. Missing or invalid premium icons refuse image, content, and media installation before a write. Removal of image menus is also checked without icons. |
| Icons | Premium icon file names and the readiness gate. Media destination icons are the 11 `flip-menu-*-premium-v2.ico` files. WMA, OPUS, WMV, and M4V have no destination icon. |
| `FlipOwner` | Planned keys carry the menu id. A foreign owner is refused. The foreign key is left in place and the install does not write. |
| Backup and rollback | Snapshot before mutation, restore after a failed write, restore after a failed delete, and an explicit non-atomic remove/restore case. Best-effort restore, not a transaction. |
| Simulated install and removal | Install and remove through the in-memory store for PNG, images, content, and media. Repeat install leaves the exported store unchanged for PNG, images, content, and media. Repeat removal is checked the same way where those checks exist. |
| Idempotence | `preview_install_idempotent`, `next_install_idempotent`, `next_image_memory_install_remove_idempotent`, `content_install_repeat_no_duplicates`, and the second media install inside `media_install_ffmpeg_fifteen_menus`. |
| Category separation | Media planning does not change image, content, or preview plans. Content planning does not change image plans. Media removal leaves image, content, preview, and the legacy `Flip.Convert` key in place, and it leaves a foreign-owned media key in place. |
| Foreign registrations | Collision with another owner is refused. A decoy subkey with another owner survives media removal. |
| CLI parameters | Install together with remove is refused. Extra arguments are refused. `bin`, `obj`, and other unapproved executable paths are refused. `%LOCALAPPDATA%\Programs\Flip` and a `Flip-Lab` path are the accepted shapes for the image, content, and media commands. These command checks return before the host starts. |

### 3. Build and publication checks

| Check | Status recorded here | Evidence |
|-------|----------------------|----------|
| Release build, zero errors and zero warnings | Reported for M5.4.5. No log in the repository. Not re-run. | B, C |
| New public package or new GitHub Release | Not claimed. Commit `e6cfcf0` is present on the remote development branch, without a GitHub Release. | A, from `ROADMAP.md` and `CHANGELOG.md` |
| FLIP 0.5.1 package | Documented historical base of this fork, not the latest upstream public release. [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md) (12 November 2026) records local validation of that package: extracted installer, 100 input checks, 117 conversion checks, and the wheel shortcut hint. Those counts belong to 0.5.1. They are not the 94/99 figures in the 0.4.0-beta.3 section above, and they are not the M5.4.5 figures. | A |

### 4. Real Windows laboratory

Recorded in the next section. It is not part of the automated counts.

### 5. Checks not executed

An empty cell is not a pass and not a failure.

| Item | Status |
|------|--------|
| 16 Pandoc-dependent content conversions in the M5.4.5 content matrix | Not executed. Pandoc was absent. |
| Hand execution of all 86 audio/video destinations | Not executed. M5.4.6 ran three conversions. The other destinations have no manual result here. |
| Every context menu, exercised by hand in File Explorer | Not executed. WAV and MP4 were the menus checked visually. |
| Windows 10 compatibility as a completed test | Not executed. Approved targets include Windows 10 and Windows 11. This file does not record a finished Windows 10 pass. |
| M5.6, overall functional test | Not concluded. The M5.4.6 result does not close it. |
| M5.5 document menus, M6 legacy removal, M7 Setup Manager | Not started. |
| `ConversionTests` (`--test-conversions`) during M5.4.5 | No result in the figures used here. Not marked passed or failed. |
| Image-matrix naming rows, and content-matrix negative rows | No separate result in those figures. Not marked passed or failed. |
| Sample SHA-256 values | Not copied. No sample checksum in the repository documents matches a value that could be repeated here. The laboratory record says the WAV original’s SHA-256 was unchanged. The hash itself is not in that record as used here. |

### 6. Limits

- Automated matrices call `ConversionEngine` directly. The file comments exclude the launcher, the session mutex, named pipes, the registry, and, for the media matrix, the wheel, the tray, and context menus.
- In-memory menu checks do not write HKCU and do not show File Explorer.
- Exit code 0 of a menu install or removal means the requested procedure returned without a registry exception.
- Restore after a partial failure is best-effort. The implementation states that the operation is not atomic.
- Three real conversions do not qualify the other destination commands.
- M5.4.6 does not close M5.6.
- The unresolved `MENU_RESIDUI` discrepancy in the laboratory section is not, by itself, a product defect.

## M5.4.6 — Isolated Windows laboratory test

Real Windows run, separate from the automated suites. Outcome recorded as **PASS** inside the perimeter below. The same perimeter, in shorter form, is already stated in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md), [ARCHITECTURE.md](ARCHITECTURE.md) section 9, the unreleased section of [CHANGELOG.md](CHANGELOG.md), and the development-branch section of [README.md](README.md). The rows in this section add the laboratory detail from the operational record. That detail was not reproduced in DOC-01.5-B (B, C). No laboratory log path is stored in the repository.

### Environment

| Item | Recorded value |
|------|----------------|
| Milestone | M5.4.6 |
| Date | 22 November 2026 |
| Account | `ITC-PC\FlipLab` |
| Session | Separate RDP session |
| Shell | Standard PowerShell, not elevated |
| Registry hive | HKCU of FlipLab, distinct from the ITC account |
| Binary | Dedicated laboratory build |
| FFmpeg | Available locally |
| Other installation | The stable installation on the ITC account was left in place |

### Installation

These are registry and Explorer observations. They are not 86 manual conversions.

| Observation | Reported result |
|-------------|-----------------|
| Audio/video menus present | 15/15 |
| Destination commands present | 86/86 |
| `FlipOwner` | Correct on the checked keys |
| Command paths | Verified |
| Destination icons | Visible |
| File Explorer | WAV menu and MP4 menu verified |

### Real conversions

Three conversions were run. The other destination commands were not run by hand.

| Conversion | Reported result |
|------------|-----------------|
| MP4 → MKV | Completed. M4 window shown. Source unchanged. Output checked with FFprobe. Full decode succeeded. |
| MP4 → MP3 | Audio extraction completed. M4 window shown. Source unchanged. Output checked with FFprobe. Full audio decode succeeded. |
| WAV → MP3 | Synthetic WAV sample. Conversion completed. M4 window shown. Original SHA-256 unchanged. MP3 was 48 kHz stereo, duration 3 seconds. FFprobe exit code 0. Decode exit code 0. |

### Removal and final audit

| Step | Reported result |
|------|-----------------|
| Preventive backup | 15 keys saved as `.reg` files, with a SHA-256 manifest. The files are not in the repository. |
| Removal | CLI removal. `REMOVE_EXIT_CODE = 0`. |
| Keys afterwards | Absence of the 15 media keys checked with the .NET registry API. Result used for the audit: 0/15. |
| File Explorer afterwards | WAV menu and MP4 menu absent. |
| Leftovers | Laboratory build, samples, and backups still intact. No FLIP host left in the FlipLab session. |
| Audit | `AUDIT_FINALE = PASS`. |

Exit code 0 is the code returned by the removal command. It does not, by itself, prove that every key was gone. The audit uses the later independent checks.

### Unresolved observation after removal

The first PowerShell check after removal reported `MENU_RESIDUI: 15/15`. Later independent checks reported 0/15. The cause was not determined. This update does not assign that first reading to a defect in the program. The audit result above follows the later checks and the Explorer check, not the first PowerShell reading alone.
