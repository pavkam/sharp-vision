// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents the Table control with mixed column sizing and headerless specimens.</summary>
internal sealed class TablePane: CompositeControlBase
{
    private Table? _progressive;
    private DelayedFailingSource? _progressiveSource;

    internal TablePane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "Table";

    /// <inheritdoc/>
    protected override void OnAttached()
    {
        base.OnAttached();

        // SetDataSource requires a running dispatcher, which is only available once this pane -
        // and, atomically with it, the progressive table built during construction - is actually
        // attached; building the table eagerly but binding its source lazily here keeps the rest
        // of this pane's content construction uniformly synchronous like every other specimen.
        // Posted rather than called directly: OnAttached fires while the whole tree's own attach
        // transaction is still open, and SetDataSource's first realization mutates the table's
        // owned presenter children - reentering that still-open transaction throws. Posting runs
        // it as a separate, later piece of dispatcher work once attachment fully settles.
        if (_progressive is { } table && _progressiveSource is { } source)
        {
            _progressive = null;
            _progressiveSource = null;

            // A quick sidebar navigation past this page can dispose this pane - and, with it,
            // the owned table - before this posted work ever runs. Skipping a stale post is the
            // same guard Table and ListView already apply to their own deferred child work.
            Dispatcher?.Post(() =>
            {
                if (!table.IsDisposed)
                {
                    table.SetDataSource(source, RowTemplate, rowHeight: 1);
                }
            });
        }
    }

    private static TableRow RowTemplate(int item) =>
        new([new Text($"Row {item:D5}")]);

    /// <inheritdoc/>
    private DocPage CreateContent()
    {
        var primary = new Table
        {
            Width = Length.Cells(46),
            ShowHeader = true,
            Style = _cellPadded,
            RowSpacing = 1
        };
        primary.Columns.Add(TableColumn.Fixed("Name", 15));
        primary.Columns.Add(TableColumn.Percent("Status", 25));
        primary.Columns.Add(TableColumn.Fill("Details"));
        primary.Rows.Add(new TableRow([
            new Text("Terminal core"),
            new Text("Stable"),
            new Text("ANSI, OSC, CSI, and input decoding.") { Overflow = Overflow.Wrap }
        ]));
        var linked = new Text(
            "Open <link=https://invisible-island.net/xterm/ctlseqs/ctlseqs.html>protocol guide</link>")
        {
            Overflow = Overflow.Wrap
        };
        primary.Rows.Add(new TableRow([
            new Text("UI toolkit"),
            new Text("Preview"),
            linked
        ]));

        var compact = new Table
        {
            Width = Length.Cells(46),
            ShowHeader = false,
            ShowGridLines = false,
            Style = _cellPadded,
            ColumnSpacing = 2
        };
        compact.Columns.Add(TableColumn.Auto("Key"));
        compact.Columns.Add(TableColumn.Fill("Meaning"));
        compact.Rows.Add(Shortcut("Ctrl+S", "Save current draft"));
        compact.Rows.Add(Shortcut("Enter", "Apply primary action"));
        compact.Rows.Add(Shortcut("Esc", "Close popup or window"));
        compact.Rows.Add(Shortcut("?", "Open shortcut guide"));

        var interactive = new Table
        {
            Width = Length.Cells(46),
            ShowGridLines = true,
            Style = _cellPadded
        };
        interactive.Columns.Add(TableColumn.Fixed("Action", 16));
        interactive.Columns.Add(TableColumn.Fill("Configuration"));
        var interactiveOption = new CheckBox
        {
            Text = "&Include integration tests",
            IsChecked = true,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        interactive.Rows.Add(new TableRow([
            new Button { Text = "&Run checks" },
            interactiveOption
        ]));

        var behaviorStatus = new Text("Click a value, press Enter to edit, or click a header to sort.");
        var behavior = new Table
        {
            Width = Length.Cells(42),
            SelectionMode = TableSelectionMode.MultipleCells,
            Style = _cellPadded,
            ShowGridLines = true
        };
        behavior.Columns.Add(TableColumn.Fixed("Field", 14, isReadOnly: true));
        behavior.Columns.Add(TableColumn.Fixed(
            "Value",
            22,
            sortKey: control => control is TextInput input ? input.Text : null));
        behavior.Rows.Add(new TableRow([
            new Text("Release"),
            new TextInput
            {
                Text = "1.0"
            }
        ]));
        behavior.Rows.Add(new TableRow([
            new Text("Channel"),
            new TextInput
            {
                Text = "Stable"
            }
        ]));
        behavior.SelectionChanged += (_, _) => behaviorStatus.Content = "Selection changed.";
        behavior.RowInvoked += (_, _) => behaviorStatus.Content = "Row activated.";
        behavior.SortChanged += (_, args) => behaviorStatus.Content =
            args.Direction == TableSortDirection.None
                ? "Sort reset."
                : $"Sorted {args.Direction.ToString().ToLowerInvariant()}.";
        var copyStatus = new Text("Clipboard: no selection copied.");
        var selectAllCells = new Button { Text = "Select &all cells" };
        selectAllCells.Click += (_, _) =>
        {
            behavior.SelectAll();
            copyStatus.Content = $"Clipboard: {behavior.CopySelection().Length} characters ready.";
        };
        var clearCells = new Button { Text = "&Clear cells" };
        clearCells.Click += (_, _) =>
        {
            behavior.ClearSelection();
            copyStatus.Content = "Clipboard: selection cleared.";
        };
        var copyCells = new Button { Text = "&Copy selection" };
        copyCells.Click += (_, _) => copyStatus.Content =
            $"Clipboard: {behavior.CopySelection().Length} characters ready.";

        var dynamic = new Table { Width = Length.Cells(42), Style = _cellPadded };
        dynamic.Columns.Add(TableColumn.Fixed("Release", 14));
        dynamic.Columns.Add(TableColumn.Fill("State"));
        dynamic.Rows.Add(new TableRow([new Text("1.0"), new Text("Stable")]));
        var rowStatus = new Text("Rows: 1");
        var addRow = new Button { Text = "&Add release row" };
        addRow.Click += (_, _) =>
        {
            dynamic.Rows.Add(new TableRow([new Text("1.1"), new Text("Preview")]));
            rowStatus.Content = $"Rows: {dynamic.Rows.Count}";
        };

        var unicode = new Table { Width = Length.Cells(44), Style = _cellPadded, ShowGridLines = true };
        unicode.Columns.Add(TableColumn.Auto("Name"));
        unicode.Columns.Add(TableColumn.Fill("Details"));
        unicode.Rows.Add(new TableRow([
            new Text("你好 👩‍💻"),
            new Text("Wide graphemes and wrapped details keep complete cell ownership.") { Overflow = Overflow.Wrap }
        ]));

        var headerOnly = new Table
        {
            Width = Length.Cells(44),
            ShowHeader = true,
            Style = _cellPadded
        };
        headerOnly.Columns.Add(TableColumn.Fixed("Environment", 14));
        headerOnly.Columns.Add(TableColumn.Fill("Status"));
        var constrained = new Table
        {
            ShowHeader = false,
            Width = Length.Cells(44),
            ShowGridLines = true,
            Style = _cellPadded
        };
        constrained.Columns.Add(TableColumn.Fixed("Shortcut", 14));
        constrained.Columns.Add(TableColumn.Fill("Description"));
        constrained.Rows.Add(new TableRow([new Text("F5"), new Text("Start debugging")]));
        constrained.Rows.Add(new TableRow([new Text("Ctrl+Shift+B"), new Text("Build solution")]));
        constrained.Rows.Add(new TableRow([new Text("Ctrl+P"), new Text("Quick open file")]));

        var scrolling = new Table
        {
            Width = Length.Cells(36),
            Height = Length.Cells(8),
            Style = _cellPadded,
            ShowGridLines = true,
            ScrollBars = ScrollBars.Both
        };
        scrolling.Columns.Add(TableColumn.Fixed("Service", 12));
        scrolling.Columns.Add(TableColumn.Fixed("State", 12));
        scrolling.Columns.Add(TableColumn.Fixed("Latest deployment detail", 24));

        for (var index = 1; index <= 10; index++)
        {
            scrolling.Rows.Add(new TableRow([
                new Text($"worker-{index:00}"),
                new Text(index % 3 == 0 ? "Queued" : "Healthy"),
                new Text($"Region eu-{(index % 3) + 1} · revision {1200 + index}")
            ]));
        }

        var progressiveSource = new DelayedFailingSource(50_000);
        var progressiveStatus = new Text("Fetches: 0 · Idle");
        var progressive = new Table
        {
            Width = Length.Cells(30),
            Height = Length.Cells(8),
            Style = _cellPadded,
            ShowGridLines = true,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            ScrollBarStyle = ScrollBarStyle.ThinLine
        };
        progressive.Columns.Add(TableColumn.Fixed("Row", 24));
        progressive.LoadStateChanged += (_, args) => progressiveStatus.Content =
            $"Fetches: {progressiveSource.FetchCount} · {args.State}";
        progressive.LoadFailed += (_, _) => progressiveStatus.Content =
            $"Fetches: {progressiveSource.FetchCount} · retrying a failed range";
        _progressive = progressive;
        _progressiveSource = progressiveSource;

        return new DocPage(
            Title,
            "<info>Table</info> owns typed <info>TableRow</info> and <info>TableColumn</info> definitions to render aligned rich terminal cells with optional headers and grid lines.",
            new DocSection(
                "📊",
                "Column sizing",
                "Fixed, automatic, percentage, and fill columns share the same finite track allocator.",
                new DocExample(
                    "Mixed data columns",
                    "Fixed identity, percentage status, and fill details stay contained while marked detail text wraps.",
                    primary,
                    "table.Columns.Add(TableColumn.Fixed(\"Name\", 15));\ntable.Columns.Add(TableColumn.Percent(\"Status\", 25));\ntable.Columns.Add(TableColumn.Fill(\"Details\"));")),
            new DocSection(
                "📊",
                "Header and grid chrome",
                "Headers and grid lines are optional; padding and spacing can carry simpler key/value structure.",
                new DocExample(
                    "Compact headerless table",
                    "Four fully visible shortcuts use emphasized keys and quieter action descriptions without needing a horizontal rail.",
                    compact)),
            new DocSection(
                "📊",
                "Interactive cells",
                "Every cell is an ordinary control, so focus, keyboard, pointer, and routed events remain available.",
                new DocExample(
                    "Actions and options",
                    "<reverse>Tab</reverse> into the <info>Button</info> and <info>CheckBox</info>; <info>Table</info> contributes layout only and does not intercept their semantics.",
                    interactive)),
            new DocSection(
                "📊",
                "Selection, sorting, and editing",
                "The table owns basic row/cell interaction while preserving ordinary TextInput ownership and the existing clipboard boundary.",
                new DocExample(
                    "Editable data table",
                    "Click a value cell and press <info>Enter</info> to edit; <info>Enter</info> commits, <info>Escape</info> cancels, and clicking a header cycles stable sorting. The Field column is read-only.",
                    new DocColumn(
                        behavior,
                        behaviorStatus,
                        new DocRow(selectAllCells, clearCells, copyCells),
                        copyStatus))),
            new DocSection(
                "📊",
                "Dynamic rows",
                "Rows transfer unique detached controls into table ownership and may be added or removed at runtime.",
                new DocExample(
                    "Append a release",
                    "Add a fresh row and observe the owned row count update without rebuilding the <info>Table</info>.",
                    new DocColumn(dynamic, addRow, rowStatus))),
            new DocSection(
                "📊",
                "Responsive text",
                "Marked links, CJK, emoji, and wrapping use the normal control and Unicode geometry pipeline inside cells.",
                new DocExample(
                    "Wide and wrapped cells",
                    "Narrow the page and the detail cell reflows while wide graphemes retain their continuation cells.",
                    unicode)),
            new DocSection(
                "📊",
                "Boundary states",
                "A header-only table renders columns and the separator without reserving data rows. A headerless table renders rows immediately.",
                new DocExample(
                    "Header-only and headerless tables",
                    "The first table defines columns but contains no data rows. The second omits the header row entirely.",
                    new DocColumn(headerOnly, constrained)),
                new DocExample(
                    "Deliberate two-axis overflow",
                    "This operational dataset is intentionally wider and taller than its viewport. Header, grid, cells, hit testing, and both themed rails stay aligned while scrolling.",
                    scrolling)),
            new DocSection(
                "🌐",
                "Progressive loading",
                "SetDataSource binds an index-addressed source instead of owned rows: only the visible window plus a small prefetch margin is ever requested, however large the logical extent.",
                new DocExample(
                    "50,000 rows over a deliberately slow, occasionally failing source",
                    "Scroll or page through 50,000 rows fetched on demand. The very first range is deliberately failed once to show the themed error placeholder recovering after a retry; every fetch is artificially delayed to keep placeholders visible.",
                    new DocColumn(progressive, progressiveStatus),
                    "table.SetDataSource(source, item => new TableRow([new Text($\"Row {item:D5}\")]), rowHeight: 1);")));
    }

    private static TableRow Shortcut(string key, string action) => new([
        new Text($"<accent><b>{Text.Escape(key)}</b></accent>"),
        new Text($"<d>{Text.Escape(action)}</d>")
    ]);

    // A deliberately slow, once-failing ITableDataSource<int> specimen: every fetch sleeps briefly
    // so placeholders stay visibly on screen, and the very first request fails once to demonstrate
    // the themed error glyph recovering through the controller's own bounded retry.
    private sealed class DelayedFailingSource(int total): ITableDataSource<int>
    {
        private readonly HashSet<int> _failedOnce = [];
        private int _fetchCount;

        /// <summary>Gets the number of LoadAsync calls issued so far, for the status specimen.</summary>
        internal int FetchCount => _fetchCount;

        public int? Count => total;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public object GetKey(int item) => item;

        public async ValueTask<TableDataResult<int>> LoadAsync(TableDataRequest request, CancellationToken cancellationToken)
        {
            _ = Interlocked.Increment(ref _fetchCount);
            await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken).ConfigureAwait(false);

            if (request.StartIndex == 0 && _failedOnce.Add(request.StartIndex))
            {
                throw new InvalidOperationException("Simulated transient failure for the showcase specimen.");
            }

            var count = Math.Min(request.Count, total - request.StartIndex);
            var items = Enumerable.Range(request.StartIndex, count).ToArray();
            return new TableDataResult<int> { Items = items, IsEndOfData = request.StartIndex + count >= total };
        }
    }
    // Every pane table wants the same one-cell horizontal cell padding, which now lives on the
    // style rather than the control.
    private static readonly TableStyle _cellPadded =
        TableStyle.Default with { CellPadding = new Thickness(1, 0) };

}
