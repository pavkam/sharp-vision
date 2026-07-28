// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using MultiplexingPolicy = Multiplexing.Policy;

/// <summary>Owns finite policy and evidence inputs for one startup negotiation.</summary>
[PublicAPI]
public sealed class NegotiationOptions
{
    /// <summary>Initializes one immutable negotiation policy.</summary>
    /// <param name="environment">Caller-supplied terminal environment values.</param>
    /// <param name="overrides">Optional explicit final overrides.</param>
    /// <param name="limits">Finite protocol limits, or null for defaults.</param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    public NegotiationOptions(
        IReadOnlyDictionary<string, string?> environment,
        Settings? overrides = null,
        Limits? limits = null) : this(
        environment,
        overrides,
        limits,
        multiplexing: null)
    {
    }

    /// <summary>Initializes one immutable negotiation policy with explicit multiplexer routing.</summary>
    /// <param name="environment">Caller-supplied terminal environment values.</param>
    /// <param name="overrides">Optional explicit final overrides.</param>
    /// <param name="limits">Finite protocol limits, or null for defaults.</param>
    /// <param name="multiplexing">Explicit multiplexer policy, or null for conservative nearest-layer detection.</param>
    /// <exception cref="ArgumentNullException"><paramref name="environment"/> is null.</exception>
    public NegotiationOptions(
        IReadOnlyDictionary<string, string?> environment,
        Settings? overrides,
        Limits? limits,
        MultiplexingPolicy? multiplexing)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var copy = new Dictionary<string, string?>(StringComparer.Ordinal);

        foreach (var pair in environment)
        {
            copy.Add(pair.Key, pair.Value);
        }

        Environment = new ReadOnlyDictionary<string, string?>(copy);
        Overrides = overrides;
        Limits = limits ?? Limits.Default;
        Multiplexing = multiplexing ?? MultiplexingPolicy.Detect(Environment);
    }

    /// <summary>Gets the owned environment snapshot.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>Gets optional explicit final overrides.</summary>
    public Settings? Overrides { get; }

    /// <summary>Gets finite parser and query limits.</summary>
    public Limits Limits { get; }

    /// <summary>Gets explicit or conservatively detected multiplexer routing policy.</summary>
    public MultiplexingPolicy Multiplexing { get; }
}
