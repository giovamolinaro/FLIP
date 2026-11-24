using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using SkiaSharp;
namespace Flix;

// Collaudo isolato della matrice PDF, TXT e dati M5.3.1. Chiama solo ConversionEngine:
// niente launcher, mutex di sessione, named pipe, Registro o menu contestuali.
internal static class ContentMatrixTests
{
    const string MissingDependency = "NON ESEGUIBILE PER DIPENDENZA ASSENTE";
    const string SentinelText = "NON-SOVRASCRIVERE-M531";
    const string TextSample = "FLIX verifica FLIXCONTENT: caffè, città, Marco, Milano.\nOriginale da conservare.\n";
    const string JsonSample = "[\n  {\"nome\":\"Marco\",\"citta\":\"Milano\",\"attivo\":true,\"anni\":34,\"nota\":\"FLIXCONTENT\"},\n  {\"nome\":\"Giulia\",\"citta\":\"Roma\",\"attivo\":false,\"anni\":29,\"nota\":\"caffe\"}\n]\n";
    const string YamlSample = "nome: Marco\ncitta: Milano\nattivo: true\nanni: 34\nnota: FLIXCONTENT\n";
    const string XmlSample = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<scheda>\n  <nome>Marco</nome>\n  <citta>Milano</citta>\n  <attivo>true</attivo>\n  <anni>34</anni>\n  <nota>FLIXCONTENT</nota>\n</scheda>\n";
    const string CsvSample = "nome,citta,attivo,anni,nota\nMarco,Milano,true,34,FLIXCONTENT\nGiulia,Roma,false,29,caffe\n";

    static readonly UTF8Encoding Utf8 = new(false);
    static readonly string[] Scope = [".pdf", ".txt", ".json", ".yaml", ".yml", ".xml", ".csv"];
    static readonly Dictionary<string, string[]> ExpectedNative = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["PNG", "JPG", "WEBP", "TXT"],
        [".txt"] = ["PDF", "PNG", "JPG", "WEBP"],
        [".json"] = ["YAML", "XML", "CSV", "TXT", "HTML", "MD", "PDF"],
        [".yaml"] = ["JSON", "XML", "CSV", "TXT", "HTML", "MD", "PDF"],
        [".yml"] = ["JSON", "XML", "CSV", "TXT", "HTML", "MD", "PDF"],
        [".xml"] = ["JSON", "YAML", "CSV", "TXT", "HTML", "MD", "PDF"],
        [".csv"] = ["JSON", "YAML", "XML", "TXT", "HTML", "MD", "PDF"]
    };
    static readonly Dictionary<string, string[]> ExpectedOptional = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["DOCX", "MD", "HTML", "EPUB", "ODT", "RTF", "FB2", "RST"],
        [".txt"] = ["DOCX", "EPUB", "MD", "HTML", "ODT", "RTF", "FB2", "RST"]
    };
    static readonly (string Extension, string Format)[] Forbidden =
    [
        (".pdf", "JSON"), (".pdf", "BMP"), (".pdf", "CSV"),
        (".txt", "JSON"), (".txt", "YAML"),
        (".json", "PNG"), (".json", "DOCX"),
        (".csv", "EPUB"), (".yaml", "GIF"), (".xml", "MP3"), (".yml", "RST")
    ];
    static readonly (string Extension, string Format)[] SameFormat =
    [
        (".pdf", "PDF"), (".txt", "TXT"), (".json", "JSON"), (".yaml", "YAML"),
        (".yml", "YAML"), (".xml", "XML"), (".csv", "CSV")
    ];

    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "Flix-M531-content-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        if (Directory.Exists(root))
        {
            Console.Error.WriteLine("Cartella di collaudo già presente, nessuna cancellazione: " + root);
            return 2;
        }
        Directory.CreateDirectory(root);
        string priorLog = Evidence.DirectoryPath;
        Evidence.DirectoryPath = Path.Combine(root, "logs");
        Directory.CreateDirectory(Evidence.DirectoryPath);
        var rows = new List<MatrixRow>();
        var negatives = new List<MatrixRow>();
        string catalogNote = "";
        var officeBefore = Directory.EnumerateDirectories(Path.GetTempPath(), "Flix-LO-*").ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            catalogNote = CheckCatalog(rows);
            bool pandoc = DocumentTools.HasPandoc;
            foreach (string extension in Scope)
            {
                string[] native = Targets(extension, false);
                string[] optional = Targets(extension, true).Where(format => !native.Contains(format, StringComparer.Ordinal)).ToArray();
                RunSource(root, extension, native, "nativa", true, rows);
                RunSource(root, extension, optional, "pandoc", pandoc, rows);
            }
            RunNegatives(root, pandoc, negatives);
            var jobs = Directory.EnumerateDirectories(root, ".flix-job-*", SearchOption.AllDirectories).ToArray();
            var leakedOffice = Directory.EnumerateDirectories(Path.GetTempPath(), "Flix-LO-*").Where(path => !officeBefore.Contains(path)).ToArray();
            bool clean = jobs.Length == 0 && leakedOffice.Length == 0;
            negatives.Add(new MatrixRow("temporanei", "cartelle di lavoro", clean ? "superato" : "fallito",
                clean ? "Nessuna cartella .flix-job e nessun profilo Flix-LO nuovo."
                    : "Residui: " + string.Join(", ", jobs.Concat(leakedOffice).Select(Path.GetFileName)),
                null, "negativo"));
        }
        catch (Exception ex)
        {
            rows.Add(new MatrixRow("*", "*", "fallito", ex.ToString(), null, "catalogo"));
        }
        finally { Evidence.DirectoryPath = priorLog; }

        int nativePassed = rows.Count(row => row.Group == "nativa" && row.Result == "superato");
        int nativeFailed = rows.Count(row => row.Group == "nativa" && row.Result == "fallito");
        int pandocPassed = rows.Count(row => row.Group == "pandoc" && row.Result == "superato");
        int pandocFailed = rows.Count(row => row.Group == "pandoc" && row.Result == "fallito");
        int pandocSkipped = rows.Count(row => row.Group == "pandoc" && row.Result == "non eseguibile");
        int catalogFailed = rows.Count(row => row.Group == "catalogo" && row.Result != "superato");
        int negativeFailed = negatives.Count(row => row.Result == "fallito");
        var report = new
        {
            utc = DateTimeOffset.UtcNow,
            root,
            method = "ConversionEngine.Convert diretto, senza GUI, mutex, named pipe, Registro o menu contestuale",
            pandoc = DocumentTools.Pandoc,
            office = DocumentTools.Office,
            ffmpeg = MediaTools.Ffmpeg,
            catalog = catalogNote,
            nativePassed,
            nativeFailed,
            pandocPassed,
            pandocFailed,
            pandocSkipped,
            rows,
            negatives
        };
        File.WriteAllText(Path.Combine(root, "content-matrix.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), Utf8);
        var summary = new StringBuilder();
        summary.AppendLine("Cartella: " + root);
        summary.AppendLine(catalogNote);
        summary.AppendLine();
        summary.AppendLine("SORGENTE → DESTINAZIONE → ESITO");
        foreach (var row in rows.Where(row => row.Group is "nativa" or "pandoc"))
            summary.AppendLine(row.Source + " → " + row.Destination + " → " + row.Result + (row.Result == "superato" ? "" : " — " + row.Detail));
        summary.AppendLine();
        summary.AppendLine("Totali native: superate=" + nativePassed + ", fallite=" + nativeFailed + ".");
        summary.AppendLine("Totali Pandoc: superate=" + pandocPassed + ", fallite=" + pandocFailed + ", non eseguibili=" + pandocSkipped + ".");
        summary.AppendLine();
        summary.AppendLine("CASI NEGATIVI E DI CONFINE");
        foreach (var row in negatives)
            summary.AppendLine(row.Source + " → " + row.Destination + " → " + row.Result + " — " + row.Detail);
        summary.AppendLine("Casi negativi: superati=" + negatives.Count(row => row.Result == "superato")
            + ", falliti=" + negativeFailed
            + ", non eseguibili=" + negatives.Count(row => row.Result == "non eseguibile") + ".");
        File.WriteAllText(Path.Combine(root, "summary.txt"), summary.ToString(), Utf8);
        Console.WriteLine(summary.ToString());
        return nativeFailed == 0 && pandocFailed == 0 && catalogFailed == 0 && negativeFailed == 0 ? 0 : 1;
    }

    static string CheckCatalog(List<MatrixRow> rows)
    {
        var problems = new List<string>();
        var note = new StringBuilder();
        bool pandoc = DocumentTools.HasPandoc;
        note.AppendLine("Motori rilevati: pandoc=" + pandoc + (pandoc ? " (" + DocumentTools.Pandoc + ")" : "")
            + ", office=" + DocumentTools.HasOffice + ", ffmpeg=" + MediaTools.HasFfmpeg + ".");
        int nativeCount = 0, optionalCount = 0;
        foreach (string extension in Scope)
        {
            string[] native = Targets(extension, false);
            string[] optional = Targets(extension, true).Where(format => !native.Contains(format, StringComparer.Ordinal)).ToArray();
            nativeCount += native.Length;
            optionalCount += optional.Length;
            if (!SameSet(native, ExpectedNative[extension]))
                problems.Add(extension + " native attese " + Join(ExpectedNative[extension]) + ", catalogo " + Join(native) + ".");
            string[] expectedOptional = ExpectedOptional.TryGetValue(extension, out string[]? listed) ? listed : [];
            if (!SameSet(optional, expectedOptional))
                problems.Add(extension + " opzionali attese " + Join(expectedOptional) + ", catalogo " + Join(optional) + ".");
            foreach (var engines in ContextMenuCatalog.EngineSnapshot.AllCombinations())
            {
                string[] actual = ContextMenuCatalog.ForExtension(extension, engines).Select(entry => entry.TargetFormat).ToArray();
                string[] expected = engines.Pandoc ? native.Concat(optional).ToArray() : native;
                if (!SameSet(actual, expected))
                    problems.Add(extension + " con pandoc=" + engines.Pandoc + ", office=" + engines.Office + ", ffmpeg=" + engines.Ffmpeg
                        + " non coincide con il catalogo atteso: " + Join(actual) + ".");
            }
            note.AppendLine(extension + " native → " + Join(native));
            note.AppendLine(extension + " opzionali → " + (optional.Length == 0 ? "(nessuna)" : Join(optional)));
        }
        if (nativeCount != 43 || optionalCount != 16)
            problems.Add("Conteggi catalogo native=" + nativeCount + ", opzionali=" + optionalCount + ".");
        string detail = problems.Count == 0
            ? "43 conversioni native e 16 opzionali Pandoc coincidono con Policy e il catalogo, per ogni combinazione di motori."
            : string.Join(" ", problems);
        rows.Add(new MatrixRow("catalogo", "PDF TXT dati", problems.Count == 0 ? "superato" : "fallito", detail, null, "catalogo"));
        note.AppendLine(detail);
        if (!pandoc) note.AppendLine("Pandoc assente nel percorso del motore: le 16 celle opzionali restano non eseguibili, senza installazione.");
        return note.ToString().TrimEnd();
    }

    static void RunSource(string root, string extension, string[] formats, string group, bool execute, List<MatrixRow> rows)
    {
        if (formats.Length == 0) return;
        string folder = Path.Combine(root, "src-" + extension.TrimStart('.'));
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione" + extension);
        if (!execute)
        {
            foreach (string format in formats)
                rows.Add(new MatrixRow(extension, format, "non eseguibile", MissingDependency, null, group));
            return;
        }
        if ((!File.Exists(source) || new FileInfo(source).Length == 0) && !TryCreateSample(source, extension, out string error))
        {
            foreach (string format in formats)
                rows.Add(new MatrixRow(extension, format, "non eseguibile", error, null, group));
            return;
        }
        foreach (string format in formats)
            rows.Add(ConvertOne(source, extension, format, group, ExpectedName(source, format)));
    }

    static MatrixRow ConvertOne(string source, string extension, string format, string group, string? expectedName)
    {
        string folder = Path.GetDirectoryName(source)!;
        string beforeHash = Hash(source);
        var before = Snapshot(folder);
        ConversionResult result;
        try
        {
            var results = ConversionEngine.Convert([source], [format], CancellationToken.None).GetAwaiter().GetResult();
            if (results.Count != 1) return new MatrixRow(extension, format, "fallito", "Risultati attesi 1, ottenuti " + results.Count + ".", null, group);
            result = results[0];
        }
        catch (Exception ex) { return new MatrixRow(extension, format, "fallito", "Eccezione: " + ex.Message, null, group); }

        var problems = new List<string>();
        if (!File.Exists(source) || !string.Equals(Hash(source), beforeHash, StringComparison.Ordinal)) problems.Add("SHA-256 del sorgente cambiato.");
        string? overwrite = OverwriteProblem(before, folder, result.Outputs);
        if (overwrite != null) problems.Add(overwrite);
        if (result.Error != null) problems.Add("Motore: " + result.Error);
        if (result.Outputs.Length != 1) problems.Add("Output attesi 1, ottenuti " + result.Outputs.Length + ".");
        string? output = result.Outputs.Length == 1 ? result.Outputs[0] : null;
        if (output != null)
        {
            if (string.Equals(Path.GetFullPath(output), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                problems.Add("L'output coincide con il sorgente.");
            if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(output)), Path.GetFullPath(folder), StringComparison.OrdinalIgnoreCase))
                problems.Add("Output fuori dalla cartella del sorgente.");
            string expectedExtension = "." + ConversionEngine.Extension(format);
            if (!Path.GetExtension(output).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
                problems.Add("Estensione " + Path.GetExtension(output) + " invece di " + expectedExtension + ".");
            if (expectedName != null && !string.Equals(Path.GetFileName(output), expectedName, StringComparison.OrdinalIgnoreCase))
                problems.Add("Nome " + Path.GetFileName(output) + " invece di " + expectedName + ".");
            if (!File.Exists(output) || new FileInfo(output).Length == 0) problems.Add("Output assente o vuoto.");
            else
            {
                try { InspectOutput(output, format, extension, problems); }
                catch (Exception ex) { problems.Add("Verifica contenuto: " + ex.Message); }
            }
        }
        string detail = problems.Count == 0
            ? "SHA-256 sorgente invariato, output " + Path.GetFileName(output) + ", formato e contenuto verificati."
            : string.Join(" ", problems);
        return new MatrixRow(extension, format, problems.Count == 0 ? "superato" : "fallito", detail, output == null ? null : Path.GetFileName(output), group);
    }

    static void InspectOutput(string output, string format, string sourceExtension, List<string> problems)
    {
        string[] tokens = RequiredTokens(sourceExtension, format);
        switch (format)
        {
            case "PDF": InspectPdf(output, tokens, problems); break;
            case "PNG":
            case "JPG":
            case "WEBP": InspectImage(output, format, problems); break;
            case "JSON": InspectJson(ReadUtf8(output), sourceExtension, problems); break;
            case "XML": InspectXml(output, sourceExtension, tokens, problems); break;
            case "CSV": InspectCsv(ReadUtf8(output), sourceExtension, tokens, problems); break;
            case "YAML": InspectYaml(ReadUtf8(output), sourceExtension, tokens, problems); break;
            case "TXT": InspectTxt(ReadUtf8(output), sourceExtension, tokens, problems); break;
            case "HTML": InspectHtml(ReadUtf8(output), sourceExtension, tokens, problems); break;
            case "MD": InspectMd(ReadUtf8(output), sourceExtension, tokens, problems); break;
            case "DOCX": InspectDocx(output, tokens, problems); break;
            case "EPUB": InspectEpub(output, tokens, problems); break;
            case "ODT": InspectOdt(output, tokens, problems); break;
            case "RTF": InspectRtf(output, tokens, problems); break;
            case "FB2": InspectFb2(output, tokens, problems); break;
            case "RST": InspectPlain(ReadUtf8(output), tokens, problems); break;
            default: problems.Add("Formato di verifica non previsto: " + format + "."); break;
        }
    }

    static void InspectPdf(string output, string[] tokens, List<string> problems)
    {
        byte[] bytes = File.ReadAllBytes(output);
        if (DetectMagic(bytes) != "PDF") problems.Add("Firma PDF assente.");
        using var document = UglyToad.PdfPig.PdfDocument.Open(output);
        if (document.NumberOfPages != 1) problems.Add("PDF con " + document.NumberOfPages + " pagine.");
        var page = document.NumberOfPages > 0 ? document.GetPage(1) : null;
        if (page != null && (Math.Abs(page.Width - 595) > 2 || Math.Abs(page.Height - 842) > 2))
            problems.Add("Pagina PDF " + page.Width.ToString("0.##") + "x" + page.Height.ToString("0.##") + " invece di 595x842.");
        int renderedPages = PDFtoImage.Conversion.GetPageCount(bytes);
        if (renderedPages != document.NumberOfPages) problems.Add("PDFtoImage legge " + renderedPages + " pagine, PdfPig " + document.NumberOfPages + ".");
        var text = new StringBuilder();
        foreach (var item in document.GetPages())
            text.AppendLine(UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(item));
        RequireTokens(text.ToString(), tokens, problems);
    }

    static void InspectImage(string output, string format, List<string> problems)
    {
        byte[] bytes = File.ReadAllBytes(output);
        string? magic = DetectMagic(bytes);
        string expected = format == "JPG" ? "JPG" : format;
        if (magic != expected) problems.Add("Firma " + (magic ?? "sconosciuta") + " invece di " + expected + ".");
        using var bitmap = ConversionEngine.ReadImage(output);
        if (bitmap.Width < 200 || bitmap.Height < 200)
            problems.Add("Immagine " + bitmap.Width + "x" + bitmap.Height + " troppo piccola per una pagina.");
        double aspect = bitmap.Width == 0 ? 0 : (double)bitmap.Height / bitmap.Width;
        if (aspect < 1.2 || aspect > 1.7)
            problems.Add("Proporzioni " + bitmap.Width + "x" + bitmap.Height + " non coerenti con una pagina A4.");
        int ink = 0;
        foreach (SKColor pixel in bitmap.Pixels)
            if (pixel.Red < 230 || pixel.Green < 230 || pixel.Blue < 230) ink++;
        if (ink < 150) problems.Add("Pagina raster senza inchiostro riconoscibile (" + ink + " pixel).");
    }

    static void InspectJson(string text, string sourceExtension, List<string> problems)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(text); }
        catch (JsonException ex) { problems.Add("JSON non leggibile: " + ex.Message); return; }
        using (document)
        {
            var root = document.RootElement;
            if (sourceExtension is ".yaml" or ".yml" or ".xml")
            {
                if (root.ValueKind != JsonValueKind.Object) { problems.Add("JSON atteso come oggetto."); return; }
                RequireJsonString(root, "nome", "Marco", problems);
                RequireJsonString(root, "citta", "Milano", problems);
                RequireJsonString(root, "nota", "FLIXCONTENT", problems);
                if (!root.TryGetProperty("attivo", out var active) || active.ValueKind != JsonValueKind.True) problems.Add("JSON attivo non coerente.");
                if (!root.TryGetProperty("anni", out var years) || years.ValueKind != JsonValueKind.Number || years.GetInt64() != 34) problems.Add("JSON anni non coerente.");
            }
            else if (sourceExtension is ".json" or ".csv")
            {
                if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 2) { problems.Add("JSON atteso come elenco di 2 schede."); return; }
                RequireRecord(root[0], "Marco", "Milano", true, 34, "FLIXCONTENT", problems);
                RequireRecord(root[1], "Giulia", "Roma", false, 29, "caffe", problems);
            }
            else problems.Add("Sorgente dati non prevista per JSON: " + sourceExtension + ".");
        }
    }

    static void RequireRecord(JsonElement item, string nome, string citta, bool active, int years, string nota, List<string> problems)
    {
        if (item.ValueKind != JsonValueKind.Object) { problems.Add("Scheda JSON non oggetto."); return; }
        RequireJsonString(item, "nome", nome, problems);
        RequireJsonString(item, "citta", citta, problems);
        RequireJsonString(item, "nota", nota, problems);
        if (!item.TryGetProperty("attivo", out var flag) || flag.ValueKind != (active ? JsonValueKind.True : JsonValueKind.False))
            problems.Add("JSON attivo di " + nome + " non coerente.");
        if (!item.TryGetProperty("anni", out var age) || age.ValueKind != JsonValueKind.Number || age.GetInt64() != years)
            problems.Add("JSON anni di " + nome + " non coerente.");
    }

    static void RequireJsonString(JsonElement item, string name, string expected, List<string> problems)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String || value.GetString() != expected)
            problems.Add("JSON " + name + " diverso da " + expected + ".");
    }

    static void InspectXml(string output, string sourceExtension, string[] tokens, List<string> problems)
    {
        XDocument document;
        try { document = XDocument.Load(output); }
        catch (Exception ex) { problems.Add("XML non leggibile: " + ex.Message); return; }
        if (document.Root == null) { problems.Add("XML senza radice."); return; }
        RequireTokens(document.ToString(), tokens, problems);
        RequireXmlScalars(document, sourceExtension, problems);
    }

    static void InspectCsv(string text, string sourceExtension, string[] tokens, List<string> problems)
    {
        var rows = ReadCsv(text, problems);
        var cells = rows.SelectMany(row => row).ToList();
        RequireTokens(string.Join("\n", cells), tokens, problems);
        if (sourceExtension is ".json" or ".csv")
        {
            if (!rows.Any(row => row.Contains("Marco") && row.Contains("Milano") && row.Contains("FLIXCONTENT")))
                problems.Add("CSV senza la riga Marco/Milano/FLIXCONTENT.");
            if (!rows.Any(row => row.Contains("Giulia") && row.Contains("Roma") && row.Contains("caffe")))
                problems.Add("CSV senza la riga Giulia/Roma/caffe.");
        }
        RequireCsvScalars(rows, sourceExtension, problems);
    }

    static void InspectYaml(string text, string sourceExtension, string[] tokens, List<string> problems)
    {
        InspectPlain(text, tokens, problems);
        if (!text.Contains(':')) problems.Add("YAML senza coppie chiave/valore.");
        RequireYamlScalars(text, sourceExtension, problems);
    }

    static void InspectTxt(string text, string sourceExtension, string[] tokens, List<string> problems)
    {
        if (Policy.Data.Contains(sourceExtension)) InspectJson(text, sourceExtension, problems);
        else
        {
            InspectPlain(text, tokens, problems);
            if (string.IsNullOrWhiteSpace(text)) problems.Add("TXT vuoto.");
        }
    }

    static void InspectHtml(string text, string sourceExtension, string[] tokens, List<string> problems)
    {
        InspectPlain(text, tokens, problems);
        if (Policy.Data.Contains(sourceExtension))
        {
            if (!text.Contains("<html", StringComparison.OrdinalIgnoreCase)) problems.Add("HTML senza struttura.");
            if (sourceExtension is ".json" or ".csv")
            {
                if (!text.Contains("<table", StringComparison.OrdinalIgnoreCase)) problems.Add("HTML senza tabella dei dati.");
            }
            else if (!text.Contains("<pre", StringComparison.OrdinalIgnoreCase)) problems.Add("HTML senza blocco leggibile.");
            RequireHtmlScalars(text, sourceExtension, problems);
        }
        else if (!text.Contains('<') || text.IndexOf("html", StringComparison.OrdinalIgnoreCase) < 0)
            problems.Add("HTML senza struttura di documento.");
    }

    static void InspectMd(string text, string sourceExtension, string[] tokens, List<string> problems)
    {
        InspectPlain(text, tokens, problems);
        if (!Policy.Data.Contains(sourceExtension)) return;
        if (!text.Contains('#')) problems.Add("Markdown senza titolo.");
        if (sourceExtension is ".json" or ".csv" && !text.Contains('|')) problems.Add("Markdown senza tabella.");
        if (sourceExtension is not (".json" or ".csv") && !text.Contains("```")) problems.Add("Markdown senza blocco leggibile.");
        RequireMdScalars(text, sourceExtension, problems);
    }

    static void InspectPlain(string text, string[] tokens, List<string> problems)
    {
        if (text.Contains('\0')) problems.Add("Testo con byte NUL.");
        if (string.IsNullOrWhiteSpace(text)) problems.Add("Contenuto vuoto.");
        RequireTokens(text, tokens, problems);
    }

    static void InspectDocx(string output, string[] tokens, List<string> problems)
    {
        using var zip = OpenZip(output, problems);
        if (zip == null) return;
        var entry = zip.GetEntry("word/document.xml");
        if (entry == null) { problems.Add("DOCX senza word/document.xml."); return; }
        var document = LoadXml(entry, problems);
        if (document == null) return;
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        RequireTokens(string.Concat(document.Descendants(word + "t").Select(node => node.Value)), tokens, problems);
    }

    static void InspectEpub(string output, string[] tokens, List<string> problems)
    {
        using var zip = OpenZip(output, problems);
        if (zip == null) return;
        var mime = zip.GetEntry("mimetype");
        if (mime == null) problems.Add("EPUB senza mimetype.");
        else
        {
            using var reader = new StreamReader(mime.Open(), Encoding.ASCII);
            if (!reader.ReadToEnd().Contains("application/epub+zip", StringComparison.Ordinal)) problems.Add("EPUB con mimetype non coerente.");
        }
        var text = new StringBuilder();
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith(".xhtml", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                || entry.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase))
                text.Append(ReadEntry(entry));
        }
        if (text.Length == 0) problems.Add("EPUB senza documenti leggibili.");
        RequireTokens(text.ToString(), tokens, problems);
    }

    static void InspectOdt(string output, string[] tokens, List<string> problems)
    {
        using var zip = OpenZip(output, problems);
        if (zip == null) return;
        var entry = zip.GetEntry("content.xml");
        if (entry == null) { problems.Add("ODT senza content.xml."); return; }
        var document = LoadXml(entry, problems);
        if (document == null) return;
        RequireTokens(string.Concat(document.Descendants().Select(node => node.Value)), tokens, problems);
    }

    static void InspectFb2(string output, string[] tokens, List<string> problems)
    {
        XDocument document;
        try { document = XDocument.Load(output); }
        catch (Exception ex) { problems.Add("FB2 non leggibile: " + ex.Message); return; }
        if (document.Root == null || !document.Root.Name.LocalName.Contains("FictionBook", StringComparison.OrdinalIgnoreCase))
            problems.Add("FB2 senza radice FictionBook.");
        RequireTokens(document.ToString(), tokens, problems);
    }

    static void InspectRtf(string output, string[] tokens, List<string> problems)
    {
        byte[] bytes = File.ReadAllBytes(output);
        string text = Encoding.ASCII.GetString(bytes);
        if (!text.StartsWith(@"{\rtf", StringComparison.Ordinal)) problems.Add("RTF senza intestazione {\\rtf.");
        RequireTokens(text, tokens, problems);
    }

    static void RequireTokens(string text, string[] tokens, List<string> problems)
    {
        foreach (string token in tokens)
            if (!text.Contains(token, StringComparison.Ordinal))
                problems.Add("Manca il contenuto \"" + token + "\".");
    }

    static void RequireYamlScalars(string text, string sourceExtension, List<string> problems)
    {
        if (!Policy.Data.Contains(sourceExtension)) return;
        RequireLine(text, "attivo: true", problems);
        RequireLine(text, "anni: 34", problems);
        if (sourceExtension is ".json" or ".csv")
        {
            RequireLine(text, "attivo: false", problems);
            RequireLine(text, "anni: 29", problems);
        }
    }

    static void RequireXmlScalars(XDocument document, string sourceExtension, List<string> problems)
    {
        if (!Policy.Data.Contains(sourceExtension)) return;
        var active = document.Descendants().Where(element => element.Name.LocalName == "attivo" && !element.HasElements).Select(element => element.Value).ToList();
        var years = document.Descendants().Where(element => element.Name.LocalName == "anni" && !element.HasElements).Select(element => element.Value).ToList();
        string[] expectedActive = sourceExtension is ".json" or ".csv" ? ["true", "false"] : ["true"];
        string[] expectedYears = sourceExtension is ".json" or ".csv" ? ["34", "29"] : ["34"];
        if (!active.SequenceEqual(expectedActive))
            problems.Add("XML attivo: " + (active.Count == 0 ? "(assente)" : string.Join(", ", active)) + ".");
        if (!years.SequenceEqual(expectedYears))
            problems.Add("XML anni: " + (years.Count == 0 ? "(assente)" : string.Join(", ", years)) + ".");
    }

    static void RequireCsvScalars(List<List<string>> rows, string sourceExtension, List<string> problems)
    {
        if (!Policy.Data.Contains(sourceExtension)) return;
        var cells = rows.SelectMany(row => row).ToList();
        RequireCell(cells, "true", problems);
        RequireCell(cells, "34", problems);
        if (sourceExtension is ".json" or ".csv")
        {
            RequireCell(cells, "false", problems);
            RequireCell(cells, "29", problems);
        }
        if (cells.Contains("True") || cells.Contains("False"))
            problems.Add("CSV con booleani non in forma JSON.");
    }

    static void RequireHtmlScalars(string text, string sourceExtension, List<string> problems)
    {
        if (sourceExtension is ".json" or ".csv")
        {
            RequireFragment(text, "<td>true</td>", problems);
            RequireFragment(text, "<td>false</td>", problems);
            RequireFragment(text, "<td>34</td>", problems);
            RequireFragment(text, "<td>29</td>", problems);
            return;
        }
        RequireFragment(text, "&quot;attivo&quot;: true", problems);
        RequireFragment(text, "&quot;anni&quot;: 34", problems);
    }

    static void RequireMdScalars(string text, string sourceExtension, List<string> problems)
    {
        if (sourceExtension is ".json" or ".csv")
        {
            RequireFragment(text, "| true |", problems);
            RequireFragment(text, "| false |", problems);
            RequireFragment(text, "| 34 |", problems);
            RequireFragment(text, "| 29 |", problems);
            return;
        }
        RequireFragment(text, "\"attivo\": true", problems);
        RequireFragment(text, "\"anni\": 34", problems);
    }

    static void RequireLine(string text, string expected, List<string> problems)
    {
        if (!text.Replace("\r\n", "\n").Split('\n').Any(line => line.Trim() == expected))
            problems.Add("Manca la riga \"" + expected + "\".");
    }

    static void RequireCell(List<string> cells, string expected, List<string> problems)
    {
        if (!cells.Contains(expected)) problems.Add("CSV senza la cella \"" + expected + "\".");
    }

    static void RequireFragment(string text, string expected, List<string> problems)
    {
        if (!text.Contains(expected, StringComparison.Ordinal))
            problems.Add("Manca \"" + expected + "\".");
    }

    static void RunNegatives(string root, bool pandoc, List<MatrixRow> negatives)
    {
        negatives.Add(PdfWithoutText(Path.Combine(root, "neg-pdf-senza-testo")));
        foreach (var item in Forbidden)
            negatives.Add(ExpectRefusal(Path.Combine(root, "neg-vietata-" + item.Extension.TrimStart('.') + "-" + item.Format.ToLowerInvariant()), item.Extension, item.Format, "non disponibile"));
        foreach (var item in SameFormat)
            negatives.Add(ExpectRefusal(Path.Combine(root, "neg-stesso-" + item.Extension.TrimStart('.') + "-" + item.Format.ToLowerInvariant()), item.Extension, item.Format, "non disponibile"));
        negatives.Add(NamingCollision(Path.Combine(root, "neg-preesistente")));
        negatives.Add(NamingSequence(Path.Combine(root, "neg-sequenza")));
        negatives.Add(NamingPdfPage(Path.Combine(root, "neg-preesistente-pdf")));
        negatives.Add(SpacedName(Path.Combine(root, "neg-spazi-txt"), ".txt", "PDF"));
        negatives.Add(SpacedName(Path.Combine(root, "neg-spazi-json"), ".json", "YAML"));
        negatives.Add(ExpectInvalid(Path.Combine(root, "neg-vuoto-json"), ".json", "YAML", "", "vuoto"));
        negatives.Add(ExpectInvalid(Path.Combine(root, "neg-rotto-json"), ".json", "YAML", "{", "JSON non valido"));
        negatives.Add(ExpectInvalid(Path.Combine(root, "neg-rotto-xml"), ".xml", "JSON", "<scheda>", "XML non valido"));
        negatives.Add(ExpectInvalid(Path.Combine(root, "neg-vuoto-csv"), ".csv", "JSON", "", "vuoto"));
        negatives.Add(ExpectInvalid(Path.Combine(root, "neg-vuoto-yaml"), ".yaml", "JSON", "", "vuoto"));
        negatives.Add(ExpectInvalidBytes(Path.Combine(root, "neg-pdf-binario"), ".pdf", "TXT", Encoding.ASCII.GetBytes("non sono un pdf"), null));
        negatives.Add(ExpectInvalidBytes(Path.Combine(root, "neg-utf8-txt"), ".txt", "PDF", [0x48, 0x69, 0xFF, 0x21], null));
        negatives.Add(MissingPandoc(Path.Combine(root, "neg-pandoc"), pandoc));
    }

    static MatrixRow PdfWithoutText(string folder)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "scansione.pdf");
        try
        {
            using var image = new SKBitmap(64, 48, SKColorType.Bgra8888, SKAlphaType.Opaque);
            using (var canvas = new SKCanvas(image))
            {
                canvas.Clear(SKColors.White);
                using var paint = new SKPaint { Color = new SKColor(200, 30, 30) };
                canvas.DrawRect(8, 8, 40, 24, paint);
            }
            ConversionEngine.ImagePdf(image, source);
            using var document = UglyToad.PdfPig.PdfDocument.Open(source);
            string text = document.NumberOfPages == 0 ? "" : UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(document.GetPage(1));
            if (!string.IsNullOrWhiteSpace(text))
                return new MatrixRow(".pdf", "TXT (senza testo)", "non eseguibile", "Il campione immagine contiene testo estraibile: " + Clip(text), null, "negativo");
        }
        catch (Exception ex)
        {
            return new MatrixRow(".pdf", "TXT (senza testo)", "non eseguibile", "Campione non creato: " + ex.Message, null, "negativo");
        }
        return ExpectExistingRefusal(source, ".pdf", "TXT (senza testo)", "TXT", "estraibile");
    }

    static MatrixRow ExpectRefusal(string folder, string extension, string format, string fragment)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione" + extension);
        if (!TryCreateSample(source, extension, out string error))
            return new MatrixRow(extension, format, "non eseguibile", error, null, "negativo");
        bool allowed = ContextMenuCatalog.EngineSnapshot.AllCombinations().Any(engines =>
            ContextMenuCatalog.ForExtension(extension, engines).Any(entry => entry.TargetFormat == format));
        if (allowed)
            return new MatrixRow(extension, format, "fallito", "Il catalogo autorizza questa conversione in almeno una combinazione di motori.", null, "negativo");
        return ExpectExistingRefusal(source, extension, format, format, fragment);
    }

    static MatrixRow ExpectExistingRefusal(string source, string extension, string label, string format, string? fragment)
    {
        string folder = Path.GetDirectoryName(source)!;
        string beforeHash = Hash(source);
        var before = Snapshot(folder);
        ConversionResult result;
        try
        {
            var results = ConversionEngine.Convert([source], [format], CancellationToken.None).GetAwaiter().GetResult();
            if (results.Count != 1) return new MatrixRow(extension, label, "fallito", "Risultati attesi 1, ottenuti " + results.Count + ".", null, "negativo");
            result = results[0];
        }
        catch (Exception ex) { return new MatrixRow(extension, label, "fallito", "Eccezione: " + ex.Message, null, "negativo"); }
        var problems = new List<string>();
        if (result.Error == null) problems.Add("La conversione è stata accettata.");
        else if (fragment != null && result.Error.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0)
            problems.Add("Messaggio \"" + Clip(result.Error) + "\" senza \"" + fragment + "\".");
        if (result.Outputs.Length != 0) problems.Add("Output inattesi: " + string.Join(", ", result.Outputs.Select(Path.GetFileName)) + ".");
        if (!File.Exists(source) || Hash(source) != beforeHash) problems.Add("SHA-256 del sorgente cambiato.");
        string? overwrite = OverwriteProblem(before, folder, []);
        if (overwrite != null) problems.Add(overwrite);
        string detail = problems.Count == 0
            ? "Rifiutata (" + Clip(result.Error ?? "") + "). Nessun output, sorgente invariato, nessun temporaneo."
            : string.Join(" ", problems);
        return new MatrixRow(extension, label, problems.Count == 0 ? "superato" : "fallito", detail, null, "negativo");
    }

    static MatrixRow ExpectInvalid(string folder, string extension, string format, string content, string fragment)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione" + extension);
        File.WriteAllText(source, content, Utf8);
        return ExpectExistingRefusal(source, extension, format + " (input non valido)", format, fragment);
    }

    static MatrixRow ExpectInvalidBytes(string folder, string extension, string format, byte[] content, string? fragment)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione" + extension);
        File.WriteAllBytes(source, content);
        return ExpectExistingRefusal(source, extension, format + " (input non valido)", format, fragment);
    }

    static MatrixRow NamingCollision(string folder)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione.json");
        if (!TryCreateSample(source, ".json", out string error))
            return new MatrixRow(".json", "TXT (destinazione esistente)", "non eseguibile", error, null, "negativo");
        string sentinel = Path.Combine(folder, "campione.converted.txt");
        string unrelated = Path.Combine(folder, "nota gia presente.txt");
        File.WriteAllText(sentinel, SentinelText, Encoding.ASCII);
        File.WriteAllText(unrelated, "PREESISTENTE", Encoding.ASCII);
        string sourceHash = Hash(source), sentinelHash = Hash(sentinel), unrelatedHash = Hash(unrelated);
        var before = Snapshot(folder);
        ConversionResult result;
        try
        {
            var results = ConversionEngine.Convert([source], ["TXT"], CancellationToken.None).GetAwaiter().GetResult();
            result = results.Count == 1 ? results[0] : new ConversionResult(source, "TXT", [], "Risultati " + results.Count);
        }
        catch (Exception ex) { return new MatrixRow(".json", "TXT (destinazione esistente)", "fallito", ex.Message, null, "negativo"); }
        string expected = Path.Combine(folder, "campione.converted (1).txt");
        var problems = NamingProblems(result, expected, source, sourceHash, before, folder);
        if (!File.Exists(sentinel) || Hash(sentinel) != sentinelHash) problems.Add("Il file .converted.txt preesistente è stato modificato.");
        if (!File.Exists(unrelated) || Hash(unrelated) != unrelatedHash) problems.Add("Il file preesistente non collegato è stato modificato.");
        if (problems.Count == 0) InspectOutput(expected, "TXT", ".json", problems);
        string detail = problems.Count == 0
            ? "Regola rispettata: campione.converted.txt conservato, creato campione.converted (1).txt."
            : string.Join(" ", problems);
        return new MatrixRow(".json", "TXT (destinazione esistente)", problems.Count == 0 ? "superato" : "fallito", detail, Path.GetFileName(expected), "negativo");
    }

    static MatrixRow NamingSequence(string folder)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione.json");
        if (!TryCreateSample(source, ".json", out string error))
            return new MatrixRow(".json", "TXT (due passate)", "non eseguibile", error, null, "negativo");
        string sourceHash = Hash(source);
        ConversionResult first, second;
        try
        {
            first = ConversionEngine.Convert([source], ["TXT"], CancellationToken.None).GetAwaiter().GetResult()[0];
            second = ConversionEngine.Convert([source], ["TXT"], CancellationToken.None).GetAwaiter().GetResult()[0];
        }
        catch (Exception ex) { return new MatrixRow(".json", "TXT (due passate)", "fallito", ex.Message, null, "negativo"); }
        string primary = Path.Combine(folder, "campione.converted.txt");
        string numbered = Path.Combine(folder, "campione.converted (1).txt");
        var problems = new List<string>();
        if (first.Error != null) problems.Add("Prima passata: " + first.Error);
        if (second.Error != null) problems.Add("Seconda passata: " + second.Error);
        if (first.Outputs.Length != 1 || !string.Equals(first.Outputs[0], primary, StringComparison.OrdinalIgnoreCase))
            problems.Add("La prima passata non ha creato campione.converted.txt.");
        string primaryHash = File.Exists(primary) ? Hash(primary) : "";
        if (second.Outputs.Length != 1 || !string.Equals(second.Outputs[0], numbered, StringComparison.OrdinalIgnoreCase))
            problems.Add("La seconda passata non ha creato campione.converted (1).txt.");
        if (File.Exists(primary) && Hash(primary) != primaryHash) problems.Add("La seconda passata ha modificato il primo output.");
        if (Hash(source) != sourceHash) problems.Add("SHA-256 del sorgente cambiato.");
        if (Directory.EnumerateDirectories(folder).Any()) problems.Add("Directory temporanea .flix-job residua.");
        if (problems.Count == 0)
        {
            InspectOutput(primary, "TXT", ".json", problems);
            InspectOutput(numbered, "TXT", ".json", problems);
        }
        string detail = problems.Count == 0
            ? "Prima passata campione.converted.txt; seconda passata campione.converted (1).txt senza sovrascrivere."
            : string.Join(" ", problems);
        return new MatrixRow(".json", "TXT (due passate)", problems.Count == 0 ? "superato" : "fallito", detail, "campione.converted.txt + (1)", "negativo");
    }

    static MatrixRow NamingPdfPage(string folder)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione.pdf");
        if (!TryCreateSample(source, ".pdf", out string error))
            return new MatrixRow(".pdf", "PNG (destinazione esistente)", "non eseguibile", error, null, "negativo");
        string sentinel = Path.Combine(folder, "campione.page-001.converted.png");
        File.WriteAllBytes(sentinel, Encoding.ASCII.GetBytes(SentinelText));
        string sourceHash = Hash(source), sentinelHash = Hash(sentinel);
        var before = Snapshot(folder);
        ConversionResult result;
        try { result = ConversionEngine.Convert([source], ["PNG"], CancellationToken.None).GetAwaiter().GetResult()[0]; }
        catch (Exception ex) { return new MatrixRow(".pdf", "PNG (destinazione esistente)", "fallito", ex.Message, null, "negativo"); }
        string expected = Path.Combine(folder, "campione.page-001.converted (1).png");
        var problems = NamingProblems(result, expected, source, sourceHash, before, folder);
        if (!File.Exists(sentinel) || Hash(sentinel) != sentinelHash) problems.Add("Il PNG preesistente è stato modificato.");
        if (problems.Count == 0) InspectOutput(expected, "PNG", ".pdf", problems);
        string detail = problems.Count == 0
            ? "Regola pagine: campione.page-001.converted.png conservato, creato campione.page-001.converted (1).png."
            : string.Join(" ", problems);
        return new MatrixRow(".pdf", "PNG (destinazione esistente)", problems.Count == 0 ? "superato" : "fallito", detail, Path.GetFileName(expected), "negativo");
    }

    static List<string> NamingProblems(ConversionResult result, string expected, string source, string sourceHash, Dictionary<string, string> before, string folder)
    {
        var problems = new List<string>();
        if (result.Error != null) problems.Add(result.Error);
        if (result.Outputs.Length != 1 || !string.Equals(result.Outputs.FirstOrDefault(), expected, StringComparison.OrdinalIgnoreCase))
            problems.Add("Atteso " + Path.GetFileName(expected) + ", ottenuto " + string.Join(", ", result.Outputs.Select(Path.GetFileName)) + ".");
        if (Hash(source) != sourceHash) problems.Add("SHA-256 del sorgente cambiato.");
        string? extra = OverwriteProblem(before, folder, result.Error == null ? result.Outputs : []);
        if (extra != null) problems.Add(extra);
        return problems;
    }

    static MatrixRow SpacedName(string folder, string extension, string format)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "scheda con spazi" + extension);
        if (!TryCreateSample(source, extension, out string error))
            return new MatrixRow(extension, format + " (nome con spazi)", "non eseguibile", error, null, "negativo");
        string expected = ExpectedName(source, format);
        var row = ConvertOne(source, extension, format, "negativo", expected);
        return row with { Destination = format + " (nome con spazi)", Detail = row.Result == "superato" ? "Nome con spazi conservato: " + expected + "." : row.Detail };
    }

    static MatrixRow MissingPandoc(string folder, bool pandoc)
    {
        string[] txtNative = Targets(".txt", false);
        string[] pdfNative = Targets(".pdf", false);
        string[] txtOptional = ExpectedOptional[".txt"];
        string[] pdfOptional = ExpectedOptional[".pdf"];
        bool hidden = txtOptional.All(format => !txtNative.Contains(format, StringComparer.Ordinal))
            && pdfOptional.All(format => !pdfNative.Contains(format, StringComparer.Ordinal))
            && ContextMenuCatalog.ForExtension(".txt", new ContextMenuCatalog.EngineSnapshot(false, true, true)).All(entry => !txtOptional.Contains(entry.TargetFormat, StringComparer.Ordinal))
            && ContextMenuCatalog.ForExtension(".pdf", new ContextMenuCatalog.EngineSnapshot(false, true, true)).All(entry => !pdfOptional.Contains(entry.TargetFormat, StringComparer.Ordinal));
        if (!hidden)
            return new MatrixRow(".txt", "DOCX (assenza Pandoc)", "fallito", "Il catalogo con pandoc=false espone ancora destinazioni opzionali.", null, "negativo");
        if (pandoc)
            return new MatrixRow(".txt", "DOCX (assenza Pandoc)", "non eseguibile",
                "Pandoc è presente: il rifiuto runtime non è stato provocato e nessun programma è stato rimosso. Il catalogo con pandoc=false esclude le 16 destinazioni opzionali.",
                null, "negativo");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione.txt");
        if (!TryCreateSample(source, ".txt", out string error))
            return new MatrixRow(".txt", "DOCX (assenza Pandoc)", "non eseguibile", error, null, "negativo");
        return ExpectExistingRefusal(source, ".txt", "DOCX (assenza Pandoc)", "DOCX", "non disponibile");
    }

    static bool TryCreateSample(string path, string extension, out string error)
    {
        error = "";
        try
        {
            switch (extension)
            {
                case ".txt":
                    File.WriteAllText(path, TextSample, Utf8);
                    if (ReadUtf8(path) != TextSample) { error = "Il TXT riletto non coincide con il campione."; return false; }
                    break;
                case ".json":
                    File.WriteAllText(path, JsonSample, Utf8);
                    using (JsonDocument.Parse(ReadUtf8(path))) { }
                    break;
                case ".yaml":
                case ".yml":
                    File.WriteAllText(path, YamlSample, Utf8);
                    if (!ReadUtf8(path).Contains("FLIXCONTENT", StringComparison.Ordinal)) { error = "Campione YAML incompleto."; return false; }
                    break;
                case ".xml":
                    File.WriteAllText(path, XmlSample, Utf8);
                    if (XDocument.Load(path).Root?.Element("nota")?.Value != "FLIXCONTENT") { error = "Campione XML non leggibile."; return false; }
                    break;
                case ".csv":
                    File.WriteAllText(path, CsvSample, Utf8);
                    var rows = ReadCsv(ReadUtf8(path), []);
                    if (rows.Count != 3 || !rows[1].Contains("FLIXCONTENT")) { error = "Campione CSV non leggibile."; return false; }
                    break;
                case ".pdf":
                    ConversionEngine.TextPdf(TextSample, path);
                    using (var document = UglyToad.PdfPig.PdfDocument.Open(path))
                    {
                        if (document.NumberOfPages != 1) { error = "Il PDF campione non ha una pagina."; return false; }
                        string text = UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(document.GetPage(1));
                        if (!text.Contains("FLIXCONTENT", StringComparison.Ordinal) || !text.Contains("caffè", StringComparison.Ordinal))
                        {
                            error = "Il PDF campione non contiene il testo atteso: " + Clip(text);
                            return false;
                        }
                    }
                    break;
                default:
                    error = "Estensione campione non prevista: " + extension;
                    return false;
            }
            return new FileInfo(path).Length > 0;
        }
        catch (Exception ex)
        {
            error = "Campione " + extension + " non creato: " + ex.Message;
            return false;
        }
    }

    static string[] RequiredTokens(string sourceExtension, string format)
    {
        if (sourceExtension is ".txt" or ".pdf")
            return format is "PDF" or "TXT"
                ? ["FLIXCONTENT", "Marco", "Milano", "caffè", "città"]
                : ["FLIXCONTENT", "Marco", "Milano"];
        if (sourceExtension is ".json" or ".csv") return ["Marco", "Milano", "Giulia", "Roma", "FLIXCONTENT"];
        return ["Marco", "Milano", "FLIXCONTENT"];
    }

    static string ExpectedName(string source, string format)
    {
        string extension = Path.GetExtension(source).ToLowerInvariant();
        string suffix = extension == ".pdf" && format is "PNG" or "JPG" or "WEBP" ? ".page-001" : "";
        return Path.GetFileNameWithoutExtension(source) + suffix + ".converted." + ConversionEngine.Extension(format);
    }

    static string[] Targets(string extension, bool pandoc) =>
        ContextMenuCatalog.ForExtension(extension, new ContextMenuCatalog.EngineSnapshot(pandoc, true, true)).Select(entry => entry.TargetFormat).ToArray();

    static bool SameSet(IEnumerable<string> actual, IEnumerable<string> expected) =>
        actual.ToHashSet(StringComparer.Ordinal).SetEquals(expected);

    static string Join(IEnumerable<string> items) => items.Any() ? string.Join(", ", items) : "(nessuna)";

    static List<List<string>> ReadCsv(string text, List<string> problems)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool quotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else quotes = false;
                }
                else cell.Append(ch);
            }
            else if (ch == '"') quotes = true;
            else if (ch == ',') { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = [];
            }
            else cell.Append(ch);
        }
        if (quotes) problems.Add("CSV con virgolette non chiuse.");
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); rows.Add(row); }
        if (rows.Count == 0) problems.Add("CSV senza righe.");
        return rows;
    }

    static ZipArchive? OpenZip(string output, List<string> problems)
    {
        byte[] bytes = File.ReadAllBytes(output);
        if (bytes.Length < 4 || bytes[0] != (byte)'P' || bytes[1] != (byte)'K') { problems.Add("Documento senza firma ZIP."); return null; }
        try { return ZipFile.OpenRead(output); }
        catch (Exception ex) { problems.Add("Archivio non apribile: " + ex.Message); return null; }
    }

    static XDocument? LoadXml(ZipArchiveEntry entry, List<string> problems)
    {
        try
        {
            using var stream = entry.Open();
            return XDocument.Load(stream);
        }
        catch (Exception ex) { problems.Add(entry.FullName + " non leggibile: " + ex.Message); return null; }
    }

    static string ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Utf8, true);
        return reader.ReadToEnd();
    }

    static string ReadUtf8(string path)
    {
        using var reader = new StreamReader(path, new UTF8Encoding(false, true), true);
        return reader.ReadToEnd();
    }

    static string? DetectMagic(byte[] data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A) return "PNG";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "JPG";
        if (data.Length >= 12 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F' && data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P') return "WEBP";
        if (data.Length >= 5 && data[0] == (byte)'%' && data[1] == (byte)'P' && data[2] == (byte)'D' && data[3] == (byte)'F' && data[4] == (byte)'-') return "PDF";
        return null;
    }

    static Dictionary<string, string> Snapshot(string directory)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.EnumerateFiles(directory)) map[Path.GetFullPath(file)] = Hash(file);
        return map;
    }

    static string? OverwriteProblem(Dictionary<string, string> before, string directory, string[] outputs)
    {
        foreach (var pair in before)
        {
            if (!File.Exists(pair.Key)) return "File preesistente rimosso: " + Path.GetFileName(pair.Key) + ".";
            if (!string.Equals(Hash(pair.Key), pair.Value, StringComparison.Ordinal)) return "File preesistente modificato: " + Path.GetFileName(pair.Key) + ".";
        }
        var created = Directory.EnumerateFiles(directory).Select(Path.GetFullPath).Except(before.Keys, StringComparer.OrdinalIgnoreCase).ToArray();
        var expected = outputs.Select(Path.GetFullPath).ToArray();
        if (created.Length != expected.Length || created.Except(expected, StringComparer.OrdinalIgnoreCase).Any() || expected.Except(created, StringComparer.OrdinalIgnoreCase).Any())
            return "File creati non coincidenti con l'output dichiarato.";
        if (Directory.EnumerateDirectories(directory).Any()) return "Directory temporanea .flix-job residua.";
        return null;
    }

    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static string Clip(string text)
    {
        string flat = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return flat.Length <= 180 ? flat : flat[..180];
    }

    internal sealed record MatrixRow(string Source, string Destination, string Result, string Detail, string? Output, string Group);
}
