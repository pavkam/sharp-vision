// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Frames one owned content control with a titled border.</summary>
public sealed class GroupBox: ContentControl
{
    /// <summary>Initializes an empty group box.</summary>
    public GroupBox()
    {
        BorderThickness = new Thickness(1);
        BorderGlyphs = Glyphs.Rounded;
    }

    /// <summary>Gets or sets the non-null title written into the top edge.</summary>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);

            if (Terminal.Unicode.Width.Measure(value).Controls > 0)
            {
                throw new ArgumentException("A group header cannot contain terminal controls.", nameof(value));
            }

            _ = SetProperty(ref field, value, ChangeImpact.Measure);
        }
    } = string.Empty;

    /// <summary>Gets or sets the local glyph family used for the frame.</summary>
    public Glyphs Glyphs { get => BorderGlyphs; set => BorderGlyphs = value; }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var child = Content;
        var hw = Header.Length == 0 ? 0 : (int) Math.Min(int.MaxValue, 2L + Terminal.Unicode.Width.Measure(Header).Cells);
        if (child is null) { return new Size(hw, 0); }
        var desired = MeasureChild(child, constraint);
        var contentWidth = child.Visibility == Visibility.Collapsed
            ? 0
            : (int) Math.Min(int.MaxValue, (long) desired.Width + child.Margin.Horizontal);
        var contentHeight = child.Visibility == Visibility.Collapsed
            ? 0
            : (int) Math.Min(int.MaxValue, (long) desired.Height + child.Margin.Vertical);
        return new Size(Math.Max(contentWidth, hw), contentHeight);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) { if (Content is { } c) { ArrangeChild(c, bounds, ResolvedAxes.Both); } }

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => new()
    {
        SkipBodyFill = true,
        SkipBorder = true,
    };

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0) { return; }
        var opaque = ControlAppearance.HasOpaqueFill(this, GetAppearanceState());
        if (opaque) { canvas.Clear(Bounds, ResolvedStyle); }
        var border = ControlAppearance.ResolveBorderStyle(this, GetAppearanceState());
        var bg = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        ControlChrome.DrawUniformBorder(canvas, Bounds, Glyphs, border, bg);
        if (!string.IsNullOrEmpty(Header) && Bounds.Width > 3) { _ = canvas.Clip(new Rect(Bounds.X + 1, Bounds.Y, Bounds.Width - 2, 1)).Draw($" {Header} ".AsSpan(), new Point(Bounds.X + 1, Bounds.Y), border, background: bg); }
    }
}
