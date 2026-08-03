// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Multiplexing;

using Capabilities;

/// <summary>Owns explicit bounded authorization for one multiplexer route.</summary>
/// <remarks>
/// Environment discovery identifies only the nearest inner multiplexer. It
/// never establishes an outer terminal profile or enables passthrough.
/// </remarks>
[PublicAPI]
public sealed class MultiplexingPolicy
{
    private const int _maximumDepth = 16;
    private const int _maximumEnvelopeBytes = 16_777_216;
    private const MultiplexingOperation _allOperations = MultiplexingOperation.CapabilityQueries |
                                             MultiplexingOperation.Clipboard |
                                             MultiplexingOperation.Graphics;
    private readonly ReadOnlyCollection<MultiplexerKind> _layers;

    /// <summary>Initializes one explicit multiplexer routing policy.</summary>
    /// <param name="layers">Multiplexer layers ordered from nearest to farthest.</param>
    /// <param name="outerProfile">The explicit outer-terminal profile, or null.</param>
    /// <param name="passthrough">The configured passthrough visibility gate.</param>
    /// <param name="paneVisible">Whether the originating pane is visible.</param>
    /// <param name="approvedOperations">The finite typed operation families allowed to pass.</param>
    /// <param name="maxDepth">The positive finite route-depth bound.</param>
    /// <param name="maxEnvelopeBytes">The positive finite encoded-envelope byte bound.</param>
    /// <exception cref="ArgumentNullException"><paramref name="layers"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An enum or bound is invalid.</exception>
    /// <exception cref="ArgumentException">A layer is none or the route exceeds <paramref name="maxDepth"/>.</exception>
    public MultiplexingPolicy(
        IReadOnlyList<MultiplexerKind> layers,
        TerminalProfile? outerProfile,
        PassthroughMode passthrough = PassthroughMode.Disabled,
        bool paneVisible = true,
        MultiplexingOperation approvedOperations = MultiplexingOperation.None,
        int maxDepth = 4,
        int maxEnvelopeBytes = 1_048_576)
    {
        ArgumentNullException.ThrowIfNull(layers);

        if (!Enum.IsDefined(passthrough))
        {
            throw new ArgumentOutOfRangeException(nameof(passthrough), passthrough, "The passthrough mode is unknown.");
        }

        if ((approvedOperations & ~_allOperations) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(approvedOperations),
                approvedOperations,
                "The operation set contains an unknown family.");
        }

        if (maxDepth is <= 0 or > _maximumDepth)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "The maximum depth must be one through sixteen.");
        }

        if (maxEnvelopeBytes is <= 0 or > _maximumEnvelopeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxEnvelopeBytes),
                maxEnvelopeBytes,
                "The envelope bound must be positive and no greater than sixteen MiB.");
        }

        if (layers.Count > maxDepth)
        {
            throw new ArgumentException("The multiplexer route exceeds its maximum depth.", nameof(layers));
        }

        var copy = new MultiplexerKind[layers.Count];

        for (var index = 0; index < copy.Length; index++)
        {
            var layer = layers[index];

            if (layer is MultiplexerKind.None || !Enum.IsDefined(layer))
            {
                throw new ArgumentException("Every route layer must identify tmux or GNU screen.", nameof(layers));
            }

            copy[index] = layer;
        }

        _layers = Array.AsReadOnly(copy);
        OuterProfile = outerProfile;
        Passthrough = passthrough;
        PaneVisible = paneVisible;
        ApprovedOperations = approvedOperations;
        MaxDepth = maxDepth;
        MaxEnvelopeBytes = maxEnvelopeBytes;
    }

    /// <summary>Gets the detected or explicit nearest multiplexer kind.</summary>
    public MultiplexerKind Kind => _layers.Count == 0 ? MultiplexerKind.None : _layers[0];

    /// <summary>Gets the owned nearest-to-farthest route.</summary>
    public IReadOnlyList<MultiplexerKind> Layers => _layers;

    /// <summary>Gets the explicit outer-terminal profile, separate from the active inner profile.</summary>
    public TerminalProfile? OuterProfile { get; }

    /// <summary>Gets the configured passthrough visibility gate.</summary>
    public PassthroughMode Passthrough { get; }

    /// <summary>Gets whether the originating pane is visible.</summary>
    public bool PaneVisible { get; }

    /// <summary>Gets the approved typed operation families.</summary>
    public MultiplexingOperation ApprovedOperations { get; }

    /// <summary>Gets the maximum accepted nesting depth.</summary>
    public int MaxDepth { get; }

    /// <summary>Gets the maximum encoded envelope bytes.</summary>
    public int MaxEnvelopeBytes { get; }

    /// <summary>Gets whether explicit evidence permits any passthrough operation.</summary>
    public bool IsActive => _layers.Count > 0 &&
                            OuterProfile is not null &&
                            ApprovedOperations != MultiplexingOperation.None &&
                            (Passthrough == PassthroughMode.All ||
                             (Passthrough == PassthroughMode.Visible && PaneVisible));

    /// <summary>Gets whether the configured route contains one GNU screen layer.</summary>
    internal bool ContainsScreen => _layers.Contains(MultiplexerKind.Screen);

    /// <summary>
    /// Gets whether Screen, when present, is the farthest layer and therefore
    /// receives the original typed query rather than another DCS envelope.
    /// </summary>
    internal bool HasSafeScreenTopology
    {
        get
        {
            var found = false;

            for (var index = 0; index < _layers.Count; index++)
            {
                if (_layers[index] != MultiplexerKind.Screen)
                {
                    continue;
                }

                if (found || index != _layers.Count - 1)
                {
                    return false;
                }

                found = true;
            }

            return true;
        }
    }

    /// <summary>Detects only the nearest multiplexer without enabling passthrough.</summary>
    /// <param name="environment">The non-null environment snapshot.</param>
    /// <returns>A conservative policy with no outer profile or approved operation.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    public static MultiplexingPolicy Detect(IReadOnlyDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _ = environment.TryGetValue(EnvironmentNames.Term, out var term);
        var kind = environment.TryGetValue(EnvironmentNames.Tmux, out var tmux) && !string.IsNullOrWhiteSpace(tmux)
            ? MultiplexerKind.Tmux
            : StartsWith(term, "tmux-")
                ? MultiplexerKind.Tmux
                : StartsWith(term, "screen-") || string.Equals(term, "screen", StringComparison.OrdinalIgnoreCase)
                    ? MultiplexerKind.Screen
                    : MultiplexerKind.None;

        return new MultiplexingPolicy(kind == MultiplexerKind.None ? [] : [kind], outerProfile: null);
    }

    /// <summary>Returns whether one exact typed operation family is authorized.</summary>
    /// <param name="operation">One defined non-composite operation family.</param>
    /// <returns>True when routing is active and the family is approved.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operation"/> is none, composite, or unknown.</exception>
    public bool Allows(MultiplexingOperation operation)
    {
        return operation switch
        {
            MultiplexingOperation.None => throw InvalidOperation(operation),
            MultiplexingOperation.CapabilityQueries =>
                IsActive && (ApprovedOperations & operation) == operation,
            MultiplexingOperation.Clipboard =>
                IsActive && (ApprovedOperations & operation) == operation,
            MultiplexingOperation.Graphics =>
                IsActive && (ApprovedOperations & operation) == operation,
            _ => throw InvalidOperation(operation)
        };
    }

    private static ArgumentOutOfRangeException InvalidOperation(MultiplexingOperation operation) => new(
        nameof(operation),
        operation,
        "One typed operation family is required.");

    private static bool StartsWith(string? value, string prefix) =>
        value?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true;
}
