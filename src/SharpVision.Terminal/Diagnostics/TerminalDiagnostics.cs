// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

/// <summary>Publishes one immutable, typed, and redacted terminal runtime diagnostic snapshot.</summary>
[PublicAPI]
public sealed class TerminalDiagnostics
{
    private readonly ReadOnlyCollection<TerminalBackendEvidence> _backendEvidence;
    private readonly ReadOnlyCollection<TerminalProtocolExtension> _backendExtensions;

    /// <summary>Initializes one complete diagnostic snapshot.</summary>
    /// <param name="backendFamily">The fixed canonical terminal family.</param>
    /// <param name="backendName">The non-blank display name for that family.</param>
    /// <param name="backendEvidence">Typed redacted identity evidence copied by this snapshot.</param>
    /// <param name="backendExtensions">Protocol-extension composition copied by this snapshot.</param>
    /// <param name="negotiationState">Current bounded negotiation state.</param>
    /// <param name="queryResults">Final owned query results only when negotiation completed.</param>
    /// <param name="route">The non-null multiplexer route diagnostics.</param>
    /// <param name="modes">The non-null mode diagnostics.</param>
    /// <param name="graphicsBackend">The selected graphics backend.</param>
    /// <exception cref="ArgumentNullException">A required reference is null.</exception>
    /// <exception cref="ArgumentException">A name is blank, a collection is invalid, or query results contradict negotiation state.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum value is undefined.</exception>
    internal TerminalDiagnostics(
        TerminalBackendFamily backendFamily,
        string backendName,
        IReadOnlyList<TerminalBackendEvidence> backendEvidence,
        IReadOnlyList<TerminalProtocolExtension> backendExtensions,
        TerminalNegotiationState negotiationState,
        TerminalQueryDiagnostics? queryResults,
        TerminalRouteDiagnostics route,
        TerminalModeDiagnostics modes,
        TerminalGraphicsBackend graphicsBackend)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(backendFamily, nameof(backendFamily), "The terminal backend family is unknown.");
        ArgumentNullException.ThrowIfNull(backendName);
        ArgumentNullException.ThrowIfNull(backendEvidence);
        ArgumentNullException.ThrowIfNull(backendExtensions);
        ArgumentOutOfRangeException.ThrowIfNotDefined(negotiationState, nameof(negotiationState), "The negotiation state is unknown.");
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(modes);
        ArgumentOutOfRangeException.ThrowIfNotDefined(graphicsBackend, nameof(graphicsBackend), "The graphics backend is unknown.");

        if (string.IsNullOrWhiteSpace(backendName))
        {
            throw new ArgumentException("The terminal backend name must not be blank.", nameof(backendName));
        }

        var completed = negotiationState == TerminalNegotiationState.Completed;

        if (completed != (queryResults is not null))
        {
            throw new ArgumentException("Query results must be present exactly when negotiation is complete.", nameof(queryResults));
        }

        var evidence = backendEvidence.ToArray();
        var extensions = backendExtensions.ToArray();

        if (extensions.Length == 0)
        {
            throw new ArgumentException("At least one backend protocol extension is required.", nameof(backendExtensions));
        }

        foreach (var extension in extensions)
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(extension, nameof(backendExtensions), "A backend protocol extension is unknown.");
        }

        BackendFamily = backendFamily;
        BackendName = backendName;
        _backendEvidence = Array.AsReadOnly(evidence);
        _backendExtensions = Array.AsReadOnly(extensions);
        NegotiationState = negotiationState;
        QueryResults = queryResults;
        Route = route;
        Modes = modes;
        GraphicsBackend = graphicsBackend;
    }

    /// <summary>Gets the fixed canonical terminal family.</summary>
    public TerminalBackendFamily BackendFamily { get; }

    /// <summary>Gets the fixed canonical terminal backend display name.</summary>
    public string BackendName { get; }

    /// <summary>Gets the owned ordered redacted backend-identity evidence.</summary>
    public IReadOnlyList<TerminalBackendEvidence> BackendEvidence => _backendEvidence;

    /// <summary>Gets the owned inherited-before-local protocol-extension composition.</summary>
    public IReadOnlyList<TerminalProtocolExtension> BackendExtensions => _backendExtensions;

    /// <summary>Gets current bounded startup-negotiation state.</summary>
    public TerminalNegotiationState NegotiationState { get; }

    /// <summary>Gets final owned query results, or null until negotiation completes or when disabled.</summary>
    public TerminalQueryDiagnostics? QueryResults { get; }

    /// <summary>Gets immutable multiplexer topology and effective route decisions.</summary>
    public TerminalRouteDiagnostics Route { get; }

    /// <summary>Gets configured and evidence-authorized terminal modes.</summary>
    public TerminalModeDiagnostics Modes { get; }

    /// <summary>Gets the selected renderer graphics backend.</summary>
    public TerminalGraphicsBackend GraphicsBackend { get; }

    /// <summary>Creates a snapshot with final negotiation and capability evidence.</summary>
    /// <param name="state">The replacement negotiation state.</param>
    /// <param name="queryResults">Final results when completed, otherwise null.</param>
    /// <param name="capabilities">The non-null capability evidence used to recompute effective modes.</param>
    /// <returns>A new snapshot preserving fixed identity and route state.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="capabilities"/> is null.</exception>
    /// <exception cref="ArgumentException">The results contradict <paramref name="state"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="state"/> is undefined.</exception>
    internal TerminalDiagnostics WithNegotiation(
        TerminalNegotiationState state,
        QueryResults? queryResults,
        TerminalCapabilities capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);

        return new TerminalDiagnostics(
            BackendFamily,
            BackendName,
            BackendEvidence,
            BackendExtensions,
            state,
            queryResults is null ? null : new TerminalQueryDiagnostics(queryResults),
            Route,
            Modes.WithCapabilities(capabilities),
            GraphicsBackend);
    }

    /// <summary>Creates a snapshot with successfully activated terminal modes.</summary>
    /// <param name="modes">The non-null replacement mode snapshot.</param>
    /// <returns>A new snapshot preserving terminal identity, query facts, and routing.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="modes"/> is null.</exception>
    internal TerminalDiagnostics WithModes(TerminalModeDiagnostics modes)
    {
        ArgumentNullException.ThrowIfNull(modes);

        return new TerminalDiagnostics(
            BackendFamily,
            BackendName,
            BackendEvidence,
            BackendExtensions,
            NegotiationState,
            QueryResults,
            Route,
            modes,
            GraphicsBackend);
    }

    /// <summary>Creates a snapshot with a renderer-selected graphics backend.</summary>
    /// <param name="graphicsBackend">The selected backend.</param>
    /// <returns>A new snapshot preserving all terminal-session facts.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="graphicsBackend"/> is undefined.</exception>
    public TerminalDiagnostics WithGraphicsBackend(TerminalGraphicsBackend graphicsBackend) => new(
        BackendFamily,
        BackendName,
        BackendEvidence,
        BackendExtensions,
        NegotiationState,
        QueryResults,
        Route,
        Modes,
        graphicsBackend);
}
