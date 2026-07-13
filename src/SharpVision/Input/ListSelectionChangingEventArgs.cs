// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.ComponentModel;

/// <summary>Provides a cancellable proposed List selection delta.</summary>
public sealed class ListSelectionChangingEventArgs: CancelEventArgs
{
    /// <summary>Initializes owned sorted added and removed index snapshots.</summary>
    /// <param name="addedIndexes">The non-negative indexes proposed for addition.</param>
    /// <param name="removedIndexes">The non-negative indexes proposed for removal.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is negative.</exception>
    public ListSelectionChangingEventArgs(
        ReadOnlySpan<int> addedIndexes,
        ReadOnlySpan<int> removedIndexes)
    {
        Validate(addedIndexes, nameof(addedIndexes));
        Validate(removedIndexes, nameof(removedIndexes));
        AddedIndexes = addedIndexes.ToArray();
        RemovedIndexes = removedIndexes.ToArray();
    }

    /// <summary>Gets the owned sorted indexes proposed for addition.</summary>
    public ReadOnlyMemory<int> AddedIndexes { get; }

    /// <summary>Gets the owned sorted indexes proposed for removal.</summary>
    public ReadOnlyMemory<int> RemovedIndexes { get; }

    private static void Validate(ReadOnlySpan<int> values, string name)
    {
        foreach (var value in values)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "Selection indexes cannot be negative.");
            }
        }
    }
}
