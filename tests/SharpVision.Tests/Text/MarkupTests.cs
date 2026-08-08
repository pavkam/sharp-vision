// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

/// <summary>Verifies inline markup parsing into visible text and semantic style spans.</summary>
public sealed class MarkupTests
{
    /// <summary>Verifies unmarked text produces one inheriting span over the visible text.</summary>
    [Fact]
    public void Parse_WhenTextIsPlain_YieldsOneInheritingSpan()
    {
        var spans = "hello".Parse(out var display);

        display.ShouldBe("hello");
        spans.Length.ShouldBe(1);
        spans[0].Offset.ShouldBe(0);
        spans[0].Length.ShouldBe(5);
        spans[0].Foreground.ShouldBeNull();
        spans[0].Attributes.ShouldBe(TerminalAttributes.None);
        spans[0].Link.ShouldBeNull();
    }

    /// <summary>Verifies an empty source produces no visible text or meaningless zero-length span.</summary>
    [Fact]
    public void Parse_WhenTextIsEmpty_YieldsNoSpans()
    {
        var spans = string.Empty.Parse(out var display);

        display.ShouldBeEmpty();
        spans.ShouldBeEmpty();
    }

    /// <summary>Verifies named and explicit foreground tags resolve RGB colors.</summary>
    [Fact]
    public void Parse_WhenColorsAreMarked_ResolvesEveryValueForm()
    {
        var spans = "<red>a</red><fg=#f80>b</fg>".Parse(out var display);

        display.ShouldBe("ab");
        spans.Length.ShouldBe(2);
        spans[0].Foreground.ShouldBe(ReferenceColors.Get(1));
        spans[1].Foreground.ShouldBe(Color.Rgb(255, 136, 0));
    }

    /// <summary>Verifies unrecognized semantic color tags pass through as literal text.</summary>
    [Fact]
    public void Parse_WhenUnknownColorTagsAreUsed_PassesThroughAsLiteralText()
    {
        var spans = "<fg=#ff8800>hello</fg>".Parse(out var display);

        display.ShouldBe("hello");
        spans.Length.ShouldBe(1);
        spans[0].Foreground.ShouldBe(Color.Rgb(0xff, 0x88, 0x00));
    }

    /// <summary>Verifies every documented ANSI color name resolves to its exact reference RGB.</summary>
    [Theory]
    [InlineData("black", 0)]
    [InlineData("red", 1)]
    [InlineData("green", 2)]
    [InlineData("yellow", 3)]
    [InlineData("blue", 4)]
    [InlineData("magenta", 5)]
    [InlineData("cyan", 6)]
    [InlineData("white", 7)]
    [InlineData("brightblack", 8)]
    [InlineData("gray", 8)]
    [InlineData("grey", 8)]
    [InlineData("brightred", 9)]
    [InlineData("brightgreen", 10)]
    [InlineData("brightyellow", 11)]
    [InlineData("brightblue", 12)]
    [InlineData("brightmagenta", 13)]
    [InlineData("brightcyan", 14)]
    [InlineData("brightwhite", 15)]
    public void Parse_WhenAnsiColorNameIsMarked_ResolvesReferenceRgb(string name, int index)
    {
        var spans = $"<{name}>x</{name}>".Parse(out var display);

        display.ShouldBe("x");
        spans.ShouldHaveSingleItem().Foreground.ShouldBe(ReferenceColors.Get(index));
    }

    /// <summary>Verifies every documented attribute spelling contributes its semantic flag.</summary>
    [Theory]
    [InlineData("b", TerminalAttributes.Bold)]
    [InlineData("bold", TerminalAttributes.Bold)]
    [InlineData("d", TerminalAttributes.Dim)]
    [InlineData("dim", TerminalAttributes.Dim)]
    [InlineData("i", TerminalAttributes.Italic)]
    [InlineData("italic", TerminalAttributes.Italic)]
    [InlineData("s", TerminalAttributes.Strike)]
    [InlineData("strike", TerminalAttributes.Strike)]
    [InlineData("reverse", TerminalAttributes.Reverse)]
    [InlineData("blink", TerminalAttributes.Blink)]
    [InlineData("rapidblink", TerminalAttributes.RapidBlink)]
    [InlineData("hidden", TerminalAttributes.Hidden)]
    [InlineData("conceal", TerminalAttributes.Hidden)]
    [InlineData("overline", TerminalAttributes.Overline)]
    public void Parse_WhenAttributeTagIsMarked_ResolvesSemanticFlag(
        string name,
        TerminalAttributes expected)
    {
        var spans = $"<{name}>x</{name}>".Parse(out var display);

        display.ShouldBe("x");
        spans.ShouldHaveSingleItem().Attributes.ShouldBe(expected);
    }

    /// <summary>Verifies explicit foreground and hyperlink aliases retain their documented semantics.</summary>
    [Fact]
    public void Parse_WhenValueAliasesAreMarked_ResolvesColorAndLink()
    {
        var spans = "<color=brightblue>a</color><a=https://example.test>b</a>".Parse(out var display);

        display.ShouldBe("ab");
        spans[0].Foreground.ShouldBe(ReferenceColors.Get(12));
        spans[1].Link.ShouldBe("https://example.test");
    }

    /// <summary>Verifies independent facets remain active when named tags close out of stack order.</summary>
    [Fact]
    public void Parse_WhenTagsOverlap_ClosesNearestMatchingName()
    {
        var spans = "<u><b>hi</u> there</b>".Parse(out var display);

        display.ShouldBe("hi there");
        spans.Length.ShouldBe(2);
        spans[0].Underline.ShouldBe(Underline.Straight);
        spans[0].Attributes.ShouldBe(TerminalAttributes.Bold);
        spans[1].Underline.ShouldBe(Underline.None);
        spans[1].Attributes.ShouldBe(TerminalAttributes.Bold);
    }

    /// <summary>Verifies the generic close removes the most recently opened tag.</summary>
    [Fact]
    public void Parse_WhenGenericCloseIsUsed_PopsMostRecentTag()
    {
        var spans = "<fg=red>a</>b".Parse(out var display);

        display.ShouldBe("ab");
        spans[0].Foreground.ShouldBe(ReferenceColors.Get(1));
        spans[1].Foreground.ShouldBeNull();
    }

    /// <summary>Verifies the latest underline shape wins and the outer shape resumes after close.</summary>
    [Fact]
    public void Parse_WhenUnderlineShapesNest_UsesLatestOpenShape()
    {
        var spans = "<u>a<u=double>b</u>c</u>".Parse(out var display);

        display.ShouldBe("abc");
        spans[0].Underline.ShouldBe(Underline.Straight);
        spans[1].Underline.ShouldBe(Underline.Paired);
        spans[2].Underline.ShouldBe(Underline.Straight);
    }

    /// <summary>Verifies slow and rapid blink cannot combine into an invalid terminal style.</summary>
    [Fact]
    public void Parse_WhenBlinkKindsNest_UsesLatestOpenKind()
    {
        var spans = "<blink>a<rapidblink>b</rapidblink>c</blink>".Parse(out _);

        spans[0].Attributes.ShouldBe(TerminalAttributes.Blink);
        spans[1].Attributes.ShouldBe(TerminalAttributes.RapidBlink);
        spans[2].Attributes.ShouldBe(TerminalAttributes.Blink);
    }

    /// <summary>Verifies value tags resolve background, underline color, and typed underline.</summary>
    [Fact]
    public void Parse_WhenValueFacetsAreMarked_ResolvesStyle()
    {
        var spans = "<bg=blue><uc=#0f0><u=curly>x</u></uc></bg>".Parse(out _);

        _ = spans.ShouldHaveSingleItem();
        spans[0].Background.ShouldBe(ReferenceColors.Get(4));
        spans[0].UnderlineColor.ShouldBe(Color.Rgb(0, 255, 0));
        spans[0].Underline.ShouldBe(Underline.Curly);
    }

    /// <summary>Verifies a valid non-empty control-free link becomes semantic cell metadata.</summary>
    [Fact]
    public void Parse_WhenLinkIsValid_RecordsTarget()
    {
        var spans = "see <link=https://example.test>here</link>".Parse(out var display);

        display.ShouldBe("see here");
        spans.Single(span => span.Link is not null).Link.ShouldBe("https://example.test");
    }

    /// <summary>Verifies an invalid link tag remains visible instead of failing during rendering.</summary>
    [Fact]
    public void Parse_WhenLinkContainsControl_PreservesRawFragment()
    {
        _ = "<link=bad\u0007>x</link>".Parse(out var display);

        display.ShouldBe("<link=bad\u0007>x");
    }

    /// <summary>Verifies a malformed candidate preserves its complete raw fragment without nested reinterpretation.</summary>
    [Fact]
    public void Parse_WhenMalformedTagContainsOpeningBracket_PreservesWholeFragment()
    {
        _ = "a<unknown <b>c".Parse(out var display);

        display.ShouldBe("a<unknown <b>c");
    }

    /// <summary>Verifies a missing closing bracket preserves the remaining source text.</summary>
    [Fact]
    public void Parse_WhenTagIsUnterminated_PreservesRemainder()
    {
        _ = "a<b".Parse(out var display);

        display.ShouldBe("a<b");
    }

    /// <summary>Verifies unclosed known tags style through the end of visible content.</summary>
    [Fact]
    public void Parse_WhenKnownTagIsUnclosed_AutoClosesAtEnd()
    {
        var spans = "<b>bold".Parse(out var display);

        display.ShouldBe("bold");
        _ = spans.ShouldHaveSingleItem();
        spans[0].Attributes.ShouldBe(TerminalAttributes.Bold);
    }

    /// <summary>Verifies escaped markup metacharacters render literally.</summary>
    [Fact]
    public void Parse_WhenMetacharactersAreEscaped_EmitsLiterals()
    {
        _ = @"a \< b \\ c".Parse(out var display);

        display.ShouldBe(@"a < b \ c");
    }

    /// <summary>Verifies the omitted break convenience remains ordinary unknown markup.</summary>
    [Fact]
    public void Parse_WhenBrTagIsUsed_PreservesItLiterally()
    {
        _ = "a<br>b".Parse(out var display);

        display.ShouldBe("a<br>b");
    }

    /// <summary>Verifies escaping arbitrary visible text round-trips through parsing.</summary>
    [Theory]
    [InlineData("plain")]
    [InlineData("a < b")]
    [InlineData(@"back\slash <tag>")]
    public void Escape_WhenParsed_YieldsOriginalText(string original)
    {
        _ = original.Escape().Parse(out var display);

        display.ShouldBe(original);
    }

    /// <summary>Verifies escaped randomized text parses back to the exact original source.</summary>
    [Fact]
    public void Escape_WhenRandomTextIsParsed_RoundTripsExactly()
    {
        var random = new Random(20260715);
        const string alphabet = "abc<>\\= /\r\n\u0301界";

        for (var trial = 0; trial < 2000; trial++)
        {
            var builder = new StringBuilder();
            var length = random.Next(0, 80);

            for (var index = 0; index < length; index++)
            {
                _ = builder.Append(alphabet[random.Next(alphabet.Length)]);
            }

            var original = builder.ToString();
            _ = original.Escape().Parse(out var display);

            display.ShouldBe(original, $"seed 20260715 trial {trial}");
        }
    }

    /// <summary>Verifies arbitrary fragment combinations produce contiguous, non-overlapping spans.</summary>
    [Fact]
    public void Parse_WhenRandomFragmentsAreCombined_TilesDisplayContiguously()
    {
        var random = new Random(20260716);
        string[] fragments =
        [
            "<b>", "</b>", "<red>", "</red>", "<u>", "<u=curly>", "</>",
            "<fg=#0f0>", "<bg=green>", "x", "<link=https://example.test>", "</link>",
            "\\<", "\\\\", "<unknown>", "<unknown <b>", "<link=bad\u0007>", "=", "界"
        ];

        for (var trial = 0; trial < 2000; trial++)
        {
            var builder = new StringBuilder();
            var length = random.Next(0, 40);

            for (var index = 0; index < length; index++)
            {
                _ = builder.Append(fragments[random.Next(fragments.Length)]);
            }

            var spans = builder.ToString().Parse(out var display);
            var cursor = 0;

            foreach (var span in spans)
            {
                span.Offset.ShouldBe(cursor, $"seed 20260716 trial {trial}");
                span.Length.ShouldBeGreaterThan(0, $"seed 20260716 trial {trial}");
                cursor += span.Length;
            }

            cursor.ShouldBe(display.Length, $"seed 20260716 trial {trial}");
        }
    }
}
