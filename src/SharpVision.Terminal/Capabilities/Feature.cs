// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Represents one optional feature and its evidence origin. Supported evidence
/// may come from a validated database, a bounded query, or an explicit caller
/// override; environment evidence remains tentative.
/// </summary>
[PublicAPI]
public readonly record struct Feature
{
    /// <summary>Initializes validated feature evidence.</summary>
    /// <param name="state">The support confidence.</param>
    /// <param name="origin">The evidence origin.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="state"/> or <paramref name="origin"/> is unknown.
    /// </exception>
    public Feature(CapabilitySupport state, Origin origin)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(state, nameof(state), "The support state is unknown.");

        ArgumentOutOfRangeException.ThrowIfNotDefined(origin, nameof(origin), "The evidence origin is unknown.");

        State = state;
        Origin = origin;
    }

    /// <summary>Gets a conservative unknown feature.</summary>
    public static Feature Unknown { get; } = new(CapabilitySupport.Unknown, Origin.Default);

    /// <summary>Gets the support confidence.</summary>
    public CapabilitySupport State { get; }

    /// <summary>Gets the evidence origin.</summary>
    public Origin Origin { get; }

    /// <summary>Gets whether safe behavior may actively use the feature.</summary>
    public bool Supported => State == CapabilitySupport.Supported;

    /// <summary>Gets whether this evidence authorizes emitting optional terminal output.</summary>
    /// <remarks>
    /// Support alone is not enough. <see cref="Origin.Default"/> and
    /// <see cref="Origin.Environment"/> are guesses — a terminal name or a defaulted profile — and
    /// a caller can construct a supported value from either. Only a terminal description, a
    /// correlated query reply, or an explicit caller override is authoritative, so only those may
    /// put optional bytes on the wire.
    /// </remarks>
    public bool Authoritative =>
        Supported && Origin is Origin.Database or Origin.Query or Origin.Override;

    /// <summary>Deconstructs the feature evidence.</summary>
    /// <param name="state">Receives the support confidence.</param>
    /// <param name="origin">Receives the evidence origin.</param>
    public void Deconstruct(out CapabilitySupport state, out Origin origin)
    {
        state = State;
        origin = Origin;
    }
}
