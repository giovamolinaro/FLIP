# FLIX per Windows

FLIX converte un file sul computer locale dal menu contestuale di Esplora file. Selezioni un file, apri **Converti con FLIX**, scegli un formato e la finestra **FLIX — Conversione** esegue quella conversione. Quando la finestra si chiude, il processo termina. Non c'è un host residente.

Questo albero di lavoro non è una release scaricabile. La fase M7, il Setup Manager, non è iniziata.

## Compilazione

Installa il [.NET 10 SDK per Windows x64](https://dotnet.microsoft.com/download/dotnet/10.0). Dalla radice del repository:

```powershell
.\scripts\Build.ps1 -Output C:\percorso\nuovo\flix
```

Lo script pubblica `Flix.exe` nella cartella indicata. Se quella cartella contiene già `Flip.exe`, lo script si ferma e non cancella nulla. Usa una cartella nuova. Compilare questo branch non produce una release pubblica. La build pubblicata dipende dal framework e richiede il .NET 10 Windows Desktop Runtime x64.

Progetto: `src/Flix/Flix.csproj`. Namespace: `Flix`. Target: `net10.0-windows`, x64. Librerie dirette: PDFtoImage e PdfPig. Vedi [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Uso

1. Seleziona un file in Esplora file.
2. **Converti con FLIX**.
3. Scegli un formato di uscita mostrato dal menu.
4. Osserva la finestra di avanzamento.
5. Il risultato è un file nuovo accanto al sorgente. Un file già presente non viene sovrascritto.

Il comando padre usa `flix-xf.ico`. Le destinazioni usano `flix-menu-*-premium-v2.ico`.

Le installazioni nuove usano gli identificativi `Flix.Convert.*` e il valore proprietario `FlixOwner`. `LegacyIdentity` riconosce le chiavi precedenti scritte da questa applicazione. Non rimuove il menu protetto `*\shell\Flip.Convert` né un'anteprima di un altro proprietario.

`Flix.exe` è accettato da `%LOCALAPPDATA%\Programs\Flix` e, per un layout già presente, da `%LOCALAPPDATA%\Programs\Flip`. I percorsi di laboratorio usano il segmento `Flip-Lab`, perché l'account di collaudo si chiama ancora così. I flag di conversione sono `--convert-menu`, `--convert` e `--files`.

I log diagnostici di questa build stanno in `%LOCALAPPDATA%\Flix\logs`. Una cartella di log precedente non viene eliminata.

## Motori

Le conversioni native di immagini, testo e dati strutturati non richiedono un programma esterno. I menu audio e video si installano solo se FFmpeg è rilevato. I menu documenti si installano se Pandoc è rilevato. LibreOffice è un'installazione opzionale separata, usata quando un documento viene reso in PDF o in un'immagine. FFmpeg, Pandoc e LibreOffice non sono inclusi.

Il menu mostra i formati che i motori rilevati possono offrire. Windows 10 e Windows 11 sono le piattaforme previste. Il controllo visivo dell'icona XF nell'account FlipLab non è stato eseguito.

## Controlli registrati

La build isolata di identità conservata fuori da questo repository, in `FLIX-AUDIT-ARCHIVE-20261124/_local/flix-identity-20261124`, ha superato 175 controlli di input e 107 controlli di conversione. Otto controlli dipendenti da LibreOffice non sono stati eseguiti. Quelle cifre restano di quella build.

La validazione di questa pulizia, il 24 novembre 2026, ha usato un `Flix.exe` isolato e Pandoc 3.12.1 già presente sulla macchina. Controlli di input: 175 superati, 0 falliti. Controlli di conversione: 107 superati, 0 falliti, 8 non eseguiti perché LibreOffice è assente. Il controllo visivo dell'icona XF nell'account FlipLab non è stato eseguito. Dettaglio: [ROADMAP.md](ROADMAP.md).

## Provenienza

Il remoto è ancora `https://github.com/giovamolinaro/FLIP.git`. La rinomina del repository non è stata eseguita. Le release pubblicate su [`thetinyfoundry/FLIP`](https://github.com/thetinyfoundry/FLIP), incluse [FLIP 0.5.1](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.5.1) e [FLIP v0.6.1 AURA Edition](https://github.com/thetinyfoundry/FLIP/releases/tag/v0.6.1), conservano il nome con cui sono state pubblicate. Non sono questo albero di lavoro e non sono una release di questo fork. Vedi [Stato di pubblicazione](docs/PUBLICATION-STATUS.md).

## Licenza

Il file [LICENSE](LICENSE) di questo fork è la licenza MIT. La riga di copyright resta `Copyright (c) 2026 Flip contributors`. Il `LICENSE` AURA di upstream, alle versioni v0.6.0 e v0.6.1, dichiara GPL-3.0-only. Quel testo non rilicenzia questo fork. I componenti di terze parti mantengono le proprie licenze: [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

Le conversioni restano in locale. Rivedi i log prima di condividerli. Vedi [SECURITY.md](SECURITY.md).

[English](README.md) · [Contribuisci](CONTRIBUTING.md) · [Changelog](CHANGELOG.md) · [Architettura](ARCHITECTURE.md) · [Sicurezza](SECURITY.md)
