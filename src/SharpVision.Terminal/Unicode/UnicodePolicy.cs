// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Unicode;

/// <summary>Defines one immutable application-wide Unicode cell policy.</summary>
[PublicAPI]
public sealed record UnicodePolicy
{
    /// <summary>Initializes validated Unicode cell policy choices.</summary>
    /// <param name="ambiguousWidth">The East Asian Ambiguous width policy.</param>
    /// <param name="orphanPresentation">The base-less cluster presentation.</param>
    /// <exception cref="ArgumentOutOfRangeException">A policy value is unknown.</exception>
    public UnicodePolicy(
        Ambiguous ambiguousWidth = Ambiguous.Narrow,
        Presentation orphanPresentation = Presentation.Replacement)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(ambiguousWidth, nameof(ambiguousWidth), "The ambiguous-width policy is unknown.");

        ArgumentOutOfRangeException.ThrowIfNotDefined(orphanPresentation, nameof(orphanPresentation), "The orphan presentation policy is unknown.");

        AmbiguousWidth = ambiguousWidth;
        OrphanPresentation = orphanPresentation;
    }

    /// <summary>Gets the default pinned narrow replacement policy.</summary>
    public static UnicodePolicy Default { get; } = new();

    /// <summary>Gets the pinned Unicode Character Database version.</summary>
    public string UnicodeVersion { get; } = UnicodeInfo.Version;

    /// <summary>Gets the East Asian Ambiguous width policy.</summary>
    public Ambiguous AmbiguousWidth { get; }

    /// <summary>Gets the safe base-less cluster presentation.</summary>
    public Presentation OrphanPresentation { get; }
}
