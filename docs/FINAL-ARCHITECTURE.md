# Architettura FLIP → FLIX

Documento vincolante per la direzione del prodotto finale. Distingue l’**obiettivo architetturale FLIX** dallo **stato implementativo effettivo** del software FLIP sul branch di sviluppo, dopo il consolidamento di M5.4.

**FLIP** è il nome attuale del software, del repository e dei comandi esistenti.

**FLIX** è il nome pianificato del prodotto finale. L’implementazione integrale non è conclusa. FLIX non è un prodotto disponibile o distribuito.

**Qualificazione delle release.** La base storica documentata di questo fork è **FLIP 0.5.1**. L’ultima release pubblica upstream verificata è **FLIP v0.6.1 AURA Edition**, sul repository `thetinyfoundry/FLIP`. Quella release appartiene a quel repository upstream. FLIX resta l’architettura pianificata di questo fork e non è distribuita. Questo fork non ha GitHub Release proprie. Il codice presente sul branch remoto di sviluppo non è una GitHub Release. Dettaglio: [PUBLICATION-STATUS.md](PUBLICATION-STATUS.md).

Questo allineamento documentale non modifica il codice, i nomi degli eseguibili né le chiavi di Registro. Le decisioni di destinazione già approvate restano in vigore. La loro attuazione segue le fasi ancora aperte.

Il comportamento di ruota, tray e avvio automatico, storico e ancora presente nel codice, è descritto nelle sezioni «Platform and input», «Dynamic visual feedback» e «Build/publication» di [TECHNICAL_DECISIONS.md](../TECHNICAL_DECISIONS.md). La rimozione è prevista in M6. Il dettaglio dei collegamenti è nella [sezione 6](#6-collegamenti-documentali).

## Categorie

| Categoria | Uso in questo documento |
|-----------|-------------------------|
| **Implementato** | Presente nel branch attuale. |
| **Collaudato** | Verificato nel perimetro di collaudo indicato, e solo in quello. |
| **Pianificato** | Approvato, non ancora realizzato. |
| **Legacy ancora presente** | Ancora nel codice attuale. Non è l’esperienza definitiva. |
| **Destinato alla rimozione** | Rimozione prevista in M6, non in questo allineamento documentale. |

Una voce può rientrare in più categorie.

---

## 1. Scopo e obiettivo dell’architettura FLIX

L’esperienza definitiva prevista per FLIX è un convertitore integrato nel **menu contestuale** di Windows Explorer (Esplora file e contesti file compatibili). Nel prodotto finale non è previsto alcun altro canale di avvio permanente.

Percorso utente desiderato, come obiettivo di prodotto:

1. Selezione dei file.
2. **Converti con Flip**.
3. Scelta del formato di destinazione.
4. Conversione.
5. Finestra di avanzamento M4 (finestra centrale M4).
6. Risultato.

La voce di menu resta **«Converti con Flip»**, nome del software attuale. Questo documento non rinomina la voce, gli eseguibili o le chiavi di Registro.

Il percorso descrive la destinazione approvata. Il codice attuale lo realizza solo nella parte già classificata come implementata nella sezione 2, e convive ancora con i componenti legacy della sezione 3.

---

## 2. Stato implementativo effettivo del branch

Stato del branch di sviluppo al consolidamento di M5.4.

| Elemento | Categoria | Stato |
|----------|-----------|--------|
| Finestra di avanzamento M4 | Implementato | Presente. Ruolo e layout restano quelli concordati per M4. |
| M5.1 — menu PNG | Implementato | Presente nel branch. |
| M5.2 — menu immagini | Implementato | Presenti nel branch. |
| M5.3 — menu PDF, TXT e dati | Implementato | Presenti nel branch. |
| M5.4 — menu audio e video | Implementato | Presenti nel branch. Dipendono da FFmpeg. |
| Collaudo isolato M5.4.6 | Collaudato | PASS, nel perimetro descritto sotto. |
| M5.5 — menu documenti | Pianificato | Ancora da implementare. |
| M5.6 — collaudo complessivo | Pianificato | Ancora aperto. |
| M6 — rimozione dei componenti legacy | Pianificato | Non iniziata. |
| M7 — Setup Manager e distribuzione | Pianificato | Non implementato. |
| Nome di prodotto FLIX | Pianificato | Non implementato integralmente. Il software, il repository e i comandi restano FLIP. |
| Revisione globale delle icone | Pianificato | Fase dedicata successiva. Nessun numero di fase è assegnato in questo documento. |

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

## 3. Componenti legacy ancora presenti

Questi componenti sono nel codice attuale. Restano fino a M6. DOC-01.1 non li rimuove.

| Componente | Categorie |
|------------|-----------|
| Ruota di selezione | Legacy ancora presente. Destinato alla rimozione in M6. |
| Audio della ruota | Legacy ancora presente. Destinato alla rimozione in M6. |
| Gesture da tastiera e drag-and-drop della ruota | Legacy ancora presente. Destinato alla rimozione in M6. |
| Tray (area notifiche) | Legacy ancora presente. Destinato alla rimozione in M6. |
| Avvio con Windows | Legacy ancora presente. Destinato alla rimozione in M6. |
| Processo residente a riposo | Legacy ancora presente. Destinato alla rimozione in M6. |
| Mutex | Legacy ancora presente. Destinato alla rimozione in M6. |
| IPC tra processi | Legacy ancora presente. Destinato alla rimozione in M6. |

L’IPC attuale è realizzato con named pipe. Questo documento non definisce un sostituto e non anticipa nuovi nomi di eseguibili o di chiavi di Registro.

---

## 4. Funzionalità previste nelle fasi future

Numerazione e dipendenze: [ROADMAP.md](../ROADMAP.md). Lo stato del branch è nella sezione 2.

- **M6** non inizia prima della chiusura di **M5**.
- **M7** non inizia prima della chiusura di **M6**.

| Fase | Categoria | Contenuto previsto |
|------|-----------|--------------------|
| M5.5 | Pianificato | Menu documenti, con i motori disponibili. Non iniziata. |
| M5.6 | Pianificato | Collaudo funzionale complessivo. Ancora aperto. Il PASS di M5.4.6 non lo sostituisce. |
| M6 | Pianificato. Destinato alla rimozione per i componenti della sezione 3. | Pulizia definitiva: rimozione di ruota, suoni, gesture, drag-and-drop, tray, avvio automatico, processo residente, mutex e IPC. Processo effimero. Gestione della concorrenza. Eliminazione del codice morto. Collaudo completo. Non iniziata. |
| M7 | Pianificato | Pacchetto installabile e FLIP Setup Manager, con i requisiti della sezione 5. Non implementato. |
| Revisione globale delle icone | Pianificato | Fase dedicata successiva a questo allineamento. Il requisito delle icone premium resta approvato nella sezione 5. |

---

## 5. Requisiti definitivi già approvati

I requisiti di questa sezione sono vincolanti per il prodotto finale. Descrivono la destinazione approvata. Lo stato del codice è nella sezione 2. I componenti ancora presenti sono nella sezione 3.

### Esperienza utente nel menu contestuale

- Una voce principale: **«Converti con Flip»**.
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
| Processo in background | **Nessun processo FLIP residente** a riposo |

Queste esclusioni valgono per il prodotto finale. Nel branch attuale i componenti corrispondenti sono legacy ancora presenti e destinati alla rimozione in M6.

### Ciclo di vita del processo

1. **Avvio** solo quando l’utente richiede una conversione dal menu contestuale (o da operazioni esplicite del Setup Manager che lanciano il convertitore).
2. **Esecuzione** del flusso M4 per la richiesta corrente.
3. **Chiusura** del processo FLIP al termine della finestra M4 (nessuna permanenza idle).

### Correttezza e sicurezza dei file

- **Protezione assoluta dei file originali**: nessuna sovrascrittura o rinomina distruttiva degli originali; strategie di output affiancato e numerazione delle collisioni come da policy di progetto.
- **Richieste contemporanee**: gestione definita in M6 (processo effimero, concorrenza, code o serializzazione dove necessario) senza corruzione di output né interferenza tra job.

### Piattaforma e installazione

- **Windows 10 e Windows 11** (ambiente di riferimento x64, allineato al codebase .NET/WPF).
- **Installazione per utente** tramite hive **HKCU** (menu contestuali e configurazione per utente, senza richiedere servizi amministrativi per l’uso ordinario).

### FLIP Setup Manager

Requisito approvato della fase **M7**. Nel branch attuale il Setup Manager **non è implementato**.

Un **unico comando PowerShell** avvia il **Setup Manager interattivo** (bootstrap definitivo in fase **M7**; vedi [Bootstrap e distribuzione](#bootstrap-e-distribuzione)).

#### Menu principale

| Voce | Azione |
|------|--------|
| `[1]` | **Installa FLIP** (attivazione automatica del menu contestuale al termine dell’installazione) |
| `[2]` | **Aggiorna FLIP** |
| `[3]` | **Attiva / disattiva menu contestuale** (voce dinamica, vedi sotto) |
| `[4]` | **Ripara FLIP** |
| `[5]` | **Disinstalla FLIP** |
| `[6]` | **Mostra stato FLIP** |
| `[0]` | **Esci** |

#### Voce `[3]` dinamica (stessa sessione PowerShell)

Il menu si **aggiorna senza riavviare PowerShell** dopo ogni operazione che cambia lo stato:

| Stato rilevato | Testo della voce `[3]` |
|----------------|-------------------------|
| Menu contestuale **attivo** | Disattiva menu contestuale |
| Menu contestuale **disattivo** | Attiva menu contestuale |
| FLIP **non installato** | Indicazione di eseguire prima `[1] Installa FLIP` (la voce non esegue attivazione finché non c’è installazione) |

### Disinstallazione

La disinstallazione tramite Setup Manager deve:

**Rimuovere**

- Voci di **menu contestuale** di proprietà FLIP (registrazione HKCU).
- **Eseguibili e icone** installati dal pacchetto FLIP.
- **Componenti e configurazioni di installazione** non condivisi con altri programmi.

**Non eliminare automaticamente** l’intero contenuto di `%LOCALAPPDATA%\Flip` senza distinguere:

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

Per `[2] Aggiorna FLIP` e procedure correlate:

- **Backup** del pacchetto e/o degli artefatti di installazione **prima dell’aggiornamento**.
- **Conservazione** dello stato **attivo/disattivo** del menu contestuale attraverso l’aggiornamento.
- **Ripristino delle chiavi di Registro** originali FLIP in caso di rollback o riparazione.
- **Ripristino della versione precedente** del binario quando il rollback è richiesto e il backup è disponibile.
- **Verifica delle collisioni** con chiavi di registro **estranee** (non sovrascrivere voci non di proprietà FLIP).
- **Nessuna eliminazione** di file personali dell’utente durante update/rollback.
- **Nessuna promessa di atomicità** dell’aggiornamento senza garanzie implementative reali (documentare il comportamento effettivo in M7).

Dettaglio operativo: la voce `[4] Ripara FLIP` riallinea registrazione del menu, percorsi e integrità dell’installazione senza toccare le conversioni dell’utente.

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

- **M6** non inizia prima della chiusura di **M5**.
- **M7** non inizia prima della chiusura di **M6**.

---

## 6. Collegamenti documentali

- Decisioni tecniche: [TECHNICAL_DECISIONS.md](../TECHNICAL_DECISIONS.md).
  - Sezioni sul comportamento storico e ancora presente nel codice, comprese ruota, tray e avvio automatico: «Platform and input», «Dynamic visual feedback» e «Build/publication».
  - La sezione «Capabilities» è allineata allo stato attuale del branch: distingue la base storica documentata **FLIP 0.5.1** dallo sviluppo consolidato **M5.1–M5.4**; le conversioni audio e video dei menu contestuali sono condizionate alla disponibilità di **FFmpeg**. Le sezioni storiche nello stesso file descrivono decisioni precedenti e componenti legacy ancora presenti nel codice. Lo stato sintetico del branch resta anche nella sezione 2 di questo documento.
  - La sezione «Definitive product architecture (approved)» non è una sezione storica. Indica questo documento come fonte vincolante per l’architettura futura **FLIX**, non per un prodotto già disponibile o distribuito.
  - In quel file non esiste una sezione «Stato storico».
- Piano di numerazione e dipendenze M5–M7: [ROADMAP.md](../ROADMAP.md). La tabella iniziale riporta gli stati aggiornati fino a **M5.4**. La sezione 2 di questo documento sintetizza lo stato implementativo del branch al consolidamento di M5.4. **FLIX** è l’architettura di prodotto futura descritta da questo documento, non un prodotto già pubblicato.
- Requisiti di Setup Manager, disinstallazione e bootstrap: sezioni [FLIP Setup Manager](#flip-setup-manager), [Disinstallazione](#disinstallazione) e [Bootstrap e distribuzione](#bootstrap-e-distribuzione) di questo documento.
