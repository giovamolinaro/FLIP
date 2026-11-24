# FLIX for Windows

FLIX converts a file on the local machine from the Explorer context menu. Select a file, open **Converti con FLIX**, choose a format, and the progress window **FLIX — Conversione** runs that conversion. When the window closes, the process exits. There is no resident host.

This working tree is not a downloadable release. Phase M7, the Setup Manager, has not started.

## Build

Install the [.NET 10 SDK for Windows x64](https://dotnet.microsoft.com/download/dotnet/10.0). From the repository root:

```powershell
.\scripts\Build.ps1 -Output C:\path\new\flix
```

The script publishes `Flix.exe` into the folder you pass. If that folder already contains `Flip.exe`, it stops and deletes nothing. Pass a new folder. Building this branch does not produce a public release. The published build is framework-dependent and needs the .NET 10 Windows Desktop Runtime x64.

Project: `src/Flix/Flix.csproj`. Namespace: `Flix`. Target: `net10.0-windows`, x64. Direct libraries: PDFtoImage and PdfPig. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Use

1. Select a file in Explorer.
2. **Converti con FLIX**.
3. Choose an output format shown by the menu.
4. Watch the progress window.
5. The result is a new file beside the source. An existing file is not overwritten.

The parent command uses `flix-xf.ico`. Destination entries use `flix-menu-*-premium-v2.ico`.

New installations use menu ids `Flix.Convert.*` and the owner value `FlixOwner`. `LegacyIdentity` recognizes older keys written by this application. It does not remove the protected menu `*\shell\Flip.Convert` or a preview owned by someone else.

`Flix.exe` is accepted from `%LOCALAPPDATA%\Programs\Flix` and, for a layout already present, from `%LOCALAPPDATA%\Programs\Flip`. Laboratory paths use the segment `Flip-Lab` because the collaudo account is still named that way. Conversion flags are `--convert-menu`, `--convert`, and `--files`.

Diagnostic logs of this build go to `%LOCALAPPDATA%\Flix\logs`. An older log folder is not deleted.

## Engines

Native image, text, and structured-data conversions do not need an external program. Audio and video menus are installed only when FFmpeg is detected. Document menus are installed when Pandoc is detected. LibreOffice is a separate optional installation used when a document is rendered to PDF or to an image. FFmpeg, Pandoc, and LibreOffice are not bundled.

The menu shows formats that the detected engines can offer. Windows 10 and Windows 11 are the intended platforms. The visual check of the XF icon in the FlipLab account has not been run.

## Recorded checks

The isolated identity build kept outside this repository, in `FLIX-AUDIT-ARCHIVE-20261124/_local/flix-identity-20261124`, passed 175 input checks and 107 conversion checks. Eight LibreOffice-dependent checks were not executed. Those figures stay with that build.

The validation of this cleanup, on 24 November 2026, used an isolated `Flix.exe` and Pandoc 3.12.1 already present on the machine. Input checks: 175 passed, 0 failed. Conversion checks: 107 passed, 0 failed, 8 not executed because LibreOffice is absent. The FlipLab visual check of the XF icon has not been run. Detail: [ROADMAP.md](ROADMAP.md).

## Provenance

The remote is still `https://github.com/giovamolinaro/FLIP.git`. Renaming the repository has not been done. Published releases on [`thetinyfoundry/FLIP`](https://github.com/thetinyfoundry/FLIP), including [FLIP 0.5.1](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1) and [FLIP v0.6.1 AURA Edition](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.6.1), keep the names they were published under. They are not this working tree and they are not a release of this fork. See [Publication status](docs/PUBLICATION-STATUS.md).

## License

The [LICENSE](LICENSE) file in this fork is the MIT License. The copyright line remains `Copyright (c) 2026 Flip contributors`. The upstream AURA `LICENSE` at v0.6.0 and v0.6.1 states GPL-3.0-only. That text does not relicense this fork. Third-party components keep their own licenses: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Conversions run locally. Review logs before sharing them. See [SECURITY.md](SECURITY.md).

[Italiano](README.it.md) · [Contributing](CONTRIBUTING.md) · [Changelog](CHANGELOG.md) · [Architecture](ARCHITECTURE.md) · [Security](SECURITY.md)
