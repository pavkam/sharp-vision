// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Diagnostics.CodeAnalysis;

/// <summary>Verifies the StyleDefinitions factory surface against synthetic style types: the
/// one-hop fallback form used by every leaf control style, and the secondary Part form. The six
/// well-known base types resolve through <see cref="Theme.GetStyleSet{TStyle}(TStyle)"/> directly
/// rather than through <see cref="StyleDefinitions"/> at all - there is no self-contained root
/// form here to test.</summary>
public sealed class StyleDefinitionsTests
{
    private static Theme CreateTheme(string controlExtra = "") =>
        ThemeCatalog.Parse(ThemeJson.Create(controlExtra: controlExtra));

    private static readonly Func<TestWidgetStyle, Theme?, TestWidgetStyle, Theme?, InvalidationImpact> _neverInvalidatesWidget =
        static (_, _, _, _) => InvalidationImpact.None;

    /// <summary>Verifies the fallback form resolves by completing the fallback type's own
    /// resolved Normal - the overload every leaf control style actually calls. A leaf declares no
    /// theme section of its own, so nothing beyond the fallback's resolved Normal and this style's
    /// own <c>complete</c> logic can move the result.</summary>
    [Fact]
    public void Control_WhenResolved_CompletesTheFallbacksNormal()
    {
        var definition = StyleDefinitions.Control(
            static t => t.GetStyleSet("control", RootDefault()),
            Complete,
            _neverInvalidatesWidget);

        var resolved = definition.Resolve(null, CreateTheme());

        resolved.Padding.ShouldBe(1);
    }

    /// <summary>Verifies the fallback form's Appearance resolves a full AppearanceStates reachable
    /// through AppearanceStates.Resolve(VisualState) - end-to-end through the public factory, not
    /// just the underlying Theme primitive. A leaf authors no theme section of its own, so
    /// PointerOver here can only come from the fallback's own resolved pointerOver contribution -
    /// authored under "control", the role section this test's fallback resolves against.</summary>
    [Fact]
    public void Control_Appearance_ResolvesFullAppearanceStates()
    {
        var theme = CreateTheme(controlExtra: """, "pointerOver": { "border": { "foreground": "accent" } } """);
        var definition = StyleDefinitions.Control(
            static t => t.GetStyleSet("control", RootDefault()),
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
        var definition = StyleDefinitions.Part(static _ => RootDefault(), _neverInvalidatesRoot);

        definition.IsControl.ShouldBeFalse();
        definition.Appearance.ShouldBeNull();
    }

    /// <summary>Verifies the fallback form rejects null delegate arguments before constructing a
    /// definition.</summary>
    [Fact]
    public void Control_WhenArgumentsAreNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() =>
            StyleDefinitions.Control<TestWidgetStyle, TestRootStyle>(null!, Complete, _neverInvalidatesWidget));
        _ = Should.Throw<ArgumentNullException>(() =>
            StyleDefinitions.Control(
                static t => t.GetStyleSet("control", RootDefault()), null!, _neverInvalidatesWidget));
        _ = Should.Throw<ArgumentNullException>(() =>
            StyleDefinitions.Control(
                static t => t.GetStyleSet("control", RootDefault()), Complete, null!));
    }

    /// <summary>Verifies Part rejects null delegate arguments before constructing a
    /// definition.</summary>
    [Fact]
    public void Part_WhenArgumentsAreNull_ThrowsArgumentNullException()
    {
        _ = Should.Throw<ArgumentNullException>(() =>
            StyleDefinitions.Part(null!, _neverInvalidatesRoot));
        _ = Should.Throw<ArgumentNullException>(() =>
            StyleDefinitions.Part(static _ => RootDefault(), null!));
    }

    private static readonly Func<TestRootStyle, Theme?, TestRootStyle, Theme?, InvalidationImpact> _neverInvalidatesRoot =
        static (_, _, _, _) => InvalidationImpact.None;

    private static TestRootStyle RootDefault() =>
        new(ControlStyle.DefaultFace, ControlStyle.NoBorder, ControlStyle.NoShadow);

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
