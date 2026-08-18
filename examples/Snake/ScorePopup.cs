// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>One floating score label that rises from an eaten apple and fades out.</summary>
/// <remarks>
/// The popup is an immutable snapshot: the board ages it by replacing the stored value with
/// <see cref="Aged"/> once per visual pulse and drops it after <see cref="Lifetime"/> pulses.
/// </remarks>
public readonly struct ScorePopup
{
    /// <summary>Defines the number of visual pulses a popup stays visible.</summary>
    public const int Lifetime = 10;

    /// <summary>Initializes a popup at its spawn cell with age zero.</summary>
    /// <param name="position">The board-relative cell the apple occupied.</param>
    /// <param name="text">The non-empty label, for example <c>+50</c>.</param>
    /// <param name="color">The label foreground color.</param>
    /// <exception cref="ArgumentException"><paramref name="text"/> is null, empty, or whitespace.</exception>
    public ScorePopup(Point position, string text, Color color)
        : this(position, text, color, age: 0)
    {
    }

    private ScorePopup(Point position, string text, Color color, int age)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Position = position;
        Text = text;
        Color = color;
        Age = age;
    }

    /// <summary>Gets the board-relative spawn cell.</summary>
    public Point Position { get; }

    /// <summary>Gets the label text.</summary>
    public string Text { get; }

    /// <summary>Gets the label foreground color.</summary>
    public Color Color { get; }

    /// <summary>Gets the popup age in visual pulses, starting at zero.</summary>
    public int Age { get; }

    /// <summary>Gets whether the popup has outlived <see cref="Lifetime"/> and should be dropped.</summary>
    public bool IsExpired => Age >= Lifetime;

    /// <summary>Gets the number of cells the label has risen above its spawn cell.</summary>
    public int Rise => Age / 2;

    /// <summary>Returns a copy aged by one visual pulse.</summary>
    public ScorePopup Aged() => new(Position, Text, Color, Age + 1);
}
