// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Text;

/// <summary>Provides the reusable parser and escaper for SharpVision's inline text markup.</summary>
[PublicAPI]
public static class TextMarkup
{
    /// <summary>Parses lenient inline markup without discarding malformed source fragments.</summary>
    /// <param name="source">The source markup.</param>
    /// <param name="display">The visible text with valid tags removed and escapes resolved.</param>
    /// <returns>Positive semantic spans that tile the complete visible text in source order.</returns>
    [Pure]
    public static StyleSpan[] Parse(ReadOnlySpan<char> source, out string display) => source.Parse(out display);

    /// <summary>Escapes visible text so markup metacharacters round-trip literally.</summary>
    /// <param name="value">The non-null visible text.</param>
    /// <returns>A string with backslash and opening-angle characters escaped.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    [Pure]
    public static string Escape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.Escape();
    }
}
