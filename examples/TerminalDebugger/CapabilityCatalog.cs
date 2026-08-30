// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Provides exhaustive presentation metadata for terminal capabilities.</summary>
internal static class CapabilityCatalog
{
    /// <summary>Gets every current terminal protocol in dashboard order.</summary>
    internal static IReadOnlyList<CapabilityDescriptor> All { get; } = Array.AsReadOnly<CapabilityDescriptor>(
    [
        new(TerminalProtocol.FocusReporting, "Input", "Focus reporting", "Reports when the terminal window gains or loses focus."),
        new(TerminalProtocol.BracketedPaste, "Input", "Bracketed paste", "Delivers pasted UTF-8 as one bounded event instead of simulated keystrokes."),
        new(TerminalProtocol.CellMouse, "Input", "Cell mouse", "Reports pointer activity using zero-based terminal-cell coordinates."),
        new(TerminalProtocol.PixelMouse, "Input", "Pixel mouse", "Adds pixel coordinates for high-resolution pointer diagnostics."),
        new(TerminalProtocol.KittyKeyboard, "Input", "Kitty keyboard", "Reports unambiguous keys, modifiers, repeats, and key releases."),
        new(TerminalProtocol.XtermKeyboard, "Input", "xterm keyboard", "Enhances modified-key reporting through modifyOtherKeys."),
        new(TerminalProtocol.SynchronizedOutput, "Output", "Synchronized output", "Batches frame updates so intermediate screen states stay hidden."),
        new(TerminalProtocol.Notifications, "Output", "Desktop notifications", "Sends an OSC 9 or OSC 777 desktop notification when explicitly enabled."),
        new(TerminalProtocol.Osc52, "Clipboard", "OSC 52 clipboard", "Reads or writes terminal clipboard text through OSC 52."),
        new(TerminalProtocol.KittyClipboard, "Clipboard", "Kitty clipboard", "Transfers typed clipboard data through Kitty OSC 5522."),
        new(TerminalProtocol.KittyGraphics, "Graphics", "Kitty graphics", "Places raster images through the Kitty graphics protocol."),
        new(TerminalProtocol.Sixel, "Graphics", "Sixel graphics", "Renders raster images through DCS sixel payloads."),
        new(TerminalProtocol.ItermImages, "Graphics", "iTerm2 images", "Displays inline images through the iTerm2 OSC extension."),
        new(TerminalProtocol.StyledUnderlines, "Rendition", "Underline styles", "Supports curly, dotted, dashed, and double underline variants."),
        new(TerminalProtocol.UnderlineColor, "Rendition", "Underline color", "Colors an underline independently from the foreground."),
        new(TerminalProtocol.Overline, "Rendition", "Overline", "Draws a line above text through SGR overline rendition.")
    ]);

    /// <summary>Validates that the catalog and active profile cover the same protocols.</summary>
    /// <param name="capabilities">The non-null active capability profile.</param>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The catalog and profile protocol sets differ.</exception>
    internal static void Validate(TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        var catalog = All.Select(static descriptor => descriptor.Protocol).Order().ToArray();
        var profile = capabilities.Features.Select(static feature => feature.Protocol).Order().ToArray();

        if (!catalog.SequenceEqual(profile))
        {
            throw new InvalidOperationException(
                "Terminal capability presentation metadata must cover every active protocol exactly once.");
        }
    }
}
