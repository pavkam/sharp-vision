// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Runtime;

using SharpVision.Terminal.Kitty.Keyboard;

/// <summary>Verifies terminal session option validation at the public boundary.</summary>
public sealed class TerminalOptionsTests
{
    /// <summary>Verifies an undefined enhancement bit is rejected at construction instead of
    /// surfacing later from <c>Kitty.Keyboard.Keyboard.Validate</c> with a parameter name the
    /// caller never wrote.</summary>
    [Fact]
    public void Keyboard_WhenValueHasUnknownBits_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new TerminalOptions { Keyboard = (KittyKeyboardEnhancement) 64 });

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies AssociatedText without AllKeys is rejected at construction.</summary>
    [Fact]
    public void Keyboard_WhenAssociatedTextIsSetWithoutAllKeys_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            new TerminalOptions { Keyboard = KittyKeyboardEnhancement.AssociatedText });

        exception.ShouldNotBeOfType<ArgumentOutOfRangeException>();
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies AssociatedText paired with AllKeys is accepted.</summary>
    [Fact]
    public void Keyboard_WhenAssociatedTextIsPairedWithAllKeys_DoesNotThrow()
    {
        var options = Should.NotThrow(() =>
            new TerminalOptions { Keyboard = KittyKeyboardEnhancement.AllKeys | KittyKeyboardEnhancement.AssociatedText });

        options.Keyboard.ShouldBe(KittyKeyboardEnhancement.AllKeys | KittyKeyboardEnhancement.AssociatedText);
    }

    /// <summary>Verifies disabling the keyboard lease entirely remains accepted.</summary>
    [Fact]
    public void Keyboard_WhenNull_DoesNotThrow()
    {
        var options = Should.NotThrow(() => new TerminalOptions { Keyboard = null });

        options.Keyboard.ShouldBeNull();
    }

    /// <summary>Verifies undefined promotion bits are rejected at the terminal option boundary.</summary>
    [Fact]
    public void DiagnosticPromotions_WhenValueHasUnknownBits_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            new TerminalOptions { DiagnosticPromotions = (DiagnosticPromotion) 32 });

        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies an unbounded or non-positive clipboard deadline is rejected at the
    /// terminal-session policy boundary.</summary>
    [Fact]
    public void ClipboardOperationTimeout_WhenValueIsNotPositiveAndFinite_ThrowsArgumentOutOfRangeException()
    {
        var zero = Should.Throw<ArgumentOutOfRangeException>(() =>
            new TerminalOptions { ClipboardOperationTimeout = TimeSpan.Zero });
        var infinite = Should.Throw<ArgumentOutOfRangeException>(() =>
            new TerminalOptions { ClipboardOperationTimeout = Timeout.InfiniteTimeSpan });

        zero.ParamName.ShouldBe("value");
        infinite.ParamName.ShouldBe("value");
    }
}
