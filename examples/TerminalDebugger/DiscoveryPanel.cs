// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

using SharpVision.Terminal.Xterm;

/// <summary>Displays every bounded startup query result retained by the runtime.</summary>
internal sealed class DiscoveryPanel: CompositeControlBase
{
    private readonly Text _content;

    /// <summary>Initializes the scrollable discovery report.</summary>
    internal DiscoveryPanel()
    {
        _content = new Text("<d>Waiting for startup negotiation…</d>") { Overflow = Overflow.Wrap };
        InitializeContent(new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Padding = new Thickness(1),
            Children = { _content }
        });
    }

    /// <summary>Refreshes the query report from one immutable snapshot.</summary>
    /// <param name="diagnostics">The non-null terminal diagnostics.</param>
    internal void Refresh(TerminalDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var results = diagnostics.QueryResults;

        if (results is null)
        {
            _content.Content = diagnostics.NegotiationState == TerminalNegotiationState.Pending
                ? "<accent><b>Startup negotiation</b></accent>\n<warning>Pending bounded terminal replies…</warning>"
                : "<accent><b>Startup negotiation</b></accent>\n<d>Disabled; the configured profile was used without active queries.</d>";
            return;
        }

        var builder = new StringBuilder()
            .Append("<accent><b>Startup negotiation: completed</b></accent>\n")
            .Append("A dash means no correlated reply was retained; it does not mean unsupported.\n\n")
            .Append("<accent><b>Private modes and extensions</b></accent>\n");
        Append(builder, "Synchronized output", results.SynchronizedOutput);
        Append(builder, "Focus reporting", results.FocusReporting);
        Append(builder, "Bracketed paste", results.BracketedPaste);
        Append(builder, "Cell mouse", results.CellMouse);
        Append(builder, "Pixel mouse", results.PixelMouse);
        Append(builder, "Kitty keyboard", results.KittyKeyboard);
        Append(builder, "xterm keyboard", results.XtermKeyboard);
        Append(builder, "OSC 52", results.Osc52);
        Append(builder, "Kitty clipboard", results.KittyClipboard);
        Append(builder, "Kitty graphics", results.KittyGraphics);
        Append(builder, "Sixel", results.Sixel);
        Append(builder, "iTerm2 images", results.ItermImages);
        Append(builder, "Styled underlines", results.StyledUnderlines);
        Append(builder, "Underline color", results.UnderlineColor);
        Append(builder, "Overline", results.Overline);
        _ = builder.Append("\n<accent><b>Colors</b></accent>\n")
            .Append("Palette 0: ").Append(Format(results.PaletteColor)).Append('\n')
            .Append("Foreground: ").Append(Format(results.ForegroundColor)).Append('\n')
            .Append("Background: ").Append(Format(results.BackgroundColor)).Append('\n')
            .Append("\n<accent><b>Geometry</b></accent>\n")
            .Append("Window pixels: ").Append(Format(results.WindowPixels)).Append('\n')
            .Append("Cell pixels: ").Append(Format(results.CellPixels)).Append('\n')
            .Append("Window cells: ").Append(Format(results.WindowCells)).Append('\n')
            .Append("\n<accent><b>XTGETTCAP</b></accent>\n")
            .Append(results.CapabilityResponseValid is { } valid
                ? $"Accepted: {valid}; items: {string.Join(", ", results.CapabilityNames)}"
                : "—");
        _content.Content = builder.ToString();
    }

    private static void Append(StringBuilder builder, string label, bool? value) =>
        _ = builder.Append(label).Append(": ").Append(value switch
        {
            true => "<success>supported</success>",
            false => "<error>unsupported</error>",
            null => "<d>—</d>"
        }).Append('\n');

    private static string Format(PaletteResponse? value) => value is { } color
        ? $"#{color.Red >> 8:X2}{color.Green >> 8:X2}{color.Blue >> 8:X2}" +
          (color.Index is { } index ? $" (index {index})" : string.Empty)
        : "—";

    private static string Format(MetricsResponse? value) => value is { } metrics
        ? $"{metrics.Size.Width}×{metrics.Size.Height}"
        : "—";
}
