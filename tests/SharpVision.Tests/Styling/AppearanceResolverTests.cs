// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Tests.Controls;


/// <summary>Verifies ambient-face inheritance decisions against the fully folded appearance.</summary>
public sealed class AppearanceResolverTests
{
    /// <summary>Verifies a state-only transparent background — invisible on the opaque Normal
    /// face the inheritance decision used to run against — still triggers ambient inheritance.</summary>
    [Fact]
    public void ResolveSnapshot_WhenOnlyStateOverlayIsTransparent_InheritsParentAmbientFace()
    {
        var normalFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(1, 1, 1),
            background: Color.Rgb(2, 2, 2));
        var profile = new AppearanceStates(
            new ControlAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)),
            disabled: new AppearanceOverlay(face: new FaceOverlay(background: Color.Transparent)));
        var control = new StyledProbe { AppearanceStatesOverride = profile };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = control.ResolveSnapshot(VisualState.Disabled, parentFace);

        resolved.Face.Foreground.Literal.ShouldBe(Color.Rgb(9, 9, 9));
        resolved.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
    }

    /// <summary>Verifies a state overlay's own explicit foreground still wins over ambient
    /// inheritance, even though that same overlay's transparent background is what triggers it —
    /// ambient inheritance must remain a base the overlay folds on top of, not a final pass that
    /// clobbers what the overlay itself explicitly authored.</summary>
    [Fact]
    public void ResolveSnapshot_WhenStateOverlaySetsOwnForegroundAndTransparentBackground_KeepsOverlayForeground()
    {
        var normalFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(1, 1, 1),
            background: Color.Transparent);
        var profile = new AppearanceStates(
            new ControlAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)),
            @checked: new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(5, 5, 5))));
        var control = new StyledProbe { AppearanceStatesOverride = profile };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = control.ResolveSnapshot(VisualState.Checked, parentFace);

        resolved.Face.Foreground.Literal.ShouldBe(Color.Rgb(5, 5, 5));
        resolved.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
    }

    /// <summary>Verifies a LocalFace keeps opting out of ambient inheritance entirely regardless of
    /// its own transparency — unlike Normal or a state overlay, it is a complete override commonly
    /// authored with its own foreground and a left-default transparent background.</summary>
    [Fact]
    public void ResolveSnapshot_WhenLocalFaceBackgroundIsTransparent_PreservesOwnForeground()
    {
        var normalFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(1, 1, 1),
            background: Color.Rgb(2, 2, 2));
        var profile = new AppearanceStates(
            new ControlAppearance(
                normalFace,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)));
        var control = new StyledProbe
        {
            AppearanceStatesOverride = profile,
            Face = AppearanceTestValues.Face(
                foreground: Color.Rgb(3, 3, 3),
                background: Color.Transparent)
        };
        var parentFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(9, 9, 9),
            attributes: TerminalAttributes.None);

        var resolved = control.ResolveSnapshot(VisualState.Normal, parentFace);

        resolved.Face.Foreground.Literal.ShouldBe(Color.Rgb(3, 3, 3));
        resolved.BackgroundMode.ShouldBe(BackgroundMode.Transparent);
    }

    /// <summary>Verifies a Face whose theme-referenced attributes only conflict with its typed
    /// underline once the theme resolves them no longer throws: <c>ResolveFace</c> reconciles the
    /// triple against the already-resolved literals - a typed underline clears the legacy attribute
    /// flag - exactly as every other <see cref="DecorationResolver.Resolve"/> call site already
    /// does, instead of deferring to Face's constructor, which can only see the conflict once every
    /// channel happens to be literal.</summary>
    [Fact]
    public void ResolveSnapshot_WhenThemeResolvedAttributesConflictWithUnderline_ReconcilesInsteadOfThrowing()
    {
        var theme = new Theme();
        theme.SetAttributes(SemanticDecoration.NormalText, TerminalAttributes.Underline);
        var face = new Face(
            Color.Rgb(1, 1, 1),
            Color.Rgb(2, 2, 2),
            SemanticDecoration.NormalText,
            Underline.Straight,
            Color.Rgb(3, 3, 3));
        var profile = new AppearanceStates(
            new ControlAppearance(
                face,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)));
        var control = new StyledProbe { AppearanceStatesOverride = profile };

        var resolved = control.ResolveSnapshot(VisualState.Normal, theme, profile, parentAmbientFace: null);

        resolved.Face.Attributes.Literal.ShouldBe(TerminalAttributes.None);
        resolved.Face.Underline.ShouldBe(Underline.Straight);
        resolved.Face.UnderlineColor.Literal.ShouldBe(Color.Rgb(3, 3, 3));
    }

    /// <summary>Verifies the funnel's second reachable route: ordinary application code setting a
    /// semantic <see cref="Face.UnderlineColor"/> with no active underline at all - no typed
    /// underline and no legacy attribute flag. The color is cleared to <see cref="Color.Default"/>
    /// once resolved, per <see cref="DecorationResolver.Resolve"/>'s third rule, rather than the
    /// resolved Face construction throwing because the channel only becomes literal - and only
    /// conflicts - once the theme resolves it.</summary>
    [Fact]
    public void ResolveSnapshot_WhenSemanticUnderlineColorHasNoActiveUnderline_ClearsItInsteadOfThrowing()
    {
        var theme = new Theme();
        theme.SetColor(SemanticColor.Accent, Color.Rgb(3, 3, 3));
        var face = AppearanceTestValues.Face(underlineColor: SemanticColor.Accent);
        var profile = new AppearanceStates(
            new ControlAppearance(
                face,
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)));
        var control = new StyledProbe { AppearanceStatesOverride = profile };

        var resolved = control.ResolveSnapshot(VisualState.Normal, theme, profile, parentAmbientFace: null);

        resolved.Face.Attributes.Literal.ShouldBe(TerminalAttributes.None);
        resolved.Face.Underline.ShouldBe(Underline.None);
        resolved.Face.UnderlineColor.Literal.ShouldBe(Color.Default);
    }

    /// <summary>Verifies <see cref="AppearanceStates.StateAuthorsOwnRelief"/> actually gates the
    /// resolved border split rather than being scanned for nothing: a state overlay that authors
    /// both <see cref="BorderRelief.Raised"/> and a differing <see cref="Border.Foreground"/> on the
    /// same slot must still resolve to the highlight/shade split, because the state that asked for
    /// its own relief owns that edge feedback. Paired with the counter-case below, this proves the
    /// suppression conjunct in <c>AppearanceResolver.FoldAuthoredAppearance</c> - consumed by <see
    /// cref="ResolvedBorderStyles.Create"/> - is load-bearing: deleting it would make this case
    /// behave like the flattened one instead.</summary>
    [Fact]
    public void ResolveSnapshot_WhenAStateAuthorsBothReliefAndForeground_KeepsTheReliefSplit()
    {
        var theme = new Theme();
        theme.SetColor(SemanticColor.ReliefHighlight, Color.Rgb(10, 10, 10));
        theme.SetColor(SemanticColor.ReliefShade, Color.Rgb(20, 20, 20));
        var normal = new ControlAppearance(
            AppearanceTestValues.Face(),
            new Border(
                BorderSide.All,
                BorderGlyphStyle.Heavy,
                Color.Rgb(1, 1, 1),
                BorderRelief.Raised,
                Color.Transparent,
                SemanticDecoration.Border),
            AppearanceTestValues.Shadow(visible: false));
        var profile = new AppearanceStates(
            normal,
            focused: new AppearanceOverlay(
                border: new BorderOverlay(foreground: Color.Rgb(3, 3, 3), relief: BorderRelief.Raised)));
        var control = new StyledProbe { AppearanceStatesOverride = profile };

        var resolved = control.ResolveSnapshot(VisualState.Focused, theme, profile, parentAmbientFace: null);

        resolved.BorderStyles.Top.Foreground.ShouldBe(Color.Rgb(10, 10, 10));
        resolved.BorderStyles.Left.Foreground.ShouldBe(Color.Rgb(10, 10, 10));
        resolved.BorderStyles.Right.Foreground.ShouldBe(Color.Rgb(20, 20, 20));
        resolved.BorderStyles.Bottom.Foreground.ShouldBe(Color.Rgb(20, 20, 20));
    }

    /// <summary>The counter-case: a state overlay that authors only a differing <see
    /// cref="Border.Foreground"/> - no relief of its own - keeps the flat authored-foreground bypass
    /// and flattens every edge despite the Raised baseline. Together with the case above, this proves
    /// <see cref="AppearanceStates.StateAuthorsOwnRelief"/> is exactly what tells the two apart, since
    /// both scenarios author the same differing foreground and only whether relief is also authored
    /// on that slot changes the outcome.</summary>
    [Fact]
    public void ResolveSnapshot_WhenAStateAuthorsOnlyForeground_FlattensDespiteTheRaisedBaseline()
    {
        var theme = new Theme();
        theme.SetColor(SemanticColor.ReliefHighlight, Color.Rgb(10, 10, 10));
        theme.SetColor(SemanticColor.ReliefShade, Color.Rgb(20, 20, 20));
        var normal = new ControlAppearance(
            AppearanceTestValues.Face(),
            new Border(
                BorderSide.All,
                BorderGlyphStyle.Heavy,
                Color.Rgb(1, 1, 1),
                BorderRelief.Raised,
                Color.Transparent,
                SemanticDecoration.Border),
            AppearanceTestValues.Shadow(visible: false));
        var profile = new AppearanceStates(
            normal,
            focused: new AppearanceOverlay(border: new BorderOverlay(foreground: Color.Rgb(3, 3, 3))));
        var control = new StyledProbe { AppearanceStatesOverride = profile };

        var resolved = control.ResolveSnapshot(VisualState.Focused, theme, profile, parentAmbientFace: null);

        resolved.BorderStyles.Top.Foreground.ShouldBe(Color.Rgb(3, 3, 3));
        resolved.BorderStyles.Right.Foreground.ShouldBe(Color.Rgb(3, 3, 3));
        resolved.BorderStyles.Bottom.Foreground.ShouldBe(Color.Rgb(3, 3, 3));
        resolved.BorderStyles.Left.Foreground.ShouldBe(Color.Rgb(3, 3, 3));
    }

    /// <summary>Verifies mode is treated as geometry by the resolved-value comparison, alongside
    /// the offset change of identical magnitude that was already classified Measure.
    ///
    /// <para>"Does this need Measure, or only Render?" was answered in three places that did not
    /// agree. The two theme-side predicates listed <c>Border.Sides</c>, <c>Shadow.IsVisible</c>, and
    /// <c>Shadow.Offset</c>; the two local-overlay checks listed <c>Border.Sides</c> alone. So the
    /// same footprint change requested Measure when a theme authored it and Render when application
    /// code did - and <c>Shadow.Mode</c>, which selects between half-row and whole-cell shadow
    /// arithmetic and so resizes the visual-overflow rectangle on its own, appeared in none of the
    /// four.</para>
    /// </summary>
    [Fact]
    public void GetImpact_WhenOnlyShadowModeChanges_RequiresMeasure() =>
        Impact(ShadowMode.Composite, ShadowMode.FractionalBlock).ShouldBe(InvalidationImpact.Measure);

    /// <summary>The comparison the case above is calibrated against: the same-magnitude change
    /// spelled as an offset, which was always Measure.</summary>
    [Fact]
    public void GetImpact_WhenOnlyShadowOffsetChanges_RequiresMeasure()
    {
        var previous = States(ShadowMode.Composite, new Point(1, 2));
        var current = States(ShadowMode.Composite, new Point(2, 1));

        Impact(previous, current).ShouldBe(InvalidationImpact.Measure);
    }

    /// <summary>The counter-case that keeps the escalation from swallowing everything: a pure color
    /// change still asks only for a repaint.</summary>
    [Fact]
    public void GetImpact_WhenOnlyAColorChanges_RequiresRenderOnly()
    {
        var previous = States(ShadowMode.Composite, new Point(1, 2));
        var current = States(ShadowMode.Composite, new Point(1, 2), Color.Rgb(9, 9, 9));

        Impact(previous, current).ShouldBe(InvalidationImpact.Render);
    }

    /// <summary>Verifies an unchanged pair still reports no work at all.</summary>
    [Fact]
    public void GetImpact_WhenNothingChanges_RequiresNothing() =>
        Impact(ShadowMode.Composite, ShadowMode.Composite).ShouldBe(InvalidationImpact.None);

    /// <summary>Verifies registering a local shadow overlay requests the same invalidation the
    /// theme-authored equivalent does. It used to request Render while VisualBounds grew by two
    /// columns and a row.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe), rather than through the
    /// resolved-appearance comparison the GetImpact_* tests above exercise directly.</remarks>
    /// </summary>
    [Fact]
    public void SetStateAppearance_WhenALocalOverlayMovesTheShadow_RequestsMeasure()
    {
        using var probe = new ChromeProbe();
        probe.Clear(Invalidation.All);

        probe.SetStateAppearance(VisualState.Focused, ShadowGeometryOverlay);

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.Measure);
    }

    /// <summary>Verifies removing that same overlay is classified identically. Inspecting only the
    /// incoming value would leave the control laid out for a footprint it no longer has.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe).</remarks>
    /// </summary>
    [Fact]
    public void SetStateAppearance_WhenAGeometryOverlayIsRemoved_RequestsMeasure()
    {
        using var probe = new ChromeProbe();
        probe.SetStateAppearance(VisualState.Focused, ShadowGeometryOverlay);
        probe.Clear(Invalidation.All);

        probe.SetStateAppearance(VisualState.Focused, null);

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.Measure);
    }

    /// <summary>The counter-case: a cosmetic local overlay is still a repaint, so this did not turn
    /// every appearance edit into a re-measure.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe).</remarks>
    /// </summary>
    [Fact]
    public void SetStateAppearance_WhenALocalOverlayIsCosmetic_RequestsRenderOnly()
    {
        using var probe = new ChromeProbe();
        probe.Clear(Invalidation.All);

        probe.SetStateAppearance(
            VisualState.Focused,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(1, 2, 3))));

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies the second local-overlay site agrees too: entering a state whose registered
    /// overlay moves the shadow re-measures. This is the path that ran on every focus gain and loss,
    /// so a weaker answer here was wrong repeatedly rather than once.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe).</remarks>
    /// </summary>
    [Fact]
    public void VisualStateChange_WhenARegisteredOverlayMovesTheShadow_RequestsMeasure()
    {
        using var probe = new ChromeProbe();
        probe.SetStateAppearance(VisualState.Disabled, ShadowGeometryOverlay);
        probe.Clear(Invalidation.All);

        probe.IsEnabled = false;

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.Measure);
    }

    /// <summary>The counter-case for that site: a cosmetic registered overlay keeps a state change
    /// render-only.
    ///
    /// <remarks>Proves AppearanceResolver's geometry classification as consumed through
    /// ControlBase's invalidation pipeline (via ChromeProbe).</remarks>
    /// </summary>
    [Fact]
    public void VisualStateChange_WhenRegisteredOverlaysAreCosmetic_RequestsRenderOnly()
    {
        using var probe = new ChromeProbe();
        probe.SetStateAppearance(
            VisualState.Disabled,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(1, 2, 3))));
        probe.Clear(Invalidation.All);

        probe.IsEnabled = false;

        (probe.Pending & Invalidation.Measure).ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies the public preview resolves identically to the live cached pipeline for
    /// the control's own inherited Theme, across control shapes, themes, and state combinations —
    /// the equivalence contract that makes a preview assertion a proof about what the control
    /// actually presents.</summary>
    [Fact]
    public void ResolveAppearance_WhenThemeMatchesTheInheritedOne_MatchesLiveResolutionExactly()
    {
        Theme[] themes = [ThemeCatalog.Dark, ThemeCatalog.White];
        VisualState[] states =
        [
            VisualState.Normal,
            VisualState.Focused,
            VisualState.Disabled,
            VisualState.Focused | VisualState.IsPointerOver,
            VisualState.Selected | VisualState.FocusWithin
        ];

        foreach (var theme in themes)
        {
            ControlBase[] controls =
                [new Button(), new TextInput(), new GroupBox(), new Window(), new Popup(), new StyledProbe()];

            foreach (var control in controls)
            {
                control.SetTheme(theme);

                foreach (var state in states)
                {
                    var preview = control.ResolveAppearance(theme, state);

                    preview.ShouldBe(new ControlAppearance(
                        control.GetActualFace(state),
                        control.GetActualBorder(state),
                        control.GetActualShadow(state)));
                }
            }
        }
    }

    /// <summary>Verifies a preview of a Theme the control does not yet inherit matches what the
    /// live pipeline resolves once that Theme actually arrives — the prospective half of the
    /// equivalence contract, resolved through the same derived hooks the transition planner
    /// uses.</summary>
    [Fact]
    public void ResolveAppearance_WhenPreviewingAProspectiveTheme_MatchesLiveResolutionAfterTheSwap()
    {
        using var control = new StyledProbe();
        control.SetTheme(ThemeCatalog.Dark);

        var preview = control.ResolveAppearance(ThemeCatalog.White, VisualState.Focused);
        control.SetTheme(ThemeCatalog.White);

        preview.ShouldBe(new ControlAppearance(
            control.GetActualFace(VisualState.Focused),
            control.GetActualBorder(VisualState.Focused),
            control.GetActualShadow(VisualState.Focused)));
    }

    /// <summary>Verifies a null Theme previews the same library-fallback resolution an unthemed
    /// control presents live: Dark's style sets with every semantic color floored to the default
    /// literal.</summary>
    [Fact]
    public void ResolveAppearance_WhenThemeIsNull_MatchesUnthemedLiveResolution()
    {
        using var control = new Button();

        var preview = control.ResolveAppearance(theme: null, VisualState.Disabled);

        preview.ShouldBe(new ControlAppearance(
            control.GetActualFace(VisualState.Disabled),
            control.GetActualBorder(VisualState.Disabled),
            control.GetActualShadow(VisualState.Disabled)));
    }

    /// <summary>Verifies the preview resolves ambient inheritance through the ancestor chain under
    /// the supplied Theme: a transparent child previews the same inherited foreground the live
    /// pipeline resolves, not its own authored one.</summary>
    [Fact]
    public void ResolveAppearance_WhenAncestorsSupplyAmbientFace_ResolvesTheChainUnderTheSuppliedTheme()
    {
        var profile = new AppearanceStates(
            new ControlAppearance(
                AppearanceTestValues.Face(
                    foreground: Color.Rgb(1, 1, 1),
                    background: Color.Transparent),
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false)));
        using var root = new Stack();
        var middle = new Stack();
        var child = new StyledProbe { AppearanceStatesOverride = profile };
        root.Children.Add(middle);
        middle.Children.Add(child);
        root.PropagateTheme(ThemeCatalog.White);

        var preview = child.ResolveAppearance(ThemeCatalog.White);

        preview.Face.ShouldBe(child.GetActualFace(VisualState.Normal));
        preview.Face.Foreground.ShouldBe(
            middle.GetActualFace(middle.AmbientAppearanceState).Foreground);
        preview.Face.Foreground.Literal.ShouldNotBe(Color.Rgb(1, 1, 1));
    }

    /// <summary>Verifies the prospective ambient walk agrees with the live one end-to-end: a
    /// child's preview taken while the tree is still unthemed equals what the live pipeline
    /// resolves once the root propagates that Theme through the subtree — the same publication a
    /// mounted application's Theme swap performs. (A bare root SetTheme is narrower: it themes the
    /// root alone and reaches descendants only ambiently, which is not the coherent whole-tree
    /// inheritance the preview models.)</summary>
    [Fact]
    public void ResolveAppearance_WhenPreviewingBeforeTheTreeIsThemed_MatchesTheChildsResolutionAfterThePropagation()
    {
        using var root = new Stack();
        var child = new ControlText("x");
        root.Children.Add(child);

        var preview = child.ResolveAppearance(ThemeCatalog.White);
        root.PropagateTheme(ThemeCatalog.White);

        preview.ShouldBe(new ControlAppearance(
            child.GetActualFace(VisualState.Normal),
            child.GetActualBorder(VisualState.Normal),
            child.GetActualShadow(VisualState.Normal)));
    }

    /// <summary>Verifies developer-owned local values — a complete local face and a per-state
    /// local overlay — participate in the preview exactly as they do live, since local values are
    /// what a control author most often needs to prove against a theme.</summary>
    [Fact]
    public void ResolveAppearance_WhenLocalAppearanceValuesExist_HonorsThemExactlyAsLiveResolutionDoes()
    {
        using var probe = new ChromeProbe();
        probe.SetTheme(ThemeCatalog.Dark);
        probe.Face = AppearanceTestValues.Face(
            foreground: Color.Rgb(3, 3, 3),
            background: Color.Rgb(4, 4, 4));
        probe.SetStateAppearance(
            VisualState.Focused,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(5, 5, 5))));

        var preview = probe.ResolveAppearance(ThemeCatalog.Dark, VisualState.Focused);

        preview.ShouldBe(new ControlAppearance(
            probe.GetActualFace(VisualState.Focused),
            probe.GetActualBorder(VisualState.Focused),
            probe.GetActualShadow(VisualState.Focused)));
        preview.Face.Foreground.Literal.ShouldBe(Color.Rgb(5, 5, 5));
    }

    /// <summary>Verifies the preview is a pure read: no resolved-appearance cache entries, no
    /// invalidation, and no property notifications — so asserting a prospective Theme in a test
    /// cannot perturb the control it asserts on.</summary>
    [Fact]
    public void ResolveAppearance_WhenCalled_BypassesTheCacheAndPublishesNothing()
    {
        using var probe = new ChromeProbe();
        probe.SetTheme(ThemeCatalog.Dark);
        var live = probe.GetActualFace(VisualState.Normal);
        var resolutions = probe.UncachedAppearanceResolutionCount;
        var notifications = new List<string?>();
        probe.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        probe.Clear(Invalidation.All);

        _ = probe.ResolveAppearance(ThemeCatalog.White);
        _ = probe.ResolveAppearance(ThemeCatalog.White, VisualState.Focused);
        _ = probe.ResolveAppearance(theme: null, VisualState.Disabled);
        _ = probe.ResolveAppearance(ThemeCatalog.Dark);

        probe.UncachedAppearanceResolutionCount.ShouldBe(resolutions);
        notifications.ShouldBeEmpty();
        probe.Pending.ShouldBe(Invalidation.None);
        probe.GetActualFace(VisualState.Normal).ShouldBe(live);
    }

    /// <summary>Verifies the public entry rejects unknown state flags with the same contract the
    /// live resolution entry enforces.</summary>
    [Fact]
    public void ResolveAppearance_WhenVisualStateContainsUnknownFlags_Throws()
    {
        using var control = new Button();

        Should.Throw<ArgumentOutOfRangeException>(
                () => control.ResolveAppearance(ThemeCatalog.Dark, (VisualState) (1 << 9)))
            .ParamName.ShouldBe("visualState");
    }

    private static AppearanceOverlay ShadowGeometryOverlay =>
        new(shadow: new ShadowOverlay(isVisible: true, offset: new Point(2, 1)));

    private static InvalidationImpact Impact(ShadowMode previous, ShadowMode current) =>
        Impact(States(previous, new Point(1, 2)), States(current, new Point(1, 2)));

    private static InvalidationImpact Impact(AppearanceStates previous, AppearanceStates current)
    {
        using var control = new StyledProbe { AppearanceStatesOverride = previous };
        return control.GetImpact(null, previous, null, current, null, null);
    }

    private static AppearanceStates States(ShadowMode mode, Point offset, Color? foreground = null) =>
        new(new ControlAppearance(
            AppearanceTestValues.Face(foreground: foreground ?? Color.Rgb(1, 1, 1)),
            AppearanceTestValues.Border(BorderSide.All),
            AppearanceTestValues.Shadow(visible: true, mode: mode, offset: offset)));
}
