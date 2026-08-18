// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines immutable structural and interaction metadata for one ownership slot.</summary>
/// <remarks>
/// The default value describes normal container children that do not participate in hit
/// testing or navigation and whose mutations require no automatic phase invalidation.
/// </remarks>
internal readonly record struct OwnedControlOptions
{
    /// <summary>Initializes validated metadata for one ownership slot.</summary>
    /// <param name="role">The structural purpose of controls held by the slot.</param>
    /// <param name="layer">The visual and input layer occupied by the slot.</param>
    /// <param name="participatesInHitTesting">
    /// Whether controls in the slot are considered by pointer hit testing.
    /// </param>
    /// <param name="participatesInNavigation">
    /// Whether controls in the slot are considered by focus navigation.
    /// </param>
    /// <param name="partKey">
    /// An optional stable, non-empty key identifying a named framework part.
    /// </param>
    /// <param name="impact">The earliest UI phase invalidated by a committed slot mutation.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="role"/>, <paramref name="layer"/>, or <paramref name="impact"/> is undefined.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="partKey"/> is empty or contains only whitespace.
    /// </exception>
    public OwnedControlOptions(
        OwnedControlRole role,
        OwnedControlLayer layer,
        bool participatesInHitTesting,
        bool participatesInNavigation,
        string? partKey,
        InvalidationImpact impact)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(role, nameof(role), "The ownership role is unknown.");

        ArgumentOutOfRangeException.ThrowIfNotDefined(layer, nameof(layer), "The ownership layer is unknown.");

        if (partKey is not null && string.IsNullOrWhiteSpace(partKey))
        {
            throw new ArgumentException("The part key must contain non-whitespace text.", nameof(partKey));
        }

        ArgumentOutOfRangeException.ThrowIfNotDefined(impact, nameof(impact), "The change impact is unknown.");

        Role = role;
        Layer = layer;
        ParticipatesInHitTesting = participatesInHitTesting;
        ParticipatesInNavigation = participatesInNavigation;
        PartKey = partKey;
        Impact = impact;
    }

    /// <summary>Gets the structural purpose of controls held by the slot.</summary>
    public OwnedControlRole Role { get; }

    /// <summary>Gets the visual and input layer occupied by the slot.</summary>
    public OwnedControlLayer Layer { get; }

    /// <summary>Gets whether controls in the slot participate in pointer hit testing.</summary>
    public bool ParticipatesInHitTesting { get; }

    /// <summary>Gets whether controls in the slot participate in focus navigation.</summary>
    public bool ParticipatesInNavigation { get; }

    /// <summary>Gets the optional stable name of a framework part represented by the slot.</summary>
    public string? PartKey { get; }

    /// <summary>Gets the earliest UI phase invalidated by a committed slot mutation.</summary>
    public InvalidationImpact Impact { get; }
}
