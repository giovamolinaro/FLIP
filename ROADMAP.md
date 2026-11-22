# Roadmap

**Architettura e prodotto finale vincolanti:** [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md)

**FLIP** è il nome attuale del software, del repository e dei comandi esistenti. **FLIX** è il nome pianificato del prodotto finale, basato esclusivamente sui menu contestuali. L’implementazione integrale non è conclusa: FLIX non è un prodotto disponibile o distribuito. **FLIP 0.5.1** è la base storica documentata di questo fork. Non è l’ultima release pubblica upstream e non è una release del branch di sviluppo `sviluppo-personalizzato`. Vedi [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

Le fasi **M5–M7** riportate di seguito sono la roadmap ufficiale approvata. Gli stati descrivono il branch di sviluppo al consolidamento di M5.4. Il contenuto storico più avanti nel file resta riferimento di contesto anteriore all’architettura definitiva. Non è un piano attivo.

La revisione grafica globale delle icone resta una fase successiva dedicata, senza un numero di milestone in questo documento.

## M5 — Menu contestuale e conversioni

| Fase | Contenuto | Stato |
|------|-----------|--------|
| **M5.1** | Piano menu PNG definitivo | Implementata nel branch. Piano e comando PNG presenti. |
| **M5.2** | Menu contestuali immagini. Il piano e il comando PNG restano M5.1 | Implementata e consolidata. |
| **M5.3** | Menu PDF, TXT e dati | Implementata e consolidata. |
| **M5.4** | Menu contestuali audio e video. Dipendenza da FFmpeg. 15 estensioni e 86 destinazioni | Implementata, collaudata nel perimetro definito e consolidata. Collaudo reale isolato M5.4.6: PASS, nel perimetro di [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md). Commit `e6cfcf0` presente sul branch remoto di sviluppo, senza GitHub Release. |
| **M5.5** | Menu contestuali documenti con motori disponibili. Fase distinta dai menu PDF, TXT e dati di M5.3 | Da fare. |
| **M5.6** | Collaudo funzionale complessivo | Da fare. Il perimetro PASS di M5.4.6 non chiude questa fase. |

M5.4.6 ha constatato i 15 menu e gli 86 comandi di destinazione. Le conversioni eseguite in quel collaudo reale isolato sono quelle elencate in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md). I controlli di Registro e di Esplora file restano distinti da quelle conversioni.

**M6 non inizia prima della chiusura di M5.**

## M6 — Pulizia definitiva

Posizione e perimetro generale invariati. La fase non è iniziata. La finalità della rimozione legacy è il passaggio a un ciclo di vita legato alla singola conversione.

- Audit dipendenze del codice.
- Rimozione legacy:
  - ruota di conversione;
  - tray e interfacce associate;
  - gesture e drag-and-drop legacy;
  - suoni e feedback della ruota;
  - avvio automatico legacy;
  - host e processo residente non necessario;
  - mutex legacy;
  - IPC e named pipe legacy.
- Processo effimero e gestione concorrenza.
- Eliminazione codice morto.
- Collaudo completo.

Il dettaglio implementativo resta fuori da questa roadmap: nessun protocollo nuovo e nessun sostituto del mutex sono definiti qui.

**M7 non inizia prima della chiusura di M6.**

## M7 — Distribuzione

Dedicata al FLIP Setup Manager. Non è iniziata. Sequenza, dipendenze e requisiti già approvati restano quelli elencati sotto e in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md).

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

Note storiche del percorso precedente. Non sono requisiti attivi di M5–M7.

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
