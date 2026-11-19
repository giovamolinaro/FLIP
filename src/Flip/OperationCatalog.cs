using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Flip;

/// <summary>Catalogo interno delle operazioni FLIP (Fase 0). Non collegato alla ruota; coerenza con <see cref="Policy.Outputs"/>.</summary>
internal static class OperationCatalog
{
    internal enum OperationKind { SimpleConversion, AdvancedPdf }
    internal enum OperationStatus { Implemented, Verified, Partial, NotAvailable }

    internal sealed record EngineNeeds(bool Ffmpeg = false, bool Pandoc = false, bool Office = false);

    internal sealed record Operation(
        string Id,
        OperationKind Kind,
        string[] InputExtensions,
        string OutputFormat,
        int RequiredFileCount,
        string[]? Parameters,
        EngineNeeds Engines,
        OperationStatus Status,
        string QualityLimits);

    static readonly Lazy<IReadOnlyList<Operation>> Catalog = new(Build);
    internal static IReadOnlyList<Operation> All => Catalog.Value;

    internal static Operation? Find(string id) => All.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.Ordinal));

    internal static bool IsSelectable(Operation operation, int fileCount, bool pandoc, bool office, bool ffmpeg)
    {
        if (operation.Status == OperationStatus.NotAvailable) return false;
        if (operation.Kind == OperationKind.AdvancedPdf) return false;
        if (fileCount < operation.RequiredFileCount || fileCount is 0 or > 128) return false;
        if (operation.Engines.Ffmpeg && !ffmpeg) return false;
        if (operation.Engines.Pandoc && !pandoc) return false;
        if (operation.Engines.Office && !office) return false;
        string ext = operation.InputExtensions[0];
        return Policy.Outputs(["catalog-probe" + ext], pandoc, office, ffmpeg).Contains(operation.OutputFormat);
    }

    internal static IReadOnlyList<Operation> SelectableFor(string path, int fileCount, bool pandoc, bool office, bool ffmpeg) =>
        All.Where(o => IsSelectable(o, fileCount, pandoc, office, ffmpeg) && MatchesInput(path, o)).ToArray();

    static bool MatchesInput(string path, Operation operation)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return operation.InputExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    static IReadOnlyList<Operation> Build()
    {
        var list = new List<Operation>();
        list.AddRange(ImageConversions());
        list.AddRange(DataConversions());
        list.AddRange(AudioConversions());
        list.AddRange(VideoConversions());
        list.AddRange(PdfConversions());
        list.AddRange(TextConversions());
        list.AddRange(DocumentConversions());
        list.AddRange(AdvancedPdfOperations());
        list.AddRange(ExcludedFormatPlaceholders());
        return list;
    }

    static IEnumerable<Operation> ImageConversions()
    {
        string limits = "Immagine nativa SkiaSharp; JPEG qualità 92; limite sorgente 100 MiB e 40 megapixel.";
        foreach (string ext in Policy.Images)
        {
            foreach (string format in new[] { "PNG", "JPG", "WEBP", "PDF", "BMP", "TIFF", "GIF" })
            {
                if (SameAsInput(ext, format)) continue;
                yield return Simple(Id("image", ext, format), ext, format, 1, [], new EngineNeeds(), OperationStatus.Verified, limits);
            }
        }
    }

    static IEnumerable<Operation> DataConversions()
    {
        string limits = "Conversione dati incorporata; limite sorgente 100 MiB.";
        foreach (string ext in Policy.Data)
        {
            foreach (string format in new[] { "JSON", "YAML", "XML", "CSV", "TXT", "HTML", "MD", "PDF" })
            {
                if (SameAsInput(ext, format)) continue;
                yield return Simple(Id("data", ext, format), ext, format, 1, [], new EngineNeeds(), OperationStatus.Verified, limits);
            }
        }
    }

    static IEnumerable<Operation> AudioConversions()
    {
        string limits = "Transcodifica FFmpeg locale; timeout dedicato; sorgente massima 2 GiB.";
        var needs = new EngineNeeds(Ffmpeg: true);
        foreach (string ext in Policy.Audio)
        {
            foreach (string format in new[] { "WAV", "MP3", "FLAC", "OGG", "M4A", "AAC" })
            {
                if (SameAsInput(ext, format)) continue;
                yield return Simple(Id("audio", ext, format), ext, format, 1, [], needs, OperationStatus.Verified, limits);
            }
        }
    }

    static IEnumerable<Operation> VideoConversions()
    {
        string limits = "Transcodifica o estrazione audio FFmpeg; sorgente massima 2 GiB.";
        var needs = new EngineNeeds(Ffmpeg: true);
        foreach (string ext in Policy.Video)
        {
            foreach (string format in new[] { "MP4", "MP3", "WAV", "MKV", "WEBM", "AVI", "MOV" })
            {
                if (SameAsInput(ext, format)) continue;
                var status = format is "MP3" or "WAV" ? OperationStatus.Verified : OperationStatus.Implemented;
                yield return Simple(Id("video", ext, format), ext, format, 1, [], needs, status, limits);
            }
        }
    }

    static IEnumerable<Operation> PdfConversions()
    {
        string raster = "Raster pagine PDFtoImage; massimo 200 pagine; 144 DPI.";
        string textOnly = "Testo estratto con PdfPig; layout e immagini non preservati.";
        foreach (string format in new[] { "PNG", "JPG", "WEBP", "TXT" })
            yield return Simple(Id("pdf", ".pdf", format), ".pdf", format, 1, [], new EngineNeeds(),
                format == "TXT" ? OperationStatus.Verified : OperationStatus.Verified, format == "TXT" ? textOnly : raster);
        foreach (string format in new[] { "DOCX", "MD", "HTML", "EPUB", "ODT", "RTF", "FB2", "RST" })
            yield return Simple(Id("pdf", ".pdf", format), ".pdf", format, 1, [], new EngineNeeds(Pandoc: true),
                OperationStatus.Partial, textOnly);
    }

    static IEnumerable<Operation> TextConversions()
    {
        string native = "Percorsi nativi TXT→PDF/immagini senza Pandoc; limite testo 2 MiB.";
        string doc = "Conversione documento Pandoc; PDF/immagini richiedono LibreOffice salvo TXT.";
        foreach (string format in new[] { "PDF", "PNG", "JPG", "WEBP" })
            yield return Simple(Id("text", ".txt", format), ".txt", format, 1, [], new EngineNeeds(), OperationStatus.Verified, native);
        foreach (string format in new[] { "DOCX", "EPUB", "MD", "HTML", "ODT", "RTF", "FB2", "RST" })
            yield return Simple(Id("text", ".txt", format), ".txt", format, 1, [], new EngineNeeds(Pandoc: true), OperationStatus.Verified, doc);
    }

    static IEnumerable<Operation> DocumentConversions()
    {
        string doc = "Conversione Pandoc; PDF e anteprime immagine richiedono LibreOffice.";
        string partialPdf = "PDF da documenti tramite LibreOffice; qualità dipendente dal motore esterno.";
        foreach (string ext in Policy.Documents.Where(e => e != ".txt"))
        {
            foreach (string format in new[] { "DOCX", "EPUB", "MD", "HTML", "TXT", "ODT", "RTF", "FB2", "RST" })
            {
                if (SameAsInput(ext, format)) continue;
                yield return Simple(Id("document", ext, format), ext, format, 1, [], new EngineNeeds(Pandoc: true), OperationStatus.Verified, doc);
            }
            foreach (string format in new[] { "PDF", "PNG", "JPG", "WEBP" })
                yield return Simple(Id("document", ext, format), ext, format, 1, [], new EngineNeeds(Pandoc: true, Office: true),
                    format == "PDF" ? OperationStatus.Verified : OperationStatus.Verified, partialPdf);
        }
    }

    static IEnumerable<Operation> AdvancedPdfOperations()
    {
        string future = "Operazione PDF avanzata non ancora esposta nella ruota (Fase 0).";
        yield return Advanced("pdf.advanced.merge", [".pdf"], "PDF", 2, ["ordine-pagine"], OperationStatus.NotAvailable, future);
        yield return Advanced("pdf.advanced.split", [".pdf"], "PDF", 1, ["intervallo-pagine"], OperationStatus.NotAvailable, future);
        yield return Advanced("pdf.advanced.compress", [".pdf"], "PDF", 1, ["livello"], OperationStatus.NotAvailable, future);
        yield return Advanced("pdf.advanced.rotate", [".pdf"], "PDF", 1, ["angolo", "pagine"], OperationStatus.NotAvailable, future);
        yield return Advanced("pdf.advanced.extract-pages", [".pdf"], "PDF", 1, ["intervallo-pagine"], OperationStatus.NotAvailable, future);
        yield return Advanced("pdf.advanced.watermark", [".pdf"], "PDF", 1, ["testo|immagine"], OperationStatus.NotAvailable, future);
        yield return Advanced("pdf.advanced.protect", [".pdf"], "PDF", 1, ["password"], OperationStatus.NotAvailable, future);
        yield return Advanced("pdf.advanced.ocr", [".pdf"], "TXT", 1, ["lingua"], OperationStatus.NotAvailable, "OCR non promesso nelle capability attuali.");
    }

    static IEnumerable<Operation> ExcludedFormatPlaceholders()
    {
        string note = "Formato escluso dal perimetro FLIP; voce solo descrittiva nel catalogo.";
        yield return Excluded("format.excluded.pages", [".pages"], "PAGES", note);
    }

    static Operation Simple(string id, string ext, string format, int files, string[] parameters, EngineNeeds engines, OperationStatus status, string limits) =>
        new(id, OperationKind.SimpleConversion, [ext], format, files, parameters, engines, status, limits);

    static Operation Advanced(string id, string[] extensions, string format, int files, string[] parameters, OperationStatus status, string limits) =>
        new(id, OperationKind.AdvancedPdf, extensions, format, files, parameters, new EngineNeeds(), status, limits);

    static Operation Excluded(string id, string[] extensions, string format, string limits) =>
        new(id, OperationKind.SimpleConversion, extensions, format, 1, [], new EngineNeeds(), OperationStatus.NotAvailable, limits);

    static string Id(string category, string ext, string format) =>
        category + "." + ext.TrimStart('.').Replace('.', '-') + ".to." + format.ToLowerInvariant();

    static bool SameAsInput(string ext, string format)
    {
        string same = ext.TrimStart('.') switch
        {
            "jpeg" => "JPG",
            "jpg" => "JPG",
            "tif" => "TIFF",
            "tiff" => "TIFF",
            "markdown" => "MD",
            "md" => "MD",
            "htm" => "HTML",
            "html" => "HTML",
            "yml" => "YAML",
            "yaml" => "YAML",
            var e => e.ToUpperInvariant()
        };
        return same == format;
    }
}