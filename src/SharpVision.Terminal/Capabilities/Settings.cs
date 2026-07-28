// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Defines explicit nullable caller overrides applied after all other evidence.</summary>
[PublicAPI]
public sealed record Settings
{
    /// <summary>Gets an optional color-depth override.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned color depth is unknown.</exception>
    public ColorDepth? ColorDepth
    {
        get;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The color depth is unknown.");
            }

            field = value;
        }
    }

    /// <summary>Gets an optional East Asian Ambiguous width override.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned policy is unknown.</exception>
    public Ambiguous? AmbiguousWidth
    {
        get;
        init
        {
            if (value.HasValue && !Enum.IsDefined(value.Value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The ambiguous-width policy is unknown.");
            }

            field = value;
        }
    }

    /// <summary>Gets an optional synchronized-output override.</summary>
    public bool? SynchronizedOutput { get; init; }

    /// <summary>Gets an optional focus-reporting override.</summary>
    public bool? FocusReporting { get; init; }

    /// <summary>Gets an optional bracketed-paste override.</summary>
    public bool? BracketedPaste { get; init; }

    /// <summary>Gets an optional pixel-mouse override.</summary>
    public bool? PixelMouse { get; init; }

    /// <summary>Gets an optional cell-mouse override.</summary>
    public bool? CellMouse { get; init; }

    /// <summary>Gets an optional Kitty keyboard override.</summary>
    public bool? KittyKeyboard { get; init; }

    /// <summary>Gets an optional xterm modifyOtherKeys override.</summary>
    public bool? XtermKeyboard { get; init; }

    /// <summary>Gets an optional OSC 52 override.</summary>
    public bool? Osc52 { get; init; }

    /// <summary>Gets an optional Kitty clipboard override.</summary>
    public bool? KittyClipboard { get; init; }

    /// <summary>Gets an optional Kitty graphics override.</summary>
    public bool? KittyGraphics { get; init; }

    /// <summary>Gets an optional sixel override.</summary>
    public bool? Sixel { get; init; }

    /// <summary>Gets an optional iTerm2 image override.</summary>
    public bool? ItermImages { get; init; }

    /// <summary>Gets an optional styled-underline override.</summary>
    public bool? StyledUnderlines { get; init; }

    /// <summary>Gets an optional underline-color override.</summary>
    public bool? UnderlineColor { get; init; }

    /// <summary>Gets an optional overline override.</summary>
    public bool? Overline { get; init; }
}
