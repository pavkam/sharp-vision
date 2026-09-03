// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies detached same-value property guards, disabled routed-input rejection, and appearance
/// fragment cloning for the basic input controls and their glyph families.</summary>
public sealed class InputGlyphAndPropertyConditionTests
{
    /// <summary>Verifies reassigning the same GroupName publishes nothing and keeps the selection.</summary>
    [Fact]
    public void GroupName_WhenReassignedTheSameValue_DoesNotNotify()
    {
        // Arrange
        var radio = new RadioButton { GroupName = "options", IsChecked = true };
        var notifications = 0;
        radio.PropertyChanged += (_, _) => notifications++;
        radio.Unchecked += (_, _) => notifications++;

        // Act
        radio.GroupName = "options";

        // Assert
        notifications.ShouldBe(0);
        radio.IsChecked.ShouldBeTrue();
        radio.GroupName.ShouldBe("options");
    }

    /// <summary>Verifies reassigning the same ThreeState value publishes nothing.</summary>
    /// <param name="threeState">The value assigned twice.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThreeState_WhenReassignedTheSameValue_DoesNotNotify(bool threeState)
    {
        // Arrange
        var checkBox = new CheckBox { ThreeState = threeState };
        var notifications = 0;
        checkBox.PropertyChanged += (_, _) => notifications++;
        checkBox.StateChanged += (_, _) => notifications++;

        // Act
        checkBox.ThreeState = threeState;

        // Assert
        notifications.ShouldBe(0);
        checkBox.ThreeState.ShouldBe(threeState);
    }

    /// <summary>Verifies a disabled Slider leaves a routed command key unhandled and its value unchanged.</summary>
    [Fact]
    public void Route_WhenSliderIsDisabled_IgnoresCommandKeys()
    {
        // Arrange
        var slider = new Slider { Maximum = 100, Value = 50, IsEnabled = false };
        var key = Key(Code.End);

        // Act
        _ = Router.Route(slider, Events.Key, key);

        // Assert
        key.IsHandled.ShouldBeFalse();
        slider.Value.ShouldBe(50);
    }

    /// <summary>Verifies a disabled ScrollBar leaves a routed command key unhandled and its value unchanged.</summary>
    [Fact]
    public void Route_WhenScrollBarIsDisabled_IgnoresCommandKeys()
    {
        // Arrange
        var bar = new ScrollBar { Maximum = 100, Value = 50, IsEnabled = false };
        var key = Key(Code.End);

        // Act
        _ = Router.Route(bar, Events.Key, key);

        // Assert
        key.IsHandled.ShouldBeFalse();
        bar.Value.ShouldBe(50);
    }

    /// <summary>Verifies every glyph family clones through the appearance-fragment seam as an equal value.</summary>
    [Fact]
    public void Clone_WhenGlyphFamilyIsCloned_ProducesEqualValue()
    {
        // Arrange
        var checkBox = new CheckBoxGlyphs(new Rune('-'), new Rune('x'), new Rune('?'));
        var radio = new RadioButtonGlyphs(new Rune('o'), new Rune('*'));
        var slider = new SliderGlyphs(new Rune('.'), new Rune('='), new Rune(':'), new Rune('#'), new Rune('T'));
        var scrollBar = new ScrollBarGlyphs(
            new Rune('^'),
            new Rune('v'),
            new Rune('<'),
            new Rune('>'),
            new Rune('.'),
            new Rune('#'),
            new Rune('-'),
            new Rune('='),
            new Rune('|'),
            new Rune('%'));

        // Act
        var checkBoxClone = ((IAppearanceFragment) checkBox).Clone();
        var radioClone = ((IAppearanceFragment) radio).Clone();
        var sliderClone = ((IAppearanceFragment) slider).Clone();
        var scrollBarClone = ((IAppearanceFragment) scrollBar).Clone();

        // Assert
        checkBoxClone.ShouldBe(checkBox);
        radioClone.ShouldBe(radio);
        sliderClone.ShouldBe(slider);
        scrollBarClone.ShouldBe(scrollBar);
        ((SliderGlyphs) sliderClone).VerticalFillGlyph.Value.ShouldBe(new Rune('#'));
        ((SliderGlyphs) sliderClone).VerticalFillGlyph.Fallback.ShouldBe(new Rune('='));
    }

    private static KeyEventArgs Key(Code code) => new(new Stroke(
        code,
        character: null,
        nativeCode: 0,
        Modifiers.None,
        KeyAction.Press));
}
