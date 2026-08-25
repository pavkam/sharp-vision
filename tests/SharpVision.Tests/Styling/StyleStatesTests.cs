// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using System.Diagnostics.CodeAnalysis;

/// <summary>Verifies the atomic-cutover primitives layered on top of Theme.Overlay:
/// GetRawStyleSection's per-state JSON extraction, GetStyleSet's self-contained root resolution
/// (used only by the six well-known base types), BuildFallbackAwareStates's one-hop declared
/// fallback chain (used by every leaf control style), and ToAppearanceStates's value-diffing adapter
/// back into today's unchanged AppearanceResolver/AppearanceStates.ApplyStates fold logic. A
/// theme's "styles" object is closed to exactly the six well-known role sections, so - unlike this
/// file's earlier synthetic-key incarnation - these primitives are exercised through synthetic
/// style TYPES (TestRootStyle/TestWidgetStyle) whose JSON is authored under a real role section:
/// "control" for the root form (which never cross-inherits, matching a synthetic root's own
/// isolation), "input" for GetRawStyleSection's raw-shape probes. There is no longer any key a
/// theme document can author that is not one of the six.</summary>
public sealed class StyleStatesTests
{
    private static Theme CreateTheme(
        string controlSides = "\"none\"",
        string controlExtra = "",
        string inputExtra = "",
        string inputStates = "") =>
        ThemeCatalog.Parse(ThemeJson.Create(
            controlSides: controlSides,
            controlExtra: controlExtra,
            inputExtra: inputExtra,
            inputStates: inputStates));

    private static TestRootStyle RootDefault() =>
        new(ControlStyle.DefaultFace, ControlStyle.NoBorder, ControlStyle.NoShadow);

    /// <summary>Verifies a styles.* key this theme never declared resolves to null rather than
    /// throwing.</summary>
    [Fact]
    public void GetRawStyleSection_WhenKeyIsAbsent_ReturnsNull()
    {
        var theme = CreateTheme();

        theme.GetRawStyleSection("missing").ShouldBeNull();
    }

    /// <summary>Verifies a declared key's JSON splits first by state name, then by leaf property
    /// name, with no code-owned default baked in yet.</summary>
    [Fact]
    public void GetRawStyleSection_WhenKeyIsPresent_SplitsByStateThenByProperty()
    {
        var theme = CreateTheme(
            inputExtra: ", \"padding\": 2",
            inputStates: """, "pointerOver": { "border": { "foreground": "accent" } } """);

        var raw = theme.GetRawStyleSection("input");

        _ = raw.ShouldNotBeNull();
        raw["normal"]["padding"].GetInt32().ShouldBe(2);
        raw["pointerOver"].ShouldContainKey("border");
    }

    /// <summary>Verifies a state name outside the nine known visual states is rejected as a
    /// likely typo, the same diagnostic-quality guarantee every other theme parsing failure has.</summary>
    [Fact]
    public void GetRawStyleSection_WhenStateNameIsUnknown_Throws()
    {
        var theme = CreateTheme(inputStates: """, "hovered": {} """);

        _ = Should.Throw<InvalidDataException>(() => theme.GetRawStyleSection("input"));
    }

    /// <summary>Verifies the self-contained root form falls back to the caller-supplied code-owned
    /// default when this theme never declared the key at all - used only by the six well-known
    /// base types, which never cross-inherit each other's theme customization.</summary>
    [Fact]
    public void GetStyleSet_WhenThemeDoesNotAuthorTheKey_ResolvesToTheCodeOwnedDefault()
    {
        var theme = CreateTheme();
        var codeDefault = RootDefault();

        var set = theme.GetStyleSet("missing", codeDefault);

        set.Normal.ShouldBeSameAs(codeDefault);
        set.IsPointerOver.ShouldBeNull();
    }

    /// <summary>Verifies root-style memoization includes the caller's complete code-owned default,
    /// so one library cannot make another library's default depend on call order.</summary>
    [Fact]
    public void GetStyleSet_WhenSameTypeAndKeyUseDifferentDefaults_CachesEachDefaultIndependently()
    {
        // Arrange
        var firstDefault = RootDefault();
        var secondDefault = firstDefault with
        {
            Face = firstDefault.Face with { Foreground = SemanticColor.Accent }
        };
        var forwardTheme = CreateTheme();
        var reverseTheme = CreateTheme();

        // Act
        var forwardFirst = forwardTheme.GetStyleSet("missing", firstDefault);
        var forwardSecond = forwardTheme.GetStyleSet("missing", secondDefault);
        var reverseSecond = reverseTheme.GetStyleSet("missing", secondDefault);
        var reverseFirst = reverseTheme.GetStyleSet("missing", firstDefault);

        // Assert
        forwardFirst.Normal.ShouldBeSameAs(firstDefault);
        forwardSecond.Normal.ShouldBeSameAs(secondDefault);
        reverseSecond.Normal.ShouldBeSameAs(secondDefault);
        reverseFirst.Normal.ShouldBeSameAs(firstDefault);
    }

    /// <summary>Verifies the root form patches both the "normal" and a named state override onto
    /// the code-owned default.</summary>
    [Fact]
    public void GetStyleSet_WhenThemeAuthorsNormalAndAState_PatchesBothOntoTheCodeOwnedDefault()
    {
        var theme = CreateTheme(
            controlSides: "\"all\"",
            controlExtra: """, "focused": { "face": { "foreground": "accent" } } """);

        var set = theme.GetStyleSet("control", RootDefault());

        set.Normal.Border.Sides.ShouldBe(BorderSide.All);
        set.Normal.Border.GlyphStyle.ShouldBe(BorderGlyphStyle.Rounded);
        _ = set.Focused.ShouldNotBeNull();
        set.Focused.Face.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies a leaf control style with no theme section of its own borrows a state's
    /// contribution from its declared one-hop fallback, recombined via `complete` - the only
    /// per-state source a leaf has, since it authors no styles.* section itself.</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenTheFallbackAuthorsAState_TheLeafInheritsIt()
    {
        var theme = CreateTheme(controlExtra: """, "focused": { "face": { "foreground": "accent" } } """);
        var resolvedNormal = ResolveWidgetNormal(theme);

        var profile = BuildWidgetProfile(theme, resolvedNormal);

        // "control" (the fallback) authors a "focused" contribution the widget never could on its
        // own - it must still reach the fold, borrowed through the declared fallback chain.
        var resolved = profile.Resolve(VisualState.Focused);
        resolved.Face.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>The critical correctness property of the value-diffing ToAppearanceStates adapter:
    /// two simultaneously active states that each author disjoint leaf properties must BOTH
    /// survive AppearanceResolver's fold - one state's complete-per-state TStyle value must not
    /// clobber a sibling active state's own contribution just because it also carries Normal's
    /// unrelated, unauthored members (see the adapter's own reasoning comment).</summary>
    [Fact]
    public void BuildFallbackAwareStates_WhenTwoStatesAreSimultaneouslyActive_BothDisjointContributionsSurviveTheFold()
    {
        var theme = CreateTheme(
            controlExtra: """, "pointerOver": { "border": { "foreground": "accent" } }, "pressed": { "face": { "background": "surface" } } """);
        var resolvedNormal = ResolveWidgetNormal(theme);

        var profile = BuildWidgetProfile(theme, resolvedNormal);

        var resolved = profile.Resolve(VisualState.IsPointerOver | VisualState.Pressed);

        resolved.Border.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
        resolved.Face.Background.ShouldBe((ControlColor) SemanticColor.Surface);
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
        var theme = CreateTheme(
            controlExtra: """, "pointerOver": { "face": { "background": "surface" } }, "pressed": { "face": { "background": "control" } } """);
        var resolvedNormal = ResolveWidgetNormal(theme);

        var profile = BuildWidgetProfile(theme, resolvedNormal);

        var resolved = profile.Resolve(VisualState.IsPointerOver | VisualState.Pressed);

        resolved.Face.Background.ShouldBe(resolvedNormal.Face.Background);
    }

    private static TestWidgetStyle ResolveWidgetNormal(Theme theme) =>
        Complete(theme.GetStyleSet("control", RootDefault()).Normal, VisualState.Normal, theme);

    private static AppearanceStates BuildWidgetProfile(Theme theme, TestWidgetStyle resolvedNormal) =>
        theme.BuildFallbackAwareStates(
            resolvedNormal,
            static t => t.GetStyleSet("control", RootDefault()),
            Complete);

    private static TestWidgetStyle Complete(TestRootStyle parent, VisualState state, Theme theme) =>
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
