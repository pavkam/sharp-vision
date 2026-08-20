// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>One process's complete snapshot from a single <c>ps</c> sample.</summary>
public sealed class ProcessSample
{
    /// <summary>Initializes a fully-populated, immutable snapshot for one process.</summary>
    /// <param name="pid">The non-negative process ID.</param>
    /// <param name="parentPid">The non-negative parent process ID.</param>
    /// <param name="user">The non-null owning user name.</param>
    /// <param name="state">The non-null raw <c>ps</c> state code (e.g. <c>"S"</c>, <c>"R+"</c>).</param>
    /// <param name="cpuPercent">The non-negative percent of one core currently attributed to this process.</param>
    /// <param name="memoryPercent">The non-negative percent of physical memory this process occupies.</param>
    /// <param name="residentKilobytes">The non-negative resident set size, in kilobytes.</param>
    /// <param name="virtualKilobytes">The non-negative virtual memory size, in kilobytes.</param>
    /// <param name="elapsed">The non-null raw <c>ps</c> elapsed-time text (e.g. <c>"03:12:07"</c>).</param>
    /// <param name="command">The non-null full command line, or empty when unavailable.</param>
    /// <exception cref="ArgumentNullException"><paramref name="user"/>, <paramref name="state"/>,
    /// <paramref name="elapsed"/>, or <paramref name="command"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric argument is negative.</exception>
    public ProcessSample(
        int pid,
        int parentPid,
        string user,
        string state,
        double cpuPercent,
        double memoryPercent,
        long residentKilobytes,
        long virtualKilobytes,
        string elapsed,
        string command)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(elapsed);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentOutOfRangeException.ThrowIfNegative(pid);
        ArgumentOutOfRangeException.ThrowIfNegative(parentPid);
        ArgumentOutOfRangeException.ThrowIfNegative(cpuPercent);
        ArgumentOutOfRangeException.ThrowIfNegative(memoryPercent);
        ArgumentOutOfRangeException.ThrowIfNegative(residentKilobytes);
        ArgumentOutOfRangeException.ThrowIfNegative(virtualKilobytes);

        Pid = pid;
        ParentPid = parentPid;
        User = user;
        State = state;
        CpuPercent = cpuPercent;
        MemoryPercent = memoryPercent;
        ResidentKilobytes = residentKilobytes;
        VirtualKilobytes = virtualKilobytes;
        Elapsed = elapsed;
        Command = command;
        DisplayName = ResolveDisplayName(command);
    }

    /// <summary>Gets the process ID.</summary>
    public int Pid { get; }

    /// <summary>Gets the parent process ID.</summary>
    public int ParentPid { get; }

    /// <summary>Gets the owning user name.</summary>
    public string User { get; }

    /// <summary>Gets the raw <c>ps</c> state code (e.g. <c>"S"</c>, <c>"R+"</c>, <c>"Z"</c>).</summary>
    public string State { get; }

    /// <summary>Gets the percent of one core currently attributed to this process.</summary>
    public double CpuPercent { get; }

    /// <summary>Gets the percent of physical memory this process occupies.</summary>
    public double MemoryPercent { get; }

    /// <summary>Gets the resident set size, in kilobytes.</summary>
    public long ResidentKilobytes { get; }

    /// <summary>Gets the virtual memory size, in kilobytes.</summary>
    public long VirtualKilobytes { get; }

    /// <summary>Gets the raw <c>ps</c> elapsed-time text.</summary>
    public string Elapsed { get; }

    /// <summary>Gets the full command line, or empty when the OS did not disclose it.</summary>
    public string Command { get; }

    /// <summary>Gets the short display name derived from <see cref="Command"/>.</summary>
    public string DisplayName { get; }

    /// <summary>Gets the human-readable state derived from the leading <see cref="State"/> letter.</summary>
    public string StateDescription => State.Length == 0
        ? "Unknown"
        : State[0] switch
        {
            'R' => "Running",
            'S' => "Sleeping",
            'I' => "Idle",
            'D' or 'U' => "Waiting on I/O",
            'T' => "Stopped",
            'Z' => "Zombie",
            _ => "Unknown"
        };

    private static string ResolveDisplayName(string command)
    {
        if (command.Length == 0)
        {
            return "(unknown)";
        }

        var spaceIndex = command.IndexOf(' ', StringComparison.Ordinal);
        var firstToken = spaceIndex < 0 ? command : command[..spaceIndex];

        return firstToken.Length == 0 ? "(unknown)" : Path.GetFileName(firstToken);
    }
}
