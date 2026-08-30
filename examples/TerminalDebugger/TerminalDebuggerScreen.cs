// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Owns the retained terminal capability and event diagnostic dashboard.</summary>
internal sealed class TerminalDebuggerScreen: Screen
{
    private readonly Text _identity;
    private readonly Text _summary;
    private readonly Text _status;

    /// <summary>Initializes the retained dashboard shell.</summary>
    internal TerminalDebuggerScreen()
    {
        var header = BuildHeader(out _identity);
        _summary = new Text("<d>Waiting for terminal discovery…</d>") { Padding = new Thickness(1, 0) };
        _status = new Text("Ready") { Overflow = Overflow.Ellipsis };

        var tabs = new TabControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            HeaderOverflowPolicy = TabHeaderOverflowPolicy.Scroll
        };
        tabs.Items.Add(new TabItem { HeaderText = "&Capabilities", Content = BuildCapabilitiesPane() });
        tabs.Items.Add(new TabItem { HeaderText = "&Input events", Content = BuildInputPane() });
        tabs.Items.Add(new TabItem { HeaderText = "&Tests", Content = BuildTestsPane() });

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

    private static Text BuildCapabilitiesPane() => new Text(
        "<b>Detected evidence</b> and <b>live verification</b> will appear here.")
    {
        Padding = new Thickness(1),
        Overflow = Overflow.Wrap
    };

    private static Text BuildInputPane() => new Text(
        "Keyboard, text, pointer, paste, focus, resize, and clipboard events will appear here.")
    {
        Padding = new Thickness(1),
        Overflow = Overflow.Wrap
    };

    private static Text BuildTestsPane() => new Text(
        "All side-effecting checks are explicit. Nothing runs merely because this tab exists.")
    {
        Padding = new Thickness(1),
        Overflow = Overflow.Wrap
    };

    /// <inheritdoc/>
    protected override void OnAttach(Application application)
    {
        ArgumentNullException.ThrowIfNull(application);
        application.Theme = ThemeCatalog.Dark;
        CapabilityCatalog.Validate(application.Capabilities);
        _identity.Content = $"{application.Terminal.Description.Name} · {application.Size.Width}×{application.Size.Height}";
        _summary.Content = $"<info>{application.Capabilities.ColorDepth}</info> color · Unicode {application.Capabilities.UnicodeVersion} · {application.Capabilities.Features.Count} optional protocols";

        if (application.Terminal.IsTitleSupported)
        {
            application.Terminal.SetTitle("SharpVision Terminal Debugger");
        }
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
}
