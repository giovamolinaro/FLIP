using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
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
        checks["catalog_not_empty"] = OperationCatalog.All.Count > 80;
        checks["catalog_ids_unique"] = OperationCatalog.All.Select(o => o.Id).Distinct().Count() == OperationCatalog.All.Count;
        var pngToJpg = OperationCatalog.Find("image.png.to.jpg");
        checks["catalog_png_jpg_native_selectable"] = pngToJpg != null && OperationCatalog.IsSelectable(pngToJpg, 1, false, false, false);
        var mp4ToMkv = OperationCatalog.Find("video.mp4.to.mkv");
        checks["catalog_video_needs_ffmpeg"] = mp4ToMkv != null && !OperationCatalog.IsSelectable(mp4ToMkv, 1, true, true, false) && OperationCatalog.IsSelectable(mp4ToMkv, 1, true, true, true);
        checks["catalog_simple_matches_policy_full_engines"] = OperationCatalog.All
            .Where(o => o.Kind == OperationCatalog.OperationKind.SimpleConversion && o.Status != OperationCatalog.OperationStatus.NotAvailable)
            .All(o =>
            {
                bool policy = Policy.Outputs(["probe" + o.InputExtensions[0]], true, true, true).Contains(o.OutputFormat);
                return policy == OperationCatalog.IsSelectable(o, 1, true, true, true);
            });
        checks["catalog_advanced_pdf_not_selectable"] = OperationCatalog.All
            .Where(o => o.Kind == OperationCatalog.OperationKind.AdvancedPdf)
            .All(o => !OperationCatalog.IsSelectable(o, Math.Max(o.RequiredFileCount, 1), true, true, true));
        checks["catalog_excluded_formats_absent"] = new[] { "format.excluded.dxf", "format.excluded.pdfa", "format.excluded.ppt", "format.excluded.pptx" }
            .All(id => OperationCatalog.Find(id) == null);
        checks["catalog_no_dxf_pdfa_ppt_outputs"] = !OperationCatalog.All.Any(o =>
            o.OutputFormat is "DXF" or "PDF/A" or "PPT" or "PPTX"
            || o.InputExtensions.Contains(".dxf", StringComparer.OrdinalIgnoreCase)
            || o.InputExtensions.Contains(".ppt", StringComparer.OrdinalIgnoreCase)
            || o.InputExtensions.Contains(".pptx", StringComparer.OrdinalIgnoreCase));
        checks["catalog_has_simple_and_advanced_kinds"] = OperationCatalog.All.Any(o => o.Kind == OperationCatalog.OperationKind.SimpleConversion && o.Status != OperationCatalog.OperationStatus.NotAvailable)
            && OperationCatalog.All.Any(o => o.Kind == OperationCatalog.OperationKind.AdvancedPdf);
        checks["catalog_html_jpg_blocked_without_office"] = OperationCatalog.Find("document.html.to.jpg") is { } htmlJpg
            && !OperationCatalog.IsSelectable(htmlJpg, 1, true, false, false)
            && OperationCatalog.IsSelectable(htmlJpg, 1, true, true, false);
        checks["catalog_pdf_to_docx_partial_with_pandoc"] = OperationCatalog.Find("pdf.pdf.to.docx") is { } pdfDocx
            && pdfDocx.Status == OperationCatalog.OperationStatus.Partial
            && OperationCatalog.IsSelectable(pdfDocx, 1, true, false, false);
        checks["catalog_merge_requires_two_files_still_off"] = OperationCatalog.Find("pdf.advanced.merge") is { } merge
            && !OperationCatalog.IsSelectable(merge, 2, true, true, true);
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
        var radial = new RadialConversionState();
        radial.Reset();
        checks["radial_initial_phase"] = radial.Phase == RadialConversionPhase.Initial && !radial.HasRealPercent && radial.ShownPercent == 0;
        radial.BeginConversion(["note.txt"], "pdf");
        checks["radial_begin_converting"] = radial.Phase == RadialConversionPhase.Converting && radial.IsIndeterminate && radial.TargetFormat == "PDF";
        checks["radial_no_percent_before_data"] = !radial.ShowNumericPercent && radial.ShownPercent == 0;
        radial.ReportProgress(null, "preparazione");
        checks["radial_step_without_percent_stays_indeterminate"] = radial.IsIndeterminate && radial.Step == "preparazione";
        radial.ReportProgress(18, null);
        checks["radial_real_percent_engaged"] = radial.HasRealPercent && radial.ShowNumericPercent && radial.TargetPercent == 18;
        radial.ReportProgress(12, null);
        checks["radial_percent_monotone"] = radial.TargetPercent >= 18;
        radial.ReportProgress(40, null);
        radial.AdvanceSmoothing();
        radial.AdvanceSmoothing();
        radial.AdvanceSmoothing();
        int shownMid = radial.ShownPercent;
        checks["radial_smoothing_never_exceeds_target_before_complete"] = shownMid <= 40 && shownMid >= 18;
        checks["radial_not_100_before_complete"] = radial.ShownPercent < 100;
        radial.Complete(true);
        checks["radial_success_shows_100"] = radial.Phase == RadialConversionPhase.CompletedSuccess && radial.ShownPercent == 100;
        radial.Reset();
        radial.BeginConversion(["a.png", "b.png"], "JPG");
        radial.ReportProgress(55, null);
        radial.Complete(false);
        checks["radial_error_not_100"] = radial.Phase == RadialConversionPhase.CompletedError && radial.ShownPercent <= 99;
        radial.Reset();
        radial.BeginConversion(["c.wav"], "MP3");
        checks["radial_cancel_only_when_confirmed"] = radial.Phase == RadialConversionPhase.Converting;
        radial.Cancel();
        checks["radial_cancelled_phase"] = radial.Phase == RadialConversionPhase.Cancelled;
        radial.Reset();
        checks["radial_reset_clears"] = radial.Phase == RadialConversionPhase.Initial && radial.TargetFormat == "";
        checks["radial_indeterminate_arc_geometry"] = RadialConversionView.IndeterminateArcData(30).Bounds.Width > 40;
        bool cancelRequested = false, escapeStillConverting = false, confirmedCancelled = false;
        Exception? radialUiError = null;
        var radialUi = new Thread(() =>
        {
            try
            {
                var view = new RadialConversionView();
                using var source = new HwndSource(new HwndSourceParameters
                {
                    Width = 320,
                    Height = 320,
                    PositionX = -32000,
                    PositionY = -32000,
                    WindowStyle = 0
                }) { RootVisual = view };
                view.BeginConversion(["doc.txt"], "PDF");
                view.Focus();
                view.CancelRequested += (_, _) => cancelRequested = true;
                view.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = UIElement.PreviewKeyDownEvent });
                escapeStillConverting = view.Phase == RadialConversionPhase.Converting;
                view.Cancel();
                confirmedCancelled = view.Phase == RadialConversionPhase.Cancelled;
            }
            catch (Exception ex) { radialUiError = ex; }
        });
        radialUi.SetApartmentState(ApartmentState.STA);
        radialUi.Start();
        radialUi.Join();
        checks["radial_escape_requests_cancel"] = radialUiError == null && cancelRequested;
        checks["radial_escape_not_auto_cancel"] = escapeStillConverting;
        checks["radial_host_cancel_confirms"] = confirmedCancelled;
        string directFixtureRoot = Path.Combine(Evidence.DirectoryPath, "direct-action-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directFixtureRoot);
        string directPng = Path.Combine(directFixtureRoot, "foto.png");
        File.WriteAllBytes(directPng, new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82, 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0, 31, 21, 196, 137, 0, 0, 0, 10, 73, 68, 65, 84, 120, 156, 99, 0, 1, 0, 0, 5, 0, 1, 13, 10, 45, 180, 0, 0, 0, 0, 73, 69, 78, 68, 174, 66, 96, 130 });
        string directTxt = Path.Combine(directFixtureRoot, "nota.txt");
        File.WriteAllText(directTxt, "prova direct action", new UTF8Encoding(false));
        var enginesFull = new DirectAction.EngineSnapshot(true, true, true);
        var enginesNative = new DirectAction.EngineSnapshot(false, false, false);
        var valid = DirectAction.Resolve(new DirectAction.Request("image.png.to.jpg", [directPng], false, true), enginesNative);
        checks["direct_action_valid_png_jpg"] = valid.Verdict == DirectAction.Verdict.Accepted
            && valid.Operation?.Id == "image.png.to.jpg"
            && valid.ValidatedFiles.Length == 1
            && valid.QualityStatus == null;
        checks["direct_action_unknown_id"] = DirectAction.Resolve(new DirectAction.Request("immagine.xyz.assente", [directPng], false, true), enginesNative).RejectReason == DirectAction.RejectReason.UnknownOperation;
        checks["direct_action_not_available"] = DirectAction.Resolve(new DirectAction.Request("format.excluded.pages", [directPng], false, true), enginesFull).RejectReason == DirectAction.RejectReason.NotAvailable;
        string directPdfPairA = Path.Combine(directFixtureRoot, "uno.pdf");
        string directPdfPairB = Path.Combine(directFixtureRoot, "due.pdf");
        File.WriteAllBytes(directPdfPairA, [37, 80, 68, 70, 45, 49, 46, 52, 10, 37, 226]);
        File.WriteAllBytes(directPdfPairB, [37, 80, 68, 70, 45, 49, 46, 52, 10, 37, 227]);
        checks["direct_action_advanced_pdf"] = DirectAction.Resolve(new DirectAction.Request("pdf.advanced.merge", [directPdfPairA, directPdfPairB], false, true), enginesFull).RejectReason == DirectAction.RejectReason.AdvancedPdfNotImplemented;
        checks["direct_action_incompatible_extension"] = DirectAction.Resolve(new DirectAction.Request("image.png.to.jpg", [directTxt], false, true), enginesNative).RejectReason == DirectAction.RejectReason.IncompatibleExtension;
        checks["direct_action_missing_file"] = DirectAction.Resolve(new DirectAction.Request("image.png.to.jpg", [Path.Combine(directFixtureRoot, "inesistente.png")], false, true), enginesNative).RejectReason == DirectAction.RejectReason.InvalidFiles;
        checks["direct_action_blocked_path"] = DirectAction.Resolve(new DirectAction.Request("image.png.to.jpg", [@"\\server\share\falso.png"], false, true), enginesNative).RejectReason == DirectAction.RejectReason.InvalidFiles;
        string fakeMp4 = Path.Combine(directFixtureRoot, "clip.mp4");
        File.WriteAllBytes(fakeMp4, [0, 0, 0, 24, 102, 116, 121, 112, 109, 112, 52, 50]);
        checks["direct_action_engine_ffmpeg_with_file"] = DirectAction.Resolve(new DirectAction.Request("video.mp4.to.mkv", [fakeMp4], false, true), enginesNative).RejectReason == DirectAction.RejectReason.EngineUnavailable;
        checks["direct_action_invalid_batch_count"] = DirectAction.Resolve(new DirectAction.Request("image.png.to.jpg", [], false, true), enginesNative).RejectReason == DirectAction.RejectReason.MissingFiles;
        checks["direct_action_option_conflict"] = DirectAction.Resolve(new DirectAction.Request("image.png.to.jpg", [directPng], true, true), enginesNative).RejectReason == DirectAction.RejectReason.OptionConflict;
        string directPdf = Path.Combine(directFixtureRoot, "doc.pdf");
        File.WriteAllBytes(directPdf, [37, 80, 68, 70, 45, 49, 46, 52, 10, 37, 226]);
        var partial = DirectAction.Resolve(new DirectAction.Request("pdf.pdf.to.docx", [directPdf], false, true), new DirectAction.EngineSnapshot(true, false, false));
        checks["direct_action_partial_preserves_limits"] = partial.Verdict == DirectAction.Verdict.Accepted
            && partial.QualityStatus == OperationCatalog.OperationStatus.Partial
            && !string.IsNullOrWhiteSpace(partial.QualityLimits);
        long pngTicks = File.GetLastWriteTimeUtc(directPng).Ticks;
        _ = DirectAction.Resolve(new DirectAction.Request("image.png.to.jpg", [directPng], false, true), enginesNative);
        checks["direct_action_validation_read_only"] = File.GetLastWriteTimeUtc(directPng).Ticks == pngTicks;
        checks["direct_action_args_roundtrip"] = DirectAction.Resolve(
            DirectAction.RequestFromArgs(["--action", "image.png.to.jpg", "--files", directPng]),
            enginesNative).Verdict == DirectAction.Verdict.Accepted;
        Directory.Delete(directFixtureRoot, true);
        File.WriteAllText(Path.Combine(Evidence.DirectoryPath, "input-tests.json"),
            JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, passed = checks.Count(c => c.Value), failed = checks.Count(c => !c.Value), checks }, new JsonSerializerOptions { WriteIndented = true }));
        return checks.All(c => c.Value) ? 0 : 1;
    }
}
