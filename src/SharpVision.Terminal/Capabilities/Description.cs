// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Describes immutable, validated metadata for one terminal description without
/// exposing native buffers or raw command strings.
/// </summary>
[PublicAPI]
public sealed class Description
{
    /// <summary>Initializes one immutable terminal-description metadata value.</summary>
    /// <param name="name">The non-blank terminal-description name.</param>
    /// <param name="origin">The trusted source of the metadata.</param>
    /// <param name="suitability">The full-screen suitability result.</param>
    /// <param name="columns">The optional positive initial column count.</param>
    /// <param name="lines">The optional positive initial line count.</param>
    /// <param name="colors">The optional positive supported color count.</param>
    /// <param name="automaticMargins">Whether writing the final column uses automatic margins.</param>
    /// <param name="backColorErase">Whether erasure uses the current background color.</param>
    /// <param name="eatNewlineGlitch">Whether a newline after an automatic-margin wrap is ignored.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or contains only white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="origin"/> or <paramref name="suitability"/> is undefined, or a present
    /// <paramref name="columns"/>, <paramref name="lines"/>, or <paramref name="colors"/> value is not positive.
    /// </exception>
    public Description(
        string name,
        DescriptionOrigin origin,
        Suitability suitability,
        int? columns = null,
        int? lines = null,
        int? colors = null,
        bool automaticMargins = false,
        bool backColorErase = false,
        bool eatNewlineGlitch = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        origin.ValidateDefined(nameof(origin), "The description origin is unknown.");

        suitability.ValidateDefined(nameof(suitability), "The suitability result is unknown.");

        ValidatePositive(columns, nameof(columns));
        ValidatePositive(lines, nameof(lines));
        ValidatePositive(colors, nameof(colors));

        Name = name;
        Origin = origin;
        Suitability = suitability;
        Columns = columns;
        Lines = lines;
        Colors = colors;
        AutomaticMargins = automaticMargins;
        BackColorErase = backColorErase;
        EatNewlineGlitch = eatNewlineGlitch;
    }

    /// <summary>Gets the terminal-description name used for diagnostics and evidence.</summary>
    public string Name { get; }

    /// <summary>Gets the trusted source of this terminal description.</summary>
    public DescriptionOrigin Origin { get; }

    /// <summary>Gets the validated full-screen suitability result.</summary>
    public Suitability Suitability { get; }

    /// <summary>Gets the optional initial terminal width in cells.</summary>
    public int? Columns { get; }

    /// <summary>Gets the optional initial terminal height in cells.</summary>
    public int? Lines { get; }

    /// <summary>Gets the optional number of addressable colors.</summary>
    public int? Colors { get; }

    /// <summary>Gets whether writing the final column uses automatic margins.</summary>
    public bool AutomaticMargins { get; }

    /// <summary>Gets whether erasure uses the current background color.</summary>
    public bool BackColorErase { get; }

    /// <summary>Gets whether a newline after an automatic-margin wrap is ignored.</summary>
    public bool EatNewlineGlitch { get; }

    /// <summary>Creates equivalent metadata with one validated suitability result.</summary>
    /// <param name="suitability">The replacement full-screen suitability result.</param>
    /// <returns>A new immutable description with otherwise identical metadata.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="suitability"/> is undefined.</exception>
    internal Description WithSuitability(Suitability suitability) => new(
        Name,
        Origin,
        suitability,
        Columns,
        Lines,
        Colors,
        AutomaticMargins,
        BackColorErase,
        EatNewlineGlitch);

    private static void ValidatePositive(int? value, string parameterName)
    {
        if (value is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "A present terminal-description numeric value must be positive.");
        }
    }
}
