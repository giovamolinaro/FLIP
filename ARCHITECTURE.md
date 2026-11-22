# Architettura tecnica attuale di FLIP

Questo documento descrive l’architettura **implementata** nel branch `sviluppo-personalizzato`, al commit `e6cfcf049277a30f93f8842a5f5769f0af01d697`, dopo il consolidamento di M5.4.

**FLIP** è il nome del software, dell’assembly, dell’eseguibile e dei comandi presenti nel codice. **FLIX** è il nome pianificato del prodotto finale. FLIX non è implementato per intero e non è un prodotto distribuito.

Il documento di direzione del prodotto finale è [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md). La sequenza delle fasi è in [ROADMAP.md](ROADMAP.md). Le decisioni approvate sono in [TECHNICAL_DECISIONS.md](TECHNICAL_DECISIONS.md). Questo file non aggiunge requisiti e non anticipa M5.5, M5.6, M6 o M7.

Il progetto è un eseguibile Windows x64, `net10.0-windows`, WPF e Windows Forms (`src/Flip/Flip.csproj`). L’assembly si chiama `Flip`. Il valore `Version` nel progetto è `0.5.1`: è il timbro di versione del codice di questo fork, allineato alla base storica documentata FLIP 0.5.1. Non è la release upstream FLIP v0.6.1 AURA Edition del repository `thetinyfoundry/FLIP`, e non è una GitHub Release di questo branch. Vedi [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

## 1. Scopo e perimetro

Il perimetro è il codice in `src/Flip` allo stato del commit indicato.

Sono descritti:

- il punto di ingresso e l’instradamento dei comandi;
- i menu contestuali M5.1–M5.4 e la finestra di avanzamento M4;
- il motore di conversione, i formati e i motori esterni realmente cercati dal codice;
- l’integrazione con il Registro HKCU;
- i componenti legacy ancora compilati e avviati insieme all’host;
- i test presenti e il perimetro del collaudo M5.4.6 già registrato.

Restano fuori da questo documento il disegno di nuovi meccanismi di concorrenza, un sostituto dell’IPC e il FLIP Setup Manager.

## 2. Stato dell’architettura al commit M5.4

| Elemento | Stato nel codice |
|----------|------------------|
| Finestra di avanzamento M4 | Implementata (`MenuProgressWindow`) |
| M5.1, menu PNG | Implementato (`--install-context-menu-png`) |
| M5.2, menu immagini | Implementato (`--install-context-menu-images`) |
| M5.3, menu PDF, TXT e dati | Implementato (`--install-context-menu-pdf-text-data`) |
| M5.4, menu audio e video | Implementato (`--install-context-menu-audio-video`): 8 estensioni audio, 7 estensioni video, 86 comandi di destinazione, installazione condizionata a FFmpeg |
| Collaudo isolato M5.4.6 | PASS nel perimetro della sezione 9. Non chiude M5.6 |
| M5.5, menu documenti | Non implementata. `Policy.Documents` è nota al motore; quelle estensioni non hanno un menu contestuale |
| M5.6, M6, M7 | Non iniziate |
| Host, ruota, tray, gesture, avvio automatico, mutex e named pipe | Ancora presenti e raggiunti da `Program.Main` |

I menu M5 sono registrazioni sperimentali lanciate da `Flip.exe`. La procedura di installazione finale prevista per M7 non esiste in questo branch.

## 3. Componenti principali e responsabilità

L’ingresso è `Program.Main` (`src/Flip/Program.cs`). I comandi di menu, anteprima, test e conversione vengono smistati prima dell’avvio dell’host. In assenza di quei comandi, `Main` acquisisce il mutex `Local\Flip.Desktop.v2` e chiama `Program.RunApplication`.

| Componente | File | Responsabilità attuale |
|------------|------|------------------------|
| `Program` | `Program.cs` | Smistamento CLI, mutex di istanza, pipe legacy `Flip.Local.Flip.v2` |
| `Host` | `Program.cs` | Finestra nascosta, tray, gesture, ruota, pannello, pipe di menu, ciclo M4 |
| `Ipc` | `Program.cs` | Classificazione dei messaggi sulla pipe legacy |
| `MenuConversion` | `MenuConversion.cs` | `--convert-menu`: analisi, inoltro, esecuzione di un solo file e un solo formato |
| `MenuProgressWindow`, `MenuProgressModel`, `MenuProgressRouter` | `MenuProgressWindow.cs` | Finestra centrale M4 |
| `ContextMenuCatalog` | `ContextMenuCatalog.cs` | Voci di sottomenu a partire da `Policy.Outputs` |
| `ContextMenuNext` | `ContextMenuNext.cs` | Piani, installazione e rimozione dei menu M5.1–M5.4 |
| `ContextMenuPreview` | `ContextMenuPreview.cs` | Prototipo PNG distinto dai menu M5 |
| `ShellIntegration` | `ShellIntegration.cs` | Voce legacy su tutte le estensioni |
| `Policy` | `Policy.cs` | Estensioni, destinazioni e accettazione dei file locali |
| `ConversionEngine` | `ConversionEngine.cs` | Esecuzione serializzata, cartella di lavoro, validazione, commit |
| `DocumentTools` | `DocumentTools.cs` | Pandoc e LibreOffice, quando i binari vengono trovati |
| `MediaTools`, `DataTools` | `MediaTools.cs` | FFmpeg per audio e video; conversioni dati senza motore esterno |
| `Wheel`, `WheelSound`, `WheelShortcut`, `ShortcutHook` | file omonimi | Ruota, suono, gesto e hook ancora collegati all’host |
| `SelectionWindow` | `SelectionWindow.cs` | Flusso legacy multi-file e multi-formato |
| `StartupIntegration` | `StartupIntegration.cs` | Valore HKCU Run e collegamenti Desktop/Start |
| `AppPreferences` | `AppPreferences.cs` | `settings.json` per utente |
| `Evidence` | `Policy.cs` | Diario `events.jsonl` |

`ContextMenuCatalog.ForExtension` chiede le destinazioni a `Policy.Outputs` e le ordina con `Wheel.OrderFormats`. Una voce espone `TargetFormat`, il titolo `Converti in {FORMAT}` e l’icona `format.{formato}`.

## 4. Flusso operativo dei menu contestuali

La voce padre registrata dai menu M5 ha `MUIVerb` **Converti con Flip**. Il padre non contiene un comando che avvia `Flip.exe`. `ExtendedSubCommandsKey` è un valore stringa che punta all’albero `shell` dello stesso menu, oppure all’albero canonico nel caso degli alias. Ogni destinazione è un verbo figlio con icona, `MultiSelectModel=Single`, `NeverDefault` e comando:

```text
"percorso\Flip.exe" --convert-menu FORMATO --files "%1"
```

`FORMATO` è il formato di destinazione (`PNG`, `MP3`, `MKV`, …), non l’estensione del file selezionato. Il comando ammette un solo formato e un solo file.

`MenuConversion.Launch` segue questo percorso:

1. `TryParse` rifiuta formati multipli, file multipli, argomenti extra e token non alfanumerici.
2. `TryPrepare` accetta solo un percorso locale completo. Rifiuta percorsi di rete e UNC. Il file deve superare `Policy.ReadFiles`. La destinazione deve comparire sia nel catalogo sia in `Policy.Outputs` per lo snapshot dei motori.
3. Se un altro processo tiene il mutex `Local\Flip.MenuConvert.v1`, la richiesta viene inoltrata sulla pipe `Flip.Local.Flip.v2.menu` (timeout di connessione 1500 ms). Il protocollo è una riga JSON `WireRequest` versione 1 e una riga `WireReply` versione 1.
4. Se quel mutex non è tenuto, `LiveMenuEndpoint.TryBecomeHost` tenta il mutex `Local\Flip.Desktop.v2` e, se lo ottiene, avvia `RunApplication`. L’host esegue la richiesta con cui è stato lanciato.
5. Se il mutex desktop è già tenuto da un processo che non espone il mutex di menu, la richiesta non parte. Il messaggio è quello di `MenuConversion.OldInstanceMessage`.

Dentro l’host, `Initialize` avvia `Listen` sulla pipe legacy e `MenuConversion.ServeAsync` sulla pipe di menu (al più 4 istanze, `CurrentUserOnly`). `RunMenu` passa da `MenuConversion.RunExclusive`: una conversione alla volta sul flag `busy` dell’host. La percentuale arriva da `ConversionEngine` attraverso `MenuProgressGate`.

La finestra M4 mostra l’avanzamento. Alla chiusura della finestra l’host non termina: `ShutdownMode` è `OnExplicitShutdown` e l’icona di tray resta attiva. Questo è il ciclo di vita attuale. Il ciclo effimero approvato per il prodotto finale appartiene a M6 ed è descritto nella sezione 10.

## 5. Motore di conversione, formati e dipendenze

`ConversionEngine.Convert` accetta da 1 a 128 file. Un `SemaphoreSlim` interno serializza le chiamate nello stesso processo. Per ogni coppia file-formato crea, accanto al sorgente, una cartella `.flip-job-` seguita da un GUID. Il risultato viene scritto in quella cartella e poi pubblicato con `File.Move` verso un nome nuovo `{nome}.converted.{estensione}`, con suffisso ` (n)` se il nome è occupato, senza sovrascrivere un file esistente. Le immagini e i PDF da rasterizzare sono letti in memoria dal sorgente. I dati sono letti come testo e scritti solo nel file di lavoro. FFmpeg riceve il percorso del sorgente come ingresso (`-i`) e scrive solo il file di lavoro. Le conversioni che passano da `DocumentTools` ricevono prima una copia nella cartella di lavoro: il commento nel motore vieta di passare a quegli strumenti il percorso originale. Il commit non rinomina né sostituisce il sorgente. Le pagine multiple usano il suffisso `.page-001` e seguenti. Se il commit di un formato fallisce a metà, i file nuovi di quel formato vengono cancellati. `DeleteJob` rimuove solo una cartella il cui nome rispetta il prefisso e il GUID e che sta nella cartella del sorgente; non segue i reparse point.

Limiti applicati dal motore: 100 MiB per i file che non sono audio o video, 2 GiB per audio e video, 40 megapixel per le immagini, 200 pagine per i PDF, 2 MiB per la lettura testo, 20 MiB per i dati, 4096 voci e 256 MiB espansi per DOCX, EPUB e ODT. `MenuConversion.FromResult` rifiuta un output il cui percorso coincide con il sorgente.

Le estensioni sorgente sono in `Policy`:

| Gruppo | Estensioni sorgente | Menu contestuale |
|--------|---------------------|------------------|
| Immagini | `.png` `.jpg` `.jpeg` `.webp` `.bmp` `.tif` `.tiff` `.gif` | M5.1 limita il comando al PNG. M5.2 registra tutte e otto |
| PDF, testo e dati | `.pdf` `.txt` `.json` `.yaml` `.yml` `.xml` `.csv` | M5.3 |
| Audio | `.mp3` `.wav` `.flac` `.ogg` `.m4a` `.aac` `.wma` `.opus` | M5.4, solo se FFmpeg è rilevato |
| Video | `.mp4` `.mkv` `.webm` `.avi` `.mov` `.wmv` `.m4v` | M5.4, solo se FFmpeg è rilevato |
| Documenti | `.md` `.markdown` `.html` `.htm` `.docx` `.odt` `.rtf` `.epub` `.rst` `.fb2` | Nessuna registrazione. Resta il lavoro di M5.5. `.txt` è già nel menu M5.3 |

La destinazione uguale al formato sorgente è esclusa (`jpeg` vale `JPG`, `tif` vale `TIFF`, `yml` vale `YAML`, `markdown` vale `MD`, `htm` vale `HTML`).

### Immagini, M5.1 e M5.2

Le destinazioni immagine sono `PNG`, `JPG`, `WEBP`, `PDF`, `BMP`, `TIFF`, `GIF`, meno il formato sorgente. `PlanImageMenus` richiede sei destinazioni per ogni estensione. Pandoc, LibreOffice e FFmpeg non modificano questo elenco.

`.jpeg` e `.tiff` non duplicano i comandi: il padre alias punta con `ExtendedSubCommandsKey` all’albero di `.jpg` e `.tif`. Gli identificativi sono `Flip.Convert.Png`, `Flip.Convert.Jpg`, `Flip.Convert.Webp`, `Flip.Convert.Bmp`, `Flip.Convert.Tif`, `Flip.Convert.Gif`.

Il comando PNG resta distinto dal comando immagini. Le chiavi stanno sotto `HKCU\Software\Classes\SystemFileAssociations\<estensione>\shell\`.

La rasterizzazione usa SkiaSharp, con un ripiego sul decoder WPF. Il PDF di una immagine è prodotto nel motore. Le librerie referenziate in modo diretto dal progetto sono PDFtoImage 5.4.0 e PdfPig 0.1.16.

### PDF, TXT e dati, M5.3

Il comando è `--install-context-menu-pdf-text-data` / `--remove-context-menu-pdf-text-data`. `.yml` è un alias di `.yaml`.

Senza Pandoc il catalogo offre:

- da `.pdf`: `PNG`, `JPG`, `WEBP`, `TXT`;
- da `.txt`: `PDF`, `PNG`, `JPG`, `WEBP`;
- dai dati: `JSON`, `YAML`, `XML`, `CSV`, `TXT`, `HTML`, `MD`, `PDF`, escluso il formato sorgente.

Con Pandoc, `.pdf` aggiunge `DOCX`, `MD`, `HTML`, `EPUB`, `ODT`, `RTF`, `FB2`, `RST`. `.txt` aggiunge `DOCX`, `EPUB`, `MD`, `HTML`, `ODT`, `RTF`, `FB2`, `RST` e mantiene `PDF`, `PNG`, `JPG`, `WEBP`. I dati non dipendono da Pandoc. LibreOffice e FFmpeg non aggiungono voci a questo menu.

La rimozione costruisce il piano con Pandoc considerato presente, così le destinazioni opzionali rientrano nell’insieme da cancellare. Cancella solo chiavi con `FlipOwner` di FLIP.

Nel motore, le immagini da PDF passano da PDFtoImage (144 DPI, limite 200 pagine). Il testo da PDF passa da PdfPig; un PDF senza testo estraibile si ferma, e il messaggio indica PNG o JPG per le scansioni. `TXT` verso `PDF` usa `TextPdf`. Le altre conversioni documentali passano da `DocumentTools`. I dati passano da `DataTools`, senza motore esterno. Per FB2, Pandoc riceve un filtro Lua locale che trasforma testo e blocchi di codice in paragrafi.

### Audio e video, M5.4

Il comando è `--install-context-menu-audio-video` / `--remove-context-menu-audio-video`.

Otto estensioni audio e sette estensioni video producono quindici menu. Non ci sono alias: ogni estensione ha il proprio albero e `ExtendedSubCommandsKey` punta a quello stesso albero.

Le destinazioni, distinte dalle estensioni sorgente, sono:

- audio: `WAV`, `MP3`, `FLAC`, `OGG`, `M4A`, `AAC`;
- video: `MP4`, `MP3`, `WAV`, `MKV`, `WEBM`, `AVI`, `MOV`.

`WMA`, `OPUS`, `WMV` e `M4V` sono solo estensioni sorgente. Non compaiono tra i formati di destinazione e non hanno un’icona di destinazione. Le altre sorgenti escludono la destinazione omonima. `VerifyMediaPlans` controlla il totale: 42 comandi audio, 44 comandi video, 86 comandi. Senza FFmpeg, `Policy.Outputs` restituisce un elenco vuoto per queste estensioni.

L’installazione rifiuta il piano prima di scrivere se FFmpeg non è rilevato, se i menu non sono quindici o se manca un’icona premium. Pandoc e LibreOffice non aggiungono voci. La rimozione usa l’insieme massimo, con FFmpeg considerato presente, e non dipende dalle icone.

Le icone richieste in installazione sono quelle dei formati destinazione: `MP3`, `WAV`, `FLAC`, `OGG`, `M4A`, `AAC`, `MP4`, `MKV`, `WEBM`, `AVI`, `MOV`. Il nome file è `flip-menu-<formato>-premium-v2.ico`, nella cartella di `Flip.exe`.

`MediaTools.Convert` invoca FFmpeg sul file sorgente verso lo staged del job, con timeout di 10 minuti.

### Motori esterni

Il codice cerca i binari. Non li dichiara come contenuto del pacchetto in `Flip.csproj` e questo documento non attribuisce loro una licenza.

| Motore | Ruolo nel codice attuale | Ricerca implementata |
|--------|--------------------------|----------------------|
| FFmpeg | Obbligatorio per installare i menu audio e video e per eseguire quelle conversioni. Assente: il piano M5.4 è vuoto e l’installazione non scrive chiavi | `tools\ffmpeg.exe` accanto all’eseguibile; `tools\ffmpeg\ffmpeg.exe` risalendo fino a `Flip.csproj`; `PATH`; `%LOCALAPPDATA%\Flip\tools\`; `%LOCALAPPDATA%\Programs\Flip\tools\`; collegamento e pacchetti WinGet; `ffmpeg\bin\ffmpeg.exe` in Program Files |
| Pandoc | Opzionale. Necessario alle conversioni documentali che passano da `DocumentTools.Convert`. Avviato con `--sandbox` e timeout di 120 secondi | `tools\pandoc.exe` accanto all’eseguibile; cartella `tools\pandoc\pandoc-3.12.1\pandoc.exe` individuata risalendo fino a `Flip.csproj`. Il numero è il percorso cercato dal sorgente, non una versione distribuita con FLIP |
| LibreOffice | Opzionale. Serve per il PDF dei documenti che passano da `OfficePdf` (`soffice.com`, profilo temporaneo `Flip-LO-`, macro a livello 3). La pulizia del profilo sta nel `finally`; se fallisce viene solo registrata nel diario. `TXT` verso `PDF` e le immagini non lo usano | `LibreOffice\program\soffice.com` in Program Files o Program Files (x86) |

Le conversioni immagine, il testo nativo, i dati e le pagine PDF restano nel processo FLIP quando il percorso di `Policy` non chiede un motore esterno. L’assenza di un motore opzionale toglie le destinazioni che dipendono da quel motore; non è modellata come un requisito di tutte le conversioni.

## 6. Integrazione Windows e Registro

Le associazioni dei menu M5 sono chiavi per utente in `HKCU\Software\Classes\SystemFileAssociations`. `CurrentUserRegistryStore` e lo store del prototipo aprono `Registry.CurrentUser` e scrivono valori stringa. Non c’è un accesso a HKLM nel percorso dei menu M5.

Ogni chiave del piano porta `FlipOwner` con l’identificativo del menu (`Flip.Convert.Png`, `Flip.Convert.Mp3`, e gli analoghi costruiti da `MenuIdFor`, `ContentMenuIdFor`, `MediaMenuIdFor`). L’installazione, prima di scrivere, rifiuta una chiave già presente con un altro proprietario e rifiuta le chiavi protette: il menu legacy `Software\Classes\*\shell\Flip.Convert`, il prototipo `Flip.Convert.Preview`, e i menu di un’altra famiglia già coperti da un piano diverso. Il messaggio di rifiuto dichiara la chiave preservata.

Prima della mutazione, `ContextMenuRegistryBackup.Capture` memorizza le chiavi del piano. Se la scrittura fallisce, `RestoreBackup` tenta di eliminare le chiavi che prima non esistevano, solo se il proprietario coincide e non restano sottochiavi, poi riscrive i valori delle chiavi che esistevano. `ImageBackupsMatch` confronta il risultato con lo snapshot. Se il ripristino fallisce o non coincide, l’eccezione dichiara che il Registro può essere rimasto parziale e che l’operazione non è atomica. HKCU non offre una transazione: il ripristino è un tentativo best-effort, non una garanzia.

La rimozione percorre le chiavi dalla più profonda. Cancella una chiave solo se esiste, se `FlipOwner` è quello del piano e se non ha sottochiavi. Una chiave altrui viene lasciata. Una chiave che conserva sottochiavi viene saltata. Il codice di uscita 0 di `ApplyImages`, `ApplyContent` e `ApplyMedia` indica che la procedura richiesta è terminata senza un’eccezione di Registro. Da solo non dimostra che tutte le chiavi del piano siano assenti.

I comandi immagini, PDF/TXT/dati e audio/video rifiutano l’installazione insieme alla rimozione e rifiutano qualunque argomento aggiuntivo, senza scrivere. Accettano `Flip.exe` solo se la cartella è `%LOCALAPPDATA%\Programs\Flip` oppure se il percorso contiene il segmento `Flip-Lab`. Rifiutano `bin`, `obj`, una build temporanea non autorizzata e `dotnet Flip.dll`. Le icone devono stare accanto a quell’eseguibile, essere ICO con i riquadri 16, 32, 48, 64, 128 e 256, e avere contrasto sui riquadri 16 e 32 (`ContextMenuPreview.IconIsValid`). Se un’icona manca, l’installazione termina prima della scrittura.

Il comando PNG M5.1 usa `ValidateRegistrableExecutable` e gli stessi controlli sulle icone premium, senza il vincolo aggiuntivo del percorso stabile o `Flip-Lab` imposto ai comandi M5.2, M5.3 e M5.4.

Queste registrazioni sono il meccanismo sperimentale M5. Il Setup Manager di M7, con attivazione, riparazione e disinstallazione del prodotto, non è nel codice.

Due registrazioni più vecchie restano raggiungibili e sono distinte dai menu M5:

- `ShellIntegration`, con `--install-context-menu` e `--remove-context-menu`, scrive `HKCU\Software\Classes\*\shell\Flip.Convert`. Il comando è `"Flip.exe" --files "%1"`, che apre la ruota. Non usa `FlipOwner`: se il comando esistente è diverso, la chiave viene preservata; la rimozione, quando il comando coincide, usa `DeleteSubKeyTree`.
- `ContextMenuPreview`, con `--install-context-menu-preview` e `--remove-context-menu-preview`, registra solo sul PNG il prototipo `Flip.Convert.Preview`, verbo **Converti con Flip (Anteprima)**, proprietario `FlipPrototype`, destinazioni `JPG`, `PDF` e `WEBP`.

L’avvio con Windows è un altro uso di HKCU: `StartupIntegration` gestisce il valore `Flip.RadialConverter` in `Software\Microsoft\Windows\CurrentVersion\Run`, con argomento `--background`. È il comportamento legacy della sezione 8.

## 7. Interfaccia M4 e ciclo di vita attuale

`MenuProgressWindow` è una finestra senza bordo, centrata, titolo `FLIP — Conversione`, scheda 440×210. `ShowLive` la apre sull’host. `--preview-menu-progress` apre la stessa finestra in anteprima e non converte.

`MenuProgressModel.View` segue la richiesta: indeterminato, percentuale misurata, successo, errore, annullamento, occupato. La percentuale mostrata durante il lavoro sta tra 1 e 99; il successo mostra 100%. Il pulsante Annulla cancella il `CancellationTokenSource` del job. In errore il dettaglio predefinito è che il file originale non è stato modificato. Se l’host è occupato, la nuova richiesta non parte. Al successo il pulsante può aprire la cartella del risultato (`Host.OpenFolder`) e un timer di circa 3,2 secondi chiude la finestra, rinviando la chiusura se il puntatore è sopra la scheda. `MenuProgressRouter.UsesBalloon` è falso: l’avanzamento M4 non è un fumetto della tray. `OpensWheel` è falso.

Il ciclo attuale, quando la voce di menu avvia FLIP, è:

1. nasce il processo, se non c’è già un host compatibile;
2. l’host mostra la finestra M4 ed esegue `ConversionEngine` per quel file e quel formato;
3. la finestra si chiude al termine o su comando dell’utente;
4. il processo resta in esecuzione con tray, pipe e mutex, finché non riceve Esci o `--stop`.

Un secondo avvio con `--background`, a mutex desktop già preso, esce con codice 0 senza messaggio. Un secondo avvio ordinario invia un messaggio sulla pipe legacy. La pipe di menu e la pipe legacy restano entrambe in ascolto per tutta la vita dell’host.

Il diario diagnostico sta in `%LOCALAPPDATA%\Flip\logs\events.jsonl`, con rotazione intorno a 2 MiB più un predecessore. Può contenere percorsi. `--evidence` sposta la cartella per i test. Le preferenze stanno in `%LOCALAPPDATA%\Flip\settings.json` e vengono sostituite tramite file temporaneo.

## 8. Componenti legacy ancora presenti

Questi componenti sono nel sorgente e sono collegati all’host. La loro rimozione è il contenuto di M6, non iniziata. Nessuno di essi è stato eliminato da questo allineamento.

| Componente | Dove si entra | Dipendenze principali |
|------------|---------------|------------------------|
| Ruota di conversione | `Host.Open`, `Wheel` | `Policy.Outputs`, `Wheel.OrderFormats`, `Native.Position`, `ConversionEngine` tramite `Host.Start` |
| Suono della ruota | `WheelSound`, voce di tray, `--preview-sound`, `--export-sound` | preferenza `SoundEnabled`, playback WinMM asincrono da memoria fissata |
| Gesture e drag-and-drop | `ShortcutHook`, `ShortcutOrderGate`, `ShortcutPressGate`, `Native` | tasto predefinito Freccia giù (`WheelShortcut.Default` = `DOWN`), Raw Input, tasto primario anche se i pulsanti del mouse sono scambiati |
| Impostazioni della ruota | `ShortcutSettingsWindow`, `--settings`, `--set-trigger` | `AppPreferences`, pipe legacy `trigger:` e `settings` |
| Tray | `Host`, `NotifyIcon` | menu conversione rapida, pannello, ultima cartella, gesto, suono, avvio con Windows, aggiornamenti GitHub, annullamento, uscita |
| Pannello multi-file | `SelectionWindow`, `--multi`, `--ui` | stesso motore, avanzamento sul pannello oltre che sulla ruota |
| Avvio automatico | `StartupIntegration`, `--enable-startup`, `--disable-startup`, `--install-shortcuts`, `--remove-shortcuts` | valore Run `Flip.RadialConverter`; collegamenti sul Desktop e nel menu Start con `--ui` |
| Host residente | `Host`, `RunApplication` | finestra WPF nascosta, `OnExplicitShutdown`, icona visibile |
| Mutex di istanza | `Local\Flip.Desktop.v2` in `Program.Main` e in `LiveMenuEndpoint` | un solo host desktop |
| Mutex di compatibilità menu | `Local\Flip.MenuConvert.v1`, tenuto da `Host` con `HoldCompatibilityMutex` | decide se inoltrare `--convert-menu` |
| Pipe legacy | `Flip.Local.Flip.v2`, `Ipc.Classify` | `exit`, `sound-test`, `settings`, `trigger:`, `select`, `multi:`, elenco file JSON. `CurrentUserOnly` |
| Pipe di menu | `Flip.Local.Flip.v2.menu` | richieste M4. Convive con la pipe legacy; non la sostituisce |
| Menu jolly e prototipo | `ShellIntegration`, `ContextMenuPreview` | comandi CLI della sezione 6 |
| Controllo aggiornamenti | `UpdateChecker`, voce di tray | interrogazione GitHub, senza installatore |

`UpdateChecker` e i collegamenti fanno parte della superficie legacy ancora esposta. Non sono il Setup Manager.

## 9. Test e limiti delle verifiche

I test automatizzati non sostituiscono un collaudo di Registro o di Esplora file.

| Verifica | Ingresso | Cosa copre | Limite |
|----------|----------|------------|--------|
| Motore generale | `--test-conversions` → `ConversionTests.Run` | Fixture locali del motore, hash dei sorgenti, presenza di Pandoc e LibreOffice registrata come esito del controllo | Non tocca il Registro dei menu né Esplora file. Un motore opzionale assente fa fallire il controllo corrispondente: è un comportamento del test, non un requisito di distribuzione |
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

Quel collaudo ha constatato i 15 menu e gli 86 comandi. Non ha eseguito manualmente tutte le 86 conversioni. I controlli di Registro e di Esplora file restano distinti dalle tre conversioni elencate. M5.6, il collaudo funzionale complessivo, non è conclusa.

## 10. Differenze rispetto all’obiettivo FLIX

**Architettura attuale.** FLIP, su questo branch, ha i menu M5.1–M5.4 e la finestra M4, e insieme tiene in vita l’infrastruttura precedente. Una conversione da menu può creare l’host residente oppure raggiungerlo. Ruota, tray, gesture, suono, avvio automatico, due mutex e due pipe fanno parte del processo che oggi esegue anche M4.

**Architettura futura.** FLIX è il convertitore Windows basato sui menu contestuali. Il flusso approvato, ancora non realizzato per intero, è:

1. selezione del file;
2. **Converti con Flip**;
3. scelta del formato di destinazione;
4. conversione;
5. finestra di avanzamento M4;
6. risultato.

Nel prodotto finale il processo parte per la richiesta e termina con la finestra M4. L’host residente a riposo è previsto in superamento. La gestione delle richieste contemporanee resta un tema di M6: questo documento non definisce un protocollo nuovo né un sostituto dei mutex `Local\Flip.Desktop.v2` e `Local\Flip.MenuConvert.v1` o delle pipe `Flip.Local.Flip.v2` e `Flip.Local.Flip.v2.menu`.

La voce di menu, gli eseguibili e le chiavi restano quelli di FLIP finché una fase successiva non li cambia. Qui non vengono rinominati.

## 11. Fasi architetturali ancora aperte

La sequenza approvata non cambia.

| Fase | Contenuto | Stato |
|------|-----------|--------|
| M5.5 | Menu contestuali dei documenti, con i motori disponibili. Fase distinta dai menu PDF, TXT e dati | Da implementare |
| M5.6 | Collaudo funzionale complessivo | Aperta. Il PASS di M5.4.6 non la chiude |
| M6 | Rimozione di ruota, tray, gesture, drag-and-drop, suoni, avvio automatico, host residente, mutex e IPC legacy; ciclo di vita legato alla singola conversione; collaudo completo | Non iniziata. Parte dopo la chiusura di M5 |
| M7 | FLIP Setup Manager e pacchetto installabile | Non iniziata. Parte dopo la chiusura di M6 |
| Revisione globale delle icone | Fase dedicata successiva | Nessun numero di milestone è assegnato |

Le icone premium già richieste dai menu M5 restano il comportamento implementato. La revisione grafica globale è un lavoro successivo, non svolto qui.

## 12. Riferimenti ai documenti ufficiali

| Documento | Uso |
|-----------|-----|
| [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md) | Direzione vincolante di FLIX, stato del branch, legacy, requisiti approvati, perimetro del collaudo M5.4.6 |
| [ROADMAP.md](ROADMAP.md) | Sequenza M5, M6 e M7 e distinzione dal contenuto storico |
| [TECHNICAL_DECISIONS.md](TECHNICAL_DECISIONS.md) | Decisioni di piattaforma e sezione «Definitive product architecture (approved)» |
| `src/Flip` | Comportamento effettivo descritto in questo file |

Se il codice e un documento di direzione divergono, lo stato implementato è quello verificabile in `src/Flip` al commit indicato. La direzione del prodotto finale resta quella dei tre documenti ufficiali, nelle fasi ancora aperte.
