# Security
FLIX processes local files with managed and native libraries and optional external subprocesses for documents and media. Treat untrusted documents with care. Process separation and limits are not a complete security sandbox.

Per-user registry changes use HKCU. New menu keys carry `FlixOwner`. If a menu write fails, restoration is best-effort: HKCU is not transactional. The original shell verb is not a key this application rewrites. Output is committed as a new file beside the source, without overwriting an existing file. That is not a guarantee for every failure. A killed process can leave a private `.flix-job-` directory, and an external engine may receive the original path as input. Diagnostic logs can contain file paths. New logs go under `%LOCALAPPDATA%\Flix\logs`. An older log folder is not deleted.

Please avoid uploading sensitive inputs or unsanitized logs in public issues. For an issue requiring confidentiality, use the repository's private vulnerability reporting if enabled; otherwise ask the maintainer for a private contact without posting exploit details.

No network service, telemetry or account is required at runtime. Building and optional dependency downloads access upstream package servers.
