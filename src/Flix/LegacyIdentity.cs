using System.IO;

namespace Flix;

// Letterali storici dell'identità precedente.
// Le installazioni nuove non scrivono questi nomi.
// Servono solo a riconoscere menu già creati da questa applicazione
// e a non cancellare il menu originale protetto, l'anteprima di un altro
// proprietario o chiavi estranee.
// Il segmento di laboratorio è il percorso dell'account di collaudo già esistente.
internal static class LegacyIdentity
{
    internal const string MenuIdPrefix = "Flip.Convert.";
    internal const string OwnerValueName = "FlipOwner";
    internal const string AncestorValueName = "FlipCreatedAncestors";
    internal const string PreviewId = "Flip.Convert.Preview";
    internal const string PreviewOwnerValueName = "FlipPrototype";
    internal const string PreviewParentKey = @"Software\Classes\SystemFileAssociations\.png\shell\Flip.Convert.Preview";
    internal const string OriginalMenuKey = @"Software\Classes\*\shell\Flip.Convert";
    internal const string OriginalMenuId = "Flip.Convert";
    internal const string OriginalMenuVerb = "Converti con Flip";
    internal const string OriginalMenuCommand = "\"C:\\Flip\\Flip.exe\" --files \"%1\"";
    internal const string OriginalMenuProgramFilesCommand = "\"C:\\Program Files\\Flip\\Flip.exe\" --files \"%1\"";
    internal const string OriginalMenuIcon = @"C:\Program Files\Flip\Flip.exe,0";
    internal const string LegacyConvertCommand = "\"C:\\Programs\\Flip\\Flip.exe\" --convert-menu JPG --files \"%1\"";
    internal const string ProgramsFolderName = "Flip";
    internal const string LaboratoryPathSegment = "Flip-Lab";

    internal static string MenuId(string currentMenuId)
    {
        if (!currentMenuId.StartsWith(ContextMenuNext.MenuIdPrefix, StringComparison.Ordinal))
            throw new IOException("Identificativo menu non previsto.");
        return MenuIdPrefix + currentMenuId[ContextMenuNext.MenuIdPrefix.Length..];
    }

    internal static bool IsOriginalMenu(string key) =>
        key.Equals(OriginalMenuKey, StringComparison.OrdinalIgnoreCase)
        || key.StartsWith(OriginalMenuKey + @"\", StringComparison.OrdinalIgnoreCase);

    internal static bool IsLegacyPreview(string key) =>
        key.Equals(PreviewParentKey, StringComparison.OrdinalIgnoreCase)
        || key.StartsWith(PreviewParentKey + @"\", StringComparison.OrdinalIgnoreCase);

    internal static bool MarkerMatches(IRegistryStore store, string key, string menuId)
    {
        string? modern = store.Read(key, ContextMenuNext.OwnerValue);
        string? historical = store.Read(key, OwnerValueName);
        if (modern != null && !string.Equals(modern, menuId, StringComparison.Ordinal)) return false;
        if (historical != null && !string.Equals(historical, menuId, StringComparison.Ordinal)) return false;
        return modern != null || historical != null;
    }

    internal static void RetireOwnerMarker(IRegistryStore store, string key, string menuId)
    {
        string? historical = store.Read(key, OwnerValueName);
        if (historical == null || !string.Equals(historical, menuId, StringComparison.Ordinal)) return;
        store.DeleteValue(key, OwnerValueName);
    }
}
