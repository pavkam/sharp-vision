// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Compatibility.Tests;

using System.Diagnostics.CodeAnalysis;
using System.Text;

using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Rendering;

/// <summary>
/// Verifies <see cref="Theme"/>'s five interaction-derived style sets -
/// <see cref="Theme.GetInteractiveControlStyleSet"/>, <see cref="Theme.GetInteractiveRowStyleSet"/>,
/// <see cref="Theme.GetFocusableContainerStyleSet"/>, <see cref="Theme.GetFocusableControlStyleSet"/>,
/// and <see cref="Theme.GetTabularControlStyleSet"/> - are a genuinely usable, sanctioned one-hop
/// fallback path for a control library with no access to SharpVision's internals. Unlike every
/// other test project in this repository, this one carries no <c>InternalsVisibleTo</c>
/// relationship with SharpVision, so every member this file touches - <see cref="GaugeStyle"/>'s
/// definition, <see cref="Gauge"/>'s resolved style, and <see cref="RowLikeControl"/>'s resolved
/// appearance - compiles and resolves against public surface alone.
/// </summary>
/// <remarks>
/// <see cref="GaugeStyle"/> and <see cref="Gauge"/> model a complete third-party control the way
/// <c>docs/concepts/theming-new-controls.md</c> describes a control with its own typed style,
/// substituting one of the five derived interaction sets for the fallback a library-owned leaf
/// control such as <c>SliderStyle</c> would use. A theme's "styles" object is closed to exactly
/// the six well-known role sections, so a third-party style - like every library leaf style - now
/// declares no <c>styles.*</c> key of its own at all: its only sources of appearance are its
/// code-owned completion logic, the declared fallback, and a locally assigned
/// <see cref="Gauge.Style"/>.
/// </remarks>
public sealed class ThemeConsumerTests
{
    /// <summary>Verifies a package consumer can build and freeze a Theme entirely through typed APIs.</summary>
    [Fact]
    public void Theme_WhenBuiltProgrammatically_ConfiguresEveryTypedValueFamily()
    {
        var theme = new Theme();
        var control = ControlStyle.Default with
        {
            Face = ControlStyle.Default.Face with { Foreground = SemanticColor.Accent }
        };

        theme.SetColor(SemanticColor.Accent, Color.Rgb(1, 2, 3));
        theme.SetAttributes(SemanticDecoration.FocusedText, TerminalAttributes.Bold);
        theme.SetGlyphs(GlyphFamily.Ascii);
        theme.SetStyleSet(new StyleStates<ControlStyle> { Normal = control });
        theme.Freeze();

        theme.ResolveColor(SemanticColor.Accent).ShouldBe(Color.Rgb(1, 2, 3));
        theme.GetStyleSet(ControlStyle.Default).Normal.ShouldBe(control);
    }

    /// <summary>Models a third-party "gauge" control's complete style: two structural members
    /// (<see cref="FillColor"/>, <see cref="FillGlyph"/>) beyond Face/Border/Shadow, falling back
    /// through <see cref="Theme.GetInteractiveControlStyleSet"/> the same way a library-owned
    /// borderless interactive leaf style falls back through it. <see cref="FillGlyph"/> exists
    /// specifically to prove the completion delegate's <see cref="Theme"/> argument reaches
    /// theme-level values beyond the fallback style's own resolved appearance, the same way a
    /// library glyph-aware style (e.g. <c>ProgressBarStyle</c>) reads <c>theme.Glyphs</c> to
    /// complete itself.</summary>
    private sealed record GaugeStyle: ControlStyle
    {
        /// <summary>Initializes a complete gauge presentation.</summary>
        [SetsRequiredMembers]
        public GaugeStyle(Face face, Border border, Shadow shadow, ControlColor fillColor, Rune fillGlyph)
            : base(face, border, shadow)
        {
            FillColor = fillColor;
            FillGlyph = fillGlyph;
        }

        /// <summary>Gets the primary gauge-style definition. A third-party style declares no
        /// <c>styles.*</c> key of its own any more, so this resolves entirely from its declared
        /// fallback and this type's own <see cref="Complete"/> logic.</summary>
        public static StyleDefinition<GaugeStyle> Definition { get; } =
            StyleDefinitions.Control(
                static theme => theme.GetInteractiveControlStyleSet(),
                Complete,
                static (previous, _, current, _) =>
                    previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

        private static GaugeStyle Complete(ControlStyle control, VisualState state, Theme theme) =>
            new(control.Face, control.Border, control.Shadow, SemanticColor.Accent, theme.Glyphs.ProgressBar.Fill);

        /// <summary>Gets the fill indicator foreground.</summary>
        public required ControlColor FillColor { get; init; }

        /// <summary>Gets the fill indicator glyph, completed from the active theme's glyph family
        /// rather than a literal hardcoded in <see cref="Complete"/>.</summary>
        public required Rune FillGlyph { get; init; }
    }

    /// <summary>Hosts <see cref="GaugeStyle"/> the way a real third-party control would: a primary
    /// style slot initialized from the public <see cref="StyleDefinition{TStyle}"/>, following
    /// <c>docs/concepts/theming-new-controls.md</c>'s <c>CommandTile</c> example exactly.</summary>
    private sealed class Gauge: ControlBase, IStyled<GaugeStyle>
    {
        private readonly StyleSlot<GaugeStyle> _style;

        public Gauge() => _style = InitializeStyle(GaugeStyle.Definition);

        /// <inheritdoc/>
        public GaugeStyle? Style
        {
            get => _style.Local;
            set => _style.Local = value;
        }

        /// <inheritdoc/>
        public GaugeStyle ActualStyle => _style.Actual;
    }

    /// <summary>Verifies an unattached <see cref="Gauge"/> - no local <see cref="Gauge.Style"/>
    /// assignment, no attached Theme - resolves its style against
    /// <see cref="ThemeCatalog.Dark"/> by completing <see cref="Theme.GetInteractiveControlStyleSet"/>'s
    /// own Normal appearance, the public one-hop fallback resolving a real complete style with
    /// nothing to override it. Also verifies <see cref="GaugeStyle.FillGlyph"/> resolves to
    /// <see cref="GlyphFamily.Default"/>'s own fill glyph - <see cref="ThemeCatalog.Dark"/> is one
    /// of the two zero-config themes, so its <c>Theme.Glyphs</c> is <see cref="GlyphFamily.Default"/>
    /// - proving <see cref="GaugeStyle.Complete"/> genuinely reads the <see cref="Theme"/> argument
    /// <see cref="StyleDefinitions"/>'s completion delegate carries rather than ignoring it.</summary>
    [Fact]
    public void ActualStyle_WhenGaugeHasNoLocalOverride_CompletesTheInteractiveControlSetsNormal()
    {
        // Arrange
        var gauge = new Gauge();
        var expectedNormal = ThemeCatalog.Dark.GetInteractiveControlStyleSet().Normal;

        // Act
        var actual = gauge.ActualStyle;

        // Assert
        actual.Face.ShouldBe(expectedNormal.Face);
        actual.Border.ShouldBe(expectedNormal.Border);
        actual.Shadow.ShouldBe(expectedNormal.Shadow);
        actual.FillGlyph.ShouldBe(GlyphFamily.Default.ProgressBar.Fill);
    }

    /// <summary>Verifies the interactive control set - the fallback <see cref="GaugeStyle"/>
    /// declares above - carries real Focused and IsPointerOver interaction contributions distinct
    /// from Normal, and that the now-public <see cref="StyleStatesExtensions.ToAppearanceStates{TStyle}"/>
    /// converts them into a non-empty Focused overlay.</summary>
    /// <remarks>
    /// <c>StyleDefinition&lt;TStyle&gt;.Appearance</c> stays internal, so a public consumer cannot
    /// resolve <see cref="GaugeStyle.Definition"/>'s own appearance states directly the way
    /// <see cref="Gauge"/>'s style slot resolves its complete style above. This instead asserts
    /// directly on the public <see cref="Theme"/> surface every fallback style of this shape
    /// declares against, which is what a consumer actually depends on.
    /// </remarks>
    [Fact]
    public void GetInteractiveControlStyleSet_WhenResolvedAgainstTheDarkTheme_CarriesDistinctFocusedAndPointerOverContributions()
    {
        // Arrange
        var theme = ThemeCatalog.Dark;

        // Act
        var set = theme.GetInteractiveControlStyleSet();
        var states = set.ToAppearanceStates();

        // Assert
        var focused = set.Focused.ShouldNotBeNull();
        focused.ShouldNotBe(set.Normal);
        var pointerOver = set.IsPointerOver.ShouldNotBeNull();
        pointerOver.ShouldNotBe(set.Normal);
        states.Focused.ShouldNotBe(AppearanceOverlay.Empty);
    }

    /// <summary>Verifies <see cref="Theme.GetFocusableContainerStyleSet"/> - the narrower sibling
    /// for a directly focusable container-shaped control such as TreeView or JsonView - populates
    /// only Focused/FocusWithin while leaving IsPointerOver exactly as the passive "container" key
    /// resolves it, since a container's own content owns hover more specifically than a blanket
    /// interactive rebase would.</summary>
    [Fact]
    public void GetFocusableContainerStyleSet_WhenResolvedAgainstTheDarkTheme_PopulatesOnlyFocusedOverThePassiveContainer()
    {
        // Arrange
        var theme = ThemeCatalog.Dark;
        var passiveContainer = theme.GetStyleSet(ContainerStyle.Default);

        // Act
        var focusableContainer = theme.GetFocusableContainerStyleSet();

        // Assert
        var focused = focusableContainer.Focused.ShouldNotBeNull();
        focused.ShouldNotBe(focusableContainer.Normal);
        focusableContainer.IsPointerOver.ShouldBe(passiveContainer.IsPointerOver);
    }

    /// <summary>Verifies <see cref="Theme.GetTabularControlStyleSet"/> - the owner-surface sibling
    /// for a control such as Table or Document that paints independently colored content over one
    /// shared surface - compiles and resolves from a genuinely public, non-<c>InternalsVisibleTo</c>
    /// call site, and populates only Focused/FocusWithin while leaving IsPointerOver exactly as the
    /// passive "control" key resolves it, the same narrowing <see cref="Theme.GetFocusableControlStyleSet"/>
    /// applies.</summary>
    [Fact]
    public void GetTabularControlStyleSet_WhenResolvedAgainstTheDarkTheme_PopulatesOnlyFocusedOverThePassiveControl()
    {
        // Arrange
        var theme = ThemeCatalog.Dark;
        var passiveControl = theme.GetStyleSet(ControlStyle.Default);

        // Act
        var tabularControl = theme.GetTabularControlStyleSet();

        // Assert
        var focused = tabularControl.Focused.ShouldNotBeNull();
        focused.ShouldNotBe(tabularControl.Normal);
        tabularControl.IsPointerOver.ShouldBe(passiveControl.IsPointerOver);
    }

    /// <summary>Models a third-party control that reuses one of the five derived interaction sets
    /// through the protected <c>GetDefaultAppearanceStates</c> hook instead of a primary style
    /// slot - the no-structural-members extension path
    /// <c>docs/concepts/theming-new-controls.md</c> documents for a control that only wants an
    /// existing type's appearance, here a selectable borderless row.</summary>
    private sealed class RowLikeControl: ControlBase
    {
        /// <inheritdoc/>
        protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
            (theme ?? ThemeCatalog.Dark).GetInteractiveRowStyleSet().ToAppearanceStates();

        /// <summary>Exposes the protected hook's own resolution for one explicit Theme, so the
        /// test can assert which style set the hook SELECTED - still in semantic form - separately
        /// from the literal resolution <see cref="ControlBase.ResolveAppearance"/> performs.</summary>
        public Face ResolveNormalFace(Theme theme) => GetDefaultAppearanceStates(theme).Normal.Face;

        /// <summary>Resolves one semantic color the way the base class does, so the literal-path
        /// test below can compute its expected value through public/protected surface alone.</summary>
        public static Color ResolveExpected(ControlColor value, Theme theme) => ResolveColor(value, theme);
    }

    /// <summary>Verifies the <c>GetDefaultAppearanceStates</c> hook overload compiles publicly and
    /// resolves end-to-end with no internals access: an instantiated <see cref="RowLikeControl"/>
    /// selects the interactive row set's own Normal face for an explicit Theme.</summary>
    /// <remarks>
    /// This resolves through the protected hook directly with an explicit Theme rather than
    /// through the inherited <c>ActualFace</c>. <c>ActualFace</c> resolves every semantic color
    /// against a control's ambient <c>Theme</c>, which only a mounted <c>Application</c> tree
    /// publishes - an unattached control's own <c>ActualFace</c> is theme-less by design, so
    /// asserting on it here would prove nothing about which style set was chosen. The sanctioned
    /// unattached preview for the RESOLVED appearance is
    /// <see cref="ControlBase.ResolveAppearance"/>, covered by the companion test below; this one
    /// pins the semantic selection itself.
    /// </remarks>
    [Fact]
    public void ResolveNormalFace_WhenControlSelectsInteractiveRowStyleSetThroughTheHook_ResolvesEndToEnd()
    {
        // Arrange
        var control = new RowLikeControl();

        // Act
        var face = control.ResolveNormalFace(ThemeCatalog.Dark);

        // Assert
        face.ShouldBe(ThemeCatalog.Dark.GetInteractiveRowStyleSet().Normal.Face);
    }

    /// <summary>Verifies <see cref="ControlBase.ResolveAppearance"/> previews the hook-selected
    /// interaction set's appearance with literals resolved against an explicit Theme, on an
    /// unattached control, through public surface alone - the seam a third-party control author
    /// unit-tests themed appearance with, no mounted application required.</summary>
    [Fact]
    public void ResolveAppearance_WhenControlSelectsInteractiveRowStyleSetThroughTheHook_ResolvesLiteralsForTheExplicitTheme()
    {
        // Arrange
        var control = new RowLikeControl();
        var theme = ThemeCatalog.Dark;
        var semantic = theme.GetInteractiveRowStyleSet().Normal.Face;

        // Act
        var appearance = control.ResolveAppearance(theme);

        // Assert
        appearance.Face.Background.Literal.ShouldBe(RowLikeControl.ResolveExpected(semantic.Background, theme));
        appearance.Face.Foreground.IsLiteral.ShouldBeTrue();
    }
}
