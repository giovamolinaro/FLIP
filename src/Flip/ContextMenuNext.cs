using System.IO;
using System.Text;

namespace Flip;

// Piano M5.1: menu contestuale PNG definitivo (JPG, PDF, WEBP) senza dicitura di anteprima.
// Non sostituisce Flip.Convert (globale) né Flip.Convert.Preview; chiavi e proprietario distinti.
internal static class ContextMenuNext
{
    internal const string MenuId = "Flip.Convert.Png";
    internal const string ParentVerb = "Converti con Flip";
    internal const string ParentKey = @"Software\Classes\SystemFileAssociations\.png\shell\Flip.Convert.Png";
    internal const string SubCommandsPath = @"SystemFileAssociations\.png\shell\Flip.Convert.Png";
    internal const string OwnerValue = "FlipOwner";
    internal const string LaboratorySegment = "Flip-Lab";
    internal static readonly string[] Formats = ContextMenuPreview.Formats;

    static readonly string StableProgramsFlip = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs", "Flip");

    internal static bool IsPlannedCommand(string[] args) =>
        args.Contains("--install-context-menu-png") || args.Contains("--remove-context-menu-png");

    internal static string CommandLine(string executable, string format) =>
        ContextMenuPreview.CommandLine(executable, format);

    internal static ContextMenuPlan CreatePlan(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        ValidateRegistrableExecutable(executable);
        var catalog = ContextMenuCatalog.ForExtension(".png", engines);
        var selected = new List<ContextMenuCatalog.ConversionEntry>();
        foreach (string format in Formats)
        {
            var entry = catalog.FirstOrDefault(item => item.TargetFormat.Equals(format, StringComparison.Ordinal));
            if (entry == null) throw new IOException("Il catalogo PNG non offre il formato " + format + ".");
            selected.Add(entry);
        }
        string directory = Path.GetDirectoryName(executable) ?? throw new IOException("Percorso eseguibile assente.");
        var values = new List<RegistryValue>
        {
            new(ParentKey, "MUIVerb", ParentVerb),
            new(ParentKey, "ExtendedSubCommandsKey", SubCommandsPath),
            new(ParentKey, "MultiSelectModel", "Single"),
            new(ParentKey, "NeverDefault", ""),
            new(ParentKey, OwnerValue, MenuId),
            new(ParentKey + @"\shell", OwnerValue, MenuId)
        };
        foreach (var entry in selected)
        {
            string verb = VerbKey(entry.TargetFormat);
            values.Add(new(verb, "MUIVerb", entry.Title));
            values.Add(new(verb, "Icon", Path.GetFullPath(Path.Combine(directory, ContextMenuPreview.IconFileName(entry.TargetFormat)))));
            values.Add(new(verb, "MultiSelectModel", "Single"));
            values.Add(new(verb, "NeverDefault", ""));
            values.Add(new(verb, OwnerValue, MenuId));
            values.Add(new(verb + @"\command", "", CommandLine(executable, entry.TargetFormat)));
            values.Add(new(verb + @"\command", OwnerValue, MenuId));
        }
        return new ContextMenuPlan(executable, Formats.ToArray(), values, VerbKey);
    }

    internal static void EnsureIcons(ContextMenuPlan plan)
    {
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
        foreach (var value in plan.Values) store.Write(value.Key, value.Name, value.Data);
    }

    internal static void Remove(IRegistryStore store)
    {
        foreach (string key in OwnedKeyPaths().OrderByDescending(path => path.Count(character => character == '\\')))
        {
            if (!store.KeyExists(key) || store.Read(key, OwnerValue) != MenuId) continue;
            if (store.SubKeys(key).Count > 0) continue;
            store.DeleteKey(key);
        }
    }

    internal static void RestoreBackup(ContextMenuRegistryBackup backup, IRegistryStore store)
    {
        foreach (var entry in backup.Entries)
        {
            if (!entry.Existed)
            {
                if (store.KeyExists(entry.Path) && store.Read(entry.Path, OwnerValue) == MenuId)
                {
                    if (store.SubKeys(entry.Path).Count == 0) store.DeleteKey(entry.Path);
                }
                continue;
            }
            foreach (var pair in entry.Values)
            {
                if (pair.Key.Length == 0)
                    store.Write(entry.Path, "", pair.Value);
                else
                    store.Write(entry.Path, pair.Key, pair.Value);
            }
        }
    }

    internal static string VerbKey(string format) => ParentKey + @"\shell\" + format.ToLowerInvariant();

    internal static void ValidateRegistrableExecutable(string executable)
    {
        ContextMenuPreview.ValidateExecutable(executable);
        string full = Path.GetFullPath(executable);
        if (PathUsesBuildArtifact(full))
            throw new IOException("Non registrare Flip.exe da cartelle bin o obj.");
        if (IsUnauthorizedTemporaryBuild(full) && !IsLaboratoryPath(full) && !IsStableInstallPath(full))
            throw new IOException("Build temporanea non autorizzata per il menu contestuale.");
    }

    internal static bool IsStableInstallPath(string executable)
    {
        try
        {
            string flipDir = Path.GetFullPath(StableProgramsFlip);
            string dir = Path.GetDirectoryName(Path.GetFullPath(executable)) ?? "";
            return dir.Equals(flipDir, StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException) { return false; }
        catch (ArgumentException) { return false; }
    }

    internal static bool IsLaboratoryPath(string executable) =>
        Path.GetFullPath(executable).Contains(LaboratorySegment, StringComparison.OrdinalIgnoreCase);

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

    static void GuardForeignMenus(ContextMenuPlan plan, IRegistryStore store)
    {
        foreach (string key in plan.Keys)
        {
            if (!key.StartsWith(ParentKey, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu PNG uscirebbe dal perimetro previsto.");
            if (key.Equals(ContextMenuPreview.OriginalMenuKey, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(ContextMenuPreview.OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu originale non può essere modificato.");
            if (key.Equals(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(ContextMenuPreview.ParentKey + @"\", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu di anteprima non può essere modificato.");
            if (store.KeyExists(key) && store.Read(key, OwnerValue) != MenuId)
                throw new IOException("Una chiave del menu PNG esiste già con un proprietario o un comando diverso: è stata preservata.");
        }
    }

    static bool PathUsesBuildArtifact(string fullPath)
    {
        foreach (string segment in fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            if (segment.Equals("bin", StringComparison.OrdinalIgnoreCase) || segment.Equals("obj", StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    static bool IsUnauthorizedTemporaryBuild(string fullPath)
    {
        string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string normalized = Path.GetFullPath(fullPath);
        return normalized.StartsWith(temp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(temp, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class ContextMenuRegistryBackup
{
    internal ContextMenuRegistryBackup(IReadOnlyList<RegistryKeyBackup> entries) => Entries = entries;

    internal IReadOnlyList<RegistryKeyBackup> Entries { get; }

    internal static ContextMenuRegistryBackup Capture(ContextMenuPlan plan, IRegistryStore store)
    {
        var entries = new List<RegistryKeyBackup>();
        foreach (string key in plan.Keys)
        {
            var names = plan.Values.Where(value => value.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .Select(value => value.Name)
                .Append("")
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var snapshot = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool existed = store.KeyExists(key);
            if (existed)
            {
                foreach (string name in names)
                {
                    string? data = store.Read(key, name);
                    if (data != null) snapshot[name] = data;
                }
            }
            entries.Add(new RegistryKeyBackup(key, existed, snapshot));
        }
        return new ContextMenuRegistryBackup(entries);
    }

    internal string Export()
    {
        var text = new StringBuilder();
        foreach (var entry in Entries.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase))
        {
            text.Append(entry.Path).Append(entry.Existed ? " (existing)" : " (new)").Append('\n');
            foreach (var pair in entry.Values.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
                text.Append("  ").Append(pair.Key.Length == 0 ? "(default)" : pair.Key).Append('=').Append(pair.Value).Append('\n');
        }
        return text.ToString();
    }
}

internal sealed record RegistryKeyBackup(string Path, bool Existed, IReadOnlyDictionary<string, string> Values);