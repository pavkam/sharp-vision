// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Geometry;

/// <summary>Represents a signed zero-based cell or pixel coordinate.</summary>
public readonly record struct Point
{
    /// <summary>Initializes a signed coordinate.</summary>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Gets the horizontal coordinate.</summary>
    public int X { get; }

    /// <summary>Gets the vertical coordinate.</summary>
    public int Y { get; }

    /// <summary>Deconstructs the coordinate.</summary>
    /// <param name="x">Receives the horizontal coordinate.</param>
    /// <param name="y">Receives the vertical coordinate.</param>
    public void Deconstruct(out int x, out int y)
    {
        x = X;
        y = Y;
    }
}
