// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Sixel;

/// <summary>Owns sorted dense sixel identifiers for colors present in one indexed raster.</summary>
internal sealed class SixelPalette
{
    private const int _cubeSize = 216;
    private readonly byte[] _colors;
    private readonly byte[] _identifiers;

    /// <summary>Builds a deterministic palette from one complete borrowed indexed raster.</summary>
    /// <param name="raster">Cube indices or <see cref="SixelQuantizer.Transparent"/>.</param>
    public SixelPalette(ReadOnlySpan<byte> raster)
    {
        Span<bool> used = stackalloc bool[_cubeSize];

        foreach (var color in raster)
        {
            if (color != SixelQuantizer.Transparent)
            {
                used[color] = true;
            }
        }

        var count = 0;

        foreach (var value in used)
        {
            if (value)
            {
                count++;
            }
        }

        _colors = new byte[count];
        _identifiers = new byte[_cubeSize];
        _identifiers.AsSpan().Fill(SixelQuantizer.Transparent);
        var identifier = 0;

        for (var color = 0; color < used.Length; color++)
        {
            if (!used[color])
            {
                continue;
            }

            _colors[identifier] = checked((byte) color);
            _identifiers[color] = checked((byte) identifier);
            identifier++;
        }
    }

    /// <summary>Gets the number of used colors.</summary>
    public int Count => _colors.Length;

    /// <summary>Gets the sorted cube color for a dense sixel identifier.</summary>
    /// <param name="identifier">The zero-based identifier.</param>
    /// <returns>The corresponding cube index.</returns>
    public byte GetColor(int identifier) => _colors[identifier];

    /// <summary>Gets the dense identifier for a used cube color.</summary>
    /// <param name="color">The used cube index.</param>
    /// <returns>The corresponding dense identifier.</returns>
    public byte GetIdentifier(byte color)
    {
        var identifier = _identifiers[color];
        Debug.Assert(identifier != SixelQuantizer.Transparent, "Only used colors have sixel identifiers.");
        return identifier;
    }
}
