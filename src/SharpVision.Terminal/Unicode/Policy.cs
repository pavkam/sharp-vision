// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Unicode;

/// <summary>Defines one immutable application-wide Unicode cell policy.</summary>
public sealed record Policy
{
    /// <summary>Initializes validated Unicode cell policy choices.</summary>
    /// <param name="ambiguousWidth">The East Asian Ambiguous width policy.</param>
    /// <param name="orphanPresentation">The base-less cluster presentation.</param>
    /// <exception cref="ArgumentOutOfRangeException">A policy value is unknown.</exception>
    public Policy(
        Ambiguous ambiguousWidth = Ambiguous.Narrow,
        Presentation orphanPresentation = Presentation.Replacement)
    {
        if (!Enum.IsDefined(ambiguousWidth))
        {
            throw new ArgumentOutOfRangeException(
                nameof(ambiguousWidth),
                ambiguousWidth,
                "The ambiguous-width policy is unknown.");
        }

        if (!Enum.IsDefined(orphanPresentation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(orphanPresentation),
                orphanPresentation,
                "The orphan presentation policy is unknown.");
        }

        AmbiguousWidth = ambiguousWidth;
        OrphanPresentation = orphanPresentation;
    }

    /// <summary>Gets the default pinned narrow replacement policy.</summary>
    public static Policy Default { get; } = new();

    /// <summary>Gets the pinned Unicode Character Database version.</summary>
    public string UnicodeVersion { get; } = Info.Version;

    /// <summary>Gets the East Asian Ambiguous width policy.</summary>
    public Ambiguous AmbiguousWidth { get; }

    /// <summary>Gets the safe base-less cluster presentation.</summary>
    public Presentation OrphanPresentation { get; }
}
