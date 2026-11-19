using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Flip;

/// <summary>Finestra desktop per conversioni dirette (Fase 2B): solo UI, nessun motore.</summary>
internal sealed class DirectConversionWindow : Window
{
    readonly RadialConversionView radial = new();
    readonly TextBlock qualityLimits = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.LightGray,
        FontSize = 12,
        Margin = new Thickness(0, 4, 0, 0),
        TextAlignment = TextAlignment.Center,
        Visibility = Visibility.Collapsed
    };
    readonly TextBlock errorDetail = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
        FontSize = 12,
        Margin = new Thickness(0, 6, 0, 0),
        TextAlignment = TextAlignment.Center,
        Visibility = Visibility.Collapsed
    };
    readonly TextBlock results = new()
    {
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.White,
        FontSize = 11,
        Margin = new Thickness(0, 8, 0, 0),
        TextAlignment = TextAlignment.Center,
        MaxWidth = 360,
        Visibility = Visibility.Collapsed
    };

    bool cancelSignaled;
    bool closePending;

    public event EventHandler? CancelRequested;

    internal RadialConversionView ConversionView => radial;
    internal RadialConversionPhase Phase => radial.Phase;
    internal bool CancelSignaled => cancelSignaled;
    internal bool ClosePending => closePending;
    internal string QualityLimitsText => qualityLimits.Text;
    internal string ErrorDetailText => errorDetail.Text;
    internal string ResultsText => results.Text;
    internal string SourceLabel => RadialConversionState.FormatSourceLabel(lastSources);
    internal string TargetFormatDisplay => string.IsNullOrWhiteSpace(lastTarget) ? "" : lastTarget.Trim().ToUpperInvariant();

    IReadOnlyList<string> lastSources = [];

    string lastTarget = "";

    public DirectConversionWindow()
    {
        Title = "Flip — conversione diretta";
        Width = 400;
        SizeToContent = SizeToContent.Height;
        MinWidth = 340;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(27, 29, 32));
        Foreground = Brushes.White;

        radial.HorizontalAlignment = HorizontalAlignment.Center;
        radial.Margin = new Thickness(0, 8, 0, 0);
        radial.CancelRequested += (_, _) => SignalCancelRequest();

        var panel = new StackPanel
        {
            Margin = new Thickness(16, 8, 16, 18),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        panel.Children.Add(radial);
        panel.Children.Add(qualityLimits);
        panel.Children.Add(errorDetail);
        panel.Children.Add(results);
        Content = panel;

        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
    }

    public void BeginConversion(IReadOnlyList<string> sources, string targetFormat, string? partialQualityLimits = null)
    {
        if (string.IsNullOrWhiteSpace(targetFormat))
            throw new ArgumentException("Target format required.", nameof(targetFormat));
        lastSources = sources ?? [];
        lastTarget = targetFormat;
        cancelSignaled = false;
        closePending = false;
        ClearSupplementaryChrome();
        SetPartialLimits(partialQualityLimits);
        radial.BeginConversion(lastSources, targetFormat);
        Focus();
        radial.Focus();
    }

    public void ReportProgress(int? percent, string? step) => radial.ReportProgress(percent, step);

    public void Complete(bool success)
    {
        radial.Complete(success);
        if (!success && string.IsNullOrWhiteSpace(errorDetail.Text))
            errorDetail.Visibility = Visibility.Collapsed;
        cancelSignaled = false;
        TryCloseIfPending();
    }

    public void ConfirmCancel()
    {
        if (Phase != RadialConversionPhase.Converting) return;
        radial.Cancel();
        cancelSignaled = false;
        TryCloseIfPending();
    }

    public void SetErrorDetail(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            errorDetail.Text = "";
            errorDetail.Visibility = Visibility.Collapsed;
            return;
        }
        errorDetail.Text = message.Trim();
        errorDetail.Visibility = Visibility.Visible;
    }

    public void SetResults(IEnumerable<string>? outputPaths)
    {
        string[] paths = outputPaths?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).ToArray() ?? [];
        if (paths.Length == 0)
        {
            results.Text = "";
            results.Visibility = Visibility.Collapsed;
            return;
        }
        results.Text = string.Join(Environment.NewLine, paths.Select(Path.GetFileName));
        results.Visibility = Visibility.Visible;
    }

    public void Reset()
    {
        cancelSignaled = false;
        closePending = false;
        ClearSupplementaryChrome();
        radial.Reset();
        lastSources = [];
        lastTarget = "";
    }

    void SetPartialLimits(string? partialQualityLimits)
    {
        if (string.IsNullOrWhiteSpace(partialQualityLimits))
        {
            qualityLimits.Text = "";
            qualityLimits.Visibility = Visibility.Collapsed;
            return;
        }
        qualityLimits.Text = partialQualityLimits.Trim();
        qualityLimits.Visibility = Visibility.Visible;
    }

    void ClearSupplementaryChrome()
    {
        qualityLimits.Text = "";
        qualityLimits.Visibility = Visibility.Collapsed;
        errorDetail.Text = "";
        errorDetail.Visibility = Visibility.Collapsed;
        results.Text = "";
        results.Visibility = Visibility.Collapsed;
    }

    void SignalCancelRequest()
    {
        if (Phase != RadialConversionPhase.Converting) return;
        if (cancelSignaled) return;
        cancelSignaled = true;
        CancelRequested?.Invoke(this, EventArgs.Empty);
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Phase != RadialConversionPhase.Converting) return;
        if (e.OriginalSource is RadialConversionView) return;
        SignalCancelRequest();
        e.Handled = true;
    }

    void OnClosing(object? sender, CancelEventArgs e)
    {
        if (IsTerminalPhase()) return;
        if (Phase == RadialConversionPhase.Converting)
        {
            e.Cancel = true;
            closePending = true;
            SignalCancelRequest();
        }
    }

    bool IsTerminalPhase() =>
        Phase is RadialConversionPhase.CompletedSuccess
            or RadialConversionPhase.CompletedError
            or RadialConversionPhase.Cancelled
            or RadialConversionPhase.Initial;

    void TryCloseIfPending()
    {
        if (!closePending || Phase == RadialConversionPhase.Converting) return;
        closePending = false;
        Close();
    }
}