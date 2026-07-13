// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Reports one committed List selection delta.</summary>
public sealed class ListSelectionChangedEventArgs: EventArgs
{
    /// <summary>Initializes owned sorted added and removed index snapshots.</summary>
    /// <param name="addedIndexes">The non-negative committed additions.</param>
    /// <param name="removedIndexes">The non-negative committed removals.</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is negative.</exception>
    public ListSelectionChangedEventArgs(
        ReadOnlySpan<int> addedIndexes,
        ReadOnlySpan<int> removedIndexes)
    {
        Validate(addedIndexes, nameof(addedIndexes));
        Validate(removedIndexes, nameof(removedIndexes));
        AddedIndexes = addedIndexes.ToArray();
        RemovedIndexes = removedIndexes.ToArray();
    }

    /// <summary>Gets the owned sorted committed additions.</summary>
    public ReadOnlyMemory<int> AddedIndexes { get; }

    /// <summary>Gets the owned sorted committed removals.</summary>
    public ReadOnlyMemory<int> RemovedIndexes { get; }

    private static void Validate(ReadOnlySpan<int> values, string name)
    {
        foreach (int value in values)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(name, value, "Selection indexes cannot be negative.");
            }
        }
    }
}
