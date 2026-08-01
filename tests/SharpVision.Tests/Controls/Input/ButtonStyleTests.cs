// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;


/// <summary>Verifies immutable Button presentation values and partial style composition.</summary>
public sealed class ButtonStyleTests
{
    /// <summary>Verifies default control styles use their role profiles and the radio checked accent.</summary>
    [Fact]
    public void Default_WhenStylesResolve_UsesSharedRoleProfiles()
    {
        ButtonStyle.Standard.Appearance.ShouldBeSameAs(ControlStyleProfiles.Control);
        CheckBoxStyle.Default.Appearance.ShouldBeSameAs(ControlStyleProfiles.Selection);
        RadioButtonStyle.Default.Appearance.Normal.ShouldBe(ControlStyleProfiles.Selection.Normal);
        RadioButtonStyle.Default.Appearance.Checked.Face.ShouldBe(
            new FaceSet(foreground: ThemeColor.Accent));
        ScrollBarStyle.Default.Appearance.ShouldBeSameAs(ControlStyleProfiles.Control);
        SpinnerStyle.Default.Appearance.ShouldBeSameAs(ControlStyleProfiles.Control);
        ChaseIndicatorStyle.Default.Appearance.ShouldBeSameAs(ControlStyleProfiles.Control);
    }

    /// <summary>Verifies the zero-initialized value resolves to the complete standard preset.</summary>
    [Fact]
    public void Standard_WhenStyleIsDefault_IsSemanticallyEquivalent()
    {
        var actual = default(ButtonStyle);

        actual.ShouldBe(ButtonStyle.Standard);
        actual.Padding.ShouldBe(new Thickness(horizontal: 1, vertical: 0));
        actual.Appearance.ShouldBeSameAs(ButtonStyle.Standard.Appearance);
    }

    /// <summary>Verifies the filled preset preserves the established compact fractional-shadow recipe.</summary>
    [Fact]
    public void Filled_WhenResolved_UsesCompactFractionalProfile()
    {
        var actual = ButtonStyle.Filled;

        actual.Padding.ShouldBe(new Thickness(horizontal: 2, vertical: 0));
        actual.Appearance.Normal.Border.Sides.ShouldBe(BorderSide.None);
        actual.Appearance.Normal.Shadow.IsVisible.ShouldBeTrue();
        actual.Appearance.Normal.Shadow.Mode.ShouldBe(ShadowMode.FractionalBlock);
        actual.Appearance.Normal.Shadow.Offset.ShouldBe(new Point(1, 1));
    }

    /// <summary>Verifies equality and hashing compare resolved semantic values rather than backing representation.</summary>
    [Fact]
    public void Equality_WhenProfilesAreEquivalent_IsConsistent()
    {
        var equivalent = new ButtonStyle(
            new Thickness(horizontal: 1, vertical: 0),
            Copy(ButtonStyle.Standard.Appearance));

        var actual = default(ButtonStyle);

        actual.Equals(equivalent).ShouldBeTrue();
        actual.Equals((object) equivalent).ShouldBeTrue();
        (actual == equivalent).ShouldBeTrue();
        (actual != equivalent).ShouldBeFalse();
        actual.GetHashCode().ShouldBe(equivalent.GetHashCode());
        actual.ShouldNotBe(ButtonStyle.Filled);
    }

    /// <summary>Verifies a supplied padding contribution does not replace the complete appearance profile.</summary>
    [Fact]
    public void Apply_WhenOnlyPaddingIsSupplied_PreservesAppearance()
    {
        var baseline = ButtonStyle.Filled;
        var set = new ButtonStyleSet(padding: new Thickness(horizontal: 3, vertical: 1));

        var actual = set.Apply(baseline);

        actual.Padding.ShouldBe(new Thickness(horizontal: 3, vertical: 1));
        actual.Appearance.ShouldBeSameAs(baseline.Appearance);
    }

    /// <summary>Verifies a partial appearance contribution composes without replacing padding or omitted members.</summary>
    [Fact]
    public void Apply_WhenOnlyAppearanceIsSupplied_ComposesProfile()
    {
        var baseline = ButtonStyle.Standard;
        var set = new ButtonStyleSet(
            appearance: new AppearanceProfileSet(
                pointerOver: new AppearanceSet(
                    face: new FaceSet(background: ThemeColor.ActiveControl))));

        var actual = set.Apply(baseline);

        actual.Padding.ShouldBe(baseline.Padding);
        actual.Appearance.Normal.ShouldBe(baseline.Appearance.Normal);
        actual.Appearance.PointerOver.Face.ShouldNotBeNull().Background.ShouldBe(ThemeColor.ActiveControl);
        actual.Appearance.PointerOver.Border.ShouldBe(baseline.Appearance.PointerOver.Border);
    }

    /// <summary>Verifies validation considers combinations of otherwise-valid visual states.</summary>
    [Fact]
    public void Constructor_WhenCombinedStatesEnableBorderAndShadow_Throws()
    {
        var profile = new ThemeProfile(
            new ThemeAppearance(
                AppearanceTestValues.Face(),
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)),
            pointerOver: new AppearanceSet(border: new BorderSet(sides: BorderSide.All)),
            focused: new AppearanceSet(shadow: new ShadowSet(isVisible: true)));

        var exception = Should.Throw<ArgumentException>(() =>
            new ButtonStyle(new Thickness(1, 0), profile));

        exception.ParamName.ShouldBe("appearance");
    }

    /// <summary>Verifies construction rejects a missing complete appearance profile.</summary>
    [Fact]
    public void Constructor_WhenAppearanceIsNull_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            new ButtonStyle(new Thickness(1, 0), null!));

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
