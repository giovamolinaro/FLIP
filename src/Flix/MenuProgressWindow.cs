using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Flix;

internal sealed class MenuProgressWindow : Window
{
    internal const double CardWidth = 440;
    internal const double CardHeight = 210;
    const double ShadowPad = 40;
    internal const double BarHeight = 4;
    const double DemoFraction = 0.40;
    internal const string PreviewFileName = "foto.png";
    internal const string PreviewFormatName = "JPG";
    internal const string RunningStatus = "Conversione in corso...";
    internal const string PartialPercentText = "40%";
    internal const string SuccessStatus = "Conversione completata";
    internal const string OpenFolderText = "Apri cartella";
    internal const string PreviewOpenTip = "Anteprima: nessuna cartella viene aperta.";
    internal const string ErrorStatus = "Conversione non riuscita.";
    internal const string ErrorDetail = "Il file originale non è stato modificato.";
    internal const string CloseText = "Chiudi";
    internal const string CancelledStatus = "Conversione annullata";
    internal const string CancelText = "Annulla";
    internal const string BusyDetail = "Nessuna nuova conversione è stata avviata.";

    static readonly FontFamily FontFace = new("Segoe UI");
    static readonly SolidColorBrush Green = Freeze(0x3D, 0xDC, 0x97);
    static readonly SolidColorBrush GreenInk = Freeze(0x10, 0x21, 0x17);
    static readonly SolidColorBrush TitleBrush = Freeze(0xF4, 0xF7, 0xF5);
    static readonly SolidColorBrush FileBrush = Freeze(0xE8, 0xED, 0xF2);
    static readonly SolidColorBrush Muted = Freeze(0x8B, 0x94, 0x9E);
    static readonly SolidColorBrush Body = Freeze(0xD7, 0xDE, 0xE6);
    static readonly SolidColorBrush Neutral = Freeze(0xC6, 0xCE, 0xD6);
    static readonly SolidColorBrush Red = Freeze(0xF0, 0x71, 0x78);
    static readonly SolidColorBrush RedSoft = Freeze(0xF0, 0xC2, 0xC6);
    static readonly SolidColorBrush GhostText = Freeze(0xE7, 0xED, 0xF2);
    static readonly SolidColorBrush GhostFill = Freeze(0xFF, 0xFF, 0xFF, 14);
    static readonly SolidColorBrush GhostBorder = Freeze(0xFF, 0xFF, 0xFF, 40);
    static readonly SolidColorBrush TrackBrush = Freeze(0xFF, 0xFF, 0xFF, 28);
    static readonly SolidColorBrush Hairline = Freeze(0xFF, 0xFF, 0xFF, 32);
    static readonly SolidColorBrush PillBrush = Freeze(0x3D, 0xDC, 0x97, 36);
    static readonly Brush CardBrush = CreateCardBrush();
    static readonly Brush VeilBrush = CreateVeilBrush();
    static readonly Brush SliderBrush = CreateSliderBrush();
    static readonly ControlTemplate ButtonTemplate = CreateButtonTemplate();

    readonly Border card;
    readonly Border veil;
    readonly TranslateTransform cardShift = new();
    readonly Grid track = new() { Height = BarHeight, SnapsToDevicePixels = true, ClipToBounds = true };
    readonly Border fill;
    readonly Border slider;
    readonly TranslateTransform sliderShift = new();
    readonly Grid mark = new() { Width = 36, Height = 36, Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed, RenderTransformOrigin = new Point(0.5, 0.5) };
    readonly Grid copy;
    readonly ScaleTransform markScale = new(1, 1);
    readonly Ellipse circle;
    readonly Polyline check;
    readonly Line stem;
    readonly Ellipse dot;
    readonly TextBlock status = new() { FontFamily = FontFace, FontSize = 13.5, Foreground = Body, TextWrapping = TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock detail = new() { FontFamily = FontFace, FontSize = 12, Foreground = RedSoft, Margin = new Thickness(0, 2, 0, 0), Visibility = Visibility.Collapsed, TextWrapping = TextWrapping.Wrap };
    readonly TextBlock percent = new() { FontFamily = FontFace, FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Green, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed };
    readonly Button button;
    readonly double circleUnits;
    readonly double checkUnits;
    readonly double stemUnits;
    Phase phase;
    TextBlock sourceName = null!;
    TextBlock formatName = null!;
    readonly bool previewMode;
    MenuProgressModel? live;
    MenuProgressModel.View applied;
    int? measuredPercent;
    DispatcherTimer? autoClose;
    int autoCloseWaits;
    bool rendered;
    bool shown;
    bool veiled;
    bool closed;
    bool loopPending;
    int veilGeneration;
    int fillGeneration;

    internal static int ShowPreview()
    {
        var application = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        application.Run(new MenuProgressWindow());
        return 0;
    }

    MenuProgressWindow(bool preview = true)
    {
        Title = "FLIX — Conversione";
        Width = CardWidth + ShadowPad * 2;
        Height = CardHeight + ShadowPad * 2;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = true;
        Topmost = false;
        UseLayoutRounding = true;
        FontFamily = FontFace;
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);

        var root = new Grid { Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)) };
        veil = new Border { Background = VeilBrush, Opacity = 0, Visibility = Visibility.Collapsed };
        root.Children.Add(veil);

        card = new Border
        {
            Width = CardWidth,
            Height = CardHeight,
            CornerRadius = new CornerRadius(18),
            Background = CardBrush,
            BorderBrush = Hairline,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransform = cardShift,
            Effect = new DropShadowEffect
            {
                BlurRadius = 22,
                ShadowDepth = 5,
                Direction = 270,
                Opacity = 0.40,
                Color = Colors.Black,
                RenderingBias = RenderingBias.Performance
            }
        };
        root.Children.Add(card);

        var body = new Grid { Margin = new Thickness(24, 0, 24, 0), VerticalAlignment = VerticalAlignment.Center };
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        card.Child = body;

        var title = new TextBlock
        {
            Text = "FLIX — Conversione",
            FontFamily = FontFace,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = TitleBrush,
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(title, 0);
        body.Children.Add(title);

        var meta = BuildMeta();
        Grid.SetRow(meta, 1);
        body.Children.Add(meta);

        var statusHost = new Grid { Height = 44, VerticalAlignment = VerticalAlignment.Center };
        statusHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusHost.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusHost.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mark.RenderTransform = markScale;
        mark.Children.Add(BuildMark(out circle, out check, out stem, out dot, out circleUnits, out checkUnits, out stemUnits));
        Grid.SetColumn(mark, 0);
        statusHost.Children.Add(mark);
        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(status);
        words.Children.Add(detail);
        Grid.SetColumn(words, 1);
        statusHost.Children.Add(words);
        Grid.SetColumn(percent, 2);
        statusHost.Children.Add(percent);
        Grid.SetRow(statusHost, 2);
        body.Children.Add(statusHost);
        copy = statusHost;

        track.Margin = new Thickness(0);
        var bar = new Border
        {
            Height = BarHeight,
            Margin = new Thickness(0, 12, 0, 16),
            CornerRadius = new CornerRadius(2),
            Background = TrackBrush,
            SnapsToDevicePixels = true,
            ClipToBounds = true,
            Child = track
        };
        fill = new Border { Height = BarHeight, Width = 0, CornerRadius = new CornerRadius(2), Background = Green, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed };
        slider = new Border { Width = 112, Height = BarHeight, CornerRadius = new CornerRadius(2), Background = SliderBrush, HorizontalAlignment = HorizontalAlignment.Left, Visibility = Visibility.Collapsed, RenderTransform = sliderShift, IsHitTestVisible = false };
        track.Children.Add(fill);
        track.Children.Add(slider);
        Grid.SetRow(bar, 3);
        body.Children.Add(bar);

        button = new Button
        {
            Height = 32,
            MinWidth = 108,
            Padding = new Thickness(0),
            FontFamily = FontFace,
            FontSize = 12.5,
            Cursor = Cursors.Hand,
            FocusVisualStyle = null,
            Template = ButtonTemplate,
            HorizontalAlignment = HorizontalAlignment.Right,
            OverridesDefaultStyle = true
        };
        button.Click += (_, _) => OnAction();
        Grid.SetRow(button, 4);
        body.Children.Add(button);

        Content = root;
        PreviewKeyDown += OnPreviewKeyDown;
        track.SizeChanged += OnTrackSize;
        previewMode = preview;
        ContentRendered += (_, _) =>
        {
            rendered = true;
            PlayEntrance();
            if (previewMode) ApplyPhase(Phase.Indeterminate);
            else ApplyLive();
            Activate();
            Focus();
        };
    }

    internal void Refresh()
    {
        if (!rendered || closed || previewMode) return;
        ApplyLive();
    }

    Grid BuildMeta()
    {
        var meta = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        meta.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        meta.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        sourceName = new TextBlock
        {
            Text = PreviewFileName,
            FontFamily = FontFace,
            FontSize = 13.5,
            Foreground = FileBrush,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        meta.Children.Add(sourceName);
        var destination = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        destination.Children.Add(new TextBlock
        {
            Text = "Destinazione",
            FontFamily = FontFace,
            FontSize = 11.5,
            Foreground = Muted,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 8, 0)
        });
        destination.Children.Add(new Border
        {
            Background = PillBrush,
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 2, 8, 2),
            Child = formatName = new TextBlock
            {
                Text = PreviewFormatName,
                FontFamily = FontFace,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Green,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
        Grid.SetColumn(destination, 1);
        meta.Children.Add(destination);
        return meta;
    }

    static Canvas BuildMark(out Ellipse circle, out Polyline check, out Line stem, out Ellipse dot, out double circleUnits, out double checkUnits, out double stemUnits)
    {
        const double thickness = 2.25;
        circle = new Ellipse
        {
            Width = 29.2,
            Height = 29.2,
            StrokeThickness = thickness,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform(-90),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(circle, 3.4);
        Canvas.SetTop(circle, 3.4);
        circleUnits = ApplyDash(circle, 2 * Math.PI * 14.6 / thickness);
        var points = new PointCollection { new Point(0, 6.2), new Point(5.4, 11.4), new Point(15, 0) };
        points.Freeze();
        check = new Polyline { Points = points, StrokeThickness = thickness, StrokeLineJoin = PenLineJoin.Round, IsHitTestVisible = false };
        Canvas.SetLeft(check, 11);
        Canvas.SetTop(check, 12.6);
        checkUnits = ApplyDash(check, Length(points) / thickness);
        stem = new Line { X1 = 1.2, Y1 = 0, X2 = 1.2, Y2 = 7.8, StrokeThickness = thickness, IsHitTestVisible = false };
        Canvas.SetLeft(stem, 16.8);
        Canvas.SetTop(stem, 11.4);
        stemUnits = ApplyDash(stem, 7.8 / thickness);
        dot = new Ellipse { Width = 2.8, Height = 2.8, Fill = Red, Opacity = 0, IsHitTestVisible = false };
        Canvas.SetLeft(dot, 16.6);
        Canvas.SetTop(dot, 22.1);
        var canvas = new Canvas { Width = 36, Height = 36, IsHitTestVisible = false };
        canvas.Children.Add(circle);
        canvas.Children.Add(check);
        canvas.Children.Add(stem);
        canvas.Children.Add(dot);
        return canvas;
    }

    static double ApplyDash(Shape shape, double units)
    {
        double span = Math.Max(0.1, units * 1.02);
        shape.StrokeStartLineCap = PenLineCap.Round;
        shape.StrokeEndLineCap = PenLineCap.Round;
        var dashes = new DoubleCollection { span, span };
        dashes.Freeze();
        shape.StrokeDashArray = dashes;
        shape.StrokeDashOffset = span;
        shape.Fill = Brushes.Transparent;
        return span;
    }

    static double Length(PointCollection points)
    {
        double length = 0;
        for (int i = 1; i < points.Count; i++) length += Distance(points[i - 1], points[i]);
        return length;
    }

    void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!previewMode)
        {
            if (e.Key != Key.Escape) return;
            e.Handled = true;
            if (live != null && live.Phase is MenuProgressModel.View.Indeterminate or MenuProgressModel.View.Determinate) live.RequestCancel();
            else Close();
            return;
        }
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }
        if (e.Key == Key.B)
        {
            e.Handled = true;
            ToggleVeil();
            return;
        }
        Phase? next = e.Key switch
        {
            Key.D1 or Key.NumPad1 => Phase.Indeterminate,
            Key.D2 or Key.NumPad2 => Phase.Partial,
            Key.D3 or Key.NumPad3 => Phase.Success,
            Key.D4 or Key.NumPad4 => Phase.Error,
            Key.D5 or Key.NumPad5 => Phase.Cancelled,
            _ => null
        };
        if (next == null) return;
        e.Handled = true;
        ApplyPhase(next.Value);
    }

    void OnAction()
    {
        if (closed) return;
        if (!previewMode)
        {
            if (live != null && live.Phase is MenuProgressModel.View.Indeterminate or MenuProgressModel.View.Determinate)
            {
                live.RequestCancel();
                return;
            }
            if (live?.Phase == MenuProgressModel.View.Success)
            {
                OpenResultFolder();
                return;
            }
            Close();
            return;
        }
        if (phase == Phase.Success) return;
        if (phase is Phase.Indeterminate or Phase.Partial) ApplyPhase(Phase.Cancelled);
        else Close();
    }

    void ApplyPhase(Phase value)
    {
        if (closed) return;
        phase = value;
        StopLoop();
        StopMark();
        detail.Visibility = Visibility.Collapsed;
        percent.Visibility = Visibility.Collapsed;
        track.Background = TrackBrush;
        switch (value)
        {
            case Phase.Indeterminate:
                status.Text = RunningStatus;
                status.Foreground = Body;
                Relabel(CancelText, false, null);
                HideMark();
                ShowIndeterminate();
                break;
            case Phase.Partial:
                status.Text = RunningStatus;
                status.Foreground = Body;
                percent.Text = PartialPercentText;
                percent.Visibility = Visibility.Visible;
                Relabel(CancelText, false, null);
                HideMark();
                AnimateFill(DemoFraction, 460);
                break;
            case Phase.Success:
                status.Text = SuccessStatus;
                status.Foreground = Green;
                Relabel(OpenFolderText, true, PreviewOpenTip);
                AnimateFill(1, 360, fill.Visibility == Visibility.Visible ? fill.ActualWidth / Math.Max(track.ActualWidth, 1) : 0);
                PlayMark(true);
                break;
            case Phase.Error:
                status.Text = ErrorStatus;
                status.Foreground = Red;
                detail.Text = ErrorDetail;
                detail.Visibility = Visibility.Visible;
                Relabel(CloseText, false, null);
                ClearFill();
                PlayMark(false);
                break;
            default:
                status.Text = CancelledStatus;
                status.Foreground = Neutral;
                Relabel(CloseText, false, null);
                HideMark();
                ShrinkFill();
                break;
        }
        if (shown) Fade(copy);
        shown = true;
    }

    void ShowIndeterminate()
    {
        fill.BeginAnimation(WidthProperty, null);
        fill.Width = 0;
        fill.Visibility = Visibility.Collapsed;
        slider.Visibility = Visibility.Visible;
        if (track.ActualWidth < 8)
        {
            loopPending = true;
            return;
        }
        StartLoop();
    }

    void StartLoop()
    {
        loopPending = false;
        double width = track.ActualWidth;
        if (width < 8 || phase != Phase.Indeterminate) return;
        var animation = new DoubleAnimation(-slider.Width, width, TimeSpan.FromMilliseconds(980))
        {
            AutoReverse = true,
            RepeatBehavior = RepeatBehavior.Forever,
            EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
        };
        sliderShift.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    void StopLoop()
    {
        loopPending = false;
        sliderShift.BeginAnimation(TranslateTransform.XProperty, null);
        sliderShift.X = 0;
        slider.Visibility = Visibility.Collapsed;
    }

    void AnimateFill(double fraction, int milliseconds, double? fromFraction = null)
    {
        fillGeneration++;
        StopLoop();
        double span = track.ActualWidth;
        fill.Background = Green;
        fill.Visibility = Visibility.Visible;
        if (span < 8)
        {
            fill.BeginAnimation(WidthProperty, null);
            fill.Width = 0;
            return;
        }
        double from = Math.Clamp((fromFraction ?? 0) * span, 0, span);
        double to = Math.Clamp(fraction * span, 0, span);
        fill.BeginAnimation(WidthProperty, null);
        fill.Width = from;
        fill.BeginAnimation(WidthProperty, new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        });
    }

    void ClearFill()
    {
        fillGeneration++;
        StopLoop();
        fill.BeginAnimation(WidthProperty, null);
        fill.Width = 0;
        fill.Visibility = Visibility.Collapsed;
    }

    void ShrinkFill()
    {
        if (fill.Visibility != Visibility.Visible)
        {
            ClearFill();
            return;
        }
        int generation = ++fillGeneration;
        double from = fill.ActualWidth;
        var animation = new DoubleAnimation(from, 0, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.HoldEnd
        };
        animation.Completed += (_, _) =>
        {
            if (closed || generation != fillGeneration || phase != Phase.Cancelled) return;
            double current = fill.ActualWidth;
            fill.BeginAnimation(WidthProperty, null);
            fill.Width = current;
            fill.Width = 0;
            fill.Visibility = Visibility.Collapsed;
        };
        fill.BeginAnimation(WidthProperty, animation);
    }

    void HideMark()
    {
        StopMark();
        mark.Visibility = Visibility.Collapsed;
    }

    void PlayMark(bool success)
    {
        StopMark();
        mark.Visibility = Visibility.Visible;
        mark.Opacity = 0;
        markScale.ScaleX = 0.94;
        markScale.ScaleY = 0.94;
        circle.Stroke = success ? Green : Red;
        check.Visibility = success ? Visibility.Visible : Visibility.Collapsed;
        stem.Visibility = success ? Visibility.Collapsed : Visibility.Visible;
        dot.Visibility = success ? Visibility.Collapsed : Visibility.Visible;
        dot.Opacity = 0;
        check.Stroke = Green;
        stem.Stroke = Red;
        ResetDash(circle, circleUnits);
        ResetDash(check, checkUnits);
        ResetDash(stem, stemUnits);
        DrawDash(circle, circleUnits, success ? 420 : 260, success ? 30 : 20);
        if (success) DrawDash(check, checkUnits, 340, 250);
        else
        {
            DrawDash(stem, stemUnits, 220, 160);
            dot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
            {
                BeginTime = TimeSpan.FromMilliseconds(220),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });
        }
        mark.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)) { FillBehavior = FillBehavior.HoldEnd });
        var scale = new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(success ? 320 : 220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        };
        markScale.BeginAnimation(ScaleTransform.ScaleXProperty, scale);
        markScale.BeginAnimation(ScaleTransform.ScaleYProperty, scale.Clone());
    }

    static void ResetDash(Shape shape, double units)
    {
        shape.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        shape.StrokeDashOffset = units;
    }

    static void DrawDash(Shape shape, double units, int durationMs, int delayMs)
    {
        shape.BeginAnimation(Shape.StrokeDashOffsetProperty, new DoubleAnimation(units, 0, TimeSpan.FromMilliseconds(durationMs))
        {
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut },
            FillBehavior = FillBehavior.HoldEnd
        });
    }

    void StopMark()
    {
        circle.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        check.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        stem.BeginAnimation(Shape.StrokeDashOffsetProperty, null);
        dot.BeginAnimation(OpacityProperty, null);
        mark.BeginAnimation(OpacityProperty, null);
        markScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        markScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        mark.Opacity = 1;
        markScale.ScaleX = 1;
        markScale.ScaleY = 1;
    }

    void Relabel(string text, bool primary, string? tip)
    {
        button.Content = text;
        button.ToolTip = tip;
        button.Background = primary ? Green : GhostFill;
        button.BorderBrush = primary ? Green : GhostBorder;
        button.BorderThickness = new Thickness(1);
        button.Foreground = primary ? GreenInk : GhostText;
        button.FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal;
        if (!shown) return;
        button.BeginAnimation(OpacityProperty, null);
        button.Opacity = 1;
        button.BeginAnimation(OpacityProperty, new DoubleAnimation(0.4, 1, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }

    static void Fade(UIElement element)
    {
        element.BeginAnimation(OpacityProperty, null);
        element.Opacity = 1;
        element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }

    void PlayEntrance()
    {
        card.Opacity = 0;
        cardShift.Y = 8;
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        });
        cardShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(280))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.HoldEnd
        });
    }

    void ToggleVeil()
    {
        if (closed) return;
        veiled = !veiled;
        int generation = ++veilGeneration;
        veil.BeginAnimation(OpacityProperty, null);
        if (veiled)
        {
            PlaceVeil();
            veil.Opacity = 0;
            veil.Visibility = Visibility.Visible;
            veil.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.HoldEnd
            });
            Activate();
            return;
        }
        double current = veil.Opacity;
        veil.Opacity = current;
        var hide = new DoubleAnimation(current, 0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn },
            FillBehavior = FillBehavior.Stop
        };
        hide.Completed += (_, _) =>
        {
            if (closed || generation != veilGeneration || veiled) return;
            veil.BeginAnimation(OpacityProperty, null);
            veil.Opacity = 0;
            veil.Visibility = Visibility.Collapsed;
            PlaceCard();
            Activate();
        };
        veil.BeginAnimation(OpacityProperty, hide);
    }

    void PlaceVeil()
    {
        double left = SystemParameters.VirtualScreenLeft;
        double top = SystemParameters.VirtualScreenTop;
        double width = SystemParameters.VirtualScreenWidth;
        double height = SystemParameters.VirtualScreenHeight;
        if (width < 200 || height < 200)
        {
            var area = SystemParameters.WorkArea;
            left = area.Left;
            top = area.Top;
            width = area.Width;
            height = area.Height;
        }
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }

    void PlaceCard()
    {
        double width = CardWidth + ShadowPad * 2;
        double height = CardHeight + ShadowPad * 2;
        var area = SystemParameters.WorkArea;
        Width = width;
        Height = height;
        Left = area.Left + Math.Max(0, (area.Width - width) / 2);
        Top = area.Top + Math.Max(0, (area.Height - height) / 2);
    }

    void OnTrackSize(object sender, SizeChangedEventArgs e)
    {
        if (closed) return;
        if (phase == Phase.Indeterminate && loopPending)
        {
            if (e.NewSize.Width < 8) return;
            StartLoop();
            return;
        }
        if (!previewMode && phase == Phase.Partial && measuredPercent is int percent && e.NewSize.Width >= 8 && fill.Width < 1)
            AnimateFill(percent / 100.0, 460, 0);
    }

    protected override void OnClosed(EventArgs e)
    {
        closed = true;
        autoClose?.Stop();
        if (!previewMode && live != null && live.Phase is MenuProgressModel.View.Indeterminate or MenuProgressModel.View.Determinate)
            live.RequestCancel();
        veilGeneration++;
        StopLoop();
        StopMark();
        fill.BeginAnimation(WidthProperty, null);
        veil.BeginAnimation(OpacityProperty, null);
        card.BeginAnimation(OpacityProperty, null);
        cardShift.BeginAnimation(TranslateTransform.YProperty, null);
        button.BeginAnimation(OpacityProperty, null);
        status.BeginAnimation(OpacityProperty, null);
        track.SizeChanged -= OnTrackSize;
        PreviewKeyDown -= OnPreviewKeyDown;
        base.OnClosed(e);
    }

    static double Distance(Point a, Point b)
    {
        double x = a.X - b.X;
        double y = a.Y - b.Y;
        return Math.Sqrt(x * x + y * y);
    }

    static SolidColorBrush Freeze(byte r, byte g, byte b, byte a = 255)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }

    static Brush CreateCardBrush()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x2C, 0x32, 0x3A), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromRgb(0x1E, 0x23, 0x29), 1));
        brush.Freeze();
        return brush;
    }

    static Brush CreateVeilBrush()
    {
        // Velo scuro semitrasparente, con caduta radiale per valutare lo spotlight. Non è un blur.
        var brush = new RadialGradientBrush { GradientOrigin = new Point(0.5, 0.5), Center = new Point(0.5, 0.5), RadiusX = 0.62, RadiusY = 0.75 };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(136, 6, 8, 12), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(170, 5, 7, 10), 0.45));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(198, 4, 6, 9), 1));
        brush.Freeze();
        return brush;
    }

    static Brush CreateSliderBrush()
    {
        var brush = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0x3D, 0xDC, 0x97), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0x3D, 0xDC, 0x97), 0.38));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0x3D, 0xDC, 0x97), 0.62));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0x3D, 0xDC, 0x97), 1));
        brush.Freeze();
        return brush;
    }

    static ControlTemplate CreateButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.Name = "bd";
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        border.SetValue(Border.PaddingProperty, new Thickness(16, 0, 16, 0));
        border.SetBinding(Border.BackgroundProperty, new Binding(nameof(Background)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderBrushProperty, new Binding(nameof(BorderBrush)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        border.SetBinding(Border.BorderThicknessProperty, new Binding(nameof(BorderThickness)) { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) });
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        presenter.SetValue(ContentPresenter.RecognizesAccessKeyProperty, false);
        border.AppendChild(presenter);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = border };
        template.Triggers.Add(new Trigger { Property = UIElement.IsMouseOverProperty, Value = true });
        ((Trigger)template.Triggers[0]).Setters.Add(new Setter(UIElement.OpacityProperty, 0.88, "bd"));
        var pressed = new Trigger { Property = ButtonBase.IsPressedProperty, Value = true };
        pressed.Setters.Add(new Setter(UIElement.OpacityProperty, 0.74, "bd"));
        template.Triggers.Add(pressed);
        return template;
    }

    enum Phase { Indeterminate, Partial, Success, Error, Cancelled }

    internal static MenuProgressWindow ShowLive(MenuProgressModel model)
    {
        var window = new MenuProgressWindow(false);
        window.live = model;
        window.sourceName.Text = string.IsNullOrWhiteSpace(model.FileName) ? "—" : model.FileName;
        window.formatName.Text = string.IsNullOrWhiteSpace(model.Format) ? "—" : model.Format;
        window.Show();
        return window;
    }

    void ApplyLive()
    {
        if (live == null || closed) return;
        var view = live.Phase;
        if (view == MenuProgressModel.View.Determinate)
        {
            if (live.Percent is not int percent || percent is < 1 or > 99) return;
            if (applied == view && measuredPercent == percent) return;
            measuredPercent = percent;
            applied = view;
            ShowMeasured(percent);
            return;
        }
        if (applied == view) return;
        applied = view;
        switch (view)
        {
            case MenuProgressModel.View.Success:
                ApplyPhase(Phase.Success);
                if (!string.IsNullOrWhiteSpace(live.ResultName)) sourceName.Text = live.ResultName;
                percent.Text = "100%";
                percent.Visibility = Visibility.Visible;
                button.ToolTip = null;
                ArmAutoClose();
                break;
            case MenuProgressModel.View.Error:
                ApplyPhase(Phase.Error);
                status.Text = string.IsNullOrWhiteSpace(live.Status) ? ErrorStatus : live.Status;
                detail.Text = string.IsNullOrWhiteSpace(live.Detail) ? ErrorDetail : live.Detail;
                detail.Visibility = Visibility.Visible;
                break;
            case MenuProgressModel.View.Busy:
                ApplyPhase(Phase.Error);
                status.Text = string.IsNullOrWhiteSpace(live.Status) ? MenuConversion.BusyMessage : live.Status;
                detail.Text = string.IsNullOrWhiteSpace(live.Detail) ? BusyDetail : live.Detail;
                detail.Visibility = Visibility.Visible;
                percent.Visibility = Visibility.Collapsed;
                break;
            case MenuProgressModel.View.Cancelled:
                ApplyPhase(Phase.Cancelled);
                break;
            default:
                ApplyPhase(Phase.Indeterminate);
                break;
        }
    }

    void ShowMeasured(int value)
    {
        if (closed || phase is Phase.Success or Phase.Error or Phase.Cancelled) return;
        int shown = Math.Clamp(value, 1, 99);
        if (phase != Phase.Partial)
        {
            phase = Phase.Partial;
            status.Text = RunningStatus;
            status.Foreground = Body;
            detail.Visibility = Visibility.Collapsed;
            Relabel(CancelText, false, null);
            HideMark();
            Fade(copy);
        }
        percent.Text = shown + "%";
        percent.Visibility = Visibility.Visible;
        double from = fill.Visibility == Visibility.Visible && track.ActualWidth > 1 ? fill.ActualWidth / track.ActualWidth : 0;
        AnimateFill(shown / 100.0, 460, from);
    }

    void OpenResultFolder()
    {
        if (string.IsNullOrWhiteSpace(live?.Folder)) return;
        OpenFolder(live.Folder);
    }

    static void OpenFolder(string folder)
    {
        Evidence.Log("folder.open.request", folder);
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        start.ArgumentList.Add(folder);
        Process.Start(start);
    }

    void ArmAutoClose()
    {
        if (autoClose != null) return;
        autoClose = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3200) };
        autoClose.Tick += (_, _) =>
        {
            if (closed || live?.Phase != MenuProgressModel.View.Success)
            {
                autoClose.Stop();
                return;
            }
            if (IsMouseOver && autoCloseWaits < 3)
            {
                autoCloseWaits++;
                autoClose.Interval = TimeSpan.FromMilliseconds(900);
                return;
            }
            autoClose.Stop();
            Close();
        };
        autoClose.Start();
    }
}

internal static class MenuProgressRouter
{
    internal enum Kind { Ignored, Busy, Started, Validation }
    internal const bool UsesBalloon = false;

    internal static Kind Classify(string? message, bool sessionOpen, out string format)
    {
        format = "";
        if (string.IsNullOrWhiteSpace(message)) return sessionOpen ? Kind.Ignored : Kind.Validation;
        if (message == MenuConversion.BusyMessage) return Kind.Busy;
        if (TryStarted(message, out format)) return sessionOpen ? Kind.Ignored : Kind.Started;
        return sessionOpen ? Kind.Ignored : Kind.Validation;
    }

    internal static bool TryStarted(string message, out string format)
    {
        format = "";
        const string prefix = "Conversione in ";
        if (!message.StartsWith(prefix, StringComparison.Ordinal) || message.Length < prefix.Length + 2 || message[^1] != '.') return false;
        string token = message[prefix.Length..^1];
        if (token.Length is < 1 or > 8 || token.Any(ch => !char.IsAsciiLetterOrDigit(ch))) return false;
        format = token;
        return true;
    }

    internal static string DisplayFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format)) return "";
        return format.All(char.IsAsciiLetterOrDigit) ? format.ToUpperInvariant() : format;
    }
}

internal sealed class MenuProgressModel
{
    internal enum View { Idle, Indeterminate, Determinate, Success, Error, Cancelled, Busy }

    readonly CancellationTokenSource? job;
    bool measured;
    internal View Phase { get; private set; } = View.Idle;
    internal string FileName { get; private set; } = "";
    internal string Format { get; private set; } = "";
    internal string Status { get; private set; } = "";
    internal string? Detail { get; private set; }
    internal int? Percent { get; private set; }
    internal bool ShowCheck { get; private set; }
    internal bool CancelRequested { get; private set; }
    internal string? Folder { get; private set; }
    internal string? ResultName { get; private set; }
    internal bool OpensWheel => false;

    MenuProgressModel(string fileName, string format, CancellationTokenSource? job)
    {
        FileName = fileName ?? "";
        Format = format ?? "";
        this.job = job;
    }

    internal static MenuProgressModel Start(string fileName, string format, CancellationTokenSource? job)
    {
        return new MenuProgressModel(fileName, format, job)
        {
            Phase = View.Indeterminate,
            Status = MenuProgressWindow.RunningStatus
        };
    }

    internal static MenuProgressModel Reject(string fileName, string format, string? message, bool busy)
    {
        return new MenuProgressModel(fileName, format, null)
        {
            Phase = busy ? View.Busy : View.Error,
            Status = busy ? MenuConversion.BusyMessage : MenuProgressWindow.ErrorStatus,
            Detail = busy ? MenuProgressWindow.BusyDetail : Clean(message)
        };
    }

    internal static MenuProgressModel FromOutcome(MenuConversion.Outcome outcome)
    {
        var model = new MenuProgressModel("", "", null);
        model.Complete(outcome);
        return model;
    }

    internal bool Report(int percent)
    {
        if (Phase is not (View.Indeterminate or View.Determinate)) return false;
        if (percent is < 1 or > 99) return false;
        // Per un solo file il motore emette 2 all'avvio, 25 sulle immagini e 35 sui dati senza misurare il lavoro.
        // Il 100 del contatore arriva prima che Outcome confermi il successo: non va mai mostrato qui.
        if (!measured)
        {
            if (percent is 2 or 25 or 35) return false;
            measured = true;
        }
        if (Percent is int prior && percent < prior) return false;
        if (Percent == percent && Phase == View.Determinate) return false;
        Percent = percent;
        Phase = View.Determinate;
        Status = MenuProgressWindow.RunningStatus;
        ShowCheck = false;
        Detail = null;
        return true;
    }

    internal void RequestCancel()
    {
        if (Phase is not (View.Indeterminate or View.Determinate)) return;
        if (CancelRequested) return;
        CancelRequested = true;
        try { job?.Cancel(); }
        catch (ObjectDisposedException) { }
    }

    internal void Complete(MenuConversion.Outcome outcome)
    {
        if (Phase is View.Success or View.Error or View.Cancelled or View.Busy) return;
        ShowCheck = false;
        Percent = null;
        Folder = null;
        ResultName = null;
        Detail = null;
        switch (outcome.Status)
        {
            case MenuConversion.Status.Success:
                Phase = View.Success;
                Status = MenuProgressWindow.SuccessStatus;
                Percent = 100;
                ShowCheck = true;
                Folder = outcome.Folder;
                ResultName = FileNameOf(outcome.Output);
                break;
            case MenuConversion.Status.Cancelled:
                Phase = View.Cancelled;
                Status = MenuProgressWindow.CancelledStatus;
                break;
            case MenuConversion.Status.Busy:
                Phase = View.Busy;
                Status = string.IsNullOrWhiteSpace(outcome.Message) ? MenuConversion.BusyMessage : Clean(outcome.Message);
                Detail = MenuProgressWindow.BusyDetail;
                break;
            default:
                Phase = View.Error;
                Status = MenuProgressWindow.ErrorStatus;
                Detail = Clean(outcome.Message);
                break;
        }
    }

    static string Clean(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return MenuProgressWindow.ErrorDetail;
        return message.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    static string? FileNameOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try { return System.IO.Path.GetFileName(path); }
        catch (ArgumentException) { return null; }
    }
}
