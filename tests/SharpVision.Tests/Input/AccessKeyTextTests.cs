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

    /// <summary>Verifies an unescaped ampersand inside a markup tag's value (for example a hyperlink
    /// target's query string) is never treated as a mnemonic marker, so the tag survives collapsing
    /// unmodified and its span still resolves to the intended link.</summary>
    [Fact]
    public void ToMarkup_WhenTagContainsUnescapedAmpersandAndNoMnemonicExists_LeavesTagIntact()
    {
        // Arrange
        const string content = "<link=https://x?a=1&b=2>Click here</link>";

        // Act
        var markup = AccessKeyText.ToMarkup(content, useMnemonic: true);

        // Assert
        markup.ShouldBe(content);
        var spans = markup.AsSpan().Parse(out var display);
        display.ShouldBe("Click here");
        spans.ShouldHaveSingleItem().Link.ShouldBe("https://x?a=1&b=2");
    }

    /// <summary>Verifies measuring the same tag+ampersand content is unaffected by mnemonic
    /// collapsing, since the only ampersand present lives inside a tag and is never a marker.</summary>
    [Fact]
    public void Measure_WhenTagContainsUnescapedAmpersandAndNoMnemonicExists_MatchesNonMnemonicMeasurement()
    {
        const string content = "<link=https://x?a=1&b=2>Click here</link>";

        AccessKeyText.Measure(content, Ambiguous.Narrow, useMnemonic: true)
            .ShouldBe(AccessKeyText.Measure(content, Ambiguous.Narrow, useMnemonic: false));
    }

    /// <summary>Verifies drawing the same tag+ampersand content produces the literal tag text with no
    /// underline, proving the in-tag ampersand was never mistaken for a mnemonic marker.</summary>
    [Fact]
    public void Draw_WhenTagContainsUnescapedAmpersandAndNoMnemonicExists_DrawsLiteralTextWithoutUnderline()
    {
        // Arrange
        const string content = "<link=https://x?a=1&b=2>Click here</link>";
        using Frame frame = new(new Size(content.Length, 1));

        // Act
        var cells = content.Draw(
            frame.Canvas,
            default,
            TerminalStyle.Default,
            BackgroundMode.Transparent,
            Ambiguous.Narrow,
            useMnemonic: true);

        // Assert
        cells.ShouldBe(content.Length);
        FrameOracle.Get(frame, default).ShouldBe("<");
        frame.GetCell(default).Style.Attributes.ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies an in-tag ampersand never produces a reported mnemonic key, matching
    /// <see cref="ControlBase.MatchesAccessKey"/>'s direct use of this scan for hotkey dispatch.</summary>
    [Fact]
    public void TryGetKey_WhenAmpersandIsInsideMarkupTag_ReportsNoMnemonic()
    {
        // Arrange
        const string content = "<link=https://x?a=1&b=2>Click here</link>";

        // Act
        var found = AccessKeyText.TryGetKey(content, out _);

        // Assert
        found.ShouldBeFalse();
    }

    /// <summary>Verifies a real mnemonic marker outside a tag is still found, and that finding it
    /// does not disturb the tag's own unescaped ampersand - proving the fix only special-cases
    /// ampersands that fall inside an open tag span.</summary>
    [Fact]
    public void TryGetKey_WhenMnemonicIsOutsideMarkupTag_ReportsMarkedScalar()
    {
        // Arrange
        const string content = "&Open <link=https://x?a=1&b=2>here</link>";

        // Act
        var found = AccessKeyText.TryGetKey(content, out var key);

        // Assert
        found.ShouldBeTrue();
        key.ShouldBe(new Rune('O'));
    }

    /// <summary>Verifies markup for a real outside-tag mnemonic underlines only the marked letter and
    /// leaves the tag's own unescaped ampersand and link target completely untouched.</summary>
    [Fact]
    public void ToMarkup_WhenMnemonicIsOutsideMarkupTag_UnderlinesMarkerAndLeavesTagIntact()
    {
        // Arrange
        const string content = "&Open <link=https://x?a=1&b=2>here</link>";

        // Act
        var markup = AccessKeyText.ToMarkup(content, useMnemonic: true);

        // Assert
        markup.ShouldBe("<u>O</u>pen <link=https://x?a=1&b=2>here</link>");
        var spans = markup.AsSpan().Parse(out var display);
        display.ShouldBe("Open here");
        spans.Length.ShouldBe(3);
        spans[0].Underline.ShouldBe(Underline.Straight);
        spans[0].Length.ShouldBe(1);
        spans[2].Link.ShouldBe("https://x?a=1&b=2");
    }
}
