// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

/// <summary>Describes configured multiplexer topology and effective typed routing decisions.</summary>
[PublicAPI]
public sealed class TerminalRouteDiagnostics
{
    private readonly ReadOnlyCollection<MultiplexerKind> _layers;
    private readonly MultiplexingPolicy _policy;
    private readonly MultiplexerRoute? _route;

    /// <summary>Initializes diagnostics from an optional immutable multiplexer policy.</summary>
    /// <param name="policy">The detected or explicit policy, or null when no multiplexer was identified.</param>
    internal TerminalRouteDiagnostics(MultiplexingPolicy? policy)
    {
        _policy = policy ?? new MultiplexingPolicy([], outerProfile: null);
        _route = _policy.Layers.Count == 0 ? null : new MultiplexerRoute(_policy);
        _layers = Array.AsReadOnly(_policy.Layers.ToArray());
        OuterProfile = _policy.OuterProfile;
        Passthrough = _policy.Passthrough;
        ApprovedOperations = _policy.ApprovedOperations;
        MaxDepth = _policy.MaxDepth;
        MaxEnvelopeBytes = _policy.MaxEnvelopeBytes;
    }

    /// <summary>Gets the owned nearest-to-farthest multiplexer layers.</summary>
    public IReadOnlyList<MultiplexerKind> Layers => _layers;

    /// <summary>Gets the explicit outer profile, which environment detection never invents.</summary>
    public TerminalProfile? OuterProfile { get; }

    /// <summary>Gets the configured passthrough visibility mode.</summary>
    public PassthroughMode Passthrough { get; }

    /// <summary>Gets whether the originating pane is currently focused. Reflects live outer-terminal
    /// focus (via tmux <c>focus-events</c>), including tmux's own focus-in/focus-out notifications
    /// on an internal pane or window switch, not only a change of the outer terminal's OS-level
    /// focus - so in a split layout this tracks keyboard focus, not on-screen visibility, and a
    /// pane can still be rendered on screen while this reads false.</summary>
    public bool PaneVisible => _policy.PaneVisible;

    /// <summary>Gets the explicitly approved typed operation families.</summary>
    public MultiplexingOperation ApprovedOperations { get; }

    /// <summary>Gets the finite permitted routing depth.</summary>
    public int MaxDepth { get; }

    /// <summary>Gets the finite encoded-envelope byte bound.</summary>
    public int MaxEnvelopeBytes { get; }

    /// <summary>Gets whether every policy authorization required for passthrough is present.</summary>
    public bool IsActive => _policy.Active;

    /// <summary>Gets whether capability queries can traverse the configured route.</summary>
    public bool CanRouteCapabilityQueries => _route?.CanRouteCapabilityQueries == true;

    /// <summary>Gets whether clipboard strings can traverse the configured route.</summary>
    public bool CanRouteClipboard => _route?.CanRouteClipboard == true;

    /// <summary>Gets whether graphics strings can traverse the configured route.</summary>
    public bool CanRouteGraphics => _route?.CanRouteGraphics == true;

    /// <summary>Gets whether notification strings can traverse the configured route.</summary>
    public bool CanRouteNotifications => _route?.CanRouteNotifications == true;

    /// <summary>Gets whether title commands can traverse the configured route.</summary>
    public bool CanRouteTitle => _route?.CanRouteTitle == true;

    /// <summary>Gets whether bell commands can traverse the configured route.</summary>
    public bool CanRouteBell => _route?.CanRouteBell == true;

    /// <summary>Gets whether routed capability queries preserve string terminators.</summary>
    public bool SupportsStringTerminatedQueries =>
        CanRouteCapabilityQueries && _route!.SupportsStringTerminatedQueries;
}
