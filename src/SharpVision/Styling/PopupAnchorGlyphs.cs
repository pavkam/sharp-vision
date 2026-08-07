// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Defines the complete immutable anchor-indicator arrow family drawn into a popup frame.</summary>
/// <remarks>
/// These four sit in the same one-cell frame slots as the eight border glyphs and are drawn by the
/// same loop, but were bare Rune literals with no style member and no width resolution. All four
/// (U+25B2/25BC/25C0/25B6) are East Asian Ambiguous, so under <c>Ambiguous.Wide</c> they measure two
/// cells, overrun their slot, and corrupt the frame row their eight neighbours are protected from
/// corrupting.
/// </remarks>
[PublicAPI]
public readonly record struct PopupAnchorGlyphs: IAppearanceFragment
{
    /// <summary>Initializes the complete anchor-indicator family.</summary>
    /// <param name="pointingUp">The arrow drawn on a top edge, for a surface placed below its anchor.</param>
    /// <param name="pointingDown">The arrow drawn on a bottom edge, for a surface placed above its anchor.</param>
    /// <param name="pointingLeft">The arrow drawn on a left edge, for a surface placed right of its anchor.</param>
    /// <param name="pointingRight">The arrow drawn on a right edge, for a surface placed left of its anchor.</param>
    /// <exception cref="ArgumentException">An arrow is a control or is not one cell wide.</exception>
    public PopupAnchorGlyphs(Rune pointingUp, Rune pointingDown, Rune pointingLeft, Rune pointingRight)
    {
        // Validated by parameter name here as well as in each init accessor, since an accessor only
        // ever knows the value as "value".
        PointingUp = pointingUp.ValidateSingleCell(nameof(pointingUp));
        PointingDown = pointingDown.ValidateSingleCell(nameof(pointingDown));
        PointingLeft = pointingLeft.ValidateSingleCell(nameof(pointingLeft));
        PointingRight = pointingRight.ValidateSingleCell(nameof(pointingRight));
    }

    /// <summary>Gets the established code-owned arrow family.</summary>
    public static PopupAnchorGlyphs Default { get; } = new(
        new Rune('▲'),
        new Rune('▼'),
        new Rune('◀'),
        new Rune('▶'));

    /// <summary>Gets the top-edge arrow.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune PointingUp
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the bottom-edge arrow.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune PointingDown
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the left-edge arrow.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune PointingLeft
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    /// <summary>Gets the right-edge arrow.</summary>
    /// <exception cref="ArgumentException">The replacement value is a control or is not one cell wide.</exception>
    public Rune PointingRight
    {
        get;
        init => field = value.ValidateSingleCell(nameof(value));
    }

    // The portable ASCII repair values stay code-owned, matching the eight border glyphs each of
    // these sits beside in the frame.
    internal ControlGlyph UpGlyph => new(PointingUp, new Rune('^'));

    internal ControlGlyph DownGlyph => new(PointingDown, new Rune('v'));

    internal ControlGlyph LeftGlyph => new(PointingLeft, new Rune('<'));

    internal ControlGlyph RightGlyph => new(PointingRight, new Rune('>'));

    IAppearanceFragment IAppearanceFragment.Clone() => this with { };
}
