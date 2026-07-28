// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Contains nullable results from bounded capability queries.</summary>
[PublicAPI]
public sealed record Queries
{
    /// <summary>Gets one optional queried palette color.</summary>
    /// <exception cref="ArgumentException">The assigned response is empty or belongs to another family.</exception>
    public PaletteResponse? PaletteColor
    {
        get;
        init => field = Validate(value, ResponseKind.PaletteColor, nameof(PaletteColor));
    }

    /// <summary>Gets the optional queried default foreground color.</summary>
    /// <exception cref="ArgumentException">The assigned response is empty or belongs to another family.</exception>
    public PaletteResponse? ForegroundColor
    {
        get;
        init => field = Validate(value, ResponseKind.ForegroundColor, nameof(ForegroundColor));
    }

    /// <summary>Gets the optional queried default background color.</summary>
    /// <exception cref="ArgumentException">The assigned response is empty or belongs to another family.</exception>
    public PaletteResponse? BackgroundColor
    {
        get;
        init => field = Validate(value, ResponseKind.BackgroundColor, nameof(BackgroundColor));
    }

    /// <summary>Gets the optional queried text-area pixel size.</summary>
    /// <exception cref="ArgumentException">The assigned response is empty or belongs to another family.</exception>
    public MetricsResponse? WindowPixels
    {
        get;
        init => field = Validate(value, ResponseKind.WindowPixels, nameof(WindowPixels));
    }

    /// <summary>Gets the optional queried character-cell pixel size.</summary>
    /// <exception cref="ArgumentException">The assigned response is empty or belongs to another family.</exception>
    public MetricsResponse? CellPixels
    {
        get;
        init => field = Validate(value, ResponseKind.CellPixels, nameof(CellPixels));
    }

    /// <summary>Gets the optional queried text-area cell size.</summary>
    /// <exception cref="ArgumentException">The assigned response is empty or belongs to another family.</exception>
    public MetricsResponse? WindowCells
    {
        get;
        init => field = Validate(value, ResponseKind.WindowCells, nameof(WindowCells));
    }

    /// <summary>Gets a synchronized-output query result.</summary>
    public bool? SynchronizedOutput { get; init; }

    /// <summary>Gets a focus-reporting query result.</summary>
    public bool? FocusReporting { get; init; }

    /// <summary>Gets a bracketed-paste query result.</summary>
    public bool? BracketedPaste { get; init; }

    /// <summary>Gets a pixel-mouse query result.</summary>
    public bool? PixelMouse { get; init; }

    /// <summary>Gets a cell-mouse query result.</summary>
    public bool? CellMouse { get; init; }

    /// <summary>Gets a Kitty keyboard query result.</summary>
    public bool? KittyKeyboard { get; init; }

    /// <summary>Gets a validated xterm modifyOtherKeys query result.</summary>
    public bool? XtermKeyboard { get; init; }

    /// <summary>Gets one owned XTGETTCAP refinement response.</summary>
    public CapabilityResponse? CapabilityString { get; init; }

    /// <summary>Gets an OSC 52 query result.</summary>
    public bool? Osc52 { get; init; }

    /// <summary>Gets a Kitty clipboard query result.</summary>
    public bool? KittyClipboard { get; init; }

    /// <summary>Gets a Kitty graphics query result.</summary>
    public bool? KittyGraphics { get; init; }

    /// <summary>Gets a sixel query result.</summary>
    public bool? Sixel { get; init; }

    /// <summary>Gets an iTerm2 image query result.</summary>
    public bool? ItermImages { get; init; }

    /// <summary>Gets a styled-underline query result.</summary>
    public bool? StyledUnderlines { get; init; }

    /// <summary>Gets an underline-color query result.</summary>
    public bool? UnderlineColor { get; init; }

    /// <summary>Gets an overline query result.</summary>
    public bool? Overline { get; init; }

    private static PaletteResponse? Validate(
        PaletteResponse? response,
        ResponseKind expected,
        string propertyName)
    {
        return response is null
            ? null
            : response.Value.IsEmpty || response.Value.Kind != expected
                ? throw new ArgumentException("The response does not match the query result family.", propertyName)
                : response;
    }

    private static MetricsResponse? Validate(
        MetricsResponse? response,
        ResponseKind expected,
        string propertyName)
    {
        return response is null
            ? null
            : response.Value.IsEmpty || response.Value.Kind != expected
                ? throw new ArgumentException("The response does not match the query result family.", propertyName)
                : response;
    }
}
