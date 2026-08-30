// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

/// <summary>Describes configured multiplexer topology and effective typed routing decisions.</summary>
[PublicAPI]
public sealed class TerminalRouteDiagnostics
{
    private readonly ReadOnlyCollection<MultiplexerKind> _layers;

    /// <summary>Initializes diagnostics from an optional immutable multiplexer policy.</summary>
    /// <param name="policy">The detected or explicit policy, or null when no multiplexer was identified.</param>
    internal TerminalRouteDiagnostics(MultiplexingPolicy? policy)
    {
        var resolved = policy ?? new MultiplexingPolicy([], outerProfile: null);
        var route = resolved.Layers.Count == 0 ? null : new MultiplexerRoute(resolved);
        _layers = Array.AsReadOnly(resolved.Layers.ToArray());
        OuterProfile = resolved.OuterProfile;
        Passthrough = resolved.Passthrough;
        PaneVisible = resolved.PaneVisible;
        ApprovedOperations = resolved.ApprovedOperations;
        MaxDepth = resolved.MaxDepth;
        MaxEnvelopeBytes = resolved.MaxEnvelopeBytes;
        IsActive = resolved.Active;
        CanRouteCapabilityQueries = route?.CanRouteCapabilityQueries == true;
        CanRouteClipboard = route?.CanRouteClipboard == true;
        CanRouteGraphics = route?.CanRouteGraphics == true;
        SupportsStringTerminatedQueries = CanRouteCapabilityQueries && route!.SupportsStringTerminatedQueries;
    }

    /// <summary>Gets the owned nearest-to-farthest multiplexer layers.</summary>
    public IReadOnlyList<MultiplexerKind> Layers => _layers;

    /// <summary>Gets the explicit outer profile, which environment detection never invents.</summary>
    public TerminalProfile? OuterProfile { get; }

    /// <summary>Gets the configured passthrough visibility mode.</summary>
    public PassthroughMode Passthrough { get; }

    /// <summary>Gets whether the originating pane was declared visible.</summary>
    public bool PaneVisible { get; }

    /// <summary>Gets the explicitly approved typed operation families.</summary>
    public MultiplexingOperation ApprovedOperations { get; }

    /// <summary>Gets the finite permitted routing depth.</summary>
    public int MaxDepth { get; }

    /// <summary>Gets the finite encoded-envelope byte bound.</summary>
    public int MaxEnvelopeBytes { get; }

    /// <summary>Gets whether every policy authorization required for passthrough is present.</summary>
    public bool IsActive { get; }

    /// <summary>Gets whether capability queries can traverse the configured route.</summary>
    public bool CanRouteCapabilityQueries { get; }

    /// <summary>Gets whether clipboard strings can traverse the configured route.</summary>
    public bool CanRouteClipboard { get; }

    /// <summary>Gets whether graphics strings can traverse the configured route.</summary>
    public bool CanRouteGraphics { get; }

    /// <summary>Gets whether routed capability queries preserve string terminators.</summary>
    public bool SupportsStringTerminatedQueries { get; }
}
