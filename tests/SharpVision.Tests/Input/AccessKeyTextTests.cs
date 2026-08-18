// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies allocation-bounded access-text parsing, measurement, and semantic drawing.</summary>
public sealed class AccessKeyTextTests
{
    /// <summary>Verifies escaped ampersands are literals and the first unescaped marker owns the key.</summary>
    [Fact]
    public void TryGetKey_WhenMarkerIsEscaped_UsesFirstUnescapedScalar()
    {
        // Arrange
        const string caption = "Fish && &界";

        // Act
        var found = AccessKeyText.TryGetKey(caption, out var key);

        // Assert
        found.ShouldBeTrue();
        key.ShouldBe(new Rune('界'));
    }

    /// <summary>Verifies marker, escape, wide-key, and trailing-ampersand syntax measures visible cells only.</summary>
    [Fact]
    public void Measure_WhenAmpersandsAreMarkedOrEscaped_UsesVisibleCells()
    {
        AccessKeyText.Measure("&界 && X&", Ambiguous.Narrow, useMnemonic: true).ShouldBe(7);
        AccessKeyText.Measure("&界 && X&", Ambiguous.Narrow, useMnemonic: false).ShouldBe(9);
    }

    /// <summary>Verifies drawing underlines a complete combining grapheme and collapses an escaped ampersand.</summary>
    [Fact]
    public void Draw_WhenMnemonicStartsCombiningGrapheme_UnderlinesCompleteGrapheme()
    {
        // Arrange
        using Frame frame = new(new Size(12, 1));

        // Act
        var cells = "&e\u0301dit && close".Draw(
            frame.Canvas,
            default,
            TerminalStyle.Default,
            BackgroundMode.Transparent,
            Ambiguous.Narrow,
            useMnemonic: true);

        // Assert
        cells.ShouldBe(12);
        FrameOracle.Get(frame, default).ShouldBe("e\u0301");
        frame.GetCell(default).Style.Attributes.ShouldBe(TerminalAttributes.Underline);
        FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("&");
        frame.GetCell(new Point(5, 0)).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies markup gives only the complete marked grapheme the access-key semantic foreground.</summary>
    [Fact]
    public void ToMarkup_WhenMnemonicIsHighlighted_UsesAccessKeyForegroundAndUnderline()
    {
        // Without a hotkeyColor, the markup wraps the mnemonic in underline only.
        AccessKeyText.ToMarkup("&e\u0301dit", useMnemonic: true)
            .ShouldBe("<u>e\u0301</u>dit");

        // With a hotkeyColor, the markup wraps the mnemonic in an fg tag as well.
        AccessKeyText.ToMarkup("&e\u0301dit", useMnemonic: true, hotkeyColor: Color.Rgb(0xff, 0xff, 0x00))
            .ShouldBe("<fg=#ffff00><u>e\u0301</u></fg>dit");
    }
}
