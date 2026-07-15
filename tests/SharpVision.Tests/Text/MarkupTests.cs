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
        var spans = Markup.Parse("hello", out var display);

        display.ShouldBe("hello");
        spans.Length.ShouldBe(1);
        spans[0].Offset.ShouldBe(0);
        spans[0].Length.ShouldBe(5);
        spans[0].Foreground.ShouldBeNull();
        spans[0].Attributes.ShouldBe(Attributes.None);
        spans[0].Link.ShouldBeNull();
    }

    /// <summary>Verifies an empty source produces no visible text or meaningless zero-length span.</summary>
    [Fact]
    public void Parse_WhenTextIsEmpty_YieldsNoSpans()
    {
        var spans = Markup.Parse(string.Empty, out var display);

        display.ShouldBeEmpty();
        spans.ShouldBeEmpty();
    }

    /// <summary>Verifies bare and explicit foreground tags resolve indexed, RGB, and role colors.</summary>
    [Fact]
    public void Parse_WhenColorsAreMarked_ResolvesEveryValueForm()
    {
        var spans = Markup.Parse(
            "<red>a</red><fg=214>b</fg><fg=#f80>c</fg><accent>d</accent>",
            out var display);

        display.ShouldBe("abcd");
        spans.Length.ShouldBe(4);
        spans[0].Foreground.ShouldBe(Color.Indexed(1));
        spans[1].Foreground.ShouldBe(Color.Indexed(214));
        spans[2].Foreground.ShouldBe(Color.Rgb(255, 136, 0));
        spans[3].Foreground.ShouldBe(Color.Role((int) ColorRole.Accent));
    }

    /// <summary>Verifies independent facets remain active when named tags close out of stack order.</summary>
    [Fact]
    public void Parse_WhenTagsOverlap_ClosesNearestMatchingName()
    {
        var spans = Markup.Parse("<u><b>hi</u> there</b>", out var display);

        display.ShouldBe("hi there");
        spans.Length.ShouldBe(2);
        spans[0].Underline.ShouldBe(Underline.Straight);
        spans[0].Attributes.ShouldBe(Attributes.Bold);
        spans[1].Underline.ShouldBe(Underline.None);
        spans[1].Attributes.ShouldBe(Attributes.Bold);
    }

    /// <summary>Verifies the generic close removes the most recently opened tag.</summary>
    [Fact]
    public void Parse_WhenGenericCloseIsUsed_PopsMostRecentTag()
    {
        var spans = Markup.Parse("<accent>a</>b", out var display);

        display.ShouldBe("ab");
        spans[0].Foreground.ShouldBe(Color.Role((int) ColorRole.Accent));
        spans[1].Foreground.ShouldBeNull();
    }

    /// <summary>Verifies the latest underline shape wins and the outer shape resumes after close.</summary>
    [Fact]
    public void Parse_WhenUnderlineShapesNest_UsesLatestOpenShape()
    {
        var spans = Markup.Parse("<u>a<u=double>b</u>c</u>", out var display);

        display.ShouldBe("abc");
        spans[0].Underline.ShouldBe(Underline.Straight);
        spans[1].Underline.ShouldBe(Underline.Paired);
        spans[2].Underline.ShouldBe(Underline.Straight);
    }

    /// <summary>Verifies slow and rapid blink cannot combine into an invalid terminal style.</summary>
    [Fact]
    public void Parse_WhenBlinkKindsNest_UsesLatestOpenKind()
    {
        var spans = Markup.Parse("<blink>a<rapidblink>b</rapidblink>c</blink>", out _);

        spans[0].Attributes.ShouldBe(Attributes.Blink);
        spans[1].Attributes.ShouldBe(Attributes.RapidBlink);
        spans[2].Attributes.ShouldBe(Attributes.Blink);
    }

    /// <summary>Verifies value tags resolve background, underline color, and typed underline.</summary>
    [Fact]
    public void Parse_WhenValueFacetsAreMarked_ResolvesStyle()
    {
        var spans = Markup.Parse("<bg=blue><uc=#0f0><u=curly>x</u></uc></bg>", out _);

        _ = spans.ShouldHaveSingleItem();
        spans[0].Background.ShouldBe(Color.Indexed(4));
        spans[0].UnderlineColor.ShouldBe(Color.Rgb(0, 255, 0));
        spans[0].Underline.ShouldBe(Underline.Curly);
    }

    /// <summary>Verifies a valid non-empty control-free link becomes semantic cell metadata.</summary>
    [Fact]
    public void Parse_WhenLinkIsValid_RecordsTarget()
    {
        var spans = Markup.Parse("see <link=https://example.test>here</link>", out var display);

        display.ShouldBe("see here");
        spans.Single(span => span.Link is not null).Link.ShouldBe("https://example.test");
    }

    /// <summary>Verifies an invalid link tag remains visible instead of failing during rendering.</summary>
    [Fact]
    public void Parse_WhenLinkContainsControl_PreservesRawFragment()
    {
        _ = Markup.Parse("<link=bad\u0007>x</link>", out var display);

        display.ShouldBe("<link=bad\u0007>x");
    }

    /// <summary>Verifies a malformed candidate preserves its complete raw fragment without nested reinterpretation.</summary>
    [Fact]
    public void Parse_WhenMalformedTagContainsOpeningBracket_PreservesWholeFragment()
    {
        _ = Markup.Parse("a<unknown <b>c", out var display);

        display.ShouldBe("a<unknown <b>c");
    }

    /// <summary>Verifies a missing closing bracket preserves the remaining source text.</summary>
    [Fact]
    public void Parse_WhenTagIsUnterminated_PreservesRemainder()
    {
        _ = Markup.Parse("a<b", out var display);

        display.ShouldBe("a<b");
    }

    /// <summary>Verifies unclosed known tags style through the end of visible content.</summary>
    [Fact]
    public void Parse_WhenKnownTagIsUnclosed_AutoClosesAtEnd()
    {
        var spans = Markup.Parse("<b>bold", out var display);

        display.ShouldBe("bold");
        _ = spans.ShouldHaveSingleItem();
        spans[0].Attributes.ShouldBe(Attributes.Bold);
    }

    /// <summary>Verifies escaped markup metacharacters render literally.</summary>
    [Fact]
    public void Parse_WhenMetacharactersAreEscaped_EmitsLiterals()
    {
        _ = Markup.Parse(@"a \< b \\ c", out var display);

        display.ShouldBe(@"a < b \ c");
    }

    /// <summary>Verifies the omitted break convenience remains ordinary unknown markup.</summary>
    [Fact]
    public void Parse_WhenBrTagIsUsed_PreservesItLiterally()
    {
        _ = Markup.Parse("a<br>b", out var display);

        display.ShouldBe("a<br>b");
    }

    /// <summary>Verifies escaping arbitrary visible text round-trips through parsing.</summary>
    [Theory]
    [InlineData("plain")]
    [InlineData("a < b")]
    [InlineData(@"back\slash <tag>")]
    public void Escape_WhenParsed_YieldsOriginalText(string original)
    {
        _ = Markup.Parse(Markup.Escape(original), out var display);

        display.ShouldBe(original);
    }
}
