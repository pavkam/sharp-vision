// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>Owns the retained Process Monitor dashboard: live CPU and memory charts, a process
/// tree, a selected-process detail panel, and the background sampling loop that feeds them.</summary>
public sealed class MonitorScreen: Screen
{
    private static readonly TimeSpan _refreshInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _killConfirmationWindow = TimeSpan.FromSeconds(5);
    private const int _maxHistoryPoints = 40;
    private const int _maxProcessCpuHistoryPoints = 30;

    private readonly SystemSampler _systemSampler = new();
    private readonly HashSet<int> _collapsedPids = [];
    private readonly Dictionary<TreeViewItem, int> _pidByItem = [];
    private readonly Queue<double> _selectedProcessCpuHistory = [];
    private readonly ChartSeries _cpuUserSeries = new("User");
    private readonly ChartSeries _cpuSystemSeries = new("System");
    private readonly ChartSeries _memorySeries = new("Memory");
    private readonly ChartSeries _selectedProcessCpuSeries = new("CPU");

    private readonly Text _headerRightText;
    private readonly Text _cpuSummaryText;
    private readonly Text _memorySummaryText;
    private readonly Text _infoBarText;
    private readonly Text _detailsHeaderText;
    private readonly Text _detailsBodyText;
    private readonly Text _detailsChildrenText;
    private readonly Sparkline _detailsCpuSparkline;
    private readonly Text _statusMessageText;
    private readonly TreeView _tree;

    private CancellationTokenSource? _refreshLoopCts;
    private Task? _refreshLoopTask;
    private bool _refreshInFlight;
    private bool _manualRefreshRequested;
    private int? _selectedPid;
    private int? _trackedCpuHistoryPid;
    private Dictionary<int, ProcessSample> _lastSamplesByPid = [];
    private int? _pendingKillPid;
    private DateTimeOffset _pendingKillExpiresAt;

    /// <summary>Initializes the full retained dashboard layout.</summary>
    public MonitorScreen()
    {
        _cpuSystemSeries.Color = new ControlColor(SemanticColor.Warning);

        var header = BuildHeader(out _headerRightText);
        var dashboard = BuildDashboard(out _cpuSummaryText, out _memorySummaryText);
        _infoBarText = new Text { Padding = new Thickness(1, 0) };
        _tree = BuildTree();
        _tree.SelectionChanged += OnTreeSelectionChanged;
        _tree.ItemInvoked += OnTreeItemInvoked;

        var detailsPanel = BuildDetailsPanel(
            out _detailsHeaderText,
            out _detailsBodyText,
            out _detailsChildrenText,
            out _detailsCpuSparkline);

        var processesGroup = new GroupBox
        {
            HeaderText = "Processes",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = _tree
        };

        var middle = new Dock { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        Dock.SetSide(detailsPanel, DockSide.Right);
        middle.Children.Add(detailsPanel);
        middle.Children.Add(processesGroup);

        var statusBar = BuildStatusBar(out _statusMessageText);

        var root = new Dock { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        Dock.SetSide(header, DockSide.Top);
        Dock.SetSide(dashboard, DockSide.Top);
        Dock.SetSide(_infoBarText, DockSide.Top);
        Dock.SetSide(statusBar, DockSide.Bottom);
        root.Children.Add(header);
        root.Children.Add(dashboard);
        root.Children.Add(_infoBarText);
        root.Children.Add(statusBar);
        root.Children.Add(middle);

        InitializeContent(root);

        _ = AddHandler(Events.Key, OnKey);
        ShowStatus("Ready. Press ? for a reminder of the keys below.", StatusSeverity.Muted);
    }

    #region Layout construction

    private static Dock BuildHeader(out Text rightText)
    {
        var left = new Text("<accent><b>SharpVision</b></accent> Process Monitor") { Padding = new Thickness(1, 0) };
        rightText = new Text { Padding = new Thickness(1, 0), TextAlignment = Alignment.End };

        var header = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(1)
        };
        Dock.SetSide(left, DockSide.Left);
        Dock.SetSide(rightText, DockSide.Right);
        header.Children.Add(left);
        header.Children.Add(rightText);
        return header;
    }

    private Grid BuildDashboard(out Text cpuSummaryText, out Text memorySummaryText)
    {
        cpuSummaryText = new Text { Padding = new Thickness(1, 0) };
        memorySummaryText = new Text { Padding = new Thickness(1, 0) };

        var cpuChart = new LineChart
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LegendPlacement = ChartLegendPlacement.Bottom,
            ShowCategoryLabels = false,
            Scale = new ChartScale(0, 100, includeZero: true),
            Series = [_cpuUserSeries, _cpuSystemSeries]
        };

        var cpuStack = new Stack { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        cpuStack.Children.Add(cpuSummaryText);
        cpuStack.Children.Add(cpuChart);

        var cpuGroup = new GroupBox
        {
            HeaderText = "CPU",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = cpuStack
        };

        var memoryChart = new VerticalBarChart
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LegendPlacement = ChartLegendPlacement.Hidden,
            Series = [_memorySeries]
        };

        var memoryStack = new Stack { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        memoryStack.Children.Add(memorySummaryText);
        memoryStack.Children.Add(memoryChart);

        var memoryGroup = new GroupBox
        {
            HeaderText = "Memory",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = memoryStack
        };

        var dashboard = new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = Length.Cells(14),
            ColumnSpacing = 1
        };
        dashboard.Columns.Add(Track.Star(1));
        dashboard.Columns.Add(Track.Star(1));
        Grid.SetColumn(cpuGroup, 0);
        Grid.SetColumn(memoryGroup, 1);
        dashboard.Children.Add(cpuGroup);
        dashboard.Children.Add(memoryGroup);
        return dashboard;
    }

    private static TreeView BuildTree() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    private static Dock BuildDetailsPanel(
        out Text headerText,
        out Text bodyText,
        out Text childrenText,
        out Sparkline cpuSparkline)
    {
        headerText = new Text { Overflow = Overflow.Wrap };
        bodyText = new Text { Overflow = Overflow.Wrap };
        childrenText = new Text { Overflow = Overflow.Wrap };
        cpuSparkline = new Sparkline { HorizontalAlignment = HorizontalAlignment.Stretch, Height = Length.Cells(3) };

        var stack = new Stack
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(1, 0),
            Spacing = 1
        };
        stack.Children.Add(headerText);
        stack.Children.Add(bodyText);
        stack.Children.Add(new Text("<d>Recent CPU</d>"));
        stack.Children.Add(cpuSparkline);
        stack.Children.Add(childrenText);

        var group = new GroupBox
        {
            HeaderText = "Details",
            Width = Length.Cells(40),
            VerticalAlignment = VerticalAlignment.Stretch,
            Content = stack
        };

        return new Dock { Width = Length.Cells(40), VerticalAlignment = VerticalAlignment.Stretch, Children = { group } };
    }

    private static StatusBar BuildStatusBar(out Text messageText)
    {
        messageText = new Text();

        var hints = new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = new Text(
                "<info>↑↓</info> <d>Navigate</d>   " +
                "<info>Enter</info> <d>Toggle</d>   " +
                "<info>K</info> <d>Kill</d>   " +
                "<info>R</info> <d>Refresh</d>   " +
                "<info>Ctrl+Q</info> <d>Quit</d>")
        };

        var statusBar = new StatusBar { Padding = new Thickness(1, 0) };
        statusBar.Items.Add(new StatusBarItem { Content = messageText });
        statusBar.Items.Add(hints);
        return statusBar;
    }

    #endregion

    #region Lifecycle

    /// <inheritdoc/>
    protected override void OnAttach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Theme = ThemeCatalog.Dark;

        if (application.Terminal.IsTitleSupported)
        {
            application.Terminal.SetTitle("Process Monitor");
        }
    }

    /// <inheritdoc/>
    protected override void OnStarted(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _ = application.Focus.Focus(_tree);
        StartRefreshLoop();
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        StopRefreshLoop();
        _tree.SelectionChanged -= OnTreeSelectionChanged;
        _tree.ItemInvoked -= OnTreeItemInvoked;
    }

    #endregion

    #region Refresh loop

    private void StartRefreshLoop()
    {
        StopRefreshLoop();

        var cts = new CancellationTokenSource();
        _refreshLoopCts = cts;
        _refreshLoopTask = Task.Run(() => RefreshLoopAsync(cts.Token));
    }

    private void StopRefreshLoop()
    {
        var cts = _refreshLoopCts;
        var task = _refreshLoopTask;
        _refreshLoopCts = null;
        _refreshLoopTask = null;

        if (cts is null)
        {
            Debug.Assert(task is null, "A refresh-loop task must have an owning cancellation source.");
            return;
        }

        try
        {
            cts.Cancel();

            try
            {
                task?.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
            }
        }
        finally
        {
            cts.Dispose();
        }
    }

    private async Task RefreshLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await CaptureAndApplyAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                await Task.Delay(_refreshInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task CaptureAndApplyAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ProcessSample> processes;
        SystemSnapshot? system;

        try
        {
            var processesTask = ProcessSampler.CaptureAsync(cancellationToken);
            var systemTask = _systemSampler.CaptureAsync(cancellationToken);
            await Task.WhenAll(processesTask, systemTask).ConfigureAwait(false);
            processes = processesTask.Result;
            system = systemTask.Result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var application = Application;

        if (application is null || cancellationToken.IsCancellationRequested)
        {
            return;
        }

        try
        {
            application.Dispatcher.Post(() =>
            {
                if (cancellationToken.IsCancellationRequested || IsDisposed)
                {
                    return;
                }

                ApplySnapshot(processes, system);
            });
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    #endregion

    #region Applying one sample

    private void ApplySnapshot(IReadOnlyList<ProcessSample> processes, SystemSnapshot? system)
    {
        _lastSamplesByPid = processes.ToDictionary(static sample => sample.Pid);
        _headerRightText.Content = DateTimeOffset.Now.ToString("ddd HH:mm:ss", CultureInfo.CurrentCulture);

        if (system is not null)
        {
            ApplySystemSnapshot(system);
        }

        ApplyProcessCounts(processes);
        RebuildTree(processes);
        UpdateSelectedProcessDetails();
        ExpirePendingKillIfNeeded();
    }

    private void ApplySystemSnapshot(SystemSnapshot system)
    {
        AppendHistoryPoint(_cpuUserSeries, system.UserPercent);
        AppendHistoryPoint(_cpuSystemSeries, system.SystemPercent);

        var usedPercent = system.UserPercent + system.SystemPercent;
        _cpuSummaryText.Content = FormattableString.Invariant(
            $"<b>{usedPercent:F1}%</b> used  <d>({system.UserPercent:F1}% user, {system.SystemPercent:F1}% sys)</d>");

        _memorySeries.Points.Clear();

        foreach (var category in system.MemoryCategories)
        {
            _memorySeries.Points.Add(new ChartDataPoint(category.Name, category.Kilobytes / 1024d));
        }

        var usedKilobytes = system.MemoryCategories
            .Where(static category => category.Name != "Free")
            .Sum(static category => category.Kilobytes);

        _memorySummaryText.Content = FormattableString.Invariant(
            $"<b>{FormatMegabytesAsGigabytes(usedKilobytes)}</b> / {FormatMegabytesAsGigabytes(system.TotalMemoryKilobytes)} used");

        _infoBarText.Content = FormattableString.Invariant(
            $"Load average: <b>{system.LoadAverage1:F2} {system.LoadAverage5:F2} {system.LoadAverage15:F2}</b>   Uptime: <b>{FormatUptime(system.Uptime)}</b>");
    }

    private void ApplyProcessCounts(IReadOnlyList<ProcessSample> processes)
    {
        var running = processes.Count(static sample => sample.State.Length > 0 && sample.State[0] == 'R');
        var sleeping = processes.Count(static sample => sample.State.Length > 0 && sample.State[0] is 'S' or 'I');
        var zombies = processes.Count(static sample => sample.State.Length > 0 && sample.State[0] == 'Z');

        var suffix = zombies > 0 ? FormattableString.Invariant($", <error>{zombies} zombie</error>") : string.Empty;
        _infoBarText.Content += FormattableString.Invariant(
            $"   Processes: <b>{processes.Count}</b> ({running} running, {sleeping} sleeping{suffix})");
    }

    private static void AppendHistoryPoint(ChartSeries series, double value)
    {
        var label = DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
        series.Points.Add(new ChartDataPoint(label, Math.Round(value, 1, MidpointRounding.AwayFromZero)));

        while (series.Points.Count > _maxHistoryPoints)
        {
            series.Points.RemoveAt(0);
        }
    }

    #endregion

    #region Process tree

    private void RebuildTree(IReadOnlyList<ProcessSample> processes)
    {
        // Captured before any mutation: clearing Items (or EndUpdate flushing that clear) fires
        // SelectionChanged with a null selection once the previously-selected item is removed,
        // which would otherwise wipe the very field this method reads to restore it afterward.
        var previousSelectedPid = _selectedPid;

        var roots = ProcessTreeBuilder.Build(processes);
        var newItemsByPid = new Dictionary<int, TreeViewItem>(processes.Count);

        _tree.BeginUpdate();

        try
        {
            _tree.Items.Clear();
            _pidByItem.Clear();

            foreach (var root in roots)
            {
                _tree.Items.Add(BuildTreeItem(root, newItemsByPid));
            }
        }
        finally
        {
            _tree.EndUpdate();
        }

        if (previousSelectedPid is { } selectedPid && newItemsByPid.TryGetValue(selectedPid, out var selectedItem))
        {
            _tree.SelectedItem = selectedItem;
        }
    }

    private TreeViewItem BuildTreeItem(ProcessNode node, Dictionary<int, TreeViewItem> itemsByPid)
    {
        var sample = node.Sample;
        var item = new TreeViewItem(FormatProcessHeader(sample))
        {
            IsExpanded = !_collapsedPids.Contains(sample.Pid)
        };
        item.Face = item.Face with { Foreground = new ControlColor(SeverityPalette.ForPercent(sample.CpuPercent, highThreshold: 50, mediumThreshold: 20)) };
        item.ExpandedChanged += (_, args) => OnItemExpandedChanged(sample.Pid, args.IsExpanded);

        itemsByPid[sample.Pid] = item;
        _pidByItem[item] = sample.Pid;

        foreach (var child in node.Children)
        {
            item.Children.Add(BuildTreeItem(child, itemsByPid));
        }

        return item;
    }

    private static string FormatProcessHeader(ProcessSample sample) => FormattableString.Invariant(
        $"{sample.Pid,-7} {sample.CpuPercent,5:F1}% {sample.MemoryPercent,5:F1}%  {sample.DisplayName}");

    private void OnItemExpandedChanged(int pid, bool isExpanded) =>
        _ = isExpanded ? _collapsedPids.Remove(pid) : _collapsedPids.Add(pid);

    private static void OnTreeItemInvoked(object? sender, TreeViewItemInvokedEventArgs e)
    {
        _ = sender;

        if (e.Item.HasChildren)
        {
            e.Item.IsExpanded = !e.Item.IsExpanded;
        }
    }

    private void OnTreeSelectionChanged(object? sender, TreeViewSelectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _selectedPid = _tree.SelectedItem is { } item && _pidByItem.TryGetValue(item, out var pid) ? pid : null;

        if (_selectedPid != _trackedCpuHistoryPid)
        {
            _selectedProcessCpuHistory.Clear();
            _selectedProcessCpuSeries.Points.Clear();
            _trackedCpuHistoryPid = _selectedPid;
        }

        UpdateSelectedProcessDetails();
    }

    #endregion

    #region Details panel

    private void UpdateSelectedProcessDetails()
    {
        if (_selectedPid is not { } pid || !_lastSamplesByPid.TryGetValue(pid, out var sample))
        {
            _detailsHeaderText.Content = "<d>Select a process to see its details.</d>";
            _detailsBodyText.Content = string.Empty;
            _detailsChildrenText.Content = string.Empty;
            _selectedProcessCpuSeries.Points.Clear();
            return;
        }

        _detailsHeaderText.Content = FormattableString.Invariant($"<accent><b>{Text.Escape(sample.DisplayName)}</b></accent>  <d>PID {sample.Pid}</d>");

        _detailsBodyText.Content = FormattableString.Invariant(
            $"""
            Parent PID: {sample.ParentPid}
            User: {Text.Escape(sample.User)}
            State: {sample.StateDescription} <d>({Text.Escape(sample.State)})</d>
            CPU: {sample.CpuPercent:F1}%   Memory: {sample.MemoryPercent:F1}%
            Resident: {FormatKilobytes(sample.ResidentKilobytes)}   Virtual: {FormatKilobytes(sample.VirtualKilobytes)}
            Elapsed: {Text.Escape(sample.Elapsed)}

            <d>{Text.Escape(TruncateCommand(sample.Command))}</d>
            """);

        _selectedProcessCpuHistory.Enqueue(sample.CpuPercent);

        while (_selectedProcessCpuHistory.Count > _maxProcessCpuHistoryPoints)
        {
            _ = _selectedProcessCpuHistory.Dequeue();
        }

        _selectedProcessCpuSeries.Points.Clear();
        var index = 0;

        foreach (var value in _selectedProcessCpuHistory)
        {
            _selectedProcessCpuSeries.Points.Add(new ChartDataPoint(index.ToString(CultureInfo.InvariantCulture), value));
            index++;
        }

        if (_detailsCpuSparkline.Series.Count == 0)
        {
            _detailsCpuSparkline.Series = [_selectedProcessCpuSeries];
        }

        _detailsChildrenText.Content = _pendingKillPid == sample.Pid
            ? FormattableString.Invariant($"<error><b>Press Enter to terminate PID {sample.Pid}. Press Esc to cancel.</b></error>")
            : string.Empty;
    }

    private static string TruncateCommand(string command)
    {
        const int MaxLength = 200;
        return command.Length == 0
            ? "(command line unavailable)"
            : command.Length <= MaxLength ? command : string.Concat(command.AsSpan(0, MaxLength), "…");
    }

    private static string FormatKilobytes(long kilobytes) => kilobytes >= 1024 * 1024
        ? FormattableString.Invariant($"{kilobytes / (1024d * 1024):F2} GB")
        : FormattableString.Invariant($"{kilobytes / 1024d:F1} MB");

    private static string FormatMegabytesAsGigabytes(long kilobytes) => FormattableString.Invariant($"{kilobytes / (1024d * 1024):F1} GB");

    private static string FormatUptime(TimeSpan uptime) => uptime switch
    {
        { TotalDays: >= 1 } => FormattableString.Invariant($"{(int) uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"),
        { TotalHours: >= 1 } => FormattableString.Invariant($"{(int) uptime.TotalHours}h {uptime.Minutes}m"),
        _ => FormattableString.Invariant($"{uptime.Minutes}m {uptime.Seconds}s")
    };

    #endregion

    #region Input and process control

    private void OnKey(object? sender, KeyEventArgs e)
    {
        _ = sender;

        if (e.IsHandled || e.Stroke.Action != KeyAction.Press)
        {
            return;
        }

        if ((e.Stroke.Modifiers & Modifiers.Control) != 0 &&
            e.Stroke is { Code: Code.Character, Character: { } controlCharacter } &&
            Rune.ToLowerInvariant(controlCharacter) == new Rune('q'))
        {
            Application?.Closed();
            e.IsHandled = true;
            return;
        }

        if (e.Stroke.Code == Code.Escape && _pendingKillPid is not null)
        {
            CancelPendingKill();
            e.IsHandled = true;
            return;
        }

        if (e.Stroke.Code == Code.Enter && _pendingKillPid is { } armedPid)
        {
            ConfirmKill(armedPid);
            e.IsHandled = true;
            return;
        }

        if (e.Stroke.Code != Code.Character || e.Stroke.Character is not { } character)
        {
            return;
        }

        switch (Rune.ToLowerInvariant(character).Value)
        {
            case 'r':
                RequestImmediateRefresh();
                e.IsHandled = true;
                break;
            case 'k':
                ArmPendingKill();
                e.IsHandled = true;
                break;
            default:
                break;
        }
    }

    private void RequestImmediateRefresh()
    {
        if (_refreshInFlight)
        {
            _manualRefreshRequested = true;
            return;
        }

        _refreshInFlight = true;
        _ = Task.Run(async () =>
        {
            do
            {
                _manualRefreshRequested = false;
                await CaptureAndApplyAsync(CancellationToken.None).ConfigureAwait(false);
            }
            while (_manualRefreshRequested);

            _refreshInFlight = false;
        });
    }

    private void ArmPendingKill()
    {
        if (_selectedPid is not { } pid || !_lastSamplesByPid.TryGetValue(pid, out var sample))
        {
            ShowStatus("Select a process first.", StatusSeverity.Warning);
            return;
        }

        if (!string.Equals(sample.User, Environment.UserName, StringComparison.Ordinal))
        {
            ShowStatus(
                FormattableString.Invariant($"Cannot terminate PID {pid}: owned by {sample.User}, not you."),
                StatusSeverity.Warning);
            return;
        }

        _pendingKillPid = pid;
        _pendingKillExpiresAt = DateTimeOffset.UtcNow + _killConfirmationWindow;
        ShowStatus(
            FormattableString.Invariant($"Press Enter to terminate {sample.DisplayName} (PID {pid}). Press Esc to cancel."),
            StatusSeverity.Warning);
        UpdateSelectedProcessDetails();
    }

    private void CancelPendingKill()
    {
        _pendingKillPid = null;
        ShowStatus("Termination cancelled.", StatusSeverity.Muted);
        UpdateSelectedProcessDetails();
    }

    private void ConfirmKill(int pid)
    {
        _pendingKillPid = null;

        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
            ShowStatus(FormattableString.Invariant($"Sent termination signal to PID {pid}."), StatusSeverity.Success);
        }
        catch (ArgumentException)
        {
            ShowStatus(FormattableString.Invariant($"PID {pid} has already exited."), StatusSeverity.Muted);
        }
        catch (Exception exception) when (exception is InvalidOperationException or Win32Exception)
        {
            ShowStatus(FormattableString.Invariant($"Could not terminate PID {pid}: {exception.Message}"), StatusSeverity.Error);
        }

        UpdateSelectedProcessDetails();
    }

    private void ExpirePendingKillIfNeeded()
    {
        if (_pendingKillPid is not null && DateTimeOffset.UtcNow >= _pendingKillExpiresAt)
        {
            CancelPendingKill();
        }
    }

    private void ShowStatus(string message, StatusSeverity severity) =>
        _statusMessageText.Content = FormattableString.Invariant($"<{ColorTagFor(severity)}>{Text.Escape(message)}</>");

    private static string ColorTagFor(StatusSeverity severity) => severity switch
    {
        StatusSeverity.Success => "success",
        StatusSeverity.Warning => "warning",
        StatusSeverity.Error => "error",
        StatusSeverity.Muted => "d",
        StatusSeverity.Info => "info",
        _ => throw new UnreachableException()
    };

    #endregion
}
