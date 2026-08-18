// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>
/// Contains one validated OSC palette or dynamic-color response. The default
/// value is an empty sentinel and is identified by <see cref="IsEmpty"/>.
/// </summary>
[PublicAPI]
public readonly record struct PaletteResponse
{
    private readonly int _index;

    /// <summary>Initializes one validated color response.</summary>
    /// <param name="kind">The palette, foreground, or background response family.</param>
    /// <param name="index">The palette index, or null for a dynamic default color.</param>
    /// <param name="red">The normalized 16-bit red component.</param>
    /// <param name="green">The normalized 16-bit green component.</param>
    /// <param name="blue">The normalized 16-bit blue component.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The response family or palette index is invalid.
    /// </exception>
    public PaletteResponse(
        ResponseKind kind,
        int? index,
        ushort red,
        ushort green,
        ushort blue)
    {
        if (kind is not ResponseKind.PaletteColor and
            not ResponseKind.ForegroundColor and
            not ResponseKind.BackgroundColor)
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "The response is not a color family.");
        }

        if ((kind == ResponseKind.PaletteColor && index is null or < 0 or > byte.MaxValue) ||
            (kind != ResponseKind.PaletteColor && index.HasValue))
        {
            throw new ArgumentOutOfRangeException(nameof(index), index, "The palette index does not match the response family.");
        }

        Kind = kind;
        _index = index ?? 0;
        Red = red;
        Green = green;
        Blue = blue;
    }

    /// <summary>Gets the palette or dynamic-color response family.</summary>
    public ResponseKind Kind { get; }

    /// <summary>Gets whether this value is the valid empty default sentinel.</summary>
    public bool IsEmpty => Kind == ResponseKind.None;

    /// <summary>Gets the palette index, or null for foreground/background defaults.</summary>
    public int? Index => Kind == ResponseKind.PaletteColor ? _index : null;

    /// <summary>Gets the normalized 16-bit red component.</summary>
    public ushort Red { get; }

    /// <summary>Gets the normalized 16-bit green component.</summary>
    public ushort Green { get; }

    /// <summary>Gets the normalized 16-bit blue component.</summary>
    public ushort Blue { get; }
}
