// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

using Capabilities;

/// <summary>Owns the bounded relevant live environment observed immediately before native lookup.</summary>
internal sealed class DescriptionEnvironment
{
    private readonly Dictionary<string, string?> _values;

    private DescriptionEnvironment(Dictionary<string, string?> values, int byteCount)
    {
        _values = values;
        ByteCount = byteCount;
    }

    /// <summary>Gets the owned UTF-8 byte count charged to the accepted snapshot budget.</summary>
    public int ByteCount { get; }

    /// <summary>Gets whether a matching inline TERMCAP value was present.</summary>
    public bool HasMatchingInlineTermcap(string terminalName)
    {
        var termcap = Value("TERMCAP");

        if (termcap is null || termcap.StartsWith('/'))
        {
            return false;
        }

        var separator = termcap.IndexOf(':');
        var names = separator < 0 ? termcap : termcap[..separator];
        return names.Split('|').Contains(terminalName, StringComparer.Ordinal);
    }

    /// <summary>Captures and validates only environment names consumed by ncurses lookup.</summary>
    public static bool TryCapture(
        Func<string, string?> read,
        string terminalName,
        DescriptionLimits limits,
        out DescriptionEnvironment? environment,
        out DescriptionDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentException.ThrowIfNullOrWhiteSpace(terminalName);
        ArgumentNullException.ThrowIfNull(limits);

        var values = new Dictionary<string, string?>(StringComparer.Ordinal);
        var bytes = Encoding.UTF8.GetByteCount(terminalName);

        foreach (var name in new[] { "TERMINFO", "HOME", "TERMINFO_DIRS", "TERMCAP", "TERMPATH" })
        {
            var value = read(name);
            values.Add(name, value);
            bytes = checked(bytes + Encoding.UTF8.GetByteCount(name));

            if (value is not null)
            {
                bytes = checked(bytes + Encoding.UTF8.GetByteCount(value));
            }
        }

        var termcapValid = ValidTermcap(values["TERMCAP"], limits, out var termcapLimit);

        if (bytes > limits.MaxDescriptionSnapshotBytes ||
            !ValidPath(values["HOME"], limits) ||
            !ValidTerminfo(values["TERMINFO"], limits) ||
            !ValidPathList(values["TERMINFO_DIRS"], limits) ||
            !ValidPathList(values["TERMPATH"], limits) ||
            !termcapValid)
        {
            environment = null;
            diagnostic = new DescriptionDiagnostic(
                termcapLimit ? DescriptionDiagnosticCode.TermcapLimit : DescriptionDiagnosticCode.EnvironmentLimit);
            return false;
        }

        environment = new DescriptionEnvironment(values, bytes);
        diagnostic = default;
        return true;
    }

    private string? Value(string name) => _values.GetValueOrDefault(name);

    private static bool ValidPath(string? value, DescriptionLimits limits) =>
        value is null || Encoding.UTF8.GetByteCount(value) <= limits.MaxDescriptionPathBytes;

    private static bool ValidTerminfo(string? value, DescriptionLimits limits) =>
        value is null || value.StartsWith("hex:", StringComparison.Ordinal) ||
        value.StartsWith("b64:", StringComparison.Ordinal) || ValidPath(value, limits);

    private static bool ValidPathList(string? value, DescriptionLimits limits)
    {
        if (value is null)
        {
            return true;
        }

        var paths = value.Split(':');
        return paths.Length <= limits.MaxDescriptionPathEntries &&
            paths.All(path => Encoding.UTF8.GetByteCount(path) <= limits.MaxDescriptionPathBytes);
    }

    private static bool ValidTermcap(string? value, DescriptionLimits limits, out bool termcapLimit)
    {
        termcapLimit = false;

        if (value is null)
        {
            return true;
        }

        if (value.StartsWith('/'))
        {
            return ValidPath(value, limits);
        }

        termcapLimit = Encoding.UTF8.GetByteCount(value) > limits.MaxTermcapVariableBytes;
        return !termcapLimit;
    }
}
