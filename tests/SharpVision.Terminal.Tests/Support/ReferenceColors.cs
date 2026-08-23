// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Support;

/// <summary>Provides an independent xterm-compatible RGB oracle for received palette positions.</summary>
/// <remarks>
/// This is a mirrored copy of the equivalent class at
/// <c>tests/SharpVision.Test.Shared/ReferenceColors.cs</c>, byte-identical apart from its
/// namespace, because layering that project's own dependency on core <c>SharpVision</c> onto this
/// Terminal test assembly - which otherwise never needs it - is worse than the duplication. Apply
/// any behavioral change to both copies.
/// </remarks>
internal static class ReferenceColors
{
    /// <summary>Returns the RGB value at one palette position.</summary>
    /// <param name="position">The palette position from zero through 255.</param>
    /// <returns>The independently calculated RGB value.</returns>
    internal static Color Get(int position)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, byte.MaxValue);

        if (position < 16)
        {
            return Basic(position);
        }

        if (position < 232)
        {
            var cube = position - 16;
            return Color.Rgb(Level(cube / 36), Level(cube % 36 / 6), Level(cube % 6));
        }

        var gray = 8 + ((position - 232) * 10);
        return Color.Rgb(gray, gray, gray);
    }

    private static int Level(int value) => value == 0 ? 0 : 55 + (value * 40);

    private static Color Basic(int position) => Color.FromHex(position switch
    {
        0 => "#000000",
        1 => "#cd0000",
        2 => "#00cd00",
        3 => "#cdcd00",
        4 => "#0000ee",
        5 => "#cd00cd",
        6 => "#00cdcd",
        7 => "#e5e5e5",
        8 => "#7f7f7f",
        9 => "#ff0000",
        10 => "#00ff00",
        11 => "#ffff00",
        12 => "#5c5cff",
        13 => "#ff00ff",
        14 => "#00ffff",
        15 => "#ffffff",
        _ => throw new UnreachableException()
    });
}
