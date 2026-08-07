// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Text.Json;

/// <summary>Verifies Theme.Overlay - the reflective fractional overlay the redesigned theming
/// engine resolves every control style through - in complete isolation, against synthetic
/// throwaway fragment types rather than any production style. Proves recursion, typo-safety,
/// wrong-shape handling, leaf-type dispatch, and non-mutation of the source instance, per the
/// RFC's own step 1 scope: zero changes to ControlBase or any production style struct.</summary>
public sealed class ThemeOverlayTests
{
    private static Dictionary<string, JsonElement> ParseOverrides(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    private static Theme CreateTheme() => ThemeCatalog.Parse(ThemeJson.Create());

    /// <summary>Verifies a leaf (non-fragment) property is replaced outright and the source
    /// instance is never mutated.</summary>
    [Fact]
    public void Overlay_WhenOverridingALeafProperty_ReplacesItAndLeavesSourceUnchanged()
    {
        var theme = CreateTheme();
        var original = new TestLeafStyle { Name = "original", Count = 1 };

        var result = (TestLeafStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"name":"patched"}"""), "test");

        result.Name.ShouldBe("patched");
        result.Count.ShouldBe(1);
        original.Name.ShouldBe("original");
    }

    /// <summary>Verifies a property whose type implements IAppearanceFragment is recursed into,
    /// patching only the named nested member and leaving every sibling nested member untouched.</summary>
    [Fact]
    public void Overlay_WhenOverridingANestedFragmentProperty_RecursesAndPreservesSiblings()
    {
        var theme = CreateTheme();
        var original = new TestNestedStyle
        {
            Label = "outer",
            Leaf = new TestLeafStyle { Name = "inner", Count = 5 }
        };

        var result = (TestNestedStyle) theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"leaf":{"name":"replaced"}}"""),
            "test");

        result.Label.ShouldBe("outer");
        result.Leaf.Name.ShouldBe("replaced");
        result.Leaf.Count.ShouldBe(5);
    }

    /// <summary>Verifies recursion composes through three nested fragment levels, patching only
    /// the deepest leaf.</summary>
    [Fact]
    public void Overlay_WhenOverridingThreeLevelsDeep_PatchesOnlyTheDeepestLeaf()
    {
        var theme = CreateTheme();
        var original = new TestDeepStyle
        {
            Nested = new TestNestedStyle
            {
                Label = "outer",
                Leaf = new TestLeafStyle { Name = "inner", Count = 5 }
            }
        };

        var result = (TestDeepStyle) theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"nested":{"leaf":{"count":42}}}"""),
            "test");

        result.Nested.Label.ShouldBe("outer");
        result.Nested.Leaf.Name.ShouldBe("inner");
        result.Nested.Leaf.Count.ShouldBe(42);
    }

    /// <summary>Verifies an override key that maps to no public property throws InvalidDataException
    /// naming the exact dotted path, not a silent no-op or a raw reflection failure.</summary>
    [Fact]
    public void Overlay_WhenKeyIsUnknown_ThrowsNamingTheExactPath()
    {
        var theme = CreateTheme();
        var original = new TestLeafStyle { Name = "x", Count = 1 };

        var exception = Should.Throw<InvalidDataException>(
            () => theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"bogus":"value"}"""), "styles.acme.normal"));

        exception.Message.ShouldContain("styles.acme.normal.bogus");
    }

    /// <summary>Verifies an unknown key nested inside a recursed fragment also names its full,
    /// nested dotted path.</summary>
    [Fact]
    public void Overlay_WhenNestedKeyIsUnknown_ThrowsNamingTheFullNestedPath()
    {
        var theme = CreateTheme();
        var original = new TestNestedStyle
        {
            Label = "outer",
            Leaf = new TestLeafStyle { Name = "inner", Count = 5 }
        };

        var exception = Should.Throw<InvalidDataException>(() => theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"leaf":{"bogus":1}}"""),
            "styles.acme.normal"));

        exception.Message.ShouldContain("styles.acme.normal.leaf.bogus");
    }

    /// <summary>Verifies a scalar value where a fragment property expects an object surfaces as an
    /// InvalidDataException, never a raw JsonException.</summary>
    [Fact]
    public void Overlay_WhenFragmentPropertyValueIsWrongShape_ThrowsInvalidDataException()
    {
        var theme = CreateTheme();
        var original = new TestNestedStyle
        {
            Label = "outer",
            Leaf = new TestLeafStyle { Name = "inner", Count = 5 }
        };

        var exception = Should.Throw<InvalidDataException>(() => theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"leaf":"not-an-object"}"""),
            "styles.acme.normal"));

        exception.Message.ShouldContain("styles.acme.normal.leaf");
        _ = exception.InnerException.ShouldNotBeNull();
    }

    /// <summary>Verifies a ControlColor leaf resolves through the same theme-color-or-literal rule
    /// ResolveSectionColor already uses - a palette key here.</summary>
    [Fact]
    public void Overlay_WhenLeafIsControlColor_ResolvesThroughThemePalette()
    {
        var theme = CreateTheme();
        var original = new TestColorStyle { Tint = Color.Default };

        var result = (TestColorStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"tint":"bg"}"""), "test");

        result.Tint.ShouldBe((ControlColor) theme.Palette["bg"]);
    }

    /// <summary>Verifies a ControlColor leaf referencing an unknown palette key throws
    /// InvalidDataException instead of silently resolving to a default color.</summary>
    [Fact]
    public void Overlay_WhenControlColorReferencesUnknownPaletteKey_Throws()
    {
        var theme = CreateTheme();
        var original = new TestColorStyle { Tint = Color.Default };

        _ = Should.Throw<InvalidDataException>(
            () => theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"tint":"no-such-key"}"""), "test"));
    }

    /// <summary>Verifies a Rune leaf resolves through the same single-Rune parser
    /// (ParseSectionGlyph) unified elsewhere in the overlay engine.</summary>
    [Fact]
    public void Overlay_WhenLeafIsRune_ParsesSingleRune()
    {
        var theme = CreateTheme();
        var original = new TestGlyphStyle { Glyph = new Rune('a') };

        var result = (TestGlyphStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"glyph":"x"}"""), "test");

        result.Glyph.ShouldBe(new Rune('x'));
    }

    /// <summary>Verifies a multi-Rune value for a Rune leaf throws InvalidDataException.</summary>
    [Fact]
    public void Overlay_WhenRuneValueHasMultipleRunes_Throws()
    {
        var theme = CreateTheme();
        var original = new TestGlyphStyle { Glyph = new Rune('a') };

        _ = Should.Throw<InvalidDataException>(
            () => theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"glyph":"xy"}"""), "test"));
    }

    /// <summary>Verifies an enum leaf resolves case-insensitively through the same parser
    /// (ParseSectionEnum&lt;TEnum&gt;) unified elsewhere, invoked here via a per-type-cached reflective call
    /// since the concrete enum type is only known at runtime.</summary>
    [Fact]
    public void Overlay_WhenLeafIsEnum_ParsesCaseInsensitively()
    {
        var theme = CreateTheme();
        var original = new TestEnumStyle { Mode = TestMode.First };

        var result = (TestEnumStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"mode":"SECOND"}"""), "test");

        result.Mode.ShouldBe(TestMode.Second);
    }

    /// <summary>Verifies an unknown enum value throws InvalidDataException instead of silently
    /// defaulting.</summary>
    [Fact]
    public void Overlay_WhenEnumValueIsUnknown_Throws()
    {
        var theme = CreateTheme();
        var original = new TestEnumStyle { Mode = TestMode.First };

        _ = Should.Throw<InvalidDataException>(
            () => theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"mode":"bogus"}"""), "test"));
    }

    /// <summary>Verifies a plain JSON-convertible leaf shape (here, an int) that is neither
    /// ControlColor, Rune, nor an enum deserializes directly through the shared JsonSerializerOptions.</summary>
    [Fact]
    public void Overlay_WhenLeafIsPlainConvertibleType_DeserializesDirectly()
    {
        var theme = CreateTheme();
        var original = new TestLeafStyle { Name = "x", Count = 1 };

        var result = (TestLeafStyle) theme.Overlay(original, ParseOverrides(/*lang=json,strict*/ """{"count":99}"""), "test");

        result.Count.ShouldBe(99);
    }

    /// <summary>Verifies the two-call composition a per-state style resolution needs - Normal
    /// patched from a code-owned default, then PointerOver patched from the already-resolved
    /// Normal - so an unspecified PointerOver property inherits Normal's value exactly like
    /// today's AppearanceOverlay-based per-state overlay already does for Face/Border/Shadow, just
    /// generalized to the whole fragment shape.</summary>
    [Fact]
    public void Overlay_WhenComposedTwiceForPerStateResolution_UnspecifiedPropertiesInheritFromNormal()
    {
        var theme = CreateTheme();
        var codeOwnedDefault = new TestLeafStyle { Name = "default", Count = 0 };

        var normal = (TestLeafStyle) theme.Overlay(
            codeOwnedDefault,
            ParseOverrides(/*lang=json,strict*/ """{"name":"normal-name","count":10}"""),
            "styles.acme.normal");
        var pointerOver = (TestLeafStyle) theme.Overlay(
            normal,
            ParseOverrides(/*lang=json,strict*/ """{"count":20}"""),
            "styles.acme.pointerOver");

        pointerOver.Name.ShouldBe("normal-name");
        pointerOver.Count.ShouldBe(20);
        normal.Count.ShouldBe(10);
    }

    /// <summary>Verifies Overlay recurses through a real production fragment - Border nested inside
    /// a synthetic style, patching one Border member and leaving its siblings and the outer
    /// style's own members untouched - proving the engine composes correctly against a type it
    /// did not define, now that Border implements IAppearanceFragment with init-only
    /// properties.</summary>
    [Fact]
    public void Overlay_WhenNestedPropertyIsARealBorder_PatchesOnlyTheNamedBorderMember()
    {
        var theme = CreateTheme();
        var border = new Border(BorderSide.All, BorderGlyphStyle.Light, Color.Default, Color.Transparent, TerminalAttributes.None);
        var original = new TestBorderHostStyle { Label = "outer", Border = border };

        var result = (TestBorderHostStyle) theme.Overlay(
            original,
            ParseOverrides(/*lang=json,strict*/ """{"border":{"sides":"none"}}"""),
            "test");

        result.Label.ShouldBe("outer");
        result.Border.Sides.ShouldBe(BorderSide.None);
        result.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Light);
        original.Border.Sides.ShouldBe(BorderSide.All);
    }

    /// <summary>Verifies ResolveProperty's cache resolves the same property for repeated lookups
    /// of the same (type, key) pair, and returns null for an unmapped key instead of throwing -
    /// Overlay itself is the layer responsible for turning that null into InvalidDataException.</summary>
    [Fact]
    public void ResolveProperty_WhenCalledRepeatedlyOrWithUnknownKey_IsConsistent()
    {
        var first = ThemeStyleFragment.ResolveProperty(typeof(TestLeafStyle), "name");
        var second = ThemeStyleFragment.ResolveProperty(typeof(TestLeafStyle), "name");
        var unknown = ThemeStyleFragment.ResolveProperty(typeof(TestLeafStyle), "doesNotExist");

        _ = first.ShouldNotBeNull();
        first.ShouldBeSameAs(second);
        unknown.ShouldBeNull();
    }

    private sealed record TestLeafStyle: IAppearanceFragment
    {
        public required string Name { get; init; }
        public required int Count { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestBorderHostStyle: IAppearanceFragment
    {
        public required string Label { get; init; }
        public required Border Border { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestNestedStyle: IAppearanceFragment
    {
        public required string Label { get; init; }
        public required TestLeafStyle Leaf { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestDeepStyle: IAppearanceFragment
    {
        public required TestNestedStyle Nested { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestColorStyle: IAppearanceFragment
    {
        public required ControlColor Tint { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private sealed record TestGlyphStyle: IAppearanceFragment
    {
        public required Rune Glyph { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }

    private enum TestMode
    {
        First,
        Second
    }

    private sealed record TestEnumStyle: IAppearanceFragment
    {
        public required TestMode Mode { get; init; }

        IAppearanceFragment IAppearanceFragment.Clone() => this with { };
    }
}
