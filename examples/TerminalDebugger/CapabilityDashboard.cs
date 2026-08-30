// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Displays terminal identity, detected capability evidence, and live verification.</summary>
internal sealed class CapabilityDashboard: CompositeControlBase
{
    private readonly Text _environment;
    private readonly ListView _list;
    private readonly Text _detail;
    private readonly Grid _matrix;
    private readonly GroupBox _detailGroup;
    private readonly Dictionary<TerminalProtocol, CapabilityStatus> _statuses = [];
    private bool _isCompact;

    /// <summary>Initializes the retained capability dashboard.</summary>
    internal CapabilityDashboard()
    {
        _environment = new Text("<d>Waiting for terminal profile…</d>")
        {
            Padding = new Thickness(1),
            Overflow = Overflow.Wrap
        };
        _list = new ListView
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            RowHeight = Length.Cells(1),
            ItemTemplate = item => new Text(((CapabilityStatus) item!).RowText)
            {
                Overflow = Overflow.Ellipsis,
                Padding = new Thickness(1, 0)
            }
        };
        _detail = new Text("Select a capability for evidence and verification details.")
        {
            Padding = new Thickness(1),
            Overflow = Overflow.Wrap
        };
        _list.SelectionChanged += OnSelectionChanged;

        _matrix = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ColumnSpacing = 1
        };
        _matrix.Columns.Add(Track.Star(3));
        _matrix.Columns.Add(Track.Star(2));
        Grid.SetColumn(_list, 0);
        _detailGroup = new GroupBox
        {
            HeaderText = "Evidence",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = _detail
        };
        Grid.SetColumn(_detailGroup, 1);
        _matrix.Children.Add(_list);
        _matrix.Children.Add(_detailGroup);

        var root = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Dock.SetSide(_environment, DockSide.Top);
        root.Children.Add(_environment);
        root.Children.Add(_matrix);
        InitializeContent(root);
    }

    /// <summary>Raised after the counts represented by the dashboard change.</summary>
    internal event EventHandler? SummaryChanged;

    /// <summary>Gets the number of detected supported optional protocols.</summary>
    internal int SupportedCount => _statuses.Values.Count(static status => status.Feature.State == CapabilitySupport.Supported);

    /// <summary>Gets the number of detected unsupported optional protocols.</summary>
    internal int UnsupportedCount => _statuses.Values.Count(static status => status.Feature.State == CapabilitySupport.Unsupported);

    /// <summary>Gets the number of optional protocols with unknown support.</summary>
    internal int UnknownCount => _statuses.Values.Count(
        static status => status.Feature.State is CapabilitySupport.Unknown or CapabilitySupport.Tentative);

    /// <summary>Gets the number of observed or passed session checks.</summary>
    internal int VerifiedCount => _statuses.Values.Count(
        static status => status.Verification is VerificationState.Observed or VerificationState.Passed);

    /// <summary>Gets the number of failed session checks.</summary>
    internal int FailedCount => _statuses.Values.Count(static status => status.Verification == VerificationState.Failed);

    /// <summary>Loads one active terminal profile and service inventory.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void Initialize(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        CapabilityCatalog.Validate(application.Capabilities);
        _statuses.Clear();

        foreach (var descriptor in CapabilityCatalog.All)
        {
            _statuses.Add(descriptor.Protocol, new CapabilityStatus(
                descriptor,
                application.Capabilities.Support(descriptor.Protocol)));
        }

        UpdateEnvironment(application);
        RefreshItems(selectFirst: true);
    }

    /// <summary>Updates detected evidence without losing live verification state.</summary>
    /// <param name="application">The non-null running application.</param>
    internal void UpdateCapabilities(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        CapabilityCatalog.Validate(application.Capabilities);

        foreach (var status in _statuses.Values)
        {
            status.UpdateFeature(application.Capabilities.Support(status.Descriptor.Protocol));
        }

        UpdateEnvironment(application);
        RefreshItems(selectFirst: false);
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

    private void UpdateEnvironment(Application application)
    {
        var description = application.Terminal.Description;
        _environment.Content =
            $"<accent><b>{TextMarkup.Escape(description.Name)}</b></accent> · source {description.Origin} · " +
            $"<info>{application.Size.Width}×{application.Size.Height}</info> cells · " +
            $"{application.Capabilities.ColorDepth} color ({application.Capabilities.ColorOrigin}) · " +
            $"Unicode {application.Capabilities.UnicodeVersion} · ambiguous width {application.Capabilities.AmbiguousWidth}\n" +
            $"Services: title {YesNo(application.Terminal.IsTitleSupported)}, bell {YesNo(application.Terminal.Bell.IsSupported)}, " +
            $"clipboard {YesNo(application.Terminal.Clipboard.IsSupported)}, notifications {YesNo(application.Terminal.Notifications.IsSupported)}";
    }

    /// <summary>Records live verification for one protocol.</summary>
    /// <param name="protocol">The verified protocol.</param>
    /// <param name="verification">The new verification state.</param>
    /// <param name="detail">The non-empty evidence explanation.</param>
    internal void SetVerification(TerminalProtocol protocol, VerificationState verification, string detail)
    {
        if (!_statuses.TryGetValue(protocol, out var status))
        {
            throw new ArgumentOutOfRangeException(nameof(protocol), protocol, "The protocol has no dashboard row.");
        }

        status.SetVerification(verification, detail);
        var selected = _list.SelectedIndex;
        RefreshItems(selectFirst: false);

        if (selected >= 0 && selected < _list.Items.Count)
        {
            _list.SelectedIndex = selected;
            ShowDetail((CapabilityStatus) _list.Items[selected]!);
        }
    }

    private void RefreshItems(bool selectFirst)
    {
        _list.Items = _statuses.Values.Cast<object?>().ToArray();

        if (selectFirst && _list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }

        SummaryChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnSelectionChanged(object? sender, ListSelectionChangedEventArgs eventArgs)
    {
        if (_list.SelectedItem is CapabilityStatus status)
        {
            ShowDetail(status);
        }
    }

    private void ShowDetail(CapabilityStatus status)
    {
        _detail.Content =
            $"<accent><b>{status.Descriptor.Label}</b></accent>\n" +
            $"Group: <info>{status.Descriptor.Group}</info>\n\n" +
            $"Detected: {SupportMarkup(status.Feature.State)}\n" +
            $"Evidence origin: <info>{status.Feature.Origin}</info>\n" +
            $"Authoritative for output: {YesNo(status.Feature.Authoritative)}\n\n" +
            $"Live check: {VerificationMarkup(status.Verification)}\n" +
            $"{status.VerificationDetail}\n\n" +
            status.Descriptor.Explanation;
    }

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
        VerificationState.Observed => "<info>Observed</info>",
        VerificationState.Passed => "<success>Passed</success>",
        VerificationState.Failed => "<error>Failed</error>",
        VerificationState.NotRun => "<d>Not run</d>",
        _ => throw new ArgumentOutOfRangeException(nameof(verification), verification, "The verification state is unknown.")
    };
}
