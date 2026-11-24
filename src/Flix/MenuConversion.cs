using System.IO;
using System.Windows;

namespace Flix;

/// <summary>Conversione richiesta dal menu contestuale. Ogni processo esegue una sola richiesta e non inoltra a un host.</summary>
internal static class MenuConversion
{
    internal const string BusyMessage = "Una conversione è già in corso.";
    internal const string CancelledMessage = "Conversione interrotta. Gli originali sono intatti.";

    internal enum Status { Success, Error, Cancelled, Busy }

    internal sealed record Request(string Format, string File);

    internal sealed record Outcome(Status Status, string Message, string? Output, string? Folder)
    {
        internal bool Succeeded => Status == Status.Success;
    }

    internal interface IEndpoint
    {
        ContextMenuCatalog.EngineSnapshot Engines { get; }
        int Run(Request request);
        void Tell(string message);
    }

    internal static int Launch(string[] args, IEndpoint endpoint)
    {
        if (!TryParse(args, out var request, out var error) || !TryPrepare(request, endpoint.Engines, out request, out error))
        {
            endpoint.Tell(error);
            return 1;
        }
        return endpoint.Run(request);
    }

    internal static bool TryParse(string[] args, out Request request, out string error)
    {
        request = new Request("", "");
        error = "";
        if (args.Count(arg => arg.Equals("--convert-menu", StringComparison.Ordinal)) != 1)
        {
            error = "Comando --convert-menu non valido.";
            return false;
        }
        string? format = null;
        string? file = null;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.Equals("--convert-menu", StringComparison.Ordinal))
            {
                if (format != null) { error = "È ammesso un solo formato."; return false; }
                if (i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    error = "Manca il formato dopo --convert-menu.";
                    return false;
                }
                string token = args[++i];
                if (token.IndexOfAny([',', ';', ' ', '\t']) >= 0) { error = "È ammesso un solo formato."; return false; }
                if (!IsFormatToken(token)) { error = "Formato non valido."; return false; }
                format = token;
            }
            else if (arg.Equals("--files", StringComparison.Ordinal))
            {
                if (file != null) { error = "Opzione --files ripetuta."; return false; }
                int count = 0;
                string? only = null;
                while (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    only = args[++i];
                    count++;
                }
                if (count != 1) { error = count == 0 ? "Manca un file dopo --files." : "La conversione dal menu accetta un solo file."; return false; }
                file = only;
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                error = "Opzione non compatibile con --convert-menu: " + arg;
                return false;
            }
            else
            {
                error = "Argomento non riconosciuto: " + arg;
                return false;
            }
        }
        if (format == null) { error = "Manca il formato dopo --convert-menu."; return false; }
        if (file == null) { error = "Manca un file dopo --files."; return false; }
        request = new Request(format, file);
        return true;
    }

    internal static bool TryPrepare(Request raw, ContextMenuCatalog.EngineSnapshot? engines, out Request canonical, out string error)
    {
        canonical = raw;
        error = "";
        if (!IsFormatToken(raw.Format)) { error = "Formato non valido."; return false; }
        string path = raw.File ?? "";
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\", StringComparison.Ordinal) || IsNetworkDrive(path))
        {
            error = "Percorso non ammesso. Indica un file locale completo, non un percorso di rete.";
            return false;
        }
        if (!File.Exists(path)) { error = "File non trovato."; return false; }
        string[] accepted;
        try { accepted = Policy.ReadFiles(new DataObject(DataFormats.FileDrop, new[] { path })); }
        catch (Exception) { error = "File non disponibile o non ammesso."; return false; }
        if (accepted.Length != 1) { error = "File non disponibile o non ammesso."; return false; }
        string full = accepted[0];
        var snapshot = engines ?? ContextMenuCatalog.EngineSnapshot.Detected;
        string[] outputs = Policy.Outputs(new[] { full }, snapshot.Pandoc, snapshot.Office, snapshot.Ffmpeg);
        var entry = ContextMenuCatalog.ForFile(full, snapshot).FirstOrDefault(item => item.TargetFormat.Equals(raw.Format, StringComparison.OrdinalIgnoreCase));
        if (entry == null || !outputs.Contains(entry.TargetFormat))
        {
            error = "Formato non disponibile per questo file: " + raw.Format.ToUpperInvariant() + ".";
            return false;
        }
        canonical = new Request(entry.TargetFormat, full);
        return true;
    }

    internal static async Task<Outcome> RunExclusive(
        Func<bool> isBusy,
        Action<bool> setBusy,
        Func<CancellationTokenSource?> getJob,
        Action<CancellationTokenSource?> setJob,
        Request raw,
        Func<string, string, CancellationToken, Task<ConversionResult>> convert,
        Action<string> notify,
        ContextMenuCatalog.EngineSnapshot? engines = null,
        Action<Outcome>? onCompleted = null)
    {
        _ = getJob;
        if (isBusy())
        {
            var busy = new Outcome(Status.Busy, BusyMessage, null, null);
            notify(busy.Message);
            return busy;
        }
        if (!TryPrepare(raw, engines, out _, out var rejected))
        {
            var invalid = new Outcome(Status.Error, rejected, null, null);
            notify(invalid.Message);
            return invalid;
        }
        setBusy(true);
        var job = new CancellationTokenSource();
        setJob(job);
        try { return await Execute(raw, convert, notify, job.Token, engines, onCompleted); }
        finally
        {
            setJob(null);
            setBusy(false);
            job.Dispose();
        }
    }

    internal static async Task<Outcome> Execute(
        Request raw,
        Func<string, string, CancellationToken, Task<ConversionResult>> convert,
        Action<string> notify,
        CancellationToken token,
        ContextMenuCatalog.EngineSnapshot? engines = null,
        Action<Outcome>? onCompleted = null)
    {
        if (!TryPrepare(raw, engines, out var request, out var error))
        {
            var invalid = new Outcome(Status.Error, error, null, null);
            notify(invalid.Message);
            return invalid;
        }
        notify(StartedMessage(request.Format));
        ConversionResult result;
        try { result = await convert(request.File, request.Format, token); }
        catch (OperationCanceledException)
        {
            var cancelled = new Outcome(Status.Cancelled, CancelledMessage, null, null);
            onCompleted?.Invoke(cancelled);
            notify(cancelled.Message);
            return cancelled;
        }
        catch (Exception ex)
        {
            var failed = new Outcome(Status.Error, string.IsNullOrWhiteSpace(ex.Message) ? "Conversione non riuscita." : ex.Message, null, null);
            onCompleted?.Invoke(failed);
            notify(failed.Message);
            return failed;
        }
        var outcome = FromResult(request.File, result);
        onCompleted?.Invoke(outcome);
        notify(outcome.Message);
        return outcome;
    }

    internal static Outcome FromResult(string source, ConversionResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.Error) || result.Outputs.Length == 0)
            return new Outcome(Status.Error, string.IsNullOrWhiteSpace(result.Error) ? "Conversione non riuscita." : result.Error, null, null);
        string sourceFull;
        try { sourceFull = Path.GetFullPath(source); }
        catch (Exception) { return new Outcome(Status.Error, "Conversione non riuscita.", null, null); }
        var outputs = new List<string>();
        foreach (string output in result.Outputs)
        {
            if (string.IsNullOrWhiteSpace(output) || !File.Exists(output))
                return new Outcome(Status.Error, "Conversione non riuscita.", null, null);
            string full;
            try { full = Path.GetFullPath(output); }
            catch (Exception) { return new Outcome(Status.Error, "Conversione non riuscita.", null, null); }
            if (string.Equals(full, sourceFull, StringComparison.OrdinalIgnoreCase))
                return new Outcome(Status.Error, "La conversione non ha protetto il file originale.", null, null);
            outputs.Add(full);
        }
        return new Outcome(Status.Success, SuccessMessage(outputs), outputs[0], Path.GetDirectoryName(outputs[0]) ?? "");
    }

    internal static string StartedMessage(string format) => "Conversione in " + format + ".";

    internal static string SuccessMessage(IReadOnlyList<string> outputs)
    {
        string folder = Path.GetDirectoryName(outputs[0]) ?? "";
        if (outputs.Count == 1) return "Creato " + Path.GetFileName(outputs[0]) + " in " + folder + ".";
        return "Creati " + outputs.Count + " file in " + folder + ". Primo: " + Path.GetFileName(outputs[0]) + ".";
    }

    static bool IsFormatToken(string value) => value.Length is >= 1 and <= 8 && value.All(char.IsAsciiLetterOrDigit);

    static bool IsNetworkDrive(string path)
    {
        try
        {
            string? root = Path.GetPathRoot(path);
            if (string.IsNullOrWhiteSpace(root)) return true;
            return new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (ArgumentException) { return true; }
        catch (IOException) { return true; }
        catch (UnauthorizedAccessException) { return true; }
    }
}
