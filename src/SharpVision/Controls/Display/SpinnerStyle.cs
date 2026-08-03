// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Collections.Immutable;

/// <summary>Defines one complete immutable spinner presentation.</summary>
[PublicAPI]
public readonly struct SpinnerStyle: IEquatable<SpinnerStyle>
{
    private static readonly ThemeProfile _standardAppearance = ControlStyleProfiles.Control;
    private static readonly ImmutableArray<Rune> _brailleFrames =
    [
        new Rune('⠋'), new Rune('⠙'), new Rune('⠹'), new Rune('⠸'), new Rune('⠼'),
        new Rune('⠴'), new Rune('⠦'), new Rune('⠧'), new Rune('⠇'), new Rune('⠏')
    ];
    private static readonly ImmutableArray<Rune> _denseBrailleFrames =
    [
        new Rune('⣿'), new Rune('⣷'), new Rune('⣯'), new Rune('⣟'),
        new Rune('⡿'), new Rune('⢿'), new Rune('⣻'), new Rune('⣽')
    ];
    private static readonly ImmutableArray<Rune> _asciiFrames =
    [
        new Rune('|'), new Rune('/'), new Rune('-'), new Rune('\\')
    ];

    private readonly ImmutableArray<Rune> _frames;
    private readonly ThemeProfile? _appearance;

    /// <summary>Gets the primary spinner-style definition.</summary>
    internal static StyleDefinition<SpinnerStyle> Definition { get; } = StyleDefinitions.Control(
        ThemeRole.Control,
        static profile => new SpinnerStyle(Default.Frames, profile),
        static style => style.Appearance,
        static (previous, _, current, _) => previous == current
            ? InvalidationImpact.None
            : InvalidationImpact.Render);

    /// <summary>Gets the maximum number of frames retained by one spinner presentation.</summary>
    public const int MaximumFrameCount = 256;

    /// <summary>Initializes a complete spinner presentation and copies the frame sequence.</summary>
    /// <param name="frames">The non-empty sequence of printable one-cell frames.</param>
    /// <param name="appearance">The complete normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentNullException"><paramref name="frames"/> or <paramref name="appearance"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="frames"/> is empty, exceeds <see cref="MaximumFrameCount"/>, or contains a control or non-one-cell glyph.</exception>
    public SpinnerStyle(IEnumerable<Rune> frames, ThemeProfile appearance)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var copiedFrames = CopyFrames(frames, nameof(frames));

        _frames = copiedFrames;
        _appearance = appearance;
    }

    /// <summary>Gets the standard ten-frame light Braille orbit.</summary>
    public static SpinnerStyle Default => default;

    /// <summary>Gets the ten-frame light Braille orbit.</summary>
    public static SpinnerStyle Braille { get; } = new(_brailleFrames, _standardAppearance);

    /// <summary>Gets the eight-frame dense Braille rotation.</summary>
    public static SpinnerStyle DenseBraille { get; } = new(_denseBrailleFrames, _standardAppearance);

    /// <summary>Gets the portable four-frame ASCII rotation.</summary>
    public static SpinnerStyle Ascii { get; } = new(_asciiFrames, _standardAppearance);

    /// <summary>Gets the immutable, non-empty frame sequence.</summary>
    public ImmutableArray<Rune> Frames => ResolveFrames();

    /// <summary>Gets the complete normal and visual-state appearance profile.</summary>
    public ThemeProfile Appearance => ResolveAppearance();

    /// <summary>Creates a validated copy with selected members replaced.</summary>
    /// <param name="frames">The optional copied replacement frame sequence.</param>
    /// <param name="appearance">The optional appearance-profile overlay.</param>
    /// <returns>A complete validated spinner style.</returns>
    /// <exception cref="ArgumentException"><paramref name="frames"/> is empty, oversized, or contains an invalid glyph.</exception>
    public SpinnerStyle With(
        IEnumerable<Rune>? frames = null,
        AppearanceProfileSet? appearance = null) => new(
        frames ?? Frames,
        appearance is null ? Appearance : StyleResolution.Apply(Appearance, appearance.Value));

    /// <summary>Determines whether this value and another style resolve to the same presentation.</summary>
    /// <param name="other">The other style to compare.</param>
    /// <returns><see langword="true"/> when all resolved presentation members are equal.</returns>
    public bool Equals(SpinnerStyle other) =>
        Frames.AsSpan().SequenceEqual(other.Frames.AsSpan()) && Appearance.Equals(other.Appearance);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SpinnerStyle other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var frame in Frames)
        {
            hash.Add(frame);
        }

        hash.Add(Appearance);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two spinner styles resolve to the same presentation.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve equally.</returns>
    public static bool operator ==(SpinnerStyle left, SpinnerStyle right) => left.Equals(right);

    /// <summary>Determines whether two spinner styles resolve to different presentations.</summary>
    /// <param name="left">The first style.</param>
    /// <param name="right">The second style.</param>
    /// <returns><see langword="true"/> when the styles resolve differently.</returns>
    public static bool operator !=(SpinnerStyle left, SpinnerStyle right) => !left.Equals(right);

    /// <summary>Copies and validates a bounded frame sequence through one enumeration.</summary>
    /// <param name="frames">The non-null frame sequence.</param>
    /// <param name="parameterName">The public parameter name used for validation failures.</param>
    /// <returns>A non-empty immutable frame sequence no longer than <see cref="MaximumFrameCount"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="frames"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The sequence is empty, too long, or contains an invalid frame.</exception>
    internal static ImmutableArray<Rune> CopyFrames(IEnumerable<Rune> frames, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(frames, parameterName);
        var builder = ImmutableArray.CreateBuilder<Rune>();

        foreach (var frame in frames)
        {
            if (builder.Count == MaximumFrameCount)
            {
                throw new ArgumentException(
                    $"A spinner style cannot contain more than {MaximumFrameCount} frames.",
                    parameterName);
            }

            builder.Add(frame.ValidateSingleCell(parameterName));
        }

        return builder.Count == 0
            ? throw new ArgumentException("A spinner style must contain at least one frame.", parameterName)
            : builder.ToImmutable();
    }

    private ImmutableArray<Rune> ResolveFrames() => _frames.IsDefault ? _brailleFrames : _frames;

    private ThemeProfile ResolveAppearance() => _appearance ?? _standardAppearance;

}
