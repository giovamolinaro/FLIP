using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Flip;

internal sealed class RadialConversionView : UserControl
{
    readonly RadialConversionState state = new();
    readonly Canvas canvas = new() { Width = 420, Height = 420 };
    readonly TextBlock center = new()
    {
        Width = 172,
        MaxHeight = 120,
        TextTrimming = TextTrimming.CharacterEllipsis,
        TextAlignment = TextAlignment.Center,
        Foreground = Brushes.White,
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        IsHitTestVisible = false
    };
    Ellipse? trackRing;
    Line? progressRay;
    System.Windows.Shapes.Path? progressArc;
    Ellipse? progressTip;
    System.Windows.Shapes.Path? indeterminateArc;
    DispatcherTimer? animationTimer;

    public event EventHandler? CancelRequested;

    public RadialConversionPhase Phase => state.Phase;

    public RadialConversionView()
    {
        Focusable = true;
        Width = 320;
        Height = 320;
        Content = new Viewbox { Child = canvas, Stretch = Stretch.Uniform };
        PreviewKeyDown += OnPreviewKeyDown;
        Unloaded += (_, _) => StopAnimationTimer();
        PaintShell();
    }

    public void Reset()
    {
        RunOnUi(() =>
        {
            StopAnimationTimer();
            state.Reset();
            PaintShell();
        });
    }

    public void BeginConversion(IReadOnlyList<string> sources, string targetFormat)
    {
        RunOnUi(() =>
        {
            StopAnimationTimer();
            state.BeginConversion(sources, targetFormat);
            BuildConvertingChrome();
            StartAnimationTimer();
            UpdateVisual();
        });
    }

    public void ReportProgress(int? percent, string? step)
    {
        RunOnUi(() =>
        {
            bool wasIndeterminate = state.IsIndeterminate;
            state.ReportProgress(percent, step);
            if (wasIndeterminate && state.HasRealPercent)
                SwitchToDeterminateProgress();
            UpdateVisual();
        });
    }

    public void Complete(bool success)
    {
        RunOnUi(() =>
        {
            if (state.Phase == RadialConversionPhase.Converting && !state.HasRealPercent)
                SwitchToDeterminateProgress();
            state.Complete(success);
            StopAnimationTimer();
            ApplyCompletionColors(success);
            UpdateVisual();
        });
    }

    public void Cancel()
    {
        RunOnUi(() =>
        {
            state.Cancel();
            StopAnimationTimer();
            UpdateVisual();
        });
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && state.Phase == RadialConversionPhase.Converting)
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    void RunOnUi(Action action)
    {
        if (Dispatcher.CheckAccess()) action();
        else Dispatcher.BeginInvoke(action);
    }

    void StartAnimationTimer()
    {
        StopAnimationTimer();
        animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        animationTimer.Tick += OnAnimationTick;
        animationTimer.Start();
    }

    void StopAnimationTimer()
    {
        if (animationTimer == null) return;
        animationTimer.Stop();
        animationTimer.Tick -= OnAnimationTick;
        animationTimer = null;
    }

    void OnAnimationTick(object? sender, EventArgs e)
    {
        if (state.Phase != RadialConversionPhase.Converting) { StopAnimationTimer(); return; }
        if (state.IsIndeterminate)
            state.AdvanceIndeterminate();
        else
            state.AdvanceSmoothing();
        UpdateVisual();
    }

    void PaintShell()
    {
        canvas.Children.Clear();
        trackRing = null;
        progressRay = null;
        progressArc = null;
        progressTip = null;
        indeterminateArc = null;

        var disk = new Ellipse
        {
            Width = 414,
            Height = 414,
            Fill = new SolidColorBrush(Color.FromArgb(214, 24, 26, 28)),
            Stroke = new SolidColorBrush(Color.FromArgb(145, 128, 131, 124)),
            StrokeThickness = 1.4
        };
        Canvas.SetLeft(disk, 3);
        Canvas.SetTop(disk, 3);
        canvas.Children.Add(disk);

        center.Text = "Flip";
        Canvas.SetLeft(center, 124);
        Canvas.SetTop(center, 166);
        canvas.Children.Add(center);
    }

    void BuildConvertingChrome()
    {
        canvas.Children.Clear();

        var disk = new Ellipse
        {
            Width = 414,
            Height = 414,
            Fill = new SolidColorBrush(Color.FromArgb(214, 24, 26, 28)),
            Stroke = new SolidColorBrush(Color.FromArgb(145, 128, 131, 124)),
            StrokeThickness = 1.4
        };
        Canvas.SetLeft(disk, 3);
        Canvas.SetTop(disk, 3);
        canvas.Children.Add(disk);

        trackRing = new Ellipse
        {
            Width = 182,
            Height = 182,
            Stroke = new SolidColorBrush(Color.FromArgb(55, 255, 255, 255)),
            StrokeThickness = 5,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(trackRing, 119);
        Canvas.SetTop(trackRing, 119);
        canvas.Children.Add(trackRing);

        indeterminateArc = CreateIndeterminateArc();
        canvas.Children.Add(indeterminateArc);

        Canvas.SetLeft(center, 124);
        Canvas.SetTop(center, 152);
        canvas.Children.Add(center);
    }

    static System.Windows.Shapes.Path CreateProgressArc() => new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(255, 136, 24)),
        StrokeThickness = 5.5,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        IsHitTestVisible = false
    };

    static Line CreateProgressRay() => new()
    {
        Stroke = new SolidColorBrush(Color.FromArgb(170, 255, 165, 60)),
        StrokeThickness = 2.2,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        IsHitTestVisible = false
    };

    static Ellipse CreateProgressTip() => new()
    {
        Width = 9,
        Height = 9,
        Fill = new SolidColorBrush(Color.FromRgb(255, 210, 130)),
        IsHitTestVisible = false
    };

    System.Windows.Shapes.Path CreateIndeterminateArc() => new()
    {
        Stroke = new SolidColorBrush(Color.FromRgb(255, 136, 24)),
        StrokeThickness = 5.5,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
        IsHitTestVisible = false
    };

    void UpdateVisual()
    {
        switch (state.Phase)
        {
            case RadialConversionPhase.Initial:
                center.Text = "Flip";
                break;
            case RadialConversionPhase.Converting:
                UpdateConvertingVisual();
                break;
            case RadialConversionPhase.CompletedSuccess:
                UpdateCompletionVisual(success: true);
                break;
            case RadialConversionPhase.CompletedError:
                UpdateCompletionVisual(success: false);
                break;
            case RadialConversionPhase.Cancelled:
                center.Text = $"{state.SourceLabel}\n→ {state.TargetFormat}\nAnnullato\nEsc per chiudere";
                break;
        }
    }

    void UpdateConvertingVisual()
    {
        if (state.IsIndeterminate)
        {
            if (indeterminateArc != null)
                indeterminateArc.Data = IndeterminateArcData(state.IndeterminateSweepDegrees, 91);
            center.Text = $"{state.SourceLabel}\n→ {state.TargetFormat}\nConversione in corso…\nEsc per annullare";
            if (!string.IsNullOrWhiteSpace(state.Step))
                center.Text = $"{state.SourceLabel}\n→ {state.TargetFormat}\n{state.Step}\nEsc per annullare";
        }
        else
        {
            if (progressArc != null && progressRay != null && progressTip != null)
            {
                double pct = Math.Clamp(state.DisplayedPercent, 1, 99.9);
                progressArc.Data = Wheel.ProgressArcData(pct, 91);
                double rad = (pct * 3.6 - 90.0) * Math.PI / 180.0;
                double cos = Math.Cos(rad), sin = Math.Sin(rad);
                progressRay.X1 = 210 + 66 * cos;
                progressRay.Y1 = 210 + 66 * sin;
                progressRay.X2 = 210 + 91 * cos;
                progressRay.Y2 = 210 + 91 * sin;
                Canvas.SetLeft(progressTip, 210 + 91 * cos - 4.5);
                Canvas.SetTop(progressTip, 210 + 91 * sin - 4.5);
            }
            string stepLine = string.IsNullOrWhiteSpace(state.Step) ? "Conversione in corso…" : state.Step!;
            center.Text = $"{state.SourceLabel}\n→ {state.TargetFormat}  ({state.ShownPercent}%)\n{stepLine}\nEsc per annullare";
        }
    }

    void UpdateCompletionVisual(bool success)
    {
        if (progressArc != null)
            progressArc.Data = Wheel.ProgressArcData(99.9, 91);
        string outcome = success
            ? $"{state.SourceLabel}\n✓ {state.TargetFormat} (100%)\nCompletato!"
            : $"{state.SourceLabel}\n✕ {state.TargetFormat}\nErrore conversione";
        center.Text = outcome;
    }

    void SwitchToDeterminateProgress()
    {
        if (indeterminateArc != null)
        {
            canvas.Children.Remove(indeterminateArc);
            indeterminateArc = null;
        }
        progressArc ??= CreateProgressArc();
        progressRay ??= CreateProgressRay();
        progressTip ??= CreateProgressTip();
        if (!canvas.Children.Contains(progressArc)) canvas.Children.Add(progressArc);
        if (!canvas.Children.Contains(progressRay)) canvas.Children.Add(progressRay);
        if (!canvas.Children.Contains(progressTip)) canvas.Children.Add(progressTip);
    }

    void ApplyCompletionColors(bool success)
    {
        var color = success ? Color.FromRgb(46, 204, 113) : Color.FromRgb(231, 76, 60);
        var brush = new SolidColorBrush(color);
        if (progressArc != null) progressArc.Stroke = brush;
        if (progressRay != null) progressRay.Stroke = brush;
        if (progressTip != null) progressTip.Fill = brush;
        if (indeterminateArc != null)
        {
            indeterminateArc.Stroke = brush;
            indeterminateArc.Data = Wheel.ProgressArcData(99.9, 91);
        }
    }

    internal static Geometry IndeterminateArcData(double startDegrees, double radius = 91, double sweepDegrees = 72)
    {
        double start = startDegrees - 90;
        double end = start + sweepDegrees;
        Point At(double deg)
        {
            double rad = deg * Math.PI / 180.0;
            return new Point(210 + radius * Math.Cos(rad), 210 + radius * Math.Sin(rad));
        }
        var fig = new PathFigure { StartPoint = At(start), IsClosed = false };
        fig.Segments.Add(new ArcSegment(At(end), new Size(radius, radius), 0, sweepDegrees > 180, SweepDirection.Clockwise, true));
        return new PathGeometry([fig]);
    }
}