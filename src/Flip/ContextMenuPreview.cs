using System.Drawing;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace Flip;

// ExtendedSubCommandsKey è un REG_SZ, non una sottochiave: Explorer apre
// HKCR\<valore>\shell. La pagina Learn lo disegna come sottochiave, ma la shell
// non enumera quel nome; il campione che funziona davvero (menu "Command Prompt")
// usa il valore. I tre verbi stanno nella shell annidata di Flip.Convert.Preview
// e il valore punta a quel verbo: un solo albero sotto SystemFileAssociations\.png,
// il predefinito del padre resta vuoto e il prototipo non è riusabile da altre estensioni.
internal static class ContextMenuPreview
{
    internal const string PrototypeId = "Flip.Convert.Preview";
    internal const string ParentVerb = "Converti con Flip (Anteprima)";
    internal const string ParentKey = @"Software\Classes\SystemFileAssociations\.png\shell\Flip.Convert.Preview";
    internal const string SubCommandsPath = @"SystemFileAssociations\.png\shell\Flip.Convert.Preview";
    internal const string OriginalMenuKey = @"Software\Classes\*\shell\Flip.Convert";
    internal const string OwnerValue = "FlipPrototype";
    internal static readonly string[] Formats = ["JPG", "PDF", "WEBP"];

    internal static bool IsPreviewCommand(string[] args) =>
        args.Contains("--install-context-menu-preview") || args.Contains("--remove-context-menu-preview");

    internal static string IconFileName(string format) => "flip-menu-" + format.ToLowerInvariant() + "-premium-v2.ico";

    internal static string CommandLine(string executable, string format) =>
        $"\"{executable}\" --convert-menu {format} --files \"%1\"";

    internal static ContextMenuPlan CreatePlan(string executable, ContextMenuCatalog.EngineSnapshot engines)
    {
        ValidateExecutable(executable);
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
            new(ParentKey, OwnerValue, PrototypeId),
            new(ParentKey + @"\shell", OwnerValue, PrototypeId)
        };
        foreach (var entry in selected)
        {
            string verb = VerbKey(entry.TargetFormat);
            values.Add(new(verb, "MUIVerb", entry.Title));
            values.Add(new(verb, "Icon", Path.GetFullPath(Path.Combine(directory, IconFileName(entry.TargetFormat)))));
            values.Add(new(verb, "MultiSelectModel", "Single"));
            values.Add(new(verb, "NeverDefault", ""));
            values.Add(new(verb, OwnerValue, PrototypeId));
            values.Add(new(verb + @"\command", "", CommandLine(executable, entry.TargetFormat)));
            values.Add(new(verb + @"\command", OwnerValue, PrototypeId));
        }
        return new ContextMenuPlan(executable, Formats.ToArray(), values);
    }

    internal static void EnsureIcons(ContextMenuPlan plan)
    {
        foreach (string format in plan.Formats)
            if (!IconIsValid(plan.Icon(format))) throw new IOException("Icona del menu di anteprima assente o non valida: " + plan.Icon(format));
    }

    internal static bool IconIsValid(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
        byte[] data;
        try { data = File.ReadAllBytes(path); }
        catch (IOException) { return false; }
        if (!IcoHasSizes(data, 16, 32, 48, 64, 128, 256)) return false;
        try
        {
            using var icon = new Icon(path);
            return FrameHasContrast(icon, 16) && FrameHasContrast(icon, 32);
        }
        catch (ArgumentException) { return false; }
        catch (IOException) { return false; }
        catch (OutOfMemoryException) { return false; }
    }

    internal static void Install(ContextMenuPlan plan, IRegistryStore store)
    {
        foreach (string key in plan.Keys)
        {
            if (!key.StartsWith(ParentKey, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu di anteprima uscirebbe dal prototipo PNG.");
            if (key.Equals(OriginalMenuKey, StringComparison.OrdinalIgnoreCase) || key.StartsWith(OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase))
                throw new IOException("Il menu originale non può essere modificato.");
            if (store.KeyExists(key) && store.Read(key, OwnerValue) != PrototypeId)
                throw new IOException("Una chiave del menu di anteprima esiste già con un proprietario o un comando diverso: è stata preservata.");
        }
        foreach (var value in plan.Values) store.Write(value.Key, value.Name, value.Data);
    }

    internal static void Remove(IRegistryStore store)
    {
        foreach (string key in OwnedKeyPaths().OrderByDescending(path => path.Count(character => character == '\\')))
        {
            if (!store.KeyExists(key) || store.Read(key, OwnerValue) != PrototypeId) continue;
            if (store.SubKeys(key).Count > 0) continue;
            store.DeleteKey(key);
        }
    }

    internal static void Apply(bool remove)
    {
        string executable = Environment.ProcessPath ?? throw new IOException("Percorso eseguibile assente.");
        ValidateExecutable(executable);
        if (remove) Remove(new WindowsRegistryStore());
        else
        {
            var plan = CreatePlan(executable, ContextMenuCatalog.EngineSnapshot.Detected);
            EnsureIcons(plan);
            Install(plan, new WindowsRegistryStore());
        }
        Evidence.Log(remove ? "shell.preview.remove" : "shell.preview.install", executable);
    }

    internal static string VerbKey(string format) => ParentKey + @"\shell\" + format.ToLowerInvariant();

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

    static void ValidateExecutable(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable) || !Path.IsPathRooted(executable) || executable.IndexOf('"') >= 0)
            throw new IOException("Percorso eseguibile non valido.");
        if (!Path.GetFileName(executable).Equals("Flip.exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Usare Flip.exe, non dotnet Flip.dll, per configurare il menu.");
    }

    static bool IcoHasSizes(byte[] data, params int[] sizes)
    {
        if (data.Length < 6 || BitConverter.ToUInt16(data, 0) != 0 || BitConverter.ToUInt16(data, 2) != 1) return false;
        int count = BitConverter.ToUInt16(data, 4);
        if (count < sizes.Length || data.Length < 6 + 16 * count) return false;
        var found = new HashSet<int>();
        for (int i = 0; i < count; i++)
        {
            int entry = 6 + 16 * i;
            int width = data[entry] == 0 ? 256 : data[entry];
            int height = data[entry + 1] == 0 ? 256 : data[entry + 1];
            int bytes = BitConverter.ToInt32(data, entry + 8);
            int image = BitConverter.ToInt32(data, entry + 12);
            if (bytes <= 0 || image < 0 || image > data.Length || bytes > data.Length - image) return false;
            if (width == height) found.Add(width);
        }
        return sizes.All(found.Contains);
    }

    static bool FrameHasContrast(Icon source, int size)
    {
        using var sized = new Icon(source, new Size(size, size));
        using var bitmap = sized.ToBitmap();
        if (bitmap.Width < 1 || bitmap.Height < 1) return false;
        int first = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height / 2).ToArgb();
        int stepX = Math.Max(1, bitmap.Width / 8);
        int stepY = Math.Max(1, bitmap.Height / 8);
        for (int y = 0; y < bitmap.Height; y += stepY)
            for (int x = 0; x < bitmap.Width; x += stepX)
                if (bitmap.GetPixel(x, y).ToArgb() != first) return true;
        return false;
    }

    sealed class WindowsRegistryStore : IRegistryStore
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

        public void Write(string path, string name, string data)
        {
            using var key = Registry.CurrentUser.CreateSubKey(path);
            key.SetValue(name, data, RegistryValueKind.String);
        }

        public void DeleteKey(string path)
        {
            if (SubKeys(path).Count > 0) throw new IOException("La chiave ha sottochiavi non rimosse.");
            Registry.CurrentUser.DeleteSubKey(path, false);
        }
    }
}

internal interface IRegistryStore
{
    bool KeyExists(string path);
    string? Read(string path, string name);
    IReadOnlyList<string> SubKeys(string path);
    void Write(string path, string name, string data);
    void DeleteKey(string path);
}

internal sealed class ContextMenuPlan
{
    internal ContextMenuPlan(string executable, string[] formats, IReadOnlyList<RegistryValue> values)
    {
        Executable = executable;
        Formats = formats;
        Values = values;
        Keys = values.Select(value => value.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal string Executable { get; }
    internal IReadOnlyList<string> Formats { get; }
    internal IReadOnlyList<RegistryValue> Values { get; }
    internal IReadOnlyList<string> Keys { get; }
    internal string Command(string format) => Required(ContextMenuPreview.VerbKey(format) + @"\command", "");
    internal string Icon(string format) => Required(ContextMenuPreview.VerbKey(format), "Icon");
    internal string VerbTitle(string format) => Required(ContextMenuPreview.VerbKey(format), "MUIVerb");
    internal string Value(string key, string name) => Required(key, name);

    string Required(string key, string name)
    {
        var found = Values.FirstOrDefault(value => value.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && value.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return found?.Data ?? throw new IOException("Valore assente nel piano del menu.");
    }
}

internal sealed class RegistryValue
{
    internal RegistryValue(string key, string name, string data)
    {
        Key = key;
        Name = name;
        Data = data;
    }

    internal string Key { get; }
    internal string Name { get; }
    internal string Data { get; }
}

internal sealed class MemoryRegistryStore : IRegistryStore
{
    readonly Dictionary<string, Dictionary<string, string>> keys = new(StringComparer.OrdinalIgnoreCase);

    internal int KeyCount => keys.Count;

    internal string Export()
    {
        var text = new StringBuilder();
        foreach (string path in keys.Keys.OrderBy(key => key, StringComparer.OrdinalIgnoreCase))
        {
            text.Append(path).Append('\n');
            foreach (var pair in keys[path].OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
                text.Append("  ").Append(pair.Key).Append('=').Append(pair.Value).Append('\n');
        }
        return text.ToString();
    }

    public bool KeyExists(string path) => keys.ContainsKey(path);

    public string? Read(string path, string name) =>
        keys.TryGetValue(path, out var values) && values.TryGetValue(name, out string? data) ? data : null;

    public IReadOnlyList<string> SubKeys(string path)
    {
        string prefix = path + @"\";
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string key in keys.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            string rest = key[prefix.Length..];
            int slash = rest.IndexOf('\\');
            names.Add(slash < 0 ? rest : rest[..slash]);
        }
        return names.ToArray();
    }

    public void Write(string path, string name, string data)
    {
        if (!keys.TryGetValue(path, out var values))
        {
            values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            keys[path] = values;
        }
        values[name] = data;
    }

    public void DeleteKey(string path)
    {
        if (!keys.ContainsKey(path)) return;
        if (SubKeys(path).Count > 0) throw new IOException("La chiave ha sottochiavi non rimosse.");
        keys.Remove(path);
    }
}
