// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Compiles and executes KDE regular expressions through the PCRE2 dialect used by
/// Qt's QRegularExpression, with one shared resource-limit policy.</summary>
internal static class SyntaxRegularExpression
{
    private static readonly PcreMatchSettings _matchSettings = new()
    {
        MatchLimit = 100_000,
        DepthLimit = 1_000,
        HeapLimit = 1_024,
    };

    /// <summary>Compiles a pattern, preserving KDE's case and inverted-greediness options. Invalid
    /// third-party patterns degrade to a never-match expression.</summary>
    internal static PcreRegex Compile(string pattern, bool insensitive, bool minimal) =>
        Compile(pattern, insensitive, minimal, out _);

    /// <summary>Compiles a pattern and reports whether the authored pattern itself was valid.</summary>
    internal static PcreRegex Compile(string pattern, bool insensitive, bool minimal, out bool isValid)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var options = PcreOptions.Utf |
            (insensitive ? PcreOptions.IgnoreCase : PcreOptions.None) |
            (minimal ? PcreOptions.Ungreedy : PcreOptions.None);

        try
        {
            var compiled = new PcreRegex(pattern, options);
            isValid = true;
            return compiled;
        }
        catch (PcrePatternException)
        {
            isValid = false;
            return new PcreRegex("(?!)", options);
        }
    }

    /// <summary>Matches from one UTF-16 offset with bounded backtracking, nesting, and heap use.</summary>
    internal static PcreMatch Match(PcreRegex regex, string line, int offset)
    {
        ArgumentNullException.ThrowIfNull(regex);
        ArgumentNullException.ThrowIfNull(line);

        return regex.Match(line, offset, PcreMatchOptions.None, null, _matchSettings);
    }

    /// <summary>Gets whether a match failure represents an exhausted resource budget.</summary>
    internal static bool IsBudgetExceeded(PcreMatchException error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return error.ErrorCode is PcreErrorCode.MatchLimit or PcreErrorCode.DepthLimit or PcreErrorCode.HeapLimit;
    }
}
