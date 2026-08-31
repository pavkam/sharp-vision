// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Displays terminal identity, detected capability evidence, and live verification.</summary>
internal sealed class CapabilityDashboard: CompositeControlBase
{
    private readonly Text _terminalSummary;
    private readonly Text _serviceSummary;
    private readonly Table _table;
    private readonly Document _detail;
    private readonly Grid _matrix;
    private readonly GroupBox _detailGroup;
    private readonly List<CapabilityStatus> _statuses = [];
    private readonly Dictionary<TableRow, CapabilityStatus> _rowStatuses = [];
    private readonly Dictionary<CapabilityStatus, Text> _detectedCells = [];
    private readonly Dictionary<CapabilityStatus, Text> _evidenceCells = [];
    private readonly Dictionary<CapabilityStatus, Text> _sessionCells = [];
    private bool _isCompact;

    /// <summary>Initializes the retained capability dashboard.</summary>
    internal CapabilityDashboard()
    {
        _terminalSummary = new Text("Waiting for terminal profile…") { Overflow = Overflow.Wrap };
        _serviceSummary = new Text("Waiting for public services…") { Overflow = Overflow.Wrap };

        var environment = new Table
        {
            IsFocusable = false,
            IsTabStop = false,
            SelectionMode = TableSelectionMode.None,
            ShowHeader = false,
            ShowGridLines = false,
            ColumnSpacing = 2,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        environment.Columns.Add(TableColumn.Fixed("Context", 12));
        environment.Columns.Add(TableColumn.Fill("Value"));
        environment.Rows.Add(CreateSummaryRow("Terminal", _terminalSummary));
        environment.Rows.Add(CreateSummaryRow("Services", _serviceSummary));

        _table = new Table
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ScrollBars = ScrollBars.Both,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            SelectionMode = TableSelectionMode.Row,
            ShowGridLines = true
        };
        _table.Columns.Add(TableColumn.Fixed("Feature", 25));
        _table.Columns.Add(TableColumn.Fixed("Detected", 12));
        _table.Columns.Add(TableColumn.Fixed("Evidence", 12));
        _table.Columns.Add(TableColumn.Fill("Session"));
        _table.SelectionChanged += OnSelectionChanged;

        _detail = new Document
        {
            Padding = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        ShowEmptyDetail();

        _matrix = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ColumnSpacing = 1
        };
        _matrix.Columns.Add(Track.Star(3));
        _matrix.Columns.Add(Track.Star(2));
        Grid.SetColumn(_table, 0);
        _detailGroup = new GroupBox
        {
            HeaderText = "What this means",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = _detail
        };
        Grid.SetColumn(_detailGroup, 1);
        _matrix.Children.Add(_table);
        _matrix.Children.Add(_detailGroup);

        var root = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(1)
        };
        Dock.SetSide(environment, DockSide.Top);
        root.Children.Add(environment);
        root.Children.Add(_matrix);
        InitializeContent(root);
    }

    /// <summary>Raised after the counts represented by the dashboard change.</summary>
    internal event EventHandler? SummaryChanged;

    /// <summary>Gets the number of detected supported optional protocols.</summary>
    internal int SupportedCount => _statuses.Count(
        static status => status.Descriptor.IsNegotiated && status.Feature.State == CapabilitySupport.Supported);

    /// <summary>Gets the number of detected unsupported optional protocols.</summary>
    internal int UnsupportedCount => _statuses.Count(
        static status => status.Descriptor.IsNegotiated && status.Feature.State == CapabilitySupport.Unsupported);

    /// <summary>Gets the number of optional protocols with unknown support.</summary>
    internal int UnknownCount => _statuses.Count(
        static status => status.Descriptor.IsNegotiated &&
                         status.Feature.State is CapabilitySupport.Unknown or CapabilitySupport.Tentative);

    /// <summary>Gets the number of automatically observed or compared session checks.</summary>
    internal int VerifiedCount => _statuses.Count(
        static status => status.Verification is VerificationState.Observed or VerificationState.Passed);

    /// <summary>Gets the number of failed automatic comparisons.</summary>
    internal int FailedCount => _statuses.Count(static status => status.Verification == VerificationState.Failed);

    /// <summary>Loads one active terminal profile and service inventory.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void Initialize(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        CapabilityCatalog.Validate(application.Capabilities);
        _statuses.Clear();

        foreach (var descriptor in CapabilityCatalog.All)
        {
            _statuses.Add(new CapabilityStatus(
                descriptor,
                descriptor.Protocol is { } protocol
                    ? application.Capabilities.Support(protocol)
                    : null));
        }

        UpdateEnvironment(application);
        BuildRows();
    }

    /// <summary>Updates detected evidence without losing live verification state.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void UpdateCapabilities(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        CapabilityCatalog.Validate(application.Capabilities);

        foreach (var status in _statuses)
        {
            if (status.Descriptor.Protocol is { } protocol)
            {
                status.UpdateFeature(application.Capabilities.Support(protocol));
            }

            UpdateRow(status);
        }

        UpdateEnvironment(application);

        if (SelectedStatus() is { } selected)
        {
            ShowDetail(selected);
        }

        SummaryChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Refreshes terminal identity, dimensions, and public service availability.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void RefreshEnvironment(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        UpdateEnvironment(application);
    }

    /// <summary>Switches between side-by-side and stacked evidence layouts.</summary>
    /// <param name="isCompact">Whether the available terminal width needs a stacked layout.</param>
    internal void SetCompact(bool isCompact)
    {
        if (_isCompact == isCompact)
        {
            return;
        }

        _isCompact = isCompact;

        if (isCompact)
        {
            Grid.SetColumn(_detailGroup, 0);
            _matrix.Columns.Clear();
            _matrix.Columns.Add(Track.Star(1));
            _matrix.Rows.Add(Track.Star(3));
            _matrix.Rows.Add(Track.Star(2));
            Grid.SetRow(_detailGroup, 1);
            _matrix.ColumnSpacing = 0;
            _matrix.RowSpacing = 1;
            return;
        }

        Grid.SetRow(_detailGroup, 0);
        _matrix.Rows.Clear();
        _matrix.Columns.Add(Track.Star(2));
        Grid.SetColumn(_detailGroup, 1);
        _matrix.Columns[0] = Track.Star(3);
        _matrix.ColumnSpacing = 1;
        _matrix.RowSpacing = 0;
    }

    /// <summary>Records automatic live verification for one protocol.</summary>
    /// <param name="protocol">The verified protocol.</param>
    /// <param name="verification">The new verification state.</param>
    /// <param name="detail">The non-empty evidence explanation.</param>
    internal void SetVerification(TerminalProtocol protocol, VerificationState verification, string detail)
    {
        var status = _statuses.FirstOrDefault(item => item.Descriptor.Protocol == protocol) ??
                     throw new ArgumentOutOfRangeException(
                         nameof(protocol),
                         protocol,
                         "The protocol has no dashboard row.");

        status.SetVerification(verification, detail);
        UpdateRow(status);

        if (ReferenceEquals(SelectedStatus(), status))
        {
            ShowDetail(status);
        }

        SummaryChanged?.Invoke(this, EventArgs.Empty);
    }

    private static TableRow CreateSummaryRow(string label, ControlBase value) => new([
        new Text($"<d>{label}</d>"),
        value
    ]);

    private void UpdateEnvironment(Application application)
    {
        var description = application.Terminal.Description;
        var diagnostics = application.TerminalDiagnostics;
        _terminalSummary.Content =
            $"<accent><b>{TextMarkup.Escape(description.Name)}</b></accent> · {description.Origin} · " +
            $"<info>{TextMarkup.Escape(diagnostics.BackendName)}</info> · {diagnostics.GraphicsBackend} graphics · " +
            $"{application.Size.Width}×{application.Size.Height} · {application.Capabilities.ColorDepth} color · " +
            $"Unicode {application.Capabilities.UnicodeVersion}";
        _serviceSummary.Content =
            $"Title {Availability(application.Terminal.IsTitleSupported)}   " +
            $"Bell {Availability(application.Terminal.Bell.IsSupported)}   " +
            $"Clipboard {Availability(application.Terminal.Clipboard.IsSupported)}   " +
            $"Notifications {Availability(application.Terminal.Notifications.IsSupported)}";
    }

    private void BuildRows()
    {
        _rowStatuses.Clear();
        _detectedCells.Clear();
        _evidenceCells.Clear();
        _sessionCells.Clear();
        _table.Rows.Clear();

        foreach (var status in _statuses)
        {
            var detected = new Text(DetectedMarkup(status));
            var evidence = new Text(status.Descriptor.IsNegotiated ? status.Feature.Origin.ToString() : "Built-in");
            var session = new Text(SessionMarkup(status)) { Overflow = Overflow.Ellipsis };
            var row = new TableRow([
                new Text(status.Descriptor.Label) { Overflow = Overflow.Ellipsis },
                detected,
                evidence,
                session
            ]);
            _table.Rows.Add(row);
            _rowStatuses.Add(row, status);
            _detectedCells.Add(status, detected);
            _evidenceCells.Add(status, evidence);
            _sessionCells.Add(status, session);
        }

        if (_table.Rows.Count > 0)
        {
            _table.SelectRow(_table.Rows[0]);
            ShowDetail(_rowStatuses[_table.Rows[0]]);
        }

        SummaryChanged?.Invoke(this, EventArgs.Empty);
    }

    private CapabilityStatus? SelectedStatus() =>
        _table.SelectedRows.Count == 1 && _rowStatuses.TryGetValue(_table.SelectedRows[0], out var status)
            ? status
            : null;

    private void UpdateRow(CapabilityStatus status)
    {
        _detectedCells[status].Content = DetectedMarkup(status);
        _evidenceCells[status].Content = status.Descriptor.IsNegotiated
            ? status.Feature.Origin.ToString()
            : "Built-in";
        _sessionCells[status].Content = SessionMarkup(status);
    }

    private void OnSelectionChanged(object? sender, TableSelectionChangedEventArgs eventArgs)
    {
        if (_table.SelectedRows.Count == 1 && _rowStatuses.TryGetValue(_table.SelectedRows[0], out var status))
        {
            ShowDetail(status);
        }
    }

    private void ShowEmptyDetail()
    {
        _detail.Blocks.Clear();
        _detail.Blocks.Add(new DocumentHeading(2, "Feature evidence"));
        _detail.Blocks.Add(new DocumentParagraph("Select a feature to see what was detected and why."));
    }

    private void ShowDetail(CapabilityStatus status)
    {
        _detail.Blocks.Clear();
        _detail.Blocks.Add(new DocumentHeading(2, TextMarkup.Escape(status.Descriptor.Label)));

        if (status.Descriptor.IsNegotiated)
        {
            _detail.Blocks.Add(new DocumentParagraph(
                $"Detected: {SupportMarkup(status.Feature.State)}\n" +
                $"Evidence: <info>{status.Feature.Origin}</info>\n" +
                $"Output authorized: {YesNo(status.Feature.Authoritative)}"));
            _detail.Blocks.Add(new DocumentHeading(3, "This session"));
            _detail.Blocks.Add(new DocumentParagraph(
                $"{VerificationMarkup(status.Verification)} — {TextMarkup.Escape(status.VerificationDetail)}"));
        }
        else
        {
            _detail.Blocks.Add(new DocumentParagraph(
                "SharpVision support: <success>implemented</success>\n" +
                "Terminal evidence: <d>not separately negotiated</d>"));
        }

        _detail.Blocks.Add(new DocumentHeading(3, "Purpose"));
        _detail.Blocks.Add(new DocumentParagraph(TextMarkup.Escape(status.Descriptor.Explanation)));
    }

    private static string DetectedMarkup(CapabilityStatus status) => status.Descriptor.IsNegotiated
        ? SupportMarkup(status.Feature.State)
        : "<success>Implemented</success>";

    private static string SessionMarkup(CapabilityStatus status) => status.Verification switch
    {
        VerificationState.Observed => "<info>Observed live</info>",
        VerificationState.Passed => "<success>Compared successfully</success>",
        VerificationState.Failed => "<error>Automatic check failed</error>",
        VerificationState.NotRun => status.Descriptor.IsNegotiated ? "<d>Not exercised</d>" : "<d>Built-in</d>",
        _ => throw new ArgumentOutOfRangeException(
            nameof(status),
            status.Verification,
            "The verification state is unknown.")
    };

    private static string Availability(bool value) => value ? "<success>available</success>" : "<d>unavailable</d>";

    private static string YesNo(bool value) => value ? "<success>yes</success>" : "<d>no</d>";

    private static string SupportMarkup(CapabilitySupport support) => support switch
    {
        CapabilitySupport.Supported => "<success>Supported</success>",
        CapabilitySupport.Unsupported => "<error>Unsupported</error>",
        CapabilitySupport.Tentative => "<warning>Tentative</warning>",
        CapabilitySupport.Unknown => "<warning>Unknown</warning>",
        _ => throw new ArgumentOutOfRangeException(nameof(support), support, "The capability support state is unknown.")
    };

    private static string VerificationMarkup(VerificationState verification) => verification switch
    {
        VerificationState.Observed => "<info>Observed live</info>",
        VerificationState.Passed => "<success>Automatic comparison passed</success>",
        VerificationState.Failed => "<error>Automatic comparison failed</error>",
        VerificationState.NotRun => "<d>Not exercised</d>",
        _ => throw new ArgumentOutOfRangeException(
            nameof(verification),
            verification,
            "The verification state is unknown.")
    };
}
