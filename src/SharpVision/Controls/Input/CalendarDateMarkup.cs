// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Input;

using Text;

/// <summary>Owns one parsed sparse calendar face and its immutable style spans.</summary>
internal sealed class CalendarDateMarkup
{
    /// <summary>Parses one non-empty authored face before it enters calendar state.</summary>
    /// <param name="source">The non-null authored markup.</param>
    /// <exception cref="ArgumentException">The markup produces no visible text.</exception>
    public CalendarDateMarkup(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Spans = source.Parse(out var display);

        if (display.Length == 0)
        {
            throw new ArgumentException("Calendar markup must produce visible text.", nameof(source));
        }

        Source = source;
        Display = display;
    }

    /// <summary>Gets the exact authored markup.</summary>
    public string Source { get; }

    /// <summary>Gets the parsed visible face.</summary>
    public string Display { get; }

    /// <summary>Gets the non-overlapping parsed style spans.</summary>
    public IReadOnlyList<StyleSpan> Spans { get; }

    /// <summary>Gets the style span containing one visible UTF-16 offset.</summary>
    /// <param name="offset">The visible UTF-16 offset.</param>
    /// <returns>The containing style span, or null when the face inherits.</returns>
    [Pure]
    public StyleSpan? SpanAt(int offset)
    {
        foreach (var span in Spans)
        {
            if (offset >= span.Offset && offset < span.Offset + span.Length)
            {
                return span;
            }
        }

        return null;
    }
}
