// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies KeyGesture construction validation and conventional display formatting.</summary>
public sealed class KeyGestureTests
{
    /// <summary>Verifies a named-code gesture with no modifiers formats as the bare code name.</summary>
    [Fact]
    public void ToString_WhenNoModifiersAreSet_FormatsBareCodeName()
    {
        var gesture = new KeyGesture(Code.F5);

        gesture.ToString().ShouldBe("F5");
    }

    /// <summary>Verifies modifiers format in conventional Ctrl/Alt/Shift order before the key.</summary>
    [Fact]
    public void ToString_WhenModifiersAreCombined_FormatsInConventionalOrder()
    {
        var gesture = new KeyGesture(Code.Character, Modifiers.Shift | Modifiers.Alt | Modifiers.Control, new Rune('s'));

        gesture.ToString().ShouldBe("Ctrl+Alt+Shift+S");
    }

    /// <summary>Verifies a character gesture formats its character uppercased.</summary>
    [Fact]
    public void ToString_WhenCodeIsCharacter_FormatsUppercasedCharacter()
    {
        var gesture = new KeyGesture(Code.Character, Modifiers.Control, new Rune('q'));

        gesture.ToString().ShouldBe("Ctrl+Q");
    }

    /// <summary>Verifies the constructor rejects an undefined code.</summary>
    [Fact]
    public void Constructor_WhenCodeIsUndefined_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new KeyGesture((Code) (-1)));

    /// <summary>Verifies the constructor rejects the unmapped native-key sentinel, which can never form a real chord.</summary>
    [Fact]
    public void Constructor_WhenCodeIsUnknown_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new KeyGesture(Code.Unknown));

    /// <summary>Verifies the constructor rejects an undefined modifier flag.</summary>
    [Fact]
    public void Constructor_WhenModifiersContainUndefinedFlags_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new KeyGesture(Code.F1, (Modifiers) (1 << 20)));

    /// <summary>Verifies a character code without a character throws.</summary>
    [Fact]
    public void Constructor_WhenCharacterCodeHasNoCharacter_Throws() =>
        _ = Should.Throw<ArgumentException>(() => new KeyGesture(Code.Character));

    /// <summary>Verifies a non-character code with a character throws.</summary>
    [Fact]
    public void Constructor_WhenNonCharacterCodeHasCharacter_Throws() =>
        _ = Should.Throw<ArgumentException>(() => new KeyGesture(Code.Enter, character: new Rune('x')));

    /// <summary>Verifies value equality compares code, modifiers, and character.</summary>
    [Fact]
    public void Equals_WhenMembersMatch_IsEqual()
    {
        var first = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'));
        var second = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'));

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies CapsLock and NumLock are stripped from the stored, compared modifier set.</summary>
    [Fact]
    public void Constructor_WhenModifiersIncludeLockKeys_StripsThemFromTheStoredValue()
    {
        var withLocks = new KeyGesture(Code.F5, Modifiers.Control | Modifiers.CapsLock | Modifiers.NumLock);
        var withoutLocks = new KeyGesture(Code.F5, Modifiers.Control);

        withLocks.Modifiers.ShouldBe(Modifiers.Control);
        withLocks.ShouldBe(withoutLocks);
        withLocks.GetHashCode().ShouldBe(withoutLocks.GetHashCode());
    }

    /// <summary>Verifies two gestures that differ only in character case compare and hash equal.</summary>
    [Fact]
    public void Equals_WhenCharacterCaseDiffers_IsEqual()
    {
        var lower = new KeyGesture(Code.Character, Modifiers.Control, new Rune('s'));
        var upper = new KeyGesture(Code.Character, Modifiers.Control, new Rune('S'));

        lower.ShouldBe(upper);
        lower.GetHashCode().ShouldBe(upper.GetHashCode());
        lower.ToString().ShouldBe(upper.ToString());
    }

    /// <summary>
    /// Verifies a bare modifier-key gesture matches a stroke for that same key, even though
    /// pressing the key alone already sets its own matching <see cref="Modifiers"/> flag.
    /// </summary>
    [Fact]
    public void Matches_WhenCodeIsBareModifierKey_IgnoresTheKeysOwnModifierFlag()
    {
        var gesture = new KeyGesture(Code.LeftShift);
        var stroke = new Stroke(Code.LeftShift, null, 57441, Modifiers.Shift, KeyAction.Press);

        gesture.Matches(stroke).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a bare modifier-key gesture still requires exact equality on any other modifier:
    /// only the key's own flag is excluded from the comparison, not every modifier.
    /// </summary>
    [Fact]
    public void Matches_WhenBareModifierKeyStrokeCarriesAnUnrelatedModifier_DoesNotMatch()
    {
        var gesture = new KeyGesture(Code.LeftShift);
        var stroke = new Stroke(Code.LeftShift, null, 57441, Modifiers.Shift | Modifiers.Control, KeyAction.Press);

        gesture.Matches(stroke).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies a modifier-key gesture that itself requires an additional modifier still matches once
    /// the key's own self-set flag is excluded, leaving only the required modifier to compare.
    /// </summary>
    [Fact]
    public void Matches_WhenBareModifierKeyGestureRequiresAnotherModifier_MatchesOnceOwnFlagIsExcluded()
    {
        var gesture = new KeyGesture(Code.LeftShift, Modifiers.Control);
        var stroke = new Stroke(Code.LeftShift, null, 57441, Modifiers.Shift | Modifiers.Control, KeyAction.Press);

        gesture.Matches(stroke).ShouldBeTrue();
    }

    /// <summary>
    /// Verifies the ISO Level3/Level5 Shift codes - which have no corresponding <see cref="Modifiers"/>
    /// flag - compare modifiers exactly like any ordinary named key, since no self-conflict is possible.
    /// </summary>
    [Fact]
    public void Matches_WhenCodeIsIsoLevelShift_ComparesModifiersExactly()
    {
        var gesture = new KeyGesture(Code.IsoLevel3Shift);
        var matchingStroke = new Stroke(Code.IsoLevel3Shift, null, 57453, Modifiers.None, KeyAction.Press);
        var nonMatchingStroke = new Stroke(Code.IsoLevel3Shift, null, 57453, Modifiers.Control, KeyAction.Press);

        gesture.Matches(matchingStroke).ShouldBeTrue();
        gesture.Matches(nonMatchingStroke).ShouldBeFalse();
    }

    /// <summary>
    /// Verifies an ordinary non-modifier-key gesture is unaffected by the self-modifier exclusion:
    /// it still requires exact modifier equality.
    /// </summary>
    [Fact]
    public void Matches_WhenCodeIsNotAModifierKey_ComparesModifiersExactly()
    {
        var gesture = new KeyGesture(Code.F5, Modifiers.Control);
        var matchingStroke = new Stroke(Code.F5, null, 0, Modifiers.Control, KeyAction.Press);
        var nonMatchingStroke = new Stroke(Code.F5, null, 0, Modifiers.Control | Modifiers.Shift, KeyAction.Press);

        gesture.Matches(matchingStroke).ShouldBeTrue();
        gesture.Matches(nonMatchingStroke).ShouldBeFalse();
    }
}
