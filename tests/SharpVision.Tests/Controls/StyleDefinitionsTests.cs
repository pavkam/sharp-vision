// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Diagnostics.CodeAnalysis;

using SharpVision.Tests.Styling;

/// <summary>Verifies the two StyleDefinitions factories against synthetic style types: the
/// self-contained root form (used only by the six well-known base types) and the one-hop
/// fallback form (used by every leaf control style). Complements
/// SharpVision.Tests.Styling.StyleStatesTests, which exercises the underlying Theme
/// primitives directly - this file exercises the public factory surface every real control style
/// actually calls.</summary>
public sealed class StyleDefinitionsTests
{
    private static Theme CreateTheme(string extraStyles = "") => ThemeCatalog.Parse(ThemeJson.Create(extraStyles: extraStyles));

    private static readonly Func<TestRootStyle, Theme?, TestRootStyle, Theme?, InvalidationImpact> _neverInvalidates =
        static (_, _, _, _) => InvalidationImpact.None;

    private static readonly Func<TestWidgetStyle, Theme?, TestWidgetStyle, Theme?, InvalidationImpact> _neverInvalidatesWidget =
        static (_, _, _, _) => InvalidationImpact.None;

    /// <summary>Verifies the root form's Resolve falls back to the code-owned default with no theme.</summary>
    [Fact]
    public void Control_RootForm_WhenLocalIsNull_ResolvesToCodeOwnedDefault()
    {
        var codeDefault = RootDefault();
        var definition = StyleDefinitions.Control("test.root", codeDefault, _neverInvalidates);

        definition.Resolve(null, null).ShouldBeSameAs(codeDefault);
    }

    /// <summary>Verifies the root form's Resolve prefers an explicit local value over the theme.</summary>
    [Fact]
    public void Control_RootForm_WhenLocalIsSupplied_LocalWins()
    {
        var codeDefault = RootDefault();
        var local = RootDefault();
        var definition = StyleDefinitions.Control("test.root", codeDefault, _neverInvalidates);
        var theme = CreateTheme(""", "test.root": { "normal": { "border": { "sides": "all" } } } """);

        definition.Resolve(local, theme).ShouldBeSameAs(local);
    }

    /// <summary>The correctness fix this file exists to pin: the fallback form's Resolve must
    /// apply this style's own key's "normal" JSON override on top of the completed fallback -
    /// not just its per-state Appearance path - otherwise a theme could restyle every other state
    /// but never the resting Normal appearance or any of TStyle's own structural members (e.g.
    /// Padding).</summary>
    [Fact]
    public void Control_FallbackForm_WhenOwnKeyAuthorsNormal_ResolvePatchesTheCompletedFallback()
    {
        var theme = CreateTheme(""", "test.root": { }, "test.widget": { "normal": { "padding": 5, "border": { "foreground": "accent" } } } """);
        var definition = StyleDefinitions.Control(
            "test.widget",
            static t => t.GetStyleSet("test.root", RootDefault()),
            Complete,
            _neverInvalidatesWidget);

        var resolved = definition.Resolve(null, theme);

        resolved.Padding.ShouldBe(5);
        resolved.Border.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies the fallback form's Appearance resolves a full AppearanceStates reachable
    /// through AppearanceStates.Resolve(VisualState) - end-to-end through the public factory, not
    /// just the underlying Theme primitive.</summary>
    [Fact]
    public void Control_FallbackForm_Appearance_ResolvesFullAppearanceStates()
    {
        var theme = CreateTheme(""", "test.widget": { "pointerOver": { "border": { "foreground": "accent" } } } """);
        var definition = StyleDefinitions.Control(
            "test.widget",
            static t => t.GetStyleSet("test.root", RootDefault()),
            Complete,
            _neverInvalidatesWidget);
        var normal = definition.Resolve(null, theme);

        var profile = definition.Appearance!(normal, theme);

        profile.Resolve(VisualState.IsPointerOver).Border.Foreground.ShouldBe((ControlColor) SemanticColor.Accent);
    }

    /// <summary>Verifies a secondary (Part) definition never owns appearance.</summary>
    [Fact]
    public void Part_Definition_NeverOwnsAppearance()
    {
        var definition = StyleDefinitions.Part(static _ => RootDefault(), _neverInvalidates);

        definition.IsControl.ShouldBeFalse();
        definition.Appearance.ShouldBeNull();
    }

    private static TestRootStyle RootDefault() =>
        new(ControlStyle.DefaultFace, ControlStyle.NoBorder, ControlStyle.NoShadow);

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
