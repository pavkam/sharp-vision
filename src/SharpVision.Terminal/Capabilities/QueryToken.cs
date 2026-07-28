// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Identifies one active query without exposing internal correlation state.</summary>
[PublicAPI]
public readonly record struct QueryToken
{
    /// <summary>Initializes a positive tracker-local token.</summary>
    /// <param name="value">The positive tracker-local value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public QueryToken(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive tracker-local value.</summary>
    public long Value { get; }

    /// <summary>Deconstructs the token.</summary>
    /// <param name="value">Receives the tracker-local value.</param>
    public void Deconstruct(out long value) => value = Value;
}
