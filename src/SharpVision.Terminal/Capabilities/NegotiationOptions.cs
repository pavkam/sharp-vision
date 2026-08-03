// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

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
        CapabilityOverrides? overrides = null,
        QueryLimits? limits = null) : this(
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
        CapabilityOverrides? overrides,
        QueryLimits? limits,
        MultiplexingPolicy? multiplexing)
    {
        ArgumentNullException.ThrowIfNull(environment);

        // Canonicalize recognized keys through the caller's comparer. An ordinal copy alone would
        // keep a case-insensitive caller's lowercase entries while making every later canonical
        // lookup miss, so negotiation would publish weaker evidence than passive detection for the
        // same input.
        Environment = EnvironmentSnapshot.Create(environment);
        Overrides = overrides;
        Limits = limits ?? QueryLimits.Default;
        Multiplexing = multiplexing ?? MultiplexingPolicy.Detect(Environment);
    }

    /// <summary>Gets the owned environment snapshot.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>Gets optional explicit final overrides.</summary>
    public CapabilityOverrides? Overrides { get; }

    /// <summary>Gets finite query limits.</summary>
    public QueryLimits Limits { get; }

    /// <summary>Gets explicit or conservatively detected multiplexer routing policy.</summary>
    public MultiplexingPolicy Multiplexing { get; }
}
