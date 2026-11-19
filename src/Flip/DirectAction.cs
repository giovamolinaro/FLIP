using System;
using System.IO;
using System.Linq;
using System.Windows;
namespace Flip;

/// <summary>Validazione richieste <c>--action</c> (Fase 2A): nessuna conversione né scrittura su disco.</summary>
internal static class DirectAction
{
    internal enum Verdict { Accepted, Rejected }

    internal enum RejectReason
    {
        OptionConflict,
        UnknownOperation,
        NotAvailable,
        AdvancedPdfNotImplemented,
        MissingFiles,
        InvalidFiles,
        IncompatibleExtension,
        OutputUnavailable,
        EngineUnavailable,
        InvalidBatch,
        NotSelectable
    }

    internal sealed record EngineSnapshot(bool Pandoc, bool Office, bool Ffmpeg);

    internal sealed record Request(string? ActionId, string[] FilePaths, bool HasConvertOption, bool HasActionOption);

    internal sealed record Result(
        Verdict Verdict,
        RejectReason? RejectReason,
        string? Message,
        OperationCatalog.Operation? Operation,
        string[] ValidatedFiles,
        OperationCatalog.OperationStatus? QualityStatus,
        string? QualityLimits);

    internal static Request RequestFromArgs(string[] args)
    {
        bool hasConvert = args.Contains("--convert");
        bool hasAction = args.Contains("--action");
        string? actionId = null;
        if (hasAction)
        {
            try { actionId = Program.OptionValue(args, "--action"); }
            catch (ArgumentException) { actionId = ""; }
        }
        string[] files = [];
        if (args.Contains("--files"))
        {
            try { files = Program.FileArguments(args); }
            catch (ArgumentException) { files = []; }
        }
        return new Request(hasAction ? actionId : null, files, hasConvert, hasAction);
    }

    internal static Result Resolve(Request request, EngineSnapshot engines) =>
        Resolve(request.ActionId, request.FilePaths, engines, request.HasConvertOption && request.HasActionOption);

    internal static Result Resolve(string? actionId, string[] filePaths, EngineSnapshot engines, bool optionConflict = false)
    {
        if (optionConflict)
            return Reject(RejectReason.OptionConflict, "Non usare --action insieme a --convert.");

        if (string.IsNullOrWhiteSpace(actionId))
            return Reject(RejectReason.UnknownOperation, "Operazione sconosciuta o ID mancante.");

        var operation = OperationCatalog.Find(actionId.Trim());
        if (operation == null)
            return Reject(RejectReason.UnknownOperation, "Operazione sconosciuta: " + actionId);

        if (operation.Kind == OperationCatalog.OperationKind.AdvancedPdf)
            return Reject(RejectReason.AdvancedPdfNotImplemented, "Operazioni PDF avanzate non ancora implementate.");

        if (operation.Status == OperationCatalog.OperationStatus.NotAvailable)
            return Reject(RejectReason.NotAvailable, "Operazione non disponibile: " + operation.Id);

        if (filePaths.Length == 0)
            return Reject(RejectReason.MissingFiles, "Manca almeno un file dopo --files.");

        if (filePaths.Length is 0 or > 128 || filePaths.Length < operation.RequiredFileCount)
            return Reject(RejectReason.InvalidBatch, "Numero di file non valido per l'operazione.");

        string[] validated = Policy.ReadFiles(new DataObject(DataFormats.FileDrop, filePaths));
        if (validated.Length == 0)
            return Reject(RejectReason.InvalidFiles, "File inesistente, non locale o non ammesso.");

        if (validated.Length != filePaths.Length)
            return Reject(RejectReason.InvalidFiles, "Uno o più file non sono validi.");

        foreach (var path in validated)
        {
            var ext = Path.GetExtension(path);
            if (!operation.InputExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                return Reject(RejectReason.IncompatibleExtension, "Estensione file non compatibile con l'operazione.");
        }

        if (operation.Engines.Ffmpeg && !engines.Ffmpeg)
            return Reject(RejectReason.EngineUnavailable, "Per questa operazione serve FFmpeg.");
        if (operation.Engines.Pandoc && !engines.Pandoc)
            return Reject(RejectReason.EngineUnavailable, "Per questa operazione serve Pandoc.");
        if (operation.Engines.Office && !engines.Office)
            return Reject(RejectReason.EngineUnavailable, "Per questa operazione serve LibreOffice.");

        var outputs = Policy.Outputs(validated, engines.Pandoc, engines.Office, engines.Ffmpeg);
        if (!outputs.Contains(operation.OutputFormat))
            return Reject(RejectReason.OutputUnavailable, "Formato di uscita non disponibile per i file indicati.");

        if (!OperationCatalog.IsSelectable(operation, validated.Length, engines.Pandoc, engines.Office, engines.Ffmpeg))
            return Reject(RejectReason.NotSelectable, "Operazione non selezionabile con i file e i motori indicati.");

        var limits = operation.Status == OperationCatalog.OperationStatus.Partial ? operation.QualityLimits : null;
        var status = operation.Status == OperationCatalog.OperationStatus.Partial ? operation.Status : (OperationCatalog.OperationStatus?)null;
        return new Result(Verdict.Accepted, null, null, operation, validated, status, limits);
    }

    static Result Reject(RejectReason reason, string message) =>
        new(Verdict.Rejected, reason, message, null, [], null, null);
}