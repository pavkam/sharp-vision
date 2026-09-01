// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Describes one whole-cell target in an immutable Pager layout.</summary>
internal readonly record struct PagerLayoutTarget
{
    /// <summary>Initializes one validated target.</summary>
    /// <param name="kind">The semantic target kind.</param>
    /// <param name="pageIndex">The zero-based destination, or -1 for an omission marker.</param>
    /// <param name="text">The non-empty printable target text.</param>
    /// <param name="cellWidth">The positive measured terminal-cell width.</param>
    /// <param name="bounds">The whole-cell arranged bounds.</param>
    /// <param name="isEnabled">Whether this target accepts input.</param>
    /// <param name="isCurrent">Whether this is the current numbered page.</param>
    public PagerLayoutTarget(
        PagerTargetKind kind,
        int pageIndex,
        string text,
        int cellWidth,
        Rect bounds,
        bool isEnabled,
        bool isCurrent)
    {
        Debug.Assert(Enum.IsDefined(kind));
        Debug.Assert(kind == PagerTargetKind.Omitted ? pageIndex == -1 : pageIndex >= 0);
        Debug.Assert(!string.IsNullOrEmpty(text));
        Debug.Assert(cellWidth > 0);
        Kind = kind;
        PageIndex = pageIndex;
        Text = text;
        CellWidth = cellWidth;
        Bounds = bounds;
        IsEnabled = isEnabled;
        IsCurrent = isCurrent;
    }

    /// <summary>Gets the semantic target kind.</summary>
    public PagerTargetKind Kind { get; }

    /// <summary>Gets the zero-based destination, or -1 for an omission marker.</summary>
    public int PageIndex { get; }

    /// <summary>Gets the exact printable target text.</summary>
    public string Text { get; }

    /// <summary>Gets the target width in terminal cells.</summary>
    public int CellWidth { get; }

    /// <summary>Gets the whole-cell arranged bounds.</summary>
    public Rect Bounds { get; }

    /// <summary>Gets whether this target accepts input.</summary>
    public bool IsEnabled { get; }

    /// <summary>Gets whether this is the current numbered page.</summary>
    public bool IsCurrent { get; }

    /// <summary>Creates the same semantic target at arranged bounds.</summary>
    /// <param name="bounds">The whole-cell arranged bounds.</param>
    /// <returns>A target retaining the same identity and state.</returns>
    public PagerLayoutTarget At(Rect bounds) => new(
        Kind,
        PageIndex,
        Text,
        CellWidth,
        bounds,
        IsEnabled,
        IsCurrent);
}
