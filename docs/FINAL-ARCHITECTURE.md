# Architettura FLIP → FLIX

Documento vincolante per la direzione del prodotto finale. Distingue l’**applicazione FLIX** nell’albero di lavoro dallo **stato ancora aperto**, che è la distribuzione M7.

**FLIX** è l'unica identità del prodotto in questo albero: `src/Flix/Flix.csproj`, namespace `Flix`, eseguibile `Flix.exe`, menu **Converti con FLIX**. Non è una release distribuita. M7 non è iniziata.

Il repository remoto si chiama ancora `FLIP`. Il pacchetto storico **FLIP 0.5.1** e le release upstream su `thetinyfoundry/FLIP` conservano il nome con cui sono state pubblicate.

**Qualificazione delle release.** La base storica documentata di questo fork è **FLIP 0.5.1**. L’ultima release pubblica upstream verificata è **FLIP v0.6.1 AURA Edition**, sul repository `thetinyfoundry/FLIP`. Quella release appartiene a quel repository upstream. FLIX è il prodotto di questo albero di lavoro e non è distribuito. Questo fork non ha GitHub Release proprie. Il codice presente sul branch remoto di sviluppo non è una GitHub Release. Dettaglio: [PUBLICATION-STATUS.md](PUBLICATION-STATUS.md).

Il rebranding ha allineato il nome visibile, l’eseguibile, il progetto, il namespace, le icone di menu e gli identificativi di Registro delle installazioni nuove. Non ha cambiato le licenze, i comandi di conversione né il comportamento del motore. Le decisioni di M7 restano da attuare. I letterali storici indispensabili stanno in `LegacyIdentity`.

Il comportamento storico di ruota, tray e avvio automatico è descritto nelle sezioni «Platform and input», «Dynamic visual feedback» e «Build/publication» di [TECHNICAL_DECISIONS.md](../TECHNICAL_DECISIONS.md). Quei componenti non fanno più parte dell’applicazione in esecuzione. Gli script di installazione ritirati sono archiviati fuori dal repository: non installano FLIX e non attivano tray o avvio automatico. Il dettaglio dei collegamenti è nella [sezione 6](#6-collegamenti-documentali).

## Categorie

| Categoria | Uso in questo documento |
|-----------|-------------------------|
| **Implementato** | Presente nel branch attuale. |
| **Collaudato** | Verificato nel perimetro di collaudo indicato, e solo in quello. |
| **Pianificato** | Approvato, non ancora realizzato. |
| **Legacy storico** | Descritto nei documenti precedenti. Non fa parte dell’applicazione in esecuzione. |
| **Rimosso da M6** | Ruota, tray, gesture, avvio automatico, host residente, mutex e IPC non sono nel processo attuale. |

Una voce può rientrare in più categorie.

---

## 1. Scopo e obiettivo dell’architettura FLIX

L’esperienza definitiva prevista per FLIX è un convertitore integrato nel **menu contestuale** di Windows Explorer (Esplora file e contesti file compatibili). Nel prodotto finale non è previsto alcun altro canale di avvio permanente.

Percorso utente desiderato, come obiettivo di prodotto:

1. Selezione dei file.
2. **Converti con FLIX**.
3. Scelta del formato di destinazione.
4. Conversione.
5. Finestra di avanzamento M4 (finestra centrale M4).
6. Risultato.

La voce di menu dell’applicazione in esecuzione è **«Converti con FLIX»**. L’icona immediatamente a sinistra è il monogramma XF bianco. Il progetto e il namespace sono `Flix`. Il repository remoto e il pacchetto storico restano con il nome pubblicato.

Il percorso è implementato nell’albero di lavoro per i menu e per la finestra M4. La distribuzione resta M7.

---

## 2. Stato implementativo effettivo del branch

Stato dell’albero di lavoro dopo M6 e dopo il rebranding. Non è committato oltre `6d6a16e`.

| Elemento | Categoria | Stato |
|----------|-----------|--------|
| Finestra di avanzamento M4 | Implementato | Presente. Titolo visibile **FLIX — Conversione**. Layout invariato. |
| M5.1 — menu PNG | Implementato | Presente. |
| M5.2 — menu immagini | Implementato | Presenti. |
| M5.3 — menu PDF, TXT e dati | Implementato | Presenti. |
| M5.4 — menu audio e video | Implementato | Presenti. Dipendono da FFmpeg. |
| Collaudo isolato M5.4.6 | Collaudato | PASS, nel perimetro descritto sotto. |
| M5.5 — menu documenti | Implementato | Presenti se Pandoc è rilevato. |
| M5.6 — collaudo complessivo | Collaudato nel perimetro M6 | Cinque conversioni, annullamento, concorrenza e rimozione. Non ogni destinazione a mano. |
| M6 — rimozione dei componenti legacy | Implementato | Fatto nel processo in esecuzione. Collaudo Esplora file PASS nel perimetro registrato. |
| Identità FLIX | Implementato | `Flix.exe`, **Converti con FLIX**, `flix-xf.ico` sul solo menu padre. Controllo visivo FlipLab non eseguito. |
| M7 — Setup Manager e distribuzione | Pianificato | Non implementato. |
| Revisione globale delle icone | Parziale | Icona padre XF approvata. Destinazioni ancora premium-v2. |

### Perimetro del collaudo M5.4.6

M5.4.6 ha completato con esito **PASS** un collaudo reale isolato nell’account Windows FlipLab.

Controlli di installazione, Esplora file e Registro:

- installazione di 15 menu;
- 86 comandi di destinazione;
- menu WAV e MP4 in Esplora file;
- rimozione dei 15 menu;
- verifica del Registro a 0/15;
- verifica grafica dopo la rimozione.

Conversioni eseguite in quel collaudo:

- MP4 → MKV;
- MP4 → MP3;
- WAV → MP3;
- integrità dei file sorgente;
- validazione e decodifica degli output.

L’audit finale è positivo.

Il collaudo ha constatato i 15 menu e gli 86 comandi di destinazione. Non ha provato manualmente tutte le 86 conversioni. I controlli di Registro e di Esplora file sono distinti dalle conversioni effettivamente eseguite, elencate sopra. Questo collaudo non chiude M5.6.

Le conversioni native restano previste senza obbligo di Pandoc, LibreOffice o FFmpeg. I menu audio e video di M5.4 sono la categoria aggiuntiva che dipende da FFmpeg, secondo il requisito di motori opzionali della sezione 5.

---

## 3. Componenti legacy

Questi componenti non fanno parte dell’applicazione in esecuzione. Restano descritti nella storia tecnica. `scripts/Install.ps1` non è in questo albero e non è stato convertito in un installer FLIX.

| Componente | Stato |
|------------|--------|
| Ruota di selezione | Rimossa dal processo. |
| Audio della ruota | Rimosso dal processo. |
| Gesture da tastiera e drag-and-drop della ruota | Rimossi dal processo. |
| Tray (area notifiche) | Rimossa dal processo. |
| Avvio con Windows | Rimosso dal processo. Gli script di installazione ritirati non sono in questo albero e non lo registrano. |
| Processo residente a riposo | Rimosso. La finestra M4 è la finestra principale e la chiusura termina il processo. |
| Mutex e IPC tra processi | Rimossi dal percorso di conversione. |

Il menu originale `Software\Classes\*\shell\Flip.Convert` non viene riscritto né cancellato. L’anteprima nuova è `Flix.Convert.Preview`. L’anteprima precedente viene rimossa solo se il marcatore coincide con l’identificativo storico di questa applicazione. Un’anteprima di un altro proprietario resta.

---

## 4. Funzionalità previste nelle fasi future

Numerazione e dipendenze: [ROADMAP.md](../ROADMAP.md). Lo stato del branch è nella sezione 2.

- **M7** non è iniziata. Il rebranding non l’ha avviata.

| Fase | Categoria | Contenuto |
|------|-----------|-----------|
| M5.5 | Implementato | Menu documenti, con i motori disponibili. |
| M5.6 | Collaudato nel perimetro M6 | Cinque conversioni reali, annullamento, concorrenza e rimozione. Non ogni destinazione a mano. |
| M6 | Implementato | Pulizia del processo: niente ruota, suoni, gesture, tray, avvio automatico, host, mutex e IPC. |
| Rebranding | Implementato, controllo visivo aperto | Nome FLIX, icona XF sul menu padre. FlipLab non eseguito. |
| M7 | Pianificato | Pacchetto installabile e Setup Manager, con i requisiti della sezione 5. Non implementato. |

---

## 5. Requisiti definitivi già approvati

I requisiti di questa sezione sono vincolanti per il prodotto finale. Descrivono la destinazione approvata. Lo stato del codice è nella sezione 2. I componenti ancora presenti sono nella sezione 3.

### Esperienza utente nel menu contestuale

- Una voce principale: **«Converti con FLIX»**, con l’icona XF bianca immediatamente a sinistra. Il segno è tenue su un menu chiaro; il disegno approvato non è stato ricolorato.
- **Sottomenu** con i soli formati di destinazione **realmente disponibili** sul sistema (motori installati, capacità native, dipendenze opzionali assenti → voce non mostrata).
- **Icone premium** distinte per voce e formato, coerenti con la qualità visiva del prodotto. La revisione globale delle icone è una fase dedicata successiva (sezione 4). Il requisito resta approvato.
- Dopo la scelta del formato si apre la **finestra centrale M4** (flusso di conversione esistente), **invariata** nel ruolo e nel layout concordato per M4.

### Esclusioni esplicite (prodotto finale)

| Area | Decisione |
|------|-----------|
| Ruota di selezione | **Nessuna ruota** |
| Audio UI ruota | **Nessun suono** associato alla ruota |
| Gesture da tastiera/hook per conversione | **Nessuna gesture** |
| Drag-and-drop sulla ruota | **Nessun drag-and-drop** della ruota |
| Area notifiche | **Nessuna tray** permanente |
| Avvio con Windows | **Nessun avvio automatico** |
| Processo in background | **Nessun processo residente** a riposo |

Queste esclusioni valgono per l’applicazione in esecuzione. Il processo di una conversione termina con la finestra M4.

### Ciclo di vita del processo

1. **Avvio** solo quando l’utente richiede una conversione dal menu contestuale (o da operazioni esplicite del Setup Manager che lanciano il convertitore).
2. **Esecuzione** del flusso M4 per la richiesta corrente.
3. **Chiusura** del processo al termine della finestra M4 (nessuna permanenza idle).

### Correttezza e sicurezza dei file

- **Protezione assoluta dei file originali**: nessuna sovrascrittura o rinomina distruttiva degli originali; strategie di output affiancato e numerazione delle collisioni come da policy di progetto.
- **Richieste contemporanee**: gestione definita in M6 (processo effimero, concorrenza, code o serializzazione dove necessario) senza corruzione di output né interferenza tra job.

### Piattaforma e installazione

- **Windows 10 e Windows 11** (ambiente di riferimento x64, allineato al codebase .NET/WPF).
- **Installazione per utente** tramite hive **HKCU** (menu contestuali e configurazione per utente, senza richiedere servizi amministrativi per l’uso ordinario).

### FLIX Setup Manager

Requisito approvato della fase **M7**. Non è implementato e non è stato avviato. Il prodotto che quel manager dovrà installare è FLIX. Il testo sotto è il requisito, non un installer esistente.

Un **unico comando PowerShell** avvia il **Setup Manager interattivo** (bootstrap definitivo in fase **M7**; vedi [Bootstrap e distribuzione](#bootstrap-e-distribuzione)).

#### Menu principale

| Voce | Azione |
|------|--------|
| `[1]` | **Installa FLIX** (attivazione automatica del menu contestuale al termine dell’installazione) |
| `[2]` | **Aggiorna FLIX** |
| `[3]` | **Attiva / disattiva menu contestuale** (voce dinamica, vedi sotto) |
| `[4]` | **Ripara FLIX** |
| `[5]` | **Disinstalla FLIX** |
| `[6]` | **Mostra stato FLIX** |
| `[0]` | **Esci** |

#### Voce `[3]` dinamica (stessa sessione PowerShell)

Il menu si **aggiorna senza riavviare PowerShell** dopo ogni operazione che cambia lo stato:

| Stato rilevato | Testo della voce `[3]` |
|----------------|-------------------------|
| Menu contestuale **attivo** | Disattiva menu contestuale |
| Menu contestuale **disattivo** | Attiva menu contestuale |
| FLIX **non installato** | Indicazione di eseguire prima `[1] Installa FLIX` (la voce non esegue attivazione finché non c’è installazione) |

### Disinstallazione

La disinstallazione tramite Setup Manager deve:

**Rimuovere**

- Voci di **menu contestuale** di proprietà di FLIX (registrazione HKCU), comprese le chiavi precedenti riconosciute da `LegacyIdentity`.
- **Eseguibili e icone** installati dal pacchetto FLIX.
- **Componenti e configurazioni di installazione** non condivisi con altri programmi.

**Non eliminare automaticamente** l’intero contenuto di `%LOCALAPPDATA%\Flix` né di `%LOCALAPPDATA%\Flip` senza distinguere:

| Categoria | Trattamento |
|-----------|-------------|
| Cache e dati temporanei | Rimovibili con la disinstallazione standard |
| Log | Eliminazione solo dopo **conferma separata** se presenti |
| Preferenze utente | Eliminazione solo dopo **conferma separata** se presenti |
| Altri dati persistenti | Valutazione esplicita; nessuna cancellazione massiva implicita |

**Vincoli assoluti**

- **Mai** cancellare documenti o file **convertiti** dall’utente.
- **Mai** disinstallare o alterare **motori esterni condivisi** (es. FFmpeg, LibreOffice, Pandoc installati per altri usi).

### Aggiornamento e ripristino

Per `[2] Aggiorna FLIX` e procedure correlate:

- **Backup** del pacchetto e/o degli artefatti di installazione **prima dell’aggiornamento**.
- **Conservazione** dello stato **attivo/disattivo** del menu contestuale attraverso l’aggiornamento.
- **Ripristino delle chiavi di Registro** di FLIX in caso di rollback o riparazione.
- **Ripristino della versione precedente** del binario quando il rollback è richiesto e il backup è disponibile.
- **Verifica delle collisioni** con chiavi di registro **estranee** (non sovrascrivere voci non di proprietà di FLIX).
- **Nessuna eliminazione** di file personali dell’utente durante update/rollback.
- **Nessuna promessa di atomicità** dell’aggiornamento senza garanzie implementative reali (documentare il comportamento effettivo in M7).

Dettaglio operativo: la voce `[4] Ripara FLIX` riallinea registrazione del menu, percorsi e integrità dell’installazione senza toccare le conversioni dell’utente.

<a id="bootstrap-e-distribuzione"></a>

### Bootstrap e distribuzione (M7)

Requisiti documentati. **URL pubblico e dominio non ancora scelti**: non usare URL di esempio come se fossero operativi. Fase **M7**, non implementata.

#### Download e fiducia

- Scarico **HTTPS** del pacchetto di installazione o del bootstrap.
- **Verifica autenticità** del pacchetto (catena di fiducia, editore attendibile).
- **Firma Authenticode** con **controllo dell’editore atteso**.
- **Verifica integrità** (hash pubblicato o incorporato nel processo di release).
- **Gestione download falliti** (ritentativi, messaggi chiari, nessuno stato parziale inconsistente).

#### PowerShell

- Compatibilità **Windows PowerShell 5.1** e **PowerShell 7+**.
- Microsoft sconsiglia `Invoke-Expression` su contenuto scaricato: la soluzione definitiva deve offrire **un solo comando di avvio** per l’utente con la **minore esposizione possibile** (es. esecuzione di script firmato, modulo firmato, o avvio di stub locale verificato). La modalità definitiva sarà **stabilita in M7**.

### Dipendenze e conversioni

| Motore | Ruolo |
|--------|--------|
| **Conversioni native** | Devono funzionare **senza** obbligo di installare Pandoc, LibreOffice o FFmpeg. |
| **Pandoc, LibreOffice, FFmpeg** | **Opzionali**; abilitano categorie aggiuntive quando presenti e validati. |

- Nel sottomenu contestuale mostrare **solo** conversioni **realmente disponibili**.
- La **distribuzione** di motori esterni (se prevista) richiede in M7: verifica **licenze**, **integrità** e politica **aggiornamenti**.

I menu audio e video già presenti nel branch (M5.4) sono la categoria aggiuntiva dipendente da FFmpeg. Non rendono FFmpeg obbligatorio per le conversioni native.

### Sequenza delle fasi

M7 non è iniziata.

---

## 6. Collegamenti documentali

- Decisioni tecniche: [TECHNICAL_DECISIONS.md](../TECHNICAL_DECISIONS.md).
  - Sezioni sul comportamento storico di ruota, tray e avvio automatico: «Platform and input», «Dynamic visual feedback» e «Build/publication». Quei componenti non sono nel processo attuale. Le sezioni restano decisioni storiche.
  - La sezione «Capabilities» distingue la base storica **FLIP 0.5.1** dallo sviluppo dei menu. Le conversioni audio e video restano condizionate a **FFmpeg**. Lo stato sintetico dell’albero di lavoro è la sezione 2 di questo documento.
  - La sezione «Definitive product architecture (approved)» indica questo documento come fonte vincolante. FLIX, in quell’accezione di prodotto distribuito, non è pubblicato.
  - In quel file non esiste una sezione «Stato storico».
- Piano di numerazione e dipendenze M5–M7: [ROADMAP.md](../ROADMAP.md). La tabella iniziale riporta M5, M6 e il rebranding. M7 resta futura. **FLIX** è l’applicazione dell’albero di lavoro, non una release pubblicata.
- Requisiti di Setup Manager, disinstallazione e bootstrap: sezioni [FLIX Setup Manager](#flix-setup-manager), [Disinstallazione](#disinstallazione) e [Bootstrap e distribuzione](#bootstrap-e-distribuzione) di questo documento.
