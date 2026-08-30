// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace TerminalDebugger;

/// <summary>Displays multiplexer topology, authorization, bounds, and effective routes.</summary>
internal sealed class RoutingPanel: CompositeControlBase
{
    private readonly Text _content;

    /// <summary>Initializes the scrollable routing report.</summary>
    internal RoutingPanel()
    {
        _content = new Text("<d>Waiting for routing diagnostics…</d>") { Overflow = Overflow.Wrap };
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

    /// <summary>Refreshes the route report from one immutable snapshot.</summary>
    /// <param name="diagnostics">The non-null terminal diagnostics.</param>
    internal void Refresh(TerminalDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        var route = diagnostics.Route;
        var layers = route.Layers.Count == 0 ? "none" : string.Join(" → ", route.Layers);
        var outer = route.OuterProfile is { } profile
            ? $"{TextMarkup.Escape(profile.Description.Name)} ({profile.Description.Origin})"
            : "none; not inferred from environment";

        _content.Content =
            $"<accent><b>Multiplexer topology</b></accent>\n" +
            $"Layers, nearest first: <info>{layers}</info>\n" +
            $"Outer profile: {outer}\n" +
            $"Passthrough: {route.Passthrough}; pane visible: {route.PaneVisible}\n" +
            $"Approved operations: {route.ApprovedOperations}\n" +
            $"Route active: {YesNo(route.IsActive)}\n" +
            $"Bounds: depth {route.MaxDepth}; envelope {route.MaxEnvelopeBytes:N0} bytes\n\n" +
            $"<accent><b>Effective decisions</b></accent>\n" +
            $"Capability queries: {Allowed(route.CanRouteCapabilityQueries)}\n" +
            $"String-terminated queries preserved: {Allowed(route.SupportsStringTerminatedQueries)}\n" +
            $"Clipboard: {Allowed(route.CanRouteClipboard)}\n" +
            $"Graphics: {Allowed(route.CanRouteGraphics)}\n\n" +
            (route.Layers.Count > 0 && !route.IsActive
                ? "<warning>A multiplexer was detected, but passthrough remains deliberately blocked until an outer profile and explicit operation authorization are supplied.</warning>"
                : "<d>Environment detection never invents outer-terminal identity or authorization.</d>");
    }

    private static string YesNo(bool value) => value ? "<success>yes</success>" : "<d>no</d>";

    private static string Allowed(bool value) => value ? "<success>routed</success>" : "<warning>not routed</warning>";
}
