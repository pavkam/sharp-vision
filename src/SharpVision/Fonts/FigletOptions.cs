// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Fonts;

/// <summary>Overrides optional FIG-font direction and layout metadata.</summary>
[PublicAPI]
public readonly record struct FigletOptions
{
    private const FigletLayout _all = (FigletLayout) 0x7fff;

    /// <summary>Initializes validated optional rendering overrides.</summary>
    /// <param name="direction">The optional scalar ordering override.</param>
    /// <param name="layout">The optional layout-bit override.</param>
    /// <exception cref="ArgumentOutOfRangeException">An enum value contains unknown data.</exception>
    public FigletOptions(FigletDirection? direction = null, FigletLayout? layout = null)
    {
        if (direction.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(direction.Value, nameof(direction), "The FIG-let direction is unknown.");
        }

        if (layout.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfUndefinedFlags(layout.Value, _all, nameof(layout), "The layout contains unknown bits.");
        }

        Direction = direction;
        Layout = layout;
    }

    /// <summary>Gets the optional scalar ordering override.</summary>
    public FigletDirection? Direction { get; }

    /// <summary>Gets the optional layout-bit override.</summary>
    public FigletLayout? Layout { get; }
}
