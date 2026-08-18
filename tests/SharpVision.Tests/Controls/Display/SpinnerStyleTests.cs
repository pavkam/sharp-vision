// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies complete spinner presentations and their frame sequence.</summary>
public sealed class SpinnerStyleTests
{
    /// <summary>Verifies Braille resolves the established ten-frame recipe and is the default.</summary>
    [Fact]
    public void Braille_ContainsTenFramesAndIsDefault()
    {
        var actual = SpinnerStyle.Default;

        actual.ShouldBe(SpinnerStyle.Braille);
        actual.Frames.Length.ShouldBe(10);
        actual.Frames[0].ShouldBe(new Rune('⠋'));
        actual.Frames[^1].ShouldBe(new Rune('⠏'));
    }

    /// <summary>Verifies construction copies caller-owned mutable frame storage.</summary>
    [Fact]
    public void Constructor_WhenCallerMutatesFrames_RetainsImmutableCopy()
    {
        var baseline = SpinnerStyle.Braille;
        var frames = new[] { new Rune('|'), new Rune('/') };
        var style = new SpinnerStyle(baseline.Face, baseline.Border, baseline.Shadow, frames);

        frames[0] = new Rune('x');

        style.Frames[0].ShouldBe(new Rune('|'));
    }

    /// <summary>Verifies the documented maximum frame count is accepted.</summary>
    [Fact]
    public void Constructor_WhenFrameCountIsMaximum_Succeeds()
    {
        var baseline = SpinnerStyle.Braille;
        var frames = Enumerable.Repeat(new Rune('|'), SpinnerStyle.MaximumFrameCount);

        var actual = new SpinnerStyle(baseline.Face, baseline.Border, baseline.Shadow, frames);

        actual.Frames.Length.ShouldBe(SpinnerStyle.MaximumFrameCount);
    }

    /// <summary>Verifies enumeration stops and throws as soon as the maximum would be exceeded.</summary>
    [Fact]
    public void Constructor_WhenFrameCountExceedsMaximum_ThrowsAtBound()
    {
        var baseline = SpinnerStyle.Braille;
        var enumerated = 0;
        var frames = Enumerable.Repeat(new Rune('|'), SpinnerStyle.MaximumFrameCount + 1000)
            .Select(frame =>
            {
                enumerated++;
                return frame;
            });

        var exception = Should.Throw<ArgumentException>(() =>
            new SpinnerStyle(baseline.Face, baseline.Border, baseline.Shadow, frames));

        exception.ParamName.ShouldBe("frames");
        enumerated.ShouldBe(SpinnerStyle.MaximumFrameCount + 1);
    }

    /// <summary>Verifies empty frame sequences are rejected from the constructor.</summary>
    [Fact]
    public void Constructor_WhenFramesAreEmpty_Throws()
    {
        var baseline = SpinnerStyle.Braille;

        var exception = Should.Throw<ArgumentException>(() =>
            new SpinnerStyle(baseline.Face, baseline.Border, baseline.Shadow, []));

        exception.ParamName.ShouldBe("frames");
    }

    /// <summary>Verifies every frame must be printable and one cell wide.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Constructor_WhenFrameIsWideOrControl_Throws(int scalar)
    {
        var baseline = SpinnerStyle.Braille;

        var exception = Should.Throw<ArgumentException>(() =>
            new SpinnerStyle(baseline.Face, baseline.Border, baseline.Shadow, [new Rune(scalar)]));

        exception.ParamName.ShouldBe("frames");
    }

    /// <summary>Verifies a <c>with</c> expression rejects empty frames too, since Frames
    /// validates in its own init accessor.</summary>
    [Fact]
    public void With_WhenFramesAreEmpty_Throws() =>
        _ = Should.Throw<ArgumentException>(() => SpinnerStyle.Braille with { Frames = [] });

    /// <summary>Verifies a <c>with</c> expression rejects an invalid frame too.</summary>
    [Fact]
    public void With_WhenFrameIsInvalid_Throws() =>
        _ = Should.Throw<ArgumentException>(() => SpinnerStyle.Braille with { Frames = [new Rune(0x4E16)] });

    /// <summary>Verifies equality compares every record member structurally.</summary>
    [Fact]
    public void Equality_WhenEveryMemberMatches_IsEqual()
    {
        var baseline = SpinnerStyle.DenseBraille;
        var equivalent = new SpinnerStyle(baseline.Face, baseline.Border, baseline.Shadow, baseline.Frames);

        equivalent.ShouldBe(baseline);
        equivalent.GetHashCode().ShouldBe(baseline.GetHashCode());
    }

    /// <summary>Verifies frame content genuinely participates in equality - the content-based
    /// comparison that replaces ImmutableArray's handle comparison must still separate two
    /// presentations whose frames differ only in one position, and must not collapse sequences of
    /// different lengths.</summary>
    [Fact]
    public void Equality_WhenFramesDiffer_IsNotEqual()
    {
        var baseline = SpinnerStyle.Braille;
        var sameLengthDifferentContent = baseline with { Frames = [new Rune('|'), new Rune('/')] };
        var differentLength = baseline with { Frames = [new Rune('|')] };
        var sameContent = baseline with { Frames = [new Rune('|'), new Rune('/')] };

        sameLengthDifferentContent.ShouldNotBe(differentLength);
        sameLengthDifferentContent.ShouldNotBe(baseline);
        sameLengthDifferentContent.ShouldBe(sameContent);
    }

    /// <summary>Verifies a presentation differing only outside the frame sequence stays unequal, so
    /// the hand-written comparison still defers to the inherited appearance members.</summary>
    [Fact]
    public void Equality_WhenOnlyBaseAppearanceDiffers_IsNotEqual()
    {
        var baseline = SpinnerStyle.Braille;

        var recolored = baseline with { Face = baseline.Face with { Foreground = Color.Rgb(1, 2, 3) } };

        recolored.ShouldNotBe(baseline);
    }
}
