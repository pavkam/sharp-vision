// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Display;

using System.Collections.Immutable;

/// <summary>Defines optional member-wise contributions to a complete spinner presentation.</summary>
[PublicAPI]
public readonly struct SpinnerStyleSet: IEquatable<SpinnerStyleSet>
{
    /// <summary>Initializes a partial spinner presentation contribution and copies supplied frames.</summary>
    /// <param name="frames">The optional replacement frame sequence.</param>
    /// <param name="appearance">The optional partial normal and visual-state appearance profile.</param>
    /// <exception cref="ArgumentException"><paramref name="frames"/> is empty, exceeds <see cref="SpinnerStyle.MaximumFrameCount"/>, or contains a control or non-one-cell glyph.</exception>
    public SpinnerStyleSet(IEnumerable<Rune>? frames = null, AppearanceProfileSet? appearance = null)
    {
        ImmutableArray<Rune>? copiedFrames = frames is null
            ? null
            : SpinnerStyle.CopyFrames(frames, nameof(frames));

        Frames = copiedFrames;
        Appearance = appearance;
    }

    /// <summary>Gets the optional immutable replacement frame sequence.</summary>
    public ImmutableArray<Rune>? Frames { get; }

    /// <summary>Gets the optional partial normal and visual-state appearance profile.</summary>
    public AppearanceProfileSet? Appearance { get; }

    /// <summary>Applies this partial contribution to a complete spinner presentation.</summary>
    /// <param name="baseline">The complete presentation that supplies omitted members.</param>
    /// <returns>The validated complete composed presentation.</returns>
    public SpinnerStyle Apply(SpinnerStyle baseline)
    {
        var appearance = Appearance is null
            ? baseline.Appearance
            : StyleResolution.Apply(baseline.Appearance, Appearance.Value);

        return new SpinnerStyle(Frames ?? baseline.Frames, appearance);
    }

    /// <summary>Determines whether supplied frame and appearance contributions are semantically equal.</summary>
    /// <param name="other">The other partial style to compare.</param>
    /// <returns><see langword="true"/> when both contributions are semantically equal.</returns>
    public bool Equals(SpinnerStyleSet other) =>
        Appearance == other.Appearance &&
        Frames.HasValue == other.Frames.HasValue &&
        (!Frames.HasValue || Frames.Value.AsSpan().SequenceEqual(other.Frames!.Value.AsSpan()));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SpinnerStyleSet other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Frames.HasValue);

        if (Frames is { } frames)
        {
            foreach (var frame in frames)
            {
                hash.Add(frame);
            }
        }

        hash.Add(Appearance);
        return hash.ToHashCode();
    }

    /// <summary>Determines whether two partial spinner styles are semantically equal.</summary>
    /// <param name="left">The first partial style.</param>
    /// <param name="right">The second partial style.</param>
    /// <returns><see langword="true"/> when both contributions are semantically equal.</returns>
    public static bool operator ==(SpinnerStyleSet left, SpinnerStyleSet right) => left.Equals(right);

    /// <summary>Determines whether two partial spinner styles are semantically different.</summary>
    /// <param name="left">The first partial style.</param>
    /// <param name="right">The second partial style.</param>
    /// <returns><see langword="true"/> when either contribution is different.</returns>
    public static bool operator !=(SpinnerStyleSet left, SpinnerStyleSet right) => !left.Equals(right);
}

