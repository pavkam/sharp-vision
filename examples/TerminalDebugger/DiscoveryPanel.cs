// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

using SharpVision.Terminal.Xterm;

/// <summary>Displays every bounded startup query result retained by the runtime.</summary>
internal sealed class DiscoveryPanel: CompositeControlBase
{
    private readonly Text _status;
    private readonly Dictionary<string, Text> _values = [];

    /// <summary>Initializes the structured discovery report.</summary>
    internal DiscoveryPanel()
    {
        _status = new Text("<warning>Waiting for bounded terminal replies…</warning>")
        {
            Overflow = Overflow.Wrap
        };

        var privateModes = CreateTable(
            "Synchronized output",
            "Focus reporting",
            "Bracketed paste",
            "Cell mouse",
            "Pixel mouse",
            "Kitty keyboard",
            "xterm keyboard",
            "OSC 52",
            "Kitty clipboard",
            "Kitty graphics",
            "Sixel",
            "iTerm2 images",
            "Styled underlines",
            "Underline color",
            "Overline");
        var colors = CreateTable("Palette 0", "Foreground", "Background");
        var geometry = CreateTable("Window pixels", "Cell pixels", "Window cells");
        var terminalDatabase = CreateTable("Response", "Capability names");

        var content = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            Padding = new Thickness(1),
            Spacing = 1,
            Children =
            {
                _status,
                Card("Private modes and extensions", privateModes),
                Card("Reported colors", colors),
                Card("Reported geometry", geometry),
                Card("XTGETTCAP", terminalDatabase)
            }
        };
        InitializeContent(content);
    }

    /// <summary>Refreshes the query report from one immutable snapshot.</summary>
    /// <param name="diagnostics">The non-null terminal diagnostics.</param>
    internal void Refresh(TerminalDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var results = diagnostics.QueryResults;

        if (results is null)
        {
            _status.Content = diagnostics.NegotiationState == TerminalNegotiationState.Pending
                ? "<warning>● Negotiating — waiting for bounded terminal replies.</warning>"
                : "<d>— Active queries disabled; the configured profile supplied the evidence.</d>";

            foreach (var value in _values.Values)
            {
                value.Content = "<d>— No reply</d>";
            }

            return;
        }

        _status.Content =
            "<success>✓ Startup negotiation complete.</success> " +
            "<d>A dash means no correlated reply—not unsupported.</d>";
        Set("Synchronized output", Format(results.SynchronizedOutput));
        Set("Focus reporting", Format(results.FocusReporting));
        Set("Bracketed paste", Format(results.BracketedPaste));
        Set("Cell mouse", Format(results.CellMouse));
        Set("Pixel mouse", Format(results.PixelMouse));
        Set("Kitty keyboard", Format(results.KittyKeyboard));
        Set("xterm keyboard", Format(results.XtermKeyboard));
        Set("OSC 52", Format(results.Osc52));
        Set("Kitty clipboard", Format(results.KittyClipboard));
        Set("Kitty graphics", Format(results.KittyGraphics));
        Set("Sixel", Format(results.Sixel));
        Set("iTerm2 images", Format(results.ItermImages));
        Set("Styled underlines", Format(results.StyledUnderlines));
        Set("Underline color", Format(results.UnderlineColor));
        Set("Overline", Format(results.Overline));
        Set("Palette 0", Format(results.PaletteColor));
        Set("Foreground", Format(results.ForegroundColor));
        Set("Background", Format(results.BackgroundColor));
        Set("Window pixels", Format(results.WindowPixels));
        Set("Cell pixels", Format(results.CellPixels));
        Set("Window cells", Format(results.WindowCells));
        Set("Response", results.CapabilityResponseValid is { } valid
            ? valid ? "<success>✓ Accepted</success>" : "<error>× Rejected</error>"
            : "<d>— No reply</d>");
        Set("Capability names", results.CapabilityNames.Count == 0
            ? "<d>— None retained</d>"
            : TextMarkup.Escape(string.Join(", ", results.CapabilityNames)));
    }

    private Table CreateTable(params string[] labels)
    {
        var table = new Table
        {
            IsFocusable = false,
            IsTabStop = false,
            SelectionMode = TableSelectionMode.None,
            ShowHeader = false,
            ShowGridLines = false,
            ColumnSpacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        table.Columns.Add(TableColumn.Fixed("Query", 24));
        table.Columns.Add(TableColumn.Fill("Result"));

        foreach (var label in labels)
        {
            var value = new Text("<d>— Pending</d>") { Overflow = Overflow.Wrap };
            _values.Add(label, value);
            table.Rows.Add(new TableRow([
                new Text(label),
                value
            ]));
        }

        return table;
    }

    private static GroupBox Card(string title, ControlBase content) => new()
    {
        HeaderText = title,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Content = content
    };

    private void Set(string label, string value) => _values[label].Content = value;

    private static string Format(bool? value) => value switch
    {
        true => "<success>✓ Supported</success>",
        false => "<error>× Unsupported</error>",
        null => "<d>— No reply</d>"
    };

    private static string Format(PaletteResponse? value) => value is { } color
        ? $"<info>#{color.Red >> 8:X2}{color.Green >> 8:X2}{color.Blue >> 8:X2}</info>" +
          (color.Index is { } index ? $" · index {index}" : string.Empty)
        : "<d>— No reply</d>";

    private static string Format(MetricsResponse? value) => value is { } metrics
        ? $"<info>{metrics.Size.Width}×{metrics.Size.Height}</info>"
        : "<d>— No reply</d>";
}
