using System.IO;
using System.Text.Json;
namespace Flip;

internal static class InputTests
{
    internal static int Run()
    {
        var shortcut = WheelShortcut.Parse("OEM102");
        var checks = new Dictionary<string, bool>
        {
            ["iso_key_down"] = shortcut.Matches(0xE2, 0, 0x56),
            ["iso_key_release"] = shortcut.Matches(0xE2, 1, 0x56),
            ["function_keys_ignored"] = !shortcut.Matches(0x77, 0, 0x42) && !shortcut.Matches(0x78, 0, 0x43),
            ["physical_key_survives_layout_mapping"] = shortcut.Matches(0xDC, 0, 0x56),
            ["extended_scan_not_iso_key"] = !shortcut.Matches(0xE2, 2, 0x56) && !shortcut.Matches(0xE2, 4, 0x56),
            ["vk_fallback_without_scan"] = shortcut.Matches(0xE2, 0),
            ["different_physical_key_rejected"] = !shortcut.Matches(0xE2, 0, 0x2B),
            ["iso_aliases_parse"] = new[] { "<>", "<", ">", "VK_OEM_102", "OEM_102" }.All(v => WheelShortcut.Parse(v) == shortcut),
            ["ctrl_never_triggers"] = !shortcut.Matches(0x11, 0) && !shortcut.Matches(0x11, 2) && !shortcut.Matches(0xA3, 0) && !shortcut.Matches(0xA2, 0),
            ["alt_shift_never_trigger"] = !shortcut.Matches(0x12, 0) && !shortcut.Matches(0x10, 0),
            ["letters_ignored"] = !shortcut.Matches(0x44, 0) && !shortcut.Matches(0x46, 0),
            ["space_ignored"] = !shortcut.Matches(0x20, 0),
            ["escape_not_trigger"] = !shortcut.Matches(0x1B, 0),
            ["swapped_primary"] = Native.PrimaryVirtualKey(true) == 2 && Native.PrimaryDown(4, true) && Native.PrimaryUp(8, true),
            ["normal_primary"] = Native.PrimaryVirtualKey(false) == 1 && Native.PrimaryDown(1, false) && Native.PrimaryUp(2, false)
        };
        checks["universal_default_is_down_arrow"] = WheelShortcut.Parse(WheelShortcut.Default).VirtualKey == 0x28;
        foreach (string id in new[] { "DOWN", "UP", "LEFT", "RIGHT", "SPACE", "OEM102", "F8", "F9", "A", "7", "VK:BF" })
        {
            var choice = WheelShortcut.Parse(id);
            checks["custom_key_roundtrip_" + id] = WheelShortcut.Parse(choice.Id) == choice;
        }
        checks["extended_arrows_match"] = WheelShortcut.Parse("DOWN").Matches(0x28, 2, 0x50);
        var consumed = new ShortcutPressGate();
        checks["drag_key_captured_once"] = consumed.Process(true, true) == (true, true) && consumed.Process(true, true) == (true, false);
        checks["captured_keyup_after_mouse_release"] = consumed.Process(false, false) == (true, false);
        checks["normal_key_after_captured_pair"] = consumed.Process(true, false) == (false, false) && consumed.Process(false, false) == (false, false);
        var typing = new ShortcutPressGate();
        checks["typing_repeat_not_stolen_by_later_mouse_hold"] = typing.Process(true, false) == (false, false) && typing.Process(true, true) == (false, false) && typing.Process(false, true) == (false, false);
        consumed.Process(true, true); consumed.Reset();
        checks["shortcut_change_resets_old_press"] = consumed.Process(true, true) == (true, true) && consumed.Process(false, false) == (true, false);
        var firstNormal = new ShortcutPressGate(); firstNormal.Process(true, false); firstNormal.ReserveRepeatsForGesture();
        checks["key_first_drag_reserves_repeats_only"] = firstNormal.Process(true, false) == (true, false) && firstNormal.Process(false, false) == (false, false);
        var keyFirst = new ShortcutOrderGate();
        checks["key_first_does_not_open_without_mouse"] = !keyFirst.KeyDown(false) && !keyFirst.Move(200, 200);
        keyFirst.MouseDown(20, 20);
        checks["key_first_click_without_drag_stays_closed"] = !keyFirst.Move(22, 22);
        checks["key_first_then_drag_opens"] = keyFirst.Move(30, 20);
        checks["key_first_opens_once"] = !keyFirst.Move(60, 20);
        var dragFirst = new ShortcutOrderGate();
        checks["drag_first_then_key_opens_once"] = dragFirst.KeyDown(true) && !dragFirst.KeyDown(true);
        var releasedFirst = new ShortcutOrderGate(); releasedFirst.KeyDown(false); releasedFirst.KeyUp(); releasedFirst.MouseDown(0, 0);
        checks["key_released_before_drag_does_not_open"] = !releasedFirst.Move(20, 0);
        var missedMouseUp = new ShortcutOrderGate(); missedMouseUp.KeyDown(false); missedMouseUp.MouseDown(0, 0); missedMouseUp.KeyUp(); missedMouseUp.KeyDown(false);
        checks["new_press_discards_stale_mouse_down"] = !missedMouseUp.Move(100, 100);
        missedMouseUp.MouseDown(100, 100);
        checks["new_drag_after_cancel_still_opens"] = missedMouseUp.Move(110, 100);
        var clickFirst = new ShortcutOrderGate(); clickFirst.KeyDown(false); clickFirst.MouseDown(0, 0); clickFirst.MouseUp(); clickFirst.MouseDown(10, 10);
        checks["key_held_across_click_then_drag"] = clickFirst.Move(25, 10);
        keyFirst.Reset();
        checks["cancel_disarms_key_first_gesture"] = !keyFirst.Held && !keyFirst.Move(300, 300);
        foreach (string legacy in new[] { "RCTRL", "LCTRL", "CTRL DESTRO", "CTRL SINISTRO", "VK:11", "VK:12", "VK:10", "VK:5B", "VK:1B", "VK:2E", "FN", "F12" })
        {
            try { WheelShortcut.Parse(legacy); checks["legacy_rejected_" + legacy] = false; }
            catch (ArgumentException) { checks["legacy_rejected_" + legacy] = true; }
        }
        foreach (string option in new[] { "--evidence", "--trigger", "--report" })
        {
            try { Program.OptionValue([option], option); checks["missing_value_" + option] = false; }
            catch (ArgumentException) { checks["missing_value_" + option] = true; }
            try { Program.OptionValue([option, "--ui"], option); checks["flag_as_value_" + option] = false; }
            catch (ArgumentException) { checks["flag_as_value_" + option] = true; }
        }
        checks["files_end_before_following_options"] = Program.FileArguments(["--files", "one.png", "two.png", "--evidence", "out"]).SequenceEqual(new[] { "one.png", "two.png" });
        checks["null_preference_is_rejected"] = false;
        try { WheelShortcut.Parse(null); } catch (ArgumentException) { checks["null_preference_is_rejected"] = true; }
        try { Program.FileArguments(["--files", "--ui"]); checks["empty_file_argument_rejected"] = false; }
        catch (ArgumentException) { checks["empty_file_argument_rejected"] = true; }
        checks["single_png_still_has_jpg"] = Policy.Outputs(["image.png"]).Contains("JPG");
        checks["mixed_mp4_does_not_silently_convert_subset"] = Policy.Outputs(["image.png", "video.mp4"]).Length == 0;
        checks["mixed_payload_explains_blocker"] = Policy.AvailabilityMessage(["image.png", "video.mp4"]).Contains("MP4");
        checks["supported_batch_unchanged"] = Policy.Outputs(["image.png", "image.webp"]).Contains("JPG");
        checks["supported_single_no_error"] = Policy.AvailabilityMessage(["image.png"]) == "";
        byte[] sound = WheelSound.CreateWave();
        checks["original_sound_is_pcm_wave"] = System.Text.Encoding.ASCII.GetString(sound, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(sound, 8, 4) == "WAVE" && BitConverter.ToInt16(sound, 20) == 1;
        checks["sound_duration_40ms"] = BitConverter.ToInt32(sound, 24) == 44100 && (sound.Length - 44) == 1764 * 2;
        var samples = Enumerable.Range(0, (sound.Length - 44) / 2).Select(i => (int)BitConverter.ToInt16(sound, 44 + i * 2)).ToArray();
        checks["sound_no_clipping"] = samples.Max(s => Math.Abs(s)) < short.MaxValue;
        checks["sound_audible_body"] = Math.Sqrt(samples.Average(s => (double)s * s)) > 1000;
        checks["sound_faded_ends"] = samples[0] == 0 && Math.Abs(samples[^1]) < 5;
        checks["sound_tick_transient"] = Enumerable.Range(1, Math.Min(441, samples.Length - 1)).Count(i => Math.Sign(samples[i]) != Math.Sign(samples[i - 1])) > 12;
        var txtOutputs = Policy.Outputs(["note.txt"], true, true);
        checks["txt_outputs_visible_in_wheel"] = txtOutputs.Length == 12 && Wheel.OrderFormats(txtOutputs).ToHashSet().SetEquals(txtOutputs);
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
            && Wheel.OrderFormats(Policy.Outputs(["flip.context.png"], false, false, false)).SequenceEqual(pngNative.Select(e => e.TargetFormat));
        checks["context_pdf_outputs_with_pandoc"] = ContextMenuCatalog.ForExtension(".pdf", new ContextMenuCatalog.EngineSnapshot(true, false, false))
            .Select(e => e.TargetFormat).ToHashSet().SetEquals(Policy.Outputs(["flip.context.pdf"], true, false, false).ToHashSet());
        checks["context_docx_outputs_with_document_tools"] = ContextMenuCatalog.ForExtension(".docx", fullEngines)
            .Select(e => e.TargetFormat).ToHashSet().SetEquals(Policy.Outputs(["flip.context.docx"], true, true, true).ToHashSet());
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
                var policy = Wheel.OrderFormats(Policy.Outputs(["flip.context" + ext], engines.Pandoc, engines.Office, engines.Ffmpeg));
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
        checks["wheel_progress_arc_geometry"] = Wheel.ProgressArcData(50).Bounds.Width > 80 && Wheel.ProgressArcData(95).Bounds.Height > 150;
        checks["update_checker_version_compare"] = UpdateChecker.IsRemoteNewer("0.4.0-beta.3", "v0.5.0") && UpdateChecker.IsRemoteNewer("0.4.0-beta.3", "v0.4.0") && !UpdateChecker.IsRemoteNewer("0.4.0-beta.3", "v0.4.0-beta.3");
        foreach (int count in new[] { 1, 2, 3, 6, 8, 12 })
        {
            bool correct = true;
            for (int i = 0; i < count; i++)
            {
                double angle = (i * 360.0 / count - 90) * Math.PI / 180;
                var point = new System.Windows.Point(210 + 143 * Math.Cos(angle), 210 + 143 * Math.Sin(angle));
                for (int j = 0; j < count; j++) correct &= Wheel.SectorData(j, count).FillContains(point) == (i == j);
                correct &= !Wheel.SectorData(i, count).FillContains(new System.Windows.Point(210, 210));
                correct &= Wheel.SectorData(i, count).Bounds.Left >= 24 && Wheel.SectorData(i, count).Bounds.Right <= 396;
            }
            checks["dynamic_sector_geometry_" + count] = correct;
        }
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
        checks["startup_command_quoted"] = StartupIntegration.CommandFor(Path.Combine(Path.GetTempPath(), "Flip app", "Flip.exe")).EndsWith("Flip.exe\" --background");
        var preferences = System.Text.Json.JsonSerializer.Deserialize<AppPreferences>("{\"SoundEnabled\":false,\"Trigger\":\"LCTRL\"}");
        checks["mute_and_key_roundtrip"] = preferences != null && !preferences.SoundEnabled && preferences.Trigger == "LCTRL";
        Directory.CreateDirectory(Evidence.DirectoryPath);
        string cleanupRoot = Path.Combine(Evidence.DirectoryPath, "cleanup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cleanupRoot);
        string cleanupJob = Path.Combine(cleanupRoot, ".flip-job-" + Guid.NewGuid().ToString("N"));
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
        File.WriteAllText(Path.Combine(Evidence.DirectoryPath, "input-tests.json"),
            JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, passed = checks.Count(c => c.Value), failed = checks.Count(c => !c.Value), checks }, new JsonSerializerOptions { WriteIndented = true }));
        return checks.All(c => c.Value) ? 0 : 1;
    }

    static void AddMenuConversionChecks(Dictionary<string, bool> checks)
    {
        string root = Path.Combine(Path.GetTempPath(), "flip-m2-" + Guid.NewGuid().ToString("N"));
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
            Check(checks, "menu_startup_does_not_open_wheel", () => StartupSkipsWheel(photo));
            Check(checks, "menu_legacy_ipc_unchanged", () => LegacyIpc(photo));
            Check(checks, "menu_old_instance_not_contacted", () => OldInstanceNotContacted(photo, engines));
            Check(checks, "menu_forward_failure_not_success", () => ForwardFailureNotSuccess(photo, engines));
            Check(checks, "menu_payload_revalidated", () => PayloadRevalidated(photo, engines));
            Check(checks, "menu_false_success_rejected", () => FalseSuccessRejected());
            Check(checks, "menu_ipc_connect_failure", () => ConnectFailure(photo));
            Check(checks, "menu_compatible_mutex_detected", () => MutexProbe());
            checks["menu_ipc_success_waits_for_engine"] = false;
            checks["menu_ipc_busy_rejected"] = false;
            try { Task.Run(() => IpcRoundtrip(checks, photo, engines)).GetAwaiter().GetResult(); }
            catch (Exception ex) { Console.Error.WriteLine("menu_ipc: " + ex); }
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
        bool network = MenuConversion.TryPrepare(new MenuConversion.Request("JPG", @"\\flip-m2\share\foto.png"), engines, out _, out string networkError);
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
            && Program.OpensWheelOnStartup(["--files", photo])
            && !Program.OpensWheelOnStartup(["--files", photo, "--multi"])
            && Program.OpensPanelOnStartup(["--multi", "--files", photo])
            && !Program.OpensWheelOnStartup(["--background"])
            && !Program.OpensPanelOnStartup(["--settings"])
            && !Program.OpensWheelOnStartup(["--files", photo, "--convert-menu"])
            && !Program.OpensPanelOnStartup(["--files", photo, "--multi", "--convert-menu"]);
    }

    static bool LegacyIpc(string photo)
    {
        string wire = MenuConversion.Serialize(new MenuConversion.Request("JPG", photo));
        return Ipc.Classify("exit") == Ipc.Kind.Exit
            && Ipc.Classify("sound-test") == Ipc.Kind.SoundTest
            && Ipc.Classify("settings") == Ipc.Kind.Settings
            && Ipc.Classify("trigger:DOWN") == Ipc.Kind.Trigger
            && Ipc.TriggerValue("trigger:DOWN") == "DOWN"
            && Ipc.Classify("select") == Ipc.Kind.Select
            && Ipc.Classify("multi:[\"a.png\"]") == Ipc.Kind.Multi
            && Ipc.MultiValue("multi:[\"a.png\"]") == "[\"a.png\"]"
            && Ipc.Classify("[\"a.png\"]") == Ipc.Kind.Files
            && Ipc.Classify(null) == Ipc.Kind.None
            && Ipc.Classify(wire) == Ipc.Kind.Files
            && MenuConversion.MenuPipeName != Program.PipeName
            && MenuConversion.MenuPipeName.EndsWith(".menu", StringComparison.Ordinal)
            && Program.FileArguments(["--files", "one.png", "two.png", "--convert-menu"]).SequenceEqual(new[] { "one.png", "two.png" })
            && Program.OptionValue(["--set-trigger", "DOWN"], "--set-trigger") == "DOWN";
    }

    static bool OldInstanceNotContacted(string photo, ContextMenuCatalog.EngineSnapshot engines)
    {
        var probe = new MenuProbe { Compatible = false, OwnedByOther = true, Engines = engines };
        int code = MenuConversion.Launch(["--convert-menu", "JPG", "--files", photo], probe);
        return code == 1 && probe.Forwards == 0 && probe.Hosts == 0 && probe.Tells == 1 && probe.Told == MenuConversion.OldInstanceMessage;
    }

    static bool ForwardFailureNotSuccess(string photo, ContextMenuCatalog.EngineSnapshot engines)
    {
        var failed = new MenuProbe
        {
            Compatible = true,
            Engines = engines,
            ForwardOutcome = new MenuConversion.Outcome(MenuConversion.Status.ForwardFailed, MenuConversion.ForwardFailedMessage, null, null)
        };
        int failedCode = MenuConversion.Launch(["--convert-menu", "JPG", "--files", photo], failed);
        var thrown = new MenuProbe { Compatible = true, Engines = engines, ForwardError = new IOException("pipe chiusa") };
        int thrownCode = MenuConversion.Launch(["--convert-menu", "JPG", "--files", photo], thrown);
        var engine = new MenuProbe
        {
            Compatible = true,
            Engines = engines,
            ForwardOutcome = new MenuConversion.Outcome(MenuConversion.Status.Error, "Encoder non disponibile.", null, null)
        };
        int engineCode = MenuConversion.Launch(["--convert-menu", "JPG", "--files", photo], engine);
        var started = new MenuProbe { Compatible = false, OwnedByOther = false, Engines = engines };
        int startedCode = MenuConversion.Launch(["--convert-menu", "JPG", "--files", photo], started);
        var success = new MenuProbe
        {
            Compatible = true,
            Engines = engines,
            ForwardOutcome = new MenuConversion.Outcome(MenuConversion.Status.Success, "Creato out.jpg in C:\\temp.", @"C:\temp\out.jpg", @"C:\temp")
        };
        int successCode = MenuConversion.Launch(["--convert-menu", "JPG", "--files", photo], success);
        return failedCode == 1 && failed.Forwards == 1 && failed.Hosts == 0 && failed.Tells == 1
            && thrownCode == 1 && thrown.Forwards == 1 && thrown.Hosts == 0 && thrown.Tells == 1 && thrown.Told == MenuConversion.ForwardFailedMessage
            && engineCode == 1 && engine.Tells == 0 && engine.Hosts == 0 && engine.Forwards == 1
            && startedCode == 0 && started.Hosts == 1 && started.Forwards == 0 && started.Tells == 0
            && successCode == 0 && success.Tells == 0 && success.Hosts == 0 && success.Forwards == 1;
    }

    static bool PayloadRevalidated(string photo, ContextMenuCatalog.EngineSnapshot engines)
    {
        string json = "{\"V\":1,\"Format\":\"DOCX\",\"File\":\"" + photo.Replace("\\", "\\\\") + "\"}";
        if (!MenuConversion.TryDeserialize(json, out var request, out _)) return false;
        int calls = 0;
        var outcome = Task.Run(() => MenuConversion.Execute(request, (_, _, _) =>
        {
            calls++;
            return Task.FromResult(new ConversionResult(photo, "DOCX", Array.Empty<string>(), null));
        }, _ => { }, CancellationToken.None, engines)).GetAwaiter().GetResult();
        bool badVersion = !MenuConversion.TryDeserialize("{\"V\":2,\"Format\":\"JPG\",\"File\":\"" + photo.Replace("\\", "\\\\") + "\"}", out _, out _);
        return badVersion && calls == 0 && !outcome.Succeeded && outcome.Message.Contains("non disponibile", StringComparison.Ordinal);
    }

    static bool FalseSuccessRejected()
    {
        var missing = MenuConversion.ParseReply("{\"V\":1,\"Status\":\"success\",\"Message\":\"Creato finto.jpg\",\"Output\":\"C:\\\\no\\\\finto.jpg\",\"Folder\":\"C:\\\\no\"}");
        var unknown = MenuConversion.ParseReply("{\"V\":1,\"Status\":\"ok\",\"Message\":\"fatto\",\"Output\":null,\"Folder\":null}");
        return missing.Status == MenuConversion.Status.Error && !missing.Succeeded
            && unknown.Status == MenuConversion.Status.ForwardFailed && !unknown.Succeeded;
    }

    static bool ConnectFailure(string photo)
    {
        var outcome = MenuConversion.Forward("Flip.MenuTest.Missing." + Guid.NewGuid().ToString("N"), new MenuConversion.Request("JPG", photo), 400);
        return outcome.Status == MenuConversion.Status.ForwardFailed && !outcome.Succeeded && outcome.Message == MenuConversion.ForwardFailedMessage;
    }

    static bool MutexProbe()
    {
        string name = "Local\\Flip.MenuConvert.Test." + Guid.NewGuid().ToString("N");
        using var held = new MenuConversion.HeldMutex(name);
        bool visible = Task.Run(() => MenuConversion.MutexHeldByOther(name)).GetAwaiter().GetResult();
        held.Dispose();
        bool free = Task.Run(() => MenuConversion.MutexHeldByOther(name)).GetAwaiter().GetResult();
        return visible && !free;
    }

    static async Task IpcRoundtrip(Dictionary<string, bool> checks, string photo, ContextMenuCatalog.EngineSnapshot engines)
    {
        string pipe = "Flip.MenuTest." + Guid.NewGuid().ToString("N");
        string output = Path.Combine(Path.GetDirectoryName(photo)!, "ipc-out.jpg");
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var stop = new CancellationTokenSource();
        bool busy = false;
        CancellationTokenSource? job = null;
        int calls = 0;
        var sync = new SemaphoreSlim(1, 1);
        var request = new MenuConversion.Request("JPG", photo);
        Task serve = MenuConversion.ServeAsync(pipe, async (incoming, token) =>
        {
            await sync.WaitAsync(token).ConfigureAwait(false);
            Task<MenuConversion.Outcome> work;
            try
            {
                work = MenuConversion.RunExclusive(() => busy, value => busy = value, () => job, value => job = value, incoming,
                    async (path, format, jobToken) =>
                    {
                        calls++;
                        entered.TrySetResult(true);
                        await release.Task.WaitAsync(jobToken).ConfigureAwait(false);
                        await File.WriteAllBytesAsync(output, new byte[] { 1, 2, 3, 4 }, jobToken).ConfigureAwait(false);
                        return new ConversionResult(path, format, new[] { output }, null);
                    }, _ => { }, engines);
            }
            finally { sync.Release(); }
            return await work.ConfigureAwait(false);
        }, stop.Token, ready);
        if (!await ready.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false)) throw new TimeoutException("Server non pronto.");
        var client = Task.Run(() => MenuConversion.Forward(pipe, request, 3000));
        if (!await entered.Task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false)) throw new TimeoutException("Motore non raggiunto.");
        if (client.IsCompleted) throw new InvalidOperationException("L'inoltro è terminato prima del motore.");
        MenuConversion.Outcome second = MenuConversion.Forward(pipe, request, 3000);
        if (second.Status == MenuConversion.Status.ForwardFailed)
        {
            await Task.Delay(200).ConfigureAwait(false);
            second = MenuConversion.Forward(pipe, request, 3000);
        }
        checks["menu_ipc_busy_rejected"] = second.Status == MenuConversion.Status.Busy && !second.Succeeded && calls == 1 && !client.IsCompleted;
        release.TrySetResult(true);
        var outcome = await client.ConfigureAwait(false);
        checks["menu_ipc_success_waits_for_engine"] = outcome.Succeeded
            && outcome.Message.Contains(Path.GetFileName(output), StringComparison.OrdinalIgnoreCase)
            && outcome.Message.Contains(Path.GetDirectoryName(output)!, StringComparison.Ordinal)
            && calls == 1
            && File.Exists(output);
        stop.Cancel();
        try { await serve.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); } catch (OperationCanceledException) { }
    }

    sealed class MenuProbe : MenuConversion.IEndpoint
    {
        internal bool Compatible;
        internal bool OwnedByOther;
        internal MenuConversion.Outcome ForwardOutcome = new(MenuConversion.Status.Error, "non impostato", null, null);
        internal Exception? ForwardError;
        internal int Forwards, Hosts, Tells;
        internal string? Told;
        public ContextMenuCatalog.EngineSnapshot Engines { get; set; } = new(false, false, false);
        public bool CompatibleHostRunning() => Compatible;
        public MenuConversion.Outcome Forward(MenuConversion.Request request)
        {
            Forwards++;
            if (ForwardError != null) throw ForwardError;
            return ForwardOutcome;
        }
        public bool TryBecomeHost(string[] args, out int exitCode)
        {
            exitCode = OwnedByOther ? 1 : 0;
            if (OwnedByOther) return false;
            Hosts++;
            return true;
        }
        public void Tell(string message) { Tells++; Told = message; }
    }
}
