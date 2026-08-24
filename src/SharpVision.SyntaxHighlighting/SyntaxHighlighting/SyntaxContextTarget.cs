// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Represents one fully resolved <see cref="SyntaxContextSwitch"/>: how many contexts to pop and,
/// in push order, which resolved contexts to push afterward.
/// </summary>
/// <remarks>
/// A reference that named a context or definition the grammar compiler could not resolve is
/// dropped rather than rejected - the same graceful-degradation philosophy upstream
/// KSyntaxHighlighting applies elsewhere for a missing embedded-language definition - but only
/// that one <see cref="Pushes"/> entry is dropped: <see cref="PopCount"/> is always preserved
/// exactly as declared, even when every push in the same switch fails to resolve, so
/// <c>#pop!Name##Missing</c> still performs its pop and simply pushes nothing. This differs from
/// upstream's own resolution step, which collapses a switch with no resolvable push targets to a
/// complete no-op (pop included). SharpVision's choice is deliberate: a broken cross-definition
/// push should not also strand the tokenizer one context deeper than the author intended, purely
/// because the intended destination happened to be unavailable.
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
