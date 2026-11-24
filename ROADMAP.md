# Roadmap

**Architettura e prodotto finale vincolanti:** [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md)

**FLIX** è l'unica identità del prodotto in questo albero: `src/Flix/Flix.csproj`, namespace `Flix`, eseguibile `Flix.exe`, menu **Converti con FLIX**. Non è una release distribuita. Il repository remoto si chiama ancora `FLIP`. **FLIP 0.5.1** è la base storica documentata di questo fork. Non è l'ultima release pubblica upstream e non è una release di questo branch. Vedi [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

Le fasi **M5–M7** riportate di seguito sono la roadmap ufficiale. Gli stati descrivono l'albero di lavoro dopo M6 e dopo il rebranding, non ancora committato. L'ultimo commit resta `6d6a16e`. Il contenuto storico più avanti nel file resta riferimento anteriore. Non è un piano attivo.

## M5 — Menu contestuale e conversioni

| Fase | Contenuto | Stato |
|------|-----------|--------|
| **M5.1** | Piano menu PNG definitivo | Implementata. |
| **M5.2** | Menu contestuali immagini. Il piano e il comando PNG restano M5.1 | Implementata e consolidata. |
| **M5.3** | Menu PDF, TXT e dati | Implementata e consolidata. |
| **M5.4** | Menu contestuali audio e video. Dipendenza da FFmpeg. 15 estensioni e 86 destinazioni | Implementata. Collaudo reale isolato M5.4.6: PASS, nel perimetro di [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md). |
| **M5.5** | Menu contestuali documenti con i motori disponibili. Fase distinta dai menu PDF, TXT e dati di M5.3 | Implementata nell'albero di lavoro. Senza Pandoc il piano non viene scritto. |
| **M5.6** | Collaudo funzionale complessivo | Il collaudo Esplora file di M6 ha dato PASS su cinque conversioni, annullamento, concorrenza interprocesso e rimozione dei menu. Non ha eseguito a mano ogni destinazione. |

M5.4.6 ha constatato i 15 menu audio/video e gli 86 comandi di destinazione. Le conversioni di quel collaudo restano quelle elencate in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md).

## M6 — Pulizia definitiva

Chiusa nel perimetro registrato sull'applicazione in esecuzione. Non è una GitHub Release.

- Ruota, tray, gesture, suoni, avvio automatico, host residente, mutex e IPC legacy non fanno parte del processo.
- Il processo di una conversione termina con la finestra M4.
- Collaudo Esplora file M6: cinque conversioni PASS, nessun host residente, nessuna tray, annullamento M4 PASS, concorrenza interprocesso PASS, menu rimossi.
- Build Release di quella chiusura: PASS. InputTests di quella chiusura: 174/174. ConversionTests: 107 PASS, 0 FAIL, 8 non eseguiti perché LibreOffice era assente.

## Rebranding FLIX — dopo M6 e prima di M7

Implementato nell'albero di lavoro. Il collaudo visivo in FlipLab non è stato eseguito. Il mockup di riferimento è archiviato fuori dal repository in `FLIX-AUDIT-ARCHIVE-20261124/branding/Screenshot_3.png`. Il sorgente nel tree è `branding/Monogramma XF in bianco fluido.png`. Non è stato generato un altro monogramma.

- Menu padre: **Converti con FLIX**.
- Icona padre: `flix-xf.ico`, 16, 24, 32, 48, 64, 128 e 256 px, canale alfa conservato.
- Il segno bianco è leggibile su menu scuro e tenue su menu chiaro. Il disegno approvato non è stato modificato.
- Icone delle destinazioni: `flix-menu-*-premium-v2.ico`. Il disegno premium non è stato ridisegnato.
- Identificativi nuovi: `Flix.Convert.*`, valore `FlixOwner`, provenienza `FlixCreatedAncestors`, anteprima `Flix.Convert.Preview` con `FlixPrototype`. I nomi precedenti restano solo in `LegacyIdentity`.
- Le chiavi precedenti di proprietà vengono riconosciute e rimosse in installazione e rimozione. Il menu originale `*\shell\Flip.Convert` e un'anteprima di un altro proprietario non vengono cancellati.
- Build isolata del rebranding: InputTests 175/175, ConversionTests 107 PASS, 0 FAIL, 8 non eseguiti perché LibreOffice era assente. Quella build non è più nell'albero. È archiviata fuori dal repository in `FLIX-AUDIT-ARCHIVE-20261124/_local/flix-identity-20261124`. I risultati delle build precedenti restano attribuiti a quelle build.
- L'integrazione nel Setup Manager appartiene a M7 e non è stata fatta.

## FLIX-AUDIT

Completata il 24 novembre 2026 sull'albero di lavoro, senza commit. Sono stati esaminati 3002 file, inclusi ignorati e non tracciati. Il backup completo fuori dal repository è stato verificato: stessi percorsi, stesse dimensioni, campioni letti e confrontati. La struttura interna di `.git` non è stata modificata.

## FLIX-CLEAN

Completata il 24 novembre 2026, senza commit. Sono stati rimossi `src/Flix/bin`, `src/Flix/obj` e le tre icone non premium non usate dal progetto. Sono stati archiviati fuori dal repository, con hash SHA-256 verificati, la cartella `app`, `evidence`, gli asset di copertina e il suono della ruota non referenziati, e le evidenze locali diverse dal sorgente del monogramma e dal laboratorio M552. `packages` resta, perché `Flix.csproj` lo usa come cache di restore. Il laboratorio M552 locale non è stato modificato.

## FLIX-IDENTITY

Verificata il 24 novembre 2026. L'identità operativa del sorgente, del progetto, delle icone di menu, degli script attivi e dei documenti correnti è FLIX. I letterali precedenti restano in `LegacyIdentity` e nei riferimenti al pacchetto storico, all'upstream e al remoto ancora chiamato FLIP. Lo script di manutenzione del monogramma scrive `src/Flix/flix-xf.ico`.

## FLIX-REPOSITORY

Non eseguita. Il remoto è ancora `https://github.com/giovamolinaro/FLIP.git`. La cartella locale è ancora `FLIP`. La procedura di rinomina è preparata e non è stata avviata.

## FLIX-FINAL-VALIDATION

Eseguita il 24 novembre 2026, senza commit e senza installare dipendenze. Build Release isolata: 0 errori, 0 avvisi. `Flix.exe` ha superato InputTests 175/175, inclusi annullamento, collisioni concorrenti, uscita dei comandi ritirati e assenza di host. ConversionTests su questa macchina: 64 PASS, 22 FAIL, 6 NON ESEGUITI. I 22 FAIL dipendono dall'assenza di Pandoc, non installato. I 6 NON ESEGUITI dipendono da LibreOffice, assente. Il risultato precedente di 107 PASS e 8 NON ESEGUITI non è stato riprodotto perché Pandoc manca. HKCU non è stato scritto. M7 non è iniziata.

## FLIX-CLEAN-FINALIZE

Eseguita il 24 novembre 2026, senza commit, senza push e senza rinomina del repository. Il backup `FLIX-AUDIT-BACKUP-20261124` coincide con l'inventario su tutti i 3002 percorsi e su tutte le dimensioni. Gli hash SHA-256 dell'archivio precedente, 1107 file, coincidono con il backup e con `INDEX-SHA256.txt`. L'unica differenza di byte sul file vivo `.git/index` è la cache delle statistiche: `git ls-files -s` è identico, 109 voci. Non ci sono reparse point.

La validazione ha usato una build Release isolata fuori dal repository e la copia già verificata di `pandoc.exe` 3.12.1, SHA-256 `EB392D2661D35ABC597A6288A548CC82F6291B179DA28B24BC5A5D06C742D35D`, messa in `tools\pandoc.exe` accanto a `Flix.exe`. Restore bloccato e offline, senza nuove dipendenze. InputTests avviati da `Flix.exe`: 175 PASS, 0 FAIL. ConversionTests: 107 PASS, 0 FAIL, 8 NON ESEGUITI perché LibreOffice è assente. Un avvio tramite `dotnet Flix.dll` ha dato 173 PASS e 2 FAIL su `menu_retired_process_exits` e `menu_concurrent_processes`, perché quei controlli richiedono che il processo si chiami `Flix.exe`. Non sono una regressione del motore. HKCU non è stato scritto da queste suite. M7 non è iniziata.

## M7 — Distribuzione

Non è iniziata. Il rebranding non ha creato un installer né un bootstrap. I requisiti approvati restano in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md). Le voci del Setup Manager non sono state rinominate né implementate.

- Pacchetto installabile.
- Setup Manager.
- Installazione con attivazione automatica del menu contestuale.
- Aggiornamento, rollback, attivazione, riparazione e disinstallazione.
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
Review the 22 October 2026 pre-publication review, archived outside the repository as `FLIX-AUDIT-ARCHIVE-20261124/PUBLIC_RELEASE_REVIEW.md`. Close the chosen publication gates, run repository privacy checks, and use a clean source export/initial repository.
Keep development evidence privately in _local. Do not publish the whole workspace or app directory.
No remote publication is automatic.

## Later
Improve accidental multi-selection handling and the optional batch panel.
Reduce distribution/debug-symbol size and profile cold-start and active conversion cost.
Evaluate audio/video, archives, OCR and office spreadsheet/presentation categories as separate modules with tested source/destination matrices.
Do not promise arbitrary conversion of every file type or unsupported Pages output.
