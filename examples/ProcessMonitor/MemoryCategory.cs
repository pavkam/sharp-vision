// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>One named slice of physical memory, in kilobytes.</summary>
public sealed record MemoryCategory
{
    /// <summary>Initializes one named memory slice.</summary>
    /// <param name="name">The non-empty category label shown on the memory chart.</param>
    /// <param name="kilobytes">The non-negative size of this slice.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kilobytes"/> is negative.</exception>
    public MemoryCategory(string name, long kilobytes)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfNegative(kilobytes);

        Name = name;
        Kilobytes = kilobytes;
    }

    /// <summary>Gets the category label shown on the memory chart.</summary>
    public string Name { get; }

    /// <summary>Gets the size of this slice, in kilobytes.</summary>
    public long Kilobytes { get; }
}
