// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

/// <summary>Hosts one passive or explicitly interactive content region in a <see cref="StatusBar"/>.</summary>
[PublicAPI]
public sealed class StatusBarItem: ContentControl
{
    private readonly StyleSlot<StatusBarItemStyle> _style;

    /// <summary>Initializes an empty passive item aligned to the leading edge group.</summary>
    public StatusBarItem() => _style = InitializeStyle(StatusBarItemStyle.Definition);

    /// <summary>Gets or sets the complete local presentation, or null for theme ownership.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public StatusBarItemStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the complete local, theme-owned, or code-owned presentation.</summary>
    public StatusBarItemStyle ActualStyle => _style.Actual;

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

    // Presence and appearance are separate facts and were previously one nullable Rune. Whether a
    // separator exists reserves a cell and so is layout, which the item owns; which glyph it draws
    // is presentation, which the theme owns. Collapsing them meant a theme could not restyle the
    // glyph without also deciding, per item, whether there was one.

    /// <summary>Gets or sets whether a separator is drawn before the retained content, reserving one
    /// cell for it.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public bool ShowLeftSeparator
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets whether a separator is drawn after the retained content, reserving one
    /// cell for it.</summary>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public bool ShowRightSeparator
    {
        get;
        set => _ = SetProperty(ref field, value, InvalidationImpact.Measure);
    }

    /// <summary>Gets or sets a per-item override for the leading separator glyph, or null to use the
    /// themed one. Ignored unless <see cref="ShowLeftSeparator"/> is set.</summary>
    /// <exception cref="ArgumentException">The value is a control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Rune? LeftSeparator
    {
        get;
        set
        {
            ValidateSeparator(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets or sets a per-item override for the trailing separator glyph, or null to use the
    /// themed one. Ignored unless <see cref="ShowRightSeparator"/> is set.</summary>
    /// <exception cref="ArgumentException">The value is a control or is not one cell wide.</exception>
    /// <exception cref="InvalidOperationException">The attached item is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The item is disposed.</exception>
    public Rune? RightSeparator
    {
        get;
        set
        {
            ValidateSeparator(value, nameof(value));
            _ = SetProperty(ref field, value, InvalidationImpact.Render);
        }
    }

    /// <summary>Gets the leading separator glyph actually drawn: this item's own override when one is
    /// assigned, otherwise the themed glyph.</summary>
    public Rune ActualLeftSeparator => LeftSeparator ?? ActualStyle.LeftSeparatorGlyph;

    /// <summary>Gets the trailing separator glyph actually drawn.</summary>
    public Rune ActualRightSeparator => RightSeparator ?? ActualStyle.RightSeparatorGlyph;

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        var separators = SeparatorCount;
        var desired = base.MeasureOverride(
            new Constraint(constraint.Width.Subtract(separators), constraint.Height));

        return new Size(
            desired.Width.Add(separators),
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
                ResolveSeparator(ActualLeftSeparator),
                new Point(bounds.X, bounds.Y),
                style,
                BackgroundMode.Transparent);
        }

        if (right != 0)
        {
            canvas.DrawRune(
                ResolveSeparator(ActualRightSeparator),
                new Point(bounds.Right - 1, bounds.Y),
                style,
                BackgroundMode.Transparent);
        }
    }

    private int SeparatorCount => (ShowLeftSeparator ? 1 : 0) + (ShowRightSeparator ? 1 : 0);

    private (int Left, int Right) Insets(int width)
    {
        Debug.Assert(width >= 0, "Status item content width is non-negative.");
        var left = ShowLeftSeparator && width > 0 ? 1 : 0;
        var right = ShowRightSeparator && width > left ? 1 : 0;
        return (left, right);
    }

    private Rune ResolveSeparator(Rune value) =>
        value.Resolve(new Rune('|'), CellPolicy.AmbiguousWidth);

    private static void ValidateSeparator(Rune? value, string parameterName)
    {
        if (value.HasValue)
        {
            _ = value.Value.ValidateSingleCell(parameterName);
        }
    }

}
