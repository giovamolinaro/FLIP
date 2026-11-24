using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;
namespace Flix;

// Collaudo isolato della matrice immagine M5.2. Chiama solo ConversionEngine:
// niente launcher, mutex di sessione, named pipe o Registro.
internal static class ImageMatrixTests
{
    const int Width = 128;
    const int Height = 96;
    const string SentinelText = "NON-SOVRASCRIVERE-M52E4";

    static readonly (int X, int Y, string Name)[] Probes = [(36, 27, "rosso"), (92, 27, "verde"), (36, 69, "blu"), (92, 69, "giallo")];
    static readonly SKColor[] Ideal = [new(220, 32, 32), new(32, 170, 48), new(32, 48, 210), new(230, 200, 32)];

    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "Flix-M52E4-matrix-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
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
        var naming = new List<MatrixRow>();
        string catalogNote = "";
        try
        {
            var detected = ContextMenuCatalog.EngineSnapshot.Detected;
            var native = new ContextMenuCatalog.EngineSnapshot(false, false, false);
            var catalog = new StringBuilder();
            catalog.AppendLine("Motori rilevati: pandoc=" + detected.Pandoc + ", office=" + detected.Office + ", ffmpeg=" + detected.Ffmpeg + ".");
            bool enginesAgree = true;
            foreach (string extension in Policy.Images)
            {
                string[] actual = Formats(extension, detected);
                string[] plain = Formats(extension, native);
                if (!actual.SequenceEqual(plain)) enginesAgree = false;
                catalog.AppendLine(extension + " → " + (actual.Length == 0 ? "(nessuna destinazione)" : string.Join(", ", actual)));
                RunSource(root, extension, actual, rows);
            }
            catalog.AppendLine(enginesAgree
                ? "Per le immagini il catalogo non dipende da Pandoc, Office o FFmpeg."
                : "ATTENZIONE: il catalogo immagine cambia con i motori opzionali.");
            catalogNote = catalog.ToString().TrimEnd();
            RunNaming(root, naming);
        }
        catch (Exception ex)
        {
            rows.Add(new MatrixRow("*", "*", "non eseguibile", ex.ToString(), null));
        }
        finally { Evidence.DirectoryPath = priorLog; }

        int passed = rows.Count(row => row.Result == "superato");
        int failed = rows.Count(row => row.Result == "fallito");
        int skipped = rows.Count(row => row.Result == "non eseguibile");
        int namingFailed = naming.Count(row => row.Result != "superato");
        var report = new
        {
            utc = DateTimeOffset.UtcNow,
            root,
            method = "ConversionEngine.Convert diretto, senza GUI, senza mutex di sessione, senza named pipe e senza menu contestuale",
            catalog = catalogNote,
            passed,
            failed,
            skipped,
            naming,
            rows
        };
        string json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(root, "image-matrix.json"), json, new UTF8Encoding(false));
        var summary = new StringBuilder();
        summary.AppendLine("Cartella: " + root);
        summary.AppendLine(catalogNote);
        summary.AppendLine();
        summary.AppendLine("SORGENTE → DESTINAZIONE → ESITO");
        foreach (var row in rows)
            summary.AppendLine(row.Source + " → " + row.Destination + " → " + row.Result + (row.Result == "superato" ? "" : " — " + row.Detail));
        summary.AppendLine();
        summary.AppendLine("Totali matrice: superati=" + passed + ", falliti=" + failed + ", non eseguibili=" + skipped + ".");
        summary.AppendLine();
        summary.AppendLine("Naming con destinazione già presente (specifica: suffisso .converted e progressivo, mai sovrascrittura):");
        foreach (var row in naming)
            summary.AppendLine(row.Source + " → " + row.Destination + " → " + row.Result + " — " + row.Detail);
        File.WriteAllText(Path.Combine(root, "summary.txt"), summary.ToString(), new UTF8Encoding(false));
        Console.WriteLine(summary.ToString());
        return failed == 0 && skipped == 0 && namingFailed == 0 ? 0 : 1;
    }

    static void RunSource(string root, string extension, string[] formats, List<MatrixRow> rows)
    {
        string folder = Path.Combine(root, "src-" + extension.TrimStart('.'));
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione" + extension);
        if (!TryCreateSample(source, extension, out string fixtureError, out SKColor[] sourceProbes))
        {
            if (formats.Length == 0) rows.Add(new MatrixRow(extension, "(nessuna)", "non eseguibile", fixtureError, null));
            foreach (string format in formats)
                rows.Add(new MatrixRow(extension, format, "non eseguibile", fixtureError, null));
            return;
        }
        if (formats.Length == 0)
        {
            rows.Add(new MatrixRow(extension, "(nessuna)", "non eseguibile", "Il catalogo non offre destinazioni.", null));
            return;
        }
        foreach (string format in formats)
            rows.Add(ConvertOne(source, extension, format, sourceProbes));
    }

    static MatrixRow ConvertOne(string source, string extension, string format, SKColor[] sourceProbes)
    {
        string folder = Path.GetDirectoryName(source)!;
        string beforeHash = Hash(source);
        var before = Snapshot(folder);
        ConversionResult result;
        try { result = ConversionEngine.Convert([source], [format], CancellationToken.None).GetAwaiter().GetResult()[0]; }
        catch (Exception ex) { return new MatrixRow(extension, format, "fallito", "Eccezione: " + ex.Message, null); }
        var problems = new List<string>();
        string afterHash = File.Exists(source) ? Hash(source) : "";
        if (!string.Equals(afterHash, beforeHash, StringComparison.Ordinal)) problems.Add("SHA-256 del sorgente cambiato.");
        string? overwrite = OverwriteProblem(before, folder, result.Outputs);
        if (overwrite != null) problems.Add(overwrite);
        if (result.Error != null) problems.Add("Motore: " + result.Error);
        if (result.Outputs.Length != 1) problems.Add("Output attesi 1, ottenuti " + result.Outputs.Length + ".");
        string? output = result.Outputs.Length == 1 ? result.Outputs[0] : null;
        if (output != null)
        {
            if (string.Equals(Path.GetFullPath(output), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                problems.Add("L'output coincide con il sorgente.");
            string expectedExtension = "." + ConversionEngine.Extension(format);
            if (!Path.GetExtension(output).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
                problems.Add("Estensione " + Path.GetExtension(output) + " invece di " + expectedExtension + ".");
            if (!File.Exists(output) || new FileInfo(output).Length == 0) problems.Add("Output assente o vuoto.");
            else
            {
                byte[] bytes = File.ReadAllBytes(output);
                string? magic = DetectMagic(bytes);
                string expectedMagic = format == "JPG" ? "JPG" : format;
                if (magic != expectedMagic) problems.Add("Firma " + (magic ?? "sconosciuta") + " invece di " + expectedMagic + ".");
                try { InspectOutput(output, format, bytes, sourceProbes, problems); }
                catch (Exception ex) { problems.Add("Decodifica: " + ex.Message); }
            }
        }
        string detail = problems.Count == 0
            ? "SHA-256 sorgente invariato, output " + Path.GetFileName(output)
            : string.Join(" ", problems);
        return new MatrixRow(extension, format, problems.Count == 0 ? "superato" : "fallito", detail, output == null ? null : Path.GetFileName(output));
    }

    static void InspectOutput(string output, string format, byte[] bytes, SKColor[] sourceProbes, List<string> problems)
    {
        if (format == "PDF")
        {
            using var document = UglyToad.PdfPig.PdfDocument.Open(output);
            if (document.NumberOfPages != 1) problems.Add("PDF con " + document.NumberOfPages + " pagine.");
            else
            {
                var page = document.GetPage(1);
                if (Math.Abs(page.Width - 595) > 2 || Math.Abs(page.Height - 842) > 2)
                    problems.Add("Pagina PDF " + page.Width.ToString("0.##") + "x" + page.Height.ToString("0.##") + " invece di 595x842.");
            }
            int pages = PDFtoImage.Conversion.GetPageCount(bytes);
            if (pages != 1) problems.Add("PDFtoImage legge " + pages + " pagine.");
            using var rendered = PDFtoImage.Conversion.ToImage(bytes, 0, null, new PDFtoImage.RenderOptions(Dpi: 72));
            int[] hits = CountColorHits(rendered, sourceProbes);
            for (int i = 0; i < Probes.Length; i++)
                if (hits[i] < 25) problems.Add("PDF senza area " + Probes[i].Name + " riconoscibile (" + hits[i] + " pixel).");
            return;
        }
        using var bitmap = ConversionEngine.ReadImage(output);
        if (bitmap.Width != Width || bitmap.Height != Height)
            problems.Add("Dimensioni " + bitmap.Width + "x" + bitmap.Height + " invece di " + Width + "x" + Height + ".");
        int tolerance = format is "JPG" or "WEBP" ? 42 : format == "GIF" ? 48 : 8;
        for (int i = 0; i < Probes.Length; i++)
        {
            var pixel = bitmap.GetPixel(Probes[i].X, Probes[i].Y);
            if (!Recognizable(Probes[i].Name, pixel) || !Near(pixel, sourceProbes[i], tolerance))
                problems.Add("Contenuto " + Probes[i].Name + " non coerente (" + pixel.Red + "," + pixel.Green + "," + pixel.Blue + ").");
        }
    }

    static void RunNaming(string root, List<MatrixRow> naming)
    {
        naming.Add(NamingCollision(Path.Combine(root, "naming-sentinel")));
        naming.Add(NamingSequence(Path.Combine(root, "naming-sequence")));
    }

    static MatrixRow NamingCollision(string folder)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione.png");
        if (!TryCreateSample(source, ".png", out string error, out _))
            return new MatrixRow(".png", "JPG", "non eseguibile", "Campione naming: " + error, null);
        string sentinel = Path.Combine(folder, "campione.converted.jpg");
        byte[] marker = Encoding.ASCII.GetBytes(SentinelText);
        File.WriteAllBytes(sentinel, marker);
        string sourceHash = Hash(source);
        string sentinelHash = Hash(sentinel);
        var before = Snapshot(folder);
        ConversionResult result;
        try { result = ConversionEngine.Convert([source], ["JPG"], CancellationToken.None).GetAwaiter().GetResult()[0]; }
        catch (Exception ex) { return new MatrixRow(".png", "JPG (destinazione esistente)", "fallito", ex.Message, null); }
        string expected = Path.Combine(folder, "campione.converted (1).jpg");
        var problems = new List<string>();
        if (result.Error != null) problems.Add(result.Error);
        if (result.Outputs.Length != 1 || !string.Equals(result.Outputs.FirstOrDefault(), expected, StringComparison.OrdinalIgnoreCase))
            problems.Add("Atteso " + Path.GetFileName(expected) + ", ottenuto " + string.Join(", ", result.Outputs.Select(Path.GetFileName)) + ".");
        if (!File.Exists(sentinel) || Hash(sentinel) != sentinelHash) problems.Add("Il file .converted.jpg preesistente è stato modificato.");
        if (Hash(source) != sourceHash) problems.Add("SHA-256 del sorgente cambiato.");
        string? extra = OverwriteProblem(before, folder, result.Outputs);
        if (extra != null) problems.Add(extra);
        if (problems.Count == 0 && !ValidJpeg(expected)) problems.Add("Il progressivo non è un JPEG decodificabile.");
        string detail = problems.Count == 0
            ? "Specifica rispettata: campione.converted.jpg conservato, creato campione.converted (1).jpg."
            : string.Join(" ", problems);
        return new MatrixRow(".png", "JPG (destinazione esistente)", problems.Count == 0 ? "superato" : "fallito", detail, Path.GetFileName(expected));
    }

    static MatrixRow NamingSequence(string folder)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione.png");
        if (!TryCreateSample(source, ".png", out string error, out _))
            return new MatrixRow(".png", "JPG (due passate)", "non eseguibile", "Campione naming: " + error, null);
        string sourceHash = Hash(source);
        ConversionResult first;
        ConversionResult second;
        try
        {
            first = ConversionEngine.Convert([source], ["JPG"], CancellationToken.None).GetAwaiter().GetResult()[0];
            second = ConversionEngine.Convert([source], ["JPG"], CancellationToken.None).GetAwaiter().GetResult()[0];
        }
        catch (Exception ex) { return new MatrixRow(".png", "JPG (due passate)", "fallito", ex.Message, null); }
        string primary = Path.Combine(folder, "campione.converted.jpg");
        string numbered = Path.Combine(folder, "campione.converted (1).jpg");
        var problems = new List<string>();
        if (first.Error != null) problems.Add("Prima passata: " + first.Error);
        if (second.Error != null) problems.Add("Seconda passata: " + second.Error);
        if (first.Outputs.Length != 1 || !string.Equals(first.Outputs[0], primary, StringComparison.OrdinalIgnoreCase))
            problems.Add("La prima passata non ha creato campione.converted.jpg.");
        string primaryHash = File.Exists(primary) ? Hash(primary) : "";
        if (second.Outputs.Length != 1 || !string.Equals(second.Outputs[0], numbered, StringComparison.OrdinalIgnoreCase))
            problems.Add("La seconda passata non ha creato campione.converted (1).jpg.");
        if (File.Exists(primary) && Hash(primary) != primaryHash) problems.Add("La seconda passata ha modificato il primo output.");
        if (Hash(source) != sourceHash) problems.Add("SHA-256 del sorgente cambiato.");
        if (problems.Count == 0 && (!ValidJpeg(primary) || !ValidJpeg(numbered))) problems.Add("Uno dei JPEG non è decodificabile.");
        string detail = problems.Count == 0
            ? "Prima passata campione.converted.jpg; seconda passata campione.converted (1).jpg senza sovrascrivere."
            : string.Join(" ", problems);
        return new MatrixRow(".png", "JPG (due passate)", problems.Count == 0 ? "superato" : "fallito", detail, "campione.converted.jpg + (1)");
    }

    static bool TryCreateSample(string path, string extension, out string error, out SKColor[] probes)
    {
        probes = new SKColor[Probes.Length];
        error = "";
        try
        {
            using (var image = DrawSample())
                ConversionEngine.WriteImage(image, path, WriteFormat(extension));
            string? magic = DetectMagic(File.ReadAllBytes(path));
            string expectedMagic = extension is ".jpg" or ".jpeg" ? "JPG" : extension is ".tif" or ".tiff" ? "TIFF" : extension.TrimStart('.').ToUpperInvariant();
            if (magic != expectedMagic) { error = "Campione " + extension + " con firma " + (magic ?? "sconosciuta") + "."; return false; }
            using var decoded = ConversionEngine.ReadImage(path);
            if (decoded.Width != Width || decoded.Height != Height)
            {
                error = "Campione " + extension + " decodificato a " + decoded.Width + "x" + decoded.Height + ".";
                return false;
            }
            int tolerance = extension is ".jpg" or ".jpeg" or ".webp" ? 42 : extension == ".gif" ? 48 : 8;
            for (int i = 0; i < Probes.Length; i++)
            {
                probes[i] = decoded.GetPixel(Probes[i].X, Probes[i].Y);
                if (!Recognizable(Probes[i].Name, probes[i]) || !Near(probes[i], Ideal[i], tolerance))
                {
                    error = "Campione " + extension + " non riconoscibile nel riquadro " + Probes[i].Name + ".";
                    return false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            error = "Campione " + extension + " non creato: " + ex.Message;
            return false;
        }
    }

    static SKBitmap DrawSample()
    {
        var image = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using var canvas = new SKCanvas(image);
        canvas.Clear(SKColors.White);
        using var paint = new SKPaint { IsAntialias = false };
        paint.Color = Ideal[0]; canvas.DrawRect(16, 12, 40, 30, paint);
        paint.Color = Ideal[1]; canvas.DrawRect(72, 12, 40, 30, paint);
        paint.Color = Ideal[2]; canvas.DrawRect(16, 54, 40, 30, paint);
        paint.Color = Ideal[3]; canvas.DrawRect(72, 54, 40, 30, paint);
        return image;
    }

    static string WriteFormat(string extension) => extension switch
    {
        ".jpg" or ".jpeg" => "JPG",
        ".tif" or ".tiff" => "TIFF",
        ".png" => "PNG",
        ".webp" => "WEBP",
        ".bmp" => "BMP",
        ".gif" => "GIF",
        _ => throw new InvalidOperationException("Estensione campione non prevista: " + extension)
    };

    static string[] Formats(string extension, ContextMenuCatalog.EngineSnapshot engines) =>
        ContextMenuCatalog.ForExtension(extension, engines).Select(entry => entry.TargetFormat).ToArray();

    static bool ValidJpeg(string path)
    {
        byte[] bytes = File.ReadAllBytes(path);
        if (DetectMagic(bytes) != "JPG") return false;
        using var bitmap = ConversionEngine.ReadImage(path);
        return bitmap.Width == Width && bitmap.Height == Height;
    }

    static int[] CountColorHits(SKBitmap bitmap, SKColor[] expected)
    {
        int[] hits = new int[expected.Length];
        foreach (var pixel in bitmap.Pixels)
        {
            for (int i = 0; i < expected.Length; i++)
                if (Near(pixel, expected[i], 80) && Recognizable(Probes[i].Name, pixel)) hits[i]++;
        }
        return hits;
    }

    static bool Recognizable(string name, SKColor pixel) => name switch
    {
        "rosso" => pixel.Red > pixel.Green + 50 && pixel.Red > pixel.Blue + 50,
        "verde" => pixel.Green > pixel.Red + 50 && pixel.Green > pixel.Blue + 50,
        "blu" => pixel.Blue > pixel.Red + 50 && pixel.Blue > pixel.Green + 50,
        "giallo" => pixel.Red > 170 && pixel.Green > 140 && pixel.Blue < 110 && pixel.Red > pixel.Blue + 60 && pixel.Green > pixel.Blue + 60,
        _ => false
    };

    static bool Near(SKColor pixel, SKColor expected, int tolerance) =>
        Math.Abs(pixel.Red - expected.Red) <= tolerance
        && Math.Abs(pixel.Green - expected.Green) <= tolerance
        && Math.Abs(pixel.Blue - expected.Blue) <= tolerance;

    static string? DetectMagic(byte[] data)
    {
        if (data.Length >= 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47 && data[4] == 0x0D && data[5] == 0x0A && data[6] == 0x1A && data[7] == 0x0A) return "PNG";
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "JPG";
        if (data.Length >= 12 && data[0] == (byte)'R' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'F' && data[8] == (byte)'W' && data[9] == (byte)'E' && data[10] == (byte)'B' && data[11] == (byte)'P') return "WEBP";
        if (data.Length >= 2 && data[0] == (byte)'B' && data[1] == (byte)'M') return "BMP";
        if (data.Length >= 4 && ((data[0] == (byte)'I' && data[1] == (byte)'I' && data[2] == 42 && data[3] == 0) || (data[0] == (byte)'M' && data[1] == (byte)'M' && data[2] == 0 && data[3] == 42))) return "TIFF";
        if (data.Length >= 6 && data[0] == (byte)'G' && data[1] == (byte)'I' && data[2] == (byte)'F' && data[3] == (byte)'8' && (data[4] == (byte)'7' || data[4] == (byte)'9') && data[5] == (byte)'a') return "GIF";
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

    internal sealed record MatrixRow(string Source, string Destination, string Result, string Detail, string? Output);
}
