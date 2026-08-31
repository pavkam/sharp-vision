// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Displays multiplexer topology, authorization, bounds, and effective routes.</summary>
internal sealed class RoutingPanel: CompositeControlBase
{
    private readonly Dictionary<string, Text> _values = [];
    private readonly Document _explanation;
    private readonly Grid _cards;
    private readonly GroupBox _topologyCard;
    private readonly GroupBox _decisionCard;
    private bool _isCompact;

    /// <summary>Initializes the structured routing report.</summary>
    internal RoutingPanel()
    {
        var topology = CreateTable(
            "Layers",
            "Outer profile",
            "Passthrough",
            "Pane visibility",
            "Approved operations",
            "Route state",
            "Bounds");
        var decisions = CreateTable(
            "Capability queries",
            "String queries",
            "Clipboard",
            "Graphics");
        _explanation = new Document
        {
            Height = Length.Cells(6),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        SetExplanation(
            "Routing policy",
            "Environment detection never invents an outer-terminal identity or authorization.");

        _cards = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ColumnSpacing = 1
        };
        _cards.Columns.Add(Track.Star(3));
        _cards.Columns.Add(Track.Star(2));
        _topologyCard = Card("Multiplexer topology", topology);
        _decisionCard = Card("Effective decisions", decisions);
        Grid.SetColumn(_topologyCard, 0);
        Grid.SetColumn(_decisionCard, 1);
        _cards.Children.Add(_topologyCard);
        _cards.Children.Add(_decisionCard);

        var root = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(1)
        };
        Dock.SetSide(_explanation, DockSide.Bottom);
        root.Children.Add(_explanation);
        root.Children.Add(_cards);
        InitializeContent(root);
    }

    /// <summary>Refreshes the route report from one immutable snapshot.</summary>
    /// <param name="diagnostics">The non-null terminal diagnostics.</param>
    internal void Refresh(TerminalDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var route = diagnostics.Route;
        var layers = route.Layers.Count == 0 ? "None" : string.Join(" → ", route.Layers);
        var outer = route.OuterProfile is { } profile
            ? $"{TextMarkup.Escape(profile.Description.Name)} · {profile.Description.Origin}"
            : "<d>None; not inferred</d>";

        Set("Layers", $"<info>{TextMarkup.Escape(layers)}</info> · nearest first");
        Set("Outer profile", outer);
        Set("Passthrough", route.Passthrough.ToString());
        Set("Pane visibility", route.PaneVisible ? "<success>✓ Visible</success>" : "<warning>○ Not visible</warning>");
        Set("Approved operations", TextMarkup.Escape(route.ApprovedOperations.ToString()));
        Set("Route state", route.IsActive ? "<success>✓ Active</success>" : "<d>— Inactive</d>");
        Set("Bounds", $"Depth {route.MaxDepth} · {route.MaxEnvelopeBytes:N0} byte envelope");
        Set("Capability queries", Allowed(route.CanRouteCapabilityQueries));
        Set("String queries", Allowed(route.SupportsStringTerminatedQueries));
        Set("Clipboard", Allowed(route.CanRouteClipboard));
        Set("Graphics", Allowed(route.CanRouteGraphics));

        if (route.Layers.Count > 0 && !route.IsActive)
        {
            SetExplanation(
                "Detected, deliberately blocked",
                "A multiplexer is present, but passthrough remains blocked until the host supplies an explicit outer profile and authorizes the operation family.");
        }
        else
        {
            SetExplanation(
                "Routing policy",
                "The table shows the effective route used by queries, clipboard operations, and graphics. Environment detection never invents outer-terminal identity or authorization.");
        }
    }

    /// <summary>Switches the route cards between side-by-side and stacked layouts.</summary>
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
            Grid.SetColumn(_decisionCard, 0);
            _cards.Columns.RemoveAt(1);
            _cards.Rows.Add(Track.Star(1));
            _cards.Rows.Add(Track.Star(1));
            Grid.SetRow(_decisionCard, 1);
            _cards.ColumnSpacing = 0;
            _cards.RowSpacing = 1;
            return;
        }

        Grid.SetRow(_decisionCard, 0);
        _cards.Rows.Clear();
        _cards.Columns.Add(Track.Star(2));
        Grid.SetColumn(_decisionCard, 1);
        _cards.ColumnSpacing = 1;
        _cards.RowSpacing = 0;
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
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded
        };
        table.Columns.Add(TableColumn.Fixed("Property", 22));
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

        return table;
    }

    private static GroupBox Card(string title, ControlBase content) => new()
    {
        HeaderText = title,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch,
        Content = content
    };

    private void Set(string label, string value) => _values[label].Content = value;

    private void SetExplanation(string title, string text)
    {
        _explanation.Blocks.Clear();
        _explanation.Blocks.Add(new DocumentHeading(2, title));
        _explanation.Blocks.Add(new DocumentParagraph(text));
    }

    private static string Allowed(bool value) => value
        ? "<success>✓ Routed</success>"
        : "<warning>○ Not routed</warning>";
}
