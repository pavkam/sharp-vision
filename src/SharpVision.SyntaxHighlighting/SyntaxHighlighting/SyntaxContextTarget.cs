// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Represents one fully resolved <see cref="SyntaxContextSwitch"/>: how many contexts to pop and,
/// in push order, which resolved contexts to push afterward.
/// </summary>
/// <remarks>
/// A reference that named a context or definition the grammar compiler could not resolve is
/// dropped rather than rejected, the same graceful-degradation behavior upstream
/// KSyntaxHighlighting uses (it logs a warning and treats the switch as a no-op for that target).
/// This keeps one missing embedded-language definition from breaking highlighting of everything
/// else in a document.
/// </remarks>
[PublicAPI]
public readonly record struct SyntaxContextTarget
{
    /// <summary>The switch that changes nothing.</summary>
    public static readonly SyntaxContextTarget Stay = new(0, []);

    /// <summary>Initializes a resolved context target.</summary>
    /// <param name="popCount">The non-negative number of contexts to pop.</param>
    /// <param name="pushes">The non-null, ordered contexts to push after popping.</param>
    internal SyntaxContextTarget(int popCount, IReadOnlyList<SyntaxContextTargetEntry> pushes)
    {
        PopCount = popCount;
        Pushes = pushes;
    }

    /// <summary>Gets the number of contexts to pop before pushing <see cref="Pushes"/>.</summary>
    public int PopCount { get; }

    /// <summary>Gets the ordered contexts to push, in push order, after popping.</summary>
    public IReadOnlyList<SyntaxContextTargetEntry> Pushes { get; }

    /// <summary>Gets whether this target changes the context stack at all.</summary>
    public bool IsStay => PopCount == 0 && Pushes.Count == 0;
}
