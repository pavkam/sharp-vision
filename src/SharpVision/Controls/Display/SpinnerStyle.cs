// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

/// <summary>Defines one complete immutable spinner presentation. This style's own
/// "spinner" theme key falls back to <see cref="ControlStyle"/>'s "control" key.</summary>
[PublicAPI]
public sealed record SpinnerStyle: ControlStyle
{
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

    /// <summary>Gets the primary spinner-style definition. A theme's own "spinner" key can
    /// restyle Frames directly - the standalone registrable "spinner" style section this used to
    /// read separately is retired; the reflective Overlay reaches every member of
    /// this type uniformly.</summary>
    internal static StyleDefinition<SpinnerStyle> Definition { get; } = StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, _, current, _) => previous == current
            ? InvalidationImpact.None
            : InvalidationImpact.Render);

    private static SpinnerStyle Complete(ControlStyle control, VisualState state) =>
        new(control.Face, control.Border, control.Shadow, _brailleFrames);

    /// <summary>Gets the maximum number of frames retained by one spinner presentation.</summary>
    public const int MaximumFrameCount = 256;

    /// <summary>Initializes a complete spinner presentation and copies the frame sequence.</summary>
    /// <param name="face">The complete normal face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="frames">The non-empty sequence of printable one-cell frames.</param>
    /// <exception cref="ArgumentNullException"><paramref name="frames"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="frames"/> is empty, exceeds <see cref="MaximumFrameCount"/>, or contains a control or non-one-cell glyph.</exception>
    [SetsRequiredMembers]
    public SpinnerStyle(Face face, Border border, Shadow shadow, IEnumerable<Rune> frames) : base(face, border, shadow) =>
        Frames = CopyFrames(frames, nameof(frames));

    /// <summary>Gets the standard ten-frame light Braille orbit.</summary>
    public static new SpinnerStyle Default => Braille;

    /// <summary>Gets the ten-frame light Braille orbit.</summary>
    public static SpinnerStyle Braille { get; } = Complete(ControlStyle.Default, VisualState.Normal);

    /// <summary>Gets the eight-frame dense Braille rotation.</summary>
    public static SpinnerStyle DenseBraille { get; } = Braille with { Frames = _denseBrailleFrames };

    /// <summary>Gets the portable four-frame ASCII rotation.</summary>
    public static SpinnerStyle Ascii { get; } = Braille with { Frames = _asciiFrames };

    /// <summary>Gets the immutable, non-empty frame sequence.</summary>
    /// <exception cref="ArgumentException">The replacement sequence is empty, oversized, or contains an invalid frame.</exception>
    public required ImmutableArray<Rune> Frames
    {
        get;
        init => field = CopyFrames(value, nameof(value));
    }

    /// <summary>Compares two presentations by frame <em>content</em>. The compiler-generated record
    /// equality would compare <see cref="Frames"/> through
    /// <see cref="ImmutableArray{T}"/>'s own <see cref="IEquatable{T}"/>, which compares the wrapped
    /// array <em>handle</em> - and <see cref="CopyFrames"/> always builds a fresh array, so two
    /// independently constructed styles over the same runes would otherwise never be equal. That
    /// made every <c>ActualStyle</c>/<c>Style</c> assertion here fail while printing identical
    /// expected and actual values, and made <see cref="Definition"/>'s <c>previous == current</c>
    /// comparison report a Render invalidation on every single resolution.</summary>
    /// <param name="other">The presentation to compare against, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when both sides carry the same frames and base appearance.</returns>
    public bool Equals(SpinnerStyle? other) =>
        other is not null && base.Equals(other) && Frames.AsSpan().SequenceEqual(other.Frames.AsSpan());

    /// <summary>Hashes the base appearance together with the frame content, staying consistent with
    /// <see cref="Equals(SpinnerStyle)"/>.</summary>
    /// <returns>A content-based hash code.</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(base.GetHashCode());
        foreach (var frame in Frames)
        {
            hash.Add(frame);
        }

        return hash.ToHashCode();
    }

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
}
