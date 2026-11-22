# Security
Flip processes local files with managed and native libraries and optional external subprocesses for documents and media. Treat untrusted documents with care. Process separation and limits are not a complete security sandbox.

Per-user registry changes use HKCU. M5 menu keys carry FlipOwner. If a menu write fails, restoration is best-effort: HKCU is not transactional. The older shell verb does not use FlipOwner. Output is committed as a new file beside the source, without overwriting an existing file. That is not a guarantee for every failure. A killed process can leave a private job directory, and an external engine may receive the original path as input. Diagnostic logs can contain file paths.

Please avoid uploading sensitive inputs or unsanitized logs in public issues. For an issue requiring confidentiality, use the repository's private vulnerability reporting if enabled; otherwise ask the maintainer for a private contact without posting exploit details.

No network service, telemetry or account is required at runtime. Building and optional dependency downloads access upstream package servers.
