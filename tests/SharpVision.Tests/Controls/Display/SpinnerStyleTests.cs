// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Display;

/// <summary>Verifies complete spinner presentations and partial style composition.</summary>
public sealed class SpinnerStyleTests
{
    /// <summary>Verifies the zero-initialized style resolves to the established ten-frame Braille recipe.</summary>
    [Fact]
    public void Braille_WhenResolved_ContainsTenFrames()
    {
        var actual = default(SpinnerStyle);

        actual.ShouldBe(SpinnerStyle.Braille);
        actual.Frames.Length.ShouldBe(10);
        actual.Frames[0].ShouldBe(new Rune('⠋'));
        actual.Frames[^1].ShouldBe(new Rune('⠏'));
    }

    /// <summary>Verifies construction copies caller-owned mutable frame storage.</summary>
    [Fact]
    public void Constructor_WhenCallerMutatesFrames_RetainsImmutableCopy()
    {
        var frames = new[] { new Rune('|'), new Rune('/') };
        var style = new SpinnerStyle(frames, SpinnerStyle.Braille.Appearance);

        frames[0] = new Rune('x');

        style.Frames[0].ShouldBe(new Rune('|'));
    }

    /// <summary>Verifies the documented maximum frame count is accepted.</summary>
    [Fact]
    public void Constructor_WhenFrameCountIsMaximum_Succeeds()
    {
        var frames = Enumerable.Repeat(new Rune('|'), SpinnerStyle.MaximumFrameCount);

        var actual = new SpinnerStyle(frames, SpinnerStyle.Braille.Appearance);

        actual.Frames.Length.ShouldBe(SpinnerStyle.MaximumFrameCount);
    }

    /// <summary>Verifies enumeration stops and throws as soon as the maximum would be exceeded.</summary>
    [Fact]
    public void Constructor_WhenFrameCountExceedsMaximum_ThrowsAtBound()
    {
        var enumerated = 0;
        var frames = Enumerable.Repeat(new Rune('|'), SpinnerStyle.MaximumFrameCount + 1000)
            .Select(frame =>
            {
                enumerated++;
                return frame;
            });

        var exception = Should.Throw<ArgumentException>(() =>
            new SpinnerStyle(frames, SpinnerStyle.Braille.Appearance));

        exception.ParamName.ShouldBe("frames");
        enumerated.ShouldBe(SpinnerStyle.MaximumFrameCount + 1);
    }

    /// <summary>Verifies empty frame sequences are rejected.</summary>
    [Fact]
    public void Constructor_WhenFramesAreEmpty_Throws()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SpinnerStyle([], SpinnerStyle.Braille.Appearance));

        exception.ParamName.ShouldBe("frames");
    }

    /// <summary>Verifies every frame must be printable and one cell wide.</summary>
    [Theory]
    [InlineData(0x4E16)]
    [InlineData(0)]
    public void Constructor_WhenFrameIsWideOrControl_Throws(int scalar)
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SpinnerStyle([new Rune(scalar)], SpinnerStyle.Braille.Appearance));

        exception.ParamName.ShouldBe("frames");
    }

    /// <summary>Verifies a partial frame contribution preserves the complete appearance.</summary>
    [Fact]
    public void Apply_WhenOnlyFramesAreSupplied_PreservesAppearance()
    {
        var baseline = SpinnerStyle.Braille;
        var set = new SpinnerStyleSet(frames: [new Rune('|'), new Rune('-')]);

        var actual = set.Apply(baseline);

        actual.Frames.ShouldBe([new Rune('|'), new Rune('-')]);
        actual.Appearance.ShouldBeSameAs(baseline.Appearance);
    }

    /// <summary>Verifies partial style construction copies caller-owned mutable frame storage.</summary>
    [Fact]
    public void StyleSet_WhenCallerMutatesFrames_RetainsImmutableCopy()
    {
        var frames = new[] { new Rune('|'), new Rune('/') };
        var set = new SpinnerStyleSet(frames: frames);

        frames[0] = new Rune('x');

        set.Frames.ShouldNotBeNull()[0].ShouldBe(new Rune('|'));
    }

    /// <summary>Verifies partial style construction rejects empty frames immediately.</summary>
    [Fact]
    public void StyleSet_WhenFramesAreEmpty_ThrowsAtConstruction()
    {
        var exception = Should.Throw<ArgumentException>(() => new SpinnerStyleSet(frames: []));

        exception.ParamName.ShouldBe("frames");
    }

    /// <summary>Verifies partial style construction rejects invalid frames immediately.</summary>
    [Fact]
    public void StyleSet_WhenFrameIsInvalid_ThrowsAtConstruction()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new SpinnerStyleSet(frames: [new Rune(0x4E16)]));

        exception.ParamName.ShouldBe("frames");
    }

    /// <summary>Verifies partial frame enumeration is bounded at the same documented maximum.</summary>
    [Fact]
    public void StyleSet_WhenFrameCountExceedsMaximum_ThrowsAtBound()
    {
        var enumerated = 0;
        var frames = Enumerable.Repeat(new Rune('|'), SpinnerStyle.MaximumFrameCount + 1000)
            .Select(frame =>
            {
                enumerated++;
                return frame;
            });

        var exception = Should.Throw<ArgumentException>(() => new SpinnerStyleSet(frames: frames));

        exception.ParamName.ShouldBe("frames");
        enumerated.ShouldBe(SpinnerStyle.MaximumFrameCount + 1);
    }

    /// <summary>Verifies partial style construction accepts the documented maximum frame count.</summary>
    [Fact]
    public void StyleSet_WhenFrameCountIsMaximum_Succeeds()
    {
        var frames = Enumerable.Repeat(new Rune('|'), SpinnerStyle.MaximumFrameCount);

        var actual = new SpinnerStyleSet(frames: frames);

        actual.Frames.ShouldNotBeNull().Length.ShouldBe(SpinnerStyle.MaximumFrameCount);
    }

    /// <summary>Verifies separately allocated equivalent frame sequences compare and hash equally.</summary>
    [Fact]
    public void StyleSetEquality_WhenFramesAreEquivalent_IsSemantic()
    {
        var left = new SpinnerStyleSet(frames: new[] { new Rune('|'), new Rune('/') });
        var right = new SpinnerStyleSet(frames: new[] { new Rune('|'), new Rune('/') });

        left.Equals(right).ShouldBeTrue();
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    /// <summary>Verifies an absent frame contribution differs from a present sequence.</summary>
    [Fact]
    public void StyleSetEquality_WhenFramesAreAbsentAndPresent_IsDifferent()
    {
        var absent = default(SpinnerStyleSet);
        var present = new SpinnerStyleSet(frames: [new Rune('|')]);

        absent.Equals(present).ShouldBeFalse();
        (absent != present).ShouldBeTrue();
    }

    /// <summary>Verifies appearance contributions compose while preserving frames.</summary>
    [Fact]
    public void Apply_WhenAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = SpinnerStyle.Ascii;
        var set = new SpinnerStyleSet(
            appearance: new AppearanceProfileSet(
                normal: new AppearanceSet(face: new FaceSet(foreground: ThemeColor.Accent))));

        var actual = set.Apply(baseline);

        actual.Frames.ShouldBe(baseline.Frames);
        actual.Appearance.Normal.Face.Foreground.ShouldBe(ThemeColor.Accent);
    }

    /// <summary>Verifies equivalent sequences and profiles compare semantically.</summary>
    [Fact]
    public void Equality_WhenValuesAreEquivalent_IsSemantic()
    {
        var baseline = SpinnerStyle.DenseBraille;
        var equivalent = new SpinnerStyle(baseline.Frames, Copy(baseline.Appearance));

        equivalent.ShouldBe(baseline);
        equivalent.GetHashCode().ShouldBe(baseline.GetHashCode());
    }

    /// <summary>Verifies a missing complete appearance is rejected.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new SpinnerStyle([new Rune('|')], null!));

        exception.ParamName.ShouldBe("appearance");
    }

    private static ThemeProfile Copy(ThemeProfile profile) => new(
        profile.Normal,
        profile.PointerOver,
        profile.FocusWithin,
        profile.Focused,
        profile.Current,
        profile.Selected,
        profile.Checked,
        profile.Indeterminate,
        profile.Pressed,
        profile.Disabled);
}
