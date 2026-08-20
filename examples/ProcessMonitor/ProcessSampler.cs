// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>Captures the complete live process table through <c>ps</c>.</summary>
/// <remarks>
/// One <c>ps -A -ww -o ...</c> invocation returns every field this application needs - PID, parent
/// PID, owner, CPU and memory percent, resident and virtual size, state, elapsed time, and the full
/// command line - already computed by the kernel's own accounting, on both GNU/Linux's procps and
/// macOS's BSD <c>ps</c>. No platform branch is needed here, unlike <see cref="SystemSampler"/>,
/// because both implementations accept the same POSIX-style <c>-o field=,field=,...</c> syntax with
/// a trailing bare <c>=</c> suppressing each column's header.
/// </remarks>
internal static class ProcessSampler
{
    private static readonly string[] _arguments =
    [
        "-A",
        "-ww",
        "-o",
        "pid=,ppid=,pcpu=,pmem=,rss=,vsz=,stat=,user=,etime=,args="
    ];

    /// <summary>Captures every currently-visible process.</summary>
    /// <param name="cancellationToken">The token observed while <c>ps</c> runs.</param>
    /// <returns>One sample per process, in the kernel's own reported order.</returns>
    internal static async Task<IReadOnlyList<ProcessSample>> CaptureAsync(CancellationToken cancellationToken)
    {
        var output = await CommandRunner.CaptureOutputAsync("ps", _arguments, cancellationToken).ConfigureAwait(false);

        if (output.Length == 0)
        {
            return [];
        }

        var samples = new List<ProcessSample>();

        foreach (var range in output.AsSpan().Split('\n'))
        {
            var line = output.AsSpan(range);

            if (TryParseLine(line, out var sample))
            {
                samples.Add(sample);
            }
        }

        return samples;
    }

    // Every fixed-width field (pid, ppid, pcpu, pmem, rss, vsz, stat, user, etime) is guaranteed to
    // contain no whitespace by ps's own output contract, but the final field - the full command
    // line - both contains spaces between its own arguments and is the only field ps ever
    // truncates or omits, so it is read as "everything remaining on the line" rather than as one
    // more whitespace-delimited token.
    private static bool TryParseLine(ReadOnlySpan<char> line, out ProcessSample sample)
    {
        Span<Range> fields = stackalloc Range[9];
        var count = 0;
        var index = 0;

        while (count < fields.Length && index < line.Length)
        {
            while (index < line.Length && char.IsWhiteSpace(line[index]))
            {
                index++;
            }

            if (index >= line.Length)
            {
                break;
            }

            var start = index;

            while (index < line.Length && !char.IsWhiteSpace(line[index]))
            {
                index++;
            }

            fields[count++] = new Range(start, index);
        }

        if (count < fields.Length)
        {
            sample = null!;
            return false;
        }

        while (index < line.Length && char.IsWhiteSpace(line[index]))
        {
            index++;
        }

        if (!int.TryParse(line[fields[0]], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid) ||
            !int.TryParse(line[fields[1]], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parentPid) ||
            !double.TryParse(line[fields[2]], NumberStyles.Float, CultureInfo.InvariantCulture, out var cpuPercent) ||
            !double.TryParse(line[fields[3]], NumberStyles.Float, CultureInfo.InvariantCulture, out var memoryPercent) ||
            !long.TryParse(line[fields[4]], NumberStyles.Integer, CultureInfo.InvariantCulture, out var residentKilobytes) ||
            !long.TryParse(line[fields[5]], NumberStyles.Integer, CultureInfo.InvariantCulture, out var virtualKilobytes))
        {
            sample = null!;
            return false;
        }

        var state = line[fields[6]].ToString();
        var user = line[fields[7]].ToString();
        var elapsed = line[fields[8]].ToString();
        var command = index < line.Length ? line[index..].ToString() : string.Empty;

        sample = new ProcessSample(
            pid,
            parentPid,
            user,
            state,
            cpuPercent,
            memoryPercent,
            residentKilobytes,
            virtualKilobytes,
            elapsed,
            command);
        return true;
    }
}
