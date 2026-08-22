// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

using SharpVision.Text;

/// <summary>Holds one inline source's parsed display text and its markup style spans.</summary>
/// <remarks>
/// Literal sources - link text and code-block lines - carry an empty span array, which resolves every
/// character to the inherited face. That lets literal and markup content share one paint path.
/// </remarks>
internal readonly struct DocumentParsedRun
{
    /// <summary>Initializes a parsed run.</summary>
    /// <param name="display">The non-null visible text with markup tags removed.</param>
    /// <param name="spans">The non-null style spans tiling <paramref name="display"/>, possibly empty.</param>
    public DocumentParsedRun(string display, StyleSpan[] spans)
    {
        Debug.Assert(display is not null, "A parsed run always has display text.");
        Debug.Assert(spans is not null, "A parsed run always has a span array, even when empty.");

        Display = display;
        Spans = spans;
    }

    /// <summary>Gets the visible text with markup tags removed.</summary>
    public string Display { get; }

    /// <summary>Gets the style spans tiling <see cref="Display"/> in source order.</summary>
    public StyleSpan[] Spans { get; }
}
