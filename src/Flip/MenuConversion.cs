using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace Flip;

/// <summary>Conversione diretta da riga di comando, senza ruota. La pipe è dedicata: i messaggi precedenti restano su <see cref="Program.PipeName"/>.</summary>
internal static class MenuConversion
{
    internal const string CompatibilityMutexName = "Local\\Flip.MenuConvert.v1";
    internal const int MenuPipeInstances = 4;
    internal const string OldInstanceMessage = "È attiva una versione di Flip che non supporta questo comando. Nessuna richiesta è stata inviata.";
    internal const string ForwardFailedMessage = "Impossibile inoltrare la richiesta a Flip. La conversione non è stata eseguita.";
    internal const string BusyMessage = "Una conversione è già in corso.";
    internal const string CancelledMessage = "Conversione interrotta. Gli originali sono intatti.";
    internal const string InvalidRequestMessage = "Richiesta non valida.";
    internal static string MenuPipeName => Program.PipeName + ".menu";

    internal enum Status { Success, Error, Cancelled, Busy, ForwardFailed }

    internal sealed record Request(string Format, string File);

    internal sealed record Outcome(Status Status, string Message, string? Output, string? Folder)
    {
        internal bool Succeeded => Status == Status.Success;
    }

    internal interface IEndpoint
    {
        ContextMenuCatalog.EngineSnapshot Engines { get; }
        bool CompatibleHostRunning();
        Outcome Forward(Request request);
        bool TryBecomeHost(string[] args, out int exitCode);
        void Tell(string message);
    }

    internal static int Launch(string[] args, IEndpoint endpoint)
    {
        if (!TryParse(args, out var request, out var error) || !TryPrepare(request, endpoint.Engines, out request, out error))
        {
            endpoint.Tell(error);
            return 1;
        }
        if (endpoint.CompatibleHostRunning())
        {
            Outcome outcome;
            try { outcome = endpoint.Forward(request); }
            catch (Exception) { endpoint.Tell(ForwardFailedMessage); return 1; }
            if (outcome.Status == Status.ForwardFailed)
            {
                endpoint.Tell(string.IsNullOrWhiteSpace(outcome.Message) ? ForwardFailedMessage : outcome.Message);
                return 1;
            }
            return outcome.Succeeded ? 0 : 1;
        }
        if (!endpoint.TryBecomeHost(args, out int exitCode))
        {
            endpoint.Tell(OldInstanceMessage);
            return 1;
        }
        return exitCode;
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

    internal static Outcome Forward(string pipeName, Request request, int connectTimeoutMs)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut);
            pipe.Connect(connectTimeoutMs);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true };
            using var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true);
            writer.WriteLine(Serialize(request));
            string? line = reader.ReadLine();
            var outcome = ParseReply(line);
            if (outcome.Succeeded && SamePath(outcome.Output, request.File))
                return new Outcome(Status.Error, "La conversione non è stata confermata.", null, null);
            return outcome;
        }
        catch (TimeoutException) { return new Outcome(Status.ForwardFailed, ForwardFailedMessage, null, null); }
        catch (IOException) { return new Outcome(Status.ForwardFailed, ForwardFailedMessage, null, null); }
        catch (UnauthorizedAccessException) { return new Outcome(Status.ForwardFailed, ForwardFailedMessage, null, null); }
    }

    internal static async Task ServeAsync(string pipeName, Func<Request, CancellationToken, Task<Outcome>> handle, CancellationToken token, TaskCompletionSource<bool>? ready = null, Action<string>? log = null)
    {
        while (!token.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, MenuPipeInstances, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                if (ready != null) { ready.TrySetResult(true); ready = null; }
                await pipe.WaitForConnectionAsync(token).ConfigureAwait(false);
                var active = pipe;
                pipe = null;
                _ = HandleConnectionAsync(active, handle, token, log);
            }
            catch (OperationCanceledException)
            {
                pipe?.Dispose();
                break;
            }
            catch (Exception ex)
            {
                pipe?.Dispose();
                log?.Invoke(ex.Message);
                if (!token.IsCancellationRequested) await Task.Delay(300, token).ConfigureAwait(false);
            }
        }
    }

    internal static string Serialize(Request request) => JsonSerializer.Serialize(new WireRequest(1, request.Format, request.File));

    internal static bool TryDeserialize(string? line, out Request request, out string error)
    {
        request = new Request("", "");
        error = InvalidRequestMessage;
        if (string.IsNullOrWhiteSpace(line) || line.Length > 32768) return false;
        try
        {
            var wire = JsonSerializer.Deserialize<WireRequest>(line);
            if (wire == null || wire.V != 1 || string.IsNullOrWhiteSpace(wire.Format) || string.IsNullOrWhiteSpace(wire.File)) return false;
            if (!IsFormatToken(wire.Format)) { error = "Formato non valido."; return false; }
            request = new Request(wire.Format, wire.File);
            return true;
        }
        catch (JsonException) { return false; }
    }

    internal static Outcome ParseReply(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > 32768)
            return new Outcome(Status.ForwardFailed, ForwardFailedMessage, null, null);
        try
        {
            var reply = JsonSerializer.Deserialize<WireReply>(line);
            if (reply == null || reply.V != 1 || string.IsNullOrWhiteSpace(reply.Status))
                return new Outcome(Status.ForwardFailed, ForwardFailedMessage, null, null);
            string message = OneLine(reply.Message ?? "");
            if (reply.Status.Equals("success", StringComparison.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(reply.Output) || !File.Exists(reply.Output))
                    return new Outcome(Status.Error, "La conversione non è stata confermata.", null, null);
                string full = Path.GetFullPath(reply.Output);
                string folder = Path.GetDirectoryName(full) ?? "";
                string fileName = Path.GetFileName(full);
                if (string.IsNullOrWhiteSpace(message) || message.IndexOf(fileName, StringComparison.OrdinalIgnoreCase) < 0)
                    message = SuccessMessage(new[] { full });
                return new Outcome(Status.Success, message, full, folder);
            }
            if (reply.Status.Equals("cancelled", StringComparison.Ordinal))
                return new Outcome(Status.Cancelled, string.IsNullOrWhiteSpace(message) ? CancelledMessage : message, null, null);
            if (reply.Status.Equals("busy", StringComparison.Ordinal))
                return new Outcome(Status.Busy, string.IsNullOrWhiteSpace(message) ? BusyMessage : message, null, null);
            if (reply.Status.Equals("error", StringComparison.Ordinal))
                return new Outcome(Status.Error, string.IsNullOrWhiteSpace(message) ? "Conversione non riuscita." : message, null, null);
            return new Outcome(Status.ForwardFailed, ForwardFailedMessage, null, null);
        }
        catch (JsonException) { return new Outcome(Status.ForwardFailed, ForwardFailedMessage, null, null); }
        catch (IOException) { return new Outcome(Status.ForwardFailed, ForwardFailedMessage, null, null); }
    }

    internal static bool CompatibleHostRunning() => MutexHeldByOther(CompatibilityMutexName);

    internal static HeldMutex HoldCompatibilityMutex() => new(CompatibilityMutexName);

    internal static bool MutexHeldByOther(string name)
    {
        try
        {
            using var mutex = Mutex.OpenExisting(name);
            try
            {
                if (!mutex.WaitOne(0)) return true;
            }
            catch (AbandonedMutexException)
            {
                mutex.ReleaseMutex();
                return false;
            }
            mutex.ReleaseMutex();
            return false;
        }
        catch (WaitHandleCannotBeOpenedException) { return false; }
        catch (UnauthorizedAccessException) { return true; }
    }

    internal sealed class HeldMutex : IDisposable
    {
        readonly Mutex mutex;
        bool released;
        internal HeldMutex(string name)
        {
            mutex = new Mutex(false, name);
            try { mutex.WaitOne(); }
            catch (AbandonedMutexException) { }
        }
        public void Dispose()
        {
            if (!released)
            {
                try { mutex.ReleaseMutex(); }
                catch (ApplicationException) { }
                released = true;
            }
            mutex.Dispose();
        }
    }

    static async Task HandleConnectionAsync(NamedPipeServerStream pipe, Func<Request, CancellationToken, Task<Outcome>> handle, CancellationToken token, Action<string>? log)
    {
        try
        {
            using (pipe)
            using (var reader = new StreamReader(pipe, Encoding.UTF8, false, 1024, leaveOpen: true))
            using (var writer = new StreamWriter(pipe, new UTF8Encoding(false), 1024, leaveOpen: true) { AutoFlush = true })
            {
                using var readLimit = CancellationTokenSource.CreateLinkedTokenSource(token);
                readLimit.CancelAfter(TimeSpan.FromSeconds(5));
                string? line = await reader.ReadLineAsync(readLimit.Token).ConfigureAwait(false);
                Outcome outcome;
                if (!TryDeserialize(line, out var request, out var error)) outcome = new Outcome(Status.Error, error, null, null);
                else outcome = await handle(request, token).ConfigureAwait(false);
                await writer.WriteLineAsync(Serialize(outcome)).ConfigureAwait(false);
            }
        }
        catch (Exception ex) { log?.Invoke(ex.Message); }
    }

    static string Serialize(Outcome outcome)
    {
        string status = outcome.Status switch
        {
            Status.Success => "success",
            Status.Cancelled => "cancelled",
            Status.Busy => "busy",
            Status.ForwardFailed => "error",
            _ => "error"
        };
        return JsonSerializer.Serialize(new WireReply(1, status, OneLine(outcome.Message), outcome.Status == Status.Success ? outcome.Output : null, outcome.Status == Status.Success ? outcome.Folder : null));
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

    static bool SamePath(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
        try { return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase); }
        catch (Exception) { return false; }
    }

    static string OneLine(string text) => text.Replace("\r", " ").Replace("\n", " ").Trim();

    internal sealed record WireRequest(int V, string Format, string File);
    internal sealed record WireReply(int V, string Status, string Message, string? Output, string? Folder);
}
