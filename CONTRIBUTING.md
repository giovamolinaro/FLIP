# Contributing
Build on Windows using README.md. Keep changes scoped and include validation relevant to the behavior you change.

Branch `sviluppo-personalizzato` still contains the legacy wheel and host together with the context menus implemented through M5.4. FLIX is the planned architecture and is not released. Notes below on the wheel, mouse buttons and Ctrl apply to that legacy path. They are not requirements of FLIX.

Do not commit conversion inputs, generated output, raw logs, local paths, credentials, packaged third-party executables or proprietary assets.
Preserve original source files. Keep Copy-only drag/drop, no-overwrite output commits and user-owned shell integration.
Clearly separate automated checks, in-memory registry checks, off-screen renders, native desktop evidence and user acceptance. An in-memory registry check does not open the user hive. Record only the evidence you actually collected.

Before changing codecs or external engines, test signatures, decode/readback, filename collisions, cancellation and cleanup. Test swapped mouse buttons and Ctrl-before/Ctrl-during behavior when the change touches the legacy wheel.
The CI input checks are not proof of physical Explorer drag/drop.
