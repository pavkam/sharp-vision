// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Navigation;

/// <summary>Owns one immutable ordered Pager target snapshot.</summary>
internal sealed class PagerLayout
{
    /// <summary>Gets the empty layout used before measure and for an empty range.</summary>
    public static PagerLayout Empty { get; } = new([], default, 0);

    private readonly PagerLayoutTarget[] _targets;

    /// <summary>Initializes one immutable layout snapshot.</summary>
    /// <param name="targets">The source-ordered targets copied into this snapshot.</param>
    /// <param name="desiredSize">The intrinsic whole-cell size.</param>
    /// <param name="generation">The owner generation that produced the geometry.</param>
    public PagerLayout(
        IReadOnlyList<PagerLayoutTarget> targets,
        Size desiredSize,
        ulong generation)
    {
        ArgumentNullException.ThrowIfNull(targets);
        _targets = new PagerLayoutTarget[targets.Count];

        for (var index = 0; index < targets.Count; index++)
        {
            _targets[index] = targets[index];
        }

        DesiredSize = desiredSize;
        Generation = generation;
    }

    /// <summary>Gets the source-ordered immutable targets.</summary>
    public IReadOnlyList<PagerLayoutTarget> Targets => _targets;

    /// <summary>Gets the intrinsic whole-cell size.</summary>
    public Size DesiredSize { get; }

    /// <summary>Gets the owner generation that produced this geometry.</summary>
    public ulong Generation { get; }
}
