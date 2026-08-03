// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.FloatingSurfaceConsumer;

/// <summary>Defines the external consumer's complete validated surface chrome.</summary>
public readonly record struct ConsumerSurfaceStyle
{
    /// <summary>Gets the aggregate policy shared by external typed controls.</summary>
    internal static StyleDefinition<ConsumerSurfaceStyle> Definition { get; } = StyleDefinitions.Part(
        static _ => Default,
        static (previous, _, current, _) => previous == current
            ? InvalidationImpact.None
            : InvalidationImpact.Measure);

    /// <summary>Initializes one complete external surface presentation.</summary>
    /// <param name="border">The complete surface border.</param>
    /// <param name="shadow">The complete surface shadow.</param>
    /// <exception cref="ArgumentException">A visible shadow is combined with reserved border sides.</exception>
    public ConsumerSurfaceStyle(Border border, Shadow shadow)
    {
        if (shadow.IsVisible && border.Sides != BorderSide.None)
        {
            throw new ArgumentException(
                "A consumer surface cannot reserve border sides while its shadow is visible.",
                nameof(shadow));
        }

        Border = border;
        Shadow = shadow;
    }

    /// <summary>Gets the valid borderless and shadowless fallback.</summary>
    public static ConsumerSurfaceStyle Default => default;

    /// <summary>Gets the complete surface border.</summary>
    public Border Border { get; }

    /// <summary>Gets the complete surface shadow.</summary>
    public Shadow Shadow { get; }
}
