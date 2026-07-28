// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Hosts one passive or explicitly interactive content region in a <see cref="StatusBar"/>.</summary>
[PublicAPI]
public sealed class StatusBarItem: ContentControl
{
    /// <summary>Initializes an empty passive item aligned to the leading edge group.</summary>
    public StatusBarItem()
    {
    }

    /// <summary>Gets or sets the physical edge group that owns this item during horizontal layout.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public StatusBarItemAlignment Alignment
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The status-bar alignment is unknown.");
            }

            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets an optional one-cell glyph rendered before the retained content.</summary>
    /// <exception cref="ArgumentException">The value is a control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Rune? LeftSeparator
    {
        get;
        set
        {
            ValidateSeparator(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <summary>Gets or sets an optional one-cell glyph rendered after the retained content.</summary>
    /// <exception cref="ArgumentException">The value is a control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Rune? RightSeparator
    {
        get;
        set
        {
            ValidateSeparator(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Measure);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var separators = SeparatorCount;
        var desired = base.MeasureOverride(
            new Constraint(LayoutMath.Subtract(constraint.Width, separators), constraint.Height));

        return new Size(
            LayoutMath.Add(desired.Width, separators),
            separators == 0 ? desired.Height : Math.Max(1, desired.Height));
    }

    /// <inheritdoc/>
    protected override void ArrangeOverride(Rect bounds)
    {
        if (Content is not { } content)
        {
            return;
        }

        var (left, right) = Insets(bounds.Width);
        ArrangeChild(
            content,
            new Rect(bounds.X + left, bounds.Y, bounds.Width - left - right, bounds.Height),
            ResolvedAxes.Both);
    }

    /// <inheritdoc/>
    protected override void OnRenderContent(TerminalCanvas canvas)
    {
        var bounds = ContentBounds;
        var (left, right) = Insets(bounds.Width);
        var style = ResolvedStyle;

        if (left != 0)
        {
            canvas.DrawRune(
                ResolveSeparator(LeftSeparator!.Value),
                new Point(bounds.X, bounds.Y),
                style,
                BackgroundMode.Transparent);
        }

        if (right != 0)
        {
            canvas.DrawRune(
                ResolveSeparator(RightSeparator!.Value),
                new Point(bounds.Right - 1, bounds.Y),
                style,
                BackgroundMode.Transparent);
        }
    }

    private int SeparatorCount => (LeftSeparator.HasValue ? 1 : 0) + (RightSeparator.HasValue ? 1 : 0);

    private (int Left, int Right) Insets(int width)
    {
        Debug.Assert(width >= 0, "Status item content width is non-negative.");
        var left = LeftSeparator.HasValue && width > 0 ? 1 : 0;
        var right = RightSeparator.HasValue && width > left ? 1 : 0;
        return (left, right);
    }

    private Rune ResolveSeparator(Rune value) =>
        CellGlyphResolver.Resolve(value, new Rune('|'), CellPolicy.AmbiguousWidth);

    private static void ValidateSeparator(Rune? value, string parameterName)
    {
        if (value.HasValue)
        {
            _ = CellGlyphResolver.ValidateSingleCell(value.Value, parameterName);
        }
    }

}
