// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

/// <summary>Frames one owned content control with a titled border.</summary>
[PublicAPI]
public sealed class GroupBox: ContentControl
{
    /// <summary>Initializes an empty group box.</summary>
    public GroupBox()
    {
    }

    /// <inheritdoc/>
    protected override ThemeRole ThemeRole => ThemeRole.Container;

    /// <summary>Gets or sets the non-null title written into the top edge.</summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    /// <exception cref="ArgumentException">The value contains a terminal control character.</exception>
    public string Header
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            value.ThrowIfContainsControls(
                nameof(value),
                "A group header cannot contain terminal controls.");
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    } = string.Empty;

    /// <inheritdoc/>
    protected override string? AccessKeyText => Header;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var child = Content;
        var hw = Header.Length == 0
            ? 0
            : (int) Math.Min(
                int.MaxValue,
                2L + Header.Measure(CellPolicy.AmbiguousWidth, UseMnemonic));
        if (child is null)
        {
            return new Size(hw, 0);
        }

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
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is { } c)
        {
            ArrangeChild(c, bounds, ResolvedAxes.Both);
        }
    }

    /// <inheritdoc/>
    protected override ChromeRenderOptions GetChromeRenderOptions() => new() { SkipBodyFill = true, SkipBorder = true };

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var opaque = this.HasOpaqueFill(GetAppearanceState());
        if (opaque)
        {
            canvas.Clear(Bounds, ResolvedStyle);
        }
    }

    /// <inheritdoc/>
    internal override void RenderOverlay(TerminalCanvas canvas)
    {
        if (Bounds.Width == 0 || Bounds.Height == 0)
        {
            return;
        }

        var opaque = this.HasOpaqueFill(GetAppearanceState());
        var border = this.ResolveBorderStyle(GetAppearanceState());
        var bg = opaque ? BackgroundMode.Opaque : BackgroundMode.Transparent;
        var actualBorder = ActualBorder;
        canvas.DrawPartialBorder(
            Bounds,
            actualBorder.Sides,
            ResolveBorderGlyphs(actualBorder.GlyphStyle),
            border,
            bg);
        if (!string.IsNullOrEmpty(Header) && Bounds.Width > 3)
        {
            var title = canvas.Clip(new Rect(Bounds.X + 1, Bounds.Y, Bounds.Width - 2, 1));
            var start = title.Draw(
                " ".AsSpan(),
                new Point(Bounds.X + 1, Bounds.Y),
                border,
                background: bg);
            var cells = Header.Draw(
                title,
                start.Final,
                border,
                bg,
                CellPolicy.AmbiguousWidth,
                UseMnemonic,
                EffectiveIsEnabled ? Theme?.Hotkey ?? Color.Default : null);
            _ = title.Draw(
                " ".AsSpan(),
                new Point(start.Final.X + cells, start.Final.Y),
                border,
                background: bg);
        }
    }
}
