# Architettura definitiva FLIP

Documento vincolante per il prodotto finale. Le decisioni qui riportate sostituiscono, per il rilascio definitivo, le funzionalità storiche orientate alla ruota, al tray e all’avvio automatico (vedi sezione «Stato storico» in [TECHNICAL_DECISIONS.md](../TECHNICAL_DECISIONS.md)).

## Scopo del prodotto

FLIP finale è **esclusivamente** un convertitore integrato nel **menu contestuale** di Windows (Esplora file e contesti file compatibili). Nessun altro canale di avvio permanente è previsto nel prodotto finale.

## Esperienza utente nel menu contestuale

- Una voce principale: **«Converti con Flip»**.
- **Sottomenu** con i soli formati di destinazione **realmente disponibili** sul sistema (motori installati, capacità native, dipendenze opzionali assenti → voce non mostrata).
- **Icone premium** distinte per voce/formato, coerenti con la qualità visiva del prodotto.
- Dopo la scelta del formato si apre la **finestra centrale M4** (flusso di conversione esistente), **invariata** nel ruolo e nel layout concordato per M4.

## Esclusioni esplicite (prodotto finale)

| Area | Decisione |
|------|-----------|
| Ruota di selezione | **Nessuna ruota** |
| Audio UI ruota | **Nessun suono** associato alla ruota |
| Gesture da tastiera/hook per conversione | **Nessuna gesture** |
| Drag-and-drop sulla ruota | **Nessun drag-and-drop** della ruota |
| Area notifiche | **Nessuna tray** permanente |
| Avvio con Windows | **Nessun avvio automatico** |
| Processo in background | **Nessun processo FLIP residente** a riposo |

## Ciclo di vita del processo

1. **Avvio** solo quando l’utente richiede una conversione dal menu contestuale (o da operazioni esplicite del Setup Manager che lanciano il convertitore).
2. **Esecuzione** del flusso M4 per la richiesta corrente.
3. **Chiusura** del processo FLIP al termine della finestra M4 (nessuna permanenza idle).

## Correttezza e sicurezza dei file

- **Protezione assoluta dei file originali**: nessuna sovrascrittura o rinomina distruttiva degli originali; strategie di output affiancato e numerazione collisioni come da policy di progetto.
- **Richieste contemporanee**: gestione definita in M6 (processo effimero, concorrenza, code o serializzazione dove necessario) senza corruzione di output né interferenza tra job.

## Piattaforma e installazione

- **Windows 10 e Windows 11** (ambiente di riferimento x64, allineato al codebase .NET/WPF).
- **Installazione per utente** tramite hive **HKCU** (menu contestuali e configurazione per utente, senza richiedere servizi amministrativi per l’uso ordinario).

---

## FLIP Setup Manager

Un **unico comando PowerShell** avvia il **Setup Manager interattivo** (bootstrap definitivo in fase **M7**; vedi [Bootstrap e distribuzione](#bootstrap-e-distribuzione)).

### Menu principale

| Voce | Azione |
|------|--------|
| `[1]` | **Installa FLIP** (attivazione automatica del menu contestuale al termine dell’installazione) |
| `[2]` | **Aggiorna FLIP** |
| `[3]` | **Attiva / disattiva menu contestuale** (voce dinamica, vedi sotto) |
| `[4]` | **Ripara FLIP** |
| `[5]` | **Disinstalla FLIP** |
| `[6]` | **Mostra stato FLIP** |
| `[0]` | **Esci** |

### Voce `[3]` dinamica (stessa sessione PowerShell)

Il menu si **aggiorna senza riavviare PowerShell** dopo ogni operazione che cambia lo stato:

| Stato rilevato | Testo della voce `[3]` |
|----------------|-------------------------|
| Menu contestuale **attivo** | Disattiva menu contestuale |
| Menu contestuale **disattivo** | Attiva menu contestuale |
| FLIP **non installato** | Indicazione di eseguire prima `[1] Installa FLIP` (la voce non esegue attivazione finché non c’è installazione) |

---

## Disinstallazione

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

---

## Aggiornamento e ripristino

Per `[2] Aggiorna FLIP` e procedure correlate:

- **Backup** del pacchetto e/o degli artefatti di installazione **prima dell’aggiornamento**.
- **Conservazione** dello stato **attivo/disattivo** del menu contestuale attraverso l’aggiornamento.
- **Ripristino delle chiavi di Registro** originali FLIP in caso di rollback o riparazione.
- **Ripristino della versione precedente** del binario quando il rollback è richiesto e il backup è disponibile.
- **Verifica delle collisioni** con chiavi di registro **estranee** (non sovrascrivere voci non di proprietà FLIP).
- **Nessuna eliminazione** di file personali dell’utente durante update/rollback.
- **Nessuna promessa di atomicità** dell’aggiornamento senza garanzie implementative reali (documentare il comportamento effettivo in M7).

Dettaglio operativo: voce `[4] Ripara FLIP` riallinea registrazione menu, percorsi e integrità installazione senza toccare conversioni utente.

---

## Bootstrap e distribuzione (M7)

Requisiti documentati; **URL pubblico e dominio non ancora scelti** — non usare URL di esempio come se fossero operativi.

### Download e fiducia

- Scarico **HTTPS** del pacchetto di installazione o del bootstrap.
- **Verifica autenticità** del pacchetto (catena di fiducia, editore attendibile).
- **Firma Authenticode** con **controllo dell’editore atteso**.
- **Verifica integrità** (hash pubblicato o incorporato nel processo di release).
- **Gestione download falliti** (ritentativi, messaggi chiari, nessuno stato parziale inconsistente).

### PowerShell

- Compatibilità **Windows PowerShell 5.1** e **PowerShell 7+**.
- Microsoft sconsiglia `Invoke-Expression` su contenuto scaricato: la soluzione definitiva deve offrire **un solo comando di avvio** per l’utente con la **minore esposizione possibile** (es. esecuzione di script firmato, modulo firmato, o avvio di stub locale verificato). La modalità definitiva sarà **stabilita in M7**.

---

## Dipendenze e conversioni

| Motore | Ruolo |
|--------|--------|
| **Conversioni native** | Devono funzionare **senza** obbligo di installare Pandoc, LibreOffice o FFmpeg. |
| **Pandoc, LibreOffice, FFmpeg** | **Opzionali**; abilitano categorie aggiuntive quando presenti e validati. |

- Nel sottomenu contestuale mostrare **solo** conversioni **realmente disponibili**.
- La **distribuzione** di motori esterni (se prevista) richiede in M7: verifica **licenze**, **integrità** e politica **aggiornamenti**.

Riferimento roadmap implementativa: [ROADMAP.md](../ROADMAP.md) (fasi M5.2–M5.5).

---

## Roadmap vincolante (sintesi)

La numerazione e le dipendenze tra fasi sono definite in [ROADMAP.md](../ROADMAP.md).

- **M6** non inizia prima della chiusura di **M5**.
- **M7** non inizia prima della chiusura di **M6**.

---

## Riferimenti

- Decisioni tecniche storiche e definitive: [TECHNICAL_DECISIONS.md](../TECHNICAL_DECISIONS.md)
- Piano di rilascio M5–M7: [ROADMAP.md](../ROADMAP.md)