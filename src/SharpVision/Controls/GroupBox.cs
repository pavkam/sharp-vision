// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Frames one owned content control with a titled border.</summary>
public sealed class GroupBox: ContentControl
{
    /// <summary>Initializes an empty group box.</summary>
    public GroupBox() { }

    /// <summary>Gets or sets the non-null title written into the top edge.</summary>
    public string Header { get; set { ArgumentNullException.ThrowIfNull(value); _ = SetProperty(ref field, value, ChangeImpact.Measure); } } = string.Empty;

    /// <summary>Gets or sets the glyph family used for the frame.</summary>
    public Glyphs Glyphs { get; set => _ = SetProperty(ref field, value, ChangeImpact.Render); } = Glyphs.Rounded;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var child = Content;
        var hw = Header.Length == 0 ? 0 : (int) Math.Min(int.MaxValue, 2L + Terminal.Unicode.Width.Measure(Header).Cells);
        if (child is null) { return new Size(Math.Max(2, hw + 2), 2); }
        var d = MeasureChild(child, new Constraint(constraint.Width.HasValue ? Math.Max(0, constraint.Width.Value - 2) : null, constraint.Height.HasValue ? Math.Max(0, constraint.Height.Value - 2) : null));
        var cw = child.Visibility == Visibility.Collapsed ? 2 : (int) Math.Min(int.MaxValue, (long) d.Width + child.Margin.Horizontal + 2);
        var ch = child.Visibility == Visibility.Collapsed ? 2 : (int) Math.Min(int.MaxValue, (long) d.Height + child.Margin.Vertical + 2);
        return new Size(Math.Max(cw, hw + 2), ch);
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds) { if (Content is { } c) { ArrangeChild(c, new Thickness(1).Deflate(bounds), ResolvedAxes.Both); } }

    /// <inheritdoc/>
    protected override void OnRender(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0) { return; }
        var opaque = ControlAppearance.HasOpaqueFill(this, GetVisualState());
        if (opaque) { canvas.Clear(Bounds, ResolvedStyle); }
        var border = ControlAppearance.ResolveBorderStyle(this, GetVisualState());
        var bg = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        ControlChrome.DrawUniformBorder(canvas, Bounds, Glyphs, border, bg);
        if (!string.IsNullOrEmpty(Header) && Bounds.Width > 3) { _ = canvas.Clip(new Rect(Bounds.X + 1, Bounds.Y, Bounds.Width - 2, 1)).Draw($" {Header} ".AsSpan(), new Point(Bounds.X + 1, Bounds.Y), border, background: bg); }
    }
}
