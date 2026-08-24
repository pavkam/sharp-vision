// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Compatibility.Tests;

using System.Diagnostics.CodeAnalysis;

using SharpVision.Controls;
using SharpVision.Styling;

/// <summary>
/// Verifies <see cref="Theme"/>'s four interaction-derived style sets -
/// <see cref="Theme.GetInteractiveControlStyleSet"/>, <see cref="Theme.GetInteractiveRowStyleSet"/>,
/// <see cref="Theme.GetFocusableContainerStyleSet"/>, and
/// <see cref="Theme.GetFocusableControlStyleSet"/> - are a genuinely usable, sanctioned one-hop
/// fallback path for a control library with no access to SharpVision's internals. Unlike every
/// other test project in this repository, this one carries no <c>InternalsVisibleTo</c>
/// relationship with SharpVision, so every member this file touches - <see cref="GaugeStyle"/>'s
/// definition, <see cref="Gauge"/>'s resolved style, and <see cref="RowLikeControl"/>'s resolved
/// appearance - compiles and resolves against public surface alone.
/// </summary>
/// <remarks>
/// <see cref="GaugeStyle"/> and <see cref="Gauge"/> model a complete third-party control the way
/// <c>docs/concepts/theming-new-controls.md</c> describes a control with its own typed style,
/// substituting one of the four derived interaction sets for the fallback a library-owned leaf
/// control such as <c>SliderStyle</c> would use.
/// </remarks>
public sealed class ThemeConsumerTests
{
    /// <summary>Models a third-party "gauge" control's complete style: one structural member
    /// (<see cref="FillColor"/>) beyond Face/Border/Shadow, falling back through
    /// <see cref="Theme.GetInteractiveControlStyleSet"/> the same way a library-owned borderless
    /// interactive leaf style falls back through it, but declared under an explicit vendor-dotted
    /// key since a third-party type name cannot contain the dot <see cref="StyleKey"/> derives keys
    /// from.</summary>
    private sealed record GaugeStyle: ControlStyle
    {
        /// <summary>Initializes a complete gauge presentation.</summary>
        [SetsRequiredMembers]
        public GaugeStyle(Face face, Border border, Shadow shadow, ControlColor fillColor)
            : base(face, border, shadow) => FillColor = fillColor;

        /// <summary>Gets the primary gauge-style definition, resolved against the public
        /// <c>"acme.gauge"</c> vendor-dotted key.</summary>
        public static StyleDefinition<GaugeStyle> Definition { get; } =
            StyleDefinitions.Control(
                "acme.gauge",
                static theme => theme.GetInteractiveControlStyleSet(),
                Complete,
                static (previous, _, current, _) =>
                    previous != current ? InvalidationImpact.Render : InvalidationImpact.None);

        private static GaugeStyle Complete(ControlStyle control, VisualState state) =>
            new(control.Face, control.Border, control.Shadow, SemanticColor.Accent);

        /// <summary>Gets the fill indicator foreground.</summary>
        public required ControlColor FillColor { get; init; }
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
    /// assignment, no attached Theme - resolves its "acme.gauge" style against
    /// <see cref="ThemeCatalog.Dark"/> by completing <see cref="Theme.GetInteractiveControlStyleSet"/>'s
    /// own Normal appearance, the public one-hop fallback resolving a real complete style with
    /// nothing to override it.</summary>
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

    /// <summary>Models a third-party control that reuses one of the four derived interaction sets
    /// through the protected <c>GetDefaultAppearanceStates</c> hook instead of a primary style
    /// slot - the no-structural-members extension path
    /// <c>docs/concepts/theming-new-controls.md</c> documents for a control that only wants an
    /// existing type's appearance, here a selectable borderless row.</summary>
    private sealed class RowLikeControl: ControlBase
    {
        /// <inheritdoc/>
        protected override AppearanceStates GetDefaultAppearanceStates(Theme? theme) =>
            (theme ?? ThemeCatalog.Dark).GetInteractiveRowStyleSet().ToAppearanceStates();

        /// <summary>Exposes the protected hook's own resolution for one explicit Theme, the way a
        /// real control author unit-tests appearance selection directly rather than through a
        /// fully mounted, themed application tree.</summary>
        public Face ResolveNormalFace(Theme theme) => GetDefaultAppearanceStates(theme).Normal.Face;
    }

    /// <summary>Verifies the <c>GetDefaultAppearanceStates</c> hook overload compiles publicly and
    /// resolves end-to-end with no internals access: an instantiated <see cref="RowLikeControl"/>
    /// selects the interactive row set's own Normal face for an explicit Theme.</summary>
    /// <remarks>
    /// This resolves through the protected hook directly with an explicit Theme rather than
    /// through the inherited <c>ActualFace</c>. <c>ActualFace</c> resolves every semantic color
    /// against a control's ambient <c>Theme</c> (<c>InheritedTheme</c>), which only a mounted
    /// <c>Application</c> tree publishes; attaching one without mounting requires the
    /// internal-only <c>SetTheme</c> test seam this project deliberately has no access to (see the
    /// class remarks). An unattached control's own <c>ActualFace</c> is theme-less by design -
    /// every semantic color floors to <c>Color.Default</c> regardless of which style set
    /// backed it - so asserting on it here would prove nothing about which style set was chosen.
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
}
