using System.IO;
using System.Security;
using System.Text;
using Microsoft.Win32;

namespace Flix;

// Menu immagini definitivo. Le destinazioni arrivano da ContextMenuCatalog (Policy.Outputs).
// JPG/JPEG e TIF/TIFF condividono l'albero dei comandi: il padre dell'alias punta al canonico.
// Non sostituisce il menu originale protetto né l'anteprima: chiavi e proprietario distinti.
// Nessuna scrittura finché piano, icone e proprietari non sono stati verificati per intero.
// HKCU non è transazionale: in caso di errore si tenta il ripristino e se ne controlla l'esito.
// Il ripristino può fallire e lasciare il Registro parziale.
// --install-context-menu-images e --remove-context-menu-images passano da ApplyImages.
// I test iniettano IRegistryStore. Il codice 0 vale solo se l'operazione richiesta è terminata.
// --install-context-menu-png e --remove-context-menu-png restano il solo menu PNG, con le sei destinazioni.
// Menu PDF, TXT e dati: stesso perimetro di backup, proprietario e percorsi dei menu immagini.
// .yml punta a .yaml con ExtendedSubCommandsKey e non duplica i comandi.
// Le destinazioni seguono lo snapshot dei motori. La rimozione usa l'insieme massimo (Pandoc)
// e cancella solo le chiavi il cui proprietario è di questa applicazione.
// HKCU non è transazionale: in caso di errore si tenta il ripristino e se ne controlla l'esito.
// Il ripristino può fallire e lasciare il Registro parziale.
// --install-context-menu-pdf-text-data e --remove-context-menu-pdf-text-data passano da ApplyContent.
// Il metodo Install dei menu immagini continua a rifiutare queste chiavi.
// Menu audio e video: le destinazioni arrivano dal catalogo, quindi da Policy.Outputs.
// Senza FFmpeg il piano è vuoto e l'installazione viene rifiutata prima di scrivere.
// Pandoc e LibreOffice non aggiungono voci. Nessun alias.
// L'installazione controlla le icone premium-v2. La rimozione usa l'insieme massimo
// e cancella solo le chiavi il cui proprietario è di questa applicazione, anche se le icone mancano.
// HKCU non è transazionale: in caso di errore si tenta il ripristino e se ne controlla l'esito.
// Il ripristino può fallire e lasciare il Registro parziale.
// --install-context-menu-audio-video e --remove-context-menu-audio-video passano da ApplyMedia.
// Menu documenti: le destinazioni arrivano dal catalogo, quindi da Policy.Outputs.
// Senza Pandoc il piano è vuoto e l'installazione viene rifiutata prima di scrivere.
// LibreOffice abilita PDF e immagini; FFmpeg non aggiunge voci.
// .markdown punta a .md e .htm punta a .html, con ExtendedSubCommandsKey.
// L'installazione controlla le icone premium-v2. La rimozione usa l'insieme massimo
// (Pandoc e LibreOffice considerati presenti) e cancella solo le chiavi il cui proprietario è di questa applicazione,
// anche se Pandoc, LibreOffice o le icone mancano.
// I padri SystemFileAssociations\{ext} e \shell creati dall'installazione sono registrati
// sul menu di proprietà e rimossi solo se, dopo i comandi FLIX, restano senza valori e senza sottochiavi.
// HKCU non è transazionale: in caso di errore si tenta il ripristino e se ne controlla l'esito.
// Il ripristino può fallire e lasciare il Registro parziale.
// --install-context-menu-documents e --remove-context-menu-documents passano da ApplyDocuments.
internal static class ContextMenuNext
{
    internal const string MenuIdPrefix = "Flix.Convert.";
    internal const string MenuId = "Flix.Convert.Png";
    internal const string ParentVerb = "Converti con FLIX";
    internal const string ParentKey = @"Software\Classes\SystemFileAssociations\.png\shell\Flix.Convert.Png";
    internal const string SubCommandsPath = @"SystemFileAssociations\.png\shell\Flix.Convert.Png";
    internal const string OwnerValue = "FlixOwner";
    internal const string ParentIconFileName = "flix-xf.ico";
    internal const string ExecutableFileName = "Flix.exe";
    internal const string LaboratorySegment = LegacyIdentity.LaboratoryPathSegment;
    internal const string InstallImagesCommand = "--install-context-menu-images";
    internal const string RemoveImagesCommand = "--remove-context-menu-images";
    internal const string InstallContentCommand = "--install-context-menu-pdf-text-data";
    internal const string RemoveContentCommand = "--remove-context-menu-pdf-text-data";
    internal const string InstallMediaCommand = "--install-context-menu-audio-video";
    internal const string RemoveMediaCommand = "--remove-context-menu-audio-video";
    internal const string InstallDocumentsCommand = "--install-context-menu-documents";
    internal const string RemoveDocumentsCommand = "--remove-context-menu-documents";
    internal const string DocumentAncestorValue = "FlixCreatedAncestors";
    internal static IReadOnlyList<string> Formats =>
        ContextMenuCatalog.ForExtension(".png", new ContextMenuCatalog.EngineSnapshot(false, false, false))
            .Select(entry => entry.TargetFormat)
            .ToArray();
    internal static IReadOnlyList<string> ImageExtensions => Policy.Images;
    internal static IReadOnlyList<string> ContentExtensions { get; } =
        [".pdf", ".txt", ".json", ".yaml", ".yml", ".xml", ".csv"];
    internal static IReadOnlyList<string> MediaExtensions { get; } =
        Policy.Audio.Concat(Policy.Video).ToArray();
    internal static IReadOnlyList<string> DocumentExtensions { get; } =
        [".md", ".markdown", ".html", ".htm", ".docx", ".odt", ".rtf", ".epub", ".rst", ".fb2"];
    static readonly ContextMenuCatalog.EngineSnapshot MaximumDocumentEngines = new(true, true, true);
    internal static readonly string[] MediaIconFormats =
    [
        "MP3", "WAV", "FLAC", "OGG", "M4A", "AAC",
        "MP4", "MKV", "WEBM", "AVI", "MOV"
    ];
    static readonly ContextMenuCatalog.EngineSnapshot MaximumMediaEngines = new(true, true, true);

    static readonly string StableProgramsFlix = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "Flix");

    static readonly string LegacyProgramsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", LegacyIdentity.ProgramsFolderName);

    internal static bool IsPlannedCommand(string[] args) =>
        args.Contains("--install-context-menu-png") || args.Contains("--remove-context-menu-png");

    internal static bool IsImageMenuCommand(string[] args) =>
        args.Contains(InstallImagesCommand) || args.Contains(RemoveImagesCommand);

    internal static bool IsContentMenuCommand(string[] args) =>
        args.Contains(InstallContentCommand) || args.Contains(RemoveContentCommand);

    internal static bool IsMediaMenuCommand(string[] args) =>
        args.Contains(InstallMediaCommand) || args.Contains(RemoveMediaCommand);

    internal static bool IsDocumentMenuCommand(string[] args) =>
        args.Contains(InstallDocumentsCommand) || args.Contains(RemoveDocumentsCommand);

    internal static int ApplyMedia(string[] args)
    {
        if (!TryIsolateMediaCommand(args, out _, out string error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        string executable;
        try { executable = RequireMediaRegistryExecutable(Environment.ProcessPath); }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        return ApplyMedia(args, executable, new CurrentUserRegistryStore(), ContextMenuCatalog.EngineSnapshot.Detected, Console.Error);
    }

    internal static int ApplyMedia(string[] args, string executable, IRegistryStore store, ContextMenuCatalog.EngineSnapshot engines, TextWriter error)
    {
        if (!TryIsolateMediaCommand(args, out bool remove, out string errorText))
        {
            error.WriteLine(errorText);
            return 1;
        }
        try
        {
            if (remove) RemoveMediaMenus(executable, store);
            else InstallMediaMenus(executable, engines, store);
            return 0;
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    internal static bool TryIsolateMediaCommand(string[] args, out bool remove, out string error)
    {
        remove = false;
        bool install = args.Contains(InstallMediaCommand);
        bool removing = args.Contains(RemoveMediaCommand);
        if (install && removing)
        {
            error = "Installazione e rimozione del menu audio e video non possono essere richieste insieme. Nessuna modifica al Registro.";
            return false;
        }
        if (!install && !removing)
        {
            error = "Comando del menu audio e video assente.";
            return false;
        }
        string expected = install ? InstallMediaCommand : RemoveMediaCommand;
        string? extra = args.FirstOrDefault(arg => !arg.Equals(expected, StringComparison.Ordinal));
        if (extra != null)
        {
            error = "Il comando " + expected + " non è compatibile con \"" + extra + "\". Nessuna modifica al Registro.";
            return false;
        }
        remove = removing;
        error = "";
        return true;
    }

    internal static int ApplyDocuments(string[] args)
    {
        if (!TryIsolateDocumentCommand(args, out _, out string error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        string executable;
        try { executable = RequireDocumentRegistryExecutable(Environment.ProcessPath); }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        return ApplyDocuments(args, executable, new CurrentUserRegistryStore(), ContextMenuCatalog.EngineSnapshot.Detected, Console.Error);
    }

    internal static int ApplyDocuments(string[] args, string executable, IRegistryStore store, ContextMenuCatalog.EngineSnapshot engines, TextWriter error)
    {
        if (!TryIsolateDocumentCommand(args, out bool remove, out string errorText))
        {
            error.WriteLine(errorText);
            return 1;
        }
        try
        {
            if (remove) RemoveDocumentMenus(executable, store);
            else InstallDocumentMenus(executable, engines, store);
            return 0;
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    internal static bool TryIsolateDocumentCommand(string[] args, out bool remove, out string error)
    {
        remove = false;
        bool install = args.Contains(InstallDocumentsCommand);
        bool removing = args.Contains(RemoveDocumentsCommand);
        if (install && removing)
        {
            error = "Installazione e rimozione del menu documenti non possono essere richieste insieme. Nessuna modifica al Registro.";
            return false;
        }
        if (!install && !removing)
        {
            error = "Comando del menu documenti assente.";
            return false;
        }
        string expected = install ? InstallDocumentsCommand : RemoveDocumentsCommand;
        string? extra = args.FirstOrDefault(arg => !arg.Equals(expected, StringComparison.Ordinal));
        if (extra != null)
        {
            error = "Il comando " + expected + " non è compatibile con \"" + extra + "\". Nessuna modifica al Registro.";
            return false;
        }
        remove = removing;
        error = "";
        return true;
    }

    internal static int ApplyImages(string[] args)
    {
        if (!TryIsolateImageCommand(args, out _, out string error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        string executable;
        try { executable = RequireImageRegistryExecutable(Environment.ProcessPath); }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        return ApplyImages(args, executable, new CurrentUserRegistryStore(), ContextMenuCatalog.EngineSnapshot.Detected, Console.Error);
    }

    internal static int ApplyImages(string[] args, string executable, IRegistryStore store, ContextMenuCatalog.EngineSnapshot engines, TextWriter error)
    {
        if (!TryIsolateImageCommand(args, out bool remove, out string errorText))
        {
            error.WriteLine(errorText);
            return 1;
        }
        try
        {
            if (remove) RemoveImageMenus(executable, store);
            else InstallImageMenus(executable, engines, store);
            return 0;
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    internal static bool TryIsolateImageCommand(string[] args, out bool remove, out string error)
    {
        remove = false;
        bool install = args.Contains(InstallImagesCommand);
        bool removing = args.Contains(RemoveImagesCommand);
        if (install && removing)
        {
            error = "Installazione e rimozione del menu immagini non possono essere richieste insieme. Nessuna modifica al Registro.";
            return false;
        }
        if (!install && !removing)
        {
            error = "Comando del menu immagini assente.";
            return false;
        }
        string expected = install ? InstallImagesCommand : RemoveImagesCommand;
        string? extra = args.FirstOrDefault(arg => !arg.Equals(expected, StringComparison.Ordinal));
        if (extra != null)
        {
            error = "Il comando " + expected + " non è compatibile con \"" + extra + "\". Nessuna modifica al Registro.";
            return false;
        }
        remove = removing;
        error = "";
        return true;
    }

    internal static int ApplyContent(string[] args)
    {
        if (!TryIsolateContentCommand(args, out _, out string error))
        {
            Console.Error.WriteLine(error);
            return 1;
        }
        string executable;
        try { executable = RequireContentRegistryExecutable(Environment.ProcessPath); }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        return ApplyContent(args, executable, new CurrentUserRegistryStore(), ContextMenuCatalog.EngineSnapshot.Detected, Console.Error);
    }

    internal static int ApplyContent(string[] args, string executable, IRegistryStore store, ContextMenuCatalog.EngineSnapshot engines, TextWriter error)
    {
        if (!TryIsolateContentCommand(args, out bool remove, out string errorText))
        {
            error.WriteLine(errorText);
            return 1;
        }
        try
        {
            if (remove) RemoveContentMenus(executable, store);
            else InstallContentMenus(executable, engines, store);
            return 0;
        }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            error.WriteLine(ex.Message);
            return 1;
        }
    }

    internal static bool TryIsolateContentCommand(string[] args, out bool remove, out string error)
    {
        remove = false;
        bool install = args.Contains(InstallContentCommand);
        bool removing = args.Contains(RemoveContentCommand);
        if (install && removing)
        {
            error = "Installazione e rimozione del menu PDF, TXT e dati non possono essere richieste insieme. Nessuna modifica al Registro.";
            return false;
        }
        if (!install && !removing)
        {
            error = "Comando del menu PDF, TXT e dati assente.";
            return false;
        }
        string expected = install ? InstallContentCommand : RemoveContentCommand;
        string? extra = args.FirstOrDefault(arg => !arg.Equals(expected, StringComparison.Ordinal));
        if (extra != null)
        {
            error = "Il comando " + expected + " non è compatibile con \"" + extra + "\". Nessuna modifica al Registro.";
            return false;
        }
        remove = removing;
        error = "";
        return true;
    }

    internal static string CommandLine(string executable, string format) =>
        ContextMenuPreview.CommandLine(executable, format);

    internal static string IconFileName(string format)
    {
        if (format.Equals("JPG", StringComparison.Ordinal)
            || format.Equals("PDF", StringComparison.Ordinal)
            || format.Equals("WEBP", StringComparison.Ordinal))
            return ContextMenuPreview.IconFileName(format);
        return format switch
        {
            "PNG" => "flix-menu-png-premium-v2.ico",
            "BMP" => "flix-menu-bmp-premium-v2.ico",
            "TIFF" => "flix-menu-tiff-premium-v2.ico",
            "GIF" => "flix-menu-gif-premium-v2.ico",
            "TXT" or "HTML" or "MD" or "JSON" or "YAML" or "XML" or "CSV"
                or "DOCX" or "EPUB" or "ODT" or "RTF" or "FB2" or "RST"
                => "flix-menu-" + format.ToLowerInvariant() + "-premium-v2.ico",
            "MP3" or "WAV" or "FLAC" or "OGG" or "M4A" or "AAC"
                or "MP4" or "MKV" or "WEBM" or "AVI" or "MOV"
                => "flix-menu-" + format.ToLowerInvariant() + "-premium-v2.ico",
            _ => throw new IOException("Nessuna icona prevista per il formato " + format + ".")
        };
    }

    internal static string MenuIdFor(string extension)
    {
        string canonical = CanonicalImageExtension(NormalizeImageExtension(extension));
        if (canonical.Equals(".png", StringComparison.Ordinal)) return MenuId;
        string stem = canonical.TrimStart('.');
        return MenuIdPrefix + char.ToUpperInvariant(stem[0]) + stem[1..];
    }

    internal static string ParentKeyFor(string extension)
    {
        string normalized = NormalizeImageExtension(extension);
        if (normalized.Equals(".png", StringComparison.Ordinal)) return ParentKey;
        return @"Software\Classes\SystemFileAssociations\" + normalized + @"\shell\" + MenuIdFor(normalized);
    }

    internal static string SubCommandsPathFor(string extension)
    {
        string canonical = CanonicalImageExtension(NormalizeImageExtension(extension));
        const string prefix = @"Software\Classes\";
        string parent = ParentKeyFor(canonical);
        if (!parent.StartsWith(prefix, StringComparison.Ordinal))
            throw new IOException("Percorso comandi non valido.");
        return parent[prefix.Length..];
    }

    internal static ImageMenuBlueprint[] PlanImageMenus(ContextMenuCatalog.EngineSnapshot engines)
    {
        var blueprints = new ImageMenuBlueprint[ImageExtensions.Count];
        for (int index = 0; index < ImageExtensions.Count; index++)
        {
            string extension = ImageExtensions[index];
            var entries = ContextMenuCatalog.ForExtension(extension, engines);
            if (entries.Length != 6)
                throw new IOException("Il catalogo non offre sei conversioni per " + extension + ".");
            string canonical = CanonicalImageExtension(extension);
            bool shared = !canonical.Equals(extension, StringComparison.Ordinal);
            if (shared)
            {
                string[] aliasFormats = entries.Select(entry => entry.TargetFormat).ToArray();
                string[] canonicalFormats = ContextMenuCatalog.ForExtension(canonical, engines).Select(entry => entry.TargetFormat).ToArray();
                if (!aliasFormats.SequenceEqual(canonicalFormats))
                    throw new IOException("Le estensioni equivalenti non condividono le destinazioni: " + extension + ".");
            }
            blueprints[index] = new ImageMenuBlueprint(
                extension,
                MenuIdFor(extension),
                ParentKeyFor(extension),
                SubCommandsPathFor(extension),
                ParentKeyFor(canonical),
                shared,
                entries);
        }
        if (blueprints.Select(item => item.ParentKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != blueprints.Length)
            throw new IOException("Chiave padre duplicata nel menu immagini.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var blueprint in blueprints)
        {
            foreach (string key in PlannedKeys(blueprint))
                if (!seen.Add(key)) throw new IOException("Chiave duplicata nel menu immagini: " + key);
        }
        return blueprints;
    }

    internal static IReadOnlyList<string> PlannedKeys(ImageMenuBlueprint blueprint)
    {
        if (blueprint.SharesCommandTree) return [blueprint.ParentKey];
        var keys = new List<string> { blueprint.ParentKey, blueprint.ParentKey + @"\shell" };
        foreach (string format in blueprint.Formats)
        {
            string verb = Verb(blueprint.ParentKey, format);
            keys.Add(verb);
            keys.Add(verb + @"\command");
        }
        return keys;
    }

    internal static bool IconsReady(string executable, IReadOnlyList<string> formats)
    {
        if (formats.Count == 0) return false;
        string? directory = Path.GetDirectoryName(Path.GetFullPath(executable));
        if (string.IsNullOrEmpty(directory)) return false;
        foreach (string format in formats)
        {
            string icon = Path.GetFullPath(Path.Combine(directory, IconFileName(format)));
            if (!ContextMenuPreview.IconIsValid(icon)) return false;
        }
        return ParentIconReady(executable);
    }

    internal static bool ParentIconReady(string executable)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(executable));
        if (string.IsNullOrEmpty(directory)) return false;
        return ContextMenuPreview.ParentIconIsValid(Path.GetFullPath(Path.Combine(directory, ParentIconFileName)));
    }

    static string ParentIconPath(string executable)
    {
        string directory = Path.GetDirectoryName(Path.GetFullPath(executable))
            ?? throw new IOException("Percorso eseguibile assente.");
        return Path.GetFullPath(Path.Combine(directory, ParentIconFileName));
    }

    internal static bool ContentIconsReady(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        string[] formats = PlanContentMenus(engines)
            .Where(plan => !plan.SharesCommandTree)
            .SelectMany(plan => plan.Formats)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return IconsReady(executable, formats);
    }

    internal static bool MediaIconsReady(string executable) =>
        IconsReady(executable, MediaIconFormats);

    internal static bool DocumentIconsReady(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        string[] formats = PlanDocumentMenus(engines)
            .Where(plan => !plan.SharesCommandTree)
            .SelectMany(plan => plan.Formats)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return IconsReady(executable, formats);
    }

    internal static void RegisterContentMenu(ContentMenuBlueprint blueprint, string executable, IRegistryStore store)
    {
        ValidateRegistrableExecutable(executable);
        string full = Path.GetFullPath(executable);
        if (!IconsReady(full, blueprint.Formats))
            throw new IOException("Icone non disponibili: nessuna chiave del menu è stata registrata.");
        InstallContentPlans([BuildContentPlan(full, blueprint)], store);
    }

    internal static void RegisterImageMenu(ImageMenuBlueprint blueprint, string executable, IRegistryStore store)
    {
        ValidateRegistrableExecutable(executable);
        if (!IconsReady(executable, blueprint.Formats))
            throw new IOException("Icone non disponibili: nessuna chiave del menu è stata registrata.");
        Install(BuildPlan(executable, blueprint), store);
    }

    internal static ContextMenuPlan CreatePlan(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        ValidateRegistrableExecutable(executable);
        var png = PlanImageMenus(engines).Single(item => item.Extension.Equals(".png", StringComparison.Ordinal));
        return BuildPlan(executable, png);
    }

    internal static ContextMenuPlan[] CreateImagePlans(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        ValidateRegistrableExecutable(executable);
        var plans = PlanImageMenus(engines).Select(blueprint => BuildPlan(executable, blueprint)).ToArray();
        VerifyImagePlans(plans);
        return plans;
    }

    static void WritePlanValues(ContextMenuPlan plan, IRegistryStore store)
    {
        foreach (var value in plan.Values) store.Write(value.Key, value.Name, value.Data);
        string owner = plan.Value(RootOf(plan), OwnerValue);
        foreach (string key in plan.Keys)
            LegacyIdentity.RetireOwnerMarker(store, key, owner);
    }

    internal static void InstallImageMenus(string executable, ContextMenuCatalog.EngineSnapshot engines, IRegistryStore store)
    {
        string full = RequireImageRegistryExecutable(executable);
        var plans = CreateImagePlans(full, engines);
        if (!IconsReady(full, plans.SelectMany(plan => plan.Formats).Distinct(StringComparer.Ordinal).ToArray()))
            throw new IOException("Icone non disponibili: nessuna chiave del menu è stata registrata.");
        foreach (var plan in plans) GuardForeignMenus(plan, store);
        var backups = plans.Select(plan => CaptureBackup(plan, store)).ToArray();
        CommitImageChanges(backups, store, () =>
        {
            RemoveLegacyOwned(plans, store);
            foreach (var plan in plans)
                WritePlanValues(plan, store);
        });
    }

    internal static void RemoveImageMenus(IRegistryStore store) =>
        RemoveImageMenus(Path.Combine(StableProgramsFlix, ExecutableFileName), store);

    internal static void RemoveImageMenus(string executable, IRegistryStore store)
    {
        string full = RequireImageRegistryExecutable(executable);
        var plans = CreateImagePlans(full, new ContextMenuCatalog.EngineSnapshot(false, false, false));
        var backups = plans.Select(plan => CaptureBackup(plan, store)).ToArray();
        CommitImageChanges(backups, store, () =>
        {
            RemoveLegacyOwned(plans, store);
            var owned = new List<(string Key, string Owner)>();
            foreach (var plan in plans)
            {
                string root = RootOf(plan);
                string owner = plan.Value(root, OwnerValue);
                foreach (string key in plan.Keys) owned.Add((key, owner));
            }
            foreach (var item in owned.OrderByDescending(item => item.Key.Count(character => character == '\\')))
            {
                if (!store.KeyExists(item.Key) || !LegacyIdentity.MarkerMatches(store, item.Key, item.Owner)) continue;
                if (store.SubKeys(item.Key).Count > 0) continue;
                store.DeleteKey(item.Key);
            }
        });
    }

    internal static void InstallContentMenus(string executable, ContextMenuCatalog.EngineSnapshot engines, IRegistryStore store)
    {
        string full = RequireContentRegistryExecutable(executable);
        var plans = CreateContentPlans(full, engines);
        if (!ContentIconsReady(full, engines))
            throw new IOException("Icone non disponibili: nessuna chiave del menu è stata registrata.");
        InstallContentPlans(plans, store);
    }

    internal static void RemoveContentMenus(IRegistryStore store) =>
        RemoveContentMenus(Path.Combine(StableProgramsFlix, ExecutableFileName), store);

    internal static void RemoveContentMenus(string executable, IRegistryStore store)
    {
        string full = RequireContentRegistryExecutable(executable);
        var plans = CreateContentPlans(full, new ContextMenuCatalog.EngineSnapshot(true, false, false));
        var backups = plans.Select(plan => CaptureBackup(plan, store)).ToArray();
        CommitImageChanges(backups, store, () =>
        {
            RemoveLegacyOwned(plans, store);
            var owned = new List<(string Key, string Owner)>();
            foreach (var plan in plans)
            {
                string root = RootOf(plan);
                string owner = plan.Value(root, OwnerValue);
                foreach (string key in plan.Keys) owned.Add((key, owner));
            }
            foreach (var item in owned.OrderByDescending(item => item.Key.Count(character => character == '\\')))
            {
                if (!store.KeyExists(item.Key) || !LegacyIdentity.MarkerMatches(store, item.Key, item.Owner)) continue;
                if (store.SubKeys(item.Key).Count > 0) continue;
                store.DeleteKey(item.Key);
            }
        });
    }

    internal static void InstallMediaMenus(string executable, ContextMenuCatalog.EngineSnapshot engines, IRegistryStore store)
    {
        string full = RequireMediaRegistryExecutable(executable);
        var blueprints = PlanMediaMenus(engines);
        if (!engines.Ffmpeg)
            throw new IOException("FFmpeg assente: nessun menu audio o video è installabile. Nessuna chiave è stata scritta.");
        if (blueprints.Length != MediaExtensions.Count)
            throw new IOException("Il piano audio e video non è completo. Nessuna chiave è stata scritta.");
        var plans = CreateMediaPlans(full, engines);
        if (!MediaIconsReady(full))
            throw new IOException("Icone non disponibili: nessuna chiave del menu è stata registrata.");
        InstallMediaPlans(plans, store);
    }

    internal static void RemoveMediaMenus(IRegistryStore store) =>
        RemoveMediaMenus(Path.Combine(StableProgramsFlix, ExecutableFileName), store);

    internal static void RemoveMediaMenus(string executable, IRegistryStore store)
    {
        string full = RequireMediaRegistryExecutable(executable);
        var plans = CreateMediaPlans(full, MaximumMediaEngines);
        var backups = plans.Select(plan => CaptureBackup(plan, store)).ToArray();
        CommitImageChanges(backups, store, () =>
        {
            RemoveLegacyOwned(plans, store);
            var owned = new List<(string Key, string Owner)>();
            foreach (var plan in plans)
            {
                string root = RootOf(plan);
                string owner = plan.Value(root, OwnerValue);
                foreach (string key in plan.Keys) owned.Add((key, owner));
            }
            foreach (var item in owned.OrderByDescending(item => item.Key.Count(character => character == '\\')))
            {
                if (!store.KeyExists(item.Key) || !LegacyIdentity.MarkerMatches(store, item.Key, item.Owner)) continue;
                if (store.SubKeys(item.Key).Count > 0) continue;
                store.DeleteKey(item.Key);
            }
        });
    }

    internal static ContextMenuPlan[] CreateMediaPlans(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        ValidateRegistrableExecutable(executable);
        var plans = PlanMediaMenus(engines).Select(blueprint => BuildMediaPlan(executable, blueprint)).ToArray();
        VerifyMediaPlans(plans);
        return plans;
    }

    internal static ContextMenuPlan[] CreateContentPlans(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        ValidateRegistrableExecutable(executable);
        var plans = PlanContentMenus(engines).Select(blueprint => BuildContentPlan(executable, blueprint)).ToArray();
        VerifyContentPlans(plans);
        return plans;
    }

    internal static void EnsureIcons(ContextMenuPlan plan)
    {
        if (!ParentIconReady(plan.Executable))
            throw new IOException("Icona XF del menu padre assente o non valida: " + ParentIconFileName);
        foreach (string format in plan.Formats)
        {
            if (!ContextMenuPreview.IconIsValid(plan.Icon(format)))
                throw new IOException("Icona del menu PNG assente o non valida: " + plan.Icon(format));
        }
    }

    internal static ContextMenuRegistryBackup CaptureBackup(ContextMenuPlan plan, IRegistryStore store) =>
        ContextMenuRegistryBackup.Capture(plan, store);

    internal static void Install(ContextMenuPlan plan, IRegistryStore store)
    {
        GuardForeignMenus(plan, store);
        var backup = CaptureBackup(plan, store);
        try
        {
            RemoveLegacyOwned([plan], store);
            WritePlanValues(plan, store);
        }
        catch (IOException ex)
        {
            try { RestoreBackup(backup, store); }
            catch (IOException restoreEx)
            {
                throw new IOException("Registrazione interrotta e ripristino incompleto. L'operazione non è atomica. " + restoreEx.Message, ex);
            }
            throw;
        }
    }

    internal static void Remove(IRegistryStore store)
    {
        foreach (string key in OwnedKeyPaths().OrderByDescending(path => path.Count(character => character == '\\')))
        {
            if (!store.KeyExists(key) || !LegacyIdentity.MarkerMatches(store, key, MenuId)) continue;
            if (store.SubKeys(key).Count > 0) continue;
            store.DeleteKey(key);
        }
        string legacyOwner = LegacyMenuId(MenuId);
        foreach (string key in OwnedKeyPaths().Select(path => LegacyKeyFor(path, MenuId)).OrderByDescending(path => path.Count(character => character == '\\')))
        {
            if (IsLegacyMenuProtected(key)) continue;
            if (!store.KeyExists(key) || !LegacyIdentity.MarkerMatches(store, key, legacyOwner)) continue;
            if (store.SubKeys(key).Count > 0) continue;
            store.DeleteKey(key);
        }
    }

    internal static void Apply(bool remove)
    {
        string executable = Environment.ProcessPath ?? throw new IOException("Percorso eseguibile assente.");
        Apply(remove, executable, new CurrentUserRegistryStore());
        Evidence.Log(remove ? "shell.png.remove" : "shell.png.install", executable);
    }

    internal static void Apply(bool remove, string executable, IRegistryStore store)
    {
        ValidateRegistrableExecutable(executable);
        if (remove)
        {
            Remove(store);
            return;
        }
        var plan = CreatePlan(executable, ContextMenuCatalog.EngineSnapshot.Detected);
        EnsureIcons(plan);
        Install(plan, store);
    }

    internal static void RestoreBackup(ContextMenuRegistryBackup backup, IRegistryStore store)
    {
        var created = backup.Entries.Where(entry => !entry.Existed)
            .OrderByDescending(entry => entry.Path.Count(character => character == '\\'))
            .ToArray();
        for (int pass = 0; pass < created.Length; pass++)
        {
            bool removed = false;
            foreach (var entry in created)
            {
                if (!store.KeyExists(entry.Path)) continue;
                string? owner = store.Read(entry.Path, OwnerValue);
                string? historical = store.Read(entry.Path, LegacyIdentity.OwnerValueName);
                if (owner != null && !owner.Equals(backup.Owner, StringComparison.Ordinal)) continue;
                if (historical != null && !historical.Equals(backup.Owner, StringComparison.Ordinal)) continue;
                if (store.SubKeys(entry.Path).Count > 0) continue;
                store.DeleteKey(entry.Path);
                removed = true;
            }
            if (!removed) break;
        }
        foreach (var entry in backup.Entries)
        {
            if (!entry.Existed) continue;
            foreach (var pair in entry.Values)
                store.Write(entry.Path, pair.Key, pair.Value);
            if (!store.KeyExists(entry.Path)) continue;
            foreach (string name in entry.AbsentNames)
                store.DeleteValue(entry.Path, name);
        }
    }

    internal static string VerbKey(string format) => Verb(ParentKey, format);

    internal static string RequireImageRegistryExecutable(string? executable) =>
        RequireApprovedExecutable(executable, "Il menu immagini accetta solo il percorso stabile o un laboratorio " + LaboratorySegment + " già previsto.");

    internal static string RequireContentRegistryExecutable(string? executable) =>
        RequireApprovedExecutable(executable, "Il menu PDF, TXT e dati accetta solo il percorso stabile o un laboratorio " + LaboratorySegment + " già previsto.");

    internal static string RequireMediaRegistryExecutable(string? executable) =>
        RequireApprovedExecutable(executable, "Il menu audio e video accetta solo il percorso stabile o un laboratorio " + LaboratorySegment + " già previsto.");

    internal static string RequireDocumentRegistryExecutable(string? executable) =>
        RequireApprovedExecutable(executable, "Il menu documenti accetta solo il percorso stabile o un laboratorio " + LaboratorySegment + " già previsto.");

    static string RequireApprovedExecutable(string? executable, string rejectedPath)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new IOException("Percorso eseguibile assente.");
        string full;
        try { full = Path.GetFullPath(executable); }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException or SecurityException)
        {
            throw new IOException("Percorso eseguibile non valido.", ex);
        }
        ValidateRegistrableExecutable(full);
        if (!IsStableInstallPath(full) && !IsLaboratoryPath(full))
            throw new IOException(rejectedPath);
        return full;
    }

    internal static void ValidateRegistrableExecutable(string executable)
    {
        ContextMenuPreview.ValidateExecutable(executable);
        string full = Path.GetFullPath(executable);
        if (PathUsesBuildArtifact(full))
            throw new IOException("Non registrare Flix.exe da cartelle bin o obj.");
        if (IsUnauthorizedTemporaryBuild(full) && !IsLaboratoryPath(full) && !IsStableInstallPath(full))
            throw new IOException("Build temporanea non autorizzata per il menu contestuale.");
    }

    internal static bool IsStableInstallPath(string executable)
    {
        try
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(executable)) ?? "";
            string flixDir = Path.GetFullPath(StableProgramsFlix);
            string legacyDir = Path.GetFullPath(LegacyProgramsDirectory);
            return dir.Equals(flixDir, StringComparison.OrdinalIgnoreCase)
                || dir.Equals(legacyDir, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (ArgumentException) { return false; }
    }

    internal static bool IsLaboratoryPath(string executable) =>
        Path.GetFullPath(executable).Contains(LaboratorySegment, StringComparison.OrdinalIgnoreCase);

    static ContextMenuPlan BuildPlan(string executable, ImageMenuBlueprint blueprint)
    {
        if (blueprint.SharesCommandTree)
        {
            var pointer = new List<RegistryValue>
            {
                new(blueprint.ParentKey, "MUIVerb", ParentVerb),
                new(blueprint.ParentKey, "Icon", ParentIconPath(executable)),
                new(blueprint.ParentKey, "ExtendedSubCommandsKey", blueprint.SubCommandsPath),
                new(blueprint.ParentKey, "MultiSelectModel", "Single"),
                new(blueprint.ParentKey, "NeverDefault", ""),
                new(blueprint.ParentKey, OwnerValue, blueprint.MenuId)
            };
            return new ContextMenuPlan(executable, blueprint.Formats.ToArray(), pointer, format => Verb(blueprint.CommandParentKey, format));
        }
        string directory = Path.GetDirectoryName(executable) ?? throw new IOException("Percorso eseguibile assente.");
        string parentKey = blueprint.ParentKey;
        var values = new List<RegistryValue>
        {
            new(parentKey, "MUIVerb", ParentVerb),
            new(parentKey, "Icon", ParentIconPath(executable)),
            new(parentKey, "ExtendedSubCommandsKey", blueprint.SubCommandsPath),
            new(parentKey, "MultiSelectModel", "Single"),
            new(parentKey, "NeverDefault", ""),
            new(parentKey, OwnerValue, blueprint.MenuId),
            new(parentKey + @"\shell", OwnerValue, blueprint.MenuId)
        };
        foreach (var entry in blueprint.Entries)
        {
            string verb = Verb(parentKey, entry.TargetFormat);
            values.Add(new(verb, "MUIVerb", entry.Title));
            values.Add(new(verb, "Icon", Path.GetFullPath(Path.Combine(directory, IconFileName(entry.TargetFormat)))));
            values.Add(new(verb, "MultiSelectModel", "Single"));
            values.Add(new(verb, "NeverDefault", ""));
            values.Add(new(verb, OwnerValue, blueprint.MenuId));
            values.Add(new(verb + @"\command", "", CommandLine(executable, entry.TargetFormat)));
            values.Add(new(verb + @"\command", OwnerValue, blueprint.MenuId));
        }
        return new ContextMenuPlan(executable, blueprint.Formats.ToArray(), values, format => Verb(parentKey, format));
    }

    static ContextMenuPlan BuildContentPlan(string executable, ContentMenuBlueprint blueprint)
    {
        var values = DescribeContentMenu(blueprint, executable);
        return new ContextMenuPlan(
            Path.GetFullPath(executable),
            blueprint.Formats.ToArray(),
            values,
            format => Verb(blueprint.CommandParentKey, format));
    }

    static ContextMenuPlan BuildMediaPlan(string executable, MediaMenuBlueprint blueprint)
    {
        var values = DescribeMediaMenu(blueprint, executable);
        return new ContextMenuPlan(
            Path.GetFullPath(executable),
            blueprint.Formats.ToArray(),
            values,
            format => Verb(blueprint.CommandParentKey, format));
    }

    static void VerifyContentPlans(IReadOnlyList<ContextMenuPlan> plans)
    {
        var roots = new HashSet<string>(plans.Select(RootOf), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in plans)
        {
            string root = RootOf(plan);
            EnsureParentIcon(plan, root);
            if (!IsContentMenuKey(root))
                throw new IOException("Il menu PDF, TXT e dati uscirebbe dal perimetro previsto.");
            foreach (string key in plan.Keys)
            {
                if (!seen.Add(key)) throw new IOException("Chiave duplicata nel menu PDF, TXT e dati: " + key);
                if (!IsContentMenuKey(key) || !key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Il menu PDF, TXT e dati uscirebbe dal perimetro previsto.");
                if (IsProtectedMenuKey(key))
                    throw new IOException("Il menu PDF, TXT e dati non può modificare menu esistenti.");
            }
            if (plan.Values.Any(value => value.Key.Equals(root, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0)
                || plan.Keys.Contains(root + @"\command"))
                throw new IOException("Il menu padre non esegue Flix.exe.");
            string subCommands = plan.Value(root, "ExtendedSubCommandsKey");
            if (!roots.Contains(@"Software\Classes\" + subCommands))
                throw new IOException("Il sottomenu non appartiene a un menu PDF, TXT o dati del piano.");
            foreach (var value in plan.Values)
            {
                if (value.Name.Length == 0 && value.Data.Contains("Flix.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!value.Key.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase)
                        || !value.Data.Contains("--convert-menu ", StringComparison.Ordinal))
                        throw new IOException("La voce di menu non usa --convert-menu.");
                }
                if (value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                    RequireIconValue(root, value, "Il menu PDF, TXT e dati richiede le icone premium.");
            }
        }
    }

    static void InstallContentPlans(IReadOnlyList<ContextMenuPlan> plans, IRegistryStore store)
    {
        VerifyContentPlans(plans);
        foreach (var plan in plans) GuardContentMenu(plan, store);
        var backups = plans.Select(plan => CaptureBackup(plan, store)).ToArray();
        CommitImageChanges(backups, store, () =>
        {
            RemoveLegacyOwned(plans, store);
            foreach (var plan in plans)
                WritePlanValues(plan, store);
        });
    }

    static void GuardContentMenu(ContextMenuPlan plan, IRegistryStore store)
    {
        string root = RootOf(plan);
        string owner = plan.Value(root, OwnerValue);
        if (!IsContentMenuKey(root))
            throw new IOException("Il menu PDF, TXT e dati uscirebbe dal perimetro previsto.");
        foreach (string key in plan.Keys)
        {
            if (!IsContentMenuKey(key) || !key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu PDF, TXT e dati uscirebbe dal perimetro previsto.");
            if (IsProtectedMenuKey(key))
                throw new IOException("Il menu PDF, TXT e dati non può modificare menu esistenti.");
            if (store.KeyExists(key) && !LegacyIdentity.MarkerMatches(store, key, owner))
                throw new IOException("Una chiave del menu PDF, TXT e dati esiste già con un proprietario diverso: è stata preservata.");
        }
    }

    static void VerifyMediaPlans(IReadOnlyList<ContextMenuPlan> plans)
    {
        if (plans.Count != MediaExtensions.Count)
            throw new IOException("Il piano audio e video non è completo.");
        var extensions = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int audio = 0, video = 0;
        foreach (var plan in plans)
        {
            string root = RootOf(plan);
            EnsureParentIcon(plan, root);
            string extension = MediaExtensionOf(plan);
            extensions.Add(extension);
            string menuId = plan.Value(root, OwnerValue);
            if (!menuId.Equals(MediaMenuIdFor(extension), StringComparison.Ordinal))
                throw new IOException("Proprietario del menu audio e video assente.");
            if (plan.Values.Any(value => value.Key.Equals(root, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0)
                || plan.Keys.Contains(root + @"\command"))
                throw new IOException("Il menu padre non esegue Flix.exe.");
            string subCommands = plan.Value(root, "ExtendedSubCommandsKey");
            if (!root.Equals(@"Software\Classes\" + subCommands, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu audio e video non prevede alias.");
            string source = extension.TrimStart('.').ToUpperInvariant();
            if (plan.Formats.Contains(source, StringComparer.Ordinal))
                throw new IOException("La destinazione coincide con la sorgente: " + extension + ".");
            if (Policy.Audio.Contains(extension)) audio += plan.Formats.Count;
            else if (Policy.Video.Contains(extension)) video += plan.Formats.Count;
            else throw new IOException("Estensione audio o video non prevista: " + extension + ".");
            foreach (string key in plan.Keys)
            {
                if (!seen.Add(key)) throw new IOException("Chiave duplicata nel menu audio e video: " + key);
                if (!IsMediaMenuKey(key) || !key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Il menu audio e video uscirebbe dal perimetro previsto.");
                if (IsProtectedFromMedia(key))
                    throw new IOException("Il menu audio e video non può modificare menu esistenti.");
                if (!plan.Value(key, OwnerValue).Equals(menuId, StringComparison.Ordinal))
                    throw new IOException("Proprietario del menu audio e video assente.");
            }
            foreach (var value in plan.Values)
            {
                if (value.Name.Length == 0 && value.Data.Contains("Flix.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!value.Key.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase)
                        || !value.Data.Contains("--convert-menu ", StringComparison.Ordinal))
                        throw new IOException("La voce di menu non usa --convert-menu.");
                }
                if (value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                    RequireIconValue(root, value, "Il menu audio e video richiede le icone premium.");
            }
        }
        var found = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        if (found.Count != plans.Count || audio != 42 || video != 44 || audio + video != 86
            || !MediaExtensions.All(found.Contains))
            throw new IOException("Il piano audio e video non è completo.");
    }

    static void InstallMediaPlans(IReadOnlyList<ContextMenuPlan> plans, IRegistryStore store)
    {
        VerifyMediaPlans(plans);
        foreach (var plan in plans) GuardMediaMenu(plan, store);
        var backups = plans.Select(plan => CaptureBackup(plan, store)).ToArray();
        CommitImageChanges(backups, store, () =>
        {
            RemoveLegacyOwned(plans, store);
            foreach (var plan in plans)
                WritePlanValues(plan, store);
        });
    }

    static void GuardMediaMenu(ContextMenuPlan plan, IRegistryStore store)
    {
        string root = RootOf(plan);
        string owner = plan.Value(root, OwnerValue);
        if (!IsMediaMenuKey(root))
            throw new IOException("Il menu audio e video uscirebbe dal perimetro previsto.");
        foreach (string key in plan.Keys)
        {
            if (!IsMediaMenuKey(key) || !key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu audio e video uscirebbe dal perimetro previsto.");
            if (IsProtectedFromMedia(key))
                throw new IOException("Il menu audio e video non può modificare menu esistenti.");
            if (store.KeyExists(key) && !LegacyIdentity.MarkerMatches(store, key, owner))
                throw new IOException("Una chiave del menu audio e video esiste già con un proprietario diverso: è stata preservata.");
        }
    }

    static void VerifyImagePlans(IReadOnlyList<ContextMenuPlan> plans)
    {
        var roots = new HashSet<string>(plans.Select(RootOf), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in plans)
        {
            string root = RootOf(plan);
            EnsureParentIcon(plan, root);
            foreach (string key in plan.Keys)
                if (!seen.Add(key)) throw new IOException("Chiave duplicata nel menu immagini: " + key);
            if (plan.Values.Any(value => value.Key.Equals(root, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0)
                || plan.Keys.Contains(root + @"\command"))
                throw new IOException("Il menu padre non esegue Flix.exe.");
            string subCommands = plan.Value(root, "ExtendedSubCommandsKey");
            if (!roots.Contains(@"Software\Classes\" + subCommands))
                throw new IOException("Il sottomenu non appartiene a un menu immagini del piano.");
            foreach (var value in plan.Values)
            {
                if (value.Name.Length == 0 && value.Data.Contains("Flix.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!value.Key.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase)
                        || !value.Data.Contains("--convert-menu ", StringComparison.Ordinal))
                        throw new IOException("La voce di menu non usa --convert-menu.");
                }
                if (value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                    RequireIconValue(root, value, "Il menu immagini richiede le icone premium.");
            }
        }
    }

    static void CommitImageChanges(ContextMenuRegistryBackup[] backups, IRegistryStore store, Action mutate)
    {
        try { mutate(); }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            RestoreImageBackups(backups, store, ex);
            throw;
        }
    }

    static void RestoreImageBackups(ContextMenuRegistryBackup[] backups, IRegistryStore store, Exception failure)
    {
        const string incomplete = "Operazione interrotta e ripristino incompleto. Il Registro potrebbe essere parziale. L'operazione non è atomica. ";
        try
        {
            for (int index = backups.Length - 1; index >= 0; index--)
                RestoreBackup(backups[index], store);
            if (!ImageBackupsMatch(backups, store))
                throw new IOException("Il ripristino non coincide con lo stato precedente.");
        }
        catch (Exception restoreEx) when (IsRegistryFailure(restoreEx))
        {
            throw new IOException(incomplete + restoreEx.Message + " Errore originale (" + failure.GetType().Name + "): " + failure.Message, failure);
        }
    }

    static bool ImageBackupsMatch(ContextMenuRegistryBackup[] backups, IRegistryStore store)
    {
        foreach (var backup in backups)
        {
            foreach (var entry in backup.Entries)
            {
                bool exists = store.KeyExists(entry.Path);
                if (!entry.Existed)
                {
                    if (exists) return false;
                    continue;
                }
                if (!exists) return false;
                foreach (var pair in entry.Values)
                    if (!string.Equals(store.Read(entry.Path, pair.Key), pair.Value, StringComparison.Ordinal))
                        return false;
                foreach (string name in entry.AbsentNames)
                    if (store.Read(entry.Path, name) != null) return false;
            }
        }
        return true;
    }

    static bool IsRegistryFailure(Exception ex) =>
        ex is IOException
            or UnauthorizedAccessException
            or SecurityException
            or InvalidOperationException
            or ArgumentException;

    static string CanonicalImageExtension(string normalized) => normalized switch
    {
        ".jpeg" => ".jpg",
        ".tiff" => ".tif",
        _ => normalized
    };

    static string Verb(string parentKey, string format) => parentKey + @"\shell\" + format.ToLowerInvariant();

    static string NormalizeImageExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) throw new IOException("Estensione immagine assente.");
        string normalized = (extension.StartsWith('.') ? extension : "." + extension).ToLowerInvariant();
        if (!ImageExtensions.Contains(normalized)) throw new IOException("Estensione immagine non prevista: " + extension + ".");
        return normalized;
    }

    static IEnumerable<string> OwnedKeyPaths()
    {
        yield return ParentKey;
        yield return ParentKey + @"\shell";
        foreach (string format in Formats)
        {
            string verb = VerbKey(format);
            yield return verb;
            yield return verb + @"\command";
        }
    }

    static void EnsureParentIcon(ContextMenuPlan plan, string root)
    {
        bool found = plan.Values.Any(value =>
            value.Key.Equals(root, StringComparison.OrdinalIgnoreCase)
            && value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase)
            && value.Data.EndsWith(ParentIconFileName, StringComparison.OrdinalIgnoreCase));
        if (!found) throw new IOException("Il menu padre richiede l'icona XF.");
    }

    static void RequireIconValue(string root, RegistryValue value, string premiumMessage)
    {
        if (value.Key.Equals(root, StringComparison.OrdinalIgnoreCase))
        {
            if (!value.Data.EndsWith(ParentIconFileName, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu padre richiede l'icona XF.");
            return;
        }
        if (!value.Data.EndsWith("-premium-v2.ico", StringComparison.OrdinalIgnoreCase))
            throw new IOException(premiumMessage);
    }

    internal static string LegacyMenuId(string menuId)
    {
        if (!menuId.StartsWith(MenuIdPrefix, StringComparison.Ordinal))
            throw new IOException("Identificativo menu non previsto.");
        return LegacyIdentity.MenuId(menuId);
    }

    internal static string LegacyKeyFor(string key, string menuId)
    {
        string current = @"\" + menuId;
        int at = key.LastIndexOf(current, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return key;
        return string.Concat(key.AsSpan(0, at), @"\", LegacyMenuId(menuId), key.AsSpan(at + current.Length));
    }

    internal static bool IsLegacyMenuProtected(string key)
    {
        if (LegacyIdentity.IsOriginalMenu(key) || ContextMenuPreview.IsPreviewKey(key))
            return true;
        return false;
    }

    static void RemoveLegacyOwned(IEnumerable<ContextMenuPlan> plans, IRegistryStore store)
    {
        var owned = new List<(string Key, string Owner)>();
        foreach (var plan in plans)
        {
            string root = RootOf(plan);
            string menuId = plan.Value(root, OwnerValue);
            string legacyOwner = LegacyMenuId(menuId);
            foreach (string key in plan.Keys)
            {
                string legacy = LegacyKeyFor(key, menuId);
                if (legacy.Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                if (IsLegacyMenuProtected(legacy)) continue;
                owned.Add((legacy, legacyOwner));
            }
        }
        foreach (var item in owned.OrderByDescending(item => item.Key.Count(character => character == '\\')))
        {
            if (!store.KeyExists(item.Key) || !LegacyIdentity.MarkerMatches(store, item.Key, item.Owner)) continue;
            if (store.SubKeys(item.Key).Count > 0) continue;
            store.DeleteKey(item.Key);
        }
    }

    static void GuardForeignMenus(ContextMenuPlan plan, IRegistryStore store)
    {
        if (plan.Keys.Any(IsMediaMenuKey))
            throw new IOException("Il menu audio e video non è registrabile prima del controllo icone. Nessuna chiave è stata scritta.");
        if (plan.Keys.Any(IsDocumentMenuKey))
            throw new IOException("Il menu documenti non è registrabile prima del gate icone e dei controlli M5.5. Nessuna chiave è stata scritta.");
        string root = RootOf(plan);
        string owner = plan.Value(root, OwnerValue);
        bool png = root.Equals(ParentKey, StringComparison.OrdinalIgnoreCase);
        if (!root.StartsWith(@"Software\Classes\SystemFileAssociations\", StringComparison.OrdinalIgnoreCase))
            throw new IOException(png ? "Il menu PNG uscirebbe dal perimetro previsto." : "Il menu definitivo uscirebbe dal perimetro previsto.");
        if (plan.Keys.Any(IsContentMenuKey))
        {
            string full = Path.GetFullPath(plan.Executable);
            if (!IconsReady(full, plan.Formats))
                throw new IOException("Icone non disponibili: nessuna chiave del menu è stata registrata.");
            RejectContentRegistration();
        }
        foreach (string key in plan.Keys)
        {
            if (IsContentMenuKey(key)) RejectContentRegistration();
            if (!key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException(png ? "Il menu PNG uscirebbe dal perimetro previsto." : "Il menu definitivo uscirebbe dal perimetro previsto.");
            if (LegacyIdentity.IsOriginalMenu(key))
                throw new IOException("Il menu originale non può essere modificato.");
            if (ContextMenuPreview.IsPreviewKey(key))
                throw new IOException("Il menu di anteprima non può essere modificato.");
            if (store.KeyExists(key) && !LegacyIdentity.MarkerMatches(store, key, owner))
                throw new IOException(png
                    ? "Una chiave del menu PNG esiste già con un proprietario o un comando diverso: è stata preservata."
                    : "Una chiave del menu definitivo esiste già con un proprietario diverso: è stata preservata.");
        }
    }

    static string RootOf(ContextMenuPlan plan) =>
        plan.Keys.OrderBy(key => key.Length).ThenBy(key => key, StringComparer.OrdinalIgnoreCase).First();

    static bool PathUsesBuildArtifact(string fullPath)
    {
        foreach (string segment in fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            if (segment.Equals("bin", StringComparison.OrdinalIgnoreCase) || segment.Equals("obj", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    internal static ContentMenuBlueprint[] PlanContentMenus(ContextMenuCatalog.EngineSnapshot engines)
    {
        var blueprints = new ContentMenuBlueprint[ContentExtensions.Count];
        for (int index = 0; index < ContentExtensions.Count; index++)
        {
            string extension = ContentExtensions[index];
            var entries = ContextMenuCatalog.ForExtension(extension, engines);
            if (entries.Length == 0)
                throw new IOException("Il catalogo non offre conversioni per " + extension + ".");
            string canonical = CanonicalContentExtension(extension);
            bool shared = !canonical.Equals(extension, StringComparison.Ordinal);
            if (shared)
            {
                string[] aliasFormats = entries.Select(entry => entry.TargetFormat).ToArray();
                string[] canonicalFormats = ContextMenuCatalog.ForExtension(canonical, engines).Select(entry => entry.TargetFormat).ToArray();
                if (!aliasFormats.SequenceEqual(canonicalFormats))
                    throw new IOException("Le estensioni equivalenti non condividono le destinazioni: " + extension + ".");
            }
            string source = SourceFormat(extension);
            if (entries.Any(entry => entry.TargetFormat.Equals(source, StringComparison.Ordinal)))
                throw new IOException("La destinazione coincide con la sorgente: " + extension + ".");
            string[] policy = Policy.OrderFormats(Policy.Outputs(["flix.context" + extension], engines.Pandoc, engines.Office, engines.Ffmpeg));
            if (!entries.Select(entry => entry.TargetFormat).SequenceEqual(policy))
                throw new IOException("L'ordine del catalogo non coincide con la Policy per " + extension + ".");
            foreach (var entry in entries) _ = IconFileName(entry.TargetFormat);
            blueprints[index] = new ContentMenuBlueprint(
                extension,
                ContentMenuIdFor(extension),
                ContentParentKeyFor(extension),
                ContentSubCommandsPathFor(extension),
                ContentParentKeyFor(canonical),
                shared,
                entries);
        }
        if (blueprints.Select(item => item.ParentKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != blueprints.Length)
            throw new IOException("Chiave padre duplicata nel menu PDF, TXT e dati.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var blueprint in blueprints)
        {
            foreach (string key in PlannedContentKeys(blueprint))
                if (!seen.Add(key)) throw new IOException("Chiave duplicata nel menu PDF, TXT e dati: " + key);
        }
        return blueprints;
    }

    internal static string ContentMenuIdFor(string extension)
    {
        string canonical = CanonicalContentExtension(NormalizeContentExtension(extension));
        string stem = canonical.TrimStart('.');
        return MenuIdPrefix + char.ToUpperInvariant(stem[0]) + stem[1..];
    }

    internal static string ContentParentKeyFor(string extension)
    {
        string normalized = NormalizeContentExtension(extension);
        return @"Software\Classes\SystemFileAssociations\" + normalized + @"\shell\" + ContentMenuIdFor(normalized);
    }

    internal static string ContentSubCommandsPathFor(string extension)
    {
        string canonical = CanonicalContentExtension(NormalizeContentExtension(extension));
        const string prefix = @"Software\Classes\";
        string parent = ContentParentKeyFor(canonical);
        if (!parent.StartsWith(prefix, StringComparison.Ordinal))
            throw new IOException("Percorso comandi non valido.");
        return parent[prefix.Length..];
    }

    internal static IReadOnlyList<string> PlannedContentKeys(ContentMenuBlueprint blueprint)
    {
        if (blueprint.SharesCommandTree) return [blueprint.ParentKey];
        var keys = new List<string> { blueprint.ParentKey, blueprint.ParentKey + @"\shell" };
        foreach (string format in blueprint.Formats)
        {
            string verb = Verb(blueprint.ParentKey, format);
            keys.Add(verb);
            keys.Add(verb + @"\command");
        }
        return keys;
    }

    internal static IReadOnlyList<RegistryValue> DescribeContentMenu(ContentMenuBlueprint blueprint, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new IOException("Percorso eseguibile assente.");
        if (blueprint.SharesCommandTree)
        {
            return
            [
                new(blueprint.ParentKey, "MUIVerb", ParentVerb),
                new(blueprint.ParentKey, "Icon", ParentIconPath(executable)),
                new(blueprint.ParentKey, "ExtendedSubCommandsKey", blueprint.SubCommandsPath),
                new(blueprint.ParentKey, "MultiSelectModel", "Single"),
                new(blueprint.ParentKey, "NeverDefault", ""),
                new(blueprint.ParentKey, OwnerValue, blueprint.MenuId)
            ];
        }
        string directory = Path.GetDirectoryName(Path.GetFullPath(executable)) ?? throw new IOException("Percorso eseguibile assente.");
        string parentKey = blueprint.ParentKey;
        var values = new List<RegistryValue>
        {
            new(parentKey, "MUIVerb", ParentVerb),
            new(parentKey, "Icon", ParentIconPath(executable)),
            new(parentKey, "ExtendedSubCommandsKey", blueprint.SubCommandsPath),
            new(parentKey, "MultiSelectModel", "Single"),
            new(parentKey, "NeverDefault", ""),
            new(parentKey, OwnerValue, blueprint.MenuId),
            new(parentKey + @"\shell", OwnerValue, blueprint.MenuId)
        };
        foreach (var entry in blueprint.Entries)
        {
            string verb = Verb(parentKey, entry.TargetFormat);
            values.Add(new(verb, "MUIVerb", entry.Title));
            values.Add(new(verb, "Icon", Path.GetFullPath(Path.Combine(directory, IconFileName(entry.TargetFormat)))));
            values.Add(new(verb, "MultiSelectModel", "Single"));
            values.Add(new(verb, "NeverDefault", ""));
            values.Add(new(verb, OwnerValue, blueprint.MenuId));
            values.Add(new(verb + @"\command", "", CommandLine(executable, entry.TargetFormat)));
            values.Add(new(verb + @"\command", OwnerValue, blueprint.MenuId));
        }
        return values;
    }

    internal static void RefuseContentMenuRegistration(IRegistryStore store)
    {
        _ = store ?? throw new IOException("Registro assente.");
        RejectContentRegistration();
    }

    static void RejectContentRegistration() =>
        throw new IOException("Il menu PDF, TXT e dati non è registrabile prima del gate icone e dei controlli M5.3.4. Nessuna chiave è stata scritta.");

    static bool IsContentMenuKey(string key)
    {
        const string prefix = @"Software\Classes\SystemFileAssociations\";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = key[prefix.Length..];
        foreach (string extension in ContentExtensions)
        {
            if (rest.Equals(extension, StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith(extension + @"\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static bool IsProtectedMenuKey(string key)
    {
        if (LegacyIdentity.IsOriginalMenu(key) || ContextMenuPreview.IsPreviewKey(key))
            return true;
        if (IsMediaMenuKey(key)) return true;
        if (IsDocumentMenuKey(key)) return true;
        const string prefix = @"Software\Classes\SystemFileAssociations\";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = key[prefix.Length..];
        foreach (string extension in ImageExtensions)
        {
            if (rest.Equals(extension, StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith(extension + @"\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string CanonicalContentExtension(string normalized) => normalized switch
    {
        ".yml" => ".yaml",
        _ => normalized
    };

    static string SourceFormat(string extension) => extension switch
    {
        ".pdf" => "PDF",
        ".txt" => "TXT",
        ".json" => "JSON",
        ".yaml" or ".yml" => "YAML",
        ".xml" => "XML",
        ".csv" => "CSV",
        _ => throw new IOException("Estensione contenuto non prevista: " + extension + ".")
    };

    static string NormalizeContentExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) throw new IOException("Estensione contenuto assente.");
        string normalized = (extension.StartsWith('.') ? extension : "." + extension).ToLowerInvariant();
        if (!ContentExtensions.Contains(normalized)) throw new IOException("Estensione contenuto non prevista: " + extension + ".");
        return normalized;
    }

    static bool IsUnauthorizedTemporaryBuild(string fullPath)
    {
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalized = Path.GetFullPath(fullPath);
        return normalized.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(temp, StringComparison.OrdinalIgnoreCase);
    }

    internal static MediaMenuBlueprint[] PlanMediaMenus(ContextMenuCatalog.EngineSnapshot engines)
    {
        var blueprints = new List<MediaMenuBlueprint>();
        foreach (string extension in MediaExtensions)
        {
            var entries = ContextMenuCatalog.ForExtension(extension, engines);
            if (entries.Length == 0) continue;
            string source = MediaSourceFormat(extension);
            if (entries.Any(entry => entry.TargetFormat.Equals(source, StringComparison.Ordinal)))
                throw new IOException("La destinazione coincide con la sorgente: " + extension + ".");
            string[] policy = Policy.OrderFormats(Policy.Outputs(["flix.context" + extension], engines.Pandoc, engines.Office, engines.Ffmpeg));
            if (!entries.Select(entry => entry.TargetFormat).SequenceEqual(policy))
                throw new IOException("L'ordine del catalogo non coincide con la Policy per " + extension + ".");
            foreach (var entry in entries) _ = IconFileName(entry.TargetFormat);
            string parent = MediaParentKeyFor(extension);
            blueprints.Add(new MediaMenuBlueprint(
                extension,
                MediaMenuIdFor(extension),
                parent,
                MediaSubCommandsPathFor(extension),
                parent,
                false,
                entries));
        }
        if (blueprints.Count != 0 && blueprints.Count != MediaExtensions.Count)
            throw new IOException("Il catalogo audio e video è incompleto.");
        if (blueprints.Select(item => item.ParentKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != blueprints.Count)
            throw new IOException("Chiave padre duplicata nel menu audio e video.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var blueprint in blueprints)
        {
            if (blueprint.SharesCommandTree)
                throw new IOException("Il menu audio e video non prevede alias: " + blueprint.Extension + ".");
            foreach (string key in PlannedMediaKeys(blueprint))
                if (!seen.Add(key)) throw new IOException("Chiave duplicata nel menu audio e video: " + key);
        }
        return blueprints.ToArray();
    }

    internal static string MediaMenuIdFor(string extension)
    {
        string stem = NormalizeMediaExtension(extension).TrimStart('.');
        if (stem.Length == 0) throw new IOException("Estensione audio o video non prevista: " + extension + ".");
        return MenuIdPrefix + char.ToUpperInvariant(stem[0]) + stem[1..];
    }

    internal static string MediaParentKeyFor(string extension)
    {
        string normalized = NormalizeMediaExtension(extension);
        return @"Software\Classes\SystemFileAssociations\" + normalized + @"\shell\" + MediaMenuIdFor(normalized);
    }

    internal static string MediaSubCommandsPathFor(string extension)
    {
        const string prefix = @"Software\Classes\";
        string parent = MediaParentKeyFor(extension);
        if (!parent.StartsWith(prefix, StringComparison.Ordinal))
            throw new IOException("Percorso comandi non valido.");
        return parent[prefix.Length..];
    }

    internal static IReadOnlyList<string> PlannedMediaKeys(MediaMenuBlueprint blueprint)
    {
        if (blueprint.SharesCommandTree)
            throw new IOException("Il menu audio e video non prevede alias: " + blueprint.Extension + ".");
        var keys = new List<string> { blueprint.ParentKey, blueprint.ParentKey + @"\shell" };
        foreach (string format in blueprint.Formats)
        {
            string verb = Verb(blueprint.ParentKey, format);
            keys.Add(verb);
            keys.Add(verb + @"\command");
        }
        return keys;
    }

    internal static IReadOnlyList<RegistryValue> DescribeMediaMenu(MediaMenuBlueprint blueprint, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new IOException("Percorso eseguibile assente.");
        if (blueprint.SharesCommandTree)
            throw new IOException("Il menu audio e video non prevede alias: " + blueprint.Extension + ".");
        string directory = Path.GetDirectoryName(Path.GetFullPath(executable)) ?? throw new IOException("Percorso eseguibile assente.");
        string parentKey = blueprint.ParentKey;
        var values = new List<RegistryValue>
        {
            new(parentKey, "MUIVerb", ParentVerb),
            new(parentKey, "Icon", ParentIconPath(executable)),
            new(parentKey, "ExtendedSubCommandsKey", blueprint.SubCommandsPath),
            new(parentKey, "MultiSelectModel", "Single"),
            new(parentKey, "NeverDefault", ""),
            new(parentKey, OwnerValue, blueprint.MenuId),
            new(parentKey + @"\shell", OwnerValue, blueprint.MenuId)
        };
        foreach (var entry in blueprint.Entries)
        {
            string verb = Verb(parentKey, entry.TargetFormat);
            values.Add(new(verb, "MUIVerb", entry.Title));
            values.Add(new(verb, "Icon", Path.GetFullPath(Path.Combine(directory, IconFileName(entry.TargetFormat)))));
            values.Add(new(verb, "MultiSelectModel", "Single"));
            values.Add(new(verb, "NeverDefault", ""));
            values.Add(new(verb, OwnerValue, blueprint.MenuId));
            values.Add(new(verb + @"\command", "", CommandLine(executable, entry.TargetFormat)));
            values.Add(new(verb + @"\command", OwnerValue, blueprint.MenuId));
        }
        return values;
    }

    static bool IsMediaMenuKey(string key)
    {
        const string prefix = @"Software\Classes\SystemFileAssociations\";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = key[prefix.Length..];
        foreach (string extension in MediaExtensions)
        {
            if (rest.Equals(extension, StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith(extension + @"\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static bool IsProtectedFromMedia(string key)
    {
        if (LegacyIdentity.IsOriginalMenu(key) || ContextMenuPreview.IsPreviewKey(key))
            return true;
        if (IsContentMenuKey(key)) return true;
        if (IsDocumentMenuKey(key)) return true;
        const string prefix = @"Software\Classes\SystemFileAssociations\";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = key[prefix.Length..];
        foreach (string extension in ImageExtensions)
        {
            if (rest.Equals(extension, StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith(extension + @"\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string MediaExtensionOf(ContextMenuPlan plan)
    {
        string root = RootOf(plan);
        const string prefix = @"Software\Classes\SystemFileAssociations\";
        if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Il menu audio e video uscirebbe dal perimetro previsto.");
        string rest = root[prefix.Length..];
        int slash = rest.IndexOf('\\');
        if (slash <= 0) throw new IOException("Il menu audio e video uscirebbe dal perimetro previsto.");
        return NormalizeMediaExtension(rest[..slash]);
    }

    static string MediaSourceFormat(string extension) =>
        NormalizeMediaExtension(extension).TrimStart('.').ToUpperInvariant();

    static string NormalizeMediaExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) throw new IOException("Estensione audio o video assente.");
        string normalized = (extension.StartsWith('.') ? extension : "." + extension).ToLowerInvariant();
        if (!MediaExtensions.Contains(normalized)) throw new IOException("Estensione audio o video non prevista: " + extension + ".");
        return normalized;
    }

    internal static void InstallDocumentMenus(string executable, ContextMenuCatalog.EngineSnapshot engines, IRegistryStore store)
    {
        string full = RequireDocumentRegistryExecutable(executable);
        var blueprints = PlanDocumentMenus(engines);
        if (!engines.Pandoc)
            throw new IOException("Pandoc assente: il piano documenti è vuoto e nessun menu è installabile. Nessuna chiave è stata scritta.");
        if (blueprints.Length == 0)
            throw new IOException("Il piano documenti è vuoto. Nessuna chiave è stata scritta.");
        if (blueprints.Length != DocumentExtensions.Count)
            throw new IOException("Il piano documenti non è completo. Nessuna chiave è stata scritta.");
        var plans = CreateDocumentPlans(full, engines);
        if (!DocumentIconsReady(full, engines))
            throw new IOException("Icone non disponibili: nessuna chiave del menu è stata registrata.");
        InstallDocumentPlans(plans, store);
    }

    internal static void RemoveDocumentMenus(IRegistryStore store) =>
        RemoveDocumentMenus(Path.Combine(StableProgramsFlix, ExecutableFileName), store);

    internal static void RemoveDocumentMenus(string executable, IRegistryStore store)
    {
        string full = RequireDocumentRegistryExecutable(executable);
        var plans = CreateDocumentPlans(full, MaximumDocumentEngines);
        var ancestors = SnapshotDocumentAncestors(plans, store, false);
        var backups = plans.Select(plan => CaptureDocumentBackup(plan, store)).ToArray();
        CommitDocumentChanges(backups, ancestors, store, () =>
        {
            RemoveLegacyOwned(plans, store);
            var owned = new List<(string Key, string Owner)>();
            foreach (var plan in plans)
            {
                string root = RootOf(plan);
                string owner = plan.Value(root, OwnerValue);
                foreach (string key in plan.Keys) owned.Add((key, owner));
            }
            foreach (var item in owned.OrderByDescending(item => item.Key.Count(character => character == '\\')))
            {
                if (!store.KeyExists(item.Key) || !LegacyIdentity.MarkerMatches(store, item.Key, item.Owner)) continue;
                if (store.SubKeys(item.Key).Count > 0) continue;
                store.DeleteKey(item.Key);
            }
            DeleteClaimedDocumentAncestors(ancestors, store);
        });
    }

    internal static ContextMenuPlan[] CreateDocumentPlans(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        ValidateRegistrableExecutable(executable);
        var plans = PlanDocumentMenus(engines).Select(blueprint => BuildDocumentPlan(executable, blueprint)).ToArray();
        VerifyDocumentPlans(plans);
        return plans;
    }

    internal static DocumentMenuBlueprint[] PlanDocumentMenus(ContextMenuCatalog.EngineSnapshot engines)
    {
        var blueprints = new List<DocumentMenuBlueprint>();
        foreach (string extension in DocumentExtensions)
        {
            var entries = ContextMenuCatalog.ForExtension(extension, engines);
            if (entries.Length == 0) continue;
            string canonical = CanonicalDocumentExtension(extension);
            bool shared = !canonical.Equals(extension, StringComparison.Ordinal);
            if (shared)
            {
                string[] aliasFormats = entries.Select(entry => entry.TargetFormat).ToArray();
                string[] canonicalFormats = ContextMenuCatalog.ForExtension(canonical, engines).Select(entry => entry.TargetFormat).ToArray();
                if (canonicalFormats.Length == 0 || !aliasFormats.SequenceEqual(canonicalFormats))
                    throw new IOException("Le estensioni equivalenti non condividono le destinazioni: " + extension + ".");
            }
            string source = DocumentSourceFormat(extension);
            if (entries.Any(entry => entry.TargetFormat.Equals(source, StringComparison.Ordinal)))
                throw new IOException("La destinazione coincide con la sorgente: " + extension + ".");
            string[] policy = Policy.OrderFormats(Policy.Outputs(["flix.context" + extension], engines.Pandoc, engines.Office, engines.Ffmpeg));
            if (!entries.Select(entry => entry.TargetFormat).SequenceEqual(policy))
                throw new IOException("L'ordine del catalogo non coincide con la Policy per " + extension + ".");
            foreach (var entry in entries) _ = IconFileName(entry.TargetFormat);
            blueprints.Add(new DocumentMenuBlueprint(
                extension,
                DocumentMenuIdFor(extension),
                DocumentParentKeyFor(extension),
                DocumentSubCommandsPathFor(extension),
                DocumentParentKeyFor(canonical),
                shared,
                entries));
        }
        if (blueprints.Count != 0 && blueprints.Count != DocumentExtensions.Count)
            throw new IOException("Il catalogo documenti è incompleto. Nessuna chiave è stata scritta.");
        if (blueprints.Select(item => item.ParentKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != blueprints.Count)
            throw new IOException("Chiave padre duplicata nel menu documenti.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var blueprint in blueprints)
        {
            foreach (string key in PlannedDocumentKeys(blueprint))
                if (!seen.Add(key)) throw new IOException("Chiave duplicata nel menu documenti: " + key);
        }
        return blueprints.ToArray();
    }

    internal static string DocumentMenuIdFor(string extension)
    {
        string canonical = CanonicalDocumentExtension(NormalizeDocumentExtension(extension));
        string stem = canonical.TrimStart('.');
        if (stem.Length == 0) throw new IOException("Estensione documenti non prevista: " + extension + ".");
        return MenuIdPrefix + char.ToUpperInvariant(stem[0]) + stem[1..];
    }

    internal static string DocumentParentKeyFor(string extension)
    {
        string normalized = NormalizeDocumentExtension(extension);
        return @"Software\Classes\SystemFileAssociations\" + normalized + @"\shell\" + DocumentMenuIdFor(normalized);
    }

    internal static string DocumentSubCommandsPathFor(string extension)
    {
        string canonical = CanonicalDocumentExtension(NormalizeDocumentExtension(extension));
        const string prefix = @"Software\Classes\";
        string parent = DocumentParentKeyFor(canonical);
        if (!parent.StartsWith(prefix, StringComparison.Ordinal))
            throw new IOException("Percorso comandi non valido.");
        return parent[prefix.Length..];
    }

    internal static IReadOnlyList<string> PlannedDocumentKeys(DocumentMenuBlueprint blueprint)
    {
        if (blueprint.SharesCommandTree) return [blueprint.ParentKey];
        var keys = new List<string> { blueprint.ParentKey, blueprint.ParentKey + @"\shell" };
        foreach (string format in blueprint.Formats)
        {
            string verb = Verb(blueprint.ParentKey, format);
            keys.Add(verb);
            keys.Add(verb + @"\command");
        }
        return keys;
    }

    internal static IReadOnlyList<RegistryValue> DescribeDocumentMenu(DocumentMenuBlueprint blueprint, string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
            throw new IOException("Percorso eseguibile assente.");
        if (blueprint.SharesCommandTree)
        {
            return
            [
                new(blueprint.ParentKey, "MUIVerb", ParentVerb),
                new(blueprint.ParentKey, "Icon", ParentIconPath(executable)),
                new(blueprint.ParentKey, "ExtendedSubCommandsKey", blueprint.SubCommandsPath),
                new(blueprint.ParentKey, "MultiSelectModel", "Single"),
                new(blueprint.ParentKey, "NeverDefault", ""),
                new(blueprint.ParentKey, OwnerValue, blueprint.MenuId)
            ];
        }
        string full = Path.GetFullPath(executable);
        string directory = Path.GetDirectoryName(full) ?? throw new IOException("Percorso eseguibile assente.");
        string parentKey = blueprint.ParentKey;
        var values = new List<RegistryValue>
        {
            new(parentKey, "MUIVerb", ParentVerb),
            new(parentKey, "Icon", ParentIconPath(executable)),
            new(parentKey, "ExtendedSubCommandsKey", blueprint.SubCommandsPath),
            new(parentKey, "MultiSelectModel", "Single"),
            new(parentKey, "NeverDefault", ""),
            new(parentKey, OwnerValue, blueprint.MenuId),
            new(parentKey + @"\shell", OwnerValue, blueprint.MenuId)
        };
        foreach (var entry in blueprint.Entries)
        {
            string verb = Verb(parentKey, entry.TargetFormat);
            values.Add(new(verb, "MUIVerb", entry.Title));
            values.Add(new(verb, "Icon", Path.GetFullPath(Path.Combine(directory, IconFileName(entry.TargetFormat)))));
            values.Add(new(verb, "MultiSelectModel", "Single"));
            values.Add(new(verb, "NeverDefault", ""));
            values.Add(new(verb, OwnerValue, blueprint.MenuId));
            values.Add(new(verb + @"\command", "", CommandLine(full, entry.TargetFormat)));
            values.Add(new(verb + @"\command", OwnerValue, blueprint.MenuId));
        }
        return values;
    }

    static ContextMenuPlan BuildDocumentPlan(string executable, DocumentMenuBlueprint blueprint)
    {
        string full = Path.GetFullPath(executable);
        var values = DescribeDocumentMenu(blueprint, full);
        return new ContextMenuPlan(full, blueprint.Formats.ToArray(), values, format => Verb(blueprint.CommandParentKey, format));
    }

    static void InstallDocumentPlans(IReadOnlyList<ContextMenuPlan> plans, IRegistryStore store)
    {
        VerifyDocumentPlans(plans);
        foreach (var plan in plans) GuardDocumentMenu(plan, store);
        var ancestors = SnapshotDocumentAncestors(plans, store, true);
        var backups = plans.Select(plan => CaptureDocumentBackup(plan, store)).ToArray();
        CommitDocumentChanges(backups, ancestors, store, () =>
        {
            RemoveLegacyOwned(plans, store);
            foreach (var state in ancestors.Where(state => state.Claimed).OrderBy(state => state.Path.Length))
                store.EnsureKey(state.Path);
            foreach (var plan in plans)
                WritePlanValues(plan, store);
            WriteDocumentProvenance(plans, ancestors, store);
        });
    }

    static void GuardDocumentMenu(ContextMenuPlan plan, IRegistryStore store)
    {
        string root = RootOf(plan);
        string owner = plan.Value(root, OwnerValue);
        if (!IsDocumentMenuKey(root))
            throw new IOException("Il menu documenti uscirebbe dal perimetro previsto.");
        foreach (string key in plan.Keys)
        {
            if (!IsDocumentMenuKey(key) || !key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu documenti uscirebbe dal perimetro previsto.");
            if (IsProtectedFromDocuments(key))
                throw new IOException("Il menu documenti non può modificare menu esistenti.");
            if (store.KeyExists(key) && !LegacyIdentity.MarkerMatches(store, key, owner))
                throw new IOException("Una chiave del menu documenti esiste già con un proprietario diverso: è stata preservata.");
        }
    }

    static void VerifyDocumentPlans(IReadOnlyList<ContextMenuPlan> plans)
    {
        if (plans.Count != DocumentExtensions.Count)
            throw new IOException("Il piano documenti non è completo. Nessuna chiave è stata scritta.");
        var roots = new HashSet<string>(plans.Select(RootOf), StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extensions = new List<string>();
        int aliases = 0;
        bool? rendering = null;
        foreach (var plan in plans)
        {
            string root = RootOf(plan);
            EnsureParentIcon(plan, root);
            string extension = DocumentExtensionOf(plan);
            extensions.Add(extension);
            string menuId = plan.Value(root, OwnerValue);
            if (!menuId.Equals(DocumentMenuIdFor(extension), StringComparison.Ordinal))
                throw new IOException("Proprietario del menu documenti assente.");
            if (plan.Value(root, "MUIVerb") != ParentVerb
                || plan.Value(root, "MultiSelectModel") != "Single"
                || plan.Value(root, "NeverDefault") != "")
                throw new IOException("Il menu documenti non ha le proprietà previste.");
            if (plan.Values.Any(value => value.Key.Equals(root, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0)
                || plan.Keys.Contains(root + @"\command"))
                throw new IOException("Il menu padre non esegue Flix.exe.");
            string subCommands = plan.Value(root, "ExtendedSubCommandsKey");
            if (!roots.Contains(@"Software\Classes\" + subCommands))
                throw new IOException("Il sottomenu non appartiene a un menu documenti del piano.");
            bool shared = !root.Equals(@"Software\Classes\" + subCommands, StringComparison.OrdinalIgnoreCase);
            string canonical = CanonicalDocumentExtension(extension);
            if (shared != !canonical.Equals(extension, StringComparison.Ordinal))
                throw new IOException("Alias documentale non coerente: " + extension + ".");
            if (shared)
            {
                aliases++;
                if (plan.Keys.Count != 1)
                    throw new IOException("L'alias documentale non deve duplicare i comandi: " + extension + ".");
                var canonicalPlan = plans.Single(item => RootOf(item).Equals(@"Software\Classes\" + subCommands, StringComparison.OrdinalIgnoreCase));
                if (!plan.Formats.SequenceEqual(canonicalPlan.Formats)
                    || plan.Value(root, OwnerValue) != canonicalPlan.Value(RootOf(canonicalPlan), OwnerValue))
                    throw new IOException("Le estensioni equivalenti non condividono le destinazioni: " + extension + ".");
            }
            string source = DocumentSourceFormat(extension);
            if (plan.Formats.Contains(source, StringComparer.Ordinal))
                throw new IOException("La destinazione coincide con la sorgente: " + extension + ".");
            bool pdf = plan.Formats.Contains("PDF");
            if (plan.Formats.Contains("PNG") != pdf || plan.Formats.Contains("JPG") != pdf || plan.Formats.Contains("WEBP") != pdf)
                throw new IOException("PDF e immagini documentali devono seguire LibreOffice insieme: " + extension + ".");
            rendering ??= pdf;
            if (rendering != pdf)
                throw new IOException("PDF e immagini documentali devono seguire LibreOffice insieme: " + extension + ".");
            foreach (string key in plan.Keys)
            {
                if (!seen.Add(key)) throw new IOException("Chiave duplicata nel menu documenti: " + key);
                if (!IsDocumentMenuKey(key) || !key.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Il menu documenti uscirebbe dal perimetro previsto.");
                if (IsProtectedFromDocuments(key))
                    throw new IOException("Il menu documenti non può modificare menu esistenti.");
                if (!plan.Value(key, OwnerValue).Equals(menuId, StringComparison.Ordinal))
                    throw new IOException("Proprietario del menu documenti assente.");
            }
            foreach (var value in plan.Values)
            {
                if (value.Name.Length == 0 && value.Data.Contains("Flix.exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!value.Key.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase)
                        || !value.Data.Contains("--convert-menu ", StringComparison.Ordinal))
                        throw new IOException("La voce di menu non usa --convert-menu.");
                }
                if (value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase))
                    RequireIconValue(root, value, "Il menu documenti richiede le icone premium.");
            }
            if (!shared)
            {
                foreach (string format in plan.Formats)
                {
                    if (plan.Command(format) != CommandLine(plan.Executable, format))
                        throw new IOException("Il comando --convert-menu non corrisponde al formato " + format + ".");
                    if (plan.VerbTitle(format) != "Converti in " + format)
                        throw new IOException("La voce di destinazione non è prevista: " + format + ".");
                }
            }
        }
        if (aliases != 2 || !DocumentExtensions.All(extensions.Contains))
            throw new IOException("Il piano documenti non è completo. Nessuna chiave è stata scritta.");
    }

    static bool IsDocumentMenuKey(string key)
    {
        const string prefix = @"Software\Classes\SystemFileAssociations\";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = key[prefix.Length..];
        foreach (string extension in DocumentExtensions.OrderByDescending(item => item.Length))
        {
            if (rest.Equals(extension, StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith(extension + @"\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static bool IsProtectedFromDocuments(string key)
    {
        if (LegacyIdentity.IsOriginalMenu(key) || ContextMenuPreview.IsPreviewKey(key))
            return true;
        if (IsContentMenuKey(key) || IsMediaMenuKey(key)) return true;
        const string prefix = @"Software\Classes\SystemFileAssociations\";
        if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        string rest = key[prefix.Length..];
        foreach (string extension in ImageExtensions)
        {
            if (rest.Equals(extension, StringComparison.OrdinalIgnoreCase)
                || rest.StartsWith(extension + @"\", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string DocumentExtensionOf(ContextMenuPlan plan)
    {
        string root = RootOf(plan);
        const string prefix = @"Software\Classes\SystemFileAssociations\";
        if (!root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Il menu documenti uscirebbe dal perimetro previsto.");
        string rest = root[prefix.Length..];
        int slash = rest.IndexOf('\\');
        if (slash <= 0) throw new IOException("Il menu documenti uscirebbe dal perimetro previsto.");
        return NormalizeDocumentExtension(rest[..slash]);
    }

    static string CanonicalDocumentExtension(string normalized) => normalized switch
    {
        ".markdown" => ".md",
        ".htm" => ".html",
        _ => normalized
    };

    static string DocumentSourceFormat(string extension) => CanonicalDocumentExtension(NormalizeDocumentExtension(extension)) switch
    {
        ".md" => "MD",
        ".html" => "HTML",
        ".docx" => "DOCX",
        ".odt" => "ODT",
        ".rtf" => "RTF",
        ".epub" => "EPUB",
        ".rst" => "RST",
        ".fb2" => "FB2",
        _ => throw new IOException("Estensione documenti non prevista: " + extension + ".")
    };

    static string NormalizeDocumentExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension)) throw new IOException("Estensione documenti assente.");
        string normalized = (extension.StartsWith('.') ? extension : "." + extension).ToLowerInvariant();
        if (!DocumentExtensions.Contains(normalized)) throw new IOException("Estensione documenti non prevista: " + extension + ".");
        return normalized;
    }

    internal static string DocumentAssociationKey(string extension) =>
        @"Software\Classes\SystemFileAssociations\" + NormalizeDocumentExtension(extension);

    internal static string DocumentShellAssociationKey(string extension) =>
        DocumentAssociationKey(extension) + @"\shell";

    static bool DocumentAssociationPresent(IRegistryStore store, string path) =>
        store.KeyExists(path) || store.SubKeys(path).Count > 0;

    static bool IsDocumentAssociationAncestor(string path)
    {
        foreach (string extension in DocumentExtensions)
        {
            if (path.Equals(DocumentAssociationKey(extension), StringComparison.OrdinalIgnoreCase)
                || path.Equals(DocumentShellAssociationKey(extension), StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static string[] DocumentAncestorTokens(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return [];
        string[] tokens = raw.Split(';');
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string token in tokens)
        {
            if (token is not ("ext" or "shell") || !seen.Add(token))
                throw new IOException("Provenienza documentale non ammessa.");
        }
        return tokens;
    }

    static string FormatDocumentAncestorTokens(IEnumerable<string> tokens) =>
        string.Join(";", tokens.Distinct(StringComparer.Ordinal).OrderBy(token => token, StringComparer.Ordinal));

    static Dictionary<string, string> ReadDocumentValues(IRegistryStore store, string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!store.KeyExists(path)) return values;
        foreach (string name in store.ValueNames(path))
        {
            string? data = store.Read(path, name);
            if (data != null) values[name] = data;
        }
        return values;
    }

    static List<DocumentAncestorState> SnapshotDocumentAncestors(IReadOnlyList<ContextMenuPlan> plans, IRegistryStore store, bool claimMissing)
    {
        var states = new List<DocumentAncestorState>();
        foreach (var plan in plans)
        {
            string root = RootOf(plan);
            string owner = plan.Value(root, OwnerValue);
            string extension = DocumentExtensionOf(plan);
            string[] recorded = [];
            if (!claimMissing && store.KeyExists(root) && LegacyIdentity.MarkerMatches(store, root, owner))
            {
                string? raw = store.Read(root, DocumentAncestorValue) ?? store.Read(root, LegacyIdentity.AncestorValueName);
                if (raw != null) recorded = DocumentAncestorTokens(raw);
            }
            foreach (string token in new[] { "ext", "shell" })
            {
                string path = token == "ext" ? DocumentAssociationKey(extension) : DocumentShellAssociationKey(extension);
                bool existed = store.KeyExists(path);
                bool claimed = claimMissing
                    ? !DocumentAssociationPresent(store, path)
                    : recorded.Contains(token, StringComparer.Ordinal);
                states.Add(new DocumentAncestorState(path, existed, claimed, ReadDocumentValues(store, path)));
            }
        }
        return states;
    }

    static void WriteDocumentProvenance(IReadOnlyList<ContextMenuPlan> plans, IReadOnlyList<DocumentAncestorState> ancestors, IRegistryStore store)
    {
        foreach (var plan in plans)
        {
            string root = RootOf(plan);
            string owner = plan.Value(root, OwnerValue);
            if (!store.KeyExists(root) || !LegacyIdentity.MarkerMatches(store, root, owner)) continue;
            string extension = DocumentExtensionOf(plan);
            var tokens = new List<string>();
            string? previous = store.Read(root, DocumentAncestorValue) ?? store.Read(root, LegacyIdentity.AncestorValueName);
            if (previous != null) tokens.AddRange(DocumentAncestorTokens(previous));
            foreach (var state in ancestors.Where(state => state.Claimed))
            {
                if (state.Path.Equals(DocumentAssociationKey(extension), StringComparison.OrdinalIgnoreCase)) tokens.Add("ext");
                else if (state.Path.Equals(DocumentShellAssociationKey(extension), StringComparison.OrdinalIgnoreCase)) tokens.Add("shell");
            }
            if (tokens.Count == 0) continue;
            store.Write(root, DocumentAncestorValue, FormatDocumentAncestorTokens(tokens));
            string? historical = store.Read(root, LegacyIdentity.AncestorValueName);
            if (historical != null && LegacyIdentity.MarkerMatches(store, root, owner))
                store.DeleteValue(root, LegacyIdentity.AncestorValueName);
        }
    }

    static void DeleteClaimedDocumentAncestors(IReadOnlyList<DocumentAncestorState> ancestors, IRegistryStore store)
    {
        foreach (var state in ancestors.Where(state => state.Claimed)
            .OrderByDescending(state => state.Path.Count(character => character == '\\'))
            .ThenByDescending(state => state.Path.Length))
        {
            if (!IsDocumentAssociationAncestor(state.Path) || !store.KeyExists(state.Path)) continue;
            if (store.SubKeys(state.Path).Count > 0 || store.ValueNames(state.Path).Count > 0) continue;
            store.DeleteKey(state.Path);
        }
    }

    static ContextMenuRegistryBackup CaptureDocumentBackup(ContextMenuPlan plan, IRegistryStore store)
    {
        var backup = CaptureBackup(plan, store);
        string root = RootOf(plan);
        if (!store.KeyExists(root)) return backup;
        string? provenance = store.Read(root, DocumentAncestorValue);
        if (provenance == null) return backup;
        var entries = new List<RegistryKeyBackup>();
        foreach (var entry in backup.Entries)
        {
            if (!entry.Path.Equals(root, StringComparison.OrdinalIgnoreCase))
            {
                entries.Add(entry);
                continue;
            }
            var values = new Dictionary<string, string>(entry.Values, StringComparer.OrdinalIgnoreCase)
            {
                [DocumentAncestorValue] = provenance
            };
            entries.Add(new RegistryKeyBackup(entry.Path, entry.Existed, values, entry.AbsentNames));
        }
        return new ContextMenuRegistryBackup(entries, backup.Owner);
    }

    static void CommitDocumentChanges(ContextMenuRegistryBackup[] backups, IReadOnlyList<DocumentAncestorState> ancestors, IRegistryStore store, Action mutate)
    {
        try { mutate(); }
        catch (Exception ex) when (IsRegistryFailure(ex))
        {
            const string incomplete = "Operazione interrotta e ripristino incompleto. Il Registro potrebbe essere parziale. L'operazione non è atomica. ";
            try
            {
                for (int index = backups.Length - 1; index >= 0; index--)
                    RestoreBackup(backups[index], store);
                if (!ImageBackupsMatch(backups, store))
                    throw new IOException("Il ripristino non coincide con lo stato precedente.");
                RestoreDocumentAncestors(ancestors, store);
            }
            catch (Exception restoreEx) when (IsRegistryFailure(restoreEx))
            {
                throw new IOException(incomplete + restoreEx.Message + " Errore originale (" + ex.GetType().Name + "): " + ex.Message, ex);
            }
            throw;
        }
    }

    static void RestoreDocumentAncestors(IReadOnlyList<DocumentAncestorState> ancestors, IRegistryStore store)
    {
        foreach (var state in ancestors.Where(state => state.Existed).OrderBy(state => state.Path.Length))
        {
            if (!IsDocumentAssociationAncestor(state.Path)) continue;
            store.EnsureKey(state.Path);
            foreach (var pair in state.Values)
                store.Write(state.Path, pair.Key, pair.Value);
        }
        foreach (var state in ancestors.Where(state => state.Claimed && !state.Existed)
            .OrderByDescending(state => state.Path.Count(character => character == '\\')))
        {
            if (!IsDocumentAssociationAncestor(state.Path) || !store.KeyExists(state.Path)) continue;
            if (store.SubKeys(state.Path).Count > 0 || store.ValueNames(state.Path).Count > 0) continue;
            store.DeleteKey(state.Path);
        }
    }

    sealed class DocumentAncestorState
    {
        internal DocumentAncestorState(string path, bool existed, bool claimed, IReadOnlyDictionary<string, string> values)
        {
            Path = path;
            Existed = existed;
            Claimed = claimed;
            Values = values;
        }

        internal string Path { get; }
        internal bool Existed { get; }
        internal bool Claimed { get; }
        internal IReadOnlyDictionary<string, string> Values { get; }
    }

    sealed class CurrentUserRegistryStore : IRegistryStore
    {
        public bool KeyExists(string path)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            return key != null;
        }

        public string? Read(string path, string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            return key?.GetValue(name) as string;
        }

        public IReadOnlyList<string> SubKeys(string path)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            return key?.GetSubKeyNames() ?? [];
        }

        public IReadOnlyList<string> ValueNames(string path)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path);
            return key?.GetValueNames() ?? [];
        }

        public void Write(string path, string name, string data)
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            key.SetValue(name, data, RegistryValueKind.String);
        }

        public void EnsureKey(string path)
        {
            using var key = Registry.CurrentUser.CreateSubKey(path) ?? throw new IOException("Chiave non creabile.");
        }

        public void DeleteKey(string path)
        {
            if (SubKeys(path).Count > 0) throw new IOException("La chiave ha sottochiavi non rimosse.");
            Registry.CurrentUser.DeleteSubKey(path, false);
        }

        public void DeleteValue(string path, string name)
        {
            using var key = Registry.CurrentUser.OpenSubKey(path, writable: true);
            key?.DeleteValue(name, throwOnMissingValue: false);
        }
    }
}

internal sealed class ImageMenuBlueprint
{
    internal ImageMenuBlueprint(
        string extension,
        string menuId,
        string parentKey,
        string subCommandsPath,
        string commandParentKey,
        bool sharesCommandTree,
        IReadOnlyList<ContextMenuCatalog.ConversionEntry> entries)
    {
        Extension = extension;
        MenuId = menuId;
        ParentKey = parentKey;
        SubCommandsPath = subCommandsPath;
        CommandParentKey = commandParentKey;
        SharesCommandTree = sharesCommandTree;
        Entries = entries;
        Formats = entries.Select(entry => entry.TargetFormat).ToArray();
    }

    internal string Extension { get; }
    internal string MenuId { get; }
    internal string ParentKey { get; }
    internal string SubCommandsPath { get; }
    internal string CommandParentKey { get; }
    internal bool SharesCommandTree { get; }
    internal IReadOnlyList<ContextMenuCatalog.ConversionEntry> Entries { get; }
    internal IReadOnlyList<string> Formats { get; }
}

internal sealed class ContentMenuBlueprint
{
    internal ContentMenuBlueprint(
        string extension,
        string menuId,
        string parentKey,
        string subCommandsPath,
        string commandParentKey,
        bool sharesCommandTree,
        IReadOnlyList<ContextMenuCatalog.ConversionEntry> entries)
    {
        Extension = extension;
        MenuId = menuId;
        ParentKey = parentKey;
        SubCommandsPath = subCommandsPath;
        CommandParentKey = commandParentKey;
        SharesCommandTree = sharesCommandTree;
        Entries = entries;
        Formats = entries.Select(entry => entry.TargetFormat).ToArray();
    }

    internal string Extension { get; }
    internal string MenuId { get; }
    internal string ParentKey { get; }
    internal string SubCommandsPath { get; }
    internal string CommandParentKey { get; }
    internal bool SharesCommandTree { get; }
    internal IReadOnlyList<ContextMenuCatalog.ConversionEntry> Entries { get; }
    internal IReadOnlyList<string> Formats { get; }
}

internal sealed class MediaMenuBlueprint
{
    internal MediaMenuBlueprint(
        string extension,
        string menuId,
        string parentKey,
        string subCommandsPath,
        string commandParentKey,
        bool sharesCommandTree,
        IReadOnlyList<ContextMenuCatalog.ConversionEntry> entries)
    {
        Extension = extension;
        MenuId = menuId;
        ParentKey = parentKey;
        SubCommandsPath = subCommandsPath;
        CommandParentKey = commandParentKey;
        SharesCommandTree = sharesCommandTree;
        Entries = entries;
        Formats = entries.Select(entry => entry.TargetFormat).ToArray();
    }

    internal string Extension { get; }
    internal string MenuId { get; }
    internal string ParentKey { get; }
    internal string SubCommandsPath { get; }
    internal string CommandParentKey { get; }
    internal bool SharesCommandTree { get; }
    internal IReadOnlyList<ContextMenuCatalog.ConversionEntry> Entries { get; }
    internal IReadOnlyList<string> Formats { get; }
}

internal sealed class DocumentMenuBlueprint
{
    internal DocumentMenuBlueprint(
        string extension,
        string menuId,
        string parentKey,
        string subCommandsPath,
        string commandParentKey,
        bool sharesCommandTree,
        IReadOnlyList<ContextMenuCatalog.ConversionEntry> entries)
    {
        Extension = extension;
        MenuId = menuId;
        ParentKey = parentKey;
        SubCommandsPath = subCommandsPath;
        CommandParentKey = commandParentKey;
        SharesCommandTree = sharesCommandTree;
        Entries = entries;
        Formats = entries.Select(entry => entry.TargetFormat).ToArray();
    }

    internal string Extension { get; }
    internal string MenuId { get; }
    internal string ParentKey { get; }
    internal string SubCommandsPath { get; }
    internal string CommandParentKey { get; }
    internal bool SharesCommandTree { get; }
    internal IReadOnlyList<ContextMenuCatalog.ConversionEntry> Entries { get; }
    internal IReadOnlyList<string> Formats { get; }
}

internal sealed class ContextMenuRegistryBackup
{
    internal ContextMenuRegistryBackup(IReadOnlyList<RegistryKeyBackup> entries, string owner)
    {
        Entries = entries;
        Owner = owner;
    }

    internal IReadOnlyList<RegistryKeyBackup> Entries { get; }
    internal string Owner { get; }

    internal static ContextMenuRegistryBackup Capture(ContextMenuPlan plan, IRegistryStore store)
    {
        string root = plan.Keys.OrderBy(key => key.Length).ThenBy(key => key, StringComparer.OrdinalIgnoreCase).First();
        string owner = plan.Value(root, ContextMenuNext.OwnerValue);
        var entries = new List<RegistryKeyBackup>();
        foreach (string key in plan.Keys)
        {
            var names = plan.Values.Where(value => value.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .Select(value => value.Name)
                .Append("")
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var absent = new List<string>();
            bool existed = store.KeyExists(key);
            if (existed)
            {
                foreach (string name in names)
                {
                    string? data = store.Read(key, name);
                    if (data != null) snapshot[name] = data;
                    else absent.Add(name);
                }
                foreach (string extra in new[] { LegacyIdentity.OwnerValueName, LegacyIdentity.AncestorValueName })
                {
                    if (snapshot.ContainsKey(extra) || absent.Contains(extra, StringComparer.OrdinalIgnoreCase)) continue;
                    string? data = store.Read(key, extra);
                    if (data != null) snapshot[extra] = data;
                    else absent.Add(extra);
                }
            }
            entries.Add(new RegistryKeyBackup(key, existed, snapshot, absent));
        }
        foreach (string key in plan.Keys)
        {
            string legacy = ContextMenuNext.LegacyKeyFor(key, owner);
            if (legacy.Equals(key, StringComparison.OrdinalIgnoreCase) || ContextMenuNext.IsLegacyMenuProtected(legacy)) continue;
            bool existed = store.KeyExists(legacy);
            var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (existed)
            {
                foreach (string name in store.ValueNames(legacy))
                {
                    string? data = store.Read(legacy, name);
                    if (data != null) snapshot[name] = data;
                }
            }
            entries.Add(new RegistryKeyBackup(legacy, existed, snapshot, []));
        }
        return new ContextMenuRegistryBackup(entries, owner);
    }

    internal string Export()
    {
        var text = new StringBuilder();
        foreach (var entry in Entries.OrderBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase))
        {
            text.Append(entry.Path).Append(entry.Existed ? " (existing)" : " (new)").Append('\n');
            foreach (var pair in entry.Values.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                text.Append("  ").Append(pair.Key.Length == 0 ? "(default)" : pair.Key).Append('=').Append(pair.Value).Append('\n');
        }
        return text.ToString();
    }
}

internal sealed record RegistryKeyBackup(
    string Path,
    bool Existed,
    IReadOnlyDictionary<string, string> Values,
    IReadOnlyList<string> AbsentNames);
