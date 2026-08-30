// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Owns the retained terminal capability and event diagnostic dashboard.</summary>
internal sealed class TerminalDebuggerScreen: Screen
{
    private readonly Text _identity;
    private readonly Text _summary;
    private readonly Text _status;
    private readonly OverviewPanel _overview;
    private readonly CapabilityDashboard _capabilities;
    private readonly DiscoveryPanel _discovery;
    private readonly RoutingPanel _routing;
    private readonly DiagnosticEventLog _eventLog;
    private readonly InputEventInspector _input;
    private readonly TerminalProbePanel _probes;
    private InputEventRecorder? _recorder;

    /// <summary>Initializes the retained dashboard shell.</summary>
    internal TerminalDebuggerScreen()
    {
        var header = BuildHeader(out _identity);
        _summary = new Text("<d>Waiting for terminal discovery…</d>") { Padding = new Thickness(1, 0) };
        _status = new Text("Ready") { Overflow = Overflow.Ellipsis };
        _overview = new OverviewPanel();
        _capabilities = new CapabilityDashboard();
        _capabilities.SummaryChanged += OnSummaryChanged;
        _eventLog = new DiagnosticEventLog();
        _input = new InputEventInspector(_eventLog);
        _probes = new TerminalProbePanel(_capabilities);
        _discovery = new DiscoveryPanel();
        _routing = new RoutingPanel();

        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HeaderOverflowPolicy = TabHeaderOverflowPolicy.Scroll
        };
        tabs.Items.Add(new TabItem { HeaderText = "&Overview", Content = _overview });
        tabs.Items.Add(new TabItem { HeaderText = "&Protocols", Content = _capabilities });
        tabs.Items.Add(new TabItem { HeaderText = "&Discovery", Content = _discovery });
        tabs.Items.Add(new TabItem { HeaderText = "&Routing", Content = _routing });
        tabs.Items.Add(new TabItem { HeaderText = "&Input events", Content = _input });
        tabs.Items.Add(new TabItem { HeaderText = "Pro&bes", Content = _probes });

        var statusBar = new StatusBar { Padding = new Thickness(1, 0) };
        statusBar.Items.Add(new StatusBarItem { Content = _status });
        statusBar.Items.Add(new StatusBarItem
        {
            Alignment = StatusBarItemAlignment.Right,
            Content = new Text("<info>Ctrl+Q</info> <d>Quit</d>")
        });

        var root = new Dock
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Dock.SetSide(header, DockSide.Top);
        Dock.SetSide(_summary, DockSide.Top);
        Dock.SetSide(statusBar, DockSide.Bottom);
        root.Children.Add(header);
        root.Children.Add(_summary);
        root.Children.Add(statusBar);
        root.Children.Add(tabs);

        InitializeContent(root);
        _ = AddHandler(Events.Key, OnKey, handledEventsToo: true);
    }

    private static Dock BuildHeader(out Text identity)
    {
        var title = new Text("<accent><b>SharpVision</b></accent> Terminal Debugger")
        {
            Padding = new Thickness(1, 0)
        };
        identity = new Text("Detecting…") { Padding = new Thickness(1, 0), TextAlignment = Alignment.End };
        var header = new Dock { HorizontalAlignment = HorizontalAlignment.Stretch, Height = Length.Cells(1) };
        Dock.SetSide(title, DockSide.Left);
        Dock.SetSide(identity, DockSide.Right);
        header.Children.Add(title);
        header.Children.Add(identity);
        return header;
    }

    /// <inheritdoc/>
    protected override void OnAttach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Theme = ThemeCatalog.Dark;
        CapabilityCatalog.Validate(application.Capabilities);
        _capabilities.Initialize(application);
        RefreshDiagnostics(application);
        _probes.Attach(application);
        _input.Attach(application);
        _recorder = new InputEventRecorder(_eventLog, _capabilities.SetVerification);
        _recorder.Attach(application, this);
        application.CapabilitiesChanged += OnCapabilitiesChanged;
        application.TerminalDiagnosticsChanged += OnTerminalDiagnosticsChanged;
        application.Resize += OnApplicationResize;
        UpdateResponsiveLayout(application.Size.Width);
        _identity.Content = $"{TextMarkup.Escape(application.Terminal.Description.Name)} · {application.Size.Width}×{application.Size.Height}";
    }

    private void OnSummaryChanged(object? sender, EventArgs eventArgs)
    {
        _summary.Content =
            $"<success>✓ {_capabilities.SupportedCount} detected</success>   " +
            $"<error>× {_capabilities.UnsupportedCount} unsupported</error>   " +
            $"<warning>? {_capabilities.UnknownCount} unknown</warning>   " +
            $"<info>● {_capabilities.VerifiedCount} verified</info>   " +
            $"<error>! {_capabilities.FailedCount} failed</error>";
    }

    private void OnCapabilitiesChanged(object? sender, CapabilitiesChangedEventArgs eventArgs)
    {
        if (Application is not { } application)
        {
            return;
        }

        _capabilities.UpdateCapabilities(application);
        _probes.RefreshAvailability(application);
        RefreshDiagnostics(application);
    }

    private void OnTerminalDiagnosticsChanged(object? sender, TerminalDiagnosticsChangedEventArgs eventArgs)
    {
        if (Application is { } application)
        {
            RefreshDiagnostics(application);
        }
    }

    private void RefreshDiagnostics(Application application)
    {
        _overview.Refresh(application);
        _discovery.Refresh(application.TerminalDiagnostics);
        _routing.Refresh(application.TerminalDiagnostics);
        _capabilities.RefreshEnvironment(application);
    }

    private void OnApplicationResize(object? sender, ResizeEventArgs eventArgs)
    {
        if (Application is { } application)
        {
            var width = eventArgs.Dimensions.Cells.Width;
            _identity.Content = $"{TextMarkup.Escape(application.Terminal.Description.Name)} · {width}×{eventArgs.Dimensions.Cells.Height}";
            _capabilities.RefreshEnvironment(application);
            _overview.Refresh(application);
            UpdateResponsiveLayout(width);
        }
    }

    private void UpdateResponsiveLayout(int width)
    {
        var isCompact = width < 100;
        _capabilities.SetCompact(isCompact);
        _input.SetCompact(isCompact);
    }

    private void OnKey(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.IsInitialKeyDown &&
            eventArgs.Stroke.Code == Code.Character &&
            eventArgs.Stroke.Character is { Value: 'q' or 'Q' } &&
            eventArgs.Stroke.Modifiers == Modifiers.Control)
        {
            Application?.Shutdown();
            eventArgs.IsHandled = true;
        }
    }

    /// <inheritdoc/>
    protected override void OnDispose()
    {
        _capabilities.SummaryChanged -= OnSummaryChanged;

        if (Application is { } application)
        {
            application.CapabilitiesChanged -= OnCapabilitiesChanged;
            application.TerminalDiagnosticsChanged -= OnTerminalDiagnosticsChanged;
            application.Resize -= OnApplicationResize;
        }

        _recorder?.Dispose();
        _recorder = null;
    }
}
