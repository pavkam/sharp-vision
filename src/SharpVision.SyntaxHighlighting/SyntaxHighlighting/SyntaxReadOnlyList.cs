// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

using System.Collections;

/// <summary>Owns an array copy exposed only through read-only enumeration and indexing.</summary>
/// <typeparam name="T">The element type.</typeparam>
internal sealed class SyntaxReadOnlyList<T>: IReadOnlyList<T>
{
    private readonly T[] _items;

    /// <summary>Initializes an owned snapshot of <paramref name="items"/>.</summary>
    /// <param name="items">The non-null values to copy.</param>
    public SyntaxReadOnlyList(IEnumerable<T> items) => _items = [.. items];

    /// <summary>Gets the shared empty snapshot.</summary>
    public static SyntaxReadOnlyList<T> Empty { get; } = new([]);

    /// <inheritdoc/>
    public int Count => _items.Length;

    /// <inheritdoc/>
    public T this[int index] => _items[index];

    /// <inheritdoc/>
    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>) _items).GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
