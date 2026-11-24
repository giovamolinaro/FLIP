using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace Flix;

// Collaudo isolato della matrice audio/video M5.4.1. Chiama solo ConversionEngine:
// niente launcher, Wheel, tray, mutex, named pipe, Registro o menu contestuali.
internal static class MediaMatrixTests
{
    const string SentinelText = "NON-SOVRASCRIVERE-M541";
    const int Width = 320;
    const int Height = 180;

    static readonly UTF8Encoding Utf8 = new(false);
    static readonly string[] AudioSources = [".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma", ".opus"];
    static readonly string[] VideoSources = [".mp4", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".m4v"];
    static readonly string[] AudioTargets = ["WAV", "MP3", "FLAC", "OGG", "M4A", "AAC"];
    static readonly string[] VideoTargets = ["MP4", "MP3", "WAV", "MKV", "WEBM", "AVI", "MOV"];
    static readonly string[] VideoOnlyTargets = ["MP4", "MKV", "WEBM", "AVI", "MOV"];
    static readonly Regex DurationRegex = new(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    static readonly Regex StreamRegex = new(@"Stream #\d+:\d+(?:\[[^\]]+\])?(?:\([^)]*\))?:\s*(Audio|Video):\s*([^,\r\n]+)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    static readonly Regex SizeRegex = new(@"\b(\d{2,5})x(\d{2,5})\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    static readonly Regex RateRegex = new(@"\b(\d+)\s*Hz\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static int Run()
    {
        string root = Path.Combine(Path.GetTempPath(), "Flix-M541-media-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
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
        var samples = new List<SampleRow>();
        string catalogNote = "";
        string ffmpegVersion = "";
        try
        {
            ffmpegVersion = FfmpegVersion();
            catalogNote = CheckCatalog(rows);
            bool ffmpeg = MediaTools.HasFfmpeg;
            var ready = new Dictionary<string, SampleRow>(StringComparer.OrdinalIgnoreCase);
            if (!ffmpeg)
            {
                samples.Add(new SampleRow("*", "", "non generato", "FFmpeg assente: nessun campione e nessuna conversione reale.", 0, ""));
            }
            else
            {
                string bank = Path.Combine(root, "campioni");
                Directory.CreateDirectory(bank);
                foreach (string extension in AudioSources.Concat(VideoSources))
                {
                    var sample = GenerateSample(bank, extension, silent: false);
                    samples.Add(sample);
                    if (sample.Result == "generato") ready[extension] = sample;
                }
                var silent = GenerateSample(bank, ".mp4", silent: true);
                samples.Add(silent);
                foreach (string extension in AudioSources.Concat(VideoSources))
                {
                    bool video = VideoSources.Contains(extension, StringComparer.OrdinalIgnoreCase);
                    string[] formats = Targets(extension, true);
                    if (!ready.TryGetValue(extension, out SampleRow? sample))
                    {
                        foreach (string format in formats)
                            rows.Add(new MatrixRow(extension, format, "non eseguibile", "Campione non valido: la generazione non è una conversione FLIX.", null, video ? "video" : "audio"));
                        continue;
                    }
                    RunSource(root, extension, formats, sample, video ? "video" : "audio", rows);
                }
                RunNegatives(root, ready, silent.Result == "generato" ? silent : null, negatives);
            }
            if (!MediaTools.HasFfmpeg) RunCatalogNegativesOnly(root, negatives);
            var jobs = Directory.EnumerateDirectories(root, ".flix-job-*", SearchOption.AllDirectories).ToArray();
            negatives.Add(new MatrixRow("temporanei", "output incompleti", jobs.Length == 0 ? "superato" : "fallito",
                jobs.Length == 0
                    ? "Nessuna directory .flix-job residua. La pulizia è verificata dopo i rifiuti e dopo gli errori FFmpeg. Un kill a metà codifica non è simulato: interrompere il processo del motore non è un collaudo isolato."
                    : "Residui: " + string.Join(", ", jobs.Select(Path.GetFileName)),
                null, "negativo"));
        }
        catch (Exception ex)
        {
            rows.Add(new MatrixRow("*", "*", "fallito", ex.ToString(), null, "catalogo"));
        }
        finally { Evidence.DirectoryPath = priorLog; }

        int audioPassed = rows.Count(row => row.Group == "audio" && row.Result == "superato");
        int audioFailed = rows.Count(row => row.Group == "audio" && row.Result != "superato");
        int videoPassed = rows.Count(row => row.Group == "video" && row.Result == "superato");
        int videoFailed = rows.Count(row => row.Group == "video" && row.Result != "superato");
        int catalogFailed = rows.Count(row => row.Group == "catalogo" && row.Result != "superato");
        int sampleFailed = samples.Count(row => row.Result is not ("generato" or "non generato"));
        int negativeFailed = negatives.Count(row => row.Result == "fallito");
        var report = new
        {
            utc = DateTimeOffset.UtcNow,
            root,
            method = "ConversionEngine.Convert diretto, senza GUI, Wheel, tray, mutex, named pipe, Registro o --convert-menu",
            ffmpeg = MediaTools.Ffmpeg,
            ffmpegVersion,
            pandoc = DocumentTools.HasPandoc,
            office = DocumentTools.HasOffice,
            catalog = catalogNote,
            audioPassed,
            videoPassed,
            audioFailed,
            videoFailed,
            samples,
            rows,
            negatives
        };
        File.WriteAllText(Path.Combine(root, "media-matrix.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), Utf8);
        var summary = new StringBuilder();
        summary.AppendLine("Cartella: " + root);
        summary.AppendLine("FFmpeg: " + (MediaTools.Ffmpeg ?? "(assente)"));
        if (ffmpegVersion.Length > 0) summary.AppendLine(ffmpegVersion);
        summary.AppendLine("Pandoc=" + DocumentTools.HasPandoc + ", LibreOffice=" + DocumentTools.HasOffice + ".");
        summary.AppendLine(catalogNote);
        summary.AppendLine();
        summary.AppendLine("CAMPIONI (generazione FFmpeg, non conversioni FLIX)");
        foreach (var sample in samples)
            summary.AppendLine(sample.Extension + (sample.Silent ? " senza audio" : "") + " → " + sample.Result + " — " + sample.Detail);
        summary.AppendLine();
        summary.AppendLine("SORGENTE → DESTINAZIONE → ESITO");
        foreach (var row in rows.Where(row => row.Group is "audio" or "video"))
            summary.AppendLine(row.Source + " → " + row.Destination + " → " + row.Result + (row.Result == "superato" ? " — " + row.Detail : " — " + row.Detail));
        summary.AppendLine();
        summary.AppendLine("Totali audio: superate=" + audioPassed + ", non superate=" + audioFailed + ".");
        summary.AppendLine("Totali video: superate=" + videoPassed + ", non superate=" + videoFailed + ".");
        summary.AppendLine("Totale conversioni: superate=" + (audioPassed + videoPassed) + ", non superate=" + (audioFailed + videoFailed) + ".");
        summary.AppendLine();
        summary.AppendLine("CASI NEGATIVI E DI CONFINE");
        foreach (var row in negatives)
            summary.AppendLine(row.Source + " → " + row.Destination + " → " + row.Result + " — " + row.Detail);
        summary.AppendLine("Casi negativi: superati=" + negatives.Count(row => row.Result == "superato")
            + ", falliti=" + negativeFailed
            + ", non eseguibili=" + negatives.Count(row => row.Result == "non eseguibile") + ".");
        File.WriteAllText(Path.Combine(root, "summary.txt"), summary.ToString(), Utf8);
        Console.WriteLine(summary.ToString());
        bool conversionsComplete = MediaTools.HasFfmpeg
            ? audioFailed == 0 && videoFailed == 0 && audioPassed == 42 && videoPassed == 44 && sampleFailed == 0
            : audioPassed + videoPassed + audioFailed + videoFailed == 0;
        return conversionsComplete && catalogFailed == 0 && negativeFailed == 0 ? 0 : 1;
    }

    static string CheckCatalog(List<MatrixRow> rows)
    {
        var problems = new List<string>();
        var note = new StringBuilder();
        note.AppendLine("Motori rilevati: pandoc=" + DocumentTools.HasPandoc + ", office=" + DocumentTools.HasOffice + ", ffmpeg=" + MediaTools.HasFfmpeg
            + (MediaTools.Ffmpeg == null ? "" : " (" + MediaTools.Ffmpeg + ")") + ".");
        if (!Policy.Audio.SequenceEqual(AudioSources, StringComparer.OrdinalIgnoreCase))
            problems.Add("Policy.Audio è " + Join(Policy.Audio) + ".");
        if (!Policy.Video.SequenceEqual(VideoSources, StringComparer.OrdinalIgnoreCase))
            problems.Add("Policy.Video è " + Join(Policy.Video) + ".");
        int audio = 0, video = 0;
        foreach (string extension in AudioSources.Concat(VideoSources))
        {
            string[] withTool = Targets(extension, true);
            if (AudioSources.Contains(extension, StringComparer.OrdinalIgnoreCase)) audio += withTool.Length;
            else video += withTool.Length;
            foreach (var engines in ContextMenuCatalog.EngineSnapshot.AllCombinations())
            {
                string[] policy = Policy.Outputs(["campione" + extension], engines.Pandoc, engines.Office, engines.Ffmpeg);
                string[] expected = Targets(extension, engines.Ffmpeg);
                string[] catalog = ContextMenuCatalog.ForExtension(extension, engines).Select(entry => entry.TargetFormat).ToArray();
                if (!policy.SequenceEqual(expected, StringComparer.Ordinal))
                    problems.Add(extension + " pandoc=" + engines.Pandoc + " office=" + engines.Office + " ffmpeg=" + engines.Ffmpeg
                        + " Policy " + Join(policy) + " invece di " + Join(expected) + ".");
                if (!catalog.SequenceEqual(Policy.OrderFormats(policy), StringComparer.Ordinal))
                    problems.Add(extension + " catalogo " + Join(catalog) + " invece di " + Join(Policy.OrderFormats(policy)) + ".");
            }
            note.AppendLine(extension + " → " + Join(withTool));
        }
        if (audio != 42 || video != 44)
            problems.Add("Conteggi con FFmpeg audio=" + audio + ", video=" + video + ", totale=" + (audio + video) + ".");
        var hidden = new ContextMenuCatalog.EngineSnapshot(true, true, false);
        var shown = new ContextMenuCatalog.EngineSnapshot(false, false, true);
        foreach (string extension in AudioSources.Concat(VideoSources))
        {
            if (ContextMenuCatalog.ForExtension(extension, hidden).Length != 0 || Policy.Outputs(["campione" + extension], true, true, false).Length != 0)
                problems.Add(extension + " resta nel catalogo con FFmpeg simulato assente.");
            if (ContextMenuCatalog.ForExtension(extension, shown).Length == 0)
                problems.Add(extension + " sparisce dal catalogo con il solo FFmpeg attivo.");
        }
        var full = ContextMenuCatalog.BuildMenu(new ContextMenuCatalog.EngineSnapshot(true, true, true));
        foreach (string extension in AudioSources)
        {
            if (!full.TryGetValue(extension, out var entries)) { problems.Add("Menu senza " + extension + "."); continue; }
            if (entries.Any(entry => VideoOnlyTargets.Contains(entry.TargetFormat, StringComparer.Ordinal) || entry.TargetFormat is "WMA" or "OPUS" or "WMV" or "M4V"))
                problems.Add(extension + " offre una destinazione video o non prevista.");
        }
        foreach (string extension in VideoSources)
        {
            if (!full.TryGetValue(extension, out var entries)) { problems.Add("Menu senza " + extension + "."); continue; }
            if (entries.Any(entry => entry.TargetFormat is "FLAC" or "OGG" or "M4A" or "AAC" or "WMA" or "OPUS" or "WMV" or "M4V"))
                problems.Add(extension + " offre FLAC/OGG/M4A/AAC oppure una destinazione non prevista.");
        }
        if (!Targets(".m4a", true).Contains("AAC", StringComparer.Ordinal) || Targets(".m4a", true).Contains("M4A", StringComparer.Ordinal))
            problems.Add("Alias imprevisto fra M4A e AAC.");
        if (!Targets(".aac", true).Contains("M4A", StringComparer.Ordinal) || Targets(".aac", true).Contains("AAC", StringComparer.Ordinal))
            problems.Add("Alias imprevisto fra AAC e M4A.");
        if (!Targets(".m4v", true).Contains("MP4", StringComparer.Ordinal) || Targets(".m4v", true).Contains("M4V", StringComparer.Ordinal))
            problems.Add("Alias imprevisto fra M4V e MP4.");
        if (!Targets(".opus", true).Contains("OGG", StringComparer.Ordinal) || Targets(".opus", true).Contains("OPUS", StringComparer.Ordinal))
            problems.Add("Alias imprevisto fra OPUS e OGG.");
        if (Targets(".wma", true).Length != 6 || Targets(".wmv", true).Length != 7 || Targets(".mp4", true).Contains("MP4", StringComparer.Ordinal))
            problems.Add("WMA, WMV o MP4 non rispettano l'esclusione senza alias.");
        bool officeAgnostic = AudioSources.Concat(VideoSources).All(extension =>
            ContextMenuCatalog.EngineSnapshot.AllCombinations()
                .Where(engines => engines.Ffmpeg)
                .Select(engines => Join(Policy.Outputs(["campione" + extension], engines.Pandoc, engines.Office, true)))
                .Distinct(StringComparer.Ordinal)
                .Count() == 1);
        if (!officeAgnostic) problems.Add("Pandoc o LibreOffice modificano la matrice multimediale.");
        string detail = problems.Count == 0
            ? "42 audio e 44 video con FFmpeg; zero destinazioni senza FFmpeg. Pandoc e LibreOffice non cambiano la matrice. Nessun alias M5.4."
            : string.Join(" ", problems);
        rows.Add(new MatrixRow("catalogo", "audio video", problems.Count == 0 ? "superato" : "fallito", detail, null, "catalogo"));
        note.AppendLine(detail);
        return note.ToString().TrimEnd();
    }

    static void RunSource(string root, string extension, string[] formats, SampleRow sample, string group, List<MatrixRow> rows)
    {
        string folder = Path.Combine(root, "src-" + extension.TrimStart('.'));
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione" + extension);
        File.Copy(sample.Path, source, false);
        foreach (string format in formats)
        {
            Console.WriteLine(extension + " → " + format);
            rows.Add(ConvertOne(source, extension, format, group, sample.Duration, format is "MP4" or "MKV" or "WEBM" or "AVI" or "MOV" ? 1 : 0, format is "MP4" or "MKV" or "WEBM" or "AVI" or "MOV" ? 1 : 1));
        }
    }

    static MatrixRow ConvertOne(string source, string extension, string format, string group, double sourceDuration, int expectedVideo, int expectedAudio)
    {
        string folder = Path.GetDirectoryName(source)!;
        string beforeHash = Hash(source);
        var before = Snapshot(folder);
        ConversionResult result;
        try
        {
            var results = ConversionEngine.Convert([source], [format], CancellationToken.None).GetAwaiter().GetResult();
            if (results.Count != 1) return new MatrixRow(extension, format, "fallito", "Risultati attesi 1, ottenuti " + results.Count + ". " + MediaCommand(source, format), null, group);
            result = results[0];
        }
        catch (Exception ex) { return new MatrixRow(extension, format, "fallito", "Eccezione: " + ex.Message + " " + MediaCommand(source, format), null, group); }

        var problems = new List<string>();
        if (!File.Exists(source) || !string.Equals(Hash(source), beforeHash, StringComparison.Ordinal)) problems.Add("SHA-256 del sorgente cambiato.");
        string? overwrite = OverwriteProblem(before, folder, result.Error == null ? result.Outputs : []);
        if (overwrite != null) problems.Add(overwrite);
        if (Directory.EnumerateDirectories(folder, ".flix-job-*").Any()) problems.Add("Directory .flix-job residua.");
        if (result.Error != null) problems.Add("Motore: " + Clip(result.Error) + " Comando: " + MediaCommand(source, format));
        if (result.Outputs.Length != 1) problems.Add("Output attesi 1, ottenuti " + result.Outputs.Length + ".");
        string? output = result.Outputs.Length == 1 ? result.Outputs[0] : null;
        string codecNote = "";
        if (output != null && result.Error == null)
        {
            if (string.Equals(Path.GetFullPath(output), Path.GetFullPath(source), StringComparison.OrdinalIgnoreCase))
                problems.Add("L'output coincide con il sorgente.");
            string expectedExtension = "." + ConversionEngine.Extension(format);
            if (!Path.GetExtension(output).Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
                problems.Add("Estensione " + Path.GetExtension(output) + " invece di " + expectedExtension + ".");
            if (!File.Exists(output) || new FileInfo(output).Length == 0) problems.Add("Output assente o vuoto.");
            else
            {
                try { codecNote = InspectOutput(output, format, sourceDuration, expectedVideo, expectedAudio, problems); }
                catch (Exception ex) { problems.Add("Analisi: " + ex.Message); }
            }
        }
        string detail = problems.Count == 0
            ? codecNote
            : string.Join(" ", problems);
        return new MatrixRow(extension, format, problems.Count == 0 ? "superato" : "fallito", detail, output == null ? null : Path.GetFileName(output), group);
    }

    static string InspectOutput(string output, string format, double sourceDuration, int expectedVideo, int expectedAudio, List<string> problems)
    {
        byte[] head = ReadHead(output);
        var info = Inspect(output);
        if (!info.Decodable) problems.Add("File non decodificabile" + (info.DecodeError.Length == 0 ? "." : ": " + Clip(info.DecodeError)));
        if (info.Duration <= 0.30 || info.Duration >= 4)
            problems.Add("Durata " + info.Duration.ToString("0.00", CultureInfo.InvariantCulture) + "s non coerente.");
        else if (sourceDuration > 0 && Math.Abs(info.Duration - sourceDuration) > 0.80)
            problems.Add("Durata " + info.Duration.ToString("0.00", CultureInfo.InvariantCulture) + "s lontana dal sorgente " + sourceDuration.ToString("0.00", CultureInfo.InvariantCulture) + "s.");
        int audio = info.Streams.Count(stream => stream.Type == "audio");
        int video = info.Streams.Count(stream => stream.Type == "video");
        if (audio != expectedAudio) problems.Add("Tracce audio " + audio + " invece di " + expectedAudio + ".");
        if (video != expectedVideo) problems.Add("Tracce video " + video + " invece di " + expectedVideo + ".");
        string? container = ContainerProblem(format, head, info);
        if (container != null) problems.Add(container);
        var audioStream = info.Streams.FirstOrDefault(stream => stream.Type == "audio");
        var videoStream = info.Streams.FirstOrDefault(stream => stream.Type == "video");
        string? expectedAudioCodec = AudioCodec(format);
        string? expectedVideoCodec = VideoCodec(format);
        if (expectedAudio > 0)
        {
            if (audioStream == null || !string.Equals(audioStream.Codec, expectedAudioCodec, StringComparison.OrdinalIgnoreCase))
                problems.Add("Codec audio " + (audioStream?.Codec ?? "assente") + " invece di " + expectedAudioCodec + ".");
            if (format == "WAV")
            {
                if (audioStream == null || audioStream.Rate != 44100) problems.Add("WAV a " + (audioStream?.Rate ?? 0) + " Hz invece di 44100.");
                if (audioStream == null || audioStream.Channels != 2) problems.Add("WAV con " + (audioStream?.Channels ?? 0) + " canali invece di 2.");
            }
        }
        else if (audioStream != null) problems.Add("Codec audio inatteso " + audioStream.Codec + ".");
        if (expectedVideo > 0)
        {
            if (videoStream == null || !string.Equals(videoStream.Codec, expectedVideoCodec, StringComparison.OrdinalIgnoreCase))
                problems.Add("Codec video " + (videoStream?.Codec ?? "assente") + " invece di " + expectedVideoCodec + ".");
            if (videoStream == null || videoStream.Width != Width || videoStream.Height != Height)
                problems.Add("Dimensioni " + (videoStream == null ? "assenti" : videoStream.Width + "x" + videoStream.Height) + " invece di " + Width + "x" + Height + ".");
        }
        else if (videoStream != null) problems.Add("Codec video inatteso " + videoStream.Codec + ".");
        string tracks = expectedVideo > 0
            ? (videoStream?.Codec ?? "-") + (expectedAudio > 0 ? "+" + (audioStream?.Codec ?? "-") : " senza audio")
            : (audioStream?.Codec ?? "-");
        string size = expectedVideo > 0 && videoStream != null ? ", " + videoStream.Width + "x" + videoStream.Height : "";
        string rate = format == "WAV" && audioStream != null ? ", " + audioStream.Rate + " Hz, " + audioStream.Channels + " canali" : "";
        return tracks + ", contenitore " + info.FormatName + (info.MajorBrand.Length == 0 ? "" : "/" + info.MajorBrand.Trim())
            + size + rate + ", " + info.Duration.ToString("0.00", CultureInfo.InvariantCulture) + "s, decodificabile";
    }

    static void RunNegatives(string root, Dictionary<string, SampleRow> ready, SampleRow? silent, List<MatrixRow> negatives)
    {
        RunPolicyNegatives(root, negatives);
        if (ready.TryGetValue(".mp3", out SampleRow? mp3)) negatives.Add(Unreadable(Path.Combine(root, "neg-illeggibile-audio"), ".mp3", "WAV"));
        else negatives.Add(Skipped(".mp3", "WAV (illeggibile)", "Manca il campione, ma il file illeggibile non dipende dal campione valido."));
        negatives.Add(Unreadable(Path.Combine(root, "neg-illeggibile-video"), ".mp4", "MKV"));
        negatives.Add(Unreadable(Path.Combine(root, "neg-illeggibile-wma"), ".wma", "MP3"));
        negatives.Add(Unreadable(Path.Combine(root, "neg-illeggibile-wmv"), ".wmv", "MP4"));
        if (silent == null)
        {
            negatives.Add(Skipped(".mp4", "MP3 (senza audio)", "Campione video senza audio non generato."));
            negatives.Add(Skipped(".mp4", "WAV (senza audio)", "Campione video senza audio non generato."));
            negatives.Add(Skipped(".mp4", "MKV (senza audio)", "Campione video senza audio non generato."));
        }
        else
        {
            string folder = Path.Combine(root, "neg-senza-audio");
            Directory.CreateDirectory(folder);
            string source = Path.Combine(folder, "senza audio.mp4");
            File.Copy(silent.Path, source, false);
            negatives.Add(ExpectRefusal(source, ".mp4", "MP3 (senza traccia audio)", "MP3", "FFmpeg"));
            negatives.Add(ExpectRefusal(source, ".mp4", "WAV (senza traccia audio)", "WAV", "FFmpeg"));
            foreach (string format in new[] { "MKV", "WEBM", "AVI" })
            {
                Console.WriteLine(".mp4 senza audio → " + format);
                var row = ConvertOne(source, ".mp4", format, "negativo", silent.Duration, 1, 0);
                negatives.Add(row with { Destination = format + " (video senza audio)", Detail = row.Result == "superato" ? "Video senza audio accettato: " + row.Detail : row.Detail });
            }
        }
        if (ready.TryGetValue(".mp3", out mp3) && ready.TryGetValue(".mp4", out SampleRow? mp4))
        {
            negatives.Add(Spaced(Path.Combine(root, "neg-spazi", "cartella con spazi"), mp3, ".mp3", "WAV"));
            negatives.Add(Spaced(Path.Combine(root, "neg-spazi-video", "cartella con spazi"), mp4, ".mp4", "MKV"));
            negatives.Add(NamingCollision(Path.Combine(root, "neg-collisione"), mp3));
            if (ready.TryGetValue(".wav", out SampleRow? wav)) negatives.Add(NamingSequence(Path.Combine(root, "neg-sequenza"), wav));
            else negatives.Add(Skipped(".wav", "MP3 (due passate)", "Campione WAV non disponibile."));
        }
        else negatives.Add(Skipped(".mp3", "percorsi e nomi", "Campioni MP3/MP4 non disponibili."));
        negatives.Add(SimulatedFfmpegAbsence());
    }

    static void RunCatalogNegativesOnly(string root, List<MatrixRow> negatives)
    {
        RunPolicyNegatives(root, negatives);
        foreach (string label in new[] { "illeggibile", "senza audio", "percorsi con spazi", "collisione nomi" })
            negatives.Add(new MatrixRow("media", label, "non eseguibile", "FFmpeg assente: nessuna conversione reale. PATH non modificato.", null, "negativo"));
        negatives.Add(SimulatedFfmpegAbsence());
    }

    static void RunPolicyNegatives(string root, List<MatrixRow> negatives)
    {
        (string Extension, string Format)[] audioToVideo =
        [
            (".mp3", "MP4"), (".wav", "MKV"), (".flac", "WEBM"), (".ogg", "AVI"),
            (".m4a", "MOV"), (".aac", "MP4"), (".wma", "MP4"), (".opus", "WEBM")
        ];
        foreach (var item in audioToVideo)
            negatives.Add(PolicyRefusal(Path.Combine(root, "neg-audio-video-" + item.Extension.TrimStart('.') + "-" + item.Format.ToLowerInvariant()), item.Extension, item.Format));
        foreach (string format in new[] { "FLAC", "OGG", "M4A", "AAC" })
            negatives.Add(PolicyRefusal(Path.Combine(root, "neg-video-audio-mp4-" + format.ToLowerInvariant()), ".mp4", format));
        negatives.Add(PolicyRefusal(Path.Combine(root, "neg-video-audio-mkv-flac"), ".mkv", "FLAC"));
        (string Extension, string Format)[] blocked =
        [
            (".mp3", "WMA"), (".mp3", "OPUS"), (".wav", "WMA"), (".flac", "OPUS"),
            (".mp4", "WMV"), (".mp4", "M4V"), (".mkv", "WMV"), (".webm", "M4V"),
            (".wma", "WMA"), (".opus", "OPUS"), (".wmv", "WMV"), (".m4v", "M4V")
        ];
        foreach (var item in blocked)
            negatives.Add(PolicyRefusal(Path.Combine(root, "neg-destinazione-" + item.Extension.TrimStart('.') + "-" + item.Format.ToLowerInvariant()), item.Extension, item.Format));
        foreach (string extension in AudioSources.Concat(VideoSources))
        {
            string format = extension.TrimStart('.').ToUpperInvariant();
            negatives.Add(PolicyRefusal(Path.Combine(root, "neg-stesso-" + extension.TrimStart('.')), extension, format));
        }
    }

    static MatrixRow PolicyRefusal(string folder, string extension, string format)
    {
        if (ContextMenuCatalog.EngineSnapshot.AllCombinations().Any(engines =>
            ContextMenuCatalog.ForExtension(extension, engines).Any(entry => entry.TargetFormat == format)))
            return new MatrixRow(extension, format, "fallito", "Il catalogo autorizza questa conversione in almeno una combinazione di motori.", null, "negativo");
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione" + extension);
        File.WriteAllBytes(source, "FLIX-M541-segnaposto"u8.ToArray());
        return ExpectRefusal(source, extension, format, format, "non disponibile");
    }

    static MatrixRow ExpectRefusal(string source, string extension, string label, string format, string fragment)
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
        else if (result.Error.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0)
            problems.Add("Messaggio \"" + Clip(result.Error) + "\" senza \"" + fragment + "\".");
        if (result.Outputs.Length != 0) problems.Add("Output inattesi: " + string.Join(", ", result.Outputs.Select(Path.GetFileName)) + ".");
        if (!File.Exists(source) || Hash(source) != beforeHash) problems.Add("SHA-256 del sorgente cambiato.");
        string? overwrite = OverwriteProblem(before, folder, []);
        if (overwrite != null) problems.Add(overwrite);
        if (Directory.EnumerateDirectories(folder, ".flix-job-*").Any()) problems.Add("Directory .flix-job residua.");
        string detail = problems.Count == 0
            ? "Rifiutata (" + Clip(result.Error ?? "") + "). Nessun output, sorgente invariato, nessun temporaneo."
            : string.Join(" ", problems);
        return new MatrixRow(extension, label, problems.Count == 0 ? "superato" : "fallito", detail, null, "negativo");
    }

    static MatrixRow Unreadable(string folder, string extension, string format)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "illeggibile" + extension);
        File.WriteAllBytes(source, "FLIX-M541-NON-E-UN-MEDIA"u8.ToArray());
        if (!Targets(extension, true).Contains(format, StringComparer.Ordinal))
            return new MatrixRow(extension, format + " (illeggibile)", "fallito", "La destinazione non è nel catalogo, quindi il file non raggiunge FFmpeg.", null, "negativo");
        var row = ExpectRefusal(source, extension, format + " (illeggibile)", format, "FFmpeg");
        return row.Result == "superato"
            ? row with { Detail = row.Detail + " Comando previsto: " + MediaCommand(source, format) }
            : row;
    }

    static MatrixRow Spaced(string folder, SampleRow sample, string extension, string format)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "clip di prova" + extension);
        File.Copy(sample.Path, source, false);
        Console.WriteLine(extension + " con spazi → " + format);
        int video = format is "MP4" or "MKV" or "WEBM" or "AVI" or "MOV" ? 1 : 0;
        var row = ConvertOne(source, extension, format, "negativo", sample.Duration, video, 1);
        return row with
        {
            Destination = format + " (percorso con spazi)",
            Detail = row.Result == "superato" ? "Percorso con spazi conservato: " + Path.GetFileName(source) + " → " + row.Output + ". " + row.Detail : row.Detail
        };
    }

    static MatrixRow NamingCollision(string folder, SampleRow sample)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione.mp3");
        File.Copy(sample.Path, source, false);
        string sentinel = Path.Combine(folder, "campione.converted.wav");
        string unrelated = Path.Combine(folder, "nota gia presente.txt");
        File.WriteAllText(sentinel, SentinelText, Encoding.ASCII);
        File.WriteAllText(unrelated, "PREESISTENTE", Encoding.ASCII);
        string sourceHash = Hash(source), sentinelHash = Hash(sentinel), unrelatedHash = Hash(unrelated);
        var before = Snapshot(folder);
        ConversionResult result;
        try { result = ConversionEngine.Convert([source], ["WAV"], CancellationToken.None).GetAwaiter().GetResult()[0]; }
        catch (Exception ex) { return new MatrixRow(".mp3", "WAV (destinazione esistente)", "fallito", ex.Message, null, "negativo"); }
        string expected = Path.Combine(folder, "campione.converted (1).wav");
        var problems = new List<string>();
        if (result.Error != null) problems.Add(result.Error);
        if (result.Outputs.Length != 1 || !string.Equals(result.Outputs[0], expected, StringComparison.OrdinalIgnoreCase))
            problems.Add("Atteso campione.converted (1).wav, ottenuto " + string.Join(", ", result.Outputs.Select(Path.GetFileName)) + ".");
        if (Hash(source) != sourceHash) problems.Add("SHA-256 del sorgente cambiato.");
        if (!File.Exists(sentinel) || Hash(sentinel) != sentinelHash) problems.Add("Il file .converted.wav preesistente è stato modificato.");
        if (!File.Exists(unrelated) || Hash(unrelated) != unrelatedHash) problems.Add("Il file preesistente non collegato è stato modificato.");
        string? extra = OverwriteProblem(before, folder, result.Error == null ? result.Outputs : []);
        if (extra != null) problems.Add(extra);
        if (problems.Count == 0) InspectOutput(expected, "WAV", sample.Duration, 0, 1, problems);
        string detail = problems.Count == 0
            ? "Regola rispettata: campione.converted.wav conservato, creato campione.converted (1).wav."
            : string.Join(" ", problems);
        return new MatrixRow(".mp3", "WAV (destinazione esistente)", problems.Count == 0 ? "superato" : "fallito", detail, Path.GetFileName(expected), "negativo");
    }

    static MatrixRow NamingSequence(string folder, SampleRow sample)
    {
        Directory.CreateDirectory(folder);
        string source = Path.Combine(folder, "campione.wav");
        File.Copy(sample.Path, source, false);
        string sourceHash = Hash(source);
        ConversionResult first, second;
        try
        {
            first = ConversionEngine.Convert([source], ["MP3"], CancellationToken.None).GetAwaiter().GetResult()[0];
            second = ConversionEngine.Convert([source], ["MP3"], CancellationToken.None).GetAwaiter().GetResult()[0];
        }
        catch (Exception ex) { return new MatrixRow(".wav", "MP3 (due passate)", "fallito", ex.Message, null, "negativo"); }
        string primary = Path.Combine(folder, "campione.converted.mp3");
        string numbered = Path.Combine(folder, "campione.converted (1).mp3");
        var problems = new List<string>();
        if (first.Error != null) problems.Add("Prima passata: " + first.Error);
        if (second.Error != null) problems.Add("Seconda passata: " + second.Error);
        if (first.Outputs.Length != 1 || !string.Equals(first.Outputs[0], primary, StringComparison.OrdinalIgnoreCase))
            problems.Add("La prima passata non ha creato campione.converted.mp3.");
        string primaryHash = File.Exists(primary) ? Hash(primary) : "";
        if (second.Outputs.Length != 1 || !string.Equals(second.Outputs[0], numbered, StringComparison.OrdinalIgnoreCase))
            problems.Add("La seconda passata non ha creato campione.converted (1).mp3.");
        if (File.Exists(primary) && Hash(primary) != primaryHash) problems.Add("La seconda passata ha modificato il primo output.");
        if (Hash(source) != sourceHash) problems.Add("SHA-256 del sorgente cambiato.");
        if (Directory.EnumerateDirectories(folder).Any()) problems.Add("Directory temporanea .flix-job residua.");
        if (problems.Count == 0)
        {
            InspectOutput(primary, "MP3", sample.Duration, 0, 1, problems);
            InspectOutput(numbered, "MP3", sample.Duration, 0, 1, problems);
        }
        string detail = problems.Count == 0
            ? "Prima passata campione.converted.mp3; seconda passata campione.converted (1).mp3 senza sovrascrivere."
            : string.Join(" ", problems);
        return new MatrixRow(".wav", "MP3 (due passate)", problems.Count == 0 ? "superato" : "fallito", detail, "campione.converted.mp3 + (1)", "negativo");
    }

    static MatrixRow SimulatedFfmpegAbsence()
    {
        var off = new ContextMenuCatalog.EngineSnapshot(true, true, false);
        bool clear = AudioSources.Concat(VideoSources).All(extension =>
            Policy.Outputs(["campione" + extension], true, true, false).Length == 0
            && ContextMenuCatalog.ForExtension(extension, off).Length == 0
            && !ContextMenuCatalog.BuildMenu(off).ContainsKey(extension));
        string detail = clear
            ? "Con ffmpeg=false il catalogo non offre destinazioni audio/video, per qualsiasi Pandoc e LibreOffice. PATH non modificato e il binario non è stato rimosso. HasFfmpeg reale=" + MediaTools.HasFfmpeg + "."
            : "Il catalogo con ffmpeg=false espone ancora destinazioni multimediali.";
        return new MatrixRow("catalogo", "FFmpeg assente simulato", clear ? "superato" : "fallito", detail, null, "negativo");
    }

    static MatrixRow Skipped(string source, string destination, string detail) =>
        new(source, destination, "non eseguibile", detail, null, "negativo");

    static SampleRow GenerateSample(string bank, string extension, bool silent)
    {
        if (MediaTools.Ffmpeg == null)
            return new SampleRow(extension, "", "non valido", "FFmpeg assente.", 0, "", silent);
        string name = silent ? "senza-audio.mp4" : "campione" + extension;
        string path = Path.Combine(bank, name);
        var args = silent ? SilentVideoArgs(path) : SampleArgs(path, extension);
        if (args == null)
            return new SampleRow(extension, path, "non valido", "Nessun generatore per " + extension + ".", 0, "", silent);
        var run = RunProcess(MediaTools.Ffmpeg, args, 60000);
        if (run.Code != 0 || !File.Exists(path) || new FileInfo(path).Length == 0)
            return new SampleRow(extension, path, "non valido", "Generazione fallita (exit " + run.Code + "): " + Clip(run.Stderr), 0, "", silent);
        var info = Inspect(path);
        var problems = new List<string>();
        if (!info.Decodable) problems.Add("Campione non decodificabile: " + Clip(info.DecodeError));
        if (info.Duration < 0.80 || info.Duration > 2.50)
            problems.Add("Durata campione " + info.Duration.ToString("0.00", CultureInfo.InvariantCulture) + "s.");
        int audio = info.Streams.Count(stream => stream.Type == "audio");
        int video = info.Streams.Count(stream => stream.Type == "video");
        if (silent || VideoSources.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            if (video != 1) problems.Add("Tracce video " + video + ".");
            var picture = info.Streams.FirstOrDefault(stream => stream.Type == "video");
            if (picture == null || picture.Width != Width || picture.Height != Height)
                problems.Add("Dimensioni campione " + (picture == null ? "assenti" : picture.Width + "x" + picture.Height) + ".");
        }
        else if (video != 0) problems.Add("Il campione audio contiene video.");
        if (silent)
        {
            if (audio != 0) problems.Add("Il campione senza audio contiene " + audio + " tracce audio.");
        }
        else if (audio != 1) problems.Add("Tracce audio " + audio + ".");
        if (!silent && extension == ".wma" && info.Streams.All(stream => stream.Codec != "wmav2")) problems.Add("WMA non è wmav2.");
        if (!silent && extension == ".opus" && info.Streams.All(stream => stream.Codec != "opus")) problems.Add("OPUS non è opus.");
        if (!silent && extension == ".wmv" && (info.Streams.All(stream => stream.Codec != "wmv2") || info.Streams.All(stream => stream.Codec != "wmav2")))
            problems.Add("WMV non è wmv2+wmav2.");
        if (!silent && extension == ".m4v" && (info.Streams.All(stream => stream.Codec != "h264") || info.Streams.All(stream => stream.Codec != "aac")))
            problems.Add("M4V non è h264+aac.");
        string codecs = string.Join("+", info.Streams.Select(stream => stream.Type + ":" + stream.Codec));
        string detail = problems.Count == 0
            ? "Campione generato, non è una conversione FLIX. " + codecs + ", " + info.FormatName + ", " + info.Duration.ToString("0.00", CultureInfo.InvariantCulture) + "s, " + new FileInfo(path).Length + " byte."
            : string.Join(" ", problems);
        return new SampleRow(extension, path, problems.Count == 0 ? "generato" : "non valido", detail, info.Duration, codecs, silent);
    }

    static List<string>? SampleArgs(string path, string extension)
    {
        var args = new List<string> { "-y", "-nostdin", "-hide_banner", "-loglevel", "error" };
        if (AudioSources.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            args.AddRange(["-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=1.2", "-ac", "1"]);
            switch (extension)
            {
                case ".mp3": args.AddRange(["-c:a", "libmp3lame", "-q:a", "4"]); break;
                case ".wav": args.AddRange(["-c:a", "pcm_s16le"]); break;
                case ".flac": args.AddRange(["-c:a", "flac"]); break;
                case ".ogg": args.AddRange(["-c:a", "libvorbis", "-q:a", "4"]); break;
                case ".m4a": args.AddRange(["-c:a", "aac", "-b:a", "128k", "-f", "ipod"]); break;
                case ".aac": args.AddRange(["-c:a", "aac", "-b:a", "128k"]); break;
                case ".wma": args.AddRange(["-c:a", "wmav2", "-b:a", "128k"]); break;
                case ".opus": args.AddRange(["-c:a", "libopus", "-b:a", "64k"]); break;
                default: return null;
            }
        }
        else if (VideoSources.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            args.AddRange([
                "-f", "lavfi", "-i", "testsrc=size=320x180:rate=12:duration=1.2",
                "-f", "lavfi", "-i", "sine=frequency=440:sample_rate=44100:duration=1.2",
                "-shortest"
            ]);
            switch (extension)
            {
                case ".mp4":
                case ".mkv":
                case ".m4v":
                    args.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-crf", "30", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k"]);
                    break;
                case ".webm":
                    args.AddRange(["-c:v", "libvpx-vp9", "-deadline", "realtime", "-cpu-used", "8", "-b:v", "200k", "-pix_fmt", "yuv420p", "-c:a", "libopus", "-b:a", "64k"]);
                    break;
                case ".avi":
                    args.AddRange(["-c:v", "mpeg4", "-q:v", "8", "-c:a", "libmp3lame", "-q:a", "6"]);
                    break;
                case ".mov":
                    args.AddRange(["-c:v", "libx264", "-preset", "veryfast", "-crf", "30", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "96k", "-movflags", "+faststart"]);
                    break;
                case ".wmv":
                    args.AddRange(["-c:v", "wmv2", "-b:v", "400k", "-c:a", "wmav2", "-b:a", "96k"]);
                    break;
                default: return null;
            }
        }
        else return null;
        args.Add(path);
        return args;
    }

    static List<string> SilentVideoArgs(string path) =>
    [
        "-y", "-nostdin", "-hide_banner", "-loglevel", "error",
        "-f", "lavfi", "-i", "testsrc=size=320x180:rate=12:duration=1.2",
        "-c:v", "libx264", "-preset", "veryfast", "-crf", "30", "-pix_fmt", "yuv420p", "-an", path
    ];

    static string[] Targets(string extension, bool ffmpeg)
    {
        if (!ffmpeg) return [];
        string[] pool = AudioSources.Contains(extension, StringComparer.OrdinalIgnoreCase) ? AudioTargets
            : VideoSources.Contains(extension, StringComparer.OrdinalIgnoreCase) ? VideoTargets : [];
        string same = extension.TrimStart('.').ToUpperInvariant();
        return pool.Where(target => !string.Equals(target, same, StringComparison.Ordinal)).ToArray();
    }

    static string? AudioCodec(string format) => format switch
    {
        "WAV" => "pcm_s16le",
        "MP3" => "mp3",
        "FLAC" => "flac",
        "OGG" => "vorbis",
        "AAC" or "M4A" or "MP4" or "MKV" or "MOV" => "aac",
        "WEBM" => "opus",
        "AVI" => "mp3",
        _ => null
    };

    static string? VideoCodec(string format) => format switch
    {
        "MP4" or "MKV" or "MOV" => "h264",
        "WEBM" => "vp9",
        "AVI" => "mpeg4",
        _ => null
    };

    static string MediaCommand(string source, string target)
    {
        var args = new List<string> { "-y", "-nostdin", "-hide_banner", "-i", source };
        switch (target)
        {
            case "WAV": args.AddRange(["-vn", "-acodec", "pcm_s16le", "-ar", "44100", "-ac", "2"]); break;
            case "MP3": args.AddRange(["-vn", "-acodec", "libmp3lame", "-q:a", "2"]); break;
            case "FLAC": args.AddRange(["-vn", "-acodec", "flac"]); break;
            case "OGG": args.AddRange(["-vn", "-acodec", "libvorbis", "-q:a", "4"]); break;
            case "AAC": args.AddRange(["-vn", "-acodec", "aac", "-b:a", "192k"]); break;
            case "M4A": args.AddRange(["-vn", "-acodec", "aac", "-b:a", "192k", "-f", "ipod"]); break;
            case "MP4": args.AddRange(["-c:v", "libx264", "-preset", "fast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-b:a", "160k", "-movflags", "+faststart"]); break;
            case "MKV": args.AddRange(["-c:v", "libx264", "-preset", "fast", "-crf", "23", "-c:a", "aac", "-b:a", "160k"]); break;
            case "WEBM": args.AddRange(["-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-deadline", "realtime", "-cpu-used", "4", "-c:a", "libopus", "-b:a", "128k"]); break;
            case "AVI": args.AddRange(["-c:v", "mpeg4", "-q:v", "5", "-c:a", "libmp3lame", "-q:a", "4"]); break;
            case "MOV": args.AddRange(["-c:v", "libx264", "-preset", "fast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac", "-movflags", "+faststart"]); break;
            default: args.Add("(formato non previsto da MediaTools)"); break;
        }
        args.Add("<file temporaneo nel job>");
        return (MediaTools.Ffmpeg ?? "ffmpeg") + " " + string.Join(" ", args.Select(Quote));
    }

    static string? ContainerProblem(string format, byte[] head, MediaInfo info)
    {
        bool formatHas(string token) => info.FormatName.Split(',').Any(name => name.Equals(token, StringComparison.OrdinalIgnoreCase));
        string brand = info.MajorBrand.Trim();
        switch (format)
        {
            case "WAV":
                if (!Ascii(head, 0, "RIFF") || !Ascii(head, 8, "WAVE")) return "Contenitore WAV senza firma RIFF/WAVE.";
                if (!formatHas("wav")) return "ffprobe segnala " + info.FormatName + " invece di wav.";
                return null;
            case "MP3":
                bool mp3 = Ascii(head, 0, "ID3") || (head.Length > 1 && head[0] == 0xFF && (head[1] & 0xE0) == 0xE0);
                if (!mp3) return "Contenitore MP3 senza firma ID3 o frame MPEG.";
                if (!formatHas("mp3")) return "ffprobe segnala " + info.FormatName + " invece di mp3.";
                return null;
            case "FLAC":
                if (!Ascii(head, 0, "fLaC")) return "Contenitore FLAC senza firma fLaC.";
                if (!formatHas("flac")) return "ffprobe segnala " + info.FormatName + " invece di flac.";
                return null;
            case "OGG":
                if (!Ascii(head, 0, "OggS")) return "Contenitore OGG senza firma OggS.";
                if (!formatHas("ogg")) return "ffprobe segnala " + info.FormatName + " invece di ogg.";
                return null;
            case "AAC":
                if (head.Length < 2 || head[0] != 0xFF || (head[1] & 0xF0) != 0xF0) return "Contenitore AAC senza sync ADTS.";
                if (!formatHas("aac")) return "ffprobe segnala " + info.FormatName + " invece di aac.";
                return null;
            case "M4A":
                if (!formatHas("mp4") && !formatHas("m4a") && !formatHas("mov")) return "Contenitore M4A segnalato come " + info.FormatName + ".";
                if (!brand.Equals("M4A", StringComparison.OrdinalIgnoreCase)) return "Brand M4A \"" + info.MajorBrand + "\" invece di M4A.";
                return null;
            case "MP4":
                if (!formatHas("mp4") && !formatHas("mov")) return "Contenitore MP4 segnalato come " + info.FormatName + ".";
                if (brand.Equals("qt", StringComparison.OrdinalIgnoreCase) || brand.Equals("M4A", StringComparison.OrdinalIgnoreCase) || brand.Equals("M4V", StringComparison.OrdinalIgnoreCase))
                    return "Brand MP4 \"" + info.MajorBrand + "\" non è un MP4.";
                if (brand.Length == 0) return "Brand MP4 assente.";
                return null;
            case "MOV":
                if (!formatHas("mov") && !formatHas("mp4")) return "Contenitore MOV segnalato come " + info.FormatName + ".";
                if (!brand.Equals("qt", StringComparison.OrdinalIgnoreCase)) return "Brand MOV \"" + info.MajorBrand + "\" invece di qt.";
                return null;
            case "MKV":
                if (!Ebml(head) || !HeadText(head, "matroska") || HeadText(head, "webm")) return "Contenitore MKV senza DocType matroska.";
                if (!formatHas("matroska")) return "ffprobe segnala " + info.FormatName + " invece di matroska.";
                return null;
            case "WEBM":
                if (!Ebml(head) || !HeadText(head, "webm")) return "Contenitore WEBM senza DocType webm.";
                if (!formatHas("webm")) return "ffprobe segnala " + info.FormatName + " invece di webm.";
                return null;
            case "AVI":
                if (!Ascii(head, 0, "RIFF") || !Ascii(head, 8, "AVI ")) return "Contenitore AVI senza firma RIFF/AVI.";
                if (!formatHas("avi")) return "ffprobe segnala " + info.FormatName + " invece di avi.";
                return null;
            default:
                return "Contenitore non classificato: " + format + ".";
        }
    }

    static MediaInfo Inspect(string path)
    {
        var info = new MediaInfo();
        string? ffprobe = FfprobePath();
        if (ffprobe != null)
        {
            var probe = RunProcess(ffprobe, ["-hide_banner", "-v", "error", "-show_format", "-show_streams", "-of", "json", path], 30000);
            info.ProbeExit = probe.Code;
            if (probe.Code == 0) ParseProbe(info, probe.Stdout);
        }
        if (info.Streams.Count == 0 && MediaTools.Ffmpeg != null)
        {
            var listed = RunProcess(MediaTools.Ffmpeg, ["-hide_banner", "-nostdin", "-i", path], 30000);
            info.ProbeExit = listed.Code;
            ParseFfmpegList(info, listed.Stderr);
        }
        if (MediaTools.Ffmpeg == null)
        {
            info.DecodeError = "FFmpeg assente.";
            return info;
        }
        var decoded = RunProcess(MediaTools.Ffmpeg, ["-hide_banner", "-nostdin", "-v", "error", "-i", path, "-f", "null", "-"], 60000);
        info.Decodable = decoded.Code == 0 && info.Streams.Count > 0;
        if (decoded.Code != 0) info.DecodeError = decoded.Stderr.Trim();
        return info;
    }

    static void ParseProbe(MediaInfo info, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.TryGetProperty("format", out var format))
        {
            info.FormatName = Text(format, "format_name");
            info.Duration = Number(format, "duration");
            if (format.TryGetProperty("tags", out var tags)) info.MajorBrand = Text(tags, "major_brand");
        }
        if (!root.TryGetProperty("streams", out var streams)) return;
        foreach (var stream in streams.EnumerateArray())
        {
            info.Streams.Add(new StreamInfo(
                Text(stream, "codec_type"),
                Text(stream, "codec_name"),
                (int)Number(stream, "sample_rate"),
                stream.TryGetProperty("channels", out var channels) && channels.TryGetInt32(out int count) ? count : 0,
                stream.TryGetProperty("width", out var width) && width.TryGetInt32(out int w) ? w : 0,
                stream.TryGetProperty("height", out var height) && height.TryGetInt32(out int h) ? h : 0));
            if (info.Duration <= 0) info.Duration = Number(stream, "duration");
        }
    }

    static void ParseFfmpegList(MediaInfo info, string stderr)
    {
        var duration = DurationRegex.Match(stderr);
        if (duration.Success)
            info.Duration = int.Parse(duration.Groups[1].Value, CultureInfo.InvariantCulture) * 3600
                + int.Parse(duration.Groups[2].Value, CultureInfo.InvariantCulture) * 60
                + double.Parse(duration.Groups[3].Value, CultureInfo.InvariantCulture);
        var input = Regex.Match(stderr, @"Input #\d+,\s*([^,]+),", RegexOptions.CultureInvariant);
        if (input.Success) info.FormatName = input.Groups[1].Value.Trim();
        foreach (Match match in StreamRegex.Matches(stderr))
        {
            string kind = match.Groups[1].Value.ToLowerInvariant();
            string codec = match.Groups[2].Value.Trim().Split(' ')[0];
            string tail = stderr[match.Index..Math.Min(stderr.Length, match.Index + 220)];
            int rate = 0, channels = 0, width = 0, height = 0;
            var rateMatch = RateRegex.Match(tail);
            if (rateMatch.Success) rate = int.Parse(rateMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (tail.Contains("stereo", StringComparison.OrdinalIgnoreCase)) channels = 2;
            else if (tail.Contains("mono", StringComparison.OrdinalIgnoreCase)) channels = 1;
            var size = SizeRegex.Match(tail);
            if (size.Success && kind == "video")
            {
                width = int.Parse(size.Groups[1].Value, CultureInfo.InvariantCulture);
                height = int.Parse(size.Groups[2].Value, CultureInfo.InvariantCulture);
            }
            info.Streams.Add(new StreamInfo(kind, codec, rate, channels, width, height));
        }
    }

    static string Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    static double Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out double number)) return number;
        if (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return parsed;
        return 0;
    }

    static string? FfprobePath()
    {
        if (MediaTools.Ffmpeg == null) return null;
        string? directory = Path.GetDirectoryName(MediaTools.Ffmpeg);
        if (directory == null) return null;
        string candidate = Path.Combine(directory, "ffprobe.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    static string FfmpegVersion()
    {
        if (MediaTools.Ffmpeg == null) return "";
        var run = RunProcess(MediaTools.Ffmpeg, ["-version"], 15000);
        string line = run.Stdout.Replace("\r", "").Split('\n').FirstOrDefault(item => item.Length > 0) ?? "";
        return line.Trim();
    }

    static (int Code, string Stdout, string Stderr) RunProcess(string exe, IReadOnlyList<string> args, int timeoutMs) =>
        Task.Run(() =>
        {
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new IOException("Impossibile avviare " + exe);
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(true); } catch (InvalidOperationException) { }
                process.WaitForExit(5000);
                return (-1, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult() + " timeout");
            }
            return (process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
        }).GetAwaiter().GetResult();

    static byte[] ReadHead(string path)
    {
        byte[] buffer = new byte[4096];
        using var stream = File.OpenRead(path);
        int read = stream.Read(buffer, 0, buffer.Length);
        if (read == buffer.Length) return buffer;
        byte[] exact = new byte[read];
        Buffer.BlockCopy(buffer, 0, exact, 0, read);
        return exact;
    }

    static bool Ascii(byte[] data, int offset, string text) =>
        data.Length >= offset + text.Length && Encoding.ASCII.GetString(data, offset, text.Length) == text;

    static bool Ebml(byte[] data) => data.Length >= 4 && data[0] == 0x1A && data[1] == 0x45 && data[2] == 0xDF && data[3] == 0xA3;

    static bool HeadText(byte[] data, string text)
    {
        int count = Math.Min(data.Length, 256);
        return Encoding.ASCII.GetString(data, 0, count).Contains(text, StringComparison.Ordinal);
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
        return null;
    }

    static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    static string Quote(string value) => value.IndexOfAny([' ', '\t', '"']) >= 0 ? "\"" + value.Replace("\"", "\\\"") + "\"" : value;

    static string Join(IEnumerable<string> items)
    {
        var list = items.ToArray();
        return list.Length == 0 ? "(nessuna)" : string.Join(", ", list);
    }

    static string Clip(string text)
    {
        string flat = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return flat.Length <= 280 ? flat : flat[..280];
    }

    internal sealed record MatrixRow(string Source, string Destination, string Result, string Detail, string? Output, string Group);
    internal sealed record SampleRow(string Extension, string Path, string Result, string Detail, double Duration, string Codecs, bool Silent = false);
    sealed record StreamInfo(string Type, string Codec, int Rate, int Channels, int Width, int Height);
    sealed class MediaInfo
    {
        public bool Decodable;
        public string DecodeError = "";
        public int ProbeExit;
        public string FormatName = "";
        public string MajorBrand = "";
        public double Duration;
        public List<StreamInfo> Streams = [];
    }
}
