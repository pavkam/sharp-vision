// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Publishes an immutable terminal feature profile.
/// </summary>
[PublicAPI]
public sealed record TerminalCapabilities
{
    /// <summary>Gets the conservative profile used before detection.</summary>
    public static TerminalCapabilities Conservative { get; } = new();

    /// <summary>Gets the safe color fidelity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned color depth is unknown.</exception>
    public ColorDepth ColorDepth
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The color depth is unknown.");
            }

            field = value;
        }
    } = ColorDepth.Basic16;

    /// <summary>Gets the color-depth evidence origin.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned origin is unknown.</exception>
    public Origin ColorOrigin
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The color-depth evidence origin is unknown.");
            }

            field = value;
        }
    } = Origin.Default;

    /// <summary>Gets the pinned Unicode Character Database version.</summary>
    public string UnicodeVersion { get; } = UnicodeInfo.Version;

    /// <summary>Gets the East Asian Ambiguous cell-width policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The assigned policy is unknown.</exception>
    public Ambiguous AmbiguousWidth
    {
        get;
        init
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The ambiguous-width policy is unknown.");
            }

            field = value;
        }
    }

    /// <summary>Gets synchronized-output support.</summary>
    public Feature SynchronizedOutput { get; init; } = Feature.Unknown;

    /// <summary>Gets focus-reporting support.</summary>
    public Feature FocusReporting { get; init; } = Feature.Unknown;

    /// <summary>Gets bracketed-paste support.</summary>
    public Feature BracketedPaste { get; init; } = Feature.Unknown;

    /// <summary>Gets pixel-coordinate mouse support.</summary>
    public Feature PixelMouse { get; init; } = Feature.Unknown;

    /// <summary>Gets cell-coordinate mouse support.</summary>
    public Feature CellMouse { get; init; } = Feature.Unknown;

    /// <summary>Gets Kitty keyboard protocol support.</summary>
    public Feature KittyKeyboard { get; init; } = Feature.Unknown;

    /// <summary>Gets xterm modifyOtherKeys protocol support.</summary>
    public Feature XtermKeyboard { get; init; } = Feature.Unknown;

    /// <summary>Gets OSC 52 clipboard support.</summary>
    public Feature Osc52 { get; init; } = Feature.Unknown;

    /// <summary>Gets Kitty OSC 5522 clipboard support.</summary>
    public Feature KittyClipboard { get; init; } = Feature.Unknown;

    /// <summary>Gets Kitty graphics extension support.</summary>
    public Feature KittyGraphics { get; init; } = Feature.Unknown;

    /// <summary>Gets sixel graphics support.</summary>
    public Feature Sixel { get; init; } = Feature.Unknown;

    /// <summary>Gets iTerm2 inline image support.</summary>
    public Feature ItermImages { get; init; } = Feature.Unknown;

    /// <summary>Gets styled underline variant support.</summary>
    public Feature StyledUnderlines { get; init; } = Feature.Unknown;

    /// <summary>Gets independent underline-color support.</summary>
    public Feature UnderlineColor { get; init; } = Feature.Unknown;

    /// <summary>Gets overline rendition support.</summary>
    public Feature Overline { get; init; } = Feature.Unknown;

    /// <summary>Gets the support evidence for one optional protocol.</summary>
    /// <param name="protocol">The protocol to query.</param>
    /// <returns>The feature evidence.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="protocol"/> is unknown.</exception>
    public Feature Support(TerminalProtocol protocol) => protocol switch
    {
        TerminalProtocol.SynchronizedOutput => SynchronizedOutput,
        TerminalProtocol.FocusReporting => FocusReporting,
        TerminalProtocol.BracketedPaste => BracketedPaste,
        TerminalProtocol.PixelMouse => PixelMouse,
        TerminalProtocol.CellMouse => CellMouse,
        TerminalProtocol.KittyKeyboard => KittyKeyboard,
        TerminalProtocol.XtermKeyboard => XtermKeyboard,
        TerminalProtocol.Osc52 => Osc52,
        TerminalProtocol.KittyClipboard => KittyClipboard,
        TerminalProtocol.KittyGraphics => KittyGraphics,
        TerminalProtocol.Sixel => Sixel,
        TerminalProtocol.ItermImages => ItermImages,
        TerminalProtocol.StyledUnderlines => StyledUnderlines,
        TerminalProtocol.UnderlineColor => UnderlineColor,
        TerminalProtocol.Overline => Overline,
        _ => throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "The terminal protocol is unknown.")
    };

    /// <summary>Gets every optional protocol paired with its support evidence.</summary>
    public IReadOnlyList<ProtocolSupport> Features =>
    [
        new(TerminalProtocol.SynchronizedOutput, SynchronizedOutput),
        new(TerminalProtocol.FocusReporting, FocusReporting),
        new(TerminalProtocol.BracketedPaste, BracketedPaste),
        new(TerminalProtocol.PixelMouse, PixelMouse),
        new(TerminalProtocol.CellMouse, CellMouse),
        new(TerminalProtocol.KittyKeyboard, KittyKeyboard),
        new(TerminalProtocol.XtermKeyboard, XtermKeyboard),
        new(TerminalProtocol.Osc52, Osc52),
        new(TerminalProtocol.KittyClipboard, KittyClipboard),
        new(TerminalProtocol.KittyGraphics, KittyGraphics),
        new(TerminalProtocol.Sixel, Sixel),
        new(TerminalProtocol.ItermImages, ItermImages),
        new(TerminalProtocol.StyledUnderlines, StyledUnderlines),
        new(TerminalProtocol.UnderlineColor, UnderlineColor),
        new(TerminalProtocol.Overline, Overline)
    ];
}
