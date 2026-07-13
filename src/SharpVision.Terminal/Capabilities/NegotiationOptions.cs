// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

using System.Collections.ObjectModel;

using SharpVision.Terminal.Protocols;

/// <summary>Owns finite policy and evidence inputs for one startup negotiation.</summary>
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
        Limits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(environment);
        Dictionary<string, string?> copy = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string?> pair in environment)
        {
            copy.Add(pair.Key, pair.Value);
        }

        Environment = new ReadOnlyDictionary<string, string?>(copy);
        Overrides = overrides;
        Limits = limits ?? Limits.Default;
    }

    /// <summary>Gets the owned environment snapshot.</summary>
    public IReadOnlyDictionary<string, string?> Environment { get; }

    /// <summary>Gets optional explicit final overrides.</summary>
    public Settings? Overrides { get; }

    /// <summary>Gets finite parser and query limits.</summary>
    public Limits Limits { get; }
}
