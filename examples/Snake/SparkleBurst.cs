// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>One expanding ring of sparkle glyphs centered on an eaten apple.</summary>
/// <remarks>
/// The burst is an immutable snapshot: the board ages it by replacing the stored value with
/// <see cref="Aged"/> once per visual pulse and drops it after <see cref="Lifetime"/> pulses.
/// </remarks>
public readonly struct SparkleBurst
{
    /// <summary>Defines the number of visual pulses a burst stays visible.</summary>
    public const int Lifetime = 6;

    /// <summary>Initializes a burst at its center cell with age zero.</summary>
    /// <param name="center">The board-relative cell the apple occupied.</param>
    /// <param name="color">The sparkle foreground color.</param>
    public SparkleBurst(Point center, Color color)
        : this(center, color, age: 0)
    {
    }

    private SparkleBurst(Point center, Color color, int age)
    {
        Center = center;
        Color = color;
        Age = age;
    }

    /// <summary>Gets the board-relative center cell.</summary>
    public Point Center { get; }

    /// <summary>Gets the sparkle foreground color.</summary>
    public Color Color { get; }

    /// <summary>Gets the burst age in visual pulses, starting at zero.</summary>
    public int Age { get; }

    /// <summary>Gets whether the burst has outlived <see cref="Lifetime"/> and should be dropped.</summary>
    public bool IsExpired => Age >= Lifetime;

    /// <summary>Returns a copy aged by one visual pulse.</summary>
    public SparkleBurst Aged() => new(Center, Color, Age + 1);
}
