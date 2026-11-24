# Contributing
Build on Windows using README.md. Keep changes scoped and include validation relevant to the behavior you change.

The product in this working tree is FLIX: `src/Flix/Flix.csproj`, namespace `Flix`. Branch `sviluppo-personalizzato` no longer contains the wheel, the tray, or the resident host. M7 has not started. Notes that mention the wheel, mouse buttons and Ctrl describe the historical 0.5.1 package. They are not requirements of FLIX.

Do not commit conversion inputs, generated output, raw logs, local paths, credentials, packaged third-party executables or proprietary assets.
Preserve original source files. Keep Copy-only drag/drop, no-overwrite output commits and user-owned shell integration.
Clearly separate automated checks, in-memory registry checks, off-screen renders, native desktop evidence and user acceptance. An in-memory registry check does not open the user hive. Record only the evidence you actually collected.

Before changing codecs or external engines, test signatures, decode/readback, filename collisions, cancellation and cleanup.
The CI input checks are not proof of an Explorer context-menu pass.
