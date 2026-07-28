// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;

/// <summary>Provides an allocation-free damage enumerator over borrowed active frames.</summary>
[PublicAPI]
public readonly ref struct DamageEnumerable
{
    private readonly Frame? _front;
    private readonly Frame _back;
    private readonly bool _full;

    /// <summary>Initializes a borrowed damage enumerable.</summary>
    /// <param name="front">The optional committed frame.</param>
    /// <param name="back">The target frame.</param>
    /// <param name="full">Whether complete damage is required.</param>
    internal DamageEnumerable(Frame? front, Frame back, bool full)
    {
        _front = front;
        _back = back;
        _full = full;
    }

    /// <summary>Creates an enumerator positioned before the first changed run.</summary>
    /// <returns>The damage enumerator.</returns>
    public DamageEnumerator GetEnumerator() => new(_front, _back, _full);
}
