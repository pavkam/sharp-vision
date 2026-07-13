// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Unicode;

/// <summary>Provides an allocation-free enumerable over borrowed UTF-16 text.</summary>
public readonly ref struct GraphemeEnumerable
{
    private readonly ReadOnlySpan<char> _value;

    /// <summary>Initializes an enumerable over a span borrowed for the enumeration.</summary>
    /// <param name="value">The borrowed UTF-16 text.</param>
    internal GraphemeEnumerable(ReadOnlySpan<char> value) => _value = value;

    /// <summary>Creates an enumerator positioned before the first cluster.</summary>
    /// <returns>The allocation-free cluster enumerator.</returns>
    public GraphemeEnumerator GetEnumerator() => new(_value);
}
