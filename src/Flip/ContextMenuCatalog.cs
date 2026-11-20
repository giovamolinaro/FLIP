using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flip;

/// <summary>Conversioni disponibili per estensione sorgente (sottomenu contestuale; Fase M1). Solo <see cref="Policy.Outputs"/>.</summary>
internal static class ContextMenuCatalog
{
    internal readonly record struct EngineSnapshot(bool Pandoc, bool Office, bool Ffmpeg)
    {
        internal static EngineSnapshot Detected => new(DocumentTools.HasPandoc, DocumentTools.HasOffice, MediaTools.HasFfmpeg);

        internal static IEnumerable<EngineSnapshot> AllCombinations()
        {
            foreach (bool pandoc in new[] { false, true })
            foreach (bool office in new[] { false, true })
            foreach (bool ffmpeg in new[] { false, true })
                yield return new EngineSnapshot(pandoc, office, ffmpeg);
        }
    }

    internal sealed record ConversionEntry(
        string Id,
        string SourceExtension,
        string TargetFormat,
        string Title,
        string Description,
        string IconId);

    static readonly string[] SupportedExtensions = BuildSupportedExtensions();

    internal static IReadOnlyList<string> SupportedSourceExtensions() => SupportedExtensions;

    internal static ConversionEntry[] ForFile(string path, EngineSnapshot? engines = null)
    {
        engines ??= EngineSnapshot.Detected;
        return ForExtension(Path.GetExtension(path), engines.Value);
    }

    internal static ConversionEntry[] ForExtension(string extension, EngineSnapshot engines)
    {
        var ext = NormalizeExtension(extension);
        if (!IsSupportedExtension(ext)) return [];
        string[] outputs = Policy.Outputs(["flip.context" + ext], engines.Pandoc, engines.Office, engines.Ffmpeg);
        if (outputs.Length == 0) return [];
        return Wheel.OrderFormats(outputs).Select(format => CreateEntry(ext, format)).ToArray();
    }

    internal static IReadOnlyDictionary<string, ConversionEntry[]> BuildMenu(EngineSnapshot engines)
    {
        var map = new Dictionary<string, ConversionEntry[]>(StringComparer.OrdinalIgnoreCase);
        foreach (string ext in SupportedExtensions)
        {
            ConversionEntry[] entries = ForExtension(ext, engines);
            if (entries.Length > 0) map[ext] = entries;
        }
        return map;
    }

    static string[] BuildSupportedExtensions()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddRange(IEnumerable<string> extensions) { foreach (string e in extensions) set.Add(NormalizeExtension(e)); }
        AddRange(Policy.Images);
        AddRange(Policy.Documents);
        AddRange(Policy.Data);
        AddRange(Policy.Audio);
        AddRange(Policy.Video);
        set.Add(".pdf");
        return set.OrderBy(e => e, StringComparer.Ordinal).ToArray();
    }

    static bool IsSupportedExtension(string extension) =>
        Array.IndexOf(SupportedExtensions, extension) >= 0;

    static string NormalizeExtension(string extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? ""
            : (extension.StartsWith('.') ? extension : "." + extension).ToLowerInvariant();

    static ConversionEntry CreateEntry(string sourceExtension, string targetFormat)
    {
        string stem = sourceExtension.TrimStart('.').Replace('.', '-');
        string formatKey = targetFormat.ToLowerInvariant();
        return new ConversionEntry(
            Id: "context." + stem + ".to." + formatKey,
            SourceExtension: sourceExtension,
            TargetFormat: targetFormat,
            Title: "Converti in " + targetFormat,
            Description: "Converte " + sourceExtension + " in " + targetFormat + " con Flip.",
            IconId: "format." + formatKey);
    }
}