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
}
