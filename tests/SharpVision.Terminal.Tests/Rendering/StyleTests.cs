// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

/// <summary>Verifies semantic terminal style validation for modern decorations.</summary>
public sealed class StyleTests
{
    /// <summary>Verifies a complete modern style preserves typed decoration state.</summary>
    [Fact]
    public void Constructor_WhenDecorationsAreValid_PreservesValues()
    {
        var style = new CellStyle(
            attributes: TerminalAttributes.RapidBlink | TerminalAttributes.Overline,
            underline: Underline.Curly,
            underlineColor: Color.Rgb(1, 2, 3));

        style.Attributes.ShouldBe(TerminalAttributes.RapidBlink | TerminalAttributes.Overline);
        style.Underline.ShouldBe(Underline.Curly);
        style.UnderlineColor.ShouldBe(Color.Rgb(1, 2, 3));
    }

    /// <summary>Verifies conflicting or invisible decoration state fails before construction.</summary>
    [Fact]
    public void Constructor_WhenDecorationsConflict_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentException>(() =>
            new CellStyle(attributes: TerminalAttributes.Underline, underline: Underline.Curly));
        _ = Should.Throw<ArgumentException>(() =>
            new CellStyle(attributes: TerminalAttributes.Blink | TerminalAttributes.RapidBlink));
        _ = Should.Throw<ArgumentException>(() =>
            new CellStyle(underlineColor: ReferenceColors.Get(1)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new CellStyle(underline: (Underline) 999));
    }

    /// <summary>Verifies transparent foreground is rejected.</summary>
    [Fact]
    public void Constructor_WhenForegroundIsTransparent_Throws() =>
        Should.Throw<ArgumentException>(() => new CellStyle(foreground: Color.Transparent));

    /// <summary>Verifies transparency is accepted only for a composited background channel.</summary>
    [Fact]
    public void Constructor_WhenColorIsTransparent_RejectsNonBackgroundChannels()
    {
        new CellStyle(background: Color.Transparent).Background.ShouldBe(Color.Transparent);
        _ = Should.Throw<ArgumentException>(() => new CellStyle(foreground: Color.Transparent));
        _ = Should.Throw<ArgumentException>(() => new CellStyle(
            underline: Underline.Straight,
            underlineColor: Color.Transparent));
    }
}
