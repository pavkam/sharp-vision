// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

using Shouldly;

/// <summary>Verifies semantic terminal style validation for modern decorations.</summary>
public sealed class StyleTests
{
    /// <summary>Verifies a complete modern style preserves typed decoration state.</summary>
    [Fact]
    public void Constructor_WhenDecorationsAreValid_PreservesValues()
    {
        Style style = new Style(
            attributes: Attributes.RapidBlink | Attributes.Overline,
            underline: Underline.Curly,
            underlineColor: Color.Rgb(1, 2, 3));

        style.Attributes.ShouldBe(Attributes.RapidBlink | Attributes.Overline);
        style.Underline.ShouldBe(Underline.Curly);
        style.UnderlineColor.ShouldBe(Color.Rgb(1, 2, 3));
    }

    /// <summary>Verifies conflicting or invisible decoration state fails before construction.</summary>
    [Fact]
    public void Constructor_WhenDecorationsConflict_ThrowsDocumentedException()
    {
        _ = Should.Throw<ArgumentException>(() =>
            new Style(attributes: Attributes.Underline, underline: Underline.Curly));
        _ = Should.Throw<ArgumentException>(() =>
            new Style(attributes: Attributes.Blink | Attributes.RapidBlink));
        _ = Should.Throw<ArgumentException>(() =>
            new Style(underlineColor: Color.Indexed(1)));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            new Style(underline: (Underline) 999));
    }
}
