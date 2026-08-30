// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

using Xterm;

/// <summary>Publishes normalized query facts without retaining raw terminal reply values.</summary>
[PublicAPI]
public sealed class TerminalQueryDiagnostics
{
    private readonly ReadOnlyCollection<CapabilityName> _capabilityNames;

    /// <summary>Copies normalized and redacted facts from one completed query batch.</summary>
    /// <param name="results">The non-null owned query results.</param>
    /// <exception cref="ArgumentNullException"><paramref name="results"/> is null.</exception>
    internal TerminalQueryDiagnostics(QueryResults results)
    {
        ArgumentNullException.ThrowIfNull(results);
        PaletteColor = results.PaletteColor;
        ForegroundColor = results.ForegroundColor;
        BackgroundColor = results.BackgroundColor;
        WindowPixels = results.WindowPixels;
        CellPixels = results.CellPixels;
        WindowCells = results.WindowCells;
        SynchronizedOutput = results.SynchronizedOutput;
        FocusReporting = results.FocusReporting;
        BracketedPaste = results.BracketedPaste;
        PixelMouse = results.PixelMouse;
        CellMouse = results.CellMouse;
        KittyKeyboard = results.KittyKeyboard;
        XtermKeyboard = results.XtermKeyboard;
        CapabilityResponseValid = results.CapabilityString?.Valid;
        _capabilityNames = Array.AsReadOnly(results.CapabilityString?.Items.Keys.ToArray() ?? []);
        Osc52 = results.Osc52;
        KittyClipboard = results.KittyClipboard;
        KittyGraphics = results.KittyGraphics;
        Sixel = results.Sixel;
        ItermImages = results.ItermImages;
        StyledUnderlines = results.StyledUnderlines;
        UnderlineColor = results.UnderlineColor;
        Overline = results.Overline;
    }

    /// <summary>Gets the normalized queried palette color.</summary>
    public PaletteResponse? PaletteColor { get; }

    /// <summary>Gets the normalized queried foreground color.</summary>
    public PaletteResponse? ForegroundColor { get; }

    /// <summary>Gets the normalized queried background color.</summary>
    public PaletteResponse? BackgroundColor { get; }

    /// <summary>Gets the queried window size in pixels.</summary>
    public MetricsResponse? WindowPixels { get; }

    /// <summary>Gets the queried cell size in pixels.</summary>
    public MetricsResponse? CellPixels { get; }

    /// <summary>Gets the queried window size in cells.</summary>
    public MetricsResponse? WindowCells { get; }

    /// <summary>Gets the synchronized-output query result.</summary>
    public bool? SynchronizedOutput { get; }

    /// <summary>Gets the focus-reporting query result.</summary>
    public bool? FocusReporting { get; }

    /// <summary>Gets the bracketed-paste query result.</summary>
    public bool? BracketedPaste { get; }

    /// <summary>Gets the pixel-mouse query result.</summary>
    public bool? PixelMouse { get; }

    /// <summary>Gets the cell-mouse query result.</summary>
    public bool? CellMouse { get; }

    /// <summary>Gets the Kitty keyboard query result.</summary>
    public bool? KittyKeyboard { get; }

    /// <summary>Gets the xterm keyboard query result.</summary>
    public bool? XtermKeyboard { get; }

    /// <summary>Gets whether the finite XTGETTCAP response was valid.</summary>
    public bool? CapabilityResponseValid { get; }

    /// <summary>Gets copied XTGETTCAP names without their raw byte values.</summary>
    public IReadOnlyList<CapabilityName> CapabilityNames => _capabilityNames;

    /// <summary>Gets the OSC 52 query result.</summary>
    public bool? Osc52 { get; }

    /// <summary>Gets the Kitty clipboard query result.</summary>
    public bool? KittyClipboard { get; }

    /// <summary>Gets the Kitty graphics query result.</summary>
    public bool? KittyGraphics { get; }

    /// <summary>Gets the sixel query result.</summary>
    public bool? Sixel { get; }

    /// <summary>Gets the iTerm2 image query result.</summary>
    public bool? ItermImages { get; }

    /// <summary>Gets the styled-underline query result.</summary>
    public bool? StyledUnderlines { get; }

    /// <summary>Gets the underline-color query result.</summary>
    public bool? UnderlineColor { get; }

    /// <summary>Gets the overline query result.</summary>
    public bool? Overline { get; }
}
