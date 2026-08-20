// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>One complete, whole-machine sample: CPU split, memory breakdown, load, and uptime.</summary>
public sealed class SystemSnapshot
{
    /// <summary>Initializes a fully-populated, immutable whole-machine sample.</summary>
    /// <param name="userPercent">The non-negative percent of all cores spent in user space.</param>
    /// <param name="systemPercent">The non-negative percent of all cores spent in kernel space.</param>
    /// <param name="idlePercent">The non-negative percent of all cores left idle.</param>
    /// <param name="totalMemoryKilobytes">The non-negative total physical memory, in kilobytes.</param>
    /// <param name="memoryCategories">The non-null, non-empty breakdown that sums to approximately
    /// <paramref name="totalMemoryKilobytes"/>.</param>
    /// <param name="loadAverage1">The non-negative one-minute load average.</param>
    /// <param name="loadAverage5">The non-negative five-minute load average.</param>
    /// <param name="loadAverage15">The non-negative fifteen-minute load average.</param>
    /// <param name="uptime">The non-negative time since the machine last booted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="memoryCategories"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="memoryCategories"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric argument is negative.</exception>
    public SystemSnapshot(
        double userPercent,
        double systemPercent,
        double idlePercent,
        long totalMemoryKilobytes,
        IReadOnlyList<MemoryCategory> memoryCategories,
        double loadAverage1,
        double loadAverage5,
        double loadAverage15,
        TimeSpan uptime)
    {
        ArgumentNullException.ThrowIfNull(memoryCategories);
        ArgumentOutOfRangeException.ThrowIfNegative(userPercent);
        ArgumentOutOfRangeException.ThrowIfNegative(systemPercent);
        ArgumentOutOfRangeException.ThrowIfNegative(idlePercent);
        ArgumentOutOfRangeException.ThrowIfNegative(totalMemoryKilobytes);
        ArgumentOutOfRangeException.ThrowIfNegative(loadAverage1);
        ArgumentOutOfRangeException.ThrowIfNegative(loadAverage5);
        ArgumentOutOfRangeException.ThrowIfNegative(loadAverage15);
        ArgumentOutOfRangeException.ThrowIfNegative(uptime.Ticks);

        if (memoryCategories.Count == 0)
        {
            throw new ArgumentException("At least one memory category is required.", nameof(memoryCategories));
        }

        UserPercent = userPercent;
        SystemPercent = systemPercent;
        IdlePercent = idlePercent;
        TotalMemoryKilobytes = totalMemoryKilobytes;
        MemoryCategories = memoryCategories;
        LoadAverage1 = loadAverage1;
        LoadAverage5 = loadAverage5;
        LoadAverage15 = loadAverage15;
        Uptime = uptime;
    }

    /// <summary>Gets the percent of all cores spent in user space.</summary>
    public double UserPercent { get; }

    /// <summary>Gets the percent of all cores spent in kernel space.</summary>
    public double SystemPercent { get; }

    /// <summary>Gets the percent of all cores left idle.</summary>
    public double IdlePercent { get; }

    /// <summary>Gets the total physical memory, in kilobytes.</summary>
    public long TotalMemoryKilobytes { get; }

    /// <summary>Gets the memory breakdown that sums to approximately <see cref="TotalMemoryKilobytes"/>.</summary>
    public IReadOnlyList<MemoryCategory> MemoryCategories { get; }

    /// <summary>Gets the one-minute load average.</summary>
    public double LoadAverage1 { get; }

    /// <summary>Gets the five-minute load average.</summary>
    public double LoadAverage5 { get; }

    /// <summary>Gets the fifteen-minute load average.</summary>
    public double LoadAverage15 { get; }

    /// <summary>Gets the time since the machine last booted.</summary>
    public TimeSpan Uptime { get; }
}
