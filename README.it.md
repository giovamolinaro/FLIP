# FLIP per Windows

Converti un file senza uscire da Esplora file. Trascinalo, premi **Freccia giù** e rilascialo sul formato nella ruota. FLIP salva una nuova copia accanto all'originale.

**FLIP** è il nome di questo progetto, di questo repository e del software pubblicato. **FLIX** è il nome pianificato dell'architettura definitiva. FLIX non è un prodotto scaricabile oggi.

Vanno distinti **quattro stati**. Dettagli: [Stato di pubblicazione](docs/PUBLICATION-STATUS.md).

| Stato | Che cos'è |
| --- | --- |
| **Ultima release pubblica upstream** | [FLIP v0.6.1 AURA Edition](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.6.1) sul repository originale [`thetinyfoundry/FLIP`](https://github.com/thetinyfoundry/FLIP). Ultima release pubblica upstream verificata nell'audit. Non è una release di questo fork, non è questo branch di sviluppo e non è FLIX. |
| **Base storica** | [FLIP 0.5.1](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1), il pacchetto documentato della linea personalizzata. Usa la ruota di conversione, l'icona nell'area di notifica e le interazioni descritte sotto. |
| **Branch di sviluppo** | `sviluppo-personalizzato` al commit `e6cfcf049277a30f93f8842a5f5769f0af01d697`. Le milestone M4–M5.4 sono implementate. Quel lavoro **non** è una release. |
| **Architettura pianificata** | FLIX: seleziona un file → **Converti con Flip** → scegli un formato → conversione → finestra di avanzamento → risultato. L'implementazione integrale è ancora aperta. |

I menu contestuali delle milestone M5.2–M5.4 non fanno parte del pacchetto storico 0.5.1.

![Ruota di conversione FLIP](docs/assets/wheel.png)

[Scarica la demo (MP4)](docs/assets/flip-demo.mp4) · [English](README.md) · [Guida all'installazione](docs/WINDOWS-INSTALL.md) · [Changelog](CHANGELOG.md) · [Stato di pubblicazione](docs/PUBLICATION-STATUS.md)

---

## Base storica — FLIP 0.5.1

La base storica documentata di questa linea è **FLIP 0.5.1**. Non è l'ultima release pubblica upstream. Le istruzioni di questa sezione descrivono quel pacchetto. Non descrivono le release AURA successive di upstream e non descrivono i menu contestuali del branch di sviluppo.

### Correzioni della 0.5.1

- L'installazione diretta da PowerShell funziona anche dallo ZIP estratto. Per l'installazione standard, fai doppio clic su `Install-FLIP.cmd`.
- FLIP rileva FFmpeg nella cartella utente indicata nella guida e abilita conversioni come MP4 in MP3.
- I PDF ottenuti da DOCX usano il formato A4.
- Il pannello di conversione multipla spiega come aprire la ruota con il tasto configurato.

### Novità della v0.5.0-beta.1

- **Conversioni audio e video**: converti tra formati video (MP4, MKV, WebM, AVI, MOV) e formati audio (MP3, WAV, FLAC, OGG, M4A, AAC), oppure estrai al volo la traccia audio da un file video con FFmpeg locale.
- **Dati strutturati nativi**: conversioni bidirezionali tra JSON, YAML, XML e CSV, oppure esportazione in PDF, HTML, Markdown e TXT, senza dipendenze esterne.
- **Arco di avanzamento radiale animato**: la ruota resta aperta durante le conversioni lunghe e mostra una linea rotante, un arco circolare fluido e la percentuale in tempo reale, con una spunta verde al completamento.
- **Controllo aggiornamenti da GitHub**: tasto destro sull'icona nell'area di notifica e voce *"Verifica aggiornamenti su GitHub…"* per cercare nuove release.
- **Feedback sonoro affidabile**: ripristinato il ticchettio tattile con riproduzione WASAPI/Media Foundation.

### Scarica e installa

**[Scarica FLIP 0.5.1 per Windows x64](https://github.com/thetinyfoundry/FLIP/releases/download/v0.5.1/Flip-0.5.1-win-x64.zip)** · [Note di rilascio e checksum](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1)

1. Apri la [pagina della release v0.5.1](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1), scarica l'archivio `Flip-0.5.1-win-x64.zip` ed estrailo.
2. Installa il [.NET 10 Windows Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0) se non è già presente.
3. Se FLIP è in esecuzione, chiudilo dal menu dell'icona vicino all'orologio, poi fai doppio clic su `Install-FLIP.cmd` nella cartella estratta. Apri FLIP dal collegamento sul Desktop o nel menu Start. Per usare l'app confezionata non servono SDK né compilatore.

Lo ZIP 0.5.1 è un pacchetto Windows x64. Non sono previsti pacchetti ARM64 né a 32 bit. L'app dipende dal framework: richiede il **.NET 10 Windows Desktop Runtime x64**, non solo il runtime .NET di base. Vedi [docs/WINDOWS-INSTALL.md](docs/WINDOWS-INSTALL.md) e [docs/PUBLICATION-STATUS.md](docs/PUBLICATION-STATUS.md).

Le release pubbliche successive di upstream, inclusa [v0.6.1 AURA Edition](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.6.1), compaiono nella pagina [Releases](https://github.com/thetinyfoundry/FLIP/releases) del repository originale. Non sono pacchetti di questo fork e non sono la base storica 0.5.1 descritta qui. Questo fork non ha GitHub Release proprie secondo l'audit.

L'installazione copia FLIP in `%LOCALAPPDATA%\Programs\Flip`.

### Motori opzionali (0.5.1)

FFmpeg, Pandoc e LibreOffice sono strumenti **esterni**. **Non** sono inclusi nello ZIP 0.5.1.

- **Audio e video**: copia `ffmpeg.exe` in `%LOCALAPPDATA%\Flip\tools` e riavvia FLIP. FLIP rileva anche FFmpeg installato tramite WinGet o disponibile nel PATH.
- **Documenti (Pandoc e LibreOffice)**: chiudi FLIP ed esegui `Install-DocumentEngine.ps1` dal pacchetto per scaricare Pandoc ufficiale. LibreOffice fornisce il rendering dei documenti.

Le conversioni di immagini e di dati strutturati funzionano senza questi motori.

### Compila dai sorgenti

Installa il [.NET 10 SDK per Windows x64](https://dotnet.microsoft.com/download/dotnet/10.0), clona o scarica questo repository e apri PowerShell nella cartella del progetto:

```powershell
.\scripts\Build.ps1 -DocumentEngine
.\scripts\Install.ps1
```

Il programma di installazione copia FLIP in `%LOCALAPPDATA%\Programs\Flip`, crea i collegamenti sul Desktop e nel menu Start e abilita l'avvio all'accesso a Windows. Usa `-NoShortcuts` o `-NoStartup` per escludere queste opzioni.

Questi comandi compilano l'albero che hai scaricato. Il binario documentato di questa base storica è lo ZIP 0.5.1 indicato sopra. Compilare il branch di sviluppo attuale **non** produce una nuova release pubblica.

FLIP resta nell'area di notifica vicino all'orologio. Tasto destro sull'icona per aprire Impostazioni, scegliere la scorciatoia, cambiare lingua, provare il suono della ruota o verificare gli aggiornamenti su GitHub. Nelle Impostazioni puoi impostare il tasto di attivazione (frecce, barra spaziatrice o un tasto singolo della tastiera).

### Usa la ruota

1. Seleziona uno o più file in Esplora file o sul Desktop.
2. Inizia a trascinare e premi **Freccia giù** (o la scorciatoia personalizzata). Quando la ruota compare puoi rilasciare il tasto.
3. Porta il cursore su un formato di uscita disponibile e rilascia.
4. Durante la conversione osserva l'arco circolare e la percentuale. Premi **Esc** in qualsiasi momento per annullare. Per annullare prima del rilascio, rilascia il file fuori dalla ruota.

FLIP riserva il tasto scelto durante i gesti del mouse in Esplora file e sul Desktop. Fuori da quel contesto lascia passare l'input da tastiera.

I nomi di uscita includono `.converted` e un numero quando serve. FLIP **non** sovrascrive mai l'originale né un file di uscita già esistente.

### Conversioni supportate (ruota 0.5.1)

Questi formati sono quelli della ruota storica 0.5.1. Non sono un elenco di menu contestuali di Esplora file.

| Categoria | Formati in ingresso | Formati in uscita | Motore |
| --- | --- | --- | --- |
| **Immagini** | PNG, JPG/JPEG, WebP, BMP, TIFF, GIF | PNG, JPG, WebP, PDF, BMP, TIFF, GIF (escluso il formato sorgente) | SkiaSharp nativo |
| **Dati strutturati** | JSON, YAML/YML, XML, CSV | JSON, YAML, XML, CSV, TXT, HTML, Markdown, PDF | Incorporato |
| **Audio** | MP3, WAV, FLAC, OGG, M4A, AAC, WMA, OPUS | WAV, MP3, FLAC, OGG, M4A, AAC | FFmpeg locale |
| **Video** | MP4, MKV, WebM, AVI, MOV, WMV, M4V | MP4, MKV, WebM, AVI, MOV, più estrazione audio in MP3 e WAV | FFmpeg locale |
| **Testo semplice** | TXT | PDF, PNG, JPG, WebP (nucleo); DOCX, EPUB, Markdown, HTML, ODT, RTF, FB2, RST (Pandoc) | Nucleo / Pandoc |
| **Documenti** | Markdown, HTML, DOCX, EPUB, ODT, RTF, FB2, RST | Altri formati documento con Pandoc; il rendering in PDF/immagini richiede LibreOffice | Pandoc / LibreOffice |
| **PDF** | PDF | PNG/JPG/WebP per pagina e TXT estratto; altri formati documento con Pandoc | Nativo / Pandoc |

### Note e limiti (0.5.1)

- Le conversioni restano in locale. Non c'è un account né un servizio di conversione sul cloud.
- Audio e video sulla ruota richiedono un FFmpeg locale già presente sulla macchina. Il pacchetto non include FFmpeg.
- Le conversioni documentali avanzate richiedono Pandoc; il rendering di documenti diversi da TXT in PDF o immagini richiede LibreOffice. FLIP non installa LibreOffice automaticamente.
- Restano limiti di collaudo del pacchetto 0.5.1: drag-and-drop nativo più ampio, ascolto, DPI misti e macchine pulite. L'integrazione del trascinamento con altri gestori di file non è dichiarata.
- Tratta con cautela i documenti non attendibili. I limiti di processo non sono una sandbox di sicurezza completa. Vedi [SECURITY.md](SECURITY.md).

---

## Branch di sviluppo

Questa sezione descrive il branch **`sviluppo-personalizzato`** al commit `e6cfcf049277a30f93f8842a5f5769f0af01d697`. È lavoro in corso. **Non** è una nuova release pubblica e **non** è lo ZIP 0.5.1.

Su questo branch i menu contestuali di Esplora file sono implementati fino alla milestone **M5.4**. Il pacchetto storico 0.5.1 non include quei menu.

| Milestone | Cosa c'è nel branch |
| --- | --- |
| **M4** | Finestra centrale di avanzamento, implementata. |
| **M5.1** | Menu contestuale PNG, implementato. |
| **M5.2** | Menu contestuali immagini, implementati. |
| **M5.3** | Menu PDF, TXT e dati, implementati. |
| **M5.4** | Menu contestuali audio e video, implementati. |

### Menu audio e video (M5.4)

Con FFmpeg rilevato, il branch prevede **8** estensioni audio sorgente, **7** estensioni video sorgente, **15** menu complessivi e **86** comandi di destinazione. Se FFmpeg manca, l'installazione viene rifiutata prima di qualsiasi scrittura nel Registro. Le voci di destinazione usano icone premium dedicate.

Questo **non** significa che tutte le 86 conversioni siano state eseguite a mano.

Il collaudo reale isolato **M5.4.6** è stato completato con **PASS** nell'account FlipLab, nel perimetro registrato in [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md):

- installazione dei 15 menu e constatazione degli 86 comandi di destinazione;
- verifiche in Esplora file su WAV e MP4;
- conversioni reali **MP4 → MKV**, **MP4 → MP3** e **WAV → MP3**, con controlli di integrità dei sorgenti e di decodifica degli output;
- rimozione dei 15 menu, poi verifiche nel Registro e visive, inclusa l'assenza delle 15 chiavi dopo la rimozione.

Su questo branch restano distinti:

1. i test automatizzati (matrici del motore e controlli in memoria);
2. i test del Registro simulato (store in memoria, senza toccare il Registro di Windows);
3. i collaudi reali di conversione (le tre coppie elencate sopra);
4. le verifiche in Esplora file e nel Registro reale.

M5.4.6 non chiude il collaudo funzionale complessivo previsto in M5.6.

### Cosa convive ancora

Il branch **non** coincide ancora con l'architettura pianificata FLIX. Oggi nello stesso processo restano:

- i menu contestuali M5;
- la finestra di avanzamento M4;
- la ruota di conversione legacy;
- l'icona nell'area di notifica;
- un host residente;
- i mutex;
- l'IPC su named pipe legacy.

Dopo una conversione dal menu contestuale la finestra di avanzamento può chiudersi, ma il processo resta residente. L'uscita al termine della conversione è lavoro futuro di **M6**. Questo README non definisce un nuovo disegno di concorrenza.

I menu contestuali dei documenti (**M5.5**) non sono ancora implementati. Non trattare il branch attuale come un prodotto finito né come sostituto della 0.5.1.

Il dettaglio implementativo sta in [ARCHITECTURE.md](ARCHITECTURE.md), non in questo README.

---

## Architettura pianificata

**FLIX** è il nome pianificato dell'architettura definitiva. Il software, il repository, l'eseguibile, i comandi CLI e le voci di Registro restano **FLIP** finché una fase successiva non li cambia. Questo README non rinomina nulla.

Il percorso utente approvato è:

1. Seleziona un file.
2. **Converti con Flip**.
3. Scegli un formato di uscita.
4. Converti.
5. Osserva la finestra di avanzamento M4.
6. Ottieni il risultato.

Quel percorso è la destinazione. Non è ancora realizzato per intero. Il prodotto definitivo sarà incentrato sull'integrazione con il menu contestuale di Esplora file. La chiusura del processo al termine della finestra di avanzamento appartiene a **M6**, che non è iniziata.

Direzione approvata, ordine delle fasi e requisiti residui:

- [docs/FINAL-ARCHITECTURE.md](docs/FINAL-ARCHITECTURE.md) — direzione vincolante di FLIX
- [ARCHITECTURE.md](ARCHITECTURE.md) — architettura realmente implementata su questo branch
- [ROADMAP.md](ROADMAP.md) — sequenza delle milestone
- [TECHNICAL_DECISIONS.md](TECHNICAL_DECISIONS.md) — decisioni approvate

### Lavoro residuo

Le milestone aperte **non** hanno una versione di rilascio pubblico assegnata.

| Lavoro | Stato |
| --- | --- |
| **M5.5** — menu contestuali documenti | Da implementare. Distinta dai menu PDF, TXT e dati di M5.3. |
| **M5.6** — collaudo funzionale complessivo | Da completare. Il PASS di M5.4.6 non chiude questa fase. |
| **M6** — rimozione dei componenti legacy | Non iniziata. Segue la chiusura di M5. Rimuove ruota, tray, host residente, mutex, IPC legacy e i componenti associati, e lega la vita del processo a una singola conversione. |
| **M7** — FLIP Setup Manager | Non iniziata. Segue la chiusura di M6. |
| Revisione globale delle icone | Fase dedicata successiva. Senza numerazione. |

La sequenza approvata non cambia: **M6 non inizia prima della chiusura di M5. M7 non inizia prima della chiusura di M6.**

---

## Requisiti e dipendenze

Non mescolare questi quattro livelli.

**Base storica 0.5.1**

- Windows x64. Windows 11 è l'obiettivo di accettazione documentato di quel pacchetto.
- [.NET 10 Windows Desktop Runtime x64](https://dotnet.microsoft.com/download/dotnet/10.0) per eseguire lo ZIP. Per quel pacchetto l'SDK non è necessario.
- FFmpeg, Pandoc e LibreOffice sono motori esterni opzionali. Non sono nello ZIP.

**Codice del branch di sviluppo**

- Stessa piattaforma Windows x64 e .NET 10 Desktop (`net10.0-windows`).
- Tra le librerie dirette del progetto ci sono PDFtoImage e PdfPig; vedi [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
- Le conversioni native di immagini, testo e dati strutturati non richiedono FFmpeg, Pandoc o LibreOffice.
- I **menu contestuali** audio/video di questo branch si installano e funzionano solo se FFmpeg è rilevato. Pandoc resta opzionale per destinazioni documentali aggiuntive. LibreOffice resta opzionale per il rendering Office/PDF.

**Strumenti esterni opzionali**

| Strumento | Ruolo | Incluso? |
| --- | --- | --- |
| **FFmpeg** | Conversioni audio e video (ruota nella 0.5.1; menu contestuali sul branch di sviluppo) | No. FLIP cerca `ffmpeg.exe` sulla macchina. |
| **Pandoc** | Conversioni documentali avanzate | No. Opzionale; lo script del pacchetto può scaricare la build ufficiale. |
| **LibreOffice** | Rendering dei documenti in PDF/immagini | No. Installazione utente separata. |

Questo README non fissa le versioni dei motori. Avvisi e licenze dei componenti che *fanno* parte del progetto sono in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) e [LICENSE](LICENSE).

**Previsto per FLIX**

- Windows 10 e Windows 11.
- Installazione per utente (HKCU).
- Pandoc, LibreOffice e FFmpeg restano opzionali. Le conversioni native devono funzionare senza di essi. Il menu deve mostrare solo i formati realmente disponibili.
- Un'eventuale distribuzione successiva dei motori esterni è lavoro futuro del Setup Manager (M7). Non è implementata.

Windows 10 è un requisito previsto per FLIX, non un collaudo già completato della base storica 0.5.1.

---

## Privacy ed esecuzione locale

Tutte le conversioni avvengono **in locale** sul tuo computer, senza servizi cloud di terzi e senza account. I file convertiti restano nella cartella originale.

I log diagnostici di runtime sono in `%LOCALAPPDATA%\Flip\logs`. Rivedili e anonimizzali prima di condividerli. Evita di caricare ingressi sensibili o log non ripuliti nelle issue pubbliche. Vedi [SECURITY.md](SECURITY.md).

---

## Licenza

Il file `LICENSE` di questo fork è la [licenza MIT](LICENSE). È il testo presente in questo repository, incluso il branch `sviluppo-personalizzato`, e sul tag storico `v0.5.1`. I componenti di terze parti mantengono le proprie licenze. Vedi [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md). Il `LICENSE` AURA di upstream, dalla v0.6.0, dichiara GPL-3.0-only; ciò non è una rilicenza di questo fork. Dettagli: [Stato di pubblicazione](docs/PUBLICATION-STATUS.md).

[Contribuisci](CONTRIBUTING.md) · [Changelog](CHANGELOG.md) · [Architettura](ARCHITECTURE.md) · [Sicurezza](SECURITY.md)
