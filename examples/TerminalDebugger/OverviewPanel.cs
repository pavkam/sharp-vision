// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Displays terminal identity, profile, services, modes, and renderer selection.</summary>
internal sealed class OverviewPanel: CompositeControlBase
{
    private readonly Dictionary<string, Text> _values = [];
    private readonly Grid _cards;
    private readonly GroupBox _connection;
    private readonly GroupBox _backend;
    private readonly GroupBox _modes;
    private readonly GroupBox _services;
    private bool _isCompact;

    /// <summary>Initializes the responsive dashboard cards.</summary>
    internal OverviewPanel()
    {
        _connection = CreateCard("Connection", "Terminal", "Geometry", "Color", "Unicode");
        _backend = CreateCard("Backend", "Selected", "Composition", "Evidence", "Graphics");
        _modes = CreateCard(
            "Live input modes",
            "Focus",
            "Bracketed paste",
            "Mouse",
            "Kitty keyboard",
            "xterm keys",
            "Clipboard events");
        _services = CreateCard("Terminal services", "Title", "Bell", "Clipboard", "Notifications");

        _cards = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(1),
            ColumnSpacing = 1,
            RowSpacing = 1
        };
        _cards.Columns.Add(Track.Star(1));
        _cards.Columns.Add(Track.Star(1));
        _cards.Rows.Add(Track.Star(1));
        _cards.Rows.Add(Track.Star(1));
        Place(_connection, column: 0, row: 0);
        Place(_backend, column: 1, row: 0);
        Place(_modes, column: 0, row: 1);
        Place(_services, column: 1, row: 1);
        _cards.Children.Add(_connection);
        _cards.Children.Add(_backend);
        _cards.Children.Add(_modes);
        _cards.Children.Add(_services);
        InitializeContent(_cards);
    }

    /// <summary>Refreshes all public environment and runtime facts.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void Refresh(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var description = application.Terminal.Description;
        var diagnostics = application.TerminalDiagnostics;
        var modes = diagnostics.Modes;
        var evidence = diagnostics.BackendEvidence.Count == 0
            ? "None; conservative fallback"
            : string.Join(", ", diagnostics.BackendEvidence.Select(
                static item => $"{item.Family} from {item.Source}"));

        Set("Terminal", $"<accent><b>{TextMarkup.Escape(description.Name)}</b></accent> · {description.Origin} · {description.Suitability}");
        Set("Geometry", $"<info>{application.Size.Width}×{application.Size.Height}</info> cells");
        Set("Color", $"{application.Capabilities.ColorDepth} · {application.Capabilities.ColorOrigin}");
        Set("Unicode", $"{application.Capabilities.UnicodeVersion} · ambiguous width {application.Capabilities.AmbiguousWidth}");

        Set("Selected", $"<info>{TextMarkup.Escape(diagnostics.BackendName)}</info> · {diagnostics.BackendFamily}");
        Set("Composition", string.Join(" → ", diagnostics.BackendExtensions));
        Set("Evidence", TextMarkup.Escape(evidence));
        Set("Graphics", $"{diagnostics.GraphicsBackend} · negotiation {diagnostics.NegotiationState}");

        Set("Focus", ConfiguredState(modes.FocusReportingConfigured, modes.FocusReportingAuthorized, modes.FocusReportingActive));
        Set("Bracketed paste", ConfiguredState(modes.BracketedPasteConfigured, modes.BracketedPasteAuthorized, modes.BracketedPasteActive));
        Set("Mouse", FormatMouse(modes));
        Set("Kitty keyboard", ConfiguredState(
            modes.KittyKeyboardEnhancements.HasValue,
            modes.KittyKeyboardAuthorized,
            modes.KittyKeyboardActive));
        Set("xterm keys", ConfiguredState(
            modes.ModifyOtherKeysLevel.HasValue,
            modes.ModifyOtherKeysAuthorized,
            modes.ModifyOtherKeysActive));
        Set("Clipboard events", ConfiguredState(
            modes.ClipboardPasteEventsConfigured,
            modes.ClipboardPasteEventsAuthorized,
            modes.ClipboardPasteEventsActive));

        Set("Title", Availability(application.Terminal.IsTitleSupported));
        Set("Bell", Availability(application.Terminal.Bell.IsSupported));
        Set("Clipboard", Availability(application.Terminal.Clipboard.IsSupported));
        Set("Notifications", Availability(application.Terminal.Notifications.IsSupported));
    }

    /// <summary>Switches the dashboard between two columns and one compact column.</summary>
    /// <param name="isCompact">Whether the available width needs a single-column layout.</param>
    internal void SetCompact(bool isCompact)
    {
        if (_isCompact == isCompact)
        {
            return;
        }

        _isCompact = isCompact;

        if (isCompact)
        {
            // Grid validates every retained child's placement before removing a definition.
            // Move column-one children into the surviving column first, then extend the rows
            // before assigning the lower cards to their new positions.
            Grid.SetColumn(_backend, 0);
            Grid.SetColumn(_services, 0);
            _cards.Columns.RemoveAt(1);
            _cards.Rows.Add(Track.Star(1));
            _cards.Rows.Add(Track.Star(1));

            Place(_connection, column: 0, row: 0);
            Place(_backend, column: 0, row: 1);
            Place(_modes, column: 0, row: 2);
            Place(_services, column: 0, row: 3);
            return;
        }

        // Move children out of rows that are about to be removed. Add the second column before
        // restoring any column-one placement for the same validated-mutation reason.
        Grid.SetRow(_modes, 0);
        Grid.SetRow(_services, 0);
        _cards.Rows.RemoveAt(3);
        _cards.Rows.RemoveAt(2);
        _cards.Columns.Add(Track.Star(1));
        Place(_connection, column: 0, row: 0);
        Place(_backend, column: 1, row: 0);
        Place(_modes, column: 0, row: 1);
        Place(_services, column: 1, row: 1);
    }

    private GroupBox CreateCard(string title, params string[] labels)
    {
        var table = new Table
        {
            IsFocusable = false,
            IsTabStop = false,
            SelectionMode = TableSelectionMode.None,
            ShowHeader = false,
            ShowGridLines = false,
            ColumnSpacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded
        };
        table.Columns.Add(TableColumn.Fixed("Property", 18));
        table.Columns.Add(TableColumn.Fill("Value"));

        foreach (var label in labels)
        {
            var value = new Text("<d>Waiting…</d>") { Overflow = Overflow.Wrap };
            _values.Add(label, value);
            table.Rows.Add(new TableRow([
                new Text($"<d>{label}</d>"),
                value
            ]));
        }

        return new GroupBox
        {
            HeaderText = title,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = table
        };
    }

    private static void Place(ControlBase control, int column, int row)
    {
        Grid.SetColumn(control, column);
        Grid.SetRow(control, row);
    }

    private void Set(string label, string value) => _values[label].Content = value;

    private static string ConfiguredState(bool configured, bool authorized, bool active) => active
        ? "<success>● Active</success>"
        : configured
            ? authorized
                ? "<warning>○ Authorized, inactive</warning>"
                : "<warning>○ Requested, blocked</warning>"
            : "<d>— Disabled</d>";

    private static string FormatMouse(TerminalModeDiagnostics modes) => modes.MouseActive
        ? $"<success>● Active</success> · {modes.MouseTracking}, {modes.MouseCoordinates}"
        : modes.MouseTracking.HasValue
            ? modes.MouseAuthorized
                ? $"<warning>○ Authorized, inactive</warning> · {modes.MouseTracking}"
                : $"<warning>○ Requested, blocked</warning> · {modes.MouseTracking}"
            : "<d>— Disabled</d>";

    private static string Availability(bool value) => value
        ? "<success>✓ Available</success>"
        : "<d>— Unavailable</d>";
}
