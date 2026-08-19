// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Graphics;

using MustUseReturnValue = JetBrains.Annotations.MustUseReturnValueAttribute;

/// <summary>Owns a finite process-local set of nonzero protocol identifiers.</summary>
internal sealed class KittyGraphicsIdentifierAllocator
{
    private readonly bool[] _active;
    private int _cursor = 1;

    /// <summary>Initializes a finite identifier range from one through capacity.</summary>
    /// <param name="capacity">The positive maximum simultaneously active identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
    public KittyGraphicsIdentifierAllocator(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _active = new bool[checked(capacity + 1)];
    }

    /// <summary>Rents one inactive nonzero identifier without allocation.</summary>
    /// <returns>The newly active identifier.</returns>
    /// <exception cref="InvalidOperationException">Every bounded identifier is active.</exception>
    [MustUseReturnValue(
        "Discarding the rented identifier leaks it: Return requires the exact value Rent returned.")]
    public uint Rent()
    {
        for (var count = 0; count < _active.Length - 1; count++)
        {
            var candidate = _cursor;
            _cursor = candidate == _active.Length - 1 ? 1 : candidate + 1;

            if (!_active[candidate])
            {
                _active[candidate] = true;
                return (uint) candidate;
            }
        }

        throw new InvalidOperationException("The bounded Kitty identifier space is exhausted.");
    }

    /// <summary>Returns one active identifier for deterministic reuse.</summary>
    /// <param name="value">The active identifier.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not active in this allocator.</exception>
    public void Return(uint value)
    {
        if (value == 0 || value >= _active.Length || !_active[value])
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The Kitty identifier is not active.");
        }

        _active[value] = false;
        _cursor = Math.Min(_cursor, (int) value);
    }
}
