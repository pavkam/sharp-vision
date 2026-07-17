// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Rendering;


using SharpVision.Terminal.Capabilities;

/// <summary>Projects semantic colors through a deterministic xterm-compatible reference palette.</summary>
internal static class Palette
{
    /// <summary>Projects one semantic color to the highest representation allowed by a color tier.</summary>
    /// <param name="source">The semantic default, indexed, or RGB color.</param>
    /// <param name="depth">The validated terminal color tier.</param>
    /// <returns>The default color or a representation contained by <paramref name="depth"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="depth"/> is unknown.</exception>
    internal static Color Project(Color source, ColorDepth depth)
    {
        if (!Enum.IsDefined(depth))
        {
            throw new ArgumentOutOfRangeException(nameof(depth), depth, "The color depth is unknown.");
        }

        if (source.Kind == ColorKind.Default || depth == ColorDepth.Monochrome)
        {
            return Color.Default;
        }

        if (depth == ColorDepth.TrueColor ||
            (depth == ColorDepth.Indexed256 && source.Kind == ColorKind.Indexed) ||
            (depth == ColorDepth.Basic16 && source.Kind == ColorKind.Indexed && source.Red < 16))
        {
            return source;
        }

        Resolve(source, out var red, out var green, out var blue);
        var count = depth == ColorDepth.Basic16 ? 16 : 256;
        return Color.Indexed(Nearest(red, green, blue, count));
    }

    private static int Nearest(byte red, byte green, byte blue, int count)
    {
        var bestIndex = 0;
        var bestDistance = int.MaxValue;

        for (var index = 0; index < 16; index++)
        {
            Consider(red, green, blue, index, ref bestIndex, ref bestDistance);
        }

        if (count == 16)
        {
            return bestIndex;
        }

        var cube = 16 +
            (NearestLevel(red) * 36) +
            (NearestLevel(green) * 6) +
            NearestLevel(blue);
        Consider(red, green, blue, cube, ref bestIndex, ref bestDistance);

        // The grayscale ramp is one-dimensional, but checking its 24 entries
        // keeps tie-breaking visibly identical to ascending palette order.
        for (var index = 232; index <= byte.MaxValue; index++)
        {
            Consider(red, green, blue, index, ref bestIndex, ref bestDistance);
        }

        return bestIndex;
    }

    private static void Consider(
        byte red,
        byte green,
        byte blue,
        int index,
        ref int bestIndex,
        ref int bestDistance)
    {
        Resolve(index, out var candidateRed, out var candidateGreen, out var candidateBlue);
        var distance = Distance(red, candidateRed) +
            Distance(green, candidateGreen) +
            Distance(blue, candidateBlue);

        if (distance < bestDistance)
        {
            bestDistance = distance;
            bestIndex = index;
        }
    }

    private static int Distance(byte left, byte right)
    {
        var difference = left - right;
        return difference * difference;
    }

    private static void Resolve(Color source, out byte red, out byte green, out byte blue)
    {
        if (source.Kind == ColorKind.Rgb)
        {
            red = source.Red;
            green = source.Green;
            blue = source.Blue;
            return;
        }

        Debug.Assert(source.Kind == ColorKind.Indexed, "Default colors return before palette resolution.");
        Resolve(source.Red, out red, out green, out blue);
    }

    private static void Resolve(int index, out byte red, out byte green, out byte blue)
    {
        Debug.Assert(index is >= 0 and <= byte.MaxValue, "Palette indices are one byte.");

        if (index < 16)
        {
            ResolveBasic(index, out red, out green, out blue);
            return;
        }

        if (index < 232)
        {
            var cube = index - 16;
            red = Level(cube / 36);
            green = Level(cube % 36 / 6);
            blue = Level(cube % 6);
            return;
        }

        var gray = (byte) (8 + ((index - 232) * 10));
        red = gray;
        green = gray;
        blue = gray;
    }

    private static byte Level(int value) => value == 0 ? (byte) 0 : (byte) (55 + (value * 40));

    private static int NearestLevel(byte component)
    {
        var best = 0;
        var bestDistance = int.MaxValue;

        for (var level = 0; level < 6; level++)
        {
            var distance = Distance(component, Level(level));

            if (distance < bestDistance)
            {
                best = level;
                bestDistance = distance;
            }
        }

        return best;
    }

    private static void ResolveBasic(int index, out byte red, out byte green, out byte blue)
    {
        var packed = index switch
        {
            0 => 0x000000,
            1 => 0xcd0000,
            2 => 0x00cd00,
            3 => 0xcdcd00,
            4 => 0x0000ee,
            5 => 0xcd00cd,
            6 => 0x00cdcd,
            7 => 0xe5e5e5,
            8 => 0x7f7f7f,
            9 => 0xff0000,
            10 => 0x00ff00,
            11 => 0xffff00,
            12 => 0x5c5cff,
            13 => 0xff00ff,
            14 => 0x00ffff,
            15 => 0xffffff,
            _ => throw new UnreachableException(),
        };
        red = (byte) (packed >> 16);
        green = (byte) (packed >> 8);
        blue = (byte) packed;
    }
}
