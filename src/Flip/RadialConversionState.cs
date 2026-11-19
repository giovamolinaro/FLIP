namespace Flip;

internal enum RadialConversionPhase
{
    Initial,
    Converting,
    CompletedSuccess,
    CompletedError,
    Cancelled
}

/// <summary>Pure conversion-indicator state for deterministic tests and UI binding.</summary>
internal sealed class RadialConversionState
{
    public RadialConversionPhase Phase { get; private set; } = RadialConversionPhase.Initial;
    public bool HasRealPercent { get; private set; }
    public double DisplayedPercent { get; private set; }
    public int? TargetPercent { get; private set; }
    public string SourceLabel { get; private set; } = "";
    public string TargetFormat { get; private set; } = "";
    public string? Step { get; private set; }
    public double IndeterminateSweepDegrees { get; private set; }

    public bool IsIndeterminate => Phase == RadialConversionPhase.Converting && !HasRealPercent;
    public bool ShowNumericPercent => Phase == RadialConversionPhase.Converting && HasRealPercent;

    public int ShownPercent
    {
        get
        {
            if (!HasRealPercent) return 0;
            int max = Phase == RadialConversionPhase.CompletedSuccess ? 100 : 99;
            return Math.Clamp((int)Math.Round(DisplayedPercent), 1, max);
        }
    }

    public void Reset()
    {
        Phase = RadialConversionPhase.Initial;
        HasRealPercent = false;
        DisplayedPercent = 0;
        TargetPercent = null;
        SourceLabel = "";
        TargetFormat = "";
        Step = null;
        IndeterminateSweepDegrees = 0;
    }

    public void BeginConversion(IReadOnlyList<string> sources, string targetFormat)
    {
        if (string.IsNullOrWhiteSpace(targetFormat)) throw new ArgumentException("Target format required.", nameof(targetFormat));
        sources ??= [];
        Phase = RadialConversionPhase.Converting;
        HasRealPercent = false;
        DisplayedPercent = 0;
        TargetPercent = null;
        Step = null;
        IndeterminateSweepDegrees = 0;
        TargetFormat = targetFormat.Trim().ToUpperInvariant();
        SourceLabel = FormatSourceLabel(sources);
    }

    public void ReportProgress(int? percent, string? step)
    {
        if (Phase != RadialConversionPhase.Converting) return;
        if (step != null) Step = step;
        if (!percent.HasValue) return;

        HasRealPercent = true;
        int clamped = Math.Clamp(percent.Value, 0, 99);
        int floor = Math.Max((int)Math.Floor(DisplayedPercent), TargetPercent ?? 0);
        int next = Math.Max(clamped, floor);
        TargetPercent = next;
        if (DisplayedPercent < next)
            DisplayedPercent = Math.Max(DisplayedPercent, next - 8);
    }

    public void AdvanceSmoothing()
    {
        if (Phase != RadialConversionPhase.Converting || !HasRealPercent || !TargetPercent.HasValue) return;
        double target = TargetPercent.Value;
        if (DisplayedPercent < target)
            DisplayedPercent = Math.Min(target, DisplayedPercent + Math.Max(0.8, (target - DisplayedPercent) * 0.28));
    }

    public void AdvanceIndeterminate(double degreesPerTick = 4.5)
    {
        if (!IsIndeterminate) return;
        IndeterminateSweepDegrees = (IndeterminateSweepDegrees + degreesPerTick) % 360;
    }

    public void Complete(bool success)
    {
        if (Phase != RadialConversionPhase.Converting) return;
        Phase = success ? RadialConversionPhase.CompletedSuccess : RadialConversionPhase.CompletedError;
        if (success)
        {
            HasRealPercent = true;
            TargetPercent = 100;
            DisplayedPercent = 100;
        }
    }

    public void Cancel()
    {
        if (Phase != RadialConversionPhase.Converting) return;
        Phase = RadialConversionPhase.Cancelled;
    }

    internal static string FormatSourceLabel(IReadOnlyList<string> sources)
    {
        if (sources.Count == 0) return "";
        if (sources.Count == 1)
        {
            string name = System.IO.Path.GetFileName(sources[0]);
            return name.Length > 20 ? name[..19] + "…" : name;
        }
        return $"{sources.Count} file";
    }
}