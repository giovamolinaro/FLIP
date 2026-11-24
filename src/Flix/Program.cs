using System.IO;
using System.Text.Json;
using System.Windows;

namespace Flix;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (ContextMenuNext.IsDocumentMenuCommand(args)) return ContextMenuNext.ApplyDocuments(args);
            if (ContextMenuNext.IsMediaMenuCommand(args)) return ContextMenuNext.ApplyMedia(args);
            if (ContextMenuNext.IsImageMenuCommand(args)) return ContextMenuNext.ApplyImages(args);
            if (ContextMenuNext.IsContentMenuCommand(args)) return ContextMenuNext.ApplyContent(args);
            if (args.Contains("--preview-menu-progress")) return MenuProgressWindow.ShowPreview();
            if (args.Contains("--convert-menu")) return MenuConversion.Launch(args, new LiveMenuEndpoint());
            int evidence = Array.IndexOf(args, "--evidence");
            if (evidence >= 0) Evidence.DirectoryPath = Path.GetFullPath(OptionValue(args, "--evidence"));
            if (args.Contains("--test-input")) return InputTests.Run();
            if (args.Contains("--test-conversions")) return ConversionTests.Run().GetAwaiter().GetResult();
            if (args.Contains("--install-context-menu-preview") || args.Contains("--remove-context-menu-preview"))
            {
                ContextMenuPreview.Apply(args.Contains("--remove-context-menu-preview"));
                return 0;
            }
            if (ContextMenuNext.IsPlannedCommand(args))
            {
                ContextMenuNext.Apply(args.Contains("--remove-context-menu-png"));
                return 0;
            }
            if (args.Contains("--convert"))
            {
                int format = Array.IndexOf(args, "--convert");
                int files = Array.IndexOf(args, "--files");
                if (files < 0 || format < 0 || format + 1 >= args.Length || files + 1 >= args.Length || args[format + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("--convert PNG,PDF --files file1 file2");
                var results = ConversionEngine.Convert(FileArguments(args).Select(Path.GetFullPath).ToArray(), args[format + 1].Split(','), CancellationToken.None).GetAwaiter().GetResult();
                string json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(json);
                int report = Array.IndexOf(args, "--report");
                if (report >= 0) File.WriteAllText(OptionValue(args, "--report"), json);
                return results.Any(result => result.Error != null) ? 1 : 0;
            }
            Evidence.Log("process.exit.idle", new { args });
            return 0;
        }
        catch (Exception ex)
        {
            Evidence.Log("fatal", ex.ToString());
            if (args.Contains("--convert") || args.Contains("--test-input") || args.Contains("--test-conversions"))
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
            if (args.Contains("--convert-menu"))
            {
                try { MenuSession.ShowNotice(ex.Message); }
                catch (Exception) { MessageBox.Show(ex.Message, "FLIX", MessageBoxButton.OK, MessageBoxImage.Error); }
                return 1;
            }
            MessageBox.Show(ex.Message, "FLIX", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }

    internal static string[] FileArguments(string[] args)
    {
        int index = Array.IndexOf(args, "--files");
        string[] files = index < 0 ? [] : args.Skip(index + 1).TakeWhile(arg => !arg.StartsWith("--", StringComparison.Ordinal)).ToArray();
        if (files.Length == 0) throw new ArgumentException("Manca un file dopo --files.");
        return files;
    }

    internal static string OptionValue(string[] args, string option)
    {
        int index = Array.IndexOf(args, option);
        if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Manca il valore per " + option + ".");
        return args[index + 1];
    }

    internal static bool StartsResidentHost(string[] args)
    {
        _ = args;
        return false;
    }

    internal static bool OpensWheelOnStartup(string[] args) => StartsResidentHost(args);

    internal static bool OpensPanelOnStartup(string[] args) => StartsResidentHost(args);

    sealed class LiveMenuEndpoint : MenuConversion.IEndpoint
    {
        public ContextMenuCatalog.EngineSnapshot Engines => ContextMenuCatalog.EngineSnapshot.Detected;
        public int Run(MenuConversion.Request request) => MenuSession.Run(request);
        public void Tell(string message) => MenuSession.ShowNotice(message);
    }
}
