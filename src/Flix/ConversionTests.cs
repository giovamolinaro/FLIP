using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SkiaSharp;
namespace Flix;

internal static class ConversionTests
{
    internal static async Task<int> Run()
    {
        Directory.CreateDirectory(Evidence.DirectoryPath); string root = Path.Combine(Evidence.DirectoryPath, "conversion-fixtures-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(root);
        var checks = new List<object>(); var all = new List<ConversionResult>(); var hashes = new Dictionary<string, string>(); int failed = 0, skipped = 0;
        var officeProfilesBefore = Directory.EnumerateDirectories(Path.GetTempPath(), "Flix-LO-*").ToHashSet(StringComparer.OrdinalIgnoreCase);
        void Check(string name, bool? pass)
        {
            string status = pass switch { true => "pass", false => "fail", _ => "non eseguito" };
            checks.Add(new { name, status, pass });
            if (pass == false) failed++;
            else if (pass == null) skipped++;
            Evidence.Log("test", new { name, status, pass });
        }
        string Hash(string p) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)));
        static bool NeedsOffice(string source, string format)
        {
            if (DocumentTools.HasOffice) return false;
            string ext = Path.GetExtension(source).ToLowerInvariant();
            if (ext is ".txt" or ".pdf" || !Policy.Documents.Contains(ext)) return false;
            return format is "PDF" or "PNG" or "JPG" or "WEBP";
        }
        string? One(IEnumerable<string> paths, string suffix) => paths.FirstOrDefault(p => p.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
        async Task<string[]> Do(string source, params string[] formats)
        {
            var runnable = new List<string>();
            foreach (string format in formats)
            {
                if (NeedsOffice(source, format)) { Check(Path.GetFileName(source) + " → " + format, null); continue; }
                runnable.Add(format);
            }
            if (runnable.Count == 0) return [];
            var watch = Stopwatch.StartNew(); var values = await ConversionEngine.Convert([source], runnable.ToArray(), CancellationToken.None); all.AddRange(values);
            foreach (var value in values) Check(Path.GetFileName(source) + " → " + value.Format, value.Error == null && value.Outputs.Length > 0);
            Evidence.Log("test.convert", new { source, formats = runnable, elapsedMs = watch.Elapsed.TotalMilliseconds, values });
            return values.SelectMany(v => v.Outputs).ToArray();
        }
        try
        {
            Check("Pandoc disponibile", DocumentTools.HasPandoc);
            if (DocumentTools.HasOffice) Check("LibreOffice disponibile", true); else Check("LibreOffice disponibile", null);
            string png = Path.Combine(root, "immagine è.png"), jpg = Path.Combine(root, "sorgente.jpg"), webp = Path.Combine(root, "sorgente.webp");
            using (var image = new SKBitmap(80, 60, SKColorType.Bgra8888, SKAlphaType.Premul))
            {
                using (var canvas = new SKCanvas(image)) { canvas.Clear(SKColors.Transparent); using var paint = new SKPaint { Color = SKColors.Red }; canvas.DrawRect(20, 10, 40, 30, paint); }
                ConversionEngine.WriteImage(image, png, "PNG"); ConversionEngine.WriteImage(image, jpg, "JPG"); ConversionEngine.WriteImage(image, webp, "WEBP");
            }
            string txt = Path.Combine(root, "testo italiano.txt"), md = Path.Combine(root, "esempio.md"), html = Path.Combine(root, "esempio.html");
            File.WriteAllText(txt, "FLIX verifica testo: caffè, città, perché.\nOriginale da conservare.\n" + string.Join("\n", Enumerable.Range(1, 180).Select(i => "Riga " + i + " del documento locale.")), new UTF8Encoding(false));
            File.WriteAllText(md, "# Documento FLIX\n\nTesto **grassetto**, caffè e città.\n\n- Primo\n- Secondo\n", new UTF8Encoding(false));
            File.WriteAllText(html, "<!doctype html><html><head><meta charset=\"utf-8\"></head><body><h1>FLIX HTML</h1><p>Documento italiano.</p></body></html>", new UTF8Encoding(false));
            foreach (string p in new[] { png, jpg, webp, txt, md, html }) hashes[p] = Hash(p);
            string collision = Path.Combine(root, "immagine è.converted.jpg"); File.WriteAllText(collision, "NON SOVRASCRIVERE"); string collisionHash = Hash(collision);
            var imageOutputs = await Do(png, "JPG", "PDF", "WEBP", "BMP", "TIFF", "GIF");
            Check("collisione preservata", Hash(collision) == collisionHash);
            string? jpeg = One(imageOutputs, ".jpg");
            if (jpeg == null) Check("JPEG prodotto", false);
            else
            {
                Check("JPEG ha firma binaria vera", File.ReadAllBytes(jpeg).Take(2).SequenceEqual(new byte[] { 0xFF, 0xD8 }));
                using var decoded = ConversionEngine.ReadImage(jpeg);
                Check("dimensioni JPEG corrette", decoded.Width == 80 && decoded.Height == 60);
                var corner = decoded.GetPixel(0, 0);
                Check("alpha appiattito su bianco", corner.Red > 245 && corner.Green > 245 && corner.Blue > 245);
            }
            await Do(jpg, "PNG"); await Do(webp, "PNG", "JPG");
            byte[] jpegBytes = File.ReadAllBytes(jpg), exif = [69, 120, 105, 102, 0, 0, 73, 73, 42, 0, 8, 0, 0, 0, 1, 0, 18, 1, 3, 0, 1, 0, 0, 0, 6, 0, 0, 0, 0, 0, 0, 0]; string rotated = Path.Combine(root, "orientation-6.jpg"); using (var stream = File.Create(rotated)) { stream.Write(jpegBytes, 0, 2); stream.Write([255, 225, 0, 34]); stream.Write(exif); stream.Write(jpegBytes, 2, jpegBytes.Length - 2); }
            hashes[rotated] = Hash(rotated); using (var oriented = ConversionEngine.ReadImage(rotated)) Check("EXIF orientation 6 applicata", oriented.Width == 60 && oriented.Height == 80); await Do(rotated, "PNG");
            var concurrent = await Task.WhenAll(ConversionEngine.Convert([png], ["JPG"], CancellationToken.None), ConversionEngine.Convert([png], ["JPG"], CancellationToken.None)); Check("collisioni concorrenti senza overwrite", concurrent.All(v => v.All(r => r.Error == null)) && concurrent.SelectMany(v => v).SelectMany(r => r.Outputs).Distinct().Count() == 2);
            foreach (var input in imageOutputs.Where(p => p.EndsWith(".bmp") || p.EndsWith(".tiff") || p.EndsWith(".gif"))) await Do(input, "PNG");
            var textOutputs = await Do(txt, "PDF", "DOCX", "EPUB", "MD", "HTML", "RTF", "ODT", "FB2", "RST"); await Do(txt, "PNG", "JPG", "WEBP");
            foreach (string document in textOutputs.Where(p => new[] { ".rtf", ".odt", ".fb2", ".rst" }.Contains(Path.GetExtension(p))))
            {
                hashes[document] = Hash(document);
                var round = await Do(document, "TXT");
                Check(Path.GetExtension(document) + " testo conservato", round.Length > 0 && File.ReadAllText(round[0]).Contains("caffè"));
            }
            string bomb = Path.Combine(root, "over-limit.docx");
            using (var archive = ZipFile.Open(bomb, ZipArchiveMode.Create))
            using (var entry = archive.CreateEntry("word/document.xml", CompressionLevel.Optimal).Open())
            { byte[] zeros = new byte[1024 * 1024]; for (int i = 0; i < 257; i++) entry.Write(zeros); }
            long bombDisk = new FileInfo(bomb).Length; long bombExpanded = 0;
            using (var zip = ZipFile.OpenRead(bomb)) foreach (var entry in zip.Entries) bombExpanded = checked(bombExpanded + entry.Length);
            bool bombReachesContainer = bombDisk <= 100L * 1024 * 1024 && bombExpanded > 256L * 1024 * 1024;
            Check("fixture sotto 100 MiB su disco", bombDisk <= 100L * 1024 * 1024);
            Check("fixture oltre 256 MiB espansi", bombExpanded > 256L * 1024 * 1024);
            var bombResult = await ConversionEngine.Convert([bomb], ["TXT"], CancellationToken.None); all.AddRange(bombResult);
            Evidence.Log("test.container", new { disk = bombDisk, expanded = bombExpanded, error = bombResult[0].Error, outputs = bombResult[0].Outputs.Length });
            Check("container espanso oltre limite rifiutato", bombReachesContainer && bombResult[0].Error?.Contains("256 MiB") == true && bombResult[0].Outputs.Length == 0);
            string? pdf = One(textOutputs, ".pdf");
            int pageCount = 0;
            if (pdf == null) Check("TXT ha prodotto PDF", false);
            else
            {
                using (var document = UglyToad.PdfPig.PdfDocument.Open(pdf))
                {
                    pageCount = document.NumberOfPages; Check("TXT PDF multipagina", pageCount >= 3);
                    string text = UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(document.GetPage(1));
                    Check("PDF contiene testo italiano estraibile", text.Contains("caffè") && text.Contains("FLIX"));
                }
                var pages = await Do(pdf, "PNG"); Check("PDF rende tutte le pagine", pages.Length == pageCount);
                Check("PNG ha firma vera", pages.Length > 0 && File.ReadAllBytes(pages[0]).Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
                await Do(pdf, "JPG", "WEBP", "TXT", "DOCX", "MD", "HTML", "EPUB", "RTF", "ODT");
            }
            string? docx = One(textOutputs, ".docx"); string? epub = One(textOutputs, ".epub");
            if (docx == null) Check("DOCX prodotto dal TXT", false);
            else
            {
                var docOutputs = await Do(docx, "PDF", "MD", "HTML", "EPUB", "TXT", "RTF", "ODT");
                using (var zip = ZipFile.OpenRead(docx)) Check("DOCX ha word/document.xml", zip.GetEntry("word/document.xml") != null);
                string? roundtrip = One(docOutputs, ".txt");
                Check("DOCX roundtrip conserva testo", roundtrip != null && File.ReadAllText(roundtrip).Contains("caffè"));
            }
            if (epub == null) Check("EPUB prodotto dal TXT", false);
            else
            {
                using (var zip = ZipFile.OpenRead(epub)) Check("EPUB ha mimetype corretto", zip.GetEntry("mimetype") != null);
                await Do(epub, "DOCX", "TXT", "PDF");
            }
            await Do(md, "DOCX", "EPUB", "PDF", "HTML"); await Do(html, "PDF", "MD", "PNG");
            string deep = Path.Combine(root, new string('p', 48)); Directory.CreateDirectory(deep);
            string deepSource = Path.Combine(deep, "documento.md"); File.Copy(md, deepSource); hashes[deepSource] = Hash(deepSource);
            var deepOutputs = await Do(deepSource, "PDF");
            if (!DocumentTools.HasOffice) Check("PDF da cartella profonda conserva testo", null);
            else if (deepOutputs.Length != 1) Check("PDF da cartella profonda conserva testo", false);
            else using (var deepPdf = UglyToad.PdfPig.PdfDocument.Open(deepOutputs[0])) Check("PDF da cartella profonda conserva testo", deepPdf.GetPage(1).Text.Contains("caffè"));
            string? scanned = One(imageOutputs, ".pdf");
            if (scanned == null) Check("PDF senza testo segnala OCR necessario", false);
            else { var scan = await ConversionEngine.Convert([scanned], ["TXT"], CancellationToken.None); all.AddRange(scan); Check("PDF senza testo segnala OCR necessario", scan[0].Error != null && scan[0].Error!.Contains("OCR")); }
            string jsonFile = Path.Combine(root, "utenti.json");
            File.WriteAllText(jsonFile, "[{\"nome\":\"Marco\",\"città\":\"Milano\",\"attivo\":true},{\"nome\":\"Giulia\",\"città\":\"Roma\",\"attivo\":false}]", new UTF8Encoding(false));
            hashes[jsonFile] = Hash(jsonFile);
            var jsonOutputs = await Do(jsonFile, "YAML", "XML", "CSV", "TXT", "HTML", "MD", "PDF");
            string? csvFromJson = One(jsonOutputs, ".csv");
            if (csvFromJson == null) Check("JSON ha prodotto CSV", false);
            else
            {
                hashes[csvFromJson] = Hash(csvFromJson);
                var jsonRoundtrip = await Do(csvFromJson, "JSON");
                string? jsonBack = One(jsonRoundtrip, ".json");
                Check("JSON → CSV → JSON conserva dati", jsonBack != null && File.ReadAllText(jsonBack).Contains("Milano") && File.ReadAllText(jsonBack).Contains("Giulia"));
            }
            if (MediaTools.HasFfmpeg)
            {
                string wavFile = Path.Combine(root, "tono.wav");
                File.WriteAllBytes(wavFile, Tone());
                hashes[wavFile] = Hash(wavFile);
                var audioOutputs = await Do(wavFile, "MP3", "FLAC", "OGG");
                string? mp3File = One(audioOutputs, ".mp3");
                if (mp3File == null) Check("WAV ha prodotto MP3", false);
                else
                {
                    hashes[mp3File] = Hash(mp3File);
                    var wavFromMp3 = await Do(mp3File, "WAV");
                    string? wavBack = One(wavFromMp3, ".wav");
                    Check("MP3 → WAV produce RIFF WAVE", wavBack != null && File.ReadAllBytes(wavBack).Take(4).SequenceEqual("RIFF"u8.ToArray()));
                }
            }
            string corrupt = Path.Combine(root, "corrotta.png"); File.WriteAllText(corrupt, "non sono pixel");
            var invalid = await ConversionEngine.Convert([corrupt], ["JPG"], CancellationToken.None); Check("immagine corrotta rifiutata", invalid[0].Error != null && invalid[0].Outputs.Length == 0);
            using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); try { await ConversionEngine.Convert([png], ["PDF"], cancel.Token); Check("cancellazione rispettata", false); } catch (OperationCanceledException) { Check("cancellazione rispettata", true); } }
            foreach (var pair in hashes) Check("hash originale " + Path.GetFileName(pair.Key), Hash(pair.Key) == pair.Value);
            Check("nessun temporaneo residuo", !Directory.EnumerateDirectories(root, ".flix-job-*").Any());
            Check("nessun profilo LibreOffice temporaneo residuo", Directory.EnumerateDirectories(Path.GetTempPath(), "Flix-LO-*").All(officeProfilesBefore.Contains));
        }
        catch (Exception ex) { Check("suite senza eccezioni", false); File.WriteAllText(Path.Combine(Evidence.DirectoryPath, "test-exception.txt"), ex.ToString()); }
        File.WriteAllText(Path.Combine(Evidence.DirectoryPath, "conversion-tests.json"), JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, root, passed = checks.Count - failed - skipped, failed, skipped, checks, conversions = all }, new JsonSerializerOptions { WriteIndented = true }));
        return failed == 0 ? 0 : 1;
    }

    static byte[] Tone()
    {
        const int rate = 8000, count = 1600;
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(36 + count * 2);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(count * 2);
        for (int i = 0; i < count; i++)
            writer.Write((short)(Math.Sin(2 * Math.PI * 440 * i / rate) * 8000));
        writer.Flush();
        return output.ToArray();
    }
}
