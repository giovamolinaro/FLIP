# Roadmap

**Architettura e prodotto finale vincolanti:** [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md)

Le fasi **M5–M7** riportate di seguito sono la roadmap ufficiale approvata. Il contenuto storico più avanti nel file resta come riferimento di contesto e non va interpretato come piano alternativo al menu contestuale e al Setup Manager definitivi.

## M5 — Menu contestuale e conversioni

| Fase | Contenuto | Stato |
|------|-----------|--------|
| **M5.1** | Piano menu PNG definitivo | Completato localmente (non ancora committato al momento della formalizzazione) |
| **M5.2** | Tutte le estensioni immagine (include eventuale collegamento CLI del menu PNG concordato in M5.1, senza rinominare questa fase) | Da fare |
| **M5.3** | PDF, TXT e dati | Da fare |
| **M5.4** | Audio e video con FFmpeg | Da fare |
| **M5.5** | Documenti con motori disponibili | Da fare |
| **M5.6** | Collaudo funzionale completo | Da fare |

**M6 non inizia prima della chiusura di M5.**

## M6 — Pulizia definitiva

- Audit dipendenze del codice.
- Rimozione ruota, suoni, gesture e drag-and-drop.
- Rimozione tray e startup.
- Processo effimero e gestione concorrenza.
- Eliminazione codice morto.
- Collaudo completo.

**M7 non inizia prima della chiusura di M6.**

## M7 — Distribuzione

- Pacchetto installabile.
- FLIP Setup Manager (menu `[0]`–`[6]` come in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md)).
- Installazione con attivazione automatica del menu contestuale.
- Aggiornamento e rollback (backup, stato attivo/disattivo, ripristino registry/versione).
- Attivazione/disattivazione menu contestuale.
- Riparazione e disinstallazione (policy su `%LOCALAPPDATA%\Flip` e file utente).
- Pubblicazione HTTPS (dominio da definire).
- Firma Authenticode, verifica integrità e sicurezza.
- Test su PC puliti Windows 10 e 11.

---

## Contenuto storico (contesto pre-architettura definitiva)

## Before public executable release
- Recheck after the installer-requested Windows restart. Authorized LibreOffice 26.8.1 upgrade and the corrected pre-restart conversion suite passed.
- Complete physical Explorer/Desktop drag with configurable keys in both orders, OLE Escape/cancellation and mixed-DPI monitor/edge checks.
- Validate the new tic on actual output devices and the revised transparency over busy folders.
- Confirm startup after actual sign-in/reboot, not just registration/manual quiet launch.
- Validate an isolated clean build/runtime and finish exact bundle notices/source obligations, packaging and signing.

## Source publication
Review PUBLIC_RELEASE_REVIEW.md, close the chosen publication gates, run repository privacy checks, and use a clean source export/initial repository.
Keep development evidence privately in _local. Do not publish the whole workspace or app directory.
No remote publication is automatic.

## Later
Improve accidental multi-selection handling and the optional batch panel.
Reduce distribution/debug-symbol size and profile cold-start and active conversion cost.
Evaluate audio/video, archives, OCR and office spreadsheet/presentation categories as separate modules with tested source/destination matrices.
Do not promise arbitrary conversion of every file type or unsupported Pages output.
