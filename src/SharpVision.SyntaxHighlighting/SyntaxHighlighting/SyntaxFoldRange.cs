// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Represents one collapsible line range a tokenized document can fold away. The default
/// value is an empty indentation range at line zero.</summary>
[PublicAPI]
public readonly record struct SyntaxFoldRange
{
#pragma warning disable IDE0032 // Nullable storage distinguishes an uninitialized struct from an authored Region value.
    private readonly SyntaxFoldRangeKind? _kind;
#pragma warning restore IDE0032

    /// <summary>Initializes a fold range.</summary>
    /// <param name="startLine">The non-negative zero-based first line of the range.</param>
    /// <param name="endLine">The zero-based last line of the range, greater than or equal to <paramref name="startLine"/>.</param>
    /// <param name="kind">How this range was detected.</param>
    /// <param name="regionName">
    /// The shared <c>beginRegion</c>/<c>endRegion</c> name for a <see cref="SyntaxFoldRangeKind.Region"/>
    /// range, or null for a <see cref="SyntaxFoldRangeKind.Indentation"/> range.
    /// </param>
    internal SyntaxFoldRange(int startLine, int endLine, SyntaxFoldRangeKind kind, string? regionName)
    {
        Debug.Assert(startLine >= 0, "The tokenizer never records a fold range starting before the document begins.");
        Debug.Assert(endLine >= startLine, "The tokenizer only closes a fold range at or after the line that opened it.");

        StartLine = startLine;
        EndLine = endLine;
        _kind = kind;
        RegionName = regionName;
    }

    /// <summary>Gets the zero-based first line of the range, which stays visible when collapsed.</summary>
    public int StartLine { get; }

    /// <summary>Gets the zero-based last line of the range.</summary>
    public int EndLine { get; }

    /// <summary>Gets how this range was detected.</summary>
    public SyntaxFoldRangeKind Kind => _kind ?? SyntaxFoldRangeKind.Indentation;

    /// <summary>
    /// Gets the shared region name for a <see cref="SyntaxFoldRangeKind.Region"/> range, or null.
    /// </summary>
    public string? RegionName { get; }
}
