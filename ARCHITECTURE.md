# Architettura tecnica attuale

Questo documento descrive l’architettura **implementata** nell’albero di lavoro del branch `sviluppo-personalizzato`, dopo M6 e dopo il rebranding FLIX. L’ultimo commit resta `6d6a16e`. Il rebranding non è committato.

**FLIX** è l'unica identità del prodotto in questo albero: assembly `Flix`, eseguibile `Flix.exe`, progetto `src/Flix/Flix.csproj`, namespace `Flix`, menu **Converti con FLIX**. Non è una release distribuita. Il repository remoto si chiama ancora `FLIP`. Il pacchetto storico **FLIP 0.5.1** e le release upstream su `thetinyfoundry/FLIP` conservano il nome con cui sono state pubblicate. I letterali necessari a riconoscere le chiavi precedenti stanno solo in `LegacyIdentity`.

Il documento di direzione è [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md). La sequenza delle fasi è in [ROADMAP.md](ROADMAP.md). Le decisioni approvate sono in [TECHNICAL_DECISIONS.md](TECHNICAL_DECISIONS.md). M7 non è descritta qui come codice esistente.

Il progetto è un eseguibile Windows x64, `net10.0-windows`, WPF e Windows Forms (`src/Flix/Flix.csproj`). Il valore `Version` nel progetto è `0.5.1`: è il timbro di versione di questo fork, allineato alla base storica FLIP 0.5.1. Non è la release upstream FLIP v0.6.1 AURA Edition e non è una GitHub Release di questo branch. Vedi [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

## 1. Scopo e perimetro

Il perimetro è il codice in `src/Flix` nell'albero di lavoro. L'ultimo commit resta `6d6a16e`; la rinomina non è committata.

Sono descritti:

- il punto di ingresso e l’instradamento dei comandi;
- i menu contestuali M5.1–M5.5 e la finestra di avanzamento M4;
- il motore di conversione, i formati e i motori esterni realmente cercati dal codice;
- l’integrazione con il Registro HKCU e la migrazione degli identificativi di proprietà;
- il ciclo di vita del processo dopo M6;
- i test presenti e i perimetri di collaudo già registrati.

Restano fuori da questo documento il Setup Manager di M7 e qualunque installer.

## 2. Stato dell’architettura nell’albero di lavoro

| Elemento | Stato nel codice |
|----------|------------------|
| Finestra di avanzamento M4 | Implementata (`MenuProgressWindow`). Titolo **FLIX — Conversione**. `ShutdownMode` è `OnMainWindowClose` |
| M5.1, menu PNG | Implementato (`--install-context-menu-png`) |
| M5.2, menu immagini | Implementato (`--install-context-menu-images`) |
| M5.3, menu PDF, TXT e dati | Implementato (`--install-context-menu-pdf-text-data`) |
| M5.4, menu audio e video | Implementato (`--install-context-menu-audio-video`): 8 estensioni audio, 7 estensioni video, 86 comandi di destinazione, installazione condizionata a FFmpeg |
| M5.5, menu documenti | Implementato (`--install-context-menu-documents`). Senza Pandoc il piano non viene scritto |
| Collaudo M5.4.6 e collaudo Esplora file M6 | PASS nei perimetri della sezione 9 |
| M7 | Non iniziata |
| Host, ruota, tray, gesture, avvio automatico, mutex e named pipe | Assenti dal processo. `Program.Main` non avvia un host |

I menu sono registrati da `Flix.exe`. La procedura di installazione finale prevista per M7 non esiste. Gli script di installazione ritirati non sono in questo albero: sono archiviati fuori dal repository e non installano FLIX.

## 3. Componenti principali e responsabilità

L’ingresso è `Program.Main` (`src/Flix/Program.cs`). I comandi di menu, anteprima, test e conversione vengono smistati ed eseguiti. Senza uno di quei comandi il processo registra l’uscita e termina. Non acquisisce un mutex e non apre una pipe.

| Componente | File | Responsabilità attuale |
|------------|------|------------------------|
| `Program` | `Program.cs` | Smistamento CLI. I titoli dei messaggi visibili sono FLIX |
| `MenuConversion` | `MenuConversion.cs` | `--convert-menu`: analisi ed esecuzione di un solo file e un solo formato |
| `MenuProgressWindow`, `MenuProgressModel`, `MenuProgressRouter` | `MenuProgressWindow.cs` | Finestra centrale M4 |
| `ContextMenuCatalog` | `ContextMenuCatalog.cs` | Voci di sottomenu a partire da `Policy.Outputs` |
| `ContextMenuNext` | `ContextMenuNext.cs` | Piani, installazione, migrazione e rimozione dei menu M5.1–M5.5 |
| `ContextMenuPreview` | `ContextMenuPreview.cs` | Prototipo PNG distinto dai menu definitivi. Testo **Converti con FLIX (Anteprima)**. Chiave e proprietario invariati |
| `Policy` | `Policy.cs` | Estensioni, destinazioni e accettazione dei file locali |
| `ConversionEngine` | `ConversionEngine.cs` | Esecuzione serializzata, cartella di lavoro, validazione, commit |
| `DocumentTools` | `DocumentTools.cs` | Pandoc e LibreOffice, quando i binari vengono trovati |
| `MediaTools`, `DataTools` | `MediaTools.cs` | FFmpeg per audio e video; conversioni dati senza motore esterno |
| `Evidence` | `Policy.cs` | Diario `events.jsonl` in `%LOCALAPPDATA%\Flix\logs` |

`ContextMenuCatalog.ForExtension` chiede le destinazioni a `Policy.Outputs` e le ordina con `Policy.OrderFormats`. Una voce espone `TargetFormat`, il titolo `Converti in {FORMAT}` e l’icona del formato. Nel Registro l’icona della destinazione è il file premium-v2 accanto a `Flix.exe`.

## 4. Flusso operativo dei menu contestuali

La voce padre registrata dai menu definitivi ha `MUIVerb` **Converti con FLIX** e l’icona `flix-xf.ico` accanto all’eseguibile. Il padre non contiene un comando che avvia `Flix.exe`. `ExtendedSubCommandsKey` è un valore stringa che punta all’albero `shell` dello stesso menu, oppure all’albero canonico nel caso degli alias. Ogni destinazione è un verbo figlio con la propria icona premium-v2, `MultiSelectModel=Single`, `NeverDefault` e comando:

```text
"percorso\Flix.exe" --convert-menu FORMATO --files "%1"
```

`FORMATO` è il formato di destinazione (`PNG`, `MP3`, `MKV`, …), non l’estensione del file selezionato. Il comando ammette un solo formato e un solo file.

`MenuConversion.Launch` analizza la richiesta e la esegue nel processo appena avviato.

1. `TryParse` rifiuta formati multipli, file multipli, argomenti extra e token non alfanumerici.
2. `TryPrepare` accetta solo un percorso locale completo. Rifiuta percorsi di rete e UNC. Il file deve superare `Policy.ReadFiles`. La destinazione deve comparire sia nel catalogo sia in `Policy.Outputs` per lo snapshot dei motori.
3. La finestra M4 mostra l’avanzamento. `ShutdownMode` è `OnMainWindowClose`: alla chiusura della finestra il processo termina. Non resta una tray.

Due processi avviati insieme non condividono un host. Il collaudo M6 ha verificato la concorrenza interprocesso nel perimetro registrato. Questo documento non definisce un nuovo protocollo di accodamento.

## 5. Motore di conversione, formati e dipendenze

`ConversionEngine.Convert` accetta da 1 a 128 file. Un `SemaphoreSlim` interno serializza le chiamate nello stesso processo. Per ogni coppia file-formato crea, accanto al sorgente, una cartella `.flix-job-` seguita da un GUID. Il risultato viene scritto in quella cartella e poi pubblicato con `File.Move` verso un nome nuovo `{nome}.converted.{estensione}`, con suffisso ` (n)` se il nome è occupato, senza sovrascrivere un file esistente. Le immagini e i PDF da rasterizzare sono letti in memoria dal sorgente. I dati sono letti come testo e scritti solo nel file di lavoro. FFmpeg riceve il percorso del sorgente come ingresso (`-i`) e scrive solo il file di lavoro. Le conversioni che passano da `DocumentTools` ricevono prima una copia nella cartella di lavoro: il commento nel motore vieta di passare a quegli strumenti il percorso originale. Il commit non rinomina né sostituisce il sorgente. Le pagine multiple usano il suffisso `.page-001` e seguenti. Se il commit di un formato fallisce a metà, i file nuovi di quel formato vengono cancellati. `DeleteJob` rimuove solo una cartella il cui nome rispetta il prefisso e il GUID e che sta nella cartella del sorgente; non segue i reparse point.

Limiti applicati dal motore: 100 MiB per i file che non sono audio o video, 2 GiB per audio e video, 40 megapixel per le immagini, 200 pagine per i PDF, 2 MiB per la lettura testo, 20 MiB per i dati, 4096 voci e 256 MiB espansi per DOCX, EPUB e ODT. `MenuConversion.FromResult` rifiuta un output il cui percorso coincide con il sorgente.

Le estensioni sorgente sono in `Policy`:

| Gruppo | Estensioni sorgente | Menu contestuale |
|--------|---------------------|------------------|
| Immagini | `.png` `.jpg` `.jpeg` `.webp` `.bmp` `.tif` `.tiff` `.gif` | M5.1 limita il comando al PNG. M5.2 registra tutte e otto |
| PDF, testo e dati | `.pdf` `.txt` `.json` `.yaml` `.yml` `.xml` `.csv` | M5.3 |
| Audio | `.mp3` `.wav` `.flac` `.ogg` `.m4a` `.aac` `.wma` `.opus` | M5.4, solo se FFmpeg è rilevato |
| Video | `.mp4` `.mkv` `.webm` `.avi` `.mov` `.wmv` `.m4v` | M5.4, solo se FFmpeg è rilevato |
| Documenti | `.md` `.markdown` `.html` `.htm` `.docx` `.odt` `.rtf` `.epub` `.rst` `.fb2` | M5.5. `.markdown` e `.htm` sono alias. `.txt` resta nel menu M5.3 |

La destinazione uguale al formato sorgente è esclusa (`jpeg` vale `JPG`, `tif` vale `TIFF`, `yml` vale `YAML`, `markdown` vale `MD`, `htm` vale `HTML`).

### Immagini, M5.1 e M5.2

Le destinazioni immagine sono `PNG`, `JPG`, `WEBP`, `PDF`, `BMP`, `TIFF`, `GIF`, meno il formato sorgente. `PlanImageMenus` richiede sei destinazioni per ogni estensione. Pandoc, LibreOffice e FFmpeg non modificano questo elenco.

`.jpeg` e `.tiff` non duplicano i comandi: il padre alias punta con `ExtendedSubCommandsKey` all’albero di `.jpg` e `.tif`. Gli identificativi sono `Flix.Convert.Png`, `Flix.Convert.Jpg`, `Flix.Convert.Webp`, `Flix.Convert.Bmp`, `Flix.Convert.Tif`, `Flix.Convert.Gif`. L’installazione e la rimozione riconoscono una foglia precedente solo tramite `LegacyIdentity`: il marcatore storico deve coincidere con l’identificativo atteso e la chiave non deve essere il menu originale protetto.

Il comando PNG resta distinto dal comando immagini. Le chiavi stanno sotto `HKCU\Software\Classes\SystemFileAssociations\<estensione>\shell\`.

La rasterizzazione usa SkiaSharp, con un ripiego sul decoder WPF. Il PDF di una immagine è prodotto nel motore. Le librerie referenziate in modo diretto dal progetto sono PDFtoImage 5.4.0 e PdfPig 0.1.16.

### PDF, TXT e dati, M5.3

Il comando è `--install-context-menu-pdf-text-data` / `--remove-context-menu-pdf-text-data`. `.yml` è un alias di `.yaml`.

Senza Pandoc il catalogo offre:

- da `.pdf`: `PNG`, `JPG`, `WEBP`, `TXT`;
- da `.txt`: `PDF`, `PNG`, `JPG`, `WEBP`;
- dai dati: `JSON`, `YAML`, `XML`, `CSV`, `TXT`, `HTML`, `MD`, `PDF`, escluso il formato sorgente.

Con Pandoc, `.pdf` aggiunge `DOCX`, `MD`, `HTML`, `EPUB`, `ODT`, `RTF`, `FB2`, `RST`. `.txt` aggiunge `DOCX`, `EPUB`, `MD`, `HTML`, `ODT`, `RTF`, `FB2`, `RST` e mantiene `PDF`, `PNG`, `JPG`, `WEBP`. I dati non dipendono da Pandoc. LibreOffice e FFmpeg non aggiungono voci a questo menu.

La rimozione costruisce il piano con Pandoc considerato presente, così le destinazioni opzionali rientrano nell’insieme da cancellare. Cancella solo chiavi il cui marcatore, `FlixOwner` oppure il marcatore storico isolato in `LegacyIdentity`, coincide con l’identificativo di questa applicazione.

Nel motore, le immagini da PDF passano da PDFtoImage (144 DPI, limite 200 pagine). Il testo da PDF passa da PdfPig; un PDF senza testo estraibile si ferma, e il messaggio indica PNG o JPG per le scansioni. `TXT` verso `PDF` usa `TextPdf`. Le altre conversioni documentali passano da `DocumentTools`. I dati passano da `DataTools`, senza motore esterno. Per FB2, Pandoc riceve un filtro Lua locale che trasforma testo e blocchi di codice in paragrafi.

### Audio e video, M5.4

Il comando è `--install-context-menu-audio-video` / `--remove-context-menu-audio-video`.

Otto estensioni audio e sette estensioni video producono quindici menu. Non ci sono alias: ogni estensione ha il proprio albero e `ExtendedSubCommandsKey` punta a quello stesso albero.

Le destinazioni, distinte dalle estensioni sorgente, sono:

- audio: `WAV`, `MP3`, `FLAC`, `OGG`, `M4A`, `AAC`;
- video: `MP4`, `MP3`, `WAV`, `MKV`, `WEBM`, `AVI`, `MOV`.

`WMA`, `OPUS`, `WMV` e `M4V` sono solo estensioni sorgente. Non compaiono tra i formati di destinazione e non hanno un’icona di destinazione. Le altre sorgenti escludono la destinazione omonima. `VerifyMediaPlans` controlla il totale: 42 comandi audio, 44 comandi video, 86 comandi. Senza FFmpeg, `Policy.Outputs` restituisce un elenco vuoto per queste estensioni.

L’installazione rifiuta il piano prima di scrivere se FFmpeg non è rilevato, se i menu non sono quindici o se manca un’icona premium. Pandoc e LibreOffice non aggiungono voci. La rimozione usa l’insieme massimo, con FFmpeg considerato presente, e non dipende dalle icone.

Le icone richieste in installazione sono quelle dei formati destinazione: `MP3`, `WAV`, `FLAC`, `OGG`, `M4A`, `AAC`, `MP4`, `MKV`, `WEBM`, `AVI`, `MOV`. Il nome file è `flix-menu-<formato>-premium-v2.ico`, nella cartella di `Flix.exe`. Il menu padre usa in più `flix-xf.ico`.

`MediaTools.Convert` invoca FFmpeg sul file sorgente verso lo staged del job, con timeout di 10 minuti.

### Motori esterni

Il codice cerca i binari. Non li dichiara come contenuto del pacchetto in `Flix.csproj` e questo documento non attribuisce loro una licenza.

| Motore | Ruolo nel codice attuale | Ricerca implementata |
|--------|--------------------------|----------------------|
| FFmpeg | Obbligatorio per installare i menu audio e video e per eseguire quelle conversioni. Assente: il piano M5.4 è vuoto e l’installazione non scrive chiavi | `tools\ffmpeg.exe` accanto all’eseguibile; `tools\ffmpeg\ffmpeg.exe` risalendo fino a `Flix.csproj`; `PATH`; `%LOCALAPPDATA%\Flix\tools\`; `%LOCALAPPDATA%\Programs\Flix\tools\`; poi le cartelle precedenti dello stesso nome storico, solo per trovare un binario già presente; collegamento e pacchetti WinGet; `ffmpeg\bin\ffmpeg.exe` in Program Files |
| Pandoc | Opzionale. Necessario alle conversioni documentali che passano da `DocumentTools.Convert`. Avviato con `--sandbox` e timeout di 120 secondi | `tools\pandoc.exe` accanto all’eseguibile; cartella `tools\pandoc\pandoc-3.12.1\pandoc.exe` individuata risalendo fino a `Flix.csproj`. Il numero è il percorso cercato dal sorgente, non una versione distribuita con FLIX |
| LibreOffice | Opzionale. Serve per il PDF dei documenti che passano da `OfficePdf` (`soffice.com`, profilo temporaneo `Flix-LO-`, macro a livello 3). La pulizia del profilo sta nel `finally`; se fallisce viene solo registrata nel diario. `TXT` verso `PDF` e le immagini non lo usano | `LibreOffice\program\soffice.com` in Program Files o Program Files (x86) |

Le conversioni immagine, il testo nativo, i dati e le pagine PDF restano nel processo FLIX quando il percorso di `Policy` non chiede un motore esterno. L’assenza di un motore opzionale toglie le destinazioni che dipendono da quel motore; non è modellata come un requisito di tutte le conversioni.

## 6. Integrazione Windows e Registro

Le associazioni dei menu M5 sono chiavi per utente in `HKCU\Software\Classes\SystemFileAssociations`. `CurrentUserRegistryStore` e lo store del prototipo aprono `Registry.CurrentUser` e scrivono valori stringa. Non c’è un accesso a HKLM nel percorso dei menu M5.

Ogni chiave nuova del piano porta `FlixOwner` con l’identificativo del menu (`Flix.Convert.Png`, `Flix.Convert.Mp3`, e gli analoghi costruiti da `MenuIdFor`, `ContentMenuIdFor`, `MediaMenuIdFor`, `DocumentMenuIdFor`). La provenienza dei padri documentali nuovi è `FlixCreatedAncestors`. L’installazione, prima di scrivere, rifiuta una chiave già presente con un altro proprietario e rifiuta le chiavi protette: il menu originale `Software\Classes\*\shell\Flip.Convert` e un’anteprima di un altro proprietario. Il messaggio di rifiuto dichiara la chiave preservata. Dopo una scrittura riuscita il marcatore storico della stessa chiave viene ritirato.

Prima della mutazione, `ContextMenuRegistryBackup.Capture` memorizza le chiavi del piano. Se la scrittura fallisce, `RestoreBackup` tenta di eliminare le chiavi che prima non esistevano, solo se il proprietario coincide e non restano sottochiavi, poi riscrive i valori delle chiavi che esistevano. `ImageBackupsMatch` confronta il risultato con lo snapshot. Se il ripristino fallisce o non coincide, l’eccezione dichiara che il Registro può essere rimasto parziale e che l’operazione non è atomica. HKCU non offre una transazione: il ripristino è un tentativo best-effort, non una garanzia.

La rimozione percorre le chiavi dalla più profonda. Cancella una chiave solo se esiste, se il marcatore `FlixOwner` o il marcatore storico isolato in `LegacyIdentity` è quello del piano e se non ha sottochiavi. Una chiave altrui viene lasciata. Una chiave che conserva sottochiavi viene saltata. Il codice di uscita 0 di `ApplyImages`, `ApplyContent` e `ApplyMedia` indica che la procedura richiesta è terminata senza un’eccezione di Registro. Da solo non dimostra che tutte le chiavi del piano siano assenti.

I comandi immagini, PDF/TXT/dati, audio/video e documenti rifiutano l’installazione insieme alla rimozione e rifiutano qualunque argomento aggiuntivo, senza scrivere. Accettano `Flix.exe` se la cartella è `%LOCALAPPDATA%\Programs\Flix`, se è il layout precedente `%LOCALAPPDATA%\Programs\Flip`, oppure se il percorso contiene il segmento `Flip-Lab`. Rifiutano `bin`, `obj`, una build temporanea non autorizzata e `dotnet Flix.dll`. Le icone delle destinazioni devono stare accanto a quell’eseguibile, essere ICO con i riquadri 16, 32, 48, 64, 128 e 256, e avere contrasto sui riquadri 16 e 32 (`ContextMenuPreview.IconIsValid`). L’icona del padre, `flix-xf.ico`, aggiunge il riquadro 24 e il contrasto a 16 e 24. Se un’icona manca, l’installazione termina prima della scrittura. Il segno bianco è tenue su sfondo chiaro; il file non è stato ricolorato.

Il comando PNG M5.1 usa `ValidateRegistrableExecutable` e gli stessi controlli sulle icone premium, senza il vincolo aggiuntivo del percorso stabile o `Flip-Lab` imposto ai comandi M5.2, M5.3 e M5.4.

Queste registrazioni sono il meccanismo sperimentale M5. Il Setup Manager di M7, con attivazione, riparazione e disinstallazione del prodotto, non è nel codice.

Due registrazioni più vecchie restano distinte dai menu definitivi e non vengono cancellate dalla migrazione:

- la chiave storica `Software\Classes\*\shell\Flip.Convert`, se presente, è protetta. Il codice attuale non la riscrive e non la elimina;
- `ContextMenuPreview`, con `--install-context-menu-preview` e `--remove-context-menu-preview`, registra solo sul PNG l’anteprima `Flix.Convert.Preview`, verbo **Converti con FLIX (Anteprima)**, proprietario `FlixPrototype`, destinazioni `JPG`, `PDF` e `WEBP`. Questa chiave non riceve `flix-xf.ico`. La chiave storica omonima viene rimossa solo se il suo marcatore coincide con l’identificativo storico di questa applicazione.

L’applicazione in esecuzione non registra un valore di avvio con Windows. Gli script di installazione ritirati non sono in questo albero e non registrano l’avvio. Il pacchetto storico 0.5.1, eseguito dal suo ZIP, è un prodotto diverso.

## 7. Interfaccia M4 e ciclo di vita attuale

`MenuProgressWindow` è una finestra senza bordo, centrata, titolo `FLIX — Conversione`, scheda 440×210. `ShowLive` la apre come finestra principale. `--preview-menu-progress` apre la stessa finestra in anteprima e non converte.

`MenuProgressModel.View` segue la richiesta: indeterminato, percentuale misurata, successo, errore, annullamento, occupato. La percentuale mostrata durante il lavoro sta tra 1 e 99; il successo mostra 100%. Il pulsante Annulla cancella il `CancellationTokenSource` del job. In errore il dettaglio predefinito è che il file originale non è stato modificato. Se una conversione è già in corso nello stesso processo, la nuova richiesta non parte. Al successo il pulsante può aprire la cartella del risultato e un timer di circa 3,2 secondi chiude la finestra, rinviando la chiusura se il puntatore è sopra la scheda. `MenuProgressRouter.UsesBalloon` è falso. `OpensWheel` è falso. Non esiste una tray.

Il ciclo attuale, quando la voce di menu avvia FLIX, è:

1. nasce il processo per quella richiesta;
2. la finestra M4 esegue `ConversionEngine` per quel file e quel formato;
3. la finestra si chiude al termine o su comando dell’utente;
4. il processo termina con la finestra. Non resta una tray.

Il diario diagnostico sta in `%LOCALAPPDATA%\Flix\logs\events.jsonl`, con rotazione intorno a 2 MiB più un predecessore. Può contenere percorsi. `--evidence` sposta la cartella per i test. La cartella precedente `%LOCALAPPDATA%\Flip\logs` non viene eliminata.

## 8. Componenti legacy

Ruota, suono, gesture, pannello multi-file, tray, avvio automatico, host, mutex e pipe non sono nel processo. I file che li implementavano non sono più in `src/Flix`. Resta la chiave protetta `*\shell\Flip.Convert`. Gli script di installazione ritirati sono archiviati fuori dal repository: non copiano file, non attivano la tray e non cancellano dati. Non sono il Setup Manager e non installano FLIX.

## 9. Test e limiti delle verifiche

I test automatizzati non sostituiscono un collaudo di Registro o di Esplora file.

| Verifica | Ingresso | Cosa copre | Limite |
|----------|----------|------------|--------|
| Motore generale | `--test-conversions` → `ConversionTests.Run` | Fixture locali del motore, hash dei sorgenti, presenza di Pandoc e LibreOffice | Non tocca il Registro dei menu né Esplora file. LibreOffice assente segna i controlli dipendenti come non eseguiti. Pandoc assente fa fallire le conversioni documentali che lo richiedono |
| Controlli in memoria | `--test-input` → `InputTests.Run` | Gesto, policy, catalogo, piani menu, store `IRegistryStore` in memoria, inclusi store che falliscono dopo una scrittura o una cancellazione | Il Registro di Windows non viene modificato da questi store |
| Matrice immagini | `--test-input --image-matrix-only` → `ImageMatrixTests` | `ConversionEngine` sulla matrice M5.2 | Il commento del file esclude launcher, mutex di sessione, named pipe e Registro |
| Matrice PDF, TXT e dati | `--test-input --content-matrix-only` → `ContentMatrixTests` | `ConversionEngine` sulla matrice M5.3.1, con celle opzionali segnate non eseguibili se Pandoc manca | Esclude launcher, mutex, pipe, Registro e menu |
| Matrice audio e video | `--test-input --media-matrix-only` → `MediaMatrixTests` | `ConversionEngine` sulla matrice M5.4.1. Senza FFmpeg esegue i negativi di catalogo | Esclude ruota, tray, mutex, pipe, Registro e menu contestuali |

Il collaudo reale M5.4.6 ha ottenuto **PASS** nell’account isolato FlipLab, nel perimetro già registrato in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md):

- installazione dei 15 menu audio e video;
- registrazione degli 86 comandi di destinazione;
- verifica di WAV e MP4 in Esplora file;
- conversioni MP4→MKV, MP4→MP3 e WAV→MP3;
- integrità dei sorgenti;
- validazione e decodifica degli output;
- rimozione dei 15 menu;
- assenza delle chiavi dopo la rimozione (verifica 0/15);
- verifica grafica dopo la rimozione;
- audit finale.

Quel collaudo ha constatato i 15 menu e gli 86 comandi. Non ha eseguito manualmente tutte le 86 conversioni.

Il collaudo Esplora file di M6, nello stesso tipo di account isolato, ha dato PASS su cinque conversioni, sull’assenza di host residente e di tray, sull’annullamento M4, sulla concorrenza interprocesso e sulla rimozione dei menu. Non ha eseguito a mano ogni destinazione.

La build isolata di questa identità ha superato 175 controlli `--test-input`, compreso `flix_registry_migration_simulated`, e 107 controlli `--test-conversions`. Otto controlli dipendenti da LibreOffice non sono stati eseguiti. Gli store di quei test sono in memoria. HKCU dell’account di sviluppo non è stato modificato. Il controllo visivo dell’icona XF in FlipLab non è stato eseguito. Quella build e la precedente non sono più nell’albero: stanno nell’archivio esterno `FLIX-AUDIT-ARCHIVE-20261124`, nelle cartelle `_local/flix-identity-20261124` e `_local/flix-rebrand-20261124`. I risultati restano attribuiti a quelle build.

## 10. Cosa resta fuori da questa applicazione

L’applicazione in esecuzione fa questo percorso:

1. selezione del file;
2. **Converti con FLIX**;
3. scelta del formato di destinazione;
4. conversione;
5. finestra di avanzamento M4;
6. risultato e termine del processo.

Restano fuori: il Setup Manager, il pacchetto installabile, la firma, la pubblicazione e il controllo visivo FlipLab dell’icona XF. Il repository remoto si chiama ancora FLIP. Due conversioni contemporanee sono due processi. Questo documento non aggiunge una coda condivisa.

## 11. Fasi

| Fase | Contenuto | Stato |
|------|-----------|--------|
| M5.5 | Menu contestuali dei documenti, con i motori disponibili | Implementata |
| M5.6 | Collaudo funzionale | Il collaudo M6 copre cinque conversioni, annullamento, concorrenza e rimozione. Non ogni destinazione a mano |
| M6 | Rimozione di ruota, tray, gesture, suoni, avvio automatico, host, mutex e IPC | Fatta nel processo |
| Rebranding | `Flix.exe`, **Converti con FLIX**, `flix-xf.ico` sul menu padre | Implementato. Controllo visivo FlipLab non eseguito |
| FLIX-AUDIT | Inventario dell'albero di lavoro | Completata il 24 novembre 2026. Nessun commit |
| FLIX-CLEAN | Rimozione degli output e archivio degli storici non necessari | Completata il 24 novembre 2026. Il laboratorio M552 locale è rimasto |
| FLIX-IDENTITY | Identità operativa FLIX | Verificata. I letterali precedenti restano in `LegacyIdentity` e nei riferimenti storici |
| FLIX-REPOSITORY | Rinomina del repository e della cartella | Non eseguita |
| FLIX-FINAL-VALIDATION | Build isolata e suite | Eseguita. InputTests 175/175. ConversionTests 64 PASS, 22 FAIL senza Pandoc, 6 NON ESEGUITI senza LibreOffice |
| FLIX-CLEAN-FINALIZE | Hash completi del backup, archivio dei documenti e degli script ritirati, validazione con Pandoc già presente | InputTests 175/175 da `Flix.exe`. ConversionTests 107 PASS, 0 FAIL, 8 NON ESEGUITI. Nessun commit |
| M7 | Setup Manager e pacchetto installabile | Non iniziata |

Le icone delle destinazioni sono `flix-menu-*-premium-v2.ico`. L’icona del padre è il monogramma XF bianco approvato.

## 12. Riferimenti ai documenti ufficiali

| Documento | Uso |
|-----------|-----|
| [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md) | Direzione vincolante di FLIX, stato del branch, legacy, requisiti approvati, perimetro del collaudo M5.4.6 |
| [ROADMAP.md](ROADMAP.md) | Sequenza M5, M6 e M7 e distinzione dal contenuto storico |
| [TECHNICAL_DECISIONS.md](TECHNICAL_DECISIONS.md) | Decisioni di piattaforma e sezione «Definitive product architecture (approved)» |
| `src/Flix` | Comportamento effettivo descritto in questo file |

Se il codice e un documento di direzione divergono, lo stato implementato è quello verificabile in `src/Flix` nell’albero di lavoro. La distribuzione resta M7.
