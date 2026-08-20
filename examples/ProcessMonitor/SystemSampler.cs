// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace ProcessMonitor;

/// <summary>Captures one whole-machine sample, branching between Linux's <c>/proc</c> filesystem
/// and macOS's command-line tools.</summary>
/// <remarks>
/// Linux publishes cumulative CPU tick counters and memory/load figures directly as text files
/// under <c>/proc</c>, so those samples never launch a process. macOS has no equivalent virtual
/// filesystem; <c>top -l 1 -n 0 -stats cpu</c> prints the same figures (already computed by the
/// kernel) in its fixed header, and <c>sysctl</c> supplies the two facts that header omits: exact
/// total memory and the boot timestamp <see cref="SystemSnapshot.Uptime"/> is measured from. This type is
/// stateful only for the Linux CPU split, which - like every <c>/proc/stat</c> consumer, including
/// <c>top</c> itself - reports a rolling percentage by diffing two consecutive absolute tick
/// counts rather than one instantaneous reading.
/// </remarks>
internal sealed class SystemSampler
{
    private static readonly string[] _memSizeArguments = ["-n", "hw.memsize"];
    private static readonly string[] _bootTimeArguments = ["-n", "kern.boottime"];
    private static readonly string[] _topArguments = ["-l", "1", "-n", "0", "-stats", "cpu"];

    private LinuxCpuTicks? _previousLinuxTicks;

    /// <summary>Captures one whole-machine sample for the running platform.</summary>
    /// <param name="cancellationToken">The token observed while any external tool runs.</param>
    /// <returns>The captured sample, or <see langword="null"/> when required data was unavailable.</returns>
    internal async Task<SystemSnapshot?> CaptureAsync(CancellationToken cancellationToken)
    {
        return OperatingSystem.IsLinux()
            ? await CaptureLinuxAsync(cancellationToken).ConfigureAwait(false)
            : await CaptureMacOsAsync(cancellationToken).ConfigureAwait(false);
    }

    #region Linux

    private async Task<SystemSnapshot?> CaptureLinuxAsync(CancellationToken cancellationToken)
    {
        var ticks = await ReadLinuxCpuTicksAsync(cancellationToken).ConfigureAwait(false);
        var memory = await ReadLinuxMemInfoAsync(cancellationToken).ConfigureAwait(false);
        var (loadAverage1, loadAverage5, loadAverage15) =
            await ReadLinuxLoadAverageAsync(cancellationToken).ConfigureAwait(false);
        var uptime = await ReadLinuxUptimeAsync(cancellationToken).ConfigureAwait(false);

        if (ticks is null || memory is null)
        {
            return null;
        }

        var previous = _previousLinuxTicks;
        _previousLinuxTicks = ticks;

        var (userPercent, systemPercent, idlePercent) = previous is null
            ? (0d, 0d, 100d)
            : ticks.Value.PercentSince(previous.Value);

        return new SystemSnapshot(
            userPercent,
            systemPercent,
            idlePercent,
            memory.Value.TotalKilobytes,
            memory.Value.ToCategories(),
            loadAverage1,
            loadAverage5,
            loadAverage15,
            uptime);
    }

    private static async Task<LinuxCpuTicks?> ReadLinuxCpuTicksAsync(CancellationToken cancellationToken)
    {
        var line = await ReadFirstLineAsync("/proc/stat", cancellationToken).ConfigureAwait(false);

        if (line is not { Length: > 0 })
        {
            return null;
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // parts[0] is the literal "cpu" label; the aggregate line always reports at least the
        // first four jiffie counters (user, nice, system, idle) even on kernels too old for the
        // later iowait/irq/softirq/steal/guest columns this monitor does not otherwise need.
        return parts.Length < 5 ||
            !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var user) ||
            !long.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nice) ||
            !long.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var system) ||
            !long.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var idle)
            ? null
            : new LinuxCpuTicks(user + nice, system, idle);
    }

    private static async Task<LinuxMemory?> ReadLinuxMemInfoAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists("/proc/meminfo"))
        {
            return null;
        }

        long? total = null;
        long? free = null;
        long? buffers = null;
        long? cached = null;

        await foreach (var line in File.ReadLinesAsync("/proc/meminfo", cancellationToken).ConfigureAwait(false))
        {
            if (TryReadMemInfoField(line, "MemTotal:", out var value))
            {
                total = value;
            }
            else if (TryReadMemInfoField(line, "MemFree:", out value))
            {
                free = value;
            }
            else if (TryReadMemInfoField(line, "Buffers:", out value))
            {
                buffers = value;
            }
            else if (TryReadMemInfoField(line, "Cached:", out value))
            {
                cached = value;
            }
        }

        return total is null || free is null
            ? null
            : new LinuxMemory(total.Value, free.Value, buffers ?? 0, cached ?? 0);
    }

    private static bool TryReadMemInfoField(string line, string prefix, out long kilobytes)
    {
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            kilobytes = 0;
            return false;
        }

        var digits = line.AsSpan(prefix.Length).Trim();
        var unitIndex = digits.IndexOf(' ');

        if (unitIndex >= 0)
        {
            digits = digits[..unitIndex];
        }

        return long.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out kilobytes);
    }

    private static async Task<(double OneMinute, double FiveMinute, double FifteenMinute)> ReadLinuxLoadAverageAsync(
        CancellationToken cancellationToken)
    {
        var line = await ReadFirstLineAsync("/proc/loadavg", cancellationToken).ConfigureAwait(false);

        if (line is not { Length: > 0 })
        {
            return (0, 0, 0);
        }

        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 3 ||
            !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var one) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var five) ||
            !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var fifteen))
        {
            return (0, 0, 0);
        }

        return (one, five, fifteen);
    }

    private static async Task<TimeSpan> ReadLinuxUptimeAsync(CancellationToken cancellationToken)
    {
        var line = await ReadFirstLineAsync("/proc/uptime", cancellationToken).ConfigureAwait(false);

        if (line is not { Length: > 0 })
        {
            return TimeSpan.Zero;
        }

        var firstField = line.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var seconds, ..]
            ? seconds
            : null;

        return firstField is not null &&
            double.TryParse(firstField, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? TimeSpan.FromSeconds(value)
            : TimeSpan.Zero;
    }

    private static async Task<string?> ReadFirstLineAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        await foreach (var line in File.ReadLinesAsync(path, cancellationToken).ConfigureAwait(false))
        {
            return line;
        }

        return null;
    }

    private readonly struct LinuxCpuTicks
    {
        internal LinuxCpuTicks(long userAndNice, long system, long idle)
        {
            UserAndNice = userAndNice;
            System = system;
            Idle = idle;
        }

        internal long UserAndNice { get; }

        internal long System { get; }

        internal long Idle { get; }

        internal (double UserPercent, double SystemPercent, double IdlePercent) PercentSince(LinuxCpuTicks previous)
        {
            var deltaUser = UserAndNice - previous.UserAndNice;
            var deltaSystem = System - previous.System;
            var deltaIdle = Idle - previous.Idle;
            var deltaTotal = deltaUser + deltaSystem + deltaIdle;

            return deltaTotal <= 0
                ? (0, 0, 100)
                : (100d * deltaUser / deltaTotal, 100d * deltaSystem / deltaTotal, 100d * deltaIdle / deltaTotal);
        }
    }

    private readonly struct LinuxMemory
    {
        internal LinuxMemory(long totalKilobytes, long freeKilobytes, long buffersKilobytes, long cachedKilobytes)
        {
            TotalKilobytes = totalKilobytes;
            FreeKilobytes = freeKilobytes;
            BuffersKilobytes = buffersKilobytes;
            CachedKilobytes = cachedKilobytes;
        }

        internal long TotalKilobytes { get; }

        internal long FreeKilobytes { get; }

        internal long BuffersKilobytes { get; }

        internal long CachedKilobytes { get; }

        internal IReadOnlyList<MemoryCategory> ToCategories()
        {
            var buffersAndCache = BuffersKilobytes + CachedKilobytes;
            var used = Math.Max(0, TotalKilobytes - FreeKilobytes - buffersAndCache);

            return
            [
                new MemoryCategory("Used", used),
                new MemoryCategory("Buffers/Cache", buffersAndCache),
                new MemoryCategory("Free", Math.Max(0, FreeKilobytes))
            ];
        }
    }

    #endregion

    #region macOS

    private static async Task<SystemSnapshot?> CaptureMacOsAsync(CancellationToken cancellationToken)
    {
        var topTask = CommandRunner.CaptureOutputAsync("top", _topArguments, cancellationToken);
        var memSizeTask = CommandRunner.CaptureOutputAsync("sysctl", _memSizeArguments, cancellationToken);
        var bootTimeTask = CommandRunner.CaptureOutputAsync("sysctl", _bootTimeArguments, cancellationToken);

        var results = await Task.WhenAll(topTask, memSizeTask, bootTimeTask).ConfigureAwait(false);
        var top = results[0];

        if (top.Length == 0)
        {
            return null;
        }

        var (userPercent, systemPercent, idlePercent) = ParseTopCpuLine(top);
        var (loadAverage1, loadAverage5, loadAverage15) = ParseTopLoadAverageLine(top);
        var (usedKilobytes, wiredKilobytes, compressedKilobytes, unusedKilobytes) = ParseTopMemoryLine(top);

        var totalKilobytes = long.TryParse(
            results[1].Trim(),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var totalBytes)
            ? totalBytes / 1024
            : usedKilobytes + unusedKilobytes;

        var otherUsedKilobytes = Math.Max(0, usedKilobytes - wiredKilobytes - compressedKilobytes);
        var freeKilobytes = Math.Max(0, totalKilobytes - usedKilobytes);

        var categories = new MemoryCategory[]
        {
            new("Wired", wiredKilobytes),
            new("Compressed", compressedKilobytes),
            new("Other used", otherUsedKilobytes),
            new("Free", freeKilobytes)
        };

        var uptime = ParseBootTimeUptime(results[2]);

        return new SystemSnapshot(
            userPercent,
            systemPercent,
            idlePercent,
            totalKilobytes,
            categories,
            loadAverage1,
            loadAverage5,
            loadAverage15,
            uptime);
    }

    private static (double UserPercent, double SystemPercent, double IdlePercent) ParseTopCpuLine(string top)
    {
        var line = FindLine(top, "CPU usage:");

        if (line is null)
        {
            return (0, 0, 0);
        }

        // "CPU usage: 23.32% user, 19.15% sys, 57.52% idle"
        var numbers = ExtractPercentages(line, expectedCount: 3);
        return numbers.Count == 3 ? (numbers[0], numbers[1], numbers[2]) : (0, 0, 0);
    }

    private static (double OneMinute, double FiveMinute, double FifteenMinute) ParseTopLoadAverageLine(string top)
    {
        var line = FindLine(top, "Load Avg:");

        if (line is null)
        {
            return (0, 0, 0);
        }

        // "Load Avg: 8.13, 7.45, 7.58"
        var values = line["Load Avg:".Length..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static token => double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                ? value
                : (double?) null)
            .Where(static value => value.HasValue)
            .Select(static value => value!.Value)
            .ToList();

        return values.Count == 3 ? (values[0], values[1], values[2]) : (0, 0, 0);
    }

    private static (long UsedKilobytes, long WiredKilobytes, long CompressedKilobytes, long UnusedKilobytes) ParseTopMemoryLine(string top)
    {
        var line = FindLine(top, "PhysMem:");

        if (line is null)
        {
            return (0, 0, 0, 0);
        }

        // "PhysMem: 31G used (4421M wired, 8035M compressor), 190M unused."
        var used = ExtractKilobytesBefore(line, "used");
        var wired = ExtractKilobytesBefore(line, "wired");
        var compressed = ExtractKilobytesBefore(line, "compressor");
        var unused = ExtractKilobytesBefore(line, "unused");

        return (used, wired, compressed, unused);
    }

    private static TimeSpan ParseBootTimeUptime(string sysctlOutput)
    {
        // "{ sec = 1786310618, usec = 992444 } Sun Aug  9 22:23:38 2026"
        var marker = "sec = ";
        var startIndex = sysctlOutput.IndexOf(marker, StringComparison.Ordinal);

        if (startIndex < 0)
        {
            return TimeSpan.Zero;
        }

        startIndex += marker.Length;
        var endIndex = sysctlOutput.IndexOf(',', startIndex);

        if (endIndex < 0)
        {
            return TimeSpan.Zero;
        }

        var secondsText = sysctlOutput[startIndex..endIndex].Trim();

        if (!long.TryParse(secondsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bootEpochSeconds))
        {
            return TimeSpan.Zero;
        }

        var bootTime = DateTimeOffset.FromUnixTimeSeconds(bootEpochSeconds);
        var uptime = DateTimeOffset.UtcNow - bootTime;
        return uptime > TimeSpan.Zero ? uptime : TimeSpan.Zero;
    }

    private static string? FindLine(string text, string prefix)
    {
        string? match = null;

        foreach (var range in text.AsSpan().Split('\n'))
        {
            var candidate = text.AsSpan(range).Trim();

            if (candidate.StartsWith(prefix, StringComparison.Ordinal))
            {
                // top -l 1 prints this header exactly once, but the same parser also handles a
                // -l N>1 capture by keeping the last occurrence - the most recent interval sample
                // rather than the first, cumulative-since-launch one.
                match = candidate.ToString();
            }
        }

        return match;
    }

    private static List<double> ExtractPercentages(string line, int expectedCount)
    {
        var results = new List<double>(expectedCount);
        var span = line.AsSpan();
        var index = 0;

        while (results.Count < expectedCount)
        {
            var percentIndex = span[index..].IndexOf('%');

            if (percentIndex < 0)
            {
                break;
            }

            percentIndex += index;
            var start = percentIndex;

            while (start > 0 && (char.IsDigit(span[start - 1]) || span[start - 1] == '.'))
            {
                start--;
            }

            if (double.TryParse(span[start..percentIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                results.Add(value);
            }

            index = percentIndex + 1;
        }

        return results;
    }

    private static long ExtractKilobytesBefore(string line, string word)
    {
        var wordIndex = line.IndexOf(word, StringComparison.Ordinal);

        if (wordIndex < 0)
        {
            return 0;
        }

        var span = line.AsSpan(0, wordIndex).TrimEnd();
        var end = span.Length;
        var start = end;

        while (start > 0 && (char.IsDigit(span[start - 1]) || span[start - 1] == '.' || char.IsLetter(span[start - 1])))
        {
            start--;
        }

        return ParseHumanSizeToKilobytes(span[start..end]);
    }

    private static long ParseHumanSizeToKilobytes(ReadOnlySpan<char> token)
    {
        if (token.Length == 0)
        {
            return 0;
        }

        var unit = char.ToUpperInvariant(token[^1]);
        var hasUnit = unit is 'K' or 'M' or 'G' or 'T';
        var numberPart = hasUnit ? token[..^1] : token;

        if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        var kilobytes = unit switch
        {
            'K' => value,
            'M' => value * 1024,
            'G' => value * 1024 * 1024,
            'T' => value * 1024 * 1024 * 1024,
            _ => value / 1024 // bare byte count, the rare fallback when top reports no suffix
        };

        return (long) Math.Round(kilobytes, MidpointRounding.AwayFromZero);
    }

    #endregion
}
