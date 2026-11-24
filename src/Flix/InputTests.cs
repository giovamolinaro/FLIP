using System.IO;
using System.Security;
using System.Text.Json;
namespace Flix;

internal static class InputTests
{
    internal static int Run()
    {
        if (Environment.GetCommandLineArgs().Any(arg => arg.Equals("--image-matrix-only", StringComparison.Ordinal)))
            return ImageMatrixTests.Run();
        if (Environment.GetCommandLineArgs().Any(arg => arg.Equals("--content-matrix-only", StringComparison.Ordinal)))
            return ContentMatrixTests.Run();
        if (Environment.GetCommandLineArgs().Any(arg => arg.Equals("--media-matrix-only", StringComparison.Ordinal)))
            return MediaMatrixTests.Run();
        var checks = new Dictionary<string, bool>();
        checks["resident_host_absent"] = !Program.StartsResidentHost([])
            && !Program.StartsResidentHost(["--background"])
            && !Program.StartsResidentHost(["--stop"])
            && !Program.OpensWheelOnStartup(["--files", "foto.png"])
            && !Program.OpensPanelOnStartup(["--multi", "--files", "foto.png"]);
        foreach (string option in new[] { "--evidence", "--trigger", "--report" })
        {
            try { Program.OptionValue([option], option); checks["missing_value_" + option] = false; }
            catch (ArgumentException) { checks["missing_value_" + option] = true; }
            try { Program.OptionValue([option, "--ui"], option); checks["flag_as_value_" + option] = false; }
            catch (ArgumentException) { checks["flag_as_value_" + option] = true; }
        }
        checks["files_end_before_following_options"] = Program.FileArguments(["--files", "one.png", "two.png", "--evidence", "out"]).SequenceEqual(new[] { "one.png", "two.png" });
        try { Program.FileArguments(["--files", "--ui"]); checks["empty_file_argument_rejected"] = false; }
        catch (ArgumentException) { checks["empty_file_argument_rejected"] = true; }
        checks["single_png_still_has_jpg"] = Policy.Outputs(["image.png"]).Contains("JPG");
        checks["mixed_mp4_does_not_silently_convert_subset"] = Policy.Outputs(["image.png", "video.mp4"]).Length == 0;
        checks["mixed_payload_explains_blocker"] = Policy.AvailabilityMessage(["image.png", "video.mp4"]).Contains("MP4");
        checks["supported_batch_unchanged"] = Policy.Outputs(["image.png", "image.webp"]).Contains("JPG");
        checks["supported_single_no_error"] = Policy.AvailabilityMessage(["image.png"]) == "";
        var txtOutputs = Policy.Outputs(["note.txt"], true, true);
        checks["txt_outputs_visible_in_wheel"] = txtOutputs.Length == 12 && Policy.OrderFormats(txtOutputs).ToHashSet().SetEquals(txtOutputs);
        checks["txt_native_without_document_tools"] = Policy.Outputs(["note.txt"], false, false).ToHashSet().SetEquals(new[] { "PDF", "PNG", "JPG", "WEBP" });
        checks["documents_without_office_omit_rendering"] = !Policy.Outputs(["note.html"], true, false).Contains("JPG") && Policy.Outputs(["note.html"], true, false).Contains("DOCX");
        checks["pages_not_advertised"] = !Policy.Outputs(["note.txt"], true, true).Contains("PAGES");
        checks["json_outputs_available"] = Policy.Outputs(["dati.json"], false, false, false).ToHashSet().SetEquals(new[] { "YAML", "XML", "CSV", "TXT", "HTML", "MD", "PDF" });
        checks["csv_offers_json"] = Policy.Outputs(["tabella.csv"], false, false, false).Contains("JSON");
        checks["mp3_offers_wav_with_ffmpeg"] = Policy.Outputs(["audio.mp3"], false, false, true).Contains("WAV");
        checks["mp4_offers_video_and_audio_with_ffmpeg"] = Policy.Outputs(["clip.mp4"], false, false, true).ToHashSet().SetEquals(new[] { "MP3", "WAV", "MKV", "WEBM", "AVI", "MOV" });
        var nativeEngines = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        var fullEngines = new ContextMenuCatalog.EngineSnapshot(true, true, true);
        var pngNative = ContextMenuCatalog.ForExtension(".png", nativeEngines);
        checks["context_png_offers_jpg_pdf_webp_native"] = pngNative.Select(e => e.TargetFormat).ToHashSet().SetEquals(new[] { "JPG", "PDF", "WEBP", "BMP", "TIFF", "GIF" })
            && Policy.OrderFormats(Policy.Outputs(["flix.context.png"], false, false, false)).SequenceEqual(pngNative.Select(e => e.TargetFormat));
        checks["context_pdf_outputs_with_pandoc"] = ContextMenuCatalog.ForExtension(".pdf", new ContextMenuCatalog.EngineSnapshot(true, false, false))
            .Select(e => e.TargetFormat).ToHashSet().SetEquals(Policy.Outputs(["flix.context.pdf"], true, false, false).ToHashSet());
        checks["context_docx_outputs_with_document_tools"] = ContextMenuCatalog.ForExtension(".docx", fullEngines)
            .Select(e => e.TargetFormat).ToHashSet().SetEquals(Policy.Outputs(["flix.context.docx"], true, true, true).ToHashSet());
        checks["context_equivalent_extensions_skip_same_format"] =
            !ContextMenuCatalog.ForExtension(".jpeg", nativeEngines).Any(e => e.TargetFormat == "JPG")
            && !ContextMenuCatalog.ForExtension(".jpg", nativeEngines).Any(e => e.TargetFormat == "JPG")
            && !ContextMenuCatalog.ForExtension(".tif", nativeEngines).Any(e => e.TargetFormat == "TIFF")
            && !ContextMenuCatalog.ForExtension(".htm", fullEngines).Any(e => e.TargetFormat == "HTML")
            && !ContextMenuCatalog.ForExtension(".markdown", fullEngines).Any(e => e.TargetFormat == "MD")
            && !ContextMenuCatalog.ForExtension(".yml", nativeEngines).Any(e => e.TargetFormat == "YAML");
        checks["context_all_engine_combinations_match_policy"] = ContextMenuCatalog.EngineSnapshot.AllCombinations().All(engines =>
            ContextMenuCatalog.SupportedSourceExtensions().All(ext =>
            {
                var policy = Policy.OrderFormats(Policy.Outputs(["flix.context" + ext], engines.Pandoc, engines.Office, engines.Ffmpeg));
                var catalog = ContextMenuCatalog.ForExtension(ext, engines).Select(e => e.TargetFormat).ToArray();
                return policy.SequenceEqual(catalog);
            }));
        var contextMenu = ContextMenuCatalog.BuildMenu(fullEngines);
        checks["context_build_omits_extensions_without_conversions"] = !contextMenu.ContainsKey(".dxf") && !contextMenu.ContainsKey(".pages")
            && !ContextMenuCatalog.BuildMenu(nativeEngines).ContainsKey(".mp3")
            && ContextMenuCatalog.ForExtension(".mp3", fullEngines).Length > 0;
        checks["context_no_forbidden_targets"] = contextMenu.Values
            .SelectMany(e => e)
            .All(e => e.TargetFormat is not ("DXF" or "PDF/A" or "PPT" or "PPTX" or "PAGES"));
        checks["context_entry_ids_unique"] = contextMenu.Values.SelectMany(e => e).Select(e => e.Id).Distinct().Count()
            == contextMenu.Values.SelectMany(e => e).Count();
        checks["context_icon_ids_follow_format"] = pngNative.First(e => e.TargetFormat == "JPG").IconId == "format.jpg"
            && pngNative.First(e => e.TargetFormat == "WEBP").IconId == "format.webp";
        checks["context_file_api_matches_extension"] = ContextMenuCatalog.ForFile("sample.png", nativeEngines).Select(e => e.Id)
            .SequenceEqual(pngNative.Select(e => e.Id));
        checks["context_unknown_extension_empty"] = ContextMenuCatalog.ForExtension(".dxf", fullEngines).Length == 0;
        try { ConversionEngine.Convert([], ["PDF"], CancellationToken.None).GetAwaiter().GetResult(); checks["empty_batch_rejected"] = false; } catch (ArgumentException) { checks["empty_batch_rejected"] = true; }
        try { ConversionEngine.Convert(Enumerable.Repeat("file.txt", 129).ToArray(), ["PDF"], CancellationToken.None).GetAwaiter().GetResult(); checks["oversized_batch_rejected"] = false; } catch (ArgumentException) { checks["oversized_batch_rejected"] = true; }
        try { ConversionEngine.Convert(["file.txt"], [], CancellationToken.None).GetAwaiter().GetResult(); checks["empty_output_rejected"] = false; } catch (ArgumentException) { checks["empty_output_rejected"] = true; }
        string priorLog = Evidence.DirectoryPath;
        try
        {
            Evidence.DirectoryPath = Path.Combine(priorLog, "rotation-check");
            Directory.CreateDirectory(Evidence.DirectoryPath);
            string log = Path.Combine(Evidence.DirectoryPath, "events.jsonl");
            File.WriteAllBytes(log, new byte[2 * 1024 * 1024]);
            Evidence.Log("rotation-test");
            checks["diagnostic_logs_bounded"] = File.Exists(log + ".previous") && new FileInfo(log).Length < 1024;
        }
        finally { Evidence.DirectoryPath = priorLog; }
        Directory.CreateDirectory(Evidence.DirectoryPath);
        string cleanupRoot = Path.Combine(Evidence.DirectoryPath, "cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cleanupRoot);
        string cleanupJob = Path.Combine(cleanupRoot, ".flix-job-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cleanupJob);
        string lockFile = Path.Combine(cleanupJob, ".~lock.prepared.pdf#");
        File.WriteAllText(lockFile, "synthetic LibreOffice lock");
        File.SetAttributes(lockFile, FileAttributes.ReadOnly | FileAttributes.Hidden);
        ConversionEngine.DeleteJob(cleanupJob, cleanupRoot);
        checks["readonly_office_lock_cleaned"] = !Directory.Exists(cleanupJob);
        try { ConversionEngine.DeleteJob(cleanupRoot, Path.GetDirectoryName(cleanupRoot)!); checks["unowned_cleanup_refused"] = false; }
        catch (IOException) { checks["unowned_cleanup_refused"] = Directory.Exists(cleanupRoot); }
        Directory.Delete(cleanupRoot);
        AddMenuConversionChecks(checks);
        AddContextMenuPreviewChecks(checks);
        AddContextMenuNextChecks(checks);
        AddContentMenuBlueprintChecks(checks);
        AddContentMenuInstallChecks(checks);
        AddMediaMenuBlueprintChecks(checks);
        AddMediaMenuInstallChecks(checks);
        AddDocumentMenuChecks(checks);
        AddMenuProgressChecks(checks);
        File.WriteAllText(Path.Combine(Evidence.DirectoryPath, "input-tests.json"),
            JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, passed = checks.Count(c => c.Value), failed = checks.Count(c => !c.Value), checks }, new JsonSerializerOptions { WriteIndented = true }));
        return checks.All(c => c.Value) ? 0 : 1;
    }

    static void AddMenuConversionChecks(Dictionary<string, bool> checks)
    {
        string root = Path.Combine(Path.GetTempPath(), "flix-m2-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string priorLog = Evidence.DirectoryPath;
        Evidence.DirectoryPath = Path.Combine(root, "logs");
        try
        {
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            string photo = Path.Combine(root, "foto.png");
            File.WriteAllBytes(photo, png);
            var engines = new ContextMenuCatalog.EngineSnapshot(false, false, false);
            Check(checks, "menu_png_to_jpg", () => PngToJpg(photo, png, engines));
            Check(checks, "menu_format_unavailable", () => FormatUnavailable(root, photo, engines));
            Check(checks, "menu_missing_file", () => MissingFile(root, engines));
            Check(checks, "menu_network_or_relative_rejected", () => DisallowedPaths(engines));
            Check(checks, "menu_incompatible_or_incomplete", () => IncompatibleOptions(photo));
            Check(checks, "menu_busy_rejected", () => BusyBlocks(photo, engines));
            Check(checks, "menu_engine_error", () => EngineError(photo, png, engines));
            Check(checks, "menu_cancelled", () => CancelKeepsOriginal(photo, png, engines));
            Check(checks, "menu_original_not_replaced", () => RejectsOriginalAsOutput(photo, png));
            Check(checks, "menu_does_not_open_wheel_or_host", () => StartupSkipsWheel(photo));
            Check(checks, "menu_direct_launch_no_forward", () => DirectLaunch(photo, engines));
            Check(checks, "menu_payload_revalidated", () => PayloadRevalidated(photo, engines));
            Check(checks, "menu_missing_output_rejected", () => MissingOutputRejected(photo));
            Check(checks, "menu_retired_process_exits", () => RetiredProcessExits());
            Check(checks, "menu_concurrent_processes", () => ConcurrentProcesses(root, png));
        }
        finally
        {
            Evidence.DirectoryPath = priorLog;
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    static void Check(Dictionary<string, bool> checks, string name, Func<bool> test)
    {
        try { checks[name] = test(); }
        catch (Exception ex) { Console.Error.WriteLine(name + ": " + ex); checks[name] = false; }
    }

    static bool PngToJpg(string photo, byte[] original, ContextMenuCatalog.EngineSnapshot engines)
    {
        string kept = Path.Combine(Path.GetDirectoryName(photo)!, "foto.converted.jpg");
        File.WriteAllBytes(kept, new byte[] { 9, 9, 9, 9 });
        bool busy = false;
        CancellationTokenSource? job = null;
        var notes = new List<string>();
        int enteredAt = -1;
        var task = Task.Run(() => MenuConversion.RunExclusive(() => busy, value => busy = value, () => job, value => job = value,
            new MenuConversion.Request("jpg", photo),
            async (path, format, token) =>
            {
                enteredAt = notes.Count;
                var results = await ConversionEngine.Convert(new[] { path }, new[] { format }, token);
                return results[0];
            },
            notes.Add, engines));
        if (!task.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Conversione PNG scaduta.");
        var outcome = task.Result;
        if (!outcome.Succeeded) throw new InvalidOperationException(outcome.Message);
        string produced = outcome.Output!;
        return enteredAt == 1
            && notes[0] == MenuConversion.StartedMessage("JPG")
            && notes[^1] == outcome.Message
            && outcome.Message.Contains(Path.GetFileName(produced), StringComparison.OrdinalIgnoreCase)
            && outcome.Message.Contains(Path.GetDirectoryName(produced)!, StringComparison.Ordinal)
            && File.Exists(produced)
            && new FileInfo(produced).Length > 0
            && !string.Equals(Path.GetFullPath(produced), Path.GetFullPath(photo), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Path.GetFullPath(produced), Path.GetFullPath(kept), StringComparison.OrdinalIgnoreCase)
            && File.ReadAllBytes(photo).SequenceEqual(original)
            && File.ReadAllBytes(kept).SequenceEqual(new byte[] { 9, 9, 9, 9 })
            && !busy && job == null;
    }

    static bool FormatUnavailable(string root, string photo, ContextMenuCatalog.EngineSnapshot engines)
    {
        string mp3 = Path.Combine(root, "audio.mp3");
        File.WriteAllBytes(mp3, new byte[] { 1, 2, 3 });
        string jpg = Path.Combine(root, "gia.jpg");
        File.WriteAllBytes(jpg, new byte[] { 4, 5 });
        bool wav = MenuConversion.TryPrepare(new MenuConversion.Request("WAV", mp3), engines, out _, out string wavError);
        bool docx = MenuConversion.TryPrepare(new MenuConversion.Request("DOCX", photo), engines, out _, out string docxError);
        bool same = MenuConversion.TryPrepare(new MenuConversion.Request("JPG", jpg), engines, out _, out string sameError);
        return !wav && wavError.Contains("non disponibile", StringComparison.Ordinal)
            && !docx && docxError.Contains("DOCX", StringComparison.Ordinal)
            && !same && sameError.Contains("non disponibile", StringComparison.Ordinal);
    }

    static bool MissingFile(string root, ContextMenuCatalog.EngineSnapshot engines)
    {
        string missing = Path.Combine(root, "assente.png");
        bool prepared = MenuConversion.TryPrepare(new MenuConversion.Request("JPG", missing), engines, out _, out string error);
        return !prepared && error.Contains("non trovato", StringComparison.Ordinal);
    }

    static bool DisallowedPaths(ContextMenuCatalog.EngineSnapshot engines)
    {
        bool network = MenuConversion.TryPrepare(new MenuConversion.Request("JPG", @"\\flix-m2\share\foto.png"), engines, out _, out string networkError);
        bool relative = MenuConversion.TryPrepare(new MenuConversion.Request("JPG", "foto.png"), engines, out _, out string relativeError);
        return !network && networkError.Contains("non ammesso", StringComparison.Ordinal)
            && !relative && relativeError.Contains("non ammesso", StringComparison.Ordinal);
    }

    static bool IncompatibleOptions(string photo)
    {
        string[] flags = ["--multi", "--convert", "--ui", "--settings", "--background", "--stop", "--preview-sound", "--set-trigger", "--trigger", "--test-input", "--install-context-menu", "--measure-idle", "--report", "--foo"];
        bool rejected = flags.All(flag => !MenuConversion.TryParse(["--convert-menu", "JPG", "--files", photo, flag], out _, out string error) && error.Contains(flag, StringComparison.Ordinal));
        bool twoFiles = !MenuConversion.TryParse(["--convert-menu", "JPG", "--files", photo, Path.Combine(Path.GetDirectoryName(photo)!, "altro.png")], out _, out string filesError)
            && filesError.Contains("un solo file", StringComparison.Ordinal);
        bool twoFormats = !MenuConversion.TryParse(["--convert-menu", "JPG,PDF", "--files", photo], out _, out string formatError)
            && formatError.Contains("un solo formato", StringComparison.Ordinal);
        bool missingFormat = !MenuConversion.TryParse(["--convert-menu", "--files", photo], out _, out _);
        bool missingFile = !MenuConversion.TryParse(["--convert-menu", "JPG"], out _, out _);
        bool valid = MenuConversion.TryParse(["--convert-menu", "JPG", "--files", photo], out var forward, out _)
            && MenuConversion.TryParse(["--files", photo, "--convert-menu", "jpg"], out var backward, out _)
            && forward.Format == "JPG" && backward.Format == "jpg" && forward.File == photo && backward.File == photo;
        return rejected && twoFiles && twoFormats && missingFormat && missingFile && valid;
    }

    static bool BusyBlocks(string photo, ContextMenuCatalog.EngineSnapshot engines)
    {
        int calls = 0;
        var notes = new List<string>();
        var outcome = MenuConversion.RunExclusive(() => true, _ => { }, () => null, _ => { },
            new MenuConversion.Request("JPG", photo),
            (_, _, _) => { calls++; return Task.FromResult(new ConversionResult(photo, "JPG", [], "non deve partire")); },
            notes.Add, engines).GetAwaiter().GetResult();
        return calls == 0 && outcome.Status == MenuConversion.Status.Busy && outcome.Message == MenuConversion.BusyMessage && !outcome.Succeeded && notes.Count == 1;
    }

    static bool EngineError(string photo, byte[] original, ContextMenuCatalog.EngineSnapshot engines)
    {
        var notes = new List<string>();
        var outcome = Task.Run(() => MenuConversion.RunExclusive(() => false, _ => { }, () => null, _ => { },
            new MenuConversion.Request("JPG", photo),
            (_, _, _) => Task.FromResult(new ConversionResult(photo, "JPG", Array.Empty<string>(), "Encoder non disponibile.")),
            notes.Add, engines)).GetAwaiter().GetResult();
        return !outcome.Succeeded
            && outcome.Status == MenuConversion.Status.Error
            && outcome.Message.Contains("Encoder non disponibile", StringComparison.Ordinal)
            && notes[0] == MenuConversion.StartedMessage("JPG")
            && notes[^1] == outcome.Message
            && !notes.Exists(note => note.StartsWith("Creato ", StringComparison.Ordinal))
            && File.ReadAllBytes(photo).SequenceEqual(original);
    }

    static bool CancelKeepsOriginal(string photo, byte[] original, ContextMenuCatalog.EngineSnapshot engines)
    {
        bool busy = false;
        CancellationTokenSource? job = null;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var notes = new List<string>();
        var task = Task.Run(() => MenuConversion.RunExclusive(() => busy, value => busy = value, () => job, value => job = value,
            new MenuConversion.Request("JPG", photo),
            async (_, _, token) =>
            {
                entered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return new ConversionResult(photo, "JPG", Array.Empty<string>(), "non completata");
            },
            notes.Add, engines));
        if (!entered.Task.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Annullamento non avviato.");
        CancellationTokenSource? cts = job ?? throw new InvalidOperationException("Manca il token di annullamento.");
        cts.Cancel();
        if (!task.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Annullamento non concluso.");
        var outcome = task.Result;
        return outcome.Status == MenuConversion.Status.Cancelled
            && outcome.Message == MenuConversion.CancelledMessage
            && !outcome.Succeeded
            && notes[0] == MenuConversion.StartedMessage("JPG")
            && notes[^1] == MenuConversion.CancelledMessage
            && File.ReadAllBytes(photo).SequenceEqual(original)
            && !busy;
    }

    static bool RejectsOriginalAsOutput(string photo, byte[] original)
    {
        var outcome = MenuConversion.FromResult(photo, new ConversionResult(photo, "JPG", new[] { photo }, null));
        return outcome.Status == MenuConversion.Status.Error && !outcome.Succeeded && File.ReadAllBytes(photo).SequenceEqual(original);
    }

    static bool StartupSkipsWheel(string photo)
    {
        string[] menu = ["--convert-menu", "JPG", "--files", photo];
        return !Program.OpensWheelOnStartup(menu)
            && !Program.OpensPanelOnStartup(menu)
            && !Program.OpensWheelOnStartup(["--files", photo])
            && !Program.OpensWheelOnStartup(["--files", photo, "--multi"])
            && !Program.OpensPanelOnStartup(["--multi", "--files", photo])
            && !Program.OpensWheelOnStartup(["--background"])
            && !Program.OpensPanelOnStartup(["--settings"])
            && !Program.StartsResidentHost(["--stop"])
            && !Program.OpensWheelOnStartup(["--files", photo, "--convert-menu"])
            && !Program.OpensPanelOnStartup(["--files", photo, "--multi", "--convert-menu"])
            && Program.FileArguments(["--files", "one.png", "two.png", "--convert"]).SequenceEqual(["one.png", "two.png"]);
    }

    static bool DirectLaunch(string photo, ContextMenuCatalog.EngineSnapshot engines)
    {
        var ran = new MenuProbe { Engines = engines, RunCode = 0 };
        int code = MenuConversion.Launch(["--convert-menu", "JPG", "--files", photo], ran);
        var invalid = new MenuProbe { Engines = engines, RunCode = 0 };
        int rejected = MenuConversion.Launch(["--convert-menu", "JPG", "--files", photo, "--stop"], invalid);
        var failed = new MenuProbe { Engines = engines, RunCode = 1 };
        int failure = MenuConversion.Launch(["--convert-menu", "jpg", "--files", photo], failed);
        return code == 0 && ran.Runs == 1 && ran.Tells == 0 && ran.Last?.Format == "JPG"
            && string.Equals(ran.Last?.File, Path.GetFullPath(photo), StringComparison.OrdinalIgnoreCase)
            && rejected == 1 && invalid.Runs == 0 && invalid.Tells == 1 && invalid.Told != null && invalid.Told.Contains("--stop", StringComparison.Ordinal)
            && failure == 1 && failed.Runs == 1 && failed.Tells == 0;
    }

    static bool PayloadRevalidated(string photo, ContextMenuCatalog.EngineSnapshot engines)
    {
        int calls = 0;
        var outcome = Task.Run(() => MenuConversion.Execute(new MenuConversion.Request("DOCX", photo), (_, _, _) =>
        {
            calls++;
            return Task.FromResult(new ConversionResult(photo, "DOCX", Array.Empty<string>(), null));
        }, _ => { }, CancellationToken.None, engines)).GetAwaiter().GetResult();
        return calls == 0 && !outcome.Succeeded && outcome.Message.Contains("non disponibile", StringComparison.Ordinal);
    }

    static bool MissingOutputRejected(string photo)
    {
        string missing = Path.Combine(Path.GetDirectoryName(photo)!, "mancante.jpg");
        var outcome = MenuConversion.FromResult(photo, new ConversionResult(photo, "JPG", [missing], null));
        return outcome.Status == MenuConversion.Status.Error && !outcome.Succeeded && !File.Exists(missing);
    }

    static bool RetiredProcessExits()
    {
        string? exe = Environment.ProcessPath;
        if (exe == null || !Path.GetFileName(exe).Equals("Flix.exe", StringComparison.OrdinalIgnoreCase)) return false;
        string[][] invocations =
        [
            [],
            ["--background"],
            ["--stop"],
            ["--ui"],
            ["--settings"],
            ["--preview-wheel"],
            ["--export-sound"],
            ["--enable-startup"],
            ["--disable-startup"],
            ["--install-shortcuts"],
            ["--remove-shortcuts"],
            ["--preview-sound"],
            ["--measure-idle"],
            ["--install-context-menu"],
            ["--remove-context-menu"],
            ["--set-trigger", "DOWN"],
            ["--files", Path.Combine(Path.GetTempPath(), "flix-m6-assente.png")],
            ["--multi", "--files", Path.Combine(Path.GetTempPath(), "flix-m6-assente.png")]
        ];
        foreach (string[] args in invocations)
            if (!ExitsWithoutResident(exe, args)) return false;
        return true;
    }

    static bool ExitsWithoutResident(string exe, string[] args)
    {
        var start = new System.Diagnostics.ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
        };
        foreach (string arg in args) start.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(start);
        if (process == null) return false;
        try
        {
            if (!process.WaitForExit(5000)) return false;
            return process.ExitCode == 0;
        }
        finally
        {
            if (!process.HasExited)
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            }
        }
    }

    static bool ConcurrentProcesses(string root, byte[] png)
    {
        string? exe = Environment.ProcessPath;
        if (exe == null || !Path.GetFileName(exe).Equals("Flix.exe", StringComparison.OrdinalIgnoreCase)) return false;
        string folder = Path.Combine(root, "concurrent");
        Directory.CreateDirectory(folder);
        string photo = Path.Combine(folder, "foto.png");
        string other = Path.Combine(folder, "altro.png");
        string broken = Path.Combine(folder, "rotto.png");
        File.WriteAllBytes(photo, png);
        File.WriteAllBytes(other, png);
        File.WriteAllText(broken, "non sono pixel");
        string sentinel = Path.Combine(folder, "foto.converted.jpg");
        byte[] sentinelBytes = [7, 7, 7, 7];
        File.WriteAllBytes(sentinel, sentinelBytes);
        var sameA = StartConvert(exe, "JPG", photo);
        var sameB = StartConvert(exe, "JPG", photo);
        var pdf = StartConvert(exe, "PDF", photo);
        var different = StartConvert(exe, "JPG", other);
        var failed = StartConvert(exe, "JPG", broken);
        var jobs = new[] { sameA, sameB, pdf, different, failed };
        if (!Task.WaitAll(jobs.Select(job => job.Done).ToArray(), TimeSpan.FromSeconds(60)))
        {
            foreach (var job in jobs) job.Stop();
            return false;
        }
        var jpg = Directory.GetFiles(folder, "foto.converted*.jpg");
        var pdfs = Directory.GetFiles(folder, "foto.converted*.pdf");
        var otherOut = Directory.GetFiles(folder, "altro.converted*.jpg");
        bool distinct = jpg.Length == 3
            && jpg.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 3
            && pdfs.Length == 1
            && otherOut.Length == 1
            && jpg.All(path => new FileInfo(path).Length > 0)
            && new FileInfo(pdfs[0]).Length > 0
            && new FileInfo(otherOut[0]).Length > 0;
        return distinct
            && sameA.Code == 0 && sameB.Code == 0 && pdf.Code == 0 && different.Code == 0 && failed.Code == 1
            && File.ReadAllBytes(photo).AsSpan().SequenceEqual(png)
            && File.ReadAllBytes(other).AsSpan().SequenceEqual(png)
            && File.ReadAllBytes(sentinel).AsSpan().SequenceEqual(sentinelBytes)
            && File.ReadAllText(broken) == "non sono pixel"
            && Directory.GetFiles(folder, "rotto.converted*").Length == 0
            && !Directory.EnumerateDirectories(folder, ".flix-job-*").Any();
    }

    static RunningConvert StartConvert(string exe, string format, string file)
    {
        var start = new System.Diagnostics.ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? ""
        };
        start.ArgumentList.Add("--convert");
        start.ArgumentList.Add(format);
        start.ArgumentList.Add("--files");
        start.ArgumentList.Add(file);
        var process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Processo non avviato.");
        var done = Task.Run(() =>
        {
            if (!process.WaitForExit(55000))
            {
                try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
                return -1;
            }
            return process.ExitCode;
        });
        return new RunningConvert(process, done);
    }

    sealed class RunningConvert
    {
        readonly System.Diagnostics.Process process;
        readonly Task<int> done;
        internal Task Done => done;
        internal int Code => done.IsCompletedSuccessfully ? done.Result : -1;
        internal RunningConvert(System.Diagnostics.Process process, Task<int> done)
        {
            this.process = process;
            this.done = done;
        }
        internal void Stop()
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
        }
    }

    static void AddContextMenuPreviewChecks(Dictionary<string, bool> checks)
    {
        var native = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        string exe = @"C:\Programmi\Altro\Flix.exe";
        string photo = @"C:\My Photos\foto.png";
        Check(checks, "preview_png_formats_jpg_pdf_webp", () => PreviewFormats(native, exe));
        Check(checks, "preview_excludes_other_png_outputs", () => PreviewExcludesOthers(native, exe));
        Check(checks, "preview_quotes_paths_with_spaces", () => PreviewQuotes(native, exe, photo));
        Check(checks, "preview_icons_present_and_valid", () => PreviewIcons(native, exe));
        Check(checks, "preview_original_menu_unchanged", () => PreviewOriginalUntouched(native, exe));
        Check(checks, "preview_install_idempotent", () => PreviewIdempotent(native, exe));
        Check(checks, "preview_remove_owned_keys_only", () => PreviewRemoveOwnedOnly(native, exe));
        Check(checks, "preview_collision_refused", () => PreviewCollisionRefused(native, exe));
        Check(checks, "preview_does_not_open_wheel", () => PreviewDoesNotOpenWheel(photo));
    }

    static ContextMenuPlan PreviewPlan(ContextMenuCatalog.EngineSnapshot engines, string exe) =>
        ContextMenuPreview.CreatePlan(exe, engines);

    static bool PreviewFormats(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var offered = ContextMenuCatalog.ForExtension(".png", engines).Select(entry => entry.TargetFormat).ToHashSet(StringComparer.Ordinal);
        var plan = PreviewPlan(engines, exe);
        bool catalog = new[] { "JPG", "PDF", "WEBP" }.All(offered.Contains);
        bool shape = plan.Formats.SequenceEqual(ContextMenuPreview.Formats)
            && plan.Value(ContextMenuPreview.ParentKey, "MUIVerb") == ContextMenuPreview.ParentVerb
            && plan.Value(ContextMenuPreview.ParentKey, "ExtendedSubCommandsKey") == ContextMenuPreview.SubCommandsPath
            && plan.Value(ContextMenuPreview.ParentKey, "MultiSelectModel") == "Single"
            && plan.Value(ContextMenuPreview.ParentKey, "NeverDefault") == ""
            && plan.Value(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId
            && !plan.Values.Any(value => value.Key.Equals(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0)
            && !plan.Keys.Contains(ContextMenuPreview.ParentKey + @"\command")
            && plan.Keys.All(key => key.StartsWith(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase))
            && plan.Formats.All(format => plan.VerbTitle(format) == "Converti in " + format
                && plan.Value(ContextMenuPreview.VerbKey(format), "MultiSelectModel") == "Single"
                && plan.Value(ContextMenuPreview.VerbKey(format), "NeverDefault") == "");
        bool dllRejected = false;
        try { ContextMenuPreview.CreatePlan(@"C:\Programmi\Altro\Flix.dll", engines); }
        catch (IOException) { dllRejected = true; }
        return catalog && shape && dllRejected && plan.Keys.Count == 8;
    }

    static bool PreviewExcludesOthers(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var offered = ContextMenuCatalog.ForExtension(".png", engines).Select(entry => entry.TargetFormat).ToHashSet(StringComparer.Ordinal);
        var plan = PreviewPlan(engines, exe);
        bool extrasAvailable = new[] { "BMP", "TIFF", "GIF" }.All(offered.Contains);
        bool absent = new[] { "BMP", "TIFF", "GIF", "PNG", "DOCX" }.All(format =>
            !plan.Formats.Contains(format)
            && !plan.Values.Any(value => value.Data.Contains("--convert-menu " + format, StringComparison.Ordinal)));
        bool pngOnly = plan.Keys.All(key => key.StartsWith(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase))
            && !plan.Keys.Any(LegacyIdentity.IsOriginalMenu)
            && !plan.Keys.Any(LegacyIdentity.IsLegacyPreview);
        return extrasAvailable && absent && pngOnly;
    }

    static bool PreviewQuotes(ContextMenuCatalog.EngineSnapshot engines, string exe, string photo)
    {
        var plan = PreviewPlan(engines, exe);
        bool commands = ContextMenuPreview.Formats.All(format =>
        {
            string command = plan.Command(format);
            string expected = "\"" + exe + "\" --convert-menu " + format + " --files \"%1\"";
            return command == expected
                && MenuConversion.TryParse(["--convert-menu", format, "--files", photo], out var request, out _)
                && request.Format == format
                && request.File == photo;
        });
        bool icons = ContextMenuPreview.Formats.All(format =>
        {
            string icon = plan.Icon(format);
            return icon == @"C:\Programmi\Altro\" + ContextMenuPreview.IconFileName(format)
                && icon.IndexOf('"') < 0;
        });
        return commands && icons;
    }

    static bool PreviewIcons(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = PreviewPlan(engines, exe);
        string directory = AppContext.BaseDirectory;
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        foreach (string format in ContextMenuPreview.Formats)
        {
            string path = Path.Combine(directory, ContextMenuPreview.IconFileName(format));
            if (!ContextMenuPreview.IconIsValid(path) || !plan.Icon(format).EndsWith(ContextMenuPreview.IconFileName(format), StringComparison.OrdinalIgnoreCase)) return false;
            hashes.Add(Convert.ToHexString(File.ReadAllBytes(path)));
        }
        string application = Path.Combine(directory, ContextMenuNext.ParentIconFileName);
        if (!ContextMenuPreview.ParentIconIsValid(application)) return false;
        hashes.Add(Convert.ToHexString(File.ReadAllBytes(application)));
        return hashes.Count == 4;
    }

    static bool PreviewOriginalUntouched(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = PreviewPlan(engines, exe);
        if (plan.Keys.Any(key => key.Equals(ContextMenuPreview.OriginalMenuKey, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(ContextMenuPreview.OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase))) return false;
        var store = new MemoryRegistryStore();
        string commandKey = ContextMenuPreview.OriginalMenuKey + @"\command";
        store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
        store.Write(ContextMenuPreview.OriginalMenuKey, "MultiSelectModel", "Single");
        store.Write(ContextMenuPreview.OriginalMenuKey, "Icon", LegacyIdentity.OriginalMenuIcon);
        store.Write(commandKey, "", LegacyIdentity.OriginalMenuProgramFilesCommand);
        string before = store.Export();
        ContextMenuPreview.Install(plan, store);
        bool kept = store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
            && store.Read(ContextMenuPreview.OriginalMenuKey, "MultiSelectModel") == "Single"
            && store.Read(ContextMenuPreview.OriginalMenuKey, "Icon") == LegacyIdentity.OriginalMenuIcon
            && store.Read(commandKey, "") == LegacyIdentity.OriginalMenuProgramFilesCommand;
        ContextMenuPreview.Remove(store);
        return kept
            && store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
            && store.Read(commandKey, "") == LegacyIdentity.OriginalMenuProgramFilesCommand
            && !store.KeyExists(ContextMenuPreview.ParentKey)
            && before.Contains(LegacyIdentity.OriginalMenuVerb, StringComparison.Ordinal);
    }

    static bool PreviewIdempotent(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = PreviewPlan(engines, exe);
        var store = new MemoryRegistryStore();
        ContextMenuPreview.Install(plan, store);
        string once = store.Export();
        ContextMenuPreview.Install(plan, store);
        if (store.Export() != once || store.KeyCount != 8) return false;
        store.DeleteKey(ContextMenuPreview.VerbKey("PDF") + @"\command");
        store.DeleteKey(ContextMenuPreview.VerbKey("PDF"));
        ContextMenuPreview.Install(plan, store);
        if (store.Export() != once) return false;
        ContextMenuPreview.Remove(store);
        ContextMenuPreview.Remove(store);
        if (store.KeyCount != 0) return false;
        ContextMenuPreview.Install(plan, store);
        return store.Export() == once;
    }

    static bool PreviewRemoveOwnedOnly(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = PreviewPlan(engines, exe);
        var store = new MemoryRegistryStore();
        ContextMenuPreview.Install(plan, store);
        string sibling = @"Software\Classes\SystemFileAssociations\.png\shell\OtherTool";
        store.Write(sibling, "MUIVerb", "Altro");
        store.Write(sibling + @"\command", "", "notepad.exe \"%1\"");
        store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
        store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
        string foreign = ContextMenuPreview.ParentKey + @"\shell\custom";
        store.Write(foreign, "MUIVerb", "Esterno");
        string decoy = Path.Combine(Path.GetTempPath(), "flix-m3-decoy-" + Guid.NewGuid().ToString("N") + ".ico");
        File.WriteAllBytes(decoy, [1, 2, 3]);
        try
        {
            ContextMenuPreview.Remove(store);
            bool ownedGone = ContextMenuPreview.Formats.All(format =>
                !store.KeyExists(ContextMenuPreview.VerbKey(format))
                && !store.KeyExists(ContextMenuPreview.VerbKey(format) + @"\command"));
            bool kept = store.Read(sibling, "MUIVerb") == "Altro"
                && store.Read(sibling + @"\command", "") == "notepad.exe \"%1\""
                && store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && store.Read(foreign, "MUIVerb") == "Esterno"
                && store.KeyExists(ContextMenuPreview.ParentKey)
                && store.KeyExists(ContextMenuPreview.ParentKey + @"\shell")
                && File.ReadAllBytes(decoy).SequenceEqual(new byte[] { 1, 2, 3 });
            var clean = new MemoryRegistryStore();
            ContextMenuPreview.Install(plan, clean);
            ContextMenuPreview.Remove(clean);
            var alien = new MemoryRegistryStore();
            alien.Write(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue, "Altro");
            alien.Write(ContextMenuPreview.ParentKey, "MUIVerb", "Non toccare");
            ContextMenuPreview.Remove(alien);
            return ownedGone && kept && clean.KeyCount == 0 && alien.Read(ContextMenuPreview.ParentKey, "MUIVerb") == "Non toccare";
        }
        finally { try { File.Delete(decoy); } catch (IOException) { } }
    }

    static bool PreviewCollisionRefused(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = PreviewPlan(engines, exe);
        var foreignParent = new MemoryRegistryStore();
        foreignParent.Write(ContextMenuPreview.ParentKey, "", "Altro programma");
        foreignParent.Write(ContextMenuPreview.ParentKey, "MUIVerb", "Non FLIX");
        if (!CollisionLeavesStore(plan, foreignParent)) return false;
        var foreignCommand = new MemoryRegistryStore();
        foreignCommand.Write(ContextMenuPreview.VerbKey("JPG") + @"\command", "", "notepad.exe \"%1\"");
        if (!CollisionLeavesStore(plan, foreignCommand)) return false;
        var owned = new MemoryRegistryStore();
        ContextMenuPreview.Install(plan, owned);
        owned.Write(ContextMenuPreview.VerbKey("WEBP") + @"\command", ContextMenuPreview.OwnerValue, "Altro");
        owned.Write(ContextMenuPreview.VerbKey("WEBP") + @"\command", "", "cmd.exe /c echo estraneo");
        return CollisionLeavesStore(plan, owned);
    }

    static bool CollisionLeavesStore(ContextMenuPlan plan, MemoryRegistryStore store)
    {
        string before = store.Export();
        try { ContextMenuPreview.Install(plan, store); return false; }
        catch (IOException) { return store.Export() == before; }
    }

    static bool PreviewDoesNotOpenWheel(string photo)
    {
        bool install = !Program.OpensWheelOnStartup(["--install-context-menu-preview"])
            && !Program.OpensPanelOnStartup(["--install-context-menu-preview"])
            && !Program.OpensWheelOnStartup(["--remove-context-menu-preview"])
            && !Program.OpensPanelOnStartup(["--remove-context-menu-preview"])
            && !Program.OpensWheelOnStartup(["--install-context-menu-preview", "--files", photo])
            && !Program.OpensPanelOnStartup(["--remove-context-menu-preview", "--multi", "--files", photo]);
        bool commands = ContextMenuPreview.Formats.All(format =>
            !Program.OpensWheelOnStartup(["--convert-menu", format, "--files", photo])
            && !Program.OpensPanelOnStartup(["--convert-menu", format, "--multi", "--files", photo]));
        return install && commands
            && !Program.OpensWheelOnStartup(["--files", photo])
            && !Program.OpensWheelOnStartup(["--install-context-menu"]);
    }

    static void AddContextMenuNextChecks(Dictionary<string, bool> checks)
    {
        var native = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        string exe = @"C:\Programmi\Altro\Flix.exe";
        string photo = @"C:\My Photos\foto.png";
        string stable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Flix", "Flix.exe");
        Check(checks, "next_png_six_formats_catalog", () => NextFormats(native, exe));
        Check(checks, "next_parent_without_anteprima", () => NextParentLabel(native, exe));
        Check(checks, "next_quotes_paths_with_spaces", () => NextQuotes(native, exe, photo));
        Check(checks, "next_icons_premium_v2", () => NextIcons(native, exe));
        Check(checks, "next_path_security_bin_obj", () => NextRejectsBinObj(native));
        Check(checks, "next_path_allows_stable_and_lab", () => NextAllowsStableAndLab(native, stable));
        Check(checks, "next_simulated_path_allowed", () => NextSimulatedPath(native, exe));
        Check(checks, "next_original_and_preview_unchanged", () => NextForeignMenusUntouched(native, exe));
        Check(checks, "next_distinct_from_preview_keys", () => NextDistinctFromPreview(native, exe));
        Check(checks, "next_install_idempotent", () => NextIdempotent(native, exe));
        Check(checks, "next_collision_refused", () => NextCollisionRefused(native, exe));
        Check(checks, "next_backup_restore_reversible", () => NextBackupRestore(native, exe));
        Check(checks, "next_does_not_open_wheel", () => NextDoesNotOpenWheel(photo));
        Check(checks, "next_preview_plan_unchanged", () => NextLeavesPreviewPlan(native, exe));
        Check(checks, "next_cli_png_linked", () => NextCliLinked(photo));
        Check(checks, "next_cli_rejects_unapproved_paths", () => NextCliRejectsPaths(native));
        Check(checks, "next_image_blueprints_match_policy", () => NextImageBlueprints(native));
        Check(checks, "next_image_menu_keys_do_not_collide", () => NextImageMenusDoNotCollide(native, exe));
        Check(checks, "next_missing_icons_block_registration", () => NextMissingIconsBlockRegistration(native));
        Check(checks, "next_png_memory_install_and_remove", () => NextPngMemoryInstallAndRemove());
        Check(checks, "next_registration_restores_after_error", () => NextRegistrationRestores(native, exe));
        Check(checks, "next_image_eight_extensions_six_targets", () => NextImageEightExtensions(native));
        Check(checks, "next_image_equivalent_extensions", () => NextEquivalentExtensions(native, exe));
        Check(checks, "next_image_memory_install_remove_idempotent", () => NextImageMemoryLifecycle(native));
        Check(checks, "next_image_foreign_owner_refused", () => NextImageForeignOwnerRefused(native));
        Check(checks, "next_image_missing_and_corrupt_icons", () => NextImageIconsRefused(native));
        Check(checks, "next_image_restore_after_write_error", () => NextImageWriteErrorRestores(native));
        Check(checks, "next_image_remove_error_rolls_back", () => NextImageRemoveErrorRollsBack(native));
        Check(checks, "next_image_remove_restore_is_not_atomic", () => NextImageRemoveRestoreIsNotAtomic(native));
        Check(checks, "next_image_leaves_original_and_preview", () => NextImageLeavesForeignMenus(native));
        Check(checks, "next_image_cli_installs_and_removes", () => NextImageCliInstallsAndRemoves(native));
        Check(checks, "next_image_cli_rejects_conflicts", () => NextImageCliRejectsConflicts(photo));
        Check(checks, "next_image_cli_rejects_paths_and_icons", () => NextImageCliRejectsPathsAndIcons(native));
        Check(checks, "next_image_rollback_access_and_security", () => NextImageRollbackAccessAndSecurity(native));
        Check(checks, "next_image_remove_without_icons", () => NextImageRemoveWithoutIcons(native));
        Check(checks, "next_image_m4_window_unchanged", () => NextImageUsesMenuProgress(native, exe));
        Check(checks, "flix_registry_migration_simulated", () => FlixRegistryMigration(native));
    }

    static void AddContentMenuBlueprintChecks(Dictionary<string, bool> checks)
    {
        var native = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        var pandoc = new ContextMenuCatalog.EngineSnapshot(true, false, false);
        string exe = @"C:\Programmi\Altro\Flix.exe";
        Check(checks, "content_seven_extensions_and_menu_ids", () => ContentSevenExtensions(native));
        Check(checks, "content_native_43_and_pandoc_59", () => ContentDestinationCounts(native, pandoc));
        Check(checks, "content_no_source_identity", () => ContentNoSourceIdentity(native, pandoc));
        Check(checks, "content_yaml_yml_alias", () => ContentYamlAlias(native, pandoc, exe));
        Check(checks, "content_order_matches_policy", () => ContentOrderMatchesPolicy());
        Check(checks, "content_office_ffmpeg_invariant", () => ContentOfficeFfmpegInvariant());
        Check(checks, "content_keys_do_not_collide", () => ContentKeysDoNotCollide(native, pandoc));
        Check(checks, "content_image_plans_unchanged", () => ContentLeavesImagePlans(native, pandoc));
        Check(checks, "content_menu_id_for_txt_unchanged", () => ContentMenuIdForTxtUnchanged());
        Check(checks, "content_icons_named_without_files", () => ContentIconsNamedWithoutFiles(native, pandoc, exe));
        Check(checks, "content_premium_v2_twenty_icons_in_output", () => ContentPremiumV2TwentyIconsInOutput());
        Check(checks, "content_icons_gate_native_pandoc_and_missing", () => ContentIconsGateRegistration(native, pandoc));
        Check(checks, "content_registration_refused", () => ContentRegistrationRefused(native, exe));
        Check(checks, "content_menu_properties", () => ContentMenuProperties(native, pandoc, exe));
    }

    static ContextMenuPlan NextPlan(ContextMenuCatalog.EngineSnapshot engines, string exe) =>
        ContextMenuNext.CreatePlan(exe, engines);

    static bool NextFormats(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var catalogFormats = ContextMenuCatalog.ForExtension(".png", engines).Select(entry => entry.TargetFormat).ToArray();
        var plan = NextPlan(engines, exe);
        bool catalog = plan.Formats.SequenceEqual(catalogFormats)
            && plan.Formats.Count == 6
            && !plan.Formats.Contains("PNG")
            && new[] { "JPG", "PDF", "WEBP", "BMP", "TIFF", "GIF" }.All(plan.Formats.Contains);
        bool titles = plan.Formats.All(format =>
            ContextMenuCatalog.ForExtension(".png", engines).Any(entry => entry.TargetFormat == format && entry.Title == plan.VerbTitle(format)));
        bool shape = plan.Formats.SequenceEqual(ContextMenuNext.Formats)
            && plan.Value(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) == ContextMenuNext.MenuId
            && !plan.Keys.Contains(ContextMenuNext.ParentKey + @"\command")
            && plan.Values.All(value => !(value.Key.Equals(ContextMenuNext.ParentKey, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0))
            && plan.Keys.All(key => key.StartsWith(ContextMenuNext.ParentKey, StringComparison.OrdinalIgnoreCase));
        return catalog && titles && shape && plan.Keys.Count == 2 + plan.Formats.Count * 2;
    }

    static bool NextParentLabel(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = NextPlan(engines, exe);
        var preview = PreviewPlan(engines, exe);
        string verb = plan.Value(ContextMenuNext.ParentKey, "MUIVerb");
        string sub = plan.Value(ContextMenuNext.ParentKey, "ExtendedSubCommandsKey");
        string previewVerb = preview.Value(ContextMenuPreview.ParentKey, "MUIVerb");
        return verb == "Converti con FLIX"
            && sub == ContextMenuNext.SubCommandsPath
            && previewVerb == ContextMenuPreview.ParentVerb;
    }

    static bool NextQuotes(ContextMenuCatalog.EngineSnapshot engines, string exe, string photo)
    {
        var plan = NextPlan(engines, exe);
        bool commands = ContextMenuNext.Formats.All(format =>
        {
            string command = plan.Command(format);
            string expected = "\"" + exe + "\" --convert-menu " + format + " --files \"%1\"";
            return command == expected
                && MenuConversion.TryParse(["--convert-menu", format, "--files", photo], out var request, out _)
                && request.Format == format
                && request.File == photo;
        });
        bool icons = ContextMenuNext.Formats.All(format =>
        {
            string icon = plan.Icon(format);
            return icon == @"C:\Programmi\Altro\" + ContextMenuPreview.IconFileName(format)
                && icon.IndexOf('"') < 0;
        });
        return commands && icons;
    }

    static bool NextIcons(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = NextPlan(engines, exe);
        bool premium = new[] { "JPG", "PDF", "WEBP", "PNG", "BMP", "TIFF", "GIF" }.All(format =>
        {
            string name = ContextMenuNext.IconFileName(format);
            string path = Path.Combine(AppContext.BaseDirectory, name);
            return File.Exists(path) && ContextMenuPreview.IconIsValid(path);
        });
        bool menu = ContextMenuNext.Formats.All(format =>
        {
            string icon = plan.Icon(format);
            return icon.EndsWith(ContextMenuPreview.IconFileName(format), StringComparison.OrdinalIgnoreCase)
                && ContextMenuPreview.IconIsValid(Path.Combine(AppContext.BaseDirectory, ContextMenuPreview.IconFileName(format)));
        });
        return premium && menu;
    }

    static bool NextRejectsBinObj(ContextMenuCatalog.EngineSnapshot engines)
    {
        bool binRejected = false;
        bool objRejected = false;
        try { ContextMenuNext.CreatePlan(@"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe", engines); }
        catch (IOException) { binRejected = true; }
        try { ContextMenuNext.CreatePlan(@"C:\dev\FLIX\src\Flix\obj\Release\net10.0-windows\Flix.exe", engines); }
        catch (IOException) { objRejected = true; }
        return binRejected && objRejected;
    }

    static bool NextAllowsStableAndLab(ContextMenuCatalog.EngineSnapshot engines, string stable)
    {
        var stablePlan = ContextMenuNext.CreatePlan(stable, engines);
        string lab = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "Flix.exe");
        var labPlan = ContextMenuNext.CreatePlan(lab, engines);
        return ContextMenuNext.IsStableInstallPath(stable)
            && ContextMenuNext.IsLaboratoryPath(lab)
            && stablePlan.Formats.Count == 6
            && stablePlan.Keys.Count == 2 + stablePlan.Formats.Count * 2
            && labPlan.Keys.Count == stablePlan.Keys.Count;
    }

    static bool NextSimulatedPath(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = NextPlan(engines, exe);
        return plan.Formats.Count == 6 && plan.Keys.Count == 2 + plan.Formats.Count * 2;
    }

    static bool NextForeignMenusUntouched(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = NextPlan(engines, exe);
        if (plan.Keys.Any(key => key.Equals(ContextMenuPreview.OriginalMenuKey, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(ContextMenuPreview.OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase)
            || key.Equals(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(ContextMenuPreview.ParentKey + @"\", StringComparison.OrdinalIgnoreCase))) return false;
        var store = new MemoryRegistryStore();
        store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
        store.Write(ContextMenuPreview.ParentKey, "MUIVerb", ContextMenuPreview.ParentVerb);
        store.Write(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue, ContextMenuPreview.PrototypeId);
        string beforeOriginal = store.Read(ContextMenuPreview.OriginalMenuKey, "")!;
        string beforePreview = store.Read(ContextMenuPreview.ParentKey, "MUIVerb")!;
        ContextMenuNext.Install(plan, store);
        bool kept = store.Read(ContextMenuPreview.OriginalMenuKey, "") == beforeOriginal
            && store.Read(ContextMenuPreview.ParentKey, "MUIVerb") == beforePreview
            && store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId;
        ContextMenuNext.Remove(store);
        return kept;
    }

    static bool NextDistinctFromPreview(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var next = NextPlan(engines, exe);
        var preview = PreviewPlan(engines, exe);
        bool disjoint = !next.Keys.Any(key => preview.Keys.Contains(key, StringComparer.OrdinalIgnoreCase));
        bool ids = ContextMenuNext.MenuId != ContextMenuPreview.PrototypeId
            && next.Value(ContextMenuNext.ParentKey, "MUIVerb") != preview.Value(ContextMenuPreview.ParentKey, "MUIVerb");
        return disjoint && ids && next.Formats.Count == 6 && next.Keys.Count == 14 && preview.Keys.Count == 8;
    }

    static bool NextIdempotent(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = NextPlan(engines, exe);
        var store = new MemoryRegistryStore();
        ContextMenuNext.Install(plan, store);
        string once = store.Export();
        ContextMenuNext.Install(plan, store);
        return store.Export() == once && store.KeyCount == plan.Keys.Count;
    }

    static bool NextCollisionRefused(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = NextPlan(engines, exe);
        var foreign = new MemoryRegistryStore();
        foreign.Write(ContextMenuNext.ParentKey, "MUIVerb", "Altro");
        string before = foreign.Export();
        try { ContextMenuNext.Install(plan, foreign); return false; }
        catch (IOException) { return foreign.Export() == before; }
    }

    static bool NextBackupRestore(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = NextPlan(engines, exe);
        var store = new MemoryRegistryStore();
        ContextMenuNext.Install(plan, store);
        store.Write(ContextMenuNext.ParentKey, "MUIVerb", "Personalizzato");
        var backup = ContextMenuNext.CaptureBackup(plan, store);
        ContextMenuNext.Install(plan, store);
        bool reset = store.Read(ContextMenuNext.ParentKey, "MUIVerb") == "Converti con FLIX";
        ContextMenuNext.Remove(store);
        ContextMenuNext.RestoreBackup(backup, store);
        bool restored = store.Read(ContextMenuNext.ParentKey, "MUIVerb") == "Personalizzato"
            && store.Read(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) == ContextMenuNext.MenuId;
        return backup.Entries.Count == plan.Keys.Count * 2 && reset && restored;
    }

    static bool NextDoesNotOpenWheel(string photo) =>
        ContextMenuNext.Formats.All(format =>
            !Program.OpensWheelOnStartup(["--convert-menu", format, "--files", photo])
            && !Program.OpensPanelOnStartup(["--convert-menu", format, "--multi", "--files", photo]));

    static bool NextLeavesPreviewPlan(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        string Snapshot(ContextMenuPlan plan) => JsonSerializer.Serialize(
            plan.Values.Select(value => new { value.Key, value.Name, value.Data })
                .OrderBy(value => value.Key, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase));
        string previewBefore = Snapshot(PreviewPlan(engines, exe));
        NextPlan(engines, exe);
        return Snapshot(PreviewPlan(engines, exe)) == previewBefore;
    }

    static bool NextCliLinked(string photo)
    {
        bool commands = ContextMenuNext.IsPlannedCommand(new[] { "--install-context-menu-png" })
            && ContextMenuNext.IsPlannedCommand(new[] { "--remove-context-menu-png" })
            && !ContextMenuNext.IsPlannedCommand(new[] { "--install-context-menu" })
            && !ContextMenuNext.IsPlannedCommand(new[] { "--remove-context-menu" })
            && !ContextMenuNext.IsPlannedCommand(new[] { "--install-context-menu-preview" })
            && !ContextMenuNext.IsPlannedCommand(new[] { "--remove-context-menu-preview" })
            && !ContextMenuNext.IsPlannedCommand(new[] { "--install-context-menu-images" })
            && !ContextMenuNext.IsPlannedCommand(new[] { "--remove-context-menu-images" })
            && !ContextMenuPreview.IsPreviewCommand(new[] { "--install-context-menu-png" });
        bool wheel = !Program.OpensWheelOnStartup(new[] { "--install-context-menu-png" })
            && !Program.OpensPanelOnStartup(new[] { "--install-context-menu-png" })
            && !Program.OpensWheelOnStartup(new[] { "--remove-context-menu-png" })
            && !Program.OpensPanelOnStartup(new[] { "--remove-context-menu-png" })
            && !Program.OpensWheelOnStartup(new[] { "--install-context-menu-png", "--files", photo })
            && !Program.OpensPanelOnStartup(new[] { "--remove-context-menu-png", "--multi", "--files", photo })
            && !Program.OpensWheelOnStartup(new[] { "--files", photo })
            && !Program.OpensWheelOnStartup(new[] { "--install-context-menu" })
            && !Program.OpensWheelOnStartup(new[] { "--install-context-menu-preview" });
        return commands && wheel;
    }

    static bool NextCliRejectsPaths(ContextMenuCatalog.EngineSnapshot engines)
    {
        string[] rejected =
        {
            @"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe",
            @"C:\dev\FLIX\src\Flix\obj\Release\net10.0-windows\Flix.exe",
            Path.Combine(Path.GetTempPath(), "Flix.exe")
        };
        foreach (string path in rejected)
        {
            var store = new TouchRegistry();
            bool installRejected = false;
            bool removeRejected = false;
            try { ContextMenuNext.Apply(false, path, store); }
            catch (IOException) { installRejected = true; }
            try { ContextMenuNext.Apply(true, path, store); }
            catch (IOException) { removeRejected = true; }
            if (!installRejected || !removeRejected || store.Calls != 0) return false;
        }
        string stable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Flix", "Flix.exe");
        string lab = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "Flix.exe");
        return ContextMenuNext.CreatePlan(stable, engines).Formats.SequenceEqual(ContextMenuNext.Formats)
            && ContextMenuNext.CreatePlan(lab, engines).Formats.SequenceEqual(ContextMenuNext.Formats);
    }

    static bool NextImageBlueprints(ContextMenuCatalog.EngineSnapshot engines)
    {
        string[] expected = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff", ".gif" };
        if (!ContextMenuNext.ImageExtensions.SequenceEqual(expected) || !Policy.Images.SequenceEqual(expected)) return false;
        var banned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "PNG", [".jpg"] = "JPG", [".jpeg"] = "JPG", [".webp"] = "WEBP",
            [".bmp"] = "BMP", [".tif"] = "TIFF", [".tiff"] = "TIFF", [".gif"] = "GIF"
        };
        var plans = ContextMenuNext.PlanImageMenus(engines);
        if (plans.Length != expected.Length || !plans.Select(plan => plan.Extension).SequenceEqual(expected)) return false;
        bool textRejected = false;
        try { ContextMenuNext.MenuIdFor(".txt"); }
        catch (IOException) { textRejected = true; }
        if (!textRejected || ContextMenuNext.ParentVerb != "Converti con FLIX" || ContextMenuNext.ParentVerb.Contains("Anteprima", StringComparison.Ordinal)) return false;
        var iconNames = new[] { "JPG", "PDF", "WEBP", "PNG", "BMP", "TIFF", "GIF" }.Select(ContextMenuNext.IconFileName).ToArray();
        if (iconNames.Distinct(StringComparer.OrdinalIgnoreCase).Count() != iconNames.Length) return false;
        if (ContextMenuNext.IconFileName("JPG") != ContextMenuPreview.IconFileName("JPG")
            || ContextMenuNext.IconFileName("PDF") != ContextMenuPreview.IconFileName("PDF")
            || ContextMenuNext.IconFileName("WEBP") != ContextMenuPreview.IconFileName("WEBP")) return false;
        if (ContextMenuNext.IconFileName("PNG") != "flix-menu-png-premium-v2.ico"
            || ContextMenuNext.IconFileName("BMP") != "flix-menu-bmp-premium-v2.ico"
            || ContextMenuNext.IconFileName("TIFF") != "flix-menu-tiff-premium-v2.ico"
            || ContextMenuNext.IconFileName("GIF") != "flix-menu-gif-premium-v2.ico") return false;
        foreach (var plan in plans)
        {
            string[] policy = Policy.OrderFormats(Policy.Outputs(new[] { "flix.context" + plan.Extension }, engines.Pandoc, engines.Office, engines.Ffmpeg));
            string[] catalog = ContextMenuCatalog.ForExtension(plan.Extension, engines).Select(entry => entry.TargetFormat).ToArray();
            if (!plan.Formats.SequenceEqual(policy) || !plan.Formats.SequenceEqual(catalog) || plan.Formats.Count != 6) return false;
            if (plan.Formats.Contains(banned[plan.Extension], StringComparer.Ordinal)) return false;
            if (plan.Entries.Select(entry => entry.Title).SequenceEqual(plan.Formats.Select(format => "Converti in " + format)) == false) return false;
            if (plan.MenuId != ContextMenuNext.MenuIdFor(plan.Extension)) return false;
            if (plan.ParentKey != ContextMenuNext.ParentKeyFor(plan.Extension)) return false;
            if (plan.SubCommandsPath != ContextMenuNext.SubCommandsPathFor(plan.Extension)) return false;
        }
        var png = plans.Single(plan => plan.Extension == ".png");
        return png.MenuId == ContextMenuNext.MenuId
            && png.ParentKey == ContextMenuNext.ParentKey
            && png.SubCommandsPath == ContextMenuNext.SubCommandsPath
            && png.Formats.SequenceEqual(ContextMenuNext.Formats)
            && png.Formats.Contains("BMP")
            && png.Formats.Contains("TIFF")
            && png.Formats.Contains("GIF")
            && !png.Formats.Contains("PNG");
    }

    static bool NextImageMenusDoNotCollide(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plans = ContextMenuNext.PlanImageMenus(engines);
        var roots = plans.Select(plan => plan.ParentKey).ToArray();
        if (roots.Distinct(StringComparer.OrdinalIgnoreCase).Count() != roots.Length) return false;
        if (plans.Select(plan => plan.MenuId).Distinct(StringComparer.Ordinal).Count() != 6) return false;
        if (plans.Single(plan => plan.Extension == ".jpg").MenuId != plans.Single(plan => plan.Extension == ".jpeg").MenuId) return false;
        if (plans.Single(plan => plan.Extension == ".tif").MenuId != plans.Single(plan => plan.Extension == ".tiff").MenuId) return false;
        var cli = NextPlan(engines, exe);
        var pngKeys = ContextMenuNext.PlannedKeys(plans.Single(plan => plan.Extension == ".png")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (cli.Keys.Count != pngKeys.Count || !cli.Keys.All(pngKeys.Contains) || cli.Keys.Contains(ContextMenuNext.ParentKey + @"\command")) return false;
        for (int i = 0; i < plans.Length; i++)
        {
            var keys = ContextMenuNext.PlannedKeys(plans[i]);
            if (keys.Contains(plans[i].ParentKey + @"\command")) return false;
            if (keys.Any(key => key.Equals(ContextMenuPreview.OriginalMenuKey, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(ContextMenuPreview.OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase)
                || key.Equals(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(ContextMenuPreview.ParentKey + @"\", StringComparison.OrdinalIgnoreCase)
                || key.Contains(@"\CLSID", StringComparison.OrdinalIgnoreCase)
                || key.Contains("UserChoice", StringComparison.OrdinalIgnoreCase)
                || key.Equals(@"Software\Classes\" + plans[i].Extension, StringComparison.OrdinalIgnoreCase))) return false;
            if (!keys.All(key => key.StartsWith(@"Software\Classes\SystemFileAssociations\", StringComparison.OrdinalIgnoreCase))) return false;
            for (int j = 0; j < plans.Length; j++)
            {
                if (i == j) continue;
                var other = ContextMenuNext.PlannedKeys(plans[j]).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (keys.Any(other.Contains)) return false;
                if (plans[j].ParentKey.StartsWith(plans[i].ParentKey + @"\", StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (plans[i].Extension != ".png" && keys.Any(key => cli.Keys.Contains(key, StringComparer.OrdinalIgnoreCase))) return false;
        }
        bool destinationIcons = new[] { "jpg", "pdf", "webp", "bmp", "tiff", "gif" }.All(format =>
            cli.Values.Any(value => value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase)
                && value.Data.Contains("flix-menu-" + format + "-premium-v2.ico", StringComparison.OrdinalIgnoreCase)));
        bool sourceIconAbsent = !cli.Values.Any(value => value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase)
            && value.Data.Contains("flix-menu-png-premium", StringComparison.OrdinalIgnoreCase));
        return cli.Value(ContextMenuNext.ParentKey, "NeverDefault") == "" && destinationIcons && sourceIconAbsent;
    }

    static bool NextMissingIconsBlockRegistration(ContextMenuCatalog.EngineSnapshot engines)
    {
        string[] allIconFormats = ["JPG", "PDF", "WEBP", "PNG", "BMP", "TIFF", "GIF"];
        string directory = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "icons-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var fingerprints = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string format in allIconFormats)
            {
                string name = ContextMenuNext.IconFileName(format);
                string source = Path.Combine(AppContext.BaseDirectory, name);
                if (!File.Exists(source) || !ContextMenuPreview.IconIsValid(source)) return false;
                fingerprints[source] = IconFingerprint(source);
                File.Copy(source, Path.Combine(directory, name));
            }
            if (!CopyParentIcon(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var plans = ContextMenuNext.PlanImageMenus(engines);
            foreach (var plan in plans)
            {
                var memory = new MemoryRegistryStore();
                ContextMenuNext.RegisterImageMenu(plan, exe, memory);
                if (!memory.KeyExists(plan.ParentKey) || memory.KeyCount == 0) return false;
            }
            var pngStore = new MemoryRegistryStore();
            ContextMenuNext.Apply(false, exe, pngStore);
            if (!pngStore.KeyExists(ContextMenuNext.ParentKey)) return false;

            string missingDir = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "missing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(missingDir);
            try
            {
                foreach (string format in new[] { "JPG", "PDF", "WEBP", "PNG", "BMP", "TIFF" })
                {
                    string name = ContextMenuNext.IconFileName(format);
                    File.Copy(Path.Combine(AppContext.BaseDirectory, name), Path.Combine(missingDir, name));
                }
                string missingExe = Path.Combine(missingDir, "Flix.exe");
                var untouched = new TouchRegistry();
                bool refused = false;
                try { ContextMenuNext.RegisterImageMenu(plans[0], missingExe, untouched); }
                catch (IOException) { refused = true; }
                if (!refused || untouched.Calls != 0) return false;
                var missingMemory = new MemoryRegistryStore();
                try { ContextMenuNext.RegisterImageMenu(plans[0], missingExe, missingMemory); }
                catch (IOException) { }
                if (missingMemory.KeyCount != 0) return false;
                string empty = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "empty-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(empty);
                try
                {
                    var emptyBlocked = new TouchRegistry();
                    bool emptyRefused = false;
                    try { ContextMenuNext.Apply(false, Path.Combine(empty, "Flix.exe"), emptyBlocked); }
                    catch (IOException) { emptyRefused = true; }
                    if (!emptyRefused || emptyBlocked.Calls != 0) return false;
                }
                finally { try { Directory.Delete(empty, true); } catch (IOException) { } }
            }
            finally { try { Directory.Delete(missingDir, true); } catch (IOException) { } }

            string invalidDir = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "invalid-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(invalidDir);
            try
            {
                foreach (string format in allIconFormats)
                {
                    string name = ContextMenuNext.IconFileName(format);
                    string dest = Path.Combine(invalidDir, name);
                    if (format == "BMP")
                        File.WriteAllBytes(dest, [1, 2, 3, 4]);
                    else
                        File.Copy(Path.Combine(AppContext.BaseDirectory, name), dest);
                }
                string invalidExe = Path.Combine(invalidDir, "Flix.exe");
                var invalidMemory = new MemoryRegistryStore();
                bool invalidRefused = false;
                try { ContextMenuNext.RegisterImageMenu(plans.Single(plan => plan.Extension == ".png"), invalidExe, invalidMemory); }
                catch (IOException) { invalidRefused = true; }
                if (!invalidRefused || invalidMemory.KeyCount != 0) return false;
            }
            finally { try { Directory.Delete(invalidDir, true); } catch (IOException) { } }

            foreach (var pair in fingerprints)
                if (IconFingerprint(pair.Key) != pair.Value) return false;
            return true;
        }
        finally { try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch (IOException) { } }
    }

    static bool NextPngMemoryInstallAndRemove()
    {
        string directory = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "menu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string format in ContextMenuNext.Formats)
            {
                string name = ContextMenuPreview.IconFileName(format);
                string source = Path.Combine(AppContext.BaseDirectory, name);
                if (!ContextMenuPreview.IconIsValid(source)) return false;
                File.Copy(source, Path.Combine(directory, name));
            }
            if (!CopyParentIcon(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var store = new MemoryRegistryStore();
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            store.Write(ContextMenuPreview.ParentKey, "MUIVerb", ContextMenuPreview.ParentVerb);
            store.Write(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue, ContextMenuPreview.PrototypeId);
            string jpg = ContextMenuNext.ParentKeyFor(".jpg");
            store.Write(jpg, ContextMenuNext.OwnerValue, ContextMenuNext.MenuIdFor(".jpg"));
            store.Write(jpg, "MUIVerb", LegacyIdentity.OriginalMenuVerb);
            string beforeForeign = store.Export();
            ContextMenuNext.Apply(false, exe, store);
            bool installed = store.KeyExists(ContextMenuNext.ParentKey)
                && store.Read(ContextMenuNext.ParentKey, "MUIVerb") == "Converti con FLIX"
                && store.Read(ContextMenuNext.ParentKey, "NeverDefault") == ""
                && store.Read(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) == ContextMenuNext.MenuId
                && !store.KeyExists(ContextMenuNext.ParentKey + @"\command")
                && ContextMenuNext.Formats.All(format =>
                {
                    string command = store.Read(ContextMenuNext.VerbKey(format) + @"\command", "") ?? "";
                    string icon = store.Read(ContextMenuNext.VerbKey(format), "Icon") ?? "";
                    return command.Contains("--convert-menu " + format, StringComparison.Ordinal)
                        && icon.EndsWith(ContextMenuPreview.IconFileName(format), StringComparison.OrdinalIgnoreCase);
                });
            bool foreignKept = store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") == LegacyIdentity.OriginalMenuCommand
                && store.Read(ContextMenuPreview.ParentKey, "MUIVerb") == ContextMenuPreview.ParentVerb
                && store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId
                && store.Read(jpg, ContextMenuNext.OwnerValue) == ContextMenuNext.MenuIdFor(".jpg")
                && store.Read(jpg, "MUIVerb") == LegacyIdentity.OriginalMenuVerb;
            ContextMenuNext.Apply(true, exe, store);
            ContextMenuNext.Apply(true, exe, store);
            bool removed = !store.KeyExists(ContextMenuNext.ParentKey)
                && !store.KeyExists(ContextMenuNext.ParentKey + @"\shell")
                && ContextMenuNext.Formats.All(format =>
                    !store.KeyExists(ContextMenuNext.VerbKey(format))
                    && !store.KeyExists(ContextMenuNext.VerbKey(format) + @"\command"))
                && store.Export() == beforeForeign;
            return installed && foreignKept && removed;
        }
        finally { try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch (IOException) { } }
    }

    static bool NextRegistrationRestores(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var plan = NextPlan(engines, exe);
        var empty = new FailAfterWriteStore { FailOn = 4 };
        empty.Inner.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
        string before = empty.Inner.Export();
        bool threw = false;
        try { ContextMenuNext.Install(plan, empty); }
        catch (IOException ex) { threw = ex.Message.Contains("errore simulato", StringComparison.Ordinal); }
        bool clean = threw
            && empty.Inner.Export() == before
            && !empty.Inner.KeyExists(ContextMenuNext.ParentKey);
        ContextMenuNext.Install(plan, empty);
        bool repeated = empty.Inner.KeyExists(ContextMenuNext.ParentKey);
        ContextMenuNext.Remove(empty);
        bool removed = !empty.Inner.KeyExists(ContextMenuNext.ParentKey)
            && empty.Inner.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb;
        var ready = new MemoryRegistryStore();
        ContextMenuNext.Install(plan, ready);
        ready.Write(ContextMenuNext.ParentKey, "MUIVerb", "Personalizzato");
        string jpg = ContextMenuNext.ParentKeyFor(".jpg");
        ready.Write(jpg, ContextMenuNext.OwnerValue, ContextMenuNext.MenuIdFor(".jpg"));
        string customized = ready.Export();
        var interrupted = new FailAfterWriteStore(ready) { FailOn = 2 };
        bool interruptedThrow = false;
        try { ContextMenuNext.Install(plan, interrupted); }
        catch (IOException ex) { interruptedThrow = ex.Message.Contains("errore simulato", StringComparison.Ordinal); }
        return clean && repeated && removed && interruptedThrow && ready.Export() == customized;
    }

    static bool NextImageEightExtensions(ContextMenuCatalog.EngineSnapshot engines)
    {
        var rich = new ContextMenuCatalog.EngineSnapshot(true, true, true);
        var plans = ContextMenuNext.PlanImageMenus(engines);
        var again = ContextMenuNext.PlanImageMenus(rich);
        if (plans.Length != 8 || !plans.Select(plan => plan.Extension).SequenceEqual(Policy.Images)) return false;
        for (int index = 0; index < plans.Length; index++)
        {
            var plan = plans[index];
            string[] catalog = ContextMenuCatalog.ForExtension(plan.Extension, engines).Select(entry => entry.TargetFormat).ToArray();
            string[] policy = Policy.OrderFormats(Policy.Outputs(new[] { "flix.context" + plan.Extension }, engines.Pandoc, engines.Office, engines.Ffmpeg));
            if (plan.Formats.Count != 6 || !plan.Formats.SequenceEqual(catalog) || !plan.Formats.SequenceEqual(policy)) return false;
            if (!plan.Formats.SequenceEqual(again[index].Formats)) return false;
            if (!plan.Entries.Select(entry => entry.Title).SequenceEqual(plan.Formats.Select(format => "Converti in " + format))) return false;
            if (plan.Entries.Select(entry => entry.TargetFormat).Distinct(StringComparer.Ordinal).Count() != 6) return false;
        }
        var png = plans.Single(plan => plan.Extension == ".png");
        return png.Formats.SequenceEqual(new[] { "PDF", "JPG", "WEBP", "BMP", "TIFF", "GIF" })
            && plans.Single(plan => plan.Extension == ".jpg").Formats.SequenceEqual(new[] { "PDF", "PNG", "WEBP", "BMP", "TIFF", "GIF" })
            && plans.Single(plan => plan.Extension == ".webp").Formats.SequenceEqual(new[] { "PDF", "JPG", "PNG", "BMP", "TIFF", "GIF" })
            && plans.Single(plan => plan.Extension == ".bmp").Formats.SequenceEqual(new[] { "PDF", "JPG", "PNG", "WEBP", "TIFF", "GIF" })
            && plans.Single(plan => plan.Extension == ".gif").Formats.SequenceEqual(new[] { "PDF", "JPG", "PNG", "WEBP", "BMP", "TIFF" });
    }

    static bool NextEquivalentExtensions(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        var blueprints = ContextMenuNext.PlanImageMenus(engines);
        var plans = ContextMenuNext.CreateImagePlans(exe, engines);
        ImageMenuBlueprint Of(string extension) => blueprints.Single(plan => plan.Extension == extension);
        var jpg = Of(".jpg");
        var jpeg = Of(".jpeg");
        var tif = Of(".tif");
        var tiff = Of(".tiff");
        if (jpg.SharesCommandTree || tif.SharesCommandTree || !jpeg.SharesCommandTree || !tiff.SharesCommandTree) return false;
        if (jpg.MenuId != "Flix.Convert.Jpg" || jpeg.MenuId != jpg.MenuId || jpeg.MenuId != ContextMenuNext.MenuIdFor(".jpeg")) return false;
        if (tif.MenuId != "Flix.Convert.Tif" || tiff.MenuId != tif.MenuId) return false;
        if (!jpg.Formats.SequenceEqual(jpeg.Formats) || !tif.Formats.SequenceEqual(tiff.Formats)) return false;
        if (jpg.Formats.Contains("JPG") || jpeg.Formats.Contains("JPG") || jpg.Formats.Contains("JPEG") || tif.Formats.Contains("TIFF") || tif.Formats.Contains("TIF") || tiff.Formats.Contains("TIFF")) return false;
        if (jpeg.SubCommandsPath != jpg.SubCommandsPath || tiff.SubCommandsPath != tif.SubCommandsPath) return false;
        if (jpeg.CommandParentKey != jpg.ParentKey || tiff.CommandParentKey != tif.ParentKey) return false;
        if (!jpeg.ParentKey.Contains(@"\.jpeg\shell\Flix.Convert.Jpg", StringComparison.Ordinal)) return false;
        if (!tiff.ParentKey.Contains(@"\.tiff\shell\Flix.Convert.Tif", StringComparison.Ordinal)) return false;
        var jpegKeys = ContextMenuNext.PlannedKeys(jpeg);
        var tiffKeys = ContextMenuNext.PlannedKeys(tiff);
        var jpgKeys = ContextMenuNext.PlannedKeys(jpg).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tifKeys = ContextMenuNext.PlannedKeys(tif).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (jpegKeys.Count != 1 || tiffKeys.Count != 1 || jpegKeys[0] != jpeg.ParentKey || tiffKeys[0] != tiff.ParentKey) return false;
        if (jpegKeys.Any(jpgKeys.Contains) || tiffKeys.Any(tifKeys.Contains)) return false;
        if (jpgKeys.Count != 14 || tifKeys.Count != 14) return false;
        bool verbs = plans.SelectMany(plan => plan.Keys).All(key =>
            !key.EndsWith(@"\jpeg", StringComparison.OrdinalIgnoreCase)
            && !key.EndsWith(@"\tif", StringComparison.OrdinalIgnoreCase)
            && !key.Contains(@"\shell\jpeg\", StringComparison.OrdinalIgnoreCase)
            && !key.Contains(@"\shell\tif\", StringComparison.OrdinalIgnoreCase));
        var jpegPlan = plans.Single(plan => plan.Keys.Count == 1 && plan.Keys[0] == jpeg.ParentKey);
        bool pointer = jpegPlan.Value(jpeg.ParentKey, "ExtendedSubCommandsKey") == jpg.SubCommandsPath
            && jpegPlan.Value(jpeg.ParentKey, "MUIVerb") == "Converti con FLIX"
            && jpegPlan.Value(jpeg.ParentKey, "Icon").EndsWith(ContextMenuNext.ParentIconFileName, StringComparison.OrdinalIgnoreCase)
            && !jpegPlan.Values.Any(value => value.Name.Length == 0)
            && !jpegPlan.Keys.Contains(jpeg.ParentKey + @"\command")
            && !jpegPlan.Keys.Contains(jpeg.ParentKey + @"\shell");
        bool commands = plans.Where(plan => plan.Keys.Count > 1).All(plan =>
            plan.Formats.All(format => plan.Command(format) == "\"" + exe + "\" --convert-menu " + format + " --files \"%1\""));
        return verbs && pointer && commands && jpg.Formats.Contains("PNG") && tif.Formats.Contains("GIF") && !tif.Formats.Contains("TIFF");
    }

    static bool NextImageMemoryLifecycle(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("menu");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var blueprints = ContextMenuNext.PlanImageMenus(engines);
            var plans = ContextMenuNext.CreateImagePlans(exe, engines);
            var store = new MemoryRegistryStore();
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            store.Write(ContextMenuPreview.ParentKey, "MUIVerb", ContextMenuPreview.ParentVerb);
            store.Write(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue, ContextMenuPreview.PrototypeId);
            string sibling = @"Software\Classes\SystemFileAssociations\.png\shell\OtherTool";
            store.Write(sibling, "MUIVerb", "Altro");
            int foreign = store.KeyCount;
            ContextMenuNext.InstallImageMenus(exe, engines, store);
            if (store.KeyCount != foreign + plans.Sum(plan => plan.Keys.Count)) return false;
            foreach (var blueprint in blueprints)
            {
                if (store.Read(blueprint.ParentKey, "MUIVerb") != "Converti con FLIX") return false;
                if (store.Read(blueprint.ParentKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                if (store.Read(blueprint.ParentKey, "") != null) return false;
                if (store.KeyExists(blueprint.ParentKey + @"\command")) return false;
                if (blueprint.SharesCommandTree)
                {
                    if (store.KeyExists(blueprint.ParentKey + @"\shell")) return false;
                    if (store.Read(blueprint.ParentKey, "ExtendedSubCommandsKey") != blueprint.SubCommandsPath) return false;
                    continue;
                }
                foreach (string format in blueprint.Formats)
                {
                    string verb = blueprint.ParentKey + @"\shell\" + format.ToLowerInvariant();
                    string command = store.Read(verb + @"\command", "") ?? "";
                    string icon = store.Read(verb, "Icon") ?? "";
                    if (command != "\"" + exe + "\" --convert-menu " + format + " --files \"%1\"") return false;
                    if (!icon.EndsWith(ContextMenuNext.IconFileName(format), StringComparison.OrdinalIgnoreCase)) return false;
                    if (!MenuConversion.TryParse(new[] { "--convert-menu", format, "--files", @"C:\My Photos\foto.png" }, out var request, out _)) return false;
                    if (request.Format != format) return false;
                }
            }
            string once = store.Export();
            ContextMenuNext.InstallImageMenus(exe, engines, store);
            if (store.Export() != once) return false;
            string decoy = ContextMenuNext.ParentKey + @"\shell\custom";
            store.Write(decoy, "MUIVerb", "Esterno");
            store.Write(decoy, ContextMenuNext.OwnerValue, "Altro");
            ContextMenuNext.RemoveImageMenus(store);
            ContextMenuNext.RemoveImageMenus(store);
            bool ownedGone = blueprints.Where(plan => plan.Extension != ".png").All(plan => !store.KeyExists(plan.ParentKey))
                && !store.KeyExists(ContextMenuNext.VerbKey("PDF"))
                && store.KeyExists(ContextMenuNext.ParentKey)
                && store.KeyExists(decoy)
                && store.Read(decoy, "MUIVerb") == "Esterno"
                && store.Read(sibling, "MUIVerb") == "Altro"
                && store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") == LegacyIdentity.OriginalMenuCommand
                && store.Read(ContextMenuPreview.ParentKey, "MUIVerb") == ContextMenuPreview.ParentVerb
                && store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId;
            var clean = new MemoryRegistryStore();
            ContextMenuNext.InstallImageMenus(exe, engines, clean);
            int installed = clean.KeyCount;
            ContextMenuNext.RemoveImageMenus(clean);
            ContextMenuNext.RemoveImageMenus(clean);
            return ownedGone && installed == plans.Sum(plan => plan.Keys.Count) && clean.KeyCount == 0;
        }
        finally { DeleteLab(directory); }
    }

    static bool NextImageForeignOwnerRefused(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("foreign");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var alias = new MemoryRegistryStore();
            alias.Write(ContextMenuNext.ParentKeyFor(".jpeg"), ContextMenuNext.OwnerValue, "Altro");
            alias.Write(ContextMenuNext.ParentKeyFor(".jpeg"), "MUIVerb", "Non toccare");
            if (!ImageInstallLeavesStore(exe, engines, alias)) return false;
            var command = new MemoryRegistryStore();
            string verb = ContextMenuNext.ParentKeyFor(".bmp") + @"\shell\png\command";
            command.Write(verb, "", "notepad.exe \"%1\"");
            return ImageInstallLeavesStore(exe, engines, command);
        }
        finally { DeleteLab(directory); }
    }

    static bool ImageInstallLeavesStore(string exe, ContextMenuCatalog.EngineSnapshot engines, MemoryRegistryStore store)
    {
        string before = store.Export();
        try { ContextMenuNext.InstallImageMenus(exe, engines, store); return false; }
        catch (IOException) { return store.Export() == before; }
    }

    static bool NextImageIconsRefused(ContextMenuCatalog.EngineSnapshot engines)
    {
        string missing = ImageLab("missing");
        string corrupt = ImageLab("corrupt");
        try
        {
            if (!CopyPremiumIcons(missing, omit: "GIF")) return false;
            if (!CopyPremiumIcons(corrupt, corrupt: "PNG")) return false;
            var missingStore = new TouchRegistry();
            bool missingRefused = false;
            try { ContextMenuNext.InstallImageMenus(Path.Combine(missing, "Flix.exe"), engines, missingStore); }
            catch (IOException) { missingRefused = true; }
            var corruptStore = new TouchRegistry();
            bool corruptRefused = false;
            try { ContextMenuNext.InstallImageMenus(Path.Combine(corrupt, "Flix.exe"), engines, corruptStore); }
            catch (IOException) { corruptRefused = true; }
            var bin = new TouchRegistry();
            bool binRefused = false;
            try { ContextMenuNext.InstallImageMenus(@"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe", engines, bin); }
            catch (IOException) { binRefused = true; }
            return missingRefused && corruptRefused && binRefused
                && missingStore.Calls == 0 && corruptStore.Calls == 0 && bin.Calls == 0;
        }
        finally
        {
            DeleteLab(missing);
            DeleteLab(corrupt);
        }
    }

    static bool NextImageWriteErrorRestores(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("write");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var store = new FailAfterWriteStore { FailOn = 4 };
            store.Inner.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Inner.Write(ContextMenuPreview.ParentKey, "MUIVerb", ContextMenuPreview.ParentVerb);
            string before = store.Inner.Export();
            bool threw = false;
            try { ContextMenuNext.InstallImageMenus(exe, engines, store); }
            catch (IOException ex) { threw = ex.Message.Contains("errore simulato", StringComparison.Ordinal); }
            return threw && store.Inner.Export() == before && !store.Inner.KeyExists(ContextMenuNext.ParentKey);
        }
        finally { DeleteLab(directory); }
    }

    static bool NextImageRemoveErrorRollsBack(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("remove");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            var installed = new MemoryRegistryStore();
            ContextMenuNext.InstallImageMenus(Path.Combine(directory, "Flix.exe"), engines, installed);
            installed.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string before = installed.Export();
            var interrupted = new FailAfterDeleteStore(installed) { FailOn = 1 };
            bool threw = false;
            try { ContextMenuNext.RemoveImageMenus(interrupted); }
            catch (IOException ex)
            {
                threw = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("non è atomica", StringComparison.Ordinal);
            }
            return threw && installed.Export() == before
                && installed.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb;
        }
        finally { DeleteLab(directory); }
    }

    static bool NextImageRemoveRestoreIsNotAtomic(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("atomic");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            var installed = new MemoryRegistryStore();
            ContextMenuNext.InstallImageMenus(Path.Combine(directory, "Flix.exe"), engines, installed);
            installed.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            installed.Write(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue, ContextMenuPreview.PrototypeId);
            var interrupted = new FailAfterDeleteStore(installed) { FailOn = 1, FailRestore = true };
            bool threw = false;
            try { ContextMenuNext.RemoveImageMenus(interrupted); }
            catch (IOException ex)
            {
                threw = ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                    && ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && ex.Message.Contains("ripristino simulato", StringComparison.Ordinal)
                    && ex.InnerException is IOException inner
                    && inner.Message.Contains("errore simulato", StringComparison.Ordinal);
            }
            return threw
                && installed.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && installed.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId
                && installed.KeyExists(ContextMenuNext.ParentKey);
        }
        finally { DeleteLab(directory); }
    }

    static bool NextImageLeavesForeignMenus(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("foreign-menus");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            string previewBefore = JsonSerializer.Serialize(PreviewPlan(engines, exe).Values.Select(value => new { value.Key, value.Name, value.Data }));
            var plans = ContextMenuNext.CreateImagePlans(exe, engines);
            if (plans.SelectMany(plan => plan.Keys).Any(key =>
                key.Equals(ContextMenuPreview.OriginalMenuKey, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(ContextMenuPreview.OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase)
                || key.Equals(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase)
                || key.StartsWith(ContextMenuPreview.ParentKey + @"\", StringComparison.OrdinalIgnoreCase)
                || key.Contains(@"\CLSID", StringComparison.OrdinalIgnoreCase))) return false;
            string previewAfter = JsonSerializer.Serialize(PreviewPlan(engines, exe).Values.Select(value => new { value.Key, value.Name, value.Data }));
            var store = new MemoryRegistryStore();
            ContextMenuPreview.Install(PreviewPlan(engines, exe), store);
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string before = store.Export();
            ContextMenuNext.InstallImageMenus(exe, engines, store);
            ContextMenuNext.RemoveImageMenus(store);
            return previewBefore == previewAfter && store.Export() == before;
        }
        finally { DeleteLab(directory); }
    }

    static int RunImageCommand(string[] args, string exe, IRegistryStore store, ContextMenuCatalog.EngineSnapshot engines, out string error)
    {
        var buffer = new StringWriter();
        int code = ContextMenuNext.ApplyImages(args, exe, store, engines, buffer);
        error = buffer.ToString();
        return code;
    }

    static bool NextImageCliInstallsAndRemoves(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("cli");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var blueprints = ContextMenuNext.PlanImageMenus(engines);
            var store = new MemoryRegistryStore();
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            store.Write(ContextMenuPreview.ParentKey, "MUIVerb", ContextMenuPreview.ParentVerb);
            store.Write(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue, ContextMenuPreview.PrototypeId);
            string sibling = @"Software\Classes\SystemFileAssociations\.png\shell\OtherTool";
            store.Write(sibling, "MUIVerb", "Altro");
            string beforeForeign = store.Export();
            int installed = RunImageCommand(new[] { "--install-context-menu-images" }, exe, store, engines, out string installError);
            if (installed != 0 || installError.Length != 0) return false;
            if (store.Export() == beforeForeign) return false;
            foreach (var blueprint in blueprints)
            {
                if (store.Read(blueprint.ParentKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                if (store.Read(blueprint.ParentKey, "") != null) return false;
                if (store.Read(blueprint.ParentKey, "NeverDefault") != "") return false;
                if (store.KeyExists(blueprint.ParentKey + @"\command")) return false;
            }
            var jpeg = blueprints.Single(plan => plan.Extension == ".jpeg");
            var jpg = blueprints.Single(plan => plan.Extension == ".jpg");
            var tiff = blueprints.Single(plan => plan.Extension == ".tiff");
            var tif = blueprints.Single(plan => plan.Extension == ".tif");
            if (store.Read(jpeg.ParentKey, "ExtendedSubCommandsKey") != jpg.SubCommandsPath) return false;
            if (store.Read(tiff.ParentKey, "ExtendedSubCommandsKey") != tif.SubCommandsPath) return false;
            if (store.KeyExists(jpeg.ParentKey + @"\shell") || store.KeyExists(tiff.ParentKey + @"\command")) return false;
            string once = store.Export();
            int again = RunImageCommand(new[] { "--install-context-menu-images", "--install-context-menu-images" }, exe, store, engines, out string againError);
            if (again != 0 || againError.Length != 0 || store.Export() != once) return false;
            int removed = RunImageCommand(new[] { "--remove-context-menu-images" }, exe, store, engines, out string removeError);
            int removedAgain = RunImageCommand(new[] { "--remove-context-menu-images" }, exe, store, engines, out string removeAgainError);
            return removed == 0 && removeError.Length == 0
                && removedAgain == 0 && removeAgainError.Length == 0
                && store.Export() == beforeForeign
                && store.Read(sibling, "MUIVerb") == "Altro"
                && store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId
                && !store.KeyExists(jpg.ParentKey)
                && !store.KeyExists(jpeg.ParentKey);
        }
        finally { DeleteLab(directory); }
    }

    static bool NextImageCliRejectsConflicts(string photo)
    {
        var engines = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        string[] extras =
        {
            "--install-context-menu-png", "--remove-context-menu-png",
            "--install-context-menu-preview", "--remove-context-menu-preview",
            "--install-context-menu", "--remove-context-menu",
            "--convert-menu", "--convert", "--test-input", "--test-conversions",
            "--files", "--multi", "--settings", "--background", "--stop",
            "--enable-startup", "--disable-startup", "--preview-menu-progress",
            "--preview-wheel", "--export-sound", "--ui", "--set-trigger", "foto.png"
        };
        var store = new TouchRegistry();
        int both = RunImageCommand(
            new[] { "--install-context-menu-images", "--remove-context-menu-images" },
            @"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe",
            store, engines, out string bothError);
        if (both == 0 || store.Calls != 0 || !bothError.Contains("Nessuna modifica al Registro", StringComparison.Ordinal)) return false;
        foreach (string extra in extras)
        {
            int install = RunImageCommand(new[] { "--install-context-menu-images", extra }, @"C:\Programmi\Altro\Flix.exe", store, engines, out string installError);
            int remove = RunImageCommand(new[] { extra, "--remove-context-menu-images" }, @"C:\Programmi\Altro\Flix.exe", store, engines, out string removeError);
            if (install == 0 || remove == 0 || store.Calls != 0) return false;
            if (!installError.Contains(extra, StringComparison.Ordinal) || !installError.Contains("Nessuna modifica al Registro", StringComparison.Ordinal)) return false;
            if (!removeError.Contains(extra, StringComparison.Ordinal) || removeError.Contains("garantito", StringComparison.OrdinalIgnoreCase)) return false;
            if (Program.OpensWheelOnStartup(new[] { "--install-context-menu-images", extra, "--files", photo })) return false;
            if (Program.OpensPanelOnStartup(new[] { "--remove-context-menu-images", "--multi", "--files", photo, extra })) return false;
        }
        return store.Calls == 0
            && ContextMenuNext.IsPlannedCommand(new[] { "--install-context-menu-png" })
            && ContextMenuPreview.IsPreviewCommand(new[] { "--install-context-menu-preview" })
            && !ContextMenuNext.IsImageMenuCommand(new[] { "--install-context-menu-png" })
            && !Program.OpensWheelOnStartup(new[] { "--install-context-menu-images" })
            && !Program.OpensPanelOnStartup(new[] { "--remove-context-menu-images" })
            && !Program.OpensWheelOnStartup(new[] { "--install-context-menu-images", "--files", photo })
            && !Program.OpensPanelOnStartup(new[] { "--remove-context-menu-images", "--multi", "--files", photo })
            && !Program.OpensWheelOnStartup(new[] { "--files", photo })
            && !Program.OpensPanelOnStartup(new[] { "--multi", "--files", photo })
            && !Program.OpensWheelOnStartup(new[] { "--install-context-menu" })
            && !Program.OpensWheelOnStartup(new[] { "--install-context-menu-preview" })
            && !Program.OpensWheelOnStartup(new[] { "--install-context-menu-png" });
    }

    static bool NextImageCliRejectsPathsAndIcons(ContextMenuCatalog.EngineSnapshot engines)
    {
        string[] rejected =
        {
            @"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe",
            @"C:\dev\FLIX\src\Flix\obj\Release\net10.0-windows\Flix.exe",
            Path.Combine(Path.GetTempPath(), "Flix.exe"),
            @"C:\Programmi\Altro\Flix.exe"
        };
        var store = new TouchRegistry();
        foreach (string path in rejected)
        {
            int install = RunImageCommand(new[] { "--install-context-menu-images" }, path, store, engines, out string installError);
            int remove = RunImageCommand(new[] { "--remove-context-menu-images" }, path, store, engines, out string removeError);
            if (install == 0 || remove == 0 || store.Calls != 0) return false;
            if (installError.Length == 0 || removeError.Length == 0) return false;
            if (installError.Contains("garantito", StringComparison.OrdinalIgnoreCase)) return false;
        }
        string missing = ImageLab("cli-missing");
        string corrupt = ImageLab("cli-corrupt");
        try
        {
            if (!CopyPremiumIcons(missing, omit: "TIFF")) return false;
            if (!CopyPremiumIcons(corrupt, corrupt: "BMP")) return false;
            int missingCode = RunImageCommand(new[] { "--install-context-menu-images" }, Path.Combine(missing, "Flix.exe"), store, engines, out string missingError);
            int corruptCode = RunImageCommand(new[] { "--install-context-menu-images" }, Path.Combine(corrupt, "Flix.exe"), store, engines, out string corruptError);
            return missingCode != 0 && corruptCode != 0 && store.Calls == 0
                && missingError.Contains("Icone non disponibili", StringComparison.Ordinal)
                && corruptError.Contains("Icone non disponibili", StringComparison.Ordinal);
        }
        finally
        {
            DeleteLab(missing);
            DeleteLab(corrupt);
        }
    }

    static bool NextImageRollbackAccessAndSecurity(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("cli-rollback");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            Exception[] failures =
            {
                new UnauthorizedAccessException("errore simulato"),
                new SecurityException("errore simulato"),
                new InvalidOperationException("errore simulato"),
                new ArgumentException("errore simulato")
            };
            foreach (var failure in failures)
            {
                var installStore = new MemoryRegistryStore();
                installStore.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
                string beforeInstall = installStore.Export();
                var installFail = new FailAfterWriteStore(installStore) { FailOn = 4, Failure = failure };
                bool installThrew = false;
                try { ContextMenuNext.InstallImageMenus(exe, engines, installFail); }
                catch (Exception ex) when (ex.GetType() == failure.GetType())
                {
                    installThrew = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                        && !ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                        && !ex.Message.Contains("non è atomica", StringComparison.Ordinal);
                }
                if (!installThrew || installStore.Export() != beforeInstall) return false;
                var commandStore = new FailAfterWriteStore { FailOn = 4, Failure = CopyFailure(failure) };
                commandStore.Inner.Write(ContextMenuPreview.ParentKey, "MUIVerb", ContextMenuPreview.ParentVerb);
                string beforeCommand = commandStore.Inner.Export();
                int code = RunImageCommand(new[] { "--install-context-menu-images" }, exe, commandStore, engines, out string error);
                if (code == 0 || commandStore.Inner.Export() != beforeCommand) return false;
                if (!error.Contains("errore simulato", StringComparison.Ordinal) || error.Contains("potrebbe essere parziale", StringComparison.Ordinal)) return false;

                var installed = new MemoryRegistryStore();
                ContextMenuNext.InstallImageMenus(exe, engines, installed);
                installed.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
                string beforeRemove = installed.Export();
                var removeFail = new FailAfterDeleteStore(installed) { FailOn = 1, Failure = CopyFailure(failure) };
                bool removeThrew = false;
                try { ContextMenuNext.RemoveImageMenus(exe, removeFail); }
                catch (Exception ex) when (ex.GetType() == failure.GetType())
                {
                    removeThrew = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                        && !ex.Message.Contains("non è atomica", StringComparison.Ordinal);
                }
                if (!removeThrew || installed.Export() != beforeRemove) return false;
                if (installed.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;
            }

            var partial = new MemoryRegistryStore();
            ContextMenuNext.InstallImageMenus(exe, engines, partial);
            partial.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            var corruptRestore = new FailAfterDeleteStore(partial) { FailOn = 1, CorruptRestoreWrites = true };
            bool partialSignaled = false;
            try { ContextMenuNext.RemoveImageMenus(exe, corruptRestore); }
            catch (IOException ex)
            {
                partialSignaled = ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                    && ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase)
                    && ex.InnerException is IOException;
            }
            return partialSignaled
                && partial.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && partial.Export().Contains("#parziale", StringComparison.Ordinal);
        }
        finally { DeleteLab(directory); }
    }

    static Exception CopyFailure(Exception failure) => failure switch
    {
        UnauthorizedAccessException => new UnauthorizedAccessException(failure.Message),
        SecurityException => new SecurityException(failure.Message),
        InvalidOperationException => new InvalidOperationException(failure.Message),
        ArgumentException => new ArgumentException(failure.Message),
        _ => new IOException(failure.Message)
    };

    static bool NextImageRemoveWithoutIcons(ContextMenuCatalog.EngineSnapshot engines)
    {
        string directory = ImageLab("cli-no-icons");
        try
        {
            if (!CopyPremiumIcons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var store = new MemoryRegistryStore();
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string sibling = @"Software\Classes\SystemFileAssociations\.webp\shell\OtherTool";
            store.Write(sibling, "MUIVerb", "Altro");
            if (RunImageCommand(new[] { "--install-context-menu-images" }, exe, store, engines, out _) != 0) return false;
            foreach (string format in new[] { "GIF", "TIFF" })
                File.Delete(Path.Combine(directory, ContextMenuNext.IconFileName(format)));
            string before = store.Read(ContextMenuPreview.OriginalMenuKey, "") + "|" + store.Read(sibling, "MUIVerb");
            int removed = RunImageCommand(new[] { "--remove-context-menu-images" }, exe, store, engines, out string error);
            return removed == 0 && error.Length == 0
                && !store.KeyExists(ContextMenuNext.ParentKey)
                && !store.KeyExists(ContextMenuNext.ParentKeyFor(".jpg"))
                && !store.KeyExists(ContextMenuNext.ParentKeyFor(".jpeg"))
                && !store.KeyExists(ContextMenuNext.ParentKeyFor(".tif"))
                && !store.KeyExists(ContextMenuNext.ParentKeyFor(".tiff"))
                && store.Read(ContextMenuPreview.OriginalMenuKey, "") + "|" + store.Read(sibling, "MUIVerb") == before
                && store.Read(sibling, "MUIVerb") == "Altro";
        }
        finally { DeleteLab(directory); }
    }

    static bool NextImageUsesMenuProgress(ContextMenuCatalog.EngineSnapshot engines, string exe)
    {
        if (!PreviewCopyUnchanged()) return false;
        var plans = ContextMenuNext.CreateImagePlans(exe, engines);
        return plans.SelectMany(plan => plan.Values).Where(value => value.Name.Length == 0).All(value =>
            value.Key.EndsWith(@"\command", StringComparison.OrdinalIgnoreCase)
            && value.Data.Contains("--convert-menu ", StringComparison.Ordinal)
            && !value.Key.EndsWith(@"\shell\command", StringComparison.OrdinalIgnoreCase));
    }

    static bool FlixRegistryMigration(ContextMenuCatalog.EngineSnapshot native)
    {
        var alien = new MemoryRegistryStore();
        string alienKey = ContextMenuNext.LegacyKeyFor(ContextMenuNext.ParentKey, ContextMenuNext.MenuId);
        alien.Write(alienKey, LegacyIdentity.OwnerValueName, "Altro");
        alien.Write(alienKey, "MUIVerb", "Non FLIX");
        alien.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
        ContextMenuNext.Remove(alien);
        if (alien.Read(alienKey, "MUIVerb") != "Non FLIX" || alien.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;

        string directory = ImageLab("flix-migration");
        try
        {
            if (!CopyPremiumIcons(directory) || !CopyPremiumV2Icons(directory) || !CopyMediaPremiumIcons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var store = new MemoryRegistryStore();
            var plan = ContextMenuNext.CreatePlan(exe, native);
            string legacyOwner = ContextMenuNext.LegacyMenuId(ContextMenuNext.MenuId);
            foreach (string key in plan.Keys)
            {
                string legacy = ContextMenuNext.LegacyKeyFor(key, ContextMenuNext.MenuId);
                store.Write(legacy, LegacyIdentity.OwnerValueName, legacyOwner);
                store.Write(legacy, "MUIVerb", LegacyIdentity.OriginalMenuVerb);
            }
            string legacyCommand = ContextMenuNext.LegacyKeyFor(ContextMenuNext.VerbKey("JPG") + @"\command", ContextMenuNext.MenuId);
            store.Write(legacyCommand, "", LegacyIdentity.LegacyConvertCommand);
            string blocked = ContextMenuNext.LegacyKeyFor(ContextMenuNext.VerbKey("GIF") + @"\command", ContextMenuNext.MenuId);
            store.Write(blocked, LegacyIdentity.OwnerValueName, "Altro");
            store.Write(blocked + @"\extra", "MUIVerb", "Non toccare");
            string foreign = @"Software\Classes\SystemFileAssociations\.png\shell\Other.App";
            store.Write(foreign, "MUIVerb", "Esterno");
            store.Write(foreign, ContextMenuNext.OwnerValue, "Altro");
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            store.Write(ContextMenuPreview.ParentKey, "MUIVerb", ContextMenuPreview.ParentVerb);
            store.Write(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue, ContextMenuPreview.PrototypeId);
            store.Write(LegacyIdentity.PreviewParentKey, LegacyIdentity.PreviewOwnerValueName, LegacyIdentity.PreviewId);
            store.Write(LegacyIdentity.PreviewParentKey, "MUIVerb", "Anteprima storica");

            ContextMenuNext.Apply(false, exe, store);
            if (store.Read(ContextMenuNext.ParentKey, "MUIVerb") != ContextMenuNext.ParentVerb) return false;
            string parentIcon = store.Read(ContextMenuNext.ParentKey, "Icon") ?? "";
            if (!parentIcon.EndsWith(ContextMenuNext.ParentIconFileName, StringComparison.OrdinalIgnoreCase)) return false;
            foreach (string format in ContextMenuNext.Formats)
            {
                string icon = store.Read(ContextMenuNext.VerbKey(format), "Icon") ?? "";
                string command = store.Read(ContextMenuNext.VerbKey(format) + @"\command", "") ?? "";
                if (!icon.EndsWith(ContextMenuNext.IconFileName(format), StringComparison.OrdinalIgnoreCase)) return false;
                if (icon.EndsWith(ContextMenuNext.ParentIconFileName, StringComparison.OrdinalIgnoreCase)) return false;
                if (!command.Contains("\\Flix.exe\"", StringComparison.OrdinalIgnoreCase) || !command.Contains("--convert-menu " + format, StringComparison.Ordinal)) return false;
            }
            if (store.KeyExists(legacyCommand)) return false;
            if (!store.KeyExists(blocked) || store.Read(blocked, LegacyIdentity.OwnerValueName) != "Altro") return false;
            if (store.Read(blocked + @"\extra", "MUIVerb") != "Non toccare") return false;
            if (store.Read(foreign, "MUIVerb") != "Esterno" || store.Read(foreign, ContextMenuNext.OwnerValue) != "Altro") return false;
            if (store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") != LegacyIdentity.OriginalMenuCommand) return false;
            if (store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) != ContextMenuPreview.PrototypeId) return false;
            if (store.Read(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) != ContextMenuNext.MenuId) return false;
            if (store.Read(ContextMenuNext.ParentKey, LegacyIdentity.OwnerValueName) != null) return false;
            if (store.Read(LegacyIdentity.PreviewParentKey, "MUIVerb") != "Anteprima storica") return false;
            string once = store.Export();
            ContextMenuNext.Apply(false, exe, store);
            if (store.Export() != once) return false;

            var clean = new MemoryRegistryStore();
            ContextMenuNext.InstallImageMenus(exe, native, clean);
            string jpegIcon = clean.Read(ContextMenuNext.ParentKeyFor(".jpeg"), "Icon") ?? "";
            if (clean.Read(ContextMenuNext.ParentKeyFor(".jpeg"), "MUIVerb") != "Converti con FLIX" || !jpegIcon.EndsWith(ContextMenuNext.ParentIconFileName, StringComparison.OrdinalIgnoreCase)) return false;
            string again = clean.Export();
            ContextMenuNext.InstallImageMenus(exe, native, clean);
            if (clean.Export() != again) return false;
            string legacyParent = ContextMenuNext.LegacyKeyFor(ContextMenuNext.ParentKey, ContextMenuNext.MenuId);
            ContextMenuNext.RemoveImageMenus(exe, clean);
            if (clean.KeyExists(ContextMenuNext.ParentKey) || clean.KeyExists(legacyParent) || clean.KeyExists(ContextMenuNext.ParentKeyFor(".jpeg"))) return false;

            if (!LegacyLeafRemoved(exe, ContextMenuNext.ContentParentKeyFor(".txt"), ContextMenuNext.ContentMenuIdFor(".txt"), @"Software\Classes\SystemFileAssociations\.txt\shell\Foreign.Txt", store => ContextMenuNext.RemoveContentMenus(exe, store))) return false;
            if (!LegacyLeafRemoved(exe, ContextMenuNext.MediaParentKeyFor(".mp3"), ContextMenuNext.MediaMenuIdFor(".mp3"), @"Software\Classes\SystemFileAssociations\.mp3\shell\Foreign.Mp3", store => ContextMenuNext.RemoveMediaMenus(exe, store))) return false;
            if (!LegacyLeafRemoved(exe, ContextMenuNext.DocumentParentKeyFor(".docx"), ContextMenuNext.DocumentMenuIdFor(".docx"), @"Software\Classes\SystemFileAssociations\.docx\shell\Foreign.Docx", store => ContextMenuNext.RemoveDocumentMenus(exe, store))) return false;

            var partial = new MemoryRegistryStore();
            partial.Write(ContextMenuNext.ParentKey, LegacyIdentity.OwnerValueName, ContextMenuNext.MenuId);
            partial.Write(ContextMenuNext.ParentKey, "MUIVerb", "Parziale");
            ContextMenuNext.Apply(false, exe, partial);
            if (partial.Read(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) != ContextMenuNext.MenuId) return false;
            if (partial.Read(ContextMenuNext.ParentKey, LegacyIdentity.OwnerValueName) != null) return false;

            var historicalPreview = new MemoryRegistryStore();
            historicalPreview.Write(LegacyIdentity.PreviewParentKey, LegacyIdentity.PreviewOwnerValueName, "Altro");
            historicalPreview.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            ContextMenuPreview.Remove(historicalPreview);
            if (historicalPreview.Read(LegacyIdentity.PreviewParentKey, LegacyIdentity.PreviewOwnerValueName) != "Altro") return false;
            if (historicalPreview.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;
            historicalPreview.Write(LegacyIdentity.PreviewParentKey, LegacyIdentity.PreviewOwnerValueName, LegacyIdentity.PreviewId);
            historicalPreview.Write(LegacyIdentity.PreviewParentKey + @"\shell", LegacyIdentity.PreviewOwnerValueName, LegacyIdentity.PreviewId);
            ContextMenuPreview.Remove(historicalPreview);
            if (historicalPreview.KeyExists(LegacyIdentity.PreviewParentKey) || historicalPreview.KeyExists(LegacyIdentity.PreviewParentKey + @"\shell")) return false;
            if (historicalPreview.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;

            var ancestors = new MemoryRegistryStore();
            string docRoot = ContextMenuNext.DocumentParentKeyFor(".docx");
            ancestors.Write(docRoot, LegacyIdentity.OwnerValueName, ContextMenuNext.DocumentMenuIdFor(".docx"));
            ancestors.Write(docRoot, LegacyIdentity.AncestorValueName, "ext");
            string extKey = ContextMenuNext.DocumentAssociationKey(".docx");
            ancestors.EnsureKey(extKey);
            ContextMenuNext.RemoveDocumentMenus(exe, ancestors);
            if (ancestors.KeyExists(docRoot) || ancestors.KeyExists(extKey)) return false;
            return true;
        }
        finally { DeleteLab(directory); }
    }

    static bool LegacyLeafRemoved(string exe, string key, string menuId, string foreign, Action<MemoryRegistryStore> remove)
    {
        var store = new MemoryRegistryStore();
        string legacy = ContextMenuNext.LegacyKeyFor(key, menuId);
        store.Write(legacy, LegacyIdentity.OwnerValueName, ContextMenuNext.LegacyMenuId(menuId));
        store.Write(foreign, "MUIVerb", "Altro");
        remove(store);
        return !store.KeyExists(legacy) && store.Read(foreign, "MUIVerb") == "Altro";
    }

    static string ImageLab(string name) =>
        Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, name + "-" + Guid.NewGuid().ToString("N"));

    static void DeleteLab(string directory)
    {
        try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        catch (IOException) { }
    }

    static bool CopyParentIcon(string directory)
    {
        Directory.CreateDirectory(directory);
        string name = ContextMenuNext.ParentIconFileName;
        string source = Path.Combine(AppContext.BaseDirectory, name);
        if (!ContextMenuPreview.ParentIconIsValid(source)) return false;
        File.Copy(source, Path.Combine(directory, name), true);
        return true;
    }

    static bool CopyPremiumIcons(string directory, string? omit = null, string? corrupt = null)
    {
        Directory.CreateDirectory(directory);
        if (!CopyParentIcon(directory)) return false;
        foreach (string format in new[] { "JPG", "PDF", "WEBP", "PNG", "BMP", "TIFF", "GIF" })
        {
            string name = ContextMenuNext.IconFileName(format);
            string dest = Path.Combine(directory, name);
            if (omit != null && format.Equals(omit, StringComparison.Ordinal)) continue;
            if (corrupt != null && format.Equals(corrupt, StringComparison.Ordinal))
            {
                File.WriteAllBytes(dest, [1, 2, 3, 4]);
                continue;
            }
            string source = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(source) || !ContextMenuPreview.IconIsValid(source)) return false;
            File.Copy(source, dest);
        }
        return true;
    }

    static int IconFingerprint(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        int hash = data.Length;
        for (int index = 0; index < data.Length; index += 64)
            hash = unchecked(hash * 31 + data[index]);
        return hash;
    }

    static void AddMenuProgressChecks(Dictionary<string, bool> checks)
    {
        string root = Path.Combine(Path.GetTempPath(), "flix-m42-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string priorLog = Evidence.DirectoryPath;
        Evidence.DirectoryPath = Path.Combine(root, "logs");
        try
        {
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
            string photo = Path.Combine(root, "foto.png");
            File.WriteAllBytes(photo, png);
            var engines = new ContextMenuCatalog.EngineSnapshot(false, false, false);
            Check(checks, "menu_png_to_pdf", () => MenuImageConverts(photo, png, "PDF", engines));
            Check(checks, "menu_png_to_webp", () => MenuImageConverts(photo, png, "WEBP", engines));
            Check(checks, "menu_png_progress_waits_for_outcome", () => EngineProgressWaits(photo, png, engines));
            Check(checks, "menu_progress_unmeasured_stays_indeterminate", () => UnmeasuredStaysIndeterminate(photo));
            Check(checks, "menu_progress_measured_caps_below_complete", () => MeasuredCapsBelowComplete(photo));
            Check(checks, "menu_progress_success_only_after_outcome", () => SuccessOnlyAfterOutcome(photo, png));
            Check(checks, "menu_progress_error_is_not_success", () => ErrorIsNotSuccess(photo, png));
            Check(checks, "menu_progress_cancel_waits_and_is_isolated", () => CancelWaitsAndIsIsolated(photo, png, engines));
            Check(checks, "menu_progress_late_update_ignored", () => LateUpdateIgnored(photo));
            Check(checks, "menu_progress_busy_does_not_cancel_other", () => BusyDoesNotCancelOther());
            Check(checks, "menu_progress_no_balloon_and_no_wheel", () => MenuSkipsBalloonAndWheel(photo));
            Check(checks, "menu_preview_graphic_copy_unchanged", () => PreviewCopyUnchanged());
            Check(checks, "menu_progress_ipc_wire_unchanged", () => ProgressLeavesIpcUnchanged(photo));
        }
        finally
        {
            Evidence.DirectoryPath = priorLog;
            try { if (Directory.Exists(root)) Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    static bool MenuImageConverts(string photo, byte[] original, string format, ContextMenuCatalog.EngineSnapshot engines)
    {
        string kept = Path.Combine(Path.GetDirectoryName(photo)!, "foto.converted." + format.ToLowerInvariant());
        File.WriteAllBytes(kept, new byte[] { 9, 9, 9, 9 });
        var percents = new List<int>();
        var notes = new List<string>();
        var outcome = Task.Run(() => MenuConversion.RunExclusive(() => false, _ => { }, () => null, _ => { },
            new MenuConversion.Request(format, photo),
            async (path, target, token) =>
            {
                var results = await ConversionEngine.Convert(new[] { path }, new[] { target }, token, null, (percent, _) => percents.Add(percent));
                return results[0];
            },
            notes.Add, engines)).GetAwaiter().GetResult();
        if (!outcome.Succeeded) throw new InvalidOperationException(outcome.Message);
        var model = MenuProgressModel.Start("foto.png", format, new CancellationTokenSource());
        foreach (int percent in percents) model.Report(percent);
        if (model.Phase != MenuProgressModel.View.Indeterminate || model.Percent != null || model.ShowCheck)
            throw new InvalidOperationException("Percentuali immagine non attese: " + string.Join(",", percents));
        model.Complete(outcome);
        string produced = outcome.Output!;
        return model.Phase == MenuProgressModel.View.Success
            && model.Percent == 100
            && model.ShowCheck
            && model.Folder == outcome.Folder
            && model.ResultName == Path.GetFileName(produced)
            && !model.OpensWheel
            && notes[0] == MenuConversion.StartedMessage(format)
            && File.Exists(produced)
            && new FileInfo(produced).Length > 0
            && !string.Equals(Path.GetFullPath(produced), Path.GetFullPath(photo), StringComparison.OrdinalIgnoreCase)
            && File.ReadAllBytes(photo).SequenceEqual(original)
            && File.ReadAllBytes(kept).SequenceEqual(new byte[] { 9, 9, 9, 9 });
    }

    static bool EngineProgressWaits(string photo, byte[] original, ContextMenuCatalog.EngineSnapshot engines)
    {
        var percents = new List<int>();
        var outcome = Task.Run(() => MenuConversion.Execute(new MenuConversion.Request("JPG", photo),
            async (path, format, token) =>
            {
                var results = await ConversionEngine.Convert(new[] { path }, new[] { format }, token, null, (percent, _) => percents.Add(percent));
                return results[0];
            },
            _ => { }, CancellationToken.None, engines)).GetAwaiter().GetResult();
        var model = MenuProgressModel.Start("foto.png", "JPG", new CancellationTokenSource());
        foreach (int percent in percents) model.Report(percent);
        bool waiting = model.Phase == MenuProgressModel.View.Indeterminate && model.Percent != 100 && !model.ShowCheck && percents.Contains(100);
        model.Complete(outcome);
        return waiting
            && outcome.Succeeded
            && model.ShowCheck
            && model.Percent == 100
            && File.ReadAllBytes(photo).SequenceEqual(original);
    }

    static bool UnmeasuredStaysIndeterminate(string photo)
    {
        var model = MenuProgressModel.Start("foto.png", "JPG", new CancellationTokenSource());
        bool ignored = !model.Report(2) && !model.Report(25) && !model.Report(35) && !model.Report(100) && !model.Report(0);
        return ignored
            && model.Phase == MenuProgressModel.View.Indeterminate
            && model.Percent == null
            && !model.ShowCheck
            && model.FileName == "foto.png"
            && model.Format == "JPG"
            && model.Status == MenuProgressWindow.RunningStatus
            && !string.Equals(Path.GetFileName(photo), "", StringComparison.Ordinal);
    }

    static bool MeasuredCapsBelowComplete(string photo)
    {
        var model = MenuProgressModel.Start("clip.mp4", "MP3", new CancellationTokenSource());
        bool start = !model.Report(2) && model.Report(5) && model.Percent == 5 && model.Phase == MenuProgressModel.View.Determinate && !model.ShowCheck;
        bool forward = model.Report(64) && model.Percent == 64;
        bool capped = !model.Report(100) && model.Percent == 64 && !model.ShowCheck;
        bool higher = model.Report(96) && model.Percent == 96 && model.Phase == MenuProgressModel.View.Determinate;
        bool backward = !model.Report(25);
        string folder = Path.GetDirectoryName(photo)!;
        var success = new MenuConversion.Outcome(MenuConversion.Status.Success, "Creato clip.mp3 in " + folder + ".", Path.Combine(folder, "clip.mp3"), folder);
        model.Complete(success);
        bool done = model.Phase == MenuProgressModel.View.Success && model.Percent == 100 && model.ShowCheck && model.Folder == folder;
        bool late = !model.Report(80) && model.Percent == 100 && model.ShowCheck;
        return start && forward && capped && higher && backward && done && late;
    }

    static bool SuccessOnlyAfterOutcome(string photo, byte[] original)
    {
        var pending = MenuProgressModel.Start("foto.png", "JPG", new CancellationTokenSource());
        pending.Report(90);
        pending.Report(100);
        bool notYet = pending.Phase == MenuProgressModel.View.Determinate && pending.Percent == 90 && !pending.ShowCheck;
        var claimed = new MenuConversion.Outcome(MenuConversion.Status.Error, "Creato finto.jpg in C:\\temp.", null, null);
        pending.Complete(claimed);
        bool rejected = pending.Phase == MenuProgressModel.View.Error && !pending.ShowCheck && pending.Percent == null && pending.Detail == claimed.Message;
        var disguised = MenuConversion.FromResult(photo, new ConversionResult(photo, "JPG", new[] { photo }, null));
        var guard = MenuProgressModel.Start("foto.png", "JPG", new CancellationTokenSource());
        guard.Report(98);
        guard.Complete(disguised);
        return notYet && rejected && disguised.Status == MenuConversion.Status.Error && guard.Phase == MenuProgressModel.View.Error && !guard.ShowCheck && File.ReadAllBytes(photo).SequenceEqual(original);
    }

    static bool ErrorIsNotSuccess(string photo, byte[] original)
    {
        var model = MenuProgressModel.Start("foto.png", "PDF", new CancellationTokenSource());
        model.Report(2);
        model.Report(40);
        model.Report(100);
        var failed = new MenuConversion.Outcome(MenuConversion.Status.Error, "Encoder non disponibile.", null, null);
        model.Complete(failed);
        bool late = !model.Report(70) && model.Percent == null && !model.ShowCheck;
        return model.Phase == MenuProgressModel.View.Error
            && model.Status == MenuProgressWindow.ErrorStatus
            && model.Detail == failed.Message
            && late
            && File.ReadAllBytes(photo).SequenceEqual(original);
    }

    static bool CancelWaitsAndIsIsolated(string photo, byte[] original, ContextMenuCatalog.EngineSnapshot engines)
    {
        bool busy = false;
        CancellationTokenSource? job = null;
        var other = new CancellationTokenSource();
        MenuProgressModel? session = null;
        bool stillRunning = false;
        bool isolated = false;
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = Task.Run(() => MenuConversion.RunExclusive(() => busy, value => busy = value, () => job, value => job = value,
            new MenuConversion.Request("JPG", photo),
            async (_, _, token) =>
            {
                session = MenuProgressModel.Start("foto.png", "JPG", job);
                stillRunning = session.Phase == MenuProgressModel.View.Indeterminate && !session.ShowCheck && !session.CancelRequested;
                session.RequestCancel();
                isolated = session.CancelRequested
                    && session.Phase == MenuProgressModel.View.Indeterminate
                    && !session.ShowCheck
                    && job!.IsCancellationRequested
                    && !other.IsCancellationRequested;
                entered.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token);
                return new ConversionResult(photo, "JPG", Array.Empty<string>(), "non completata");
            },
            _ => { }, engines));
        if (!entered.Task.Wait(TimeSpan.FromSeconds(3))) throw new TimeoutException("Annullamento non avviato.");
        if (!task.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Annullamento non concluso.");
        var outcome = task.Result;
        session!.Complete(outcome);
        bool late = !session.Report(55) && !session.ShowCheck && session.Percent == null;
        return stillRunning
            && isolated
            && outcome.Status == MenuConversion.Status.Cancelled
            && !outcome.Succeeded
            && session.Phase == MenuProgressModel.View.Cancelled
            && session.Status == MenuProgressWindow.CancelledStatus
            && late
            && File.ReadAllBytes(photo).SequenceEqual(original)
            && !other.IsCancellationRequested;
    }

    static bool LateUpdateIgnored(string photo)
    {
        string folder = Path.GetDirectoryName(photo)!;
        var done = MenuProgressModel.Start("foto.png", "WEBP", new CancellationTokenSource());
        done.Report(33);
        done.Complete(new MenuConversion.Outcome(MenuConversion.Status.Success, "Creato foto.webp in " + folder + ".", Path.Combine(folder, "foto.webp"), folder));
        bool success = !done.Report(100) && !done.Report(12) && done.Percent == 100 && done.ShowCheck;
        var stopped = MenuProgressModel.Start("foto.png", "JPG", new CancellationTokenSource());
        stopped.Report(18);
        stopped.Complete(new MenuConversion.Outcome(MenuConversion.Status.Cancelled, MenuConversion.CancelledMessage, null, null));
        bool cancelled = !stopped.Report(99) && stopped.Phase == MenuProgressModel.View.Cancelled && !stopped.ShowCheck && stopped.Percent == null;
        var failed = MenuProgressModel.Start("foto.png", "PDF", new CancellationTokenSource());
        failed.Complete(new MenuConversion.Outcome(MenuConversion.Status.Error, "Conversione non riuscita.", null, null));
        bool error = !failed.Report(100) && failed.Phase == MenuProgressModel.View.Error && !failed.ShowCheck;
        return success && cancelled && error;
    }

    static bool BusyDoesNotCancelOther()
    {
        var active = new CancellationTokenSource();
        var running = MenuProgressModel.Start("foto.png", "JPG", active);
        running.Report(22);
        var rejected = MenuProgressModel.Reject("altro.png", "PDF", MenuConversion.BusyMessage, busy: true);
        rejected.RequestCancel();
        bool router = MenuProgressRouter.Classify(MenuConversion.BusyMessage, true, out _) == MenuProgressRouter.Kind.Busy
            && MenuProgressRouter.Classify(MenuConversion.StartedMessage("PDF"), true, out _) == MenuProgressRouter.Kind.Ignored
            && MenuProgressRouter.Classify(MenuConversion.StartedMessage("PDF"), false, out string format) == MenuProgressRouter.Kind.Started
            && format == "PDF";
        return !active.IsCancellationRequested
            && running.Phase == MenuProgressModel.View.Determinate
            && running.Percent == 22
            && !running.ShowCheck
            && rejected.Phase == MenuProgressModel.View.Busy
            && rejected.Status == MenuConversion.BusyMessage
            && rejected.Detail == MenuProgressWindow.BusyDetail
            && !rejected.ShowCheck
            && rejected.FileName == "altro.png"
            && router;
    }

    static bool MenuSkipsBalloonAndWheel(string photo)
    {
        int balloons = 0;
        Action<string> balloon = _ => balloons++;
        _ = balloon;
        bool notices = !MenuProgressRouter.UsesBalloon
            && MenuProgressRouter.Classify(MenuConversion.StartedMessage("JPG"), false, out string format) == MenuProgressRouter.Kind.Started
            && format == "JPG"
            && MenuProgressRouter.Classify("Creato foto.jpg in C:\\temp.", true, out _) == MenuProgressRouter.Kind.Ignored
            && MenuProgressRouter.Classify("File non trovato.", false, out _) == MenuProgressRouter.Kind.Validation
            && MenuProgressRouter.Classify(MenuConversion.CancelledMessage, true, out _) == MenuProgressRouter.Kind.Ignored;
        bool wheel = !Program.OpensWheelOnStartup(["--convert-menu", "JPG", "--files", photo])
            && !Program.OpensWheelOnStartup(["--convert-menu", "PDF", "--files", photo])
            && !Program.OpensWheelOnStartup(["--convert-menu", "WEBP", "--files", photo])
            && !Program.OpensPanelOnStartup(["--convert-menu", "JPG", "--files", photo])
            && !Program.OpensWheelOnStartup(["--preview-menu-progress"])
            && !Program.OpensPanelOnStartup(["--preview-menu-progress"])
            && !Program.OpensWheelOnStartup(["--preview-menu-progress", "--files", photo])
            && !Program.OpensPanelOnStartup(["--preview-menu-progress", "--multi", "--files", photo])
            && !Program.OpensWheelOnStartup(["--files", photo])
            && !Program.OpensPanelOnStartup(["--multi", "--files", photo]);
        return balloons == 0 && notices && wheel;
    }

    static bool PreviewCopyUnchanged()
    {
        return MenuProgressWindow.CardWidth == 440
            && MenuProgressWindow.CardHeight == 210
            && MenuProgressWindow.BarHeight == 4
            && MenuProgressWindow.PreviewFileName == "foto.png"
            && MenuProgressWindow.PreviewFormatName == "JPG"
            && MenuProgressWindow.RunningStatus == "Conversione in corso..."
            && MenuProgressWindow.PartialPercentText == "40%"
            && MenuProgressWindow.SuccessStatus == "Conversione completata"
            && MenuProgressWindow.OpenFolderText == "Apri cartella"
            && MenuProgressWindow.PreviewOpenTip == "Anteprima: nessuna cartella viene aperta."
            && MenuProgressWindow.ErrorStatus == "Conversione non riuscita."
            && MenuProgressWindow.ErrorDetail == "Il file originale non è stato modificato."
            && MenuProgressWindow.CancelledStatus == "Conversione annullata"
            && MenuProgressWindow.CancelText == "Annulla"
            && MenuProgressWindow.CloseText == "Chiudi";
    }

    static bool ProgressLeavesIpcUnchanged(string photo)
    {
        return MenuConversion.TryParse(["--convert-menu", "JPG", "--files", photo], out var request, out _)
            && request.Format == "JPG"
            && request.File == photo
            && !Program.StartsResidentHost(["--convert-menu", "JPG", "--files", photo])
            && !Program.OpensWheelOnStartup(["--files", photo])
            && !Program.OpensPanelOnStartup(["--multi", "--files", photo]);
    }

    static bool ContentSevenExtensions(ContextMenuCatalog.EngineSnapshot engines)
    {
        string[] expected = [".pdf", ".txt", ".json", ".yaml", ".yml", ".xml", ".csv"];
        if (!ContextMenuNext.ContentExtensions.SequenceEqual(expected)) return false;
        var plans = ContextMenuNext.PlanContentMenus(engines);
        if (plans.Length != 7 || !plans.Select(plan => plan.Extension).SequenceEqual(expected)) return false;
        string[] ids = ["Flix.Convert.Pdf", "Flix.Convert.Txt", "Flix.Convert.Json", "Flix.Convert.Yaml", "Flix.Convert.Yaml", "Flix.Convert.Xml", "Flix.Convert.Csv"];
        if (!plans.Select(plan => plan.MenuId).SequenceEqual(ids)) return false;
        var supported = ContextMenuCatalog.SupportedSourceExtensions();
        return expected.All(extension => supported.Contains(extension))
            && plans.All(plan => plan.MenuId == ContextMenuNext.ContentMenuIdFor(plan.Extension)
                && plan.ParentKey == ContextMenuNext.ContentParentKeyFor(plan.Extension)
                && plan.SubCommandsPath == ContextMenuNext.ContentSubCommandsPathFor(plan.Extension));
    }

    static bool ContentDestinationCounts(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        var without = ContextMenuNext.PlanContentMenus(native);
        var with = ContextMenuNext.PlanContentMenus(pandoc);
        if (without.Sum(plan => plan.Formats.Count) != 43) return false;
        if (with.Sum(plan => plan.Formats.Count) != 59) return false;
        int added = 0;
        for (int index = 0; index < without.Length; index++)
        {
            int delta = with[index].Formats.Count - without[index].Formats.Count;
            if (without[index].Extension is ".pdf" or ".txt")
            {
                if (delta != 8) return false;
            }
            else if (delta != 0 || !without[index].Formats.SequenceEqual(with[index].Formats)) return false;
            added += delta;
        }
        return added == 16
            && without.Single(plan => plan.Extension == ".pdf").Formats.SequenceEqual(["TXT", "JPG", "PNG", "WEBP"])
            && without.Single(plan => plan.Extension == ".txt").Formats.SequenceEqual(["PDF", "JPG", "PNG", "WEBP"])
            && without.Single(plan => plan.Extension == ".json").Formats.SequenceEqual(["PDF", "HTML", "MD", "TXT", "YAML", "XML", "CSV"])
            && without.Single(plan => plan.Extension == ".yaml").Formats.SequenceEqual(["PDF", "HTML", "MD", "TXT", "JSON", "XML", "CSV"])
            && without.Single(plan => plan.Extension == ".xml").Formats.SequenceEqual(["PDF", "HTML", "MD", "TXT", "JSON", "YAML", "CSV"])
            && without.Single(plan => plan.Extension == ".csv").Formats.SequenceEqual(["PDF", "HTML", "MD", "TXT", "JSON", "YAML", "XML"]);
    }

    static bool ContentNoSourceIdentity(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        var banned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = "PDF", [".txt"] = "TXT", [".json"] = "JSON",
            [".yaml"] = "YAML", [".yml"] = "YAML", [".xml"] = "XML", [".csv"] = "CSV"
        };
        foreach (var engines in new[] { native, pandoc })
        {
            foreach (var plan in ContextMenuNext.PlanContentMenus(engines))
            {
                if (plan.Formats.Contains(banned[plan.Extension], StringComparer.Ordinal)) return false;
                if (plan.Formats.Contains("YML", StringComparer.Ordinal)) return false;
                if (plan.Formats.Distinct(StringComparer.Ordinal).Count() != plan.Formats.Count) return false;
            }
        }
        return true;
    }

    static bool ContentYamlAlias(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc, string exe)
    {
        foreach (var engines in new[] { native, pandoc })
        {
            var plans = ContextMenuNext.PlanContentMenus(engines);
            var yaml = plans.Single(plan => plan.Extension == ".yaml");
            var yml = plans.Single(plan => plan.Extension == ".yml");
            if (yaml.SharesCommandTree || !yml.SharesCommandTree) return false;
            if (yaml.MenuId != "Flix.Convert.Yaml" || yml.MenuId != yaml.MenuId) return false;
            if (!yaml.Formats.SequenceEqual(yml.Formats) || yaml.Formats.Contains("YAML")) return false;
            if (yml.SubCommandsPath != yaml.SubCommandsPath || yml.CommandParentKey != yaml.ParentKey) return false;
            if (yml.SubCommandsPath != @"SystemFileAssociations\.yaml\shell\Flix.Convert.Yaml") return false;
            if (!yml.ParentKey.Contains(@"\.yml\shell\Flix.Convert.Yaml", StringComparison.Ordinal)) return false;
            var ymlKeys = ContextMenuNext.PlannedContentKeys(yml);
            var yamlKeys = ContextMenuNext.PlannedContentKeys(yaml);
            if (ymlKeys.Count != 1 || ymlKeys[0] != yml.ParentKey) return false;
            if (ymlKeys.Any(key => yamlKeys.Contains(key, StringComparer.OrdinalIgnoreCase))) return false;
            if (yamlKeys.Count != 2 + yaml.Formats.Count * 2) return false;
            if (yamlKeys.Any(key => key.Contains(@"\.yml\", StringComparison.OrdinalIgnoreCase))) return false;
            var pointer = ContextMenuNext.DescribeContentMenu(yml, exe);
            var tree = ContextMenuNext.DescribeContentMenu(yaml, exe);
            if (pointer.Count != 6
                || pointer.Any(value => value.Name.Length == 0)
                || !pointer.Any(value => value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase)
                    && value.Data.EndsWith(ContextMenuNext.ParentIconFileName, StringComparison.OrdinalIgnoreCase))) return false;
            if (pointer.Single(value => value.Name == "ExtendedSubCommandsKey").Data != yaml.SubCommandsPath) return false;
            int commands = tree.Count(value => value.Name.Length == 0);
            if (commands != yaml.Formats.Count) return false;
            if (tree.Any(value => value.Key.Contains(@"\.yml\", StringComparison.OrdinalIgnoreCase))) return false;
        }
        return true;
    }

    static bool ContentOrderMatchesPolicy()
    {
        foreach (var engines in ContextMenuCatalog.EngineSnapshot.AllCombinations())
        {
            foreach (var plan in ContextMenuNext.PlanContentMenus(engines))
            {
                string[] policy = Policy.OrderFormats(Policy.Outputs(["flix.context" + plan.Extension], engines.Pandoc, engines.Office, engines.Ffmpeg));
                string[] catalog = ContextMenuCatalog.ForExtension(plan.Extension, engines).Select(entry => entry.TargetFormat).ToArray();
                if (!plan.Formats.SequenceEqual(policy) || !plan.Formats.SequenceEqual(catalog)) return false;
                if (!plan.Entries.Select(entry => entry.Title).SequenceEqual(plan.Formats.Select(format => "Converti in " + format))) return false;
            }
        }
        return true;
    }

    static bool ContentOfficeFfmpegInvariant()
    {
        var native = ContextMenuNext.PlanContentMenus(new ContextMenuCatalog.EngineSnapshot(false, false, false));
        foreach (bool office in new[] { false, true })
        foreach (bool ffmpeg in new[] { false, true })
        {
            var again = ContextMenuNext.PlanContentMenus(new ContextMenuCatalog.EngineSnapshot(false, office, ffmpeg));
            if (!ContentSameDestinations(native, again)) return false;
        }
        var pandoc = ContextMenuNext.PlanContentMenus(new ContextMenuCatalog.EngineSnapshot(true, false, false));
        foreach (bool office in new[] { false, true })
        foreach (bool ffmpeg in new[] { false, true })
        {
            var again = ContextMenuNext.PlanContentMenus(new ContextMenuCatalog.EngineSnapshot(true, office, ffmpeg));
            if (!ContentSameDestinations(pandoc, again)) return false;
        }
        return !ContentSameDestinations(native, pandoc);
    }

    static bool ContentSameDestinations(ContentMenuBlueprint[] left, ContentMenuBlueprint[] right)
    {
        if (left.Length != right.Length) return false;
        for (int index = 0; index < left.Length; index++)
            if (left[index].Extension != right[index].Extension || !left[index].Formats.SequenceEqual(right[index].Formats)) return false;
        return true;
    }

    static bool ContentKeysDoNotCollide(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        var imageKeys = ContextMenuNext.PlanImageMenus(native).SelectMany(ContextMenuNext.PlannedKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imageIds = ContextMenuNext.PlanImageMenus(native).Select(plan => plan.MenuId).ToHashSet(StringComparer.Ordinal);
        foreach (var engines in new[] { native, pandoc })
        {
            var plans = ContextMenuNext.PlanContentMenus(engines);
            if (plans.Select(plan => plan.ParentKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != plans.Length) return false;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var plan in plans)
            {
                if (imageIds.Contains(plan.MenuId) || plan.MenuId == ContextMenuPreview.PrototypeId || plan.MenuId == LegacyIdentity.OriginalMenuId) return false;
                foreach (string key in ContextMenuNext.PlannedContentKeys(plan))
                {
                    if (!seen.Add(key) || imageKeys.Contains(key)) return false;
                    if (key.Equals(ContextMenuPreview.OriginalMenuKey, StringComparison.OrdinalIgnoreCase)
                        || key.StartsWith(ContextMenuPreview.OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase)
                        || key.Equals(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase)
                        || key.StartsWith(ContextMenuPreview.ParentKey + @"\", StringComparison.OrdinalIgnoreCase)) return false;
                    if (!key.StartsWith(@"Software\Classes\SystemFileAssociations\", StringComparison.OrdinalIgnoreCase)) return false;
                    if (key.Contains(@"\CLSID", StringComparison.OrdinalIgnoreCase) || key.Contains("UserChoice", StringComparison.OrdinalIgnoreCase)) return false;
                    if (key.Contains(ContextMenuNext.ParentKey, StringComparison.OrdinalIgnoreCase)) return false;
                }
            }
        }
        return true;
    }

    static bool ContentLeavesImagePlans(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string[] before = ImagePlanSignature(native);
        _ = ContextMenuNext.PlanContentMenus(native);
        _ = ContextMenuNext.PlanContentMenus(pandoc);
        _ = ContextMenuNext.PlanContentMenus(new ContextMenuCatalog.EngineSnapshot(true, true, true));
        if (!before.SequenceEqual(ImagePlanSignature(native))) return false;
        if (!before.SequenceEqual(ImagePlanSignature(pandoc))) return false;
        if (!before.SequenceEqual(ImagePlanSignature(new ContextMenuCatalog.EngineSnapshot(true, true, true)))) return false;
        var png = ContextMenuNext.PlanImageMenus(native).Single(plan => plan.Extension == ".png");
        return png.MenuId == ContextMenuNext.MenuId
            && png.ParentKey == ContextMenuNext.ParentKey
            && png.Formats.SequenceEqual(ContextMenuNext.Formats)
            && png.Formats.SequenceEqual(["PDF", "JPG", "WEBP", "BMP", "TIFF", "GIF"])
            && ContextMenuNext.ImageExtensions.SequenceEqual(Policy.Images);
    }

    static string[] ImagePlanSignature(ContextMenuCatalog.EngineSnapshot engines) =>
        ContextMenuNext.PlanImageMenus(engines).Select(plan =>
            plan.Extension + "|" + plan.MenuId + "|" + plan.ParentKey + "|" + plan.SharesCommandTree
            + "|" + string.Join(",", plan.Formats)
            + "|" + string.Join(";", ContextMenuNext.PlannedKeys(plan))).ToArray();

    static bool ContentMenuIdForTxtUnchanged()
    {
        bool textRejected = false;
        try { ContextMenuNext.MenuIdFor(".txt"); }
        catch (IOException) { textRejected = true; }
        bool pdfRejected = false;
        try { ContextMenuNext.MenuIdFor(".pdf"); }
        catch (IOException) { pdfRejected = true; }
        bool parentRejected = false;
        try { ContextMenuNext.ParentKeyFor(".txt"); }
        catch (IOException) { parentRejected = true; }
        return textRejected
            && pdfRejected
            && parentRejected
            && ContextMenuNext.ContentMenuIdFor(".txt") == "Flix.Convert.Txt"
            && ContextMenuNext.ContentMenuIdFor("TXT") == "Flix.Convert.Txt"
            && ContextMenuNext.MenuId == "Flix.Convert.Png";
    }

    static bool ContentIconsNamedWithoutFiles(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc, string exe)
    {
        string[] future = ["TXT", "HTML", "MD", "JSON", "YAML", "XML", "CSV", "DOCX", "EPUB", "ODT", "RTF", "FB2", "RST"];
        if (future.Length != 13) return false;
        var names = future.Select(ContextMenuNext.IconFileName).ToArray();
        if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 13) return false;
        if (names.Any(name => !name.EndsWith("-premium-v2.ico", StringComparison.OrdinalIgnoreCase))) return false;
        for (int index = 0; index < future.Length; index++)
            if (names[index] != "flix-menu-" + future[index].ToLowerInvariant() + "-premium-v2.ico") return false;
        if (ContextMenuNext.IconFileName("PNG") != "flix-menu-png-premium-v2.ico"
            || ContextMenuNext.IconFileName("JPG") != ContextMenuPreview.IconFileName("JPG")
            || ContextMenuNext.IconFileName("PDF") != ContextMenuPreview.IconFileName("PDF")
            || ContextMenuNext.IconFileName("WEBP") != ContextMenuPreview.IconFileName("WEBP")) return false;
        string directory = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "content-icons-" + Guid.NewGuid().ToString("N"));
        string isolated = Path.Combine(directory, "Flix.exe");
        foreach (var engines in new[] { native, pandoc })
        {
            foreach (var plan in ContextMenuNext.PlanContentMenus(engines).Where(plan => !plan.SharesCommandTree))
            {
                var values = ContextMenuNext.DescribeContentMenu(plan, isolated);
                foreach (string format in plan.Formats)
                {
                    string icon = values.Single(value => value.Key.EndsWith(@"\shell\" + format.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) && value.Name == "Icon").Data;
                    if (!icon.EndsWith(ContextMenuNext.IconFileName(format), StringComparison.OrdinalIgnoreCase)) return false;
                    if (File.Exists(icon)) return false;
                }
            }
        }
        return !Directory.Exists(directory);
    }

    static bool ContentRegistrationRefused(ContextMenuCatalog.EngineSnapshot engines, string _)
    {
        if (ContextMenuNext.IsPlannedCommand(["--install-context-menu-content"])
            || ContextMenuNext.IsPlannedCommand(["--install-context-menu-pdf"])
            || ContextMenuNext.IsImageMenuCommand(["--install-context-menu-content"])
            || ContextMenuNext.IsImageMenuCommand(["--remove-context-menu-pdf"])) return false;
        var touch = new TouchRegistry();
        bool refused = false;
        try { ContextMenuNext.RefuseContentMenuRegistration(touch); }
        catch (IOException ex) { refused = ex.Message.Contains("M5.3.4", StringComparison.Ordinal) && ex.Message.Contains("Nessuna chiave", StringComparison.Ordinal); }
        if (!refused || touch.Calls != 0) return false;
        string directory = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "content-reg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            var pdf = ContextMenuNext.PlanContentMenus(engines).Single(plan => plan.Extension == ".pdf");
            var values = ContextMenuNext.DescribeContentMenu(pdf, exe);
            var plan = new ContextMenuPlan(exe, pdf.Formats.ToArray(), values);
            var memory = new MemoryRegistryStore();
            bool installRefused = false;
            try { ContextMenuNext.Install(plan, memory); }
            catch (IOException ex) { installRefused = ex.Message.Contains("M5.3.4", StringComparison.Ordinal); }
            var blocked = new TouchRegistry();
            try { ContextMenuNext.Install(plan, blocked); }
            catch (IOException) { }
            return installRefused && memory.KeyCount == 0 && blocked.Calls == 0 && !memory.KeyExists(pdf.ParentKey);
        }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }

    static readonly string[] PremiumV2IconFiles =
    [
        "flix-menu-jpg-premium-v2.ico", "flix-menu-pdf-premium-v2.ico", "flix-menu-webp-premium-v2.ico",
        "flix-menu-png-premium-v2.ico", "flix-menu-bmp-premium-v2.ico", "flix-menu-tiff-premium-v2.ico",
        "flix-menu-gif-premium-v2.ico", "flix-menu-txt-premium-v2.ico", "flix-menu-json-premium-v2.ico",
        "flix-menu-yaml-premium-v2.ico", "flix-menu-xml-premium-v2.ico", "flix-menu-csv-premium-v2.ico",
        "flix-menu-html-premium-v2.ico", "flix-menu-md-premium-v2.ico", "flix-menu-docx-premium-v2.ico",
        "flix-menu-epub-premium-v2.ico", "flix-menu-odt-premium-v2.ico", "flix-menu-rtf-premium-v2.ico",
        "flix-menu-fb2-premium-v2.ico", "flix-menu-rst-premium-v2.ico"
    ];

    static bool CopyPremiumV2Icons(string directory)
    {
        if (!CopyParentIcon(directory)) return false;
        foreach (string name in PremiumV2IconFiles)
        {
            string source = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(source) || !ContextMenuPreview.IconIsValid(source)) return false;
            File.Copy(source, Path.Combine(directory, name), true);
        }
        return true;
    }

    static bool ContentPremiumV2TwentyIconsInOutput()
    {
        if (PremiumV2IconFiles.Length != 20 || PremiumV2IconFiles.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 20) return false;
        foreach (string name in PremiumV2IconFiles)
        {
            string path = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(path) || !ContextMenuPreview.IconIsValid(path)) return false;
        }
        string[] contentOnly =
        [
            "flix-menu-txt-premium-v2.ico", "flix-menu-json-premium-v2.ico", "flix-menu-yaml-premium-v2.ico",
            "flix-menu-xml-premium-v2.ico", "flix-menu-csv-premium-v2.ico", "flix-menu-html-premium-v2.ico",
            "flix-menu-md-premium-v2.ico", "flix-menu-docx-premium-v2.ico", "flix-menu-epub-premium-v2.ico",
            "flix-menu-odt-premium-v2.ico", "flix-menu-rtf-premium-v2.ico", "flix-menu-fb2-premium-v2.ico",
            "flix-menu-rst-premium-v2.ico"
        ];
        return contentOnly.All(name => PremiumV2IconFiles.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    static bool ContentIconsGateRegistration(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string directory = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "content-gate-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            if (!ContextMenuNext.ContentIconsReady(exe, native) || !ContextMenuNext.ContentIconsReady(exe, pandoc)) return false;
            var nativePdf = ContextMenuNext.PlanContentMenus(native).Single(plan => plan.Extension == ".pdf");
            var pandocPdf = ContextMenuNext.PlanContentMenus(pandoc).Single(plan => plan.Extension == ".pdf");
            var nativeMemory = new MemoryRegistryStore();
            ContextMenuNext.RegisterContentMenu(nativePdf, exe, nativeMemory);
            if (!ContentBlueprintInstalled(nativeMemory, exe, nativePdf)) return false;
            if (nativeMemory.KeyExists(ContextMenuNext.ContentParentKeyFor(".txt"))) return false;
            string nativeOnce = nativeMemory.Export();
            ContextMenuNext.RegisterContentMenu(nativePdf, exe, nativeMemory);
            if (nativeMemory.Export() != nativeOnce) return false;
            var pandocMemory = new MemoryRegistryStore();
            ContextMenuNext.RegisterContentMenu(pandocPdf, exe, pandocMemory);
            if (!ContentBlueprintInstalled(pandocMemory, exe, pandocPdf)) return false;
            if (pandocPdf.Formats.Count <= nativePdf.Formats.Count || pandocMemory.KeyCount <= nativeMemory.KeyCount) return false;

            string missingDir = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "content-missing-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(missingDir);
            try
            {
                foreach (string name in PremiumV2IconFiles)
                {
                    if (name.Equals("flix-menu-docx-premium-v2.ico", StringComparison.OrdinalIgnoreCase)) continue;
                    File.Copy(Path.Combine(directory, name), Path.Combine(missingDir, name));
                }
                string missingExe = Path.Combine(missingDir, "Flix.exe");
                if (ContextMenuNext.ContentIconsReady(missingExe, pandoc)) return false;
                var untouched = new TouchRegistry();
                bool missingRefused = false;
                try { ContextMenuNext.RegisterContentMenu(pandocPdf, missingExe, untouched); }
                catch (IOException ex) { missingRefused = ex.Message.Contains("Icone non disponibili", StringComparison.Ordinal); }
                if (!missingRefused || untouched.Calls != 0) return false;
                var blocked = new MemoryRegistryStore();
                try { ContextMenuNext.RegisterContentMenu(pandocPdf, missingExe, blocked); }
                catch (IOException) { }
                if (blocked.KeyCount != 0) return false;
            }
            finally { try { Directory.Delete(missingDir, true); } catch (IOException) { } }

            var png = ContextMenuNext.PlanImageMenus(native).Single(plan => plan.Extension == ".png");
            var imageMemory = new MemoryRegistryStore();
            ContextMenuNext.RegisterImageMenu(png, exe, imageMemory);
            return imageMemory.KeyExists(ContextMenuNext.ParentKey);
        }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }

    static bool ContentMenuProperties(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc, string exe)
    {
        foreach (var engines in new[] { native, pandoc })
        {
            foreach (var blueprint in ContextMenuNext.PlanContentMenus(engines))
            {
                var values = ContextMenuNext.DescribeContentMenu(blueprint, exe);
                string parent = blueprint.ParentKey;
                if (Value(values, parent, "MUIVerb") != "Converti con FLIX") return false;
                if (Value(values, parent, "ExtendedSubCommandsKey") != blueprint.SubCommandsPath) return false;
                if (Value(values, parent, "MultiSelectModel") != "Single") return false;
                if (Value(values, parent, "NeverDefault") != "") return false;
                if (Value(values, parent, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                if (values.Any(value => value.Key.Equals(parent, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0)) return false;
                if (values.Any(value => value.Key.Equals(parent + @"\command", StringComparison.OrdinalIgnoreCase))) return false;
                if (blueprint.SharesCommandTree)
                {
                    if (values.Count != 6 || !Value(values, parent, "Icon").EndsWith(ContextMenuNext.ParentIconFileName, StringComparison.OrdinalIgnoreCase)) return false;
                    continue;
                }
                if (Value(values, parent + @"\shell", ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                foreach (string format in blueprint.Formats)
                {
                    string verb = parent + @"\shell\" + format.ToLowerInvariant();
                    if (Value(values, verb, "MUIVerb") != "Converti in " + format) return false;
                    if (Value(values, verb, "MultiSelectModel") != "Single") return false;
                    if (Value(values, verb, "NeverDefault") != "") return false;
                    if (Value(values, verb, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                    if (!Value(values, verb, "Icon").EndsWith(ContextMenuNext.IconFileName(format), StringComparison.OrdinalIgnoreCase)) return false;
                    if (Value(values, verb + @"\command", "") != "\"" + exe + "\" --convert-menu " + format + " --files \"%1\"") return false;
                    if (Value(values, verb + @"\command", ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                }
            }
        }
        return true;
    }

    static string Value(IReadOnlyList<RegistryValue> values, string key, string name) =>
        values.Single(value => value.Key.Equals(key, StringComparison.OrdinalIgnoreCase) && value.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Data;

    static void AddContentMenuInstallChecks(Dictionary<string, bool> checks)
    {
        var native = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        var pandoc = new ContextMenuCatalog.EngineSnapshot(true, false, false);
        string photo = @"C:\Documenti\nota.pdf";
        Check(checks, "content_install_native_43", () => ContentInstallNative(native));
        Check(checks, "content_install_pandoc_59", () => ContentInstallPandoc(native, pandoc));
        Check(checks, "content_yaml_yml_one_command_tree", () => ContentInstalledAlias(native, pandoc));
        Check(checks, "content_install_repeat_no_duplicates", () => ContentInstallRepeat(native));
        Check(checks, "content_remove_only_owned_keys", () => ContentRemoveOwnedOnly(native));
        Check(checks, "content_foreign_owner_preserved", () => ContentForeignOwnerPreserved(native, pandoc));
        Check(checks, "content_install_error_restores_absent_and_existing", () => ContentInstallErrorRestores(native));
        Check(checks, "content_remove_error_restores_previous_state", () => ContentRemoveErrorRestores(native, pandoc));
        Check(checks, "content_missing_icon_refuses_before_write", () => ContentMissingIconRefuses(native, pandoc));
        Check(checks, "content_unauthorized_path_refused", () => ContentUnauthorizedPathRefused(native));
        Check(checks, "content_cli_rejects_combinations", () => ContentCliRejectsCombinations(photo));
        Check(checks, "content_protects_existing_reserved_menus", () => ContentProtectsExistingMenus(native, pandoc));
        Check(checks, "content_ipc_mutex_pipe_unchanged", () => ContentIpcUnchanged(photo));
    }

    static int RunContentCommand(string[] args, string exe, IRegistryStore store, ContextMenuCatalog.EngineSnapshot engines, out string error)
    {
        var buffer = new StringWriter();
        int code = ContextMenuNext.ApplyContent(args, exe, store, engines, buffer);
        error = buffer.ToString();
        return code;
    }

    static bool ContentMenusMatch(MemoryRegistryStore store, string exe, ContextMenuCatalog.EngineSnapshot engines, int expectedDestinations, int foreignKeys)
    {
        var blueprints = ContextMenuNext.PlanContentMenus(engines);
        if (blueprints.Length != 7 || blueprints.Sum(plan => plan.Formats.Count) != expectedDestinations) return false;
        int commands = 0;
        var commandKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var blueprint in blueprints)
        {
            if (store.Read(blueprint.ParentKey, "MUIVerb") != "Converti con FLIX") return false;
            if (store.Read(blueprint.ParentKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            if (store.Read(blueprint.ParentKey, "") != null) return false;
            if (store.KeyExists(blueprint.ParentKey + @"\command")) return false;
            if (store.Read(blueprint.ParentKey, "ExtendedSubCommandsKey") != blueprint.SubCommandsPath) return false;
            if (store.Read(blueprint.ParentKey, "MultiSelectModel") != "Single") return false;
            if (store.Read(blueprint.ParentKey, "NeverDefault") != "") return false;
            if (blueprint.SharesCommandTree)
            {
                if (store.KeyExists(blueprint.ParentKey + @"\shell")) return false;
                continue;
            }
            if (store.Read(blueprint.ParentKey + @"\shell", ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            string[] policy = Policy.OrderFormats(Policy.Outputs(["flix.context" + blueprint.Extension], engines.Pandoc, engines.Office, engines.Ffmpeg));
            if (!blueprint.Formats.SequenceEqual(policy)) return false;
            foreach (string format in blueprint.Formats)
            {
                string verb = blueprint.ParentKey + @"\shell\" + format.ToLowerInvariant();
                string commandKey = verb + @"\command";
                if (!commandKeys.Add(commandKey)) return false;
                if (store.Read(commandKey, "") != "\"" + Path.GetFullPath(exe) + "\" --convert-menu " + format + " --files \"%1\"") return false;
                if (store.Read(commandKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                string icon = store.Read(verb, "Icon") ?? "";
                if (!icon.EndsWith(ContextMenuNext.IconFileName(format), StringComparison.OrdinalIgnoreCase)) return false;
                if (!icon.EndsWith("-premium-v2.ico", StringComparison.OrdinalIgnoreCase)) return false;
                if (store.Read(verb, "MUIVerb") != "Converti in " + format) return false;
                if (store.Read(verb, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                if (!MenuConversion.TryParse(["--convert-menu", format, "--files", @"C:\Documenti\nota.pdf"], out var request, out _)) return false;
                if (request.Format != format || request.File != @"C:\Documenti\nota.pdf") return false;
                commands++;
            }
        }
        var yaml = blueprints.Single(plan => plan.Extension == ".yaml");
        var yml = blueprints.Single(plan => plan.Extension == ".yml");
        int expectedCommands = blueprints.Where(plan => !plan.SharesCommandTree).Sum(plan => plan.Formats.Count);
        int expectedKeys = blueprints.Sum(plan => ContextMenuNext.PlannedContentKeys(plan).Count);
        return !yaml.SharesCommandTree
            && yml.SharesCommandTree
            && yaml.Formats.SequenceEqual(yml.Formats)
            && store.Read(yml.ParentKey, "ExtendedSubCommandsKey") == yaml.SubCommandsPath
            && !store.KeyExists(yml.ParentKey + @"\shell")
            && commands == expectedCommands
            && expectedDestinations == expectedCommands + yml.Formats.Count
            && store.KeyCount == expectedKeys + foreignKeys;
    }

    static bool ContentBlueprintInstalled(MemoryRegistryStore store, string exe, ContentMenuBlueprint blueprint)
    {
        if (store.KeyCount != ContextMenuNext.PlannedContentKeys(blueprint).Count) return false;
        if (store.Read(blueprint.ParentKey, "MUIVerb") != "Converti con FLIX") return false;
        if (store.Read(blueprint.ParentKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
        if (store.Read(blueprint.ParentKey, "") != null) return false;
        if (store.KeyExists(blueprint.ParentKey + @"\command")) return false;
        if (blueprint.SharesCommandTree) return !store.KeyExists(blueprint.ParentKey + @"\shell");
        foreach (string format in blueprint.Formats)
        {
            string verb = blueprint.ParentKey + @"\shell\" + format.ToLowerInvariant();
            if (store.Read(verb + @"\command", "") != "\"" + Path.GetFullPath(exe) + "\" --convert-menu " + format + " --files \"%1\"") return false;
            if (store.Read(verb, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            if (store.Read(verb + @"\command", ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
        }
        return true;
    }

    static bool ContentInstallNative(ContextMenuCatalog.EngineSnapshot native)
    {
        string directory = ImageLab("content-native");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            var store = new MemoryRegistryStore();
            int code = RunContentCommand([ContextMenuNext.InstallContentCommand], exe, store, native, out string error);
            if (code != 0 || error.Length != 0 || !ContentMenusMatch(store, exe, native, 43, 0)) return false;
            var office = new MemoryRegistryStore();
            ContextMenuNext.InstallContentMenus(exe, new ContextMenuCatalog.EngineSnapshot(false, true, true), office);
            return office.Export() == store.Export();
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentInstallPandoc(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string directory = ImageLab("content-pandoc");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            var nativeStore = new MemoryRegistryStore();
            ContextMenuNext.InstallContentMenus(exe, native, nativeStore);
            var store = new MemoryRegistryStore();
            int code = RunContentCommand([ContextMenuNext.InstallContentCommand], exe, store, pandoc, out string error);
            if (code != 0 || error.Length != 0 || !ContentMenusMatch(store, exe, pandoc, 59, 0)) return false;
            if (store.KeyCount <= nativeStore.KeyCount) return false;
            var office = new MemoryRegistryStore();
            ContextMenuNext.InstallContentMenus(exe, new ContextMenuCatalog.EngineSnapshot(true, true, true), office);
            int nativeCommands = ContextMenuNext.PlanContentMenus(native).Where(plan => !plan.SharesCommandTree).Sum(plan => plan.Formats.Count);
            int pandocCommands = ContextMenuNext.PlanContentMenus(pandoc).Where(plan => !plan.SharesCommandTree).Sum(plan => plan.Formats.Count);
            return office.Export() == store.Export() && nativeCommands == 36 && pandocCommands == 52;
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentInstalledAlias(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string directory = ImageLab("content-alias");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            foreach (var engines in new[] { native, pandoc })
            {
                var store = new MemoryRegistryStore();
                ContextMenuNext.InstallContentMenus(exe, engines, store);
                var plans = ContextMenuNext.PlanContentMenus(engines);
                var yaml = plans.Single(plan => plan.Extension == ".yaml");
                var yml = plans.Single(plan => plan.Extension == ".yml");
                if (store.Read(yml.ParentKey, "ExtendedSubCommandsKey") != yaml.SubCommandsPath) return false;
                if (store.Read(yaml.ParentKey, "ExtendedSubCommandsKey") != yaml.SubCommandsPath) return false;
                if (store.KeyExists(yml.ParentKey + @"\shell") || store.KeyExists(yml.ParentKey + @"\command")) return false;
                int yamlCommands = 0;
                foreach (string format in yaml.Formats)
                {
                    string yamlCommand = yaml.ParentKey + @"\shell\" + format.ToLowerInvariant() + @"\command";
                    string ymlCommand = yml.ParentKey + @"\shell\" + format.ToLowerInvariant() + @"\command";
                    if (!store.KeyExists(yamlCommand) || store.KeyExists(ymlCommand)) return false;
                    if (store.Read(yamlCommand, "") != "\"" + exe + "\" --convert-menu " + format + " --files \"%1\"") return false;
                    yamlCommands++;
                }
                int treeKeys = ContextMenuNext.PlannedContentKeys(yaml).Count;
                if (yamlCommands != yaml.Formats.Count || ContextMenuNext.PlannedContentKeys(yml).Count != 1) return false;
                if (store.KeyCount != plans.Sum(plan => ContextMenuNext.PlannedContentKeys(plan).Count)) return false;
                if (treeKeys != 2 + yaml.Formats.Count * 2) return false;
            }
            return true;
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentInstallRepeat(ContextMenuCatalog.EngineSnapshot native)
    {
        string directory = ImageLab("content-repeat");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            var store = new MemoryRegistryStore();
            ContextMenuNext.InstallContentMenus(exe, native, store);
            string once = store.Export();
            int count = store.KeyCount;
            int again = RunContentCommand(
                [ContextMenuNext.InstallContentCommand, ContextMenuNext.InstallContentCommand],
                exe, store, native, out string error);
            if (again != 0 || error.Length != 0 || store.Export() != once || store.KeyCount != count) return false;
            var pdf = ContextMenuNext.PlanContentMenus(native).Single(plan => plan.Extension == ".pdf");
            string command = pdf.ParentKey + @"\shell\" + pdf.Formats[0].ToLowerInvariant() + @"\command";
            store.DeleteKey(command);
            if (store.KeyCount != count - 1) return false;
            ContextMenuNext.InstallContentMenus(exe, native, store);
            return store.Export() == once && store.KeyCount == count;
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentRemoveOwnedOnly(ContextMenuCatalog.EngineSnapshot native)
    {
        string directory = ImageLab("content-remove");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            var store = new MemoryRegistryStore();
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            ContextMenuNext.InstallContentMenus(exe, native, store);
            var pdf = ContextMenuNext.PlanContentMenus(native).Single(plan => plan.Extension == ".pdf");
            string decoy = pdf.ParentKey + @"\shell\custom";
            store.Write(decoy, "MUIVerb", "Esterno");
            store.Write(decoy, ContextMenuNext.OwnerValue, "Altro");
            string csv = ContextMenuNext.ContentParentKeyFor(".csv");
            store.Write(csv, ContextMenuNext.OwnerValue, "AltroProgramma");
            store.Write(csv, "MUIVerb", "Non toccare");
            int removed = RunContentCommand([ContextMenuNext.RemoveContentCommand], exe, store, native, out string error);
            int removedAgain = RunContentCommand([ContextMenuNext.RemoveContentCommand], exe, store, native, out string againError);
            bool kept = removed == 0 && error.Length == 0
                && removedAgain == 0 && againError.Length == 0
                && store.KeyExists(pdf.ParentKey)
                && store.KeyExists(decoy)
                && store.Read(decoy, "MUIVerb") == "Esterno"
                && store.Read(csv, ContextMenuNext.OwnerValue) == "AltroProgramma"
                && store.Read(csv, "MUIVerb") == "Non toccare"
                && store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") == LegacyIdentity.OriginalMenuCommand
                && !store.KeyExists(ContextMenuNext.ContentParentKeyFor(".txt"))
                && !store.KeyExists(ContextMenuNext.ContentParentKeyFor(".json"))
                && !store.KeyExists(ContextMenuNext.ContentParentKeyFor(".yaml"))
                && !store.KeyExists(ContextMenuNext.ContentParentKeyFor(".yml"))
                && !store.KeyExists(ContextMenuNext.ContentParentKeyFor(".xml"));
            string stable = store.Export();
            ContextMenuNext.RemoveContentMenus(exe, store);
            if (store.Export() != stable) return false;
            var clean = new MemoryRegistryStore();
            ContextMenuNext.InstallContentMenus(exe, native, clean);
            int installed = clean.KeyCount;
            ContextMenuNext.RemoveContentMenus(clean);
            ContextMenuNext.RemoveContentMenus(clean);
            return kept && installed > 0 && clean.KeyCount == 0;
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentForeignOwnerPreserved(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string directory = ImageLab("content-foreign");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            foreach (var engines in new[] { native, pandoc })
            {
                var alias = new MemoryRegistryStore();
                string yml = ContextMenuNext.ContentParentKeyFor(".yml");
                alias.Write(yml, ContextMenuNext.OwnerValue, "Altro");
                alias.Write(yml, "MUIVerb", "Non toccare");
                if (!ContentInstallLeavesStore(exe, engines, alias)) return false;
                var command = new MemoryRegistryStore();
                string verb = ContextMenuNext.ContentParentKeyFor(".xml") + @"\shell\txt\command";
                command.Write(verb, "", "notepad.exe \"%1\"");
                if (!ContentInstallLeavesStore(exe, engines, command)) return false;
            }
            return true;
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentInstallLeavesStore(string exe, ContextMenuCatalog.EngineSnapshot engines, MemoryRegistryStore store)
    {
        string before = store.Export();
        try { ContextMenuNext.InstallContentMenus(exe, engines, store); return false; }
        catch (IOException ex)
        {
            return ex.Message.Contains("preservata", StringComparison.Ordinal)
                && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase)
                && store.Export() == before;
        }
    }

    static bool ContentInstallErrorRestores(ContextMenuCatalog.EngineSnapshot native)
    {
        string directory = ImageLab("content-write");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            string pdf = ContextMenuNext.ContentParentKeyFor(".pdf");
            string txt = ContextMenuNext.ContentParentKeyFor(".txt");
            string yamlShell = ContextMenuNext.ContentParentKeyFor(".yaml") + @"\shell";
            Exception[] failures =
            [
                new IOException("errore simulato"),
                new UnauthorizedAccessException("errore simulato"),
                new SecurityException("errore simulato"),
                new InvalidOperationException("errore simulato")
            ];
            foreach (var failure in failures)
            {
                var absent = new MemoryRegistryStore();
                absent.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
                string beforeAbsent = absent.Export();
                var fail = new FailAfterWriteStore(absent) { FailOn = 4, Failure = failure };
                bool threw = false;
                try { ContextMenuNext.InstallContentMenus(exe, native, fail); }
                catch (Exception ex) when (ex.GetType() == failure.GetType())
                {
                    threw = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                        && !ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                        && !ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                        && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
                }
                if (!threw || absent.Export() != beforeAbsent) return false;
                if (absent.KeyExists(pdf) || absent.KeyExists(txt) || absent.KeyExists(yamlShell)) return false;
                if (absent.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;
            }

            var pdfPlan = ContextMenuNext.PlanContentMenus(native).Single(plan => plan.Extension == ".pdf");
            var existing = new MemoryRegistryStore();
            existing.Write(pdf, "MUIVerb", "Personalizzato");
            existing.Write(pdf, "ExtendedSubCommandsKey", pdfPlan.SubCommandsPath);
            existing.Write(pdf, "MultiSelectModel", "Single");
            existing.Write(pdf, "NeverDefault", "");
            existing.Write(pdf, ContextMenuNext.OwnerValue, pdfPlan.MenuId);
            existing.Write(pdf, "Nota", "tenere");
            existing.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            if (existing.KeyExists(pdf + @"\shell") || existing.KeyExists(txt)) return false;
            string beforeExisting = existing.Export();
            var interrupted = new FailAfterWriteStore(existing) { FailOn = 12 };
            bool interruptedThrow = false;
            try { ContextMenuNext.InstallContentMenus(exe, native, interrupted); }
            catch (IOException ex)
            {
                interruptedThrow = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
            }
            if (!interruptedThrow || existing.Export() != beforeExisting) return false;
            if (existing.KeyExists(pdf + @"\shell") || existing.KeyExists(txt) || existing.KeyExists(yamlShell)) return false;
            if (existing.Read(pdf, "MUIVerb") != "Personalizzato" || existing.Read(pdf, "Nota") != "tenere") return false;

            var partial = new MemoryRegistryStore();
            partial.Write(pdf, "MUIVerb", "Personalizzato");
            partial.Write(pdf, "ExtendedSubCommandsKey", pdfPlan.SubCommandsPath);
            partial.Write(pdf, "MultiSelectModel", "Single");
            partial.Write(pdf, "NeverDefault", "");
            partial.Write(pdf, ContextMenuNext.OwnerValue, pdfPlan.MenuId);
            partial.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            var corrupt = new FailAfterWriteStore(partial) { FailOn = 12, CorruptRestoreWrites = true };
            bool partialSignaled = false;
            try { ContextMenuNext.InstallContentMenus(exe, native, corrupt); }
            catch (IOException ex)
            {
                partialSignaled = ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                    && ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
            }
            return partialSignaled
                && partial.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && partial.Export().Contains("#parziale", StringComparison.Ordinal);
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentRemoveErrorRestores(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string directory = ImageLab("content-remove-error");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            var pdfOnly = new MemoryRegistryStore();
            var pdf = ContextMenuNext.PlanContentMenus(native).Single(plan => plan.Extension == ".pdf");
            ContextMenuNext.RegisterContentMenu(pdf, exe, pdfOnly);
            pdfOnly.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string txt = ContextMenuNext.ContentParentKeyFor(".txt");
            string extraFormat = ContextMenuNext.PlanContentMenus(pandoc).Single(plan => plan.Extension == ".pdf").Formats
                .First(format => !pdf.Formats.Contains(format, StringComparer.Ordinal));
            string extraKey = pdf.ParentKey + @"\shell\" + extraFormat.ToLowerInvariant();
            if (pdfOnly.KeyExists(txt) || pdfOnly.KeyExists(extraKey)) return false;
            string beforePdf = pdfOnly.Export();
            var pdfFail = new FailAfterDeleteStore(pdfOnly) { FailOn = 1 };
            bool pdfThrew = false;
            try { ContextMenuNext.RemoveContentMenus(exe, pdfFail); }
            catch (IOException ex)
            {
                pdfThrew = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
            }
            if (!pdfThrew || pdfOnly.Export() != beforePdf) return false;
            if (pdfOnly.KeyExists(txt) || pdfOnly.KeyExists(extraKey)) return false;
            if (pdfOnly.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;
            if (pdfOnly.Read(pdf.ParentKey, ContextMenuNext.OwnerValue) != pdf.MenuId) return false;

            Exception[] failures =
            [
                new IOException("errore simulato"),
                new UnauthorizedAccessException("errore simulato"),
                new SecurityException("errore simulato")
            ];
            foreach (var failure in failures)
            {
                var installed = new MemoryRegistryStore();
                ContextMenuNext.InstallContentMenus(exe, native, installed);
                installed.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
                string before = installed.Export();
                var interrupted = new FailAfterDeleteStore(installed) { FailOn = 1, Failure = failure };
                bool threw = false;
                try { ContextMenuNext.RemoveContentMenus(exe, interrupted); }
                catch (Exception ex) when (ex.GetType() == failure.GetType())
                {
                    threw = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                        && !ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                        && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
                }
                if (!threw || installed.Export() != before) return false;
                if (installed.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;
            }

            var partial = new MemoryRegistryStore();
            ContextMenuNext.InstallContentMenus(exe, pandoc, partial);
            partial.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            var failedRestore = new FailAfterDeleteStore(partial) { FailOn = 1, FailRestore = true };
            bool signaled = false;
            try { ContextMenuNext.RemoveContentMenus(exe, failedRestore); }
            catch (IOException ex)
            {
                signaled = ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                    && ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && ex.Message.Contains("ripristino simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase)
                    && ex.InnerException is IOException;
            }
            return signaled && partial.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb;
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentMissingIconRefuses(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string missingTxt = ImageLab("content-missing-txt");
        string missingDocx = ImageLab("content-missing-docx");
        string corrupt = ImageLab("content-corrupt-yaml");
        try
        {
            Directory.CreateDirectory(missingTxt);
            Directory.CreateDirectory(missingDocx);
            Directory.CreateDirectory(corrupt);
            if (!CopyPremiumIconsSelective(missingTxt, "flix-menu-txt-premium-v2.ico")) return false;
            if (!CopyPremiumIconsSelective(missingDocx, "flix-menu-docx-premium-v2.ico")) return false;
            if (!CopyPremiumV2Icons(corrupt)) return false;
            File.WriteAllBytes(Path.Combine(corrupt, "flix-menu-yaml-premium-v2.ico"), [1, 2, 3, 4]);
            string txtExe = Path.GetFullPath(Path.Combine(missingTxt, "Flix.exe"));
            string docxExe = Path.GetFullPath(Path.Combine(missingDocx, "Flix.exe"));
            string corruptExe = Path.GetFullPath(Path.Combine(corrupt, "Flix.exe"));
            if (ContextMenuNext.ContentIconsReady(txtExe, native) || ContextMenuNext.ContentIconsReady(docxExe, pandoc)) return false;
            if (ContextMenuNext.ContentIconsReady(corruptExe, native)) return false;
            var probes = new (string Exe, ContextMenuCatalog.EngineSnapshot Engines)[]
            {
                (txtExe, native),
                (docxExe, pandoc),
                (corruptExe, native),
                (corruptExe, pandoc)
            };
            foreach (var probe in probes)
            {
                var touch = new TouchRegistry();
                bool refused = false;
                try { ContextMenuNext.InstallContentMenus(probe.Exe, probe.Engines, touch); }
                catch (IOException ex) { refused = ex.Message.Contains("Icone non disponibili", StringComparison.Ordinal); }
                if (!refused || touch.Calls != 0) return false;
                var memory = new MemoryRegistryStore();
                try { ContextMenuNext.InstallContentMenus(probe.Exe, probe.Engines, memory); }
                catch (IOException) { }
                if (memory.KeyCount != 0) return false;
                int code = RunContentCommand([ContextMenuNext.InstallContentCommand], probe.Exe, touch, probe.Engines, out string error);
                if (code == 0 || touch.Calls != 0 || !error.Contains("Icone non disponibili", StringComparison.Ordinal)) return false;
                if (error.Contains("garantito", StringComparison.OrdinalIgnoreCase)) return false;
            }
            var pandocPdf = ContextMenuNext.PlanContentMenus(pandoc).Single(plan => plan.Extension == ".pdf");
            var single = new TouchRegistry();
            bool singleRefused = false;
            try { ContextMenuNext.RegisterContentMenu(pandocPdf, docxExe, single); }
            catch (IOException ex) { singleRefused = ex.Message.Contains("Icone non disponibili", StringComparison.Ordinal); }
            return singleRefused && single.Calls == 0;
        }
        finally
        {
            DeleteLab(missingTxt);
            DeleteLab(missingDocx);
            DeleteLab(corrupt);
        }
    }

    static bool CopyPremiumIconsSelective(string directory, string omit)
    {
        if (!CopyParentIcon(directory)) return false;
        foreach (string name in PremiumV2IconFiles)
        {
            if (name.Equals(omit, StringComparison.OrdinalIgnoreCase)) continue;
            string source = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(source) || !ContextMenuPreview.IconIsValid(source)) return false;
            File.Copy(source, Path.Combine(directory, name), true);
        }
        return true;
    }

    static bool ContentUnauthorizedPathRefused(ContextMenuCatalog.EngineSnapshot native)
    {
        string[] rejected =
        [
            @"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe",
            @"C:\dev\FLIX\src\Flix\obj\Release\net10.0-windows\Flix.exe",
            Path.Combine(Path.GetTempPath(), "Flix.exe"),
            @"C:\Programmi\Altro\Flix.exe",
            Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "Flix.dll"),
            ""
        ];
        var store = new TouchRegistry();
        foreach (string path in rejected)
        {
            int install = RunContentCommand([ContextMenuNext.InstallContentCommand], path, store, native, out string installError);
            int remove = RunContentCommand([ContextMenuNext.RemoveContentCommand], path, store, native, out string removeError);
            if (install == 0 || remove == 0 || store.Calls != 0) return false;
            if (installError.Length == 0 || removeError.Length == 0) return false;
            if (installError.Contains("garantito", StringComparison.OrdinalIgnoreCase)) return false;
            bool installThrew = false;
            bool removeThrew = false;
            try { ContextMenuNext.InstallContentMenus(path, native, store); }
            catch (IOException) { installThrew = true; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or InvalidOperationException or ArgumentException) { installThrew = true; }
            try { ContextMenuNext.RemoveContentMenus(path, store); }
            catch (IOException) { removeThrew = true; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or InvalidOperationException or ArgumentException) { removeThrew = true; }
            if (!installThrew || !removeThrew || store.Calls != 0) return false;
        }
        return true;
    }

    static bool ContentCliRejectsCombinations(string photo)
    {
        var engines = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        string[] extras =
        [
            "--install-context-menu-images", "--remove-context-menu-images",
            "--install-context-menu-png", "--remove-context-menu-png",
            "--install-context-menu-preview", "--remove-context-menu-preview",
            "--install-context-menu", "--remove-context-menu",
            "--convert-menu", "--convert", "--test-input", "--files", photo
        ];
        var store = new TouchRegistry();
        int both = RunContentCommand(
            [ContextMenuNext.InstallContentCommand, ContextMenuNext.RemoveContentCommand],
            @"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe",
            store, engines, out string bothError);
        if (both == 0 || store.Calls != 0 || !bothError.Contains("Nessuna modifica al Registro", StringComparison.Ordinal)) return false;
        foreach (string extra in extras)
        {
            int install = RunContentCommand([ContextMenuNext.InstallContentCommand, extra], @"C:\Programmi\Altro\Flix.exe", store, engines, out string installError);
            int remove = RunContentCommand([extra, ContextMenuNext.RemoveContentCommand], @"C:\Programmi\Altro\Flix.exe", store, engines, out string removeError);
            if (install == 0 || remove == 0 || store.Calls != 0) return false;
            if (!installError.Contains(extra, StringComparison.Ordinal) || !installError.Contains("Nessuna modifica al Registro", StringComparison.Ordinal)) return false;
            if (!removeError.Contains(extra, StringComparison.Ordinal) || removeError.Contains("garantito", StringComparison.OrdinalIgnoreCase)) return false;
        }
        var imageStore = new TouchRegistry();
        int imageBlocked = ContextMenuNext.ApplyImages(
            [ContextMenuNext.InstallImagesCommand, ContextMenuNext.InstallContentCommand],
            @"C:\Programmi\Altro\Flix.exe",
            imageStore,
            engines,
            new StringWriter());
        return imageBlocked != 0
            && imageStore.Calls == 0
            && store.Calls == 0
            && ContextMenuNext.IsContentMenuCommand([ContextMenuNext.InstallContentCommand])
            && ContextMenuNext.IsContentMenuCommand([ContextMenuNext.RemoveContentCommand])
            && !ContextMenuNext.IsContentMenuCommand([ContextMenuNext.InstallImagesCommand])
            && !ContextMenuNext.IsContentMenuCommand(["--install-context-menu-png"])
            && !ContextMenuNext.IsContentMenuCommand(["--convert-menu", "PDF", "--files", photo])
            && !ContextMenuNext.IsImageMenuCommand([ContextMenuNext.InstallContentCommand])
            && !ContextMenuNext.IsPlannedCommand([ContextMenuNext.RemoveContentCommand])
            && !ContextMenuPreview.IsPreviewCommand([ContextMenuNext.InstallContentCommand])
            && !Program.OpensWheelOnStartup([ContextMenuNext.InstallContentCommand])
            && !Program.OpensPanelOnStartup([ContextMenuNext.RemoveContentCommand])
            && !Program.OpensWheelOnStartup([ContextMenuNext.InstallContentCommand, "--files", photo])
            && !Program.OpensPanelOnStartup([ContextMenuNext.RemoveContentCommand, "--multi", "--files", photo])
            && !Program.OpensWheelOnStartup(["--convert-menu", "PDF", "--files", photo])
            && !Program.OpensPanelOnStartup(["--convert-menu", "TXT", "--multi", "--files", photo])
            && !Program.OpensWheelOnStartup(["--files", photo])
            && !Program.OpensPanelOnStartup(["--multi", "--files", photo]);
    }

    static bool ContentProtectsExistingMenus(ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string directory = ImageLab("content-protect");
        try
        {
            Directory.CreateDirectory(directory);
            if (!CopyPremiumV2Icons(directory)) return false;
            string exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
            foreach (var engines in new[] { native, pandoc })
            {
                var store = new MemoryRegistryStore();
                ContextMenuNext.InstallImageMenus(exe, native, store);
                ContextMenuPreview.Install(PreviewPlan(native, exe), store);
                store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
                store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
                string before = store.Export();
                int protectedKeys = store.KeyCount;
                ContextMenuNext.InstallContentMenus(exe, engines, store);
                if (store.KeyCount <= protectedKeys) return false;
                if (store.Read(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) != ContextMenuNext.MenuId) return false;
                if (store.Read(ContextMenuNext.ParentKeyFor(".jpg"), ContextMenuNext.OwnerValue) != ContextMenuNext.MenuIdFor(".jpg")) return false;
                if (store.Read(ContextMenuNext.ParentKeyFor(".jpeg"), "ExtendedSubCommandsKey") != ContextMenuNext.SubCommandsPathFor(".jpg")) return false;
                if (store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) != ContextMenuPreview.PrototypeId) return false;
                if (store.Read(ContextMenuPreview.ParentKey, "MUIVerb") != ContextMenuPreview.ParentVerb) return false;
                if (store.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;
                if (store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") != LegacyIdentity.OriginalMenuCommand) return false;
                ContextMenuNext.RemoveContentMenus(exe, store);
                ContextMenuNext.RemoveContentMenus(exe, store);
                if (store.Export() != before) return false;
                if (store.KeyExists(ContextMenuNext.ContentParentKeyFor(".pdf"))) return false;
                if (store.KeyExists(ContextMenuNext.ContentParentKeyFor(".yml"))) return false;
            }
            return true;
        }
        finally { DeleteLab(directory); }
    }

    static bool ContentIpcUnchanged(string photo)
    {
        return MenuConversion.TryParse(["--convert-menu", "PDF", "--files", photo], out var request, out _)
            && request.Format == "PDF"
            && request.File == photo
            && !Program.StartsResidentHost([ContextMenuNext.InstallContentCommand])
            && !Program.StartsResidentHost([ContextMenuNext.RemoveContentCommand])
            && !Program.OpensWheelOnStartup(["--files", photo])
            && !Program.OpensPanelOnStartup(["--multi", "--files", photo]);
    }

    static void AddMediaMenuBlueprintChecks(Dictionary<string, bool> checks)
    {
        var ffmpeg = new ContextMenuCatalog.EngineSnapshot(false, false, true);
        var absent = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        string exe = @"C:\Programmi\Altro\Flix.exe";
        Check(checks, "media_fifteen_ids_and_extensions", () => MediaFifteenIds(ffmpeg));
        Check(checks, "media_destinations_42_audio_44_video", () => MediaDestinationCounts(ffmpeg));
        Check(checks, "media_no_alias_identity_or_forbidden", () => MediaNoAliasOrForbidden(ffmpeg));
        Check(checks, "media_order_matches_catalog", () => MediaOrderMatchesCatalog());
        Check(checks, "media_ffmpeg_absent_pandoc_office_invariant", () => MediaEngineIndependence(ffmpeg, absent));
        Check(checks, "media_commands_owner_hkcu", () => MediaCommandsOwnerAndKeys(ffmpeg, exe));
        Check(checks, "media_icons_do_not_block_blueprint", () => MediaIconsDoNotBlockBlueprint(ffmpeg));
        Check(checks, "media_icons_ready_gate", () => MediaIconsReadyGate());
        Check(checks, "media_no_collision_prior_blueprints", () => MediaLeavesPriorBlueprints(ffmpeg));
    }

    static bool MediaFifteenIds(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        string[] extensions =
        [
            ".mp3", ".wav", ".flac", ".ogg", ".m4a", ".aac", ".wma", ".opus",
            ".mp4", ".mkv", ".webm", ".avi", ".mov", ".wmv", ".m4v"
        ];
        string[] ids =
        [
            "Flix.Convert.Mp3", "Flix.Convert.Wav", "Flix.Convert.Flac", "Flix.Convert.Ogg",
            "Flix.Convert.M4a", "Flix.Convert.Aac", "Flix.Convert.Wma", "Flix.Convert.Opus",
            "Flix.Convert.Mp4", "Flix.Convert.Mkv", "Flix.Convert.Webm", "Flix.Convert.Avi",
            "Flix.Convert.Mov", "Flix.Convert.Wmv", "Flix.Convert.M4v"
        ];
        if (!ContextMenuNext.MediaExtensions.SequenceEqual(extensions)) return false;
        if (!Policy.Audio.Concat(Policy.Video).SequenceEqual(extensions)) return false;
        if (!ContextMenuNext.MediaExtensions.Take(Policy.Audio.Length).SequenceEqual(Policy.Audio)) return false;
        if (!ContextMenuNext.MediaExtensions.Skip(Policy.Audio.Length).SequenceEqual(Policy.Video)) return false;
        var plans = ContextMenuNext.PlanMediaMenus(ffmpeg);
        if (plans.Length != 15 || !plans.Select(plan => plan.Extension).SequenceEqual(extensions)) return false;
        if (!plans.Select(plan => plan.MenuId).SequenceEqual(ids)) return false;
        if (plans.Select(plan => plan.MenuId).Distinct(StringComparer.Ordinal).Count() != 15) return false;
        if (plans.Any(plan => plan.SharesCommandTree || plan.CommandParentKey != plan.ParentKey)) return false;
        foreach (var plan in plans)
        {
            if (plan.MenuId != ContextMenuNext.MediaMenuIdFor(plan.Extension)) return false;
            if (plan.MenuId != ContextMenuNext.MediaMenuIdFor(plan.Extension.TrimStart('.').ToUpperInvariant())) return false;
            if (plan.ParentKey != ContextMenuNext.MediaParentKeyFor(plan.Extension)) return false;
            if (plan.SubCommandsPath != ContextMenuNext.MediaSubCommandsPathFor(plan.Extension)) return false;
        }
        bool imageRejected = ThrowsIo(() => ContextMenuNext.MenuIdFor(".mp3")) && ThrowsIo(() => ContextMenuNext.MenuIdFor(".mp4"));
        bool contentRejected = ThrowsIo(() => ContextMenuNext.ContentMenuIdFor(".mp3")) && ThrowsIo(() => ContextMenuNext.ContentMenuIdFor(".webm"));
        bool parentRejected = ThrowsIo(() => ContextMenuNext.ParentKeyFor(".wav")) && ThrowsIo(() => ContextMenuNext.ContentParentKeyFor(".mkv"));
        bool mediaRejected = ThrowsIo(() => ContextMenuNext.MediaMenuIdFor(".png"))
            && ThrowsIo(() => ContextMenuNext.MediaMenuIdFor(".pdf"))
            && ThrowsIo(() => ContextMenuNext.MediaMenuIdFor(".yml"))
            && ThrowsIo(() => ContextMenuNext.MediaMenuIdFor(".jpeg"));
        return imageRejected && contentRejected && parentRejected && mediaRejected
            && ContextMenuNext.MediaMenuIdFor(".MP3") == "Flix.Convert.Mp3"
            && ContextMenuNext.MediaMenuIdFor("m4v") == "Flix.Convert.M4v";
    }

    static bool MediaDestinationCounts(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        var expected = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            [".mp3"] = ["WAV", "FLAC", "OGG", "M4A", "AAC"],
            [".wav"] = ["MP3", "FLAC", "OGG", "M4A", "AAC"],
            [".flac"] = ["MP3", "WAV", "OGG", "M4A", "AAC"],
            [".ogg"] = ["MP3", "WAV", "FLAC", "M4A", "AAC"],
            [".m4a"] = ["MP3", "WAV", "FLAC", "OGG", "AAC"],
            [".aac"] = ["MP3", "WAV", "FLAC", "OGG", "M4A"],
            [".wma"] = ["MP3", "WAV", "FLAC", "OGG", "M4A", "AAC"],
            [".opus"] = ["MP3", "WAV", "FLAC", "OGG", "M4A", "AAC"],
            [".mp4"] = ["MP3", "WAV", "MKV", "WEBM", "AVI", "MOV"],
            [".mkv"] = ["MP4", "MP3", "WAV", "WEBM", "AVI", "MOV"],
            [".webm"] = ["MP4", "MP3", "WAV", "MKV", "AVI", "MOV"],
            [".avi"] = ["MP4", "MP3", "WAV", "MKV", "WEBM", "MOV"],
            [".mov"] = ["MP4", "MP3", "WAV", "MKV", "WEBM", "AVI"],
            [".wmv"] = ["MP4", "MP3", "WAV", "MKV", "WEBM", "AVI", "MOV"],
            [".m4v"] = ["MP4", "MP3", "WAV", "MKV", "WEBM", "AVI", "MOV"]
        };
        var plans = ContextMenuNext.PlanMediaMenus(ffmpeg);
        if (plans.Length != expected.Count) return false;
        int audio = 0, video = 0;
        foreach (var plan in plans)
        {
            string[] catalog = ContextMenuCatalog.ForExtension(plan.Extension, ffmpeg).Select(entry => entry.TargetFormat).ToArray();
            string[] policy = Policy.OrderFormats(Policy.Outputs(["flix.context" + plan.Extension], ffmpeg.Pandoc, ffmpeg.Office, ffmpeg.Ffmpeg));
            if (!expected.TryGetValue(plan.Extension, out string[]? targets) || !plan.Formats.SequenceEqual(targets)) return false;
            if (!plan.Formats.SequenceEqual(catalog) || !plan.Formats.SequenceEqual(policy)) return false;
            if (Policy.Audio.Contains(plan.Extension)) audio += plan.Formats.Count;
            else if (Policy.Video.Contains(plan.Extension)) video += plan.Formats.Count;
            else return false;
        }
        return audio == 42 && video == 44 && audio + video == 86;
    }

    static bool MediaNoAliasOrForbidden(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        string[] audioAllowed = ["WAV", "MP3", "FLAC", "OGG", "M4A", "AAC"];
        string[] videoAllowed = ["MP4", "MP3", "WAV", "MKV", "WEBM", "AVI", "MOV"];
        string[] banned =
        [
            "PNG", "JPG", "JPEG", "WEBP", "BMP", "TIFF", "GIF", "PDF", "TXT", "HTML", "MD",
            "JSON", "YAML", "XML", "CSV", "DOCX", "WMA", "OPUS", "WMV", "M4V",
            "DXF", "PAGES", "PPT", "PPTX", "PDF/A"
        ];
        var plans = ContextMenuNext.PlanMediaMenus(ffmpeg);
        if (plans.Count(plan => plan.SharesCommandTree) != 0) return false;
        var paths = plans.Select(plan => plan.SubCommandsPath).ToArray();
        if (paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() != plans.Length) return false;
        foreach (var plan in plans)
        {
            string source = plan.Extension.TrimStart('.').ToUpperInvariant();
            string[] allowed = Policy.Audio.Contains(plan.Extension) ? audioAllowed : videoAllowed;
            if (plan.Formats.Contains(source, StringComparer.Ordinal)) return false;
            if (plan.Formats.Distinct(StringComparer.Ordinal).Count() != plan.Formats.Count) return false;
            if (plan.Formats.Any(format => !allowed.Contains(format, StringComparer.Ordinal))) return false;
            if (plan.Formats.Any(format => banned.Contains(format, StringComparer.Ordinal))) return false;
            if (!plan.SubCommandsPath.Contains(@"\" + plan.Extension + @"\shell\" + plan.MenuId, StringComparison.Ordinal)) return false;
            if (plan.Entries.Any(entry => entry.SourceExtension != plan.Extension)) return false;
        }
        return plans.SelectMany(plan => plan.Entries).Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count() == 86;
    }

    static bool MediaOrderMatchesCatalog()
    {
        foreach (var engines in ContextMenuCatalog.EngineSnapshot.AllCombinations())
        {
            var plans = ContextMenuNext.PlanMediaMenus(engines);
            if (!engines.Ffmpeg)
            {
                if (plans.Length != 0) return false;
                continue;
            }
            if (plans.Length != ContextMenuNext.MediaExtensions.Count) return false;
            foreach (var plan in plans)
            {
                string[] policy = Policy.OrderFormats(Policy.Outputs(["flix.context" + plan.Extension], engines.Pandoc, engines.Office, engines.Ffmpeg));
                string[] catalog = ContextMenuCatalog.ForExtension(plan.Extension, engines).Select(entry => entry.TargetFormat).ToArray();
                if (!plan.Formats.SequenceEqual(policy) || !plan.Formats.SequenceEqual(catalog)) return false;
                if (!plan.Entries.Select(entry => entry.Title).SequenceEqual(plan.Formats.Select(format => "Converti in " + format))) return false;
            }
        }
        return true;
    }

    static bool MediaEngineIndependence(ContextMenuCatalog.EngineSnapshot ffmpeg, ContextMenuCatalog.EngineSnapshot absent)
    {
        var reference = ContextMenuNext.PlanMediaMenus(ffmpeg);
        if (reference.Length != 15 || reference.Sum(plan => plan.Formats.Count) != 86) return false;
        if (ContextMenuNext.PlanMediaMenus(absent).Length != 0) return false;
        foreach (bool pandoc in new[] { false, true })
        foreach (bool office in new[] { false, true })
        {
            var without = ContextMenuNext.PlanMediaMenus(new ContextMenuCatalog.EngineSnapshot(pandoc, office, false));
            if (without.Length != 0 || without.Sum(plan => plan.Formats.Count) != 0) return false;
            var with = ContextMenuNext.PlanMediaMenus(new ContextMenuCatalog.EngineSnapshot(pandoc, office, true));
            if (!MediaSameDestinations(reference, with)) return false;
            var menu = ContextMenuCatalog.BuildMenu(new ContextMenuCatalog.EngineSnapshot(pandoc, office, false));
            var ready = ContextMenuCatalog.BuildMenu(new ContextMenuCatalog.EngineSnapshot(pandoc, office, true));
            foreach (string extension in ContextMenuNext.MediaExtensions)
            {
                if (menu.ContainsKey(extension) || !ready.ContainsKey(extension)) return false;
                if (ContextMenuCatalog.ForExtension(extension, new ContextMenuCatalog.EngineSnapshot(pandoc, office, false)).Length != 0) return false;
                if (Policy.Outputs(["flix.context" + extension], pandoc, office, false).Length != 0) return false;
                if (Policy.Outputs(["flix.context" + extension], pandoc, office, true).Length == 0) return false;
            }
        }
        return true;
    }

    static bool MediaSameDestinations(MediaMenuBlueprint[] left, MediaMenuBlueprint[] right)
    {
        if (left.Length != right.Length) return false;
        for (int index = 0; index < left.Length; index++)
            if (left[index].Extension != right[index].Extension || left[index].MenuId != right[index].MenuId
                || !left[index].Formats.SequenceEqual(right[index].Formats)) return false;
        return true;
    }

    static bool MediaCommandsOwnerAndKeys(ContextMenuCatalog.EngineSnapshot ffmpeg, string exe)
    {
        foreach (var blueprint in ContextMenuNext.PlanMediaMenus(ffmpeg))
        {
            string parent = @"Software\Classes\SystemFileAssociations\" + blueprint.Extension + @"\shell\" + blueprint.MenuId;
            if (blueprint.ParentKey != parent) return false;
            if (@"Software\Classes\" + blueprint.SubCommandsPath != blueprint.ParentKey) return false;
            if (!blueprint.ParentKey.StartsWith(@"Software\Classes\", StringComparison.Ordinal)) return false;
            var values = ContextMenuNext.DescribeMediaMenu(blueprint, exe);
            if (Value(values, parent, "MUIVerb") != "Converti con FLIX") return false;
            if (Value(values, parent, "MUIVerb").Contains("Anteprima", StringComparison.Ordinal)) return false;
            if (Value(values, parent, "ExtendedSubCommandsKey") != blueprint.SubCommandsPath) return false;
            if (Value(values, parent, "MultiSelectModel") != "Single") return false;
            if (Value(values, parent, "NeverDefault") != "") return false;
            if (Value(values, parent, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            if (values.Any(value => value.Key.Equals(parent, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0)) return false;
            if (values.Any(value => value.Key.Equals(parent + @"\command", StringComparison.OrdinalIgnoreCase))) return false;
            if (Value(values, parent + @"\shell", ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            var keys = ContextMenuNext.PlannedMediaKeys(blueprint);
            if (keys.Count != 2 + blueprint.Formats.Count * 2) return false;
            if (keys.Contains(parent + @"\command")) return false;
            foreach (string format in blueprint.Formats)
            {
                string verb = parent + @"\shell\" + format.ToLowerInvariant();
                string command = "\"" + exe + "\" --convert-menu " + format + " --files \"%1\"";
                if (Value(values, verb, "MUIVerb") != "Converti in " + format) return false;
                if (Value(values, verb, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                if (Value(values, verb, "MultiSelectModel") != "Single" || Value(values, verb, "NeverDefault") != "") return false;
                if (Value(values, verb + @"\command", "") != command) return false;
                if (Value(values, verb + @"\command", ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                if (!command.Contains("--convert-menu " + format + " --files \"%1\"", StringComparison.Ordinal)) return false;
                if (command.Contains("--convert ", StringComparison.Ordinal) || command.Contains("--multi", StringComparison.Ordinal)) return false;
                if (!MenuConversion.TryParse(["--convert-menu", format, "--files", @"C:\Audio\brano" + blueprint.Extension], out var request, out _)) return false;
                if (!request.Format.Equals(format, StringComparison.Ordinal) || request.File != @"C:\Audio\brano" + blueprint.Extension) return false;
            }
        }
        return true;
    }

    static bool MediaIconsDoNotBlockBlueprint(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        if (ContextMenuNext.MediaIconFormats.Length != 11
            || ContextMenuNext.MediaIconFormats.Distinct(StringComparer.Ordinal).Count() != 11) return false;
        foreach (string format in ContextMenuNext.MediaIconFormats)
            if (ContextMenuNext.IconFileName(format) != "flix-menu-" + format.ToLowerInvariant() + "-premium-v2.ico") return false;
        if (ContextMenuNext.IconFileName("PNG") != "flix-menu-png-premium-v2.ico"
            || ContextMenuNext.IconFileName("JPG") != ContextMenuPreview.IconFileName("JPG")
            || ContextMenuNext.IconFileName("PDF") != ContextMenuPreview.IconFileName("PDF")
            || ContextMenuNext.IconFileName("WEBP") != ContextMenuPreview.IconFileName("WEBP")
            || ContextMenuNext.IconFileName("TXT") != "flix-menu-txt-premium-v2.ico"
            || ContextMenuNext.IconFileName("GIF") != "flix-menu-gif-premium-v2.ico") return false;
        foreach (string absent in new[] { "WMA", "OPUS", "WMV", "M4V" })
            if (!ThrowsIo(() => ContextMenuNext.IconFileName(absent))) return false;
        string directory = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "media-icons-" + Guid.NewGuid().ToString("N"));
        string isolated = Path.Combine(directory, "Flix.exe");
        var plans = ContextMenuNext.PlanMediaMenus(ffmpeg);
        if (plans.Length != 15) return false;
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plan in plans)
        {
            var values = ContextMenuNext.DescribeMediaMenu(plan, isolated);
            foreach (string format in plan.Formats)
            {
                string icon = values.Single(value => value.Key.EndsWith(@"\shell\" + format.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) && value.Name == "Icon").Data;
                if (!icon.EndsWith(ContextMenuNext.IconFileName(format), StringComparison.OrdinalIgnoreCase)) return false;
                if (File.Exists(icon)) return false;
                used.Add(format);
            }
            if (values.Any(value => value.Name.Equals("Icon", StringComparison.OrdinalIgnoreCase)
                && (value.Data.Contains("flix-menu-wma", StringComparison.OrdinalIgnoreCase)
                    || value.Data.Contains("flix-menu-opus", StringComparison.OrdinalIgnoreCase)
                    || value.Data.Contains("flix-menu-wmv", StringComparison.OrdinalIgnoreCase)
                    || value.Data.Contains("flix-menu-m4v", StringComparison.OrdinalIgnoreCase)))) return false;
        }
        return used.SetEquals(ContextMenuNext.MediaIconFormats)
            && !ContextMenuNext.MediaIconsReady(isolated)
            && !Directory.Exists(directory);
    }

    static readonly string[] MediaPremiumV2IconFiles =
    [
        "flix-menu-mp3-premium-v2.ico", "flix-menu-wav-premium-v2.ico", "flix-menu-flac-premium-v2.ico",
        "flix-menu-ogg-premium-v2.ico", "flix-menu-m4a-premium-v2.ico", "flix-menu-aac-premium-v2.ico",
        "flix-menu-mp4-premium-v2.ico", "flix-menu-mkv-premium-v2.ico", "flix-menu-webm-premium-v2.ico",
        "flix-menu-avi-premium-v2.ico", "flix-menu-mov-premium-v2.ico"
    ];

    static bool MediaIconsReadyGate()
    {
        if (MediaPremiumV2IconFiles.Length != ContextMenuNext.MediaIconFormats.Length) return false;
        for (int index = 0; index < MediaPremiumV2IconFiles.Length; index++)
            if (ContextMenuNext.IconFileName(ContextMenuNext.MediaIconFormats[index]) != MediaPremiumV2IconFiles[index]) return false;
        if (PremiumV2IconFiles.Length != 20) return false;
        foreach (string name in PremiumV2IconFiles.Concat(MediaPremiumV2IconFiles))
        {
            string source = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(source) || !ContextMenuPreview.IconIsValid(source)) return false;
        }
        string directory = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "media-ready-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            foreach (string name in MediaPremiumV2IconFiles)
                File.Copy(Path.Combine(AppContext.BaseDirectory, name), Path.Combine(directory, name));
            if (!CopyParentIcon(directory)) return false;
            string exe = Path.Combine(directory, "Flix.exe");
            if (!ContextMenuNext.MediaIconsReady(exe)) return false;
            File.WriteAllBytes(Path.Combine(directory, "flix-menu-jpg.ico"), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(directory, "flix-menu-pdf.ico"), [1, 2, 3, 4]);
            File.WriteAllBytes(Path.Combine(directory, "flix-menu-webp.ico"), [1, 2, 3, 4]);
            if (!ContextMenuNext.MediaIconsReady(exe)) return false;
            foreach (string omit in MediaPremiumV2IconFiles)
            {
                string missingDir = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "media-miss-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(missingDir);
                try
                {
                    foreach (string name in MediaPremiumV2IconFiles)
                    {
                        if (name.Equals(omit, StringComparison.OrdinalIgnoreCase)) continue;
                        File.Copy(Path.Combine(directory, name), Path.Combine(missingDir, name));
                    }
                    if (!CopyParentIcon(missingDir)) return false;
                    if (ContextMenuNext.MediaIconsReady(Path.Combine(missingDir, "Flix.exe"))) return false;
                }
                finally { try { Directory.Delete(missingDir, true); } catch (IOException) { } }
            }
            string corruptDir = Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "media-bad-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(corruptDir);
            try
            {
                foreach (string name in MediaPremiumV2IconFiles)
                    File.Copy(Path.Combine(directory, name), Path.Combine(corruptDir, name));
                File.WriteAllBytes(Path.Combine(corruptDir, "flix-menu-mkv-premium-v2.ico"), [1, 2, 3, 4]);
                if (!CopyParentIcon(corruptDir)) return false;
                if (ContextMenuNext.MediaIconsReady(Path.Combine(corruptDir, "Flix.exe"))) return false;
            }
            finally { try { Directory.Delete(corruptDir, true); } catch (IOException) { } }
            return ContextMenuNext.MediaIconsReady(exe);
        }
        finally { try { Directory.Delete(directory, true); } catch (IOException) { } }
    }

    static bool MediaLeavesPriorBlueprints(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        var native = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        var pandoc = new ContextMenuCatalog.EngineSnapshot(true, false, false);
        string[] imagesBefore = ImagePlanSignature(native);
        string[] contentNativeBefore = ContentPlanSignature(native);
        string[] contentPandocBefore = ContentPlanSignature(pandoc);
        var previewBefore = ContextMenuPreview.CreatePlan(@"C:\Programmi\Altro\Flix.exe", native);
        string previewId = previewBefore.Value(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue);
        _ = ContextMenuNext.PlanMediaMenus(ffmpeg);
        _ = ContextMenuNext.PlanMediaMenus(new ContextMenuCatalog.EngineSnapshot(true, true, true));
        _ = ContextMenuNext.PlanMediaMenus(native);
        if (!imagesBefore.SequenceEqual(ImagePlanSignature(native))) return false;
        if (!imagesBefore.SequenceEqual(ImagePlanSignature(ffmpeg))) return false;
        if (!contentNativeBefore.SequenceEqual(ContentPlanSignature(native))) return false;
        if (!contentPandocBefore.SequenceEqual(ContentPlanSignature(pandoc))) return false;
        if (!contentPandocBefore.SequenceEqual(ContentPlanSignature(new ContextMenuCatalog.EngineSnapshot(true, true, true)))) return false;
        var previewAfter = ContextMenuPreview.CreatePlan(@"C:\Programmi\Altro\Flix.exe", native);
        if (previewAfter.Value(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) != previewId
            || previewId != ContextMenuPreview.PrototypeId
            || !previewBefore.Formats.SequenceEqual(previewAfter.Formats)) return false;
        var imageKeys = ContextMenuNext.PlanImageMenus(native).SelectMany(ContextMenuNext.PlannedKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var imageIds = ContextMenuNext.PlanImageMenus(native).Select(plan => plan.MenuId).ToHashSet(StringComparer.Ordinal);
        var contentKeys = ContextMenuNext.PlanContentMenus(pandoc).SelectMany(ContextMenuNext.PlannedContentKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contentIds = ContextMenuNext.PlanContentMenus(pandoc).Select(plan => plan.MenuId).ToHashSet(StringComparer.Ordinal);
        var media = ContextMenuNext.PlanMediaMenus(ffmpeg);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in media)
        {
            if (imageIds.Contains(plan.MenuId) || contentIds.Contains(plan.MenuId)) return false;
            if (plan.MenuId == ContextMenuPreview.PrototypeId || plan.MenuId == LegacyIdentity.OriginalMenuId) return false;
            foreach (string key in ContextMenuNext.PlannedMediaKeys(plan))
            {
                if (!seen.Add(key) || imageKeys.Contains(key) || contentKeys.Contains(key)) return false;
                if (key.Equals(ContextMenuPreview.OriginalMenuKey, StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith(ContextMenuPreview.OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase)
                    || key.Equals(ContextMenuPreview.ParentKey, StringComparison.OrdinalIgnoreCase)
                    || key.StartsWith(ContextMenuPreview.ParentKey + @"\", StringComparison.OrdinalIgnoreCase)) return false;
                if (!key.StartsWith(@"Software\Classes\SystemFileAssociations\", StringComparison.OrdinalIgnoreCase)) return false;
                if (key.Contains(@"\CLSID", StringComparison.OrdinalIgnoreCase) || key.Contains("UserChoice", StringComparison.OrdinalIgnoreCase)) return false;
                if (key.Contains(LegacyIdentity.OriginalMenuKey, StringComparison.OrdinalIgnoreCase)) return false;
            }
        }
        var png = ContextMenuNext.PlanImageMenus(native).Single(plan => plan.Extension == ".png");
        var txt = ContextMenuNext.PlanContentMenus(native).Single(plan => plan.Extension == ".txt");
        return png.MenuId == "Flix.Convert.Png"
            && png.Formats.SequenceEqual(["PDF", "JPG", "WEBP", "BMP", "TIFF", "GIF"])
            && txt.MenuId == "Flix.Convert.Txt"
            && ContextMenuNext.ImageExtensions.SequenceEqual(Policy.Images)
            && ContextMenuNext.ContentExtensions.SequenceEqual([".pdf", ".txt", ".json", ".yaml", ".yml", ".xml", ".csv"]);
    }

    static string[] ContentPlanSignature(ContextMenuCatalog.EngineSnapshot engines) =>
        ContextMenuNext.PlanContentMenus(engines).Select(plan =>
            plan.Extension + "|" + plan.MenuId + "|" + plan.ParentKey + "|" + plan.SharesCommandTree
            + "|" + string.Join(",", plan.Formats)
            + "|" + string.Join(";", ContextMenuNext.PlannedContentKeys(plan))).ToArray();

    static void AddMediaMenuInstallChecks(Dictionary<string, bool> checks)
    {
        var ffmpeg = new ContextMenuCatalog.EngineSnapshot(false, false, true);
        var absent = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        var native = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        var pandoc = new ContextMenuCatalog.EngineSnapshot(true, false, false);
        string photo = @"C:\Audio\brano.mp3";
        Check(checks, "media_install_ffmpeg_fifteen_menus", () => MediaInstallWithFfmpeg(ffmpeg));
        Check(checks, "media_install_ffmpeg_absent_no_write", () => MediaInstallWithoutFfmpeg(ffmpeg, absent));
        Check(checks, "media_install_icons_gate_no_write", () => MediaInstallIconsGate(ffmpeg));
        Check(checks, "media_foreign_owner_no_write", () => MediaForeignOwnerPreserved(ffmpeg));
        Check(checks, "media_install_rollback_restores", () => MediaInstallRollback(ffmpeg));
        Check(checks, "media_remove_only_audio_video_keys", () => MediaRemoveOwnedOnly(ffmpeg, native));
        Check(checks, "media_cli_rejects_combinations", () => MediaCliRejectsCombinations(ffmpeg, photo));
        Check(checks, "media_unauthorized_path_refused", () => MediaUnauthorizedPathRefused(ffmpeg));
        Check(checks, "media_prior_menus_unchanged", () => MediaPriorMenusUnchanged(ffmpeg, native, pandoc));
        Check(checks, "media_flags_close_before_host", () => MediaFlagsCloseBeforeHost(photo));
    }

    static int RunMediaCommand(string[] args, string exe, IRegistryStore store, ContextMenuCatalog.EngineSnapshot engines, out string error)
    {
        var buffer = new StringWriter();
        int code = ContextMenuNext.ApplyMedia(args, exe, store, engines, buffer);
        error = buffer.ToString();
        return code;
    }

    static bool PrepareMediaLab(string name, out string directory, out string exe)
    {
        directory = ImageLab(name);
        Directory.CreateDirectory(directory);
        exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
        return CopyMediaPremiumIcons(directory) && ContextMenuNext.MediaIconsReady(exe);
    }

    static bool PrepareMixedLab(string name, out string directory, out string exe)
    {
        directory = ImageLab(name);
        Directory.CreateDirectory(directory);
        exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
        return CopyPremiumV2Icons(directory) && CopyMediaPremiumIcons(directory) && ContextMenuNext.MediaIconsReady(exe);
    }

    static bool CopyMediaPremiumIcons(string directory, string? omit = null, string? corrupt = null)
    {
        if (!CopyParentIcon(directory)) return false;
        bool copied = false;
        foreach (string name in MediaPremiumV2IconFiles)
        {
            string dest = Path.Combine(directory, name);
            if (omit != null && name.Equals(omit, StringComparison.OrdinalIgnoreCase)) continue;
            if (corrupt != null && name.Equals(corrupt, StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllBytes(dest, [1, 2, 3, 4]);
                copied = true;
                continue;
            }
            string source = Path.Combine(AppContext.BaseDirectory, name);
            if (!File.Exists(source) || !ContextMenuPreview.IconIsValid(source)) return false;
            File.Copy(source, dest, true);
            copied = true;
        }
        return copied;
    }

    static bool MediaMenusMatch(MemoryRegistryStore store, string exe, ContextMenuCatalog.EngineSnapshot engines, int foreignKeys = 0)
    {
        var blueprints = ContextMenuNext.PlanMediaMenus(engines);
        if (blueprints.Length != 15 || blueprints.Sum(plan => plan.Formats.Count) != 86) return false;
        int commands = 0, audio = 0, video = 0;
        var commandKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string full = Path.GetFullPath(exe);
        foreach (var blueprint in blueprints)
        {
            if (store.Read(blueprint.ParentKey, "MUIVerb") != "Converti con FLIX") return false;
            if (store.Read(blueprint.ParentKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            if (store.Read(blueprint.ParentKey, "") != null) return false;
            if (store.KeyExists(blueprint.ParentKey + @"\command")) return false;
            if (store.Read(blueprint.ParentKey, "ExtendedSubCommandsKey") != blueprint.SubCommandsPath) return false;
            if (store.Read(blueprint.ParentKey, "MultiSelectModel") != "Single") return false;
            if (store.Read(blueprint.ParentKey, "NeverDefault") != "") return false;
            if (store.Read(blueprint.ParentKey + @"\shell", ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            foreach (string format in blueprint.Formats)
            {
                string verb = blueprint.ParentKey + @"\shell\" + format.ToLowerInvariant();
                string commandKey = verb + @"\command";
                if (!commandKeys.Add(commandKey)) return false;
                if (store.Read(commandKey, "") != "\"" + full + "\" --convert-menu " + format + " --files \"%1\"") return false;
                if (store.Read(commandKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                string icon = store.Read(verb, "Icon") ?? "";
                string expectedIcon = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(full)!, ContextMenuNext.IconFileName(format)));
                if (!icon.Equals(expectedIcon, StringComparison.OrdinalIgnoreCase)) return false;
                if (!icon.EndsWith("-premium-v2.ico", StringComparison.OrdinalIgnoreCase)) return false;
                if (store.Read(verb, "MUIVerb") != "Converti in " + format) return false;
                if (store.Read(verb, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                if (store.Read(verb, "MultiSelectModel") != "Single" || store.Read(verb, "NeverDefault") != "") return false;
                if (!MenuConversion.TryParse(["--convert-menu", format, "--files", @"C:\Audio\brano" + blueprint.Extension], out var request, out _)) return false;
                if (request.Format != format || request.File != @"C:\Audio\brano" + blueprint.Extension) return false;
                commands++;
            }
            foreach (string key in ContextMenuNext.PlannedMediaKeys(blueprint))
                if (!store.KeyExists(key) || store.Read(key, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            if (Policy.Audio.Contains(blueprint.Extension)) audio += blueprint.Formats.Count;
            else if (Policy.Video.Contains(blueprint.Extension)) video += blueprint.Formats.Count;
            else return false;
        }
        int expectedKeys = blueprints.Sum(plan => ContextMenuNext.PlannedMediaKeys(plan).Count);
        return commands == 86 && audio == 42 && video == 44 && expectedKeys == 202
            && store.KeyCount == expectedKeys + foreignKeys;
    }

    static bool MediaInstallWithFfmpeg(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        if (!PrepareMediaLab("media-install", out string directory, out string exe)) return false;
        try
        {
            var store = new MemoryRegistryStore();
            int code = RunMediaCommand([ContextMenuNext.InstallMediaCommand], exe, store, ffmpeg, out string error);
            if (code != 0 || error.Length != 0 || !MediaMenusMatch(store, exe, ffmpeg)) return false;
            string once = store.Export();
            int again = RunMediaCommand(
                [ContextMenuNext.InstallMediaCommand, ContextMenuNext.InstallMediaCommand],
                exe, store, ffmpeg, out string againError);
            if (again != 0 || againError.Length != 0 || store.Export() != once) return false;
            var office = new MemoryRegistryStore();
            ContextMenuNext.InstallMediaMenus(exe, new ContextMenuCatalog.EngineSnapshot(true, true, true), office);
            return office.Export() == once;
        }
        finally { DeleteLab(directory); }
    }

    static bool MediaInstallWithoutFfmpeg(ContextMenuCatalog.EngineSnapshot ffmpeg, ContextMenuCatalog.EngineSnapshot absent)
    {
        if (!PrepareMediaLab("media-no-ffmpeg", out string directory, out string exe)) return false;
        try
        {
            if (ContextMenuNext.PlanMediaMenus(absent).Length != 0 || ContextMenuNext.PlanMediaMenus(ffmpeg).Length != 15) return false;
            var sentinel = new MemoryRegistryStore();
            sentinel.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string before = sentinel.Export();
            bool threw = false;
            try { ContextMenuNext.InstallMediaMenus(exe, absent, sentinel); }
            catch (IOException ex)
            {
                threw = ex.Message.Contains("FFmpeg assente", StringComparison.Ordinal)
                    && ex.Message.Contains("Nessuna chiave è stata scritta", StringComparison.Ordinal)
                    && !ex.Message.Contains("Icone non disponibili", StringComparison.Ordinal)
                    && !ex.Message.Contains("non è atomica", StringComparison.Ordinal);
            }
            if (!threw || sentinel.Export() != before || sentinel.KeyExists(ContextMenuNext.MediaParentKeyFor(".mp3"))) return false;
            var touch = new TouchRegistry();
            int code = RunMediaCommand([ContextMenuNext.InstallMediaCommand], exe, touch, absent, out string error);
            return code == 1
                && touch.Calls == 0
                && error.Contains("FFmpeg assente", StringComparison.Ordinal)
                && error.Contains("Nessuna chiave è stata scritta", StringComparison.Ordinal)
                && !error.Contains("garantito", StringComparison.OrdinalIgnoreCase);
        }
        finally { DeleteLab(directory); }
    }

    static bool MediaInstallIconsGate(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        foreach (string omit in MediaPremiumV2IconFiles)
        {
            string missing = ImageLab("media-install-miss");
            Directory.CreateDirectory(missing);
            try
            {
                if (!CopyMediaPremiumIcons(missing, omit)) return false;
                string exe = Path.GetFullPath(Path.Combine(missing, "Flix.exe"));
                if (ContextMenuNext.MediaIconsReady(exe)) return false;
                if (!MediaIconInstallRefused(exe, ffmpeg)) return false;
            }
            finally { DeleteLab(missing); }
        }
        string corrupt = ImageLab("media-install-bad");
        Directory.CreateDirectory(corrupt);
        try
        {
            if (!CopyMediaPremiumIcons(corrupt, corrupt: "flix-menu-mkv-premium-v2.ico")) return false;
            string exe = Path.GetFullPath(Path.Combine(corrupt, "Flix.exe"));
            if (ContextMenuNext.MediaIconsReady(exe)) return false;
            return MediaIconInstallRefused(exe, ffmpeg);
        }
        finally { DeleteLab(corrupt); }
    }

    static bool MediaIconInstallRefused(string exe, ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        var touch = new TouchRegistry();
        bool refused = false;
        try { ContextMenuNext.InstallMediaMenus(exe, ffmpeg, touch); }
        catch (IOException ex) { refused = ex.Message.Contains("Icone non disponibili", StringComparison.Ordinal); }
        if (!refused || touch.Calls != 0) return false;
        var memory = new MemoryRegistryStore();
        try { ContextMenuNext.InstallMediaMenus(exe, ffmpeg, memory); }
        catch (IOException) { }
        if (memory.KeyCount != 0) return false;
        int code = RunMediaCommand([ContextMenuNext.InstallMediaCommand], exe, touch, ffmpeg, out string error);
        return code == 1
            && touch.Calls == 0
            && error.Contains("Icone non disponibili", StringComparison.Ordinal)
            && !error.Contains("garantito", StringComparison.OrdinalIgnoreCase);
    }

    static bool MediaForeignOwnerPreserved(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        if (!PrepareMediaLab("media-foreign", out string directory, out string exe)) return false;
        try
        {
            var parent = new MemoryRegistryStore();
            string mp3 = ContextMenuNext.MediaParentKeyFor(".mp3");
            parent.Write(mp3, ContextMenuNext.OwnerValue, "Altro");
            parent.Write(mp3, "MUIVerb", "Non toccare");
            if (!MediaInstallLeavesStore(exe, ffmpeg, parent)) return false;
            var command = new MemoryRegistryStore();
            string verb = ContextMenuNext.MediaParentKeyFor(".mkv") + @"\shell\mp4\command";
            command.Write(verb, "", "notepad.exe \"%1\"");
            command.Write(verb, ContextMenuNext.OwnerValue, "AltroProgramma");
            return MediaInstallLeavesStore(exe, ffmpeg, command);
        }
        finally { DeleteLab(directory); }
    }

    static bool MediaInstallLeavesStore(string exe, ContextMenuCatalog.EngineSnapshot engines, MemoryRegistryStore store)
    {
        string before = store.Export();
        try { ContextMenuNext.InstallMediaMenus(exe, engines, store); return false; }
        catch (IOException ex)
        {
            return ex.Message.Contains("preservata", StringComparison.Ordinal)
                && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase)
                && store.Export() == before;
        }
    }

    static bool MediaInstallRollback(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        if (!PrepareMediaLab("media-rollback", out string directory, out string exe)) return false;
        try
        {
            int totalWrites = ContextMenuNext.PlanMediaMenus(ffmpeg).Sum(plan => ContextMenuNext.DescribeMediaMenu(plan, exe).Count);
            int midpoint = totalWrites / 2;
            if (midpoint < 2 || midpoint >= totalWrites) return false;
            Exception[] failures =
            [
                new IOException("errore simulato"),
                new UnauthorizedAccessException("errore simulato"),
                new SecurityException("errore simulato"),
                new InvalidOperationException("errore simulato"),
                new ArgumentException("errore simulato")
            ];
            foreach (var failure in failures)
            {
                var absent = new MemoryRegistryStore();
                absent.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
                string before = absent.Export();
                var fail = new FailAfterWriteStore(absent) { FailOn = midpoint, Failure = failure };
                bool threw = false;
                try { ContextMenuNext.InstallMediaMenus(exe, ffmpeg, fail); }
                catch (Exception ex) when (ex.GetType() == failure.GetType())
                {
                    threw = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                        && !ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                        && !ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                        && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
                }
                if (!threw || absent.Export() != before) return false;
                if (absent.KeyExists(ContextMenuNext.MediaParentKeyFor(".mp3"))) return false;
                if (absent.KeyExists(ContextMenuNext.MediaParentKeyFor(".mp4"))) return false;
                if (absent.Read(ContextMenuPreview.OriginalMenuKey, "") != LegacyIdentity.OriginalMenuVerb) return false;
            }

            var mp3 = ContextMenuNext.PlanMediaMenus(ffmpeg).Single(plan => plan.Extension == ".mp3");
            var existing = new MemoryRegistryStore();
            existing.Write(mp3.ParentKey, "MUIVerb", "Personalizzato");
            existing.Write(mp3.ParentKey, "ExtendedSubCommandsKey", mp3.SubCommandsPath);
            existing.Write(mp3.ParentKey, "MultiSelectModel", "Single");
            existing.Write(mp3.ParentKey, "NeverDefault", "");
            existing.Write(mp3.ParentKey, ContextMenuNext.OwnerValue, mp3.MenuId);
            existing.Write(mp3.ParentKey, "Nota", "tenere");
            existing.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string beforeExisting = existing.Export();
            var interrupted = new FailAfterWriteStore(existing) { FailOn = midpoint };
            bool interruptedThrow = false;
            try { ContextMenuNext.InstallMediaMenus(exe, ffmpeg, interrupted); }
            catch (IOException ex)
            {
                interruptedThrow = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
            }
            if (!interruptedThrow || existing.Export() != beforeExisting) return false;
            if (existing.KeyExists(ContextMenuNext.MediaParentKeyFor(".wav"))) return false;
            if (existing.Read(mp3.ParentKey, "MUIVerb") != "Personalizzato" || existing.Read(mp3.ParentKey, "Nota") != "tenere") return false;

            var partial = new MemoryRegistryStore();
            partial.Write(mp3.ParentKey, "MUIVerb", "Personalizzato");
            partial.Write(mp3.ParentKey, "ExtendedSubCommandsKey", mp3.SubCommandsPath);
            partial.Write(mp3.ParentKey, "MultiSelectModel", "Single");
            partial.Write(mp3.ParentKey, "NeverDefault", "");
            partial.Write(mp3.ParentKey, ContextMenuNext.OwnerValue, mp3.MenuId);
            partial.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            var corrupt = new FailAfterWriteStore(partial) { FailOn = midpoint, CorruptRestoreWrites = true };
            bool partialSignaled = false;
            try { ContextMenuNext.InstallMediaMenus(exe, ffmpeg, corrupt); }
            catch (IOException ex)
            {
                partialSignaled = ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                    && ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
            }
            return partialSignaled
                && partial.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && partial.Export().Contains("#parziale", StringComparison.Ordinal);
        }
        finally { DeleteLab(directory); }
    }

    static bool MediaRemoveOwnedOnly(ContextMenuCatalog.EngineSnapshot ffmpeg, ContextMenuCatalog.EngineSnapshot native)
    {
        if (!PrepareMixedLab("media-remove", out string directory, out string exe)) return false;
        try
        {
            var store = new MemoryRegistryStore();
            ContextMenuNext.InstallImageMenus(exe, native, store);
            ContextMenuPreview.Install(PreviewPlan(native, exe), store);
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            ContextMenuNext.InstallContentMenus(exe, native, store);
            ContextMenuNext.InstallMediaMenus(exe, ffmpeg, store);
            var mp3 = ContextMenuNext.PlanMediaMenus(ffmpeg).Single(plan => plan.Extension == ".mp3");
            var opus = ContextMenuNext.PlanMediaMenus(ffmpeg).Single(plan => plan.Extension == ".opus");
            string decoy = mp3.ParentKey + @"\shell\custom";
            store.Write(decoy, "MUIVerb", "Esterno");
            store.Write(decoy, ContextMenuNext.OwnerValue, "Altro");
            store.Write(opus.ParentKey, ContextMenuNext.OwnerValue, "AltroProgramma");
            store.Write(opus.ParentKey, "MUIVerb", "Non toccare");
            int removed = RunMediaCommand([ContextMenuNext.RemoveMediaCommand], exe, store, native, out string error);
            string after = store.Export();
            int removedAgain = RunMediaCommand([ContextMenuNext.RemoveMediaCommand], exe, store, ffmpeg, out string againError);
            string mp3Command = mp3.ParentKey + @"\shell\" + mp3.Formats[0].ToLowerInvariant() + @"\command";
            string opusCommand = opus.ParentKey + @"\shell\" + opus.Formats[0].ToLowerInvariant() + @"\command";
            bool kept = removed == 0 && error.Length == 0
                && removedAgain == 0 && againError.Length == 0
                && store.Export() == after
                && store.KeyExists(mp3.ParentKey)
                && store.KeyExists(decoy)
                && store.Read(decoy, "MUIVerb") == "Esterno"
                && !store.KeyExists(mp3Command)
                && store.KeyExists(opus.ParentKey)
                && !store.KeyExists(opusCommand)
                && store.Read(opus.ParentKey, ContextMenuNext.OwnerValue) == "AltroProgramma"
                && store.Read(opus.ParentKey, "MUIVerb") == "Non toccare"
                && !store.KeyExists(ContextMenuNext.MediaParentKeyFor(".wav"))
                && !store.KeyExists(ContextMenuNext.MediaParentKeyFor(".mp4"))
                && !store.KeyExists(ContextMenuNext.MediaParentKeyFor(".mkv"))
                && store.Read(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) == ContextMenuNext.MenuId
                && store.Read(ContextMenuNext.ParentKeyFor(".jpg"), ContextMenuNext.OwnerValue) == ContextMenuNext.MenuIdFor(".jpg")
                && store.Read(ContextMenuNext.ContentParentKeyFor(".pdf"), ContextMenuNext.OwnerValue) == ContextMenuNext.ContentMenuIdFor(".pdf")
                && store.Read(ContextMenuNext.ContentParentKeyFor(".txt"), ContextMenuNext.OwnerValue) == ContextMenuNext.ContentMenuIdFor(".txt")
                && store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId
                && store.Read(ContextMenuPreview.ParentKey, "MUIVerb") == ContextMenuPreview.ParentVerb
                && store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb
                && store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") == LegacyIdentity.OriginalMenuCommand;
            if (!kept) return false;

            var clean = new MemoryRegistryStore();
            ContextMenuNext.InstallMediaMenus(exe, ffmpeg, clean);
            int installed = clean.KeyCount;
            ContextMenuNext.RemoveMediaMenus(clean);
            ContextMenuNext.RemoveMediaMenus(clean);
            if (installed != 202 || clean.KeyCount != 0) return false;

            var noIcons = new MemoryRegistryStore();
            ContextMenuNext.InstallMediaMenus(exe, ffmpeg, noIcons);
            foreach (string name in MediaPremiumV2IconFiles)
                File.Delete(Path.Combine(directory, name));
            if (ContextMenuNext.MediaIconsReady(exe)) return false;
            ContextMenuNext.RemoveMediaMenus(exe, noIcons);
            ContextMenuNext.RemoveMediaMenus(exe, noIcons);
            return noIcons.KeyCount == 0
                && !noIcons.KeyExists(ContextMenuNext.MediaParentKeyFor(".flac"))
                && !noIcons.KeyExists(ContextMenuNext.MediaParentKeyFor(".webm"));
        }
        finally { DeleteLab(directory); }
    }

    static bool MediaCliRejectsCombinations(ContextMenuCatalog.EngineSnapshot ffmpeg, string photo)
    {
        if (!PrepareMediaLab("media-cli", out string directory, out string exe)) return false;
        try
        {
            string[] extras =
            [
                "--install-context-menu-images", "--remove-context-menu-images",
                "--install-context-menu-pdf-text-data", "--remove-context-menu-pdf-text-data",
                "--install-context-menu-png", "--remove-context-menu-png",
                "--install-context-menu-preview", "--remove-context-menu-preview",
                "--install-context-menu", "--remove-context-menu",
                "--convert-menu", "--convert", "--test-input", "--files", photo, "extra"
            ];
            var store = new TouchRegistry();
            int both = RunMediaCommand(
                [ContextMenuNext.InstallMediaCommand, ContextMenuNext.RemoveMediaCommand],
                exe, store, ffmpeg, out string bothError);
            if (both == 0 || store.Calls != 0 || !bothError.Contains("Nessuna modifica al Registro", StringComparison.Ordinal)) return false;
            foreach (string extra in extras)
            {
                int install = RunMediaCommand([ContextMenuNext.InstallMediaCommand, extra], exe, store, ffmpeg, out string installError);
                int remove = RunMediaCommand([extra, ContextMenuNext.RemoveMediaCommand], exe, store, ffmpeg, out string removeError);
                if (install == 0 || remove == 0 || store.Calls != 0) return false;
                if (!installError.Contains(extra, StringComparison.Ordinal) || !installError.Contains("Nessuna modifica al Registro", StringComparison.Ordinal)) return false;
                if (!removeError.Contains(extra, StringComparison.Ordinal) || removeError.Contains("garantito", StringComparison.OrdinalIgnoreCase)) return false;
            }
            var imageStore = new TouchRegistry();
            int imageBlocked = ContextMenuNext.ApplyImages(
                [ContextMenuNext.InstallImagesCommand, ContextMenuNext.InstallMediaCommand],
                exe, imageStore, ffmpeg, new StringWriter());
            var contentStore = new TouchRegistry();
            int contentBlocked = ContextMenuNext.ApplyContent(
                [ContextMenuNext.InstallContentCommand, ContextMenuNext.RemoveMediaCommand],
                exe, contentStore, ffmpeg, new StringWriter());
            return imageBlocked != 0
                && contentBlocked != 0
                && imageStore.Calls == 0
                && contentStore.Calls == 0
                && store.Calls == 0
                && ContextMenuNext.IsMediaMenuCommand([ContextMenuNext.InstallMediaCommand])
                && ContextMenuNext.IsMediaMenuCommand([ContextMenuNext.RemoveMediaCommand])
                && !ContextMenuNext.IsMediaMenuCommand([ContextMenuNext.InstallImagesCommand])
                && !ContextMenuNext.IsMediaMenuCommand([ContextMenuNext.InstallContentCommand])
                && !ContextMenuNext.IsImageMenuCommand([ContextMenuNext.InstallMediaCommand])
                && !ContextMenuNext.IsContentMenuCommand([ContextMenuNext.RemoveMediaCommand])
                && !ContextMenuNext.IsPlannedCommand([ContextMenuNext.InstallMediaCommand])
                && !ContextMenuPreview.IsPreviewCommand([ContextMenuNext.RemoveMediaCommand]);
        }
        finally { DeleteLab(directory); }
    }

    static bool MediaUnauthorizedPathRefused(ContextMenuCatalog.EngineSnapshot ffmpeg)
    {
        string[] rejected =
        [
            @"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe",
            @"C:\dev\FLIX\src\Flix\obj\Release\net10.0-windows\Flix.exe",
            Path.Combine(Path.GetTempPath(), "Flix.exe"),
            @"C:\Programmi\Altro\Flix.exe",
            Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "Flix.dll"),
            ""
        ];
        var store = new TouchRegistry();
        foreach (string path in rejected)
        {
            int install = RunMediaCommand([ContextMenuNext.InstallMediaCommand], path, store, ffmpeg, out string installError);
            int remove = RunMediaCommand([ContextMenuNext.RemoveMediaCommand], path, store, ffmpeg, out string removeError);
            if (install == 0 || remove == 0 || store.Calls != 0) return false;
            if (installError.Length == 0 || removeError.Length == 0) return false;
            if (installError.Contains("garantito", StringComparison.OrdinalIgnoreCase)) return false;
            bool installThrew = false;
            bool removeThrew = false;
            try { ContextMenuNext.InstallMediaMenus(path, ffmpeg, store); }
            catch (IOException) { installThrew = true; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or InvalidOperationException or ArgumentException) { installThrew = true; }
            try { ContextMenuNext.RemoveMediaMenus(path, store); }
            catch (IOException) { removeThrew = true; }
            catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException or InvalidOperationException or ArgumentException) { removeThrew = true; }
            if (!installThrew || !removeThrew || store.Calls != 0) return false;
        }
        return true;
    }

    static bool MediaPriorMenusUnchanged(ContextMenuCatalog.EngineSnapshot ffmpeg, ContextMenuCatalog.EngineSnapshot native, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        if (!PrepareMixedLab("media-prior", out string directory, out string exe)) return false;
        try
        {
            string[] imagesBefore = ImagePlanSignature(native);
            string[] contentBefore = ContentPlanSignature(native);
            string[] contentPandocBefore = ContentPlanSignature(pandoc);
            var previewBefore = ContextMenuPreview.CreatePlan(exe, native);
            var store = new MemoryRegistryStore();
            ContextMenuNext.InstallImageMenus(exe, native, store);
            ContextMenuPreview.Install(previewBefore, store);
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            ContextMenuNext.InstallContentMenus(exe, pandoc, store);
            string before = store.Export();
            int protectedKeys = store.KeyCount;
            _ = ContextMenuNext.PlanMediaMenus(ffmpeg);
            ContextMenuNext.InstallMediaMenus(exe, ffmpeg, store);
            if (store.KeyCount != protectedKeys + 202) return false;
            if (store.Read(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) != ContextMenuNext.MenuId) return false;
            if (store.Read(ContextMenuNext.ParentKeyFor(".jpeg"), "ExtendedSubCommandsKey") != ContextMenuNext.SubCommandsPathFor(".jpg")) return false;
            if (store.Read(ContextMenuNext.ContentParentKeyFor(".yml"), "ExtendedSubCommandsKey") != ContextMenuNext.ContentSubCommandsPathFor(".yaml")) return false;
            if (store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) != ContextMenuPreview.PrototypeId) return false;
            if (store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") != LegacyIdentity.OriginalMenuCommand) return false;
            ContextMenuNext.RemoveMediaMenus(exe, store);
            ContextMenuNext.RemoveMediaMenus(exe, store);
            if (store.Export() != before) return false;
            if (store.KeyExists(ContextMenuNext.MediaParentKeyFor(".aac"))) return false;
            if (store.KeyExists(ContextMenuNext.MediaParentKeyFor(".m4v"))) return false;
            var mp3 = ContextMenuNext.PlanMediaMenus(ffmpeg).Single(plan => plan.Extension == ".mp3");
            var blocked = new ContextMenuPlan(exe, mp3.Formats.ToArray(), ContextMenuNext.DescribeMediaMenu(mp3, exe));
            var touch = new TouchRegistry();
            var memory = new MemoryRegistryStore();
            bool refused = false;
            try { ContextMenuNext.Install(blocked, touch); }
            catch (IOException ex) { refused = ex.Message.Contains("Nessuna chiave", StringComparison.Ordinal) && ex.Message.Contains("audio e video", StringComparison.Ordinal); }
            try { ContextMenuNext.Install(blocked, memory); }
            catch (IOException) { }
            var previewAfter = ContextMenuPreview.CreatePlan(exe, native);
            return refused
                && touch.Calls == 0
                && memory.KeyCount == 0
                && imagesBefore.SequenceEqual(ImagePlanSignature(native))
                && imagesBefore.SequenceEqual(ImagePlanSignature(ffmpeg))
                && contentBefore.SequenceEqual(ContentPlanSignature(native))
                && contentPandocBefore.SequenceEqual(ContentPlanSignature(pandoc))
                && contentPandocBefore.SequenceEqual(ContentPlanSignature(new ContextMenuCatalog.EngineSnapshot(true, true, true)))
                && previewBefore.Formats.SequenceEqual(previewAfter.Formats)
                && previewAfter.Value(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId;
        }
        finally { DeleteLab(directory); }
    }

    static bool MediaFlagsCloseBeforeHost(string photo)
    {
        foreach (string flag in new[] { ContextMenuNext.InstallMediaCommand, ContextMenuNext.RemoveMediaCommand })
        {
            if (!ContextMenuNext.IsMediaMenuCommand([flag])) return false;
            if (ContextMenuNext.IsImageMenuCommand([flag]) || ContextMenuNext.IsContentMenuCommand([flag])) return false;
            if (ContextMenuNext.IsPlannedCommand([flag]) || ContextMenuPreview.IsPreviewCommand([flag])) return false;
            if (Program.StartsResidentHost([flag]) || Program.OpensWheelOnStartup([flag]) || Program.OpensPanelOnStartup([flag])) return false;
            if (Program.OpensWheelOnStartup([flag, "--files", photo])) return false;
            if (Program.OpensPanelOnStartup([flag, "--multi", "--files", photo])) return false;
        }
        return MenuConversion.TryParse(["--convert-menu", "MP3", "--files", photo], out var request, out _)
            && request.Format == "MP3"
            && request.File == photo
            && !Program.OpensWheelOnStartup(["--convert-menu", "MP3", "--files", photo])
            && !Program.OpensPanelOnStartup(["--convert-menu", "WAV", "--multi", "--files", photo])
            && !Program.OpensWheelOnStartup(["--files", photo])
            && !Program.OpensPanelOnStartup(["--multi", "--files", photo]);
    }

    static void AddDocumentMenuChecks(Dictionary<string, bool> checks)
    {
        var absent = new ContextMenuCatalog.EngineSnapshot(false, false, false);
        var pandoc = new ContextMenuCatalog.EngineSnapshot(true, false, false);
        var office = new ContextMenuCatalog.EngineSnapshot(true, true, false);
        var officeFfmpeg = new ContextMenuCatalog.EngineSnapshot(true, true, true);
        string note = @"C:\Documenti\nota.md";
        Check(checks, "documents_ten_extensions_and_aliases", () => DocumentTenExtensions(pandoc));
        Check(checks, "documents_destinations_order_and_identity", () => DocumentDestinations(office, pandoc));
        Check(checks, "documents_engine_combinations", () => DocumentEngineCombinations());
        Check(checks, "documents_premium_icons", () => DocumentPremiumIcons(office));
        Check(checks, "documents_plan_commands_and_properties", () => DocumentPlanCommands(officeFfmpeg));
        Check(checks, "documents_install_with_pandoc_and_office", () => DocumentInstallOffice(office, officeFfmpeg));
        Check(checks, "documents_install_pandoc_without_office", () => DocumentInstallWithoutOffice(pandoc, office));
        Check(checks, "documents_pandoc_absent_no_write", () => DocumentPandocAbsentNoWrite(absent, office));
        Check(checks, "documents_icons_gate_no_write", () => DocumentIconsGate(office, pandoc));
        Check(checks, "documents_foreign_owner_preserved", () => DocumentForeignOwner(office));
        Check(checks, "documents_backup_rollback_write_and_delete", () => DocumentRollback(office));
        Check(checks, "documents_remove_without_pandoc_idempotent", () => DocumentRemoveWithoutEngines(office, absent));
        Check(checks, "documents_cli_rejects_combinations", () => DocumentCliRejects(office, note));
        Check(checks, "documents_unauthorized_path_refused", () => DocumentUnauthorizedPath(office));
        Check(checks, "documents_prior_families_unchanged", () => DocumentPriorFamiliesUnchanged(office, absent, pandoc));
        Check(checks, "documents_association_baseline_restored", () => DocumentAssociationBaseline(office));
    }

    static void DocumentExpect(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }

    static string[] ExpectedDocumentFormats(string extension, bool office)
    {
        string[] all = office
            ? ["DOCX", "PDF", "HTML", "MD", "TXT", "RTF", "ODT", "EPUB", "FB2", "RST", "JPG", "PNG", "WEBP"]
            : ["DOCX", "HTML", "MD", "TXT", "RTF", "ODT", "EPUB", "FB2", "RST"];
        string source = extension switch
        {
            ".md" or ".markdown" => "MD",
            ".html" or ".htm" => "HTML",
            _ => extension.TrimStart('.').ToUpperInvariant()
        };
        return all.Where(format => !format.Equals(source, StringComparison.Ordinal)).ToArray();
    }

    static bool PrepareDocumentLab(string name, out string directory, out string exe)
    {
        directory = ImageLab(name);
        Directory.CreateDirectory(directory);
        exe = Path.GetFullPath(Path.Combine(directory, "Flix.exe"));
        return CopyPremiumV2Icons(directory);
    }

    static int RunDocumentCommand(string[] args, string exe, IRegistryStore store, ContextMenuCatalog.EngineSnapshot engines, out string error)
    {
        var buffer = new StringWriter();
        int code = ContextMenuNext.ApplyDocuments(args, exe, store, engines, buffer);
        error = buffer.ToString();
        return code;
    }

    static bool DocumentTenExtensions(ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string[] expected = [".md", ".markdown", ".html", ".htm", ".docx", ".odt", ".rtf", ".epub", ".rst", ".fb2"];
        DocumentExpect(ContextMenuNext.DocumentExtensions.SequenceEqual(expected), "elenco estensioni");
        string[] excluded = [".pdf", ".txt", ".json", ".yaml", ".yml", ".xml", ".csv", ".doc", ".xlsx", ".pptx", ".pages", ".png", ".jpg", ".mp3", ".mp4"];
        DocumentExpect(!excluded.Any(ContextMenuNext.DocumentExtensions.Contains), "estensione estranea nel menu documenti");
        var plans = ContextMenuNext.PlanDocumentMenus(pandoc);
        DocumentExpect(plans.Length == 10 && plans.Select(plan => plan.Extension).SequenceEqual(expected), "dieci menu nell'ordine previsto");
        var ids = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [".md"] = "Flix.Convert.Md", [".markdown"] = "Flix.Convert.Md",
            [".html"] = "Flix.Convert.Html", [".htm"] = "Flix.Convert.Html",
            [".docx"] = "Flix.Convert.Docx", [".odt"] = "Flix.Convert.Odt",
            [".rtf"] = "Flix.Convert.Rtf", [".epub"] = "Flix.Convert.Epub",
            [".rst"] = "Flix.Convert.Rst", [".fb2"] = "Flix.Convert.Fb2"
        };
        foreach (var plan in plans)
        {
            bool alias = plan.Extension is ".markdown" or ".htm";
            DocumentExpect(plan.MenuId == ids[plan.Extension], plan.Extension + " id " + plan.MenuId);
            DocumentExpect(plan.SharesCommandTree == alias, plan.Extension + " alias");
            DocumentExpect(plan.ParentKey.StartsWith(@"Software\Classes\SystemFileAssociations\" + plan.Extension + @"\shell\", StringComparison.Ordinal), plan.ParentKey);
            DocumentExpect(plan.MenuId == ContextMenuNext.DocumentMenuIdFor(plan.Extension), "id helper");
        }
        var md = plans.Single(plan => plan.Extension == ".md");
        var markdown = plans.Single(plan => plan.Extension == ".markdown");
        var html = plans.Single(plan => plan.Extension == ".html");
        var htm = plans.Single(plan => plan.Extension == ".htm");
        DocumentExpect(markdown.SubCommandsPath == md.SubCommandsPath && htm.SubCommandsPath == html.SubCommandsPath, "ExtendedSubCommandsKey alias");
        DocumentExpect(markdown.CommandParentKey == md.ParentKey && htm.CommandParentKey == html.ParentKey, "padre canonico");
        DocumentExpect(markdown.Formats.SequenceEqual(md.Formats) && htm.Formats.SequenceEqual(html.Formats), "destinazioni alias");
        DocumentExpect(ContextMenuNext.PlannedDocumentKeys(markdown).Count == 1 && ContextMenuNext.PlannedDocumentKeys(htm).Count == 1, "alias senza comandi propri");
        DocumentExpect(!md.SharesCommandTree && !html.SharesCommandTree, "canonici con albero proprio");
        var keys = plans.SelectMany(ContextMenuNext.PlannedDocumentKeys).ToArray();
        DocumentExpect(keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() == keys.Length, "chiavi duplicate");
        return true;
    }

    static bool DocumentDestinations(ContextMenuCatalog.EngineSnapshot office, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        var officePlans = ContextMenuNext.PlanDocumentMenus(office);
        var textPlans = ContextMenuNext.PlanDocumentMenus(pandoc);
        int officeCommands = 0, textCommands = 0;
        string[] rendering = ["PDF", "JPG", "PNG", "WEBP"];
        string[] forbidden = ["JSON", "YAML", "XML", "CSV", "BMP", "TIFF", "GIF", "MP3", "MP4", "WAV", "DOC", "XLSX", "PPTX", "PAGES"];
        foreach (string extension in ContextMenuNext.DocumentExtensions)
        {
            var withOffice = officePlans.Single(plan => plan.Extension == extension);
            var without = textPlans.Single(plan => plan.Extension == extension);
            string[] expectedOffice = ExpectedDocumentFormats(extension, true);
            string[] expectedText = ExpectedDocumentFormats(extension, false);
            string[] policyOffice = Policy.OrderFormats(Policy.Outputs(["flix.context" + extension], true, true, false));
            string[] policyText = Policy.OrderFormats(Policy.Outputs(["flix.context" + extension], true, false, true));
            string[] catalogOffice = ContextMenuCatalog.ForExtension(extension, office).Select(entry => entry.TargetFormat).ToArray();
            string[] catalogText = ContextMenuCatalog.ForExtension(extension, new ContextMenuCatalog.EngineSnapshot(true, false, false)).Select(entry => entry.TargetFormat).ToArray();
            DocumentExpect(withOffice.Formats.SequenceEqual(expectedOffice), extension + " office " + string.Join(",", withOffice.Formats));
            DocumentExpect(without.Formats.SequenceEqual(expectedText), extension + " testo " + string.Join(",", without.Formats));
            DocumentExpect(withOffice.Formats.SequenceEqual(policyOffice) && withOffice.Formats.SequenceEqual(catalogOffice), extension + " policy office");
            DocumentExpect(without.Formats.SequenceEqual(policyText) && without.Formats.SequenceEqual(catalogText), extension + " policy testo");
            string source = extension switch
            {
                ".md" or ".markdown" => "MD",
                ".html" or ".htm" => "HTML",
                _ => extension.TrimStart('.').ToUpperInvariant()
            };
            DocumentExpect(!withOffice.Formats.Contains(source) && !without.Formats.Contains(source), extension + " identità");
            DocumentExpect(rendering.All(withOffice.Formats.Contains) && !rendering.Any(without.Formats.Contains), extension + " rendering");
            DocumentExpect(!withOffice.Formats.Any(forbidden.Contains) && !without.Formats.Any(forbidden.Contains), extension + " formato non previsto");
            if (!withOffice.SharesCommandTree) officeCommands += withOffice.Formats.Count;
            if (!without.SharesCommandTree) textCommands += without.Formats.Count;
        }
        DocumentExpect(officeCommands == 96 && textCommands == 64, "comandi " + officeCommands + "/" + textCommands);
        return true;
    }

    static bool DocumentEngineCombinations()
    {
        var seen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var engines in ContextMenuCatalog.EngineSnapshot.AllCombinations())
        {
            var toggledEngines = new ContextMenuCatalog.EngineSnapshot(engines.Pandoc, engines.Office, !engines.Ffmpeg);
            var plan = ContextMenuNext.PlanDocumentMenus(engines);
            var other = ContextMenuNext.PlanDocumentMenus(toggledEngines);
            DocumentExpect(plan.Length == other.Length, "FFmpeg cambia il numero dei menu");
            if (!engines.Pandoc)
            {
                DocumentExpect(plan.Length == 0, "Pandoc assente ma il piano non è vuoto");
                continue;
            }
            DocumentExpect(plan.Length == 10, "Pandoc presente senza dieci menu");
            for (int index = 0; index < plan.Length; index++)
            {
                DocumentExpect(plan[index].Extension == other[index].Extension && plan[index].Formats.SequenceEqual(other[index].Formats), "FFmpeg cambia " + plan[index].Extension);
                DocumentExpect(plan[index].Formats.Count == (engines.Office ? 12 : 8), plan[index].Extension + " conteggio");
            }
            string signature = string.Join("|", plan.Select(item => item.Extension + ":" + string.Join(",", item.Formats)));
            string key = (engines.Office ? "office" : "text");
            if (!seen.ContainsKey(key)) seen[key] = signature;
            else DocumentExpect(seen[key] == signature, "combinazione non stabile");
        }
        DocumentExpect(ContextMenuNext.PlanDocumentMenus(new ContextMenuCatalog.EngineSnapshot(false, true, true)).Length == 0, "LibreOffice senza Pandoc");
        return seen.Count == 2;
    }

    static bool DocumentPremiumIcons(ContextMenuCatalog.EngineSnapshot office)
    {
        string[] formats = ContextMenuNext.PlanDocumentMenus(office)
            .Where(plan => !plan.SharesCommandTree)
            .SelectMany(plan => plan.Formats)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(format => format, StringComparer.Ordinal)
            .ToArray();
        string[] expected = ["DOCX", "EPUB", "FB2", "HTML", "JPG", "MD", "ODT", "PDF", "PNG", "RST", "RTF", "TXT", "WEBP"];
        DocumentExpect(formats.SequenceEqual(expected), "icone " + string.Join(",", formats));
        foreach (string format in formats)
        {
            string name = ContextMenuNext.IconFileName(format);
            DocumentExpect(name.EndsWith("-premium-v2.ico", StringComparison.OrdinalIgnoreCase), name);
            DocumentExpect(name != "flix-menu-jpg.ico" && name != "flix-menu-pdf.ico" && name != "flix-menu-webp.ico", name);
            string path = Path.Combine(AppContext.BaseDirectory, name);
            DocumentExpect(ContextMenuPreview.IconIsValid(path), "icona non valida " + name);
        }
        return true;
    }

    static bool DocumentPlanCommands(ContextMenuCatalog.EngineSnapshot engines)
    {
        string exe = Path.GetFullPath(@"C:\Programmi\Altro\Flix.exe");
        var plans = ContextMenuNext.CreateDocumentPlans(exe, engines);
        DocumentExpect(plans.Length == 10, "piani");
        foreach (var plan in plans)
        {
            string root = plan.Keys.OrderBy(key => key.Length).First();
            DocumentExpect(plan.Value(root, "MUIVerb") == "Converti con FLIX", "voce padre");
            DocumentExpect(plan.Value(root, "MultiSelectModel") == "Single" && plan.Value(root, "NeverDefault") == "", "proprietà");
            DocumentExpect(plan.Value(root, ContextMenuNext.OwnerValue).StartsWith("Flix.Convert.", StringComparison.Ordinal), "FlixOwner");
            DocumentExpect(!plan.Keys.Contains(root + @"\command"), "padre eseguibile");
            DocumentExpect(plan.Values.All(value => !(value.Key.Equals(root, StringComparison.OrdinalIgnoreCase) && value.Name.Length == 0)), "default padre");
            DocumentExpect(plan.Keys.All(key => key.StartsWith(@"Software\Classes\SystemFileAssociations\", StringComparison.Ordinal)), "HKCU");
            bool shared = plan.Keys.Count == 1;
            if (shared) continue;
            foreach (string format in plan.Formats)
            {
                DocumentExpect(plan.Command(format) == "\"" + exe + "\" --convert-menu " + format + " --files \"%1\"", format);
                DocumentExpect(plan.VerbTitle(format) == "Converti in " + format, format);
                DocumentExpect(plan.Icon(format).EndsWith(ContextMenuNext.IconFileName(format), StringComparison.OrdinalIgnoreCase), format);
                DocumentExpect(MenuConversion.TryParse(["--convert-menu", format, "--files", @"C:\Documenti\nota.md"], out var request, out _), format);
                DocumentExpect(request.Format == format && request.File == @"C:\Documenti\nota.md", format);
            }
        }
        return true;
    }

    static bool DocumentMenusMatch(MemoryRegistryStore store, string exe, ContextMenuCatalog.EngineSnapshot engines, int commands, int keys, int foreignKeys)
    {
        var blueprints = ContextMenuNext.PlanDocumentMenus(engines);
        if (blueprints.Length != 10) return false;
        int found = 0;
        var commandKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string full = Path.GetFullPath(exe);
        foreach (var blueprint in blueprints)
        {
            if (store.Read(blueprint.ParentKey, "MUIVerb") != "Converti con FLIX") return false;
            if (store.Read(blueprint.ParentKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            if (store.Read(blueprint.ParentKey, "") != null) return false;
            if (store.KeyExists(blueprint.ParentKey + @"\command")) return false;
            if (store.Read(blueprint.ParentKey, "ExtendedSubCommandsKey") != blueprint.SubCommandsPath) return false;
            if (store.Read(blueprint.ParentKey, "MultiSelectModel") != "Single") return false;
            if (store.Read(blueprint.ParentKey, "NeverDefault") != "") return false;
            if (blueprint.SharesCommandTree)
            {
                if (store.KeyExists(blueprint.ParentKey + @"\shell") || store.KeyExists(blueprint.ParentKey + @"\command")) return false;
                continue;
            }
            if (store.Read(blueprint.ParentKey + @"\shell", ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
            foreach (string format in blueprint.Formats)
            {
                string verb = blueprint.ParentKey + @"\shell\" + format.ToLowerInvariant();
                string commandKey = verb + @"\command";
                if (!commandKeys.Add(commandKey)) return false;
                if (store.Read(commandKey, "") != "\"" + full + "\" --convert-menu " + format + " --files \"%1\"") return false;
                if (store.Read(commandKey, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                string icon = store.Read(verb, "Icon") ?? "";
                string expectedIcon = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(full)!, ContextMenuNext.IconFileName(format)));
                if (!icon.Equals(expectedIcon, StringComparison.OrdinalIgnoreCase)) return false;
                if (!icon.EndsWith("-premium-v2.ico", StringComparison.OrdinalIgnoreCase)) return false;
                if (store.Read(verb, "MUIVerb") != "Converti in " + format) return false;
                if (store.Read(verb, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
                if (!MenuConversion.TryParse(["--convert-menu", format, "--files", @"C:\Documenti\nota" + blueprint.Extension], out var request, out _)) return false;
                if (request.Format != format) return false;
                found++;
            }
            foreach (string key in ContextMenuNext.PlannedDocumentKeys(blueprint))
                if (!store.KeyExists(key) || store.Read(key, ContextMenuNext.OwnerValue) != blueprint.MenuId) return false;
        }
        var md = blueprints.Single(plan => plan.Extension == ".md");
        var markdown = blueprints.Single(plan => plan.Extension == ".markdown");
        var html = blueprints.Single(plan => plan.Extension == ".html");
        var htm = blueprints.Single(plan => plan.Extension == ".htm");
        int expectedKeys = blueprints.Sum(plan => ContextMenuNext.PlannedDocumentKeys(plan).Count);
        int associationKeys = blueprints.Count(plan => store.KeyExists(ContextMenuNext.DocumentAssociationKey(plan.Extension)))
            + blueprints.Count(plan => store.KeyExists(ContextMenuNext.DocumentShellAssociationKey(plan.Extension)));
        return found == commands
            && expectedKeys == keys
            && store.KeyCount == expectedKeys + associationKeys + foreignKeys
            && markdown.Formats.SequenceEqual(md.Formats)
            && htm.Formats.SequenceEqual(html.Formats)
            && store.Read(markdown.ParentKey, "ExtendedSubCommandsKey") == md.SubCommandsPath
            && store.Read(htm.ParentKey, "ExtendedSubCommandsKey") == html.SubCommandsPath
            && !store.KeyExists(markdown.ParentKey + @"\shell")
            && !store.KeyExists(htm.ParentKey + @"\shell");
    }

    static bool DocumentInstallOffice(ContextMenuCatalog.EngineSnapshot office, ContextMenuCatalog.EngineSnapshot officeFfmpeg)
    {
        if (!PrepareDocumentLab("doc-office", out string directory, out string exe)) return false;
        try
        {
            var store = new MemoryRegistryStore();
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            int code = RunDocumentCommand([ContextMenuNext.InstallDocumentsCommand], exe, store, office, out string error);
            DocumentExpect(code == 0 && error.Length == 0, error);
            DocumentExpect(DocumentMenusMatch(store, exe, office, 96, 210, 1), "store office");
            string once = store.Export();
            int again = RunDocumentCommand(
                [ContextMenuNext.InstallDocumentsCommand, ContextMenuNext.InstallDocumentsCommand],
                exe, store, office, out string againError);
            DocumentExpect(again == 0 && againError.Length == 0 && store.Export() == once, "idempotenza");
            var ffmpeg = new MemoryRegistryStore();
            ContextMenuNext.InstallDocumentMenus(exe, officeFfmpeg, ffmpeg);
            ffmpeg.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            DocumentExpect(ffmpeg.Export() == once, "FFmpeg ha cambiato il menu");
            return store.Read(ContextMenuPreview.OriginalMenuKey, "") == LegacyIdentity.OriginalMenuVerb;
        }
        finally { DeleteLab(directory); }
    }

    static bool DocumentInstallWithoutOffice(ContextMenuCatalog.EngineSnapshot pandoc, ContextMenuCatalog.EngineSnapshot office)
    {
        if (!PrepareDocumentLab("doc-text", out string directory, out string exe)) return false;
        try
        {
            File.Delete(Path.Combine(directory, "flix-menu-pdf-premium-v2.ico"));
            var store = new MemoryRegistryStore();
            int code = RunDocumentCommand([ContextMenuNext.InstallDocumentsCommand], exe, store, pandoc, out string error);
            DocumentExpect(code == 0 && error.Length == 0 && DocumentMenusMatch(store, exe, pandoc, 64, 146, 0), error);
            DocumentExpect(!store.KeyExists(ContextMenuNext.DocumentParentKeyFor(".md") + @"\shell\pdf"), "PDF senza LibreOffice");
            string before = store.Export();
            var touch = new TouchRegistry();
            int blocked = RunDocumentCommand([ContextMenuNext.InstallDocumentsCommand], exe, touch, office, out string blockedError);
            return blocked == 1
                && touch.Calls == 0
                && blockedError.Contains("Icone non disponibili", StringComparison.Ordinal)
                && store.Export() == before;
        }
        finally { DeleteLab(directory); }
    }

    static bool DocumentPandocAbsentNoWrite(ContextMenuCatalog.EngineSnapshot absent, ContextMenuCatalog.EngineSnapshot office)
    {
        if (!PrepareDocumentLab("doc-no-pandoc", out string directory, out string exe)) return false;
        try
        {
            DocumentExpect(ContextMenuNext.PlanDocumentMenus(absent).Length == 0, "piano");
            DocumentExpect(ContextMenuNext.PlanDocumentMenus(office).Length == 10, "piano pieno");
            var sentinel = new MemoryRegistryStore();
            sentinel.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string before = sentinel.Export();
            bool threw = false;
            try { ContextMenuNext.InstallDocumentMenus(exe, absent, sentinel); }
            catch (IOException ex)
            {
                threw = ex.Message.Contains("Pandoc assente", StringComparison.Ordinal)
                    && ex.Message.Contains("Nessuna chiave è stata scritta", StringComparison.Ordinal)
                    && !ex.Message.Contains("Icone non disponibili", StringComparison.Ordinal)
                    && !ex.Message.Contains("non è atomica", StringComparison.Ordinal);
            }
            DocumentExpect(threw && sentinel.Export() == before, "installazione senza Pandoc");
            var touch = new TouchRegistry();
            int code = RunDocumentCommand([ContextMenuNext.InstallDocumentsCommand], exe, touch, absent, out string error);
            return code == 1
                && touch.Calls == 0
                && error.Contains("Pandoc assente", StringComparison.Ordinal)
                && error.Contains("Nessuna chiave è stata scritta", StringComparison.Ordinal);
        }
        finally { DeleteLab(directory); }
    }

    static bool DocumentIconsGate(ContextMenuCatalog.EngineSnapshot office, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        string[] formats = ContextMenuNext.PlanDocumentMenus(office)
            .SelectMany(plan => plan.Formats)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (string format in formats)
        {
            if (!PrepareDocumentLab("doc-miss", out string directory, out string exe)) return false;
            try
            {
                File.Delete(Path.Combine(directory, ContextMenuNext.IconFileName(format)));
                if (ContextMenuNext.DocumentIconsReady(exe, office)) return false;
                if (!DocumentIconInstallRefused(exe, office)) return false;
            }
            finally { DeleteLab(directory); }
        }
        if (!PrepareDocumentLab("doc-bad", out string corruptDir, out string corruptExe)) return false;
        try
        {
            File.WriteAllBytes(Path.Combine(corruptDir, "flix-menu-docx-premium-v2.ico"), [1, 2, 3, 4]);
            if (ContextMenuNext.DocumentIconsReady(corruptExe, office) || ContextMenuNext.DocumentIconsReady(corruptExe, pandoc)) return false;
            if (!DocumentIconInstallRefused(corruptExe, office)) return false;
        }
        finally { DeleteLab(corruptDir); }
        if (!PrepareDocumentLab("doc-pdf", out string pdfDir, out string pdfExe)) return false;
        try
        {
            File.WriteAllBytes(Path.Combine(pdfDir, "flix-menu-pdf-premium-v2.ico"), [1, 2, 3, 4]);
            return ContextMenuNext.DocumentIconsReady(pdfExe, pandoc)
                && !ContextMenuNext.DocumentIconsReady(pdfExe, office);
        }
        finally { DeleteLab(pdfDir); }
    }

    static bool DocumentIconInstallRefused(string exe, ContextMenuCatalog.EngineSnapshot engines)
    {
        var touch = new TouchRegistry();
        bool refused = false;
        try { ContextMenuNext.InstallDocumentMenus(exe, engines, touch); }
        catch (IOException ex) { refused = ex.Message.Contains("Icone non disponibili", StringComparison.Ordinal); }
        if (!refused || touch.Calls != 0) return false;
        var memory = new MemoryRegistryStore();
        try { ContextMenuNext.InstallDocumentMenus(exe, engines, memory); }
        catch (IOException) { }
        int code = RunDocumentCommand([ContextMenuNext.InstallDocumentsCommand], exe, touch, engines, out string error);
        return memory.KeyCount == 0
            && code == 1
            && touch.Calls == 0
            && error.Contains("Icone non disponibili", StringComparison.Ordinal)
            && !error.Contains("garantito", StringComparison.OrdinalIgnoreCase);
    }

    static bool DocumentForeignOwner(ContextMenuCatalog.EngineSnapshot office)
    {
        if (!PrepareDocumentLab("doc-foreign", out string directory, out string exe)) return false;
        try
        {
            var parent = new MemoryRegistryStore();
            string md = ContextMenuNext.DocumentParentKeyFor(".md");
            parent.Write(md, ContextMenuNext.OwnerValue, "Altro");
            parent.Write(md, "MUIVerb", "Non toccare");
            if (!DocumentInstallLeavesStore(exe, office, parent)) return false;
            var alias = new MemoryRegistryStore();
            string htm = ContextMenuNext.DocumentParentKeyFor(".htm");
            alias.Write(htm, ContextMenuNext.OwnerValue, "AltroProgramma");
            alias.Write(htm, "MUIVerb", "Alias");
            return DocumentInstallLeavesStore(exe, office, alias);
        }
        finally { DeleteLab(directory); }
    }

    static bool DocumentInstallLeavesStore(string exe, ContextMenuCatalog.EngineSnapshot engines, MemoryRegistryStore store)
    {
        string before = store.Export();
        try { ContextMenuNext.InstallDocumentMenus(exe, engines, store); return false; }
        catch (IOException ex)
        {
            return ex.Message.Contains("preservata", StringComparison.Ordinal)
                && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase)
                && store.Export() == before;
        }
    }

    static bool DocumentRollback(ContextMenuCatalog.EngineSnapshot office)
    {
        if (!PrepareDocumentLab("doc-rollback", out string directory, out string exe)) return false;
        try
        {
            int totalWrites = ContextMenuNext.PlanDocumentMenus(office).Sum(plan => ContextMenuNext.DescribeDocumentMenu(plan, exe).Count);
            int midpoint = totalWrites / 2;
            if (midpoint < 2 || midpoint >= totalWrites) return false;
            Exception[] failures =
            [
                new IOException("errore simulato"),
                new UnauthorizedAccessException("errore simulato"),
                new SecurityException("errore simulato")
            ];
            foreach (var failure in failures)
            {
                var absent = new MemoryRegistryStore();
                absent.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
                string before = absent.Export();
                var fail = new FailAfterWriteStore(absent) { FailOn = midpoint, Failure = failure };
                bool threw = false;
                try { ContextMenuNext.InstallDocumentMenus(exe, office, fail); }
                catch (Exception ex) when (ex.GetType() == failure.GetType())
                {
                    threw = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                        && !ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                        && !ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal);
                }
                if (!threw || absent.Export() != before || absent.KeyExists(ContextMenuNext.DocumentParentKeyFor(".md"))) return false;
            }

            var installed = new MemoryRegistryStore();
            ContextMenuNext.InstallDocumentMenus(exe, office, installed);
            installed.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string beforeDelete = installed.Export();
            var interrupted = new FailAfterDeleteStore(installed) { FailOn = 1 };
            bool deleteThrew = false;
            try { ContextMenuNext.RemoveDocumentMenus(exe, interrupted); }
            catch (IOException ex)
            {
                deleteThrew = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("non è atomica", StringComparison.Ordinal);
            }
            if (!deleteThrew || installed.Export() != beforeDelete) return false;

            var partial = new MemoryRegistryStore();
            ContextMenuNext.InstallDocumentMenus(exe, office, partial);
            partial.Write(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue, ContextMenuPreview.PrototypeId);
            var corrupt = new FailAfterDeleteStore(partial) { FailOn = 1, FailRestore = true };
            bool partialSignaled = false;
            try { ContextMenuNext.RemoveDocumentMenus(exe, corrupt); }
            catch (IOException ex)
            {
                partialSignaled = ex.Message.Contains("potrebbe essere parziale", StringComparison.Ordinal)
                    && ex.Message.Contains("non è atomica", StringComparison.Ordinal)
                    && ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("garantito", StringComparison.OrdinalIgnoreCase);
            }
            return partialSignaled
                && partial.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) == ContextMenuPreview.PrototypeId
                && partial.KeyExists(ContextMenuNext.DocumentParentKeyFor(".docx"));
        }
        finally { DeleteLab(directory); }
    }

    static bool DocumentRemoveWithoutEngines(ContextMenuCatalog.EngineSnapshot office, ContextMenuCatalog.EngineSnapshot absent)
    {
        if (!PrepareDocumentLab("doc-remove", out string directory, out string exe)) return false;
        try
        {
            var store = new MemoryRegistryStore();
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            string sibling = @"Software\Classes\SystemFileAssociations\.md\shell\OtherTool";
            store.Write(sibling, "MUIVerb", "Altro");
            ContextMenuNext.InstallDocumentMenus(exe, office, store);
            var md = ContextMenuNext.PlanDocumentMenus(office).Single(plan => plan.Extension == ".md");
            string decoy = md.ParentKey + @"\shell\custom";
            store.Write(decoy, "MUIVerb", "Esterno");
            store.Write(decoy, ContextMenuNext.OwnerValue, "Altro");
            string html = ContextMenuNext.DocumentParentKeyFor(".html");
            int removed = RunDocumentCommand([ContextMenuNext.RemoveDocumentsCommand], exe, store, absent, out string error);
            DocumentExpect(removed == 0 && error.Length == 0, error);
            DocumentExpect(store.KeyExists(decoy) && store.Read(decoy, "MUIVerb") == "Esterno", "decoy");
            DocumentExpect(store.KeyExists(md.ParentKey) && !store.KeyExists(md.ParentKey + @"\shell\pdf\command"), "rimozione selettiva");
            DocumentExpect(!store.KeyExists(html) && !store.KeyExists(ContextMenuNext.DocumentParentKeyFor(".markdown")), "canonico e alias rimossi");
            DocumentExpect(store.Read(sibling, "MUIVerb") == "Altro", "fratello");
            DocumentExpect(store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") == LegacyIdentity.OriginalMenuCommand, "legacy");
            string after = store.Export();
            int again = RunDocumentCommand([ContextMenuNext.RemoveDocumentsCommand], exe, store, new ContextMenuCatalog.EngineSnapshot(false, true, true), out string againError);
            DocumentExpect(again == 0 && againError.Length == 0 && store.Export() == after, "seconda rimozione");

            var clean = new MemoryRegistryStore();
            ContextMenuNext.InstallDocumentMenus(exe, office, clean);
            int installed = clean.KeyCount;
            foreach (string name in PremiumV2IconFiles) File.Delete(Path.Combine(directory, name));
            DocumentExpect(!ContextMenuNext.DocumentIconsReady(exe, office), "icone ancora valide");
            ContextMenuNext.RemoveDocumentMenus(exe, clean);
            ContextMenuNext.RemoveDocumentMenus(exe, clean);
            int planned = ContextMenuNext.PlanDocumentMenus(office).Sum(plan => ContextMenuNext.PlannedDocumentKeys(plan).Count);
            return installed == planned + (ContextMenuNext.DocumentExtensions.Count * 2)
                && clean.KeyCount == 0
                && !clean.KeyExists(ContextMenuNext.DocumentParentKeyFor(".epub"))
                && !clean.KeyExists(ContextMenuNext.DocumentAssociationKey(".epub"))
                && !clean.KeyExists(ContextMenuNext.DocumentShellAssociationKey(".epub"))
                && !clean.KeyExists(ContextMenuNext.DocumentParentKeyFor(".md") + @"\shell\pdf");
        }
        finally { DeleteLab(directory); }
    }

    static bool DocumentAssociationBaseline(ContextMenuCatalog.EngineSnapshot office)
    {
        if (!PrepareDocumentLab("doc-assoc", out string directory, out string exe)) return false;
        try
        {
            var store = new MemoryRegistryStore();
            ContextMenuNext.InstallDocumentMenus(exe, office, store);
            foreach (string extension in ContextMenuNext.DocumentExtensions)
            {
                string menu = ContextMenuNext.DocumentParentKeyFor(extension);
                DocumentExpect(store.KeyExists(ContextMenuNext.DocumentAssociationKey(extension)), extension);
                DocumentExpect(store.KeyExists(ContextMenuNext.DocumentShellAssociationKey(extension)), extension + " shell");
                DocumentExpect(store.ValueNames(ContextMenuNext.DocumentAssociationKey(extension)).Count == 0, extension + " valori");
                DocumentExpect(store.ValueNames(ContextMenuNext.DocumentShellAssociationKey(extension)).Count == 0, extension + " shell valori");
                DocumentExpect(store.Read(menu, ContextMenuNext.DocumentAncestorValue) == "ext;shell", extension + " provenienza");
                DocumentExpect(store.Read(menu, ContextMenuNext.OwnerValue) == ContextMenuNext.DocumentMenuIdFor(extension), extension + " proprietario");
            }
            ContextMenuNext.RemoveDocumentMenus(exe, store);
            DocumentExpect(store.KeyCount == 0, "rimozione completa");
            foreach (string extension in ContextMenuNext.DocumentExtensions)
            {
                DocumentExpect(!store.KeyExists(ContextMenuNext.DocumentAssociationKey(extension)), extension + " padre");
                DocumentExpect(!store.KeyExists(ContextMenuNext.DocumentShellAssociationKey(extension)), extension + " shell rimossa");
                DocumentExpect(!store.KeyExists(ContextMenuNext.DocumentParentKeyFor(extension)), extension + " menu");
            }
            ContextMenuNext.RemoveDocumentMenus(exe, store);
            DocumentExpect(store.KeyCount == 0, "doppia rimozione");

            ContextMenuNext.InstallDocumentMenus(exe, office, store);
            string once = store.Export();
            ContextMenuNext.InstallDocumentMenus(exe, office, store);
            DocumentExpect(store.Export() == once, "doppia installazione");
            ContextMenuNext.RemoveDocumentMenus(exe, store);
            DocumentExpect(store.KeyCount == 0, "rimozione dopo doppia installazione");

            string ext = ContextMenuNext.DocumentAssociationKey(".docx");
            string shell = ContextMenuNext.DocumentShellAssociationKey(".docx");
            store.Write(ext, "", "predefinito");
            store.Write(ext, "PerceivedType", "document");
            ContextMenuNext.InstallDocumentMenus(exe, office, store);
            DocumentExpect(store.Read(ContextMenuNext.DocumentParentKeyFor(".docx"), ContextMenuNext.DocumentAncestorValue) == "shell", "solo shell");
            ContextMenuNext.RemoveDocumentMenus(exe, store);
            DocumentExpect(store.KeyExists(ext) && store.Read(ext, "") == "predefinito" && store.Read(ext, "PerceivedType") == "document", "valori preesistenti");
            DocumentExpect(!store.KeyExists(shell) && !store.KeyExists(ContextMenuNext.DocumentParentKeyFor(".docx")), "shell creata rimossa");
            DocumentExpect(!store.KeyExists(ContextMenuNext.DocumentAssociationKey(".md")), "md non preesistente");

            var emptyShell = new MemoryRegistryStore();
            string mdExt = ContextMenuNext.DocumentAssociationKey(".md");
            string mdShell = ContextMenuNext.DocumentShellAssociationKey(".md");
            emptyShell.EnsureKey(mdExt);
            emptyShell.EnsureKey(mdShell);
            ContextMenuNext.InstallDocumentMenus(exe, office, emptyShell);
            DocumentExpect(emptyShell.Read(ContextMenuNext.DocumentParentKeyFor(".md"), ContextMenuNext.DocumentAncestorValue) == null, "padri gia presenti");
            ContextMenuNext.RemoveDocumentMenus(exe, emptyShell);
            DocumentExpect(emptyShell.KeyExists(mdExt) && emptyShell.KeyExists(mdShell), "padri vuoti conservati");
            DocumentExpect(emptyShell.ValueNames(mdExt).Count == 0 && emptyShell.ValueNames(mdShell).Count == 0, "padri ancora vuoti");
            DocumentExpect(!emptyShell.KeyExists(ContextMenuNext.DocumentParentKeyFor(".md")), "menu md rimosso");
            DocumentExpect(!emptyShell.KeyExists(ContextMenuNext.DocumentShellAssociationKey(".html")), "html shell rimossa");
            DocumentExpect(!emptyShell.KeyExists(ContextMenuNext.DocumentAssociationKey(".html")), "html ext rimosso");

            var foreign = new MemoryRegistryStore();
            string other = ContextMenuNext.DocumentShellAssociationKey(".rtf") + @"\OtherApp";
            foreign.Write(other, "MUIVerb", "Altro");
            foreign.Write(other, ContextMenuNext.OwnerValue, "Altro");
            string kept = @"Software\Classes\SystemFileAssociations\.png\shell\AltraApp";
            foreign.Write(kept, "MUIVerb", "Immagine");
            ContextMenuNext.InstallDocumentMenus(exe, office, foreign);
            ContextMenuNext.RemoveDocumentMenus(exe, foreign);
            DocumentExpect(foreign.KeyExists(other) && foreign.Read(other, "MUIVerb") == "Altro", "altra applicazione");
            DocumentExpect(foreign.Read(kept, "MUIVerb") == "Immagine", "chiave estranea");
            DocumentExpect(!foreign.KeyExists(ContextMenuNext.DocumentParentKeyFor(".rtf")), "menu rtf rimosso");

            var beforeStore = new MemoryRegistryStore();
            beforeStore.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            string before = beforeStore.Export();
            int total = ContextMenuNext.PlanDocumentMenus(office).Sum(plan => ContextMenuNext.DescribeDocumentMenu(plan, exe).Count);
            var fail = new FailAfterWriteStore(beforeStore) { FailOn = Math.Max(2, total / 2), Failure = new IOException("errore simulato") };
            bool threw = false;
            try { ContextMenuNext.InstallDocumentMenus(exe, office, fail); }
            catch (IOException ex)
            {
                threw = ex.Message.Contains("errore simulato", StringComparison.Ordinal)
                    && !ex.Message.Contains("non è atomica", StringComparison.Ordinal);
            }
            DocumentExpect(threw && beforeStore.Export() == before, "errore installazione");
            foreach (string extension in ContextMenuNext.DocumentExtensions)
                DocumentExpect(!beforeStore.KeyExists(ContextMenuNext.DocumentAssociationKey(extension)) && !beforeStore.KeyExists(ContextMenuNext.DocumentShellAssociationKey(extension)), "residuo " + extension);
            return true;
        }
        finally { DeleteLab(directory); }
    }

    static bool DocumentCliRejects(ContextMenuCatalog.EngineSnapshot office, string note)
    {
        if (!PrepareDocumentLab("doc-cli", out string directory, out string exe)) return false;
        try
        {
            string[] extras =
            [
                "--install-context-menu-images", "--remove-context-menu-images",
                "--install-context-menu-pdf-text-data", "--remove-context-menu-pdf-text-data",
                "--install-context-menu-audio-video", "--remove-context-menu-audio-video",
                "--install-context-menu-png", "--remove-context-menu-png",
                "--install-context-menu-preview", "--remove-context-menu-preview",
                "--install-context-menu", "--remove-context-menu",
                "--convert-menu", "--convert", "--test-input", "--files", note, "extra"
            ];
            var store = new TouchRegistry();
            int both = RunDocumentCommand(
                [ContextMenuNext.InstallDocumentsCommand, ContextMenuNext.RemoveDocumentsCommand],
                exe, store, office, out string bothError);
            DocumentExpect(both != 0 && store.Calls == 0 && bothError.Contains("Nessuna modifica al Registro", StringComparison.Ordinal), bothError);
            foreach (string extra in extras)
            {
                int install = RunDocumentCommand([ContextMenuNext.InstallDocumentsCommand, extra], exe, store, office, out string installError);
                int remove = RunDocumentCommand([extra, ContextMenuNext.RemoveDocumentsCommand], exe, store, office, out string removeError);
                DocumentExpect(install != 0 && remove != 0 && store.Calls == 0, extra);
                DocumentExpect(installError.Contains(extra, StringComparison.Ordinal) && installError.Contains("Nessuna modifica al Registro", StringComparison.Ordinal), installError);
            }
            var imageStore = new TouchRegistry();
            int imageBlocked = ContextMenuNext.ApplyImages(
                [ContextMenuNext.InstallImagesCommand, ContextMenuNext.InstallDocumentsCommand],
                exe, imageStore, office, new StringWriter());
            var contentStore = new TouchRegistry();
            int contentBlocked = ContextMenuNext.ApplyContent(
                [ContextMenuNext.InstallContentCommand, ContextMenuNext.RemoveDocumentsCommand],
                exe, contentStore, office, new StringWriter());
            var mediaStore = new TouchRegistry();
            int mediaBlocked = ContextMenuNext.ApplyMedia(
                [ContextMenuNext.InstallMediaCommand, ContextMenuNext.InstallDocumentsCommand],
                exe, mediaStore, office, new StringWriter());
            DocumentExpect(imageBlocked != 0 && contentBlocked != 0 && mediaBlocked != 0, "famiglie");
            DocumentExpect(imageStore.Calls == 0 && contentStore.Calls == 0 && mediaStore.Calls == 0, "scritture incrociate");
            return ContextMenuNext.IsDocumentMenuCommand([ContextMenuNext.InstallDocumentsCommand])
                && ContextMenuNext.IsDocumentMenuCommand([ContextMenuNext.RemoveDocumentsCommand])
                && !ContextMenuNext.IsDocumentMenuCommand([ContextMenuNext.InstallMediaCommand])
                && !ContextMenuNext.IsMediaMenuCommand([ContextMenuNext.InstallDocumentsCommand])
                && !ContextMenuNext.IsImageMenuCommand([ContextMenuNext.RemoveDocumentsCommand])
                && !ContextMenuNext.IsContentMenuCommand([ContextMenuNext.InstallDocumentsCommand])
                && !ContextMenuNext.IsPlannedCommand([ContextMenuNext.InstallDocumentsCommand])
                && !ContextMenuPreview.IsPreviewCommand([ContextMenuNext.RemoveDocumentsCommand])
                && !Program.OpensWheelOnStartup([ContextMenuNext.InstallDocumentsCommand, "--files", note])
                && !Program.OpensPanelOnStartup([ContextMenuNext.RemoveDocumentsCommand, "--multi", "--files", note])
                && !Program.OpensWheelOnStartup(["--files", note]);
        }
        finally { DeleteLab(directory); }
    }

    static bool DocumentUnauthorizedPath(ContextMenuCatalog.EngineSnapshot office)
    {
        string[] rejected =
        [
            @"C:\dev\FLIX\src\Flix\bin\Release\net10.0-windows\Flix.exe",
            @"C:\dev\FLIX\src\Flix\obj\Release\net10.0-windows\Flix.exe",
            Path.Combine(Path.GetTempPath(), "Flix.exe"),
            @"C:\Programmi\Altro\Flix.exe",
            Path.Combine(Path.GetTempPath(), ContextMenuNext.LaboratorySegment, "Flix.dll"),
            ""
        ];
        var store = new TouchRegistry();
        foreach (string path in rejected)
        {
            int install = RunDocumentCommand([ContextMenuNext.InstallDocumentsCommand], path, store, office, out string installError);
            int remove = RunDocumentCommand([ContextMenuNext.RemoveDocumentsCommand], path, store, office, out string removeError);
            if (install == 0 || remove == 0 || store.Calls != 0 || installError.Length == 0 || removeError.Length == 0) return false;
        }
        return true;
    }

    static bool DocumentPriorFamiliesUnchanged(ContextMenuCatalog.EngineSnapshot office, ContextMenuCatalog.EngineSnapshot absent, ContextMenuCatalog.EngineSnapshot pandoc)
    {
        if (!PrepareMixedLab("doc-prior", out string directory, out string exe)) return false;
        try
        {
            var ffmpeg = new ContextMenuCatalog.EngineSnapshot(false, false, true);
            string[] imagesBefore = ImagePlanSignature(absent);
            string[] contentBefore = ContentPlanSignature(pandoc);
            var previewBefore = ContextMenuPreview.CreatePlan(exe, absent);
            var store = new MemoryRegistryStore();
            ContextMenuNext.InstallImageMenus(exe, absent, store);
            ContextMenuPreview.Install(previewBefore, store);
            store.Write(ContextMenuPreview.OriginalMenuKey, "", LegacyIdentity.OriginalMenuVerb);
            store.Write(ContextMenuPreview.OriginalMenuKey + @"\command", "", LegacyIdentity.OriginalMenuCommand);
            ContextMenuNext.InstallContentMenus(exe, pandoc, store);
            ContextMenuNext.InstallMediaMenus(exe, ffmpeg, store);
            string before = store.Export();
            int protectedKeys = store.KeyCount;
            ContextMenuNext.InstallDocumentMenus(exe, office, store);
            if (!DocumentMenusMatch(store, exe, office, 96, 210, protectedKeys)) return false;
            if (store.Read(ContextMenuNext.ParentKey, ContextMenuNext.OwnerValue) != ContextMenuNext.MenuId) return false;
            if (store.Read(ContextMenuNext.ParentKeyFor(".jpeg"), "ExtendedSubCommandsKey") != ContextMenuNext.SubCommandsPathFor(".jpg")) return false;
            if (store.Read(ContextMenuNext.ContentParentKeyFor(".yml"), "ExtendedSubCommandsKey") != ContextMenuNext.ContentSubCommandsPathFor(".yaml")) return false;
            if (store.Read(ContextMenuNext.MediaParentKeyFor(".mp3"), ContextMenuNext.OwnerValue) != ContextMenuNext.MediaMenuIdFor(".mp3")) return false;
            if (store.Read(ContextMenuPreview.ParentKey, ContextMenuPreview.OwnerValue) != ContextMenuPreview.PrototypeId) return false;
            if (store.Read(ContextMenuPreview.OriginalMenuKey + @"\command", "") != LegacyIdentity.OriginalMenuCommand) return false;
            int removed = RunDocumentCommand([ContextMenuNext.RemoveDocumentsCommand], exe, store, absent, out string error);
            if (removed != 0 || error.Length != 0 || store.Export() != before) return false;
            if (store.KeyExists(ContextMenuNext.DocumentParentKeyFor(".odt")) || store.KeyExists(ContextMenuNext.DocumentParentKeyFor(".htm"))) return false;
            var doc = ContextMenuNext.PlanDocumentMenus(office).Single(plan => plan.Extension == ".md");
            var blocked = new ContextMenuPlan(exe, doc.Formats.ToArray(), ContextMenuNext.DescribeDocumentMenu(doc, exe));
            var touch = new TouchRegistry();
            var memory = new MemoryRegistryStore();
            bool refused = false;
            try { ContextMenuNext.Install(blocked, touch); }
            catch (IOException ex) { refused = ex.Message.Contains("M5.5", StringComparison.Ordinal) && ex.Message.Contains("Nessuna chiave", StringComparison.Ordinal); }
            try { ContextMenuNext.Install(blocked, memory); }
            catch (IOException) { }
            return refused
                && touch.Calls == 0
                && memory.KeyCount == 0
                && imagesBefore.SequenceEqual(ImagePlanSignature(absent))
                && contentBefore.SequenceEqual(ContentPlanSignature(pandoc))
                && previewBefore.Formats.SequenceEqual(ContextMenuPreview.CreatePlan(exe, absent).Formats);
        }
        finally { DeleteLab(directory); }
    }

    static bool ThrowsIo(Action action)
    {
        try { action(); return false; }
        catch (IOException) { return true; }
    }

    sealed class TouchRegistry : IRegistryStore
    {
        internal int Calls;
        public bool KeyExists(string path) { Calls++; throw new InvalidOperationException("Registro non previsto."); }
        public string? Read(string path, string name) { Calls++; throw new InvalidOperationException("Registro non previsto."); }
        public IReadOnlyList<string> SubKeys(string path) { Calls++; throw new InvalidOperationException("Registro non previsto."); }
        public IReadOnlyList<string> ValueNames(string path) { Calls++; throw new InvalidOperationException("Registro non previsto."); }
        public void Write(string path, string name, string data) { Calls++; throw new InvalidOperationException("Registro non previsto."); }
        public void EnsureKey(string path) { Calls++; throw new InvalidOperationException("Registro non previsto."); }
        public void DeleteKey(string path) { Calls++; throw new InvalidOperationException("Registro non previsto."); }
        public void DeleteValue(string path, string name) { Calls++; throw new InvalidOperationException("Registro non previsto."); }
    }

    sealed class FailAfterWriteStore : IRegistryStore
    {
        readonly MemoryRegistryStore inner;
        int writes;
        bool tripped;
        internal int FailOn { get; init; }
        internal Exception? Failure { get; init; }
        internal bool CorruptRestoreWrites { get; init; }
        internal MemoryRegistryStore Inner => inner;
        internal FailAfterWriteStore() : this(new MemoryRegistryStore()) { }
        internal FailAfterWriteStore(MemoryRegistryStore inner) => this.inner = inner;
        public bool KeyExists(string path) => inner.KeyExists(path);
        public string? Read(string path, string name) => inner.Read(path, name);
        public IReadOnlyList<string> SubKeys(string path) => inner.SubKeys(path);
        public IReadOnlyList<string> ValueNames(string path) => inner.ValueNames(path);
        public void EnsureKey(string path) => inner.EnsureKey(path);
        public void DeleteKey(string path) => inner.DeleteKey(path);
        public void DeleteValue(string path, string name) => inner.DeleteValue(path, name);
        public void Write(string path, string name, string data)
        {
            writes++;
            if (!tripped && writes == FailOn)
            {
                tripped = true;
                throw Failure ?? new IOException("errore simulato");
            }
            if (tripped && CorruptRestoreWrites)
            {
                inner.Write(path, name, data + "#parziale");
                return;
            }
            inner.Write(path, name, data);
        }
    }

    sealed class FailAfterDeleteStore : IRegistryStore
    {
        readonly MemoryRegistryStore inner;
        int deletes;
        bool tripped;
        internal int FailOn { get; init; } = 1;
        internal bool FailRestore { get; init; }
        internal bool CorruptRestoreWrites { get; init; }
        internal Exception? Failure { get; init; }
        internal FailAfterDeleteStore(MemoryRegistryStore inner) => this.inner = inner;
        public bool KeyExists(string path) => inner.KeyExists(path);
        public string? Read(string path, string name) => inner.Read(path, name);
        public IReadOnlyList<string> SubKeys(string path) => inner.SubKeys(path);
        public IReadOnlyList<string> ValueNames(string path) => inner.ValueNames(path);
        public void EnsureKey(string path) => inner.EnsureKey(path);
        public void Write(string path, string name, string data)
        {
            if (tripped && FailRestore) throw new IOException("ripristino simulato");
            if (tripped && CorruptRestoreWrites)
            {
                inner.Write(path, name, data + "#parziale");
                return;
            }
            inner.Write(path, name, data);
        }
        public void DeleteValue(string path, string name) => inner.DeleteValue(path, name);
        public void DeleteKey(string path)
        {
            inner.DeleteKey(path);
            deletes++;
            if (!tripped && deletes == FailOn)
            {
                tripped = true;
                throw Failure ?? new IOException("errore simulato");
            }
        }
    }

    sealed class MenuProbe : MenuConversion.IEndpoint
    {
        internal int RunCode;
        internal int Runs, Tells;
        internal string? Told;
        internal MenuConversion.Request? Last;
        public ContextMenuCatalog.EngineSnapshot Engines { get; set; } = new(false, false, false);
        public int Run(MenuConversion.Request request)
        {
            Runs++;
            Last = request;
            return RunCode;
        }
        public void Tell(string message) { Tells++; Told = message; }
    }
}
