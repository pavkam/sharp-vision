// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Provides exhaustive presentation metadata for terminal capabilities.</summary>
internal static class CapabilityCatalog
{
    /// <summary>Gets every current terminal protocol in dashboard order.</summary>
    internal static IReadOnlyList<CapabilityDescriptor> All { get; } = Array.AsReadOnly<CapabilityDescriptor>(
    [
        new("Foundation", "Terminal descriptions", "Loads bounded terminfo/termcap-style programs and key maps with explicit fallback diagnostics."),
        new("Foundation", "ECMA-48 controls", "Parses and emits the ECMA-48 control-function foundation used by terminal streams."),
        new("Foundation", "ANSI / VT screen", "Implements the ANSI/VT cursor, erasure, scrolling, and screen-state foundation."),
        new("Framing", "CSI grammar", "Parses incremental CSI parameter, intermediate, and final-byte sequences."),
        new("Framing", "OSC strings", "Parses and emits bounded Operating System Command strings with BEL or ST termination."),
        new("Framing", "DCS strings", "Parses and emits bounded Device Control Strings including status and sixel families."),
        new("Framing", "APC / PM / SOS", "Frames bounded application-program, privacy-message, and start-of-string sequences for safe recovery."),
        new("Output", "DEC private modes", "Owns reversible DEC mode leases for focus, paste, mouse, synchronized output, and cursor state."),
        new("Rendition", "SGR colors + styles", "Renders basic, indexed, and true color plus standard text attributes through SGR."),
        new("Input", "UTF-8 + graphemes", "Decodes Unicode scalars and renders extended grapheme clusters without splitting wide cells."),
        new("Input", "Terminfo key map", "Matches description-provided key sequences before applying ANSI and enhanced-keyboard grammars."),
        new("Discovery", "Device attributes", "Correlates primary and secondary device-attribute replies inside a bounded startup query batch."),
        new("Discovery", "Private mode queries", "Correlates DECRQM replies for optional terminal modes without trusting unrelated input."),
        new("Discovery", "XTGETTCAP", "Queries a finite approved set of terminal capability strings through DCS."),
        new("Discovery", "Palette queries", "Queries indexed and default colors through bounded OSC replies."),
        new("Discovery", "Window metrics", "Queries text-area pixels, cell pixels, and text-area cells through XTWINOPS."),
        new("Multiplexer", "tmux passthrough", "Wraps explicitly authorized query, clipboard, and graphics operations through bounded tmux DCS envelopes."),
        new("Multiplexer", "GNU screen passthrough", "Routes the safe supported subset through GNU screen without inventing outer-terminal identity."),
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

        var catalog = All.Where(static descriptor => descriptor.Protocol.HasValue)
            .Select(static descriptor => descriptor.Protocol!.Value)
            .Order()
            .ToArray();
        var profile = capabilities.Features.Select(static feature => feature.Protocol).Order().ToArray();

        if (!catalog.SequenceEqual(profile))
        {
            throw new InvalidOperationException(
                "Terminal capability presentation metadata must cover every active protocol exactly once.");
        }
    }
}
