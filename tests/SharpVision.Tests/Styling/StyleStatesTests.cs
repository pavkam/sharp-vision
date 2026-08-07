// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Verifies the atomic-cutover primitives layered on top of Theme.Overlay:
/// GetRawStyleSection's per-state JSON extraction, GetStyleSet's self-contained root resolution
/// (used only by the six well-known base types), BuildFallbackAwareStates's one-hop declared
/// fallback chain (used by every leaf control style), and ToAppearanceStates's value-diffing adapter
/// back into today's unchanged AppearanceResolver/AppearanceStates.ApplyStates fold logic. Uses
/// synthetic throwaway style types under a namespaced "test." key, not any production style or
/// the still-unmigrated six fixed profile names, matching the isolation discipline the rollout
/// plan for this cutover requires before touching shared infrastructure.</summary>
public sealed class StyleStatesTests
{
    private static Theme CreateTheme(string extraStyles = "") => ThemeCatalog.Parse(ThemeJson.Create(extraStyles: extraStyles));

    private static TestRootStyle RootDefault() =>
        new(ControlStyle.DefaultFace, ControlStyle.NoBorder, ControlStyle.NoShadow);

    /// <summary>Verifies a styles.* key this theme never declared resolves to null rather than
    /// throwing.</summary>
    [Fact]
    public void GetRawStyleSection_WhenKeyIsAbsent_ReturnsNull()
    {
        var theme = CreateTheme();

        theme.GetRawStyleSection("test.widget").ShouldBeNull();
    }

    /// <summary>Verifies a declared key's JSON splits first by state name, then by leaf property
    /// name, with no code-owned default baked in yet.</summary>
    [Fact]
    public void GetRawStyleSection_WhenKeyIsPresent_SplitsByStateThenByProperty()
    {
        var theme = CreateTheme(""", "test.widget": { "normal": { "padding": 2 }, "pointerOver": { "border": { "foreground": "accent" } } } """);

        var raw = theme.GetRawStyleSection("test.widget");

        _ = raw.ShouldNotBeNull();
        raw["normal"]["padding"].GetInt32().ShouldBe(2);
        raw["pointerOver"].ShouldContainKey("border");
    }

    /// <summary>Verifies a state name outside the nine known visual states is rejected as a
    /// likely typo, the same diagnostic-quality guarantee every other theme parsing failure has.</summary>
    [Fact]
    public void GetRawStyleSection_WhenStateNameIsUnknown_Throws()
    {
        var theme = CreateTheme(""", "test.widget": { "hovered": {} } """);

        _ = Should.Throw<InvalidDataException>(() => theme.GetRawStyleSection("test.widget"));
    }

    /// <summary>Verifies the self-contained root form falls back to the caller-supplied code-owned
    /// default when this theme never declared the key at all - used only by the six well-known
    /// base types, which never cross-inherit each other's theme customization.</summary>
    [Fact]
    public void GetStyleSet_WhenThemeDoesNotAuthorTheKey_ResolvesToTheCodeOwnedDefault()
    {
        var theme = CreateTheme();
        var codeDefault = RootDefault();

        var set = theme.GetStyleSet("test.root", codeDefault);

        set.Normal.ShouldBeSameAs(codeDefault);
        set.PointerOver.ShouldBeNull();
    }

    /// <summary>Verifies the root form patches both the "normal" and a named state override onto
    /// the code-owned default.</summary>
    [Fact]
    public void GetStyleSet_WhenThemeAuthorsNormalAndAState_PatchesBothOntoTheCodeOwnedDefault()
    {
        var theme = CreateTheme(""", "test.root": { "normal": { "border": { "sides": "all", "glyphStyle": "rounded" } }, "focused": { "face": { "foreground": "accent" } } } """);

        var set = theme.GetStyleSet("test.root", RootDefault());

        set.Normal.Border.Sides.ShouldBe(BorderSide.All);
        set.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
        _ = set.Focused.ShouldNotBeNull();
        set.Focused.Face.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies a leaf control style with no own-key override for a given state borrows
    /// that state's contribution from its declared one-hop fallback, recombined via `complete` -
    /// the recursive per-state fallback chain this mechanism describes.</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenOwnKeyIsUnauthored_InheritsTheFallbackTypesNormal()
    {
        var theme = CreateTheme(""", "test.root": { "focused": { "face": { "foreground": "accent" } } } """);
        var resolvedNormal = ResolveWidgetNormal(theme);

        var profile = BuildWidgetProfile(theme, resolvedNormal);

        // "test.root" authors a "focused" contribution that "test.widget" does not author itself
        // - it must still reach the fold, borrowed through the declared chain.
        var resolved = profile.Resolve(VisualState.Focused);
        resolved.Face.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies a state the own key does author patches onto this control's own resolved
    /// Normal directly, never re-consulting the fallback for that same state.</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenOwnKeyAuthorsOneState_ThatStatePatchesOwnResolvedNormalNotTheFallback()
    {
        var theme = CreateTheme(""", "test.widget": { "pointerOver": { "border": { "foreground": "accent" } } } """);
        var resolvedNormal = ResolveWidgetNormal(theme);

        var profile = BuildWidgetProfile(theme, resolvedNormal);

        var resolved = profile.Resolve(VisualState.PointerOver);
        resolved.Border.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
        // Everything this state didn't author still comes from widget's own resolved Normal.
        resolved.Face.Foreground.ShouldBe(resolvedNormal.Face.Foreground);
    }

    /// <summary>The critical correctness property of the value-diffing ToAppearanceStates adapter:
    /// two simultaneously active states that each author disjoint leaf properties must BOTH
    /// survive AppearanceResolver's fold - one state's complete-per-state TStyle value must not
    /// clobber a sibling active state's own contribution just because it also carries Normal's
    /// unrelated, unauthored members (see the adapter's own reasoning comment).</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenTwoStatesAreSimultaneouslyActive_BothDisjointContributionsSurviveTheFold()
    {
        var theme = CreateTheme(""", "test.widget": { "pointerOver": { "border": { "foreground": "accent" } }, "pressed": { "face": { "background": "accent" } } } """);
        var resolvedNormal = ResolveWidgetNormal(theme);

        var profile = BuildWidgetProfile(theme, resolvedNormal);

        var resolved = profile.Resolve(VisualState.PointerOver | VisualState.Pressed);

        resolved.Border.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
        resolved.Face.Background.ShouldBe((ControlColor) SemanticColor.Accent);
        // Neither active state authored Shadow - it must still carry Normal's value through untouched.
        resolved.Shadow.ShouldBe(resolvedNormal.Shadow);
    }

    /// <summary>The sibling case the disjoint test above does not reach, and the one value-diffing
    /// alone got wrong: two simultaneously active states contending for the SAME member, where the
    /// later one authored it back to exactly Normal's value. Under a pure value diff that member
    /// looked like a no-op and was dropped, so the fold's <c>later.X ?? X</c> handed it to the
    /// earlier state - the opposite of what the author wrote, and unexpressible by any other
    /// spelling.</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenALaterStateAuthorsNormalsOwnValue_ItWinsTheContestedMember()
    {
        var theme = CreateTheme(""", "test.root": { "normal": { "face": { "background": "surface" } } }, "test.widget": { "pointerOver": { "face": { "background": "accent" } }, "pressed": { "face": { "background": "surface" } } } """);
        var resolvedNormal = ResolveWidgetNormal(theme);

        var profile = BuildWidgetProfile(theme, resolvedNormal);

        var resolved = profile.Resolve(VisualState.PointerOver | VisualState.Pressed);

        resolved.Face.Background.ShouldBe(resolvedNormal.Face.Background);
    }

    private static TestWidgetStyle ResolveWidgetNormal(Theme theme) =>
        Complete(theme.GetStyleSet("test.root", RootDefault()).Normal, VisualState.Normal);

    private static AppearanceStates BuildWidgetProfile(Theme theme, TestWidgetStyle resolvedNormal) =>
        theme.BuildFallbackAwareStates(
            resolvedNormal,
            "test.widget",
            static t => t.GetStyleSet("test.root", RootDefault()),
            Complete);

    private static TestWidgetStyle Complete(TestRootStyle parent, VisualState state) =>
        new(parent.Face, parent.Border, parent.Shadow, padding: 1);

    private sealed record TestRootStyle: ControlStyle
    {
        [SetsRequiredMembers]
        public TestRootStyle(Face face, Border border, Shadow shadow) : base(face, border, shadow)
        {
        }
    }

    private sealed record TestWidgetStyle: ControlStyle
    {
        [SetsRequiredMembers]
        public TestWidgetStyle(Face face, Border border, Shadow shadow, int padding) : base(face, border, shadow) =>
            Padding = padding;

        public required int Padding { get; init; }
    }
}
