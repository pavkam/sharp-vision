// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies complete local appearance values and resolved theme ownership.</summary>
public sealed partial class ControlBaseTests
{
    /// <summary>Verifies every failing prospective style delegate preserves all observable state.</summary>
    /// <param name="failure">Selects resolver, comparer, appearance, or invalid-impact failure.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Style_WhenProspectiveDelegateFails_PreservesCompleteState(int failure)
    {
        var control = new StyledProbe();
        var expectedStyle = control.ActualStyle;
        var expectedFace = control.ActualFace;
        var expectedBorder = control.ActualBorder;
        var expectedShadow = control.ActualShadow;
        var expectedResolutions = control.UncachedAppearanceResolutionCount;
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.ThrowOnStyleResolution = failure == 0;
        control.ThrowOnCompareStructure = failure == 1;
        control.ThrowOnAppearanceSelection = failure == 2;
        control.ReturnInvalidImpact = failure == 3;
        control.Clear(Invalidation.All);

        if (failure == 3)
        {
            _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Style = ButtonStyle.Filled);
        }
        else
        {
            _ = Should.Throw<InvalidOperationException>(() => control.Style = ButtonStyle.Filled);
        }

        control.ThrowOnStyleResolution = false;
        control.ThrowOnCompareStructure = false;
        control.ThrowOnAppearanceSelection = false;
        control.ReturnInvalidImpact = false;
        control.Style.ShouldBeNull();
        control.ActualStyle.ShouldBe(expectedStyle);
        control.ActualFace.ShouldBe(expectedFace);
        control.ActualBorder.ShouldBe(expectedBorder);
        control.ActualShadow.ShouldBe(expectedShadow);
        control.UncachedAppearanceResolutionCount.ShouldBe(expectedResolutions);
        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies nested transparent style authoring compares the inherited concrete face.</summary>
    [Fact]
    public void Style_WhenNestedTransparentControlsResolveToSameAmbientFace_DoesNotInvalidateAppearance()
    {
        var previous = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)));
        var current = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6)));
        var child = new StyledProbe { Style = previous };
        var parent = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: Color.Rgb(7, 8, 9))
        };
        parent.Children.Add(child);
        var expectedFace = child.ActualFace;
        var notifications = new List<string?>();
        child.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        child.Clear(Invalidation.All);

        child.Style = current;

        child.ActualFace.ShouldBe(expectedFace);
        child.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBe([nameof(StyledProbe.Style), nameof(StyledProbe.ActualStyle)]);
        notifications.ShouldNotContain(nameof(ControlBase.ActualFace));
    }

    /// <summary>Verifies direct root Theme changes publish exact ambient descendant appearance only.</summary>
    [Fact]
    public void SetTheme_WhenTransparentDescendantInheritsChangedRootFace_PublishesAppearanceOnly()
    {
        var previousTheme = ThemeWithControl(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithControl(Color.Rgb(4, 5, 6));
        var childStyle = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(7, 8, 9)));
        var child = new StyledProbe { Style = childStyle };
        var root = new ProbeContainer();
        root.Children.Add(child);
        root.SetTheme(previousTheme);
        var rootNotifications = new List<string?>();
        var childNotifications = new List<string?>();
        root.PropertyChanged += (_, eventArgs) =>
        {
            root.Theme.ShouldBeSameAs(currentTheme);
            root.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
            rootNotifications.Add(eventArgs.PropertyName);
        };
        child.PropertyChanged += (_, eventArgs) =>
        {
            child.Theme.ShouldBeNull();
            child.ActualStyle.ShouldBe(childStyle);
            child.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
            childNotifications.Add(eventArgs.PropertyName);
        };
        root.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        root.SetTheme(currentTheme);

        root.Pending.ShouldBe(Invalidation.Render);
        child.Pending.ShouldBe(Invalidation.Render);
        rootNotifications.ShouldBe([
            nameof(ControlBase.Theme),
            nameof(Container.ActualScrollBarStyle),
            nameof(ControlBase.ActualFace)
        ]);
        childNotifications.ShouldBe([nameof(ControlBase.ActualFace)]);
        childNotifications.ShouldNotContain(nameof(ControlBase.Theme));
        childNotifications.ShouldNotContain(nameof(StyledProbe.ActualStyle));
    }

    /// <summary>Verifies child ambient snapshots bracket the complete parent-first Theme propagation.</summary>
    [Fact]
    public void PropagateTheme_WhenTransparentChildInheritsParentFace_PublishesExactCommittedFace()
    {
        var previousTheme = ThemeWithControl(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithControl(Color.Rgb(4, 5, 6));
        var child = new ProbeControl();
        var parent = new ProbeContainer();
        parent.Children.Add(child);
        parent.PropagateTheme(previousTheme);
        var notifications = new List<string?>();
        child.PropertyChanged += (_, eventArgs) =>
        {
            child.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
            notifications.Add(eventArgs.PropertyName);
        };

        parent.PropagateTheme(currentTheme);

        notifications.ShouldContain(nameof(ControlBase.ActualFace));
    }

    /// <summary>Verifies cache-neutral Theme staging never consumes cold parent or child appearance caches.</summary>
    [Fact]
    public void PropagateTheme_WhenAmbientCachesAreCold_DoesNotPopulateOrCountSnapshotResolution()
    {
        var previousTheme = ThemeWithControl(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithControl(Color.Rgb(4, 5, 6));
        var child = new ProbeControl();
        var parent = new ProbeContainer();
        parent.Children.Add(child);
        parent.PropagateTheme(previousTheme);
        var parentResolutions = parent.UncachedAppearanceResolutionCount;
        var childResolutions = child.UncachedAppearanceResolutionCount;
        parent.InvalidateResolvedStyleCache();
        child.InvalidateResolvedStyleCache();

        parent.PropagateTheme(currentTheme);

        parent.UncachedAppearanceResolutionCount.ShouldBe(parentResolutions);
        child.UncachedAppearanceResolutionCount.ShouldBe(childResolutions);
    }

    /// <summary>Verifies direct and propagated Theme cache invalidation visit each node exactly once.</summary>
    [Fact]
    public void ThemeChange_WhenTreeIsDeep_ClearsEachResolvedAppearanceCacheOnce()
    {
        const int depth = 64;
        var previousTheme = ThemeWithControl(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithControl(Color.Rgb(4, 5, 6));
        var propagatedTheme = ThemeWithControl(Color.Rgb(7, 8, 9));
        var root = new ProbeContainer();
        var controls = new List<ControlBase> { root };
        var parent = root;

        for (var index = 1; index < depth; index++)
        {
            var child = new ProbeContainer();
            parent.Children.Add(child);
            controls.Add(child);
            parent = child;
        }

        root.PropagateTheme(previousTheme);
        var previousClearCounts = controls
            .Select(control => control.ResolvedAppearanceCacheInvalidationCount)
            .ToArray();

        root.SetTheme(currentTheme);

        for (var index = 0; index < controls.Count; index++)
        {
            controls[index].ResolvedAppearanceCacheInvalidationCount
                .ShouldBe(previousClearCounts[index] + 1);
        }

        var directClearCounts = controls
            .Select(control => control.ResolvedAppearanceCacheInvalidationCount)
            .ToArray();

        root.PropagateTheme(propagatedTheme);

        for (var index = 0; index < controls.Count; index++)
        {
            controls[index].ResolvedAppearanceCacheInvalidationCount
                .ShouldBe(directClearCounts[index] + 1);
        }
    }

    /// <summary>Verifies a Face change render-invalidates every descendant, not only the control it
    /// was set on. Clearing a descendant's resolved-appearance cache without also marking it render
    /// bit lets a render-clean descendant take the render-clean-reuse copy path and paint stale
    /// previous-frame cells that never reflect the freshly cleared cache.</summary>
    [Fact]
    public void Face_WhenChanged_RenderInvalidatesEveryDescendant()
    {
        var grandchild = new ProbeControl();
        var child = new ProbeContainer();
        var root = new ProbeContainer();
        child.Children.Add(grandchild);
        root.Children.Add(child);
        root.Clear(Invalidation.All);
        child.Clear(Invalidation.All);
        grandchild.Clear(Invalidation.All);

        var previousFace = root.Face;
        root.Face = new Face(
            previousFace.Foreground,
            Color.Rgb(1, 2, 3),
            previousFace.Attributes,
            previousFace.Underline,
            previousFace.UnderlineColor);

        ((root.Pending & Invalidation.Render) != 0).ShouldBeTrue();
        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
        ((grandchild.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies IsAppearanceBoundary defaults to false, leaving a transparent-background
    /// descendant free to inherit its parent's ambient face - the behavior every control has until
    /// it opts out.</summary>
    [Fact]
    public void IsAppearanceBoundary_WhenUnset_DefaultsToFalseAndInheritsAmbientFace()
    {
        var parent = new ProbeContainer { Face = AppearanceTestValues.Face(foreground: Color.Rgb(7, 8, 9)) };
        var child = new StyledProbe
        {
            Style = StyleWith(face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)))
        };
        parent.Children.Add(child);

        child.IsAppearanceBoundary.ShouldBeFalse();
        child.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(7, 8, 9));
    }

    /// <summary>Verifies setting IsAppearanceBoundary stops ambient face inheritance - the boundary
    /// control falls back to its own authored foreground instead of the parent's - and requests a
    /// repaint so the now-differently-resolved face reaches the screen.</summary>
    [Fact]
    public void IsAppearanceBoundary_WhenSetTrue_StopsAmbientInheritanceAndInvalidatesRender()
    {
        var parent = new ProbeContainer { Face = AppearanceTestValues.Face(foreground: Color.Rgb(7, 8, 9)) };
        var child = new StyledProbe
        {
            Style = StyleWith(face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)))
        };
        parent.Children.Add(child);
        child.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(7, 8, 9));
        child.Clear(Invalidation.All);

        child.IsAppearanceBoundary = true;

        child.IsAppearanceBoundary.ShouldBeTrue();
        child.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(1, 2, 3));
        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies a local style suppresses unchanged resolved notifications across Theme identity changes.</summary>
    [Fact]
    public void SetTheme_WhenExplicitStyleKeepsOutputEqual_NotifiesOnlyTheme()
    {
        var previousTheme = ThemeWithInputFace(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithInputFace(Color.Rgb(4, 5, 6));
        var local = StyleWith(face: AppearanceTestValues.Face(foreground: Color.Rgb(7, 8, 9)));
        var control = new StyledProbe { Style = local };
        control.SetTheme(previousTheme);
        var expectedFace = control.ActualFace;
        var expectedBorder = control.ActualBorder;
        var expectedShadow = control.ActualShadow;
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) =>
        {
            control.Theme.ShouldBeSameAs(currentTheme);
            control.ActualStyle.ShouldBe(local);
            control.ActualFace.ShouldBe(expectedFace);
            control.ActualBorder.ShouldBe(expectedBorder);
            control.ActualShadow.ShouldBe(expectedShadow);
            notifications.Add(eventArgs.PropertyName);
        };
        control.Clear(Invalidation.All);

        control.SetTheme(currentTheme);

        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBe([nameof(ControlBase.Theme)]);
        notifications.ShouldNotContain(string.Empty);
    }

    /// <summary>Verifies Theme-owned style publication precedes exact changed appearance notifications.</summary>
    [Fact]
    public void SetTheme_WhenThemeOwnedStyleChanges_PublishesCommittedExactValuesInOrder()
    {
        var previousTheme = ThemeWithInputFace(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithInputFace(Color.Rgb(4, 5, 6));
        var currentStyle = ButtonStyle.Definition.Resolve(null, currentTheme);
        var control = new StyledProbe();
        control.SetTheme(previousTheme);
        var expectedBorder = control.ActualBorder;
        var expectedShadow = control.ActualShadow;
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) =>
        {
            control.Theme.ShouldBeSameAs(currentTheme);
            control.ActualStyle.ShouldBe(currentStyle);
            control.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
            control.ActualBorder.ShouldBe(expectedBorder);
            control.ActualShadow.ShouldBe(expectedShadow);
            notifications.Add(eventArgs.PropertyName);
        };

        control.SetTheme(currentTheme);

        notifications.ShouldBe(
        [
            nameof(ControlBase.Theme),
            nameof(StyledProbe.ActualStyle),
            nameof(ControlBase.ActualFace)
        ]);
        notifications.ShouldNotContain(string.Empty);
    }

    /// <summary>Verifies an ordinary role control publishes Theme and only concretely changed appearance members.</summary>
    [Fact]
    public void SetTheme_WhenStyleAppearanceChanges_PublishesExactAppearanceWithoutWildcard()
    {
        var previousTheme = ThemeWithControl(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithControl(Color.Rgb(4, 5, 6));
        var control = new ProbeControl();
        control.SetTheme(previousTheme);
        var expectedBorder = control.ActualBorder;
        var expectedShadow = control.ActualShadow;
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) =>
        {
            control.Theme.ShouldBeSameAs(currentTheme);
            control.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
            control.ActualBorder.ShouldBe(expectedBorder);
            control.ActualShadow.ShouldBe(expectedShadow);
            notifications.Add(eventArgs.PropertyName);
        };

        control.SetTheme(currentTheme);

        notifications.ShouldBe([nameof(ControlBase.Theme), nameof(ControlBase.ActualFace)]);
        notifications.ShouldNotContain(string.Empty);
    }

    /// <summary>Verifies earlier subtree observers cannot erase a later committed resolved-style notification.</summary>
    [Fact]
    public void PropagateTheme_WhenEarlierObserverChangesChildOwnership_PreservesStagedActualStyleChange()
    {
        var currentStyle = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6)));
        var previousTheme = ThemeWithInputFace(Color.Rgb(1, 2, 3));
        var currentTheme = ThemeWithInputFace(Color.Rgb(7, 8, 9));
        var child = new StyledProbe();
        var root = new Stack { Children = { child } };
        root.PropagateTheme(previousTheme);
        var childNotifications = new List<string?>();
        child.PropertyChanged += (_, eventArgs) => childNotifications.Add(eventArgs.PropertyName);
        root.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.Theme))
            {
                child.Style = currentStyle;
            }
        };

        root.PropagateTheme(currentTheme);

        child.Style.ShouldBe(currentStyle);
        childNotifications.ShouldContain(nameof(StyledProbe.ActualStyle));
    }

    /// <summary>Verifies a Theme's per-state "input" overrides - the role section StyledProbe's
    /// ButtonStyle falls back to - supply distinct normal and active resolved appearance. A style
    /// value can no longer embed its own per-state profile (only Normal), and a leaf declares no
    /// theme section of its own any more, so per-state customization is now exclusively driven by
    /// the declared fallback's own Theme JSON.</summary>
    [Fact]
    public void ActualAppearance_WhenThemeProvidesPerStateAppearance_UsesNormalAndActiveValues()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create(
            // "activeBorderColor" (not "activeBorder") because a bare "activeBorder" palette key
            // would collide with the real SemanticColor.ActiveBorder role name - Theme.ResolveSectionColor
            // checks semantic-enum names before palette keys, so a colliding name silently resolves to
            // the theme's semantic ActiveBorder color instead of this palette entry.
            palette: "\"normalFace\":\"#010203\",\"normalBorder\":\"#040506\",\"activeFace\":\"#070809\",\"activeBorderColor\":\"#0a0b0c\",\"activeShadow\":\"#0d0e0f\"",
            inputSides: "\"all\"",
            inputBorderExtra: ", \"foreground\": \"normalBorder\"",
            inputExtra: """, "face": { "foreground": "normalFace" }, "shadow": { "visible": false } """,
            inputStates:
            """, "pointerOver": { "face": { "foreground": "activeFace" }, "border": { "foreground": "activeBorderColor" }, "shadow": { "background": "activeShadow" } } """));
        var control = new StyledProbe();
        control.SetTheme(theme);

        var normalFaceActual = control.ActualFace;
        var normalBorderActual = control.ActualBorder;
        var normalShadowActual = control.ActualShadow;
        var activeFace = control.GetActualFace(VisualState.IsPointerOver);
        var activeBorder = control.GetActualBorder(VisualState.IsPointerOver);
        var activeShadow = control.GetActualShadow(VisualState.IsPointerOver);

        normalFaceActual.Foreground.Literal.ShouldBe(Color.Rgb(1, 2, 3));
        normalBorderActual.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
        normalShadowActual.IsVisible.ShouldBeFalse();
        activeFace.Foreground.Literal.ShouldBe(Color.Rgb(7, 8, 9));
        activeBorder.Foreground.Literal.ShouldBe(Color.Rgb(10, 11, 12));
        activeShadow.Background.Literal.ShouldBe(Color.Rgb(13, 14, 15));
    }

    /// <summary>Verifies the protected virtual profile property owns current resolved appearance.</summary>
    [Fact]
    public void ActualAppearance_WhenDerivedPropertyOverridesAppearanceStates_UsesEveryOverriddenMember()
    {
        var expectedFace = AppearanceTestValues.Face(
            foreground: Color.Rgb(21, 22, 23),
            attributes: TerminalAttributes.Bold);
        var expectedBorder = AppearanceTestValues.Border(
            BorderSide.All,
            foreground: Color.Rgb(24, 25, 26),
            attributes: TerminalAttributes.None);
        var expectedShadow = AppearanceTestValues.Shadow(
            visible: true,
            offset: new Point(2, 1),
            foreground: Color.Rgb(27, 28, 29),
            attributes: TerminalAttributes.None);
        var profile = new AppearanceStates(
            new ControlAppearance(expectedFace, expectedBorder, expectedShadow));
        var control = new StyledProbe { AppearanceStatesOverride = profile };

        var actualFace = control.ActualFace;
        var actualBorder = control.ActualBorder;
        var actualShadow = control.ActualShadow;

        control.Profile.ShouldBeSameAs(profile);
        actualFace.ShouldBe(expectedFace);
        actualBorder.ShouldBe(expectedBorder);
        actualShadow.ShouldBe(expectedShadow);
    }

    /// <summary>Verifies nullable local ownership follows local, Theme, and fallback precedence.</summary>
    [Fact]
    public void Style_WhenSetAndReset_FollowsLocalThemeAndFallbackPrecedence()
    {
        var control = new StyledProbe();

        control.ActualStyle.ShouldBe(ButtonStyle.Definition.Resolve(null, ThemeCatalog.Dark));
        var theme = ThemeWithInputFace(Color.Rgb(50, 60, 70));
        control.SetTheme(theme);
        var themedStyle = ButtonStyle.Definition.Resolve(null, theme);
        control.ActualStyle.ShouldBe(themedStyle);

        control.Style = ButtonStyle.Standard;
        control.ActualStyle.ShouldBe(ButtonStyle.Standard);

        control.Style = null;
        control.Style.ShouldBeNull();
        control.ActualStyle.ShouldBe(themedStyle);
    }

    /// <summary>Verifies semantic-equal local assignments do not notify or invalidate.</summary>
    [Fact]
    public void Style_WhenAssignedSemanticEqualValue_IsNoOp()
    {
        var control = new StyledProbe { Style = ButtonStyle.Standard };
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.Clear(Invalidation.All);

        control.Style = new ButtonStyle(
            ButtonStyle.Standard.Face,
            ButtonStyle.Standard.Border,
            ButtonStyle.Standard.Shadow,
            ButtonStyle.Standard.Padding);

        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies visual-only appearance changes request rendering without layout. A
    /// style value can no longer embed its own per-state overlay (only Normal), so this
    /// exercises a Normal-only visual change rather than a state-specific one.</summary>
    [Fact]
    public void Style_WhenOnlyVisualAppearanceChanges_InvalidatesRender()
    {
        var baseline = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)));
        var changed = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6)));
        var control = new StyledProbe { Style = baseline };
        control.Clear(Invalidation.All);

        control.Style = changed;

        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies changing enabled border edges requests complete measurement.</summary>
    [Fact]
    public void Style_WhenBorderSidesChange_InvalidatesMeasure()
    {
        var control = new StyledProbe { Style = StyleWith(borderSides: BorderSide.None) };
        control.Clear(Invalidation.All);

        control.Style = StyleWith(borderSides: BorderSide.All);

        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies changing shadow footprint visibility or offset requests complete measurement.</summary>
    [Fact]
    public void Style_WhenShadowFootprintChanges_InvalidatesMeasure()
    {
        var control = new StyledProbe { Style = StyleWith(shadowVisible: false) };
        control.Clear(Invalidation.All);

        control.Style = StyleWith(shadowVisible: true, shadowOffset: new Point(1, 1));

        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies local ownership changes without a resolved change publish only the local property.</summary>
    [Fact]
    public void Style_WhenLocalOwnershipChangesButResolvedStyleIsEqual_DoesNotInvalidateDerivedValues()
    {
        var theme = ThemeCatalog.Parse(ThemeJson.Create());
        var control = new StyledProbe();
        control.SetTheme(theme);
        var themedStyle = ButtonStyle.Definition.Resolve(null, theme);
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.Clear(Invalidation.All);

        control.Style = themedStyle;

        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBe([nameof(StyledProbe.Style)]);
    }

    /// <summary>Verifies distinct semantic authoring that resolves identically requests no phase work.</summary>
    [Fact]
    public void Style_WhenStructuralAndResolvedVisualValuesAreEqual_DoesNotInvalidate()
    {
        var theme = ThemeCatalog.Dark;
        var semantic = StyleWith(
            face: AppearanceTestValues.Face(foreground: SemanticColor.ControlText));
        var literal = StyleWith(
            face: AppearanceTestValues.Face(
                foreground: theme.ResolveColor(SemanticColor.ControlText)));
        var control = new StyledProbe { Style = semantic };
        control.SetTheme(theme);
        control.Clear(Invalidation.All);

        control.Style = literal;

        control.Style.ShouldBe(literal);
        control.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies style observers receive committed local, resolved, and appearance values in stable order.</summary>
    [Fact]
    public void Style_WhenAppearanceChanges_NotifiesAfterEveryResolvedValueIsCommitted()
    {
        var expected = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)),
            borderForeground: Color.Rgb(4, 5, 6),
            shadowVisible: true,
            shadowOffset: new Point(1, 1));
        var control = new StyledProbe();
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) =>
        {
            control.Style.ShouldBe(expected);
            control.ActualStyle.ShouldBe(expected);
            control.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(1, 2, 3));
            control.ActualBorder.Sides.ShouldBe(BorderSide.None);
            control.ActualBorder.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
            control.ActualShadow.IsVisible.ShouldBeTrue();
            control.ActualShadow.Offset.ShouldBe(new Point(1, 1));
            notifications.Add(eventArgs.PropertyName);
        };

        control.Style = expected;

        notifications.ShouldBe(
        [
            nameof(StyledProbe.Style),
            nameof(StyledProbe.ActualStyle),
            nameof(ControlBase.ActualFace),
            nameof(ControlBase.ActualBorder),
            nameof(ControlBase.ActualShadow)
        ]);
    }

    /// <summary>Verifies attached style mutation fails before changing local ownership.</summary>
    [Fact]
    public async Task Style_WhenAttachedAndMutatedOffDispatcher_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new StyledProbe();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.Style = ButtonStyle.Filled);

        control.Style.ShouldBeNull();
    }

    /// <summary>Verifies every live resolved-style getter serves an off-dispatcher snapshot without
    /// mutating either the typed-style or appearance cache.</summary>
    [Fact]
    public async Task ActualStyle_WhenAttachedAndReadOffDispatcher_ResolvesWithoutCachingAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new StyledProbe();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);
        var resolutions = control.UncachedAppearanceResolutionCount;

        _ = control.ActualStyle.ShouldNotBeNull();
        _ = control.ActualFace;
        _ = control.ActualBorder;
        _ = control.ActualShadow;

        control.UncachedAppearanceResolutionCount.ShouldBe(resolutions);
    }

    /// <summary>Verifies prospective Theme impact is calculated while the inherited Theme remains
    /// unchanged. Driven through "input" - StyledProbe's ButtonStyle falls back to it, and Border
    /// passes through unchanged from ButtonStyle.Complete's fallback argument - since a leaf
    /// declares no theme section of its own, and Padding (the member the original scenario used)
    /// is now a fixed code-owned constant with no theme lever left at all.</summary>
    [Fact]
    public void SetTheme_WhenResolvedStyleChanges_CommitsOnlyAfterCalculatingImpact()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create());
        var current = ThemeCatalog.Parse(ThemeJson.Create(inputSides: "\"none\""));
        var control = new StyledProbe();
        control.SetTheme(previous);
        control.Clear(Invalidation.All);

        control.SetTheme(current);

        control.ThemeObservedDuringImpact.ShouldBeSameAs(previous);
        control.Theme.ShouldBeSameAs(current);
        control.ActualStyle.ShouldBe(ButtonStyle.Definition.Resolve(null, current));
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies an explicit literal local style ignores unrelated Theme style replacement.</summary>
    [Fact]
    public void SetTheme_WhenExplicitStyleMakesThemeIrrelevant_DoesNotInvalidateResolvedValues()
    {
        var previous = ThemeWithInputFace(Color.Rgb(10, 20, 30));
        var current = ThemeWithInputFace(Color.Rgb(40, 50, 60));
        var local = StyleWith(face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)));
        var control = new StyledProbe { Style = local };
        control.SetTheme(previous);
        control.Clear(Invalidation.All);

        control.SetTheme(current);

        control.Theme.ShouldBeSameAs(current);
        control.ActualStyle.ShouldBe(local);
        control.Pending.ShouldBe(Invalidation.None);
    }

    /// <summary>Verifies an inactive Theme state does not synchronously schedule layout until that state becomes active.</summary>
    [Fact]
    public void SetTheme_WhenOnlyInactiveStateChangesBorderSides_DefersMeasureUntilStateActivates()
    {
        var previous = ThemeWithControlStateOverride("");
        var current = ThemeWithControlStateOverride(
            "\"pointerOver\": { \"border\": { \"sides\": \"all\" } }");
        var control = new ProbeControl();
        control.SetTheme(previous);
        control.Clear(Invalidation.All);

        control.SetTheme(current);

        control.Pending.ShouldBe(Invalidation.None);
        control.SetPointerOver(value: true, directlyOver: true);
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies IsEnabled routes through the same invalidation-impact decision every other
    /// visual-state driver (SetFocused, SetPressed, ...) already makes, instead of the previously
    /// hard-coded Render - a themed disabled border that changes chrome geometry must re-measure
    /// and re-arrange, or content is left painted under the new border instead of moved out of its
    /// way.</summary>
    [Fact]
    public void IsEnabled_WhenDisabledStateChangesBorderSides_RequestsMeasureNotOnlyRender()
    {
        var previous = ThemeWithControlStateOverride("");
        var current = ThemeWithControlStateOverride(
            "\"disabled\": { \"border\": { \"sides\": \"all\" } }");
        var control = new ProbeControl();
        control.SetTheme(previous);
        control.Clear(Invalidation.All);

        control.SetTheme(current);

        control.Pending.ShouldBe(Invalidation.None);
        control.IsEnabled = false;
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies a descendant that inherits Disabled from an ancestor's IsEnabled change
    /// gets its own invalidation-impact decision, not a hard-coded Render forwarded from the
    /// ancestor - an inherited state change must repair the descendant's chrome geometry exactly
    /// as if that descendant had reached the state directly.</summary>
    [Fact]
    public void IsEnabled_WhenDescendantInheritsDisabledAndBorderSidesChange_RequestsMeasureForDescendant()
    {
        var theme = ThemeWithControlStateOverride(
            "\"disabled\": { \"border\": { \"sides\": \"all\" } }");
        var child = new ProbeControl();
        var parent = new ProbeContainer();
        parent.Children.Add(child);
        parent.PropagateTheme(theme);
        parent.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        parent.IsEnabled = false;

        child.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies ordinary disabling - a theme whose disabled state authors no border or
    /// geometry override - requests only a repaint, not a re-measure. This is the negative
    /// counterpart to <see cref="IsEnabled_WhenDisabledStateChangesBorderSides_RequestsMeasureNotOnlyRender"/>:
    /// most disabled controls only recolor, and demanding a full measure for every one of them would
    /// be wasted layout work on the common path.</summary>
    [Fact]
    public void IsEnabled_WhenDisabledStateDoesNotChangeGeometry_RequestsRenderOnly()
    {
        var theme = ThemeWithControlStateOverride("");
        var control = new ProbeControl();
        control.SetTheme(theme);
        control.Clear(Invalidation.All);

        control.IsEnabled = false;

        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a disabled control's Bounds and DesiredSize at a genuinely different slot
    /// size match an enabled control arranged directly at that same size. A same-size before/after
    /// comparison on one instance would be vacuous - the layout engine short-circuits an arrange
    /// pass whose offered slot did not change - so this forces a real re-measure and re-arrange by
    /// moving to a different size after disabling, then compares against an independent enabled
    /// control instead of the pre-disable snapshot.</summary>
    [Fact]
    public void Arrange_WhenDisabledAtGenuinelyDifferentSlotSize_MatchesEnabledGeometry()
    {
        var theme = ThemeWithControlStateOverride("");

        var enabledChild = StretchingChild();
        var enabledContainer = StretchingContainer(enabledChild);
        enabledContainer.PropagateTheme(theme);
        new LayoutEngine().Layout(enabledContainer, new Size(20, 10));

        var disabledChild = StretchingChild();
        var disabledContainer = StretchingContainer(disabledChild);
        disabledContainer.PropagateTheme(theme);
        new LayoutEngine().Layout(disabledContainer, new Size(6, 3));
        disabledChild.IsEnabled = false;

        new LayoutEngine().Layout(disabledContainer, new Size(20, 10));

        disabledChild.Bounds.ShouldBe(enabledChild.Bounds);
        disabledChild.DesiredSize.ShouldBe(enabledChild.DesiredSize);
    }

    /// <summary>Verifies non-specialized controls resolve the universal ControlStyle appearance.</summary>
    [Fact]
    public void ActualAppearance_WhenControlIsNotSpecialized_UsesTheUniversalControlStyle()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        control.ActualFace.ShouldBe(control.GetActualFace(VisualState.Normal));
        control.ActualBorder.Sides.ShouldBe(ThemeCatalog.Dark.Control.Normal.Border.Sides);
        control.ActualBorder.Foreground.Literal.ShouldBe(
            ThemeCatalog.Dark.ResolveColor(SemanticColor.ControlBorder));
    }

    /// <summary>Verifies an explicit complete border wins over every theme state.</summary>
    [Fact]
    public void ActualBorder_WhenDeveloperBorderIsSet_WinsOverFocusedThemeBorder()
    {
        var expected = CreateBorder(Color.Rgb(1, 2, 3));
        var control = new ProbeControl { Border = expected };
        control.SetTheme(ThemeCatalog.Dark);

        var actual = control.GetActualBorder(VisualState.Focused);

        actual.ShouldBe(expected);
    }

    /// <summary>Verifies resetting a complete border returns ownership to the semantic profile.</summary>
    [Fact]
    public void ResetBorder_WhenThemeIsActive_ReturnsOwnershipToTheme()
    {
        var control = new ProbeControl { Border = CreateBorder(Color.Rgb(1, 2, 3)) };
        control.SetTheme(ThemeCatalog.Dark);

        control.ResetBorder();

        var actual = control.ActualBorder;
        actual.Sides.ShouldBe(ThemeCatalog.Dark.Control.Normal.Border.Sides);
        actual.Foreground.Literal.ShouldBe(ThemeCatalog.Dark.ResolveColor(SemanticColor.ControlBorder));
    }

    /// <summary>Verifies resetting a border whose local Sides matches the resolved theme Sides
    /// invalidates only Render - the documented exact-phase distinction ResetBorder draws between a
    /// chrome-geometry-changing reset and a color-only one.</summary>
    [Fact]
    public void ResetBorder_WhenLocalSidesMatchThemeSides_InvalidatesRenderOnly()
    {
        var control = new ProbeControl
        {
            Border = new Border(
                ThemeCatalog.Dark.Control.Normal.Border.Sides,
                BorderGlyphStyle.Ascii,
                Color.Rgb(1, 2, 3),
                Color.Transparent,
                TerminalAttributes.None)
        };
        control.SetTheme(ThemeCatalog.Dark);
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.Clear(Invalidation.All);

        control.ResetBorder();

        control.Pending.ShouldBe(Invalidation.Render);
        notifications.ShouldBe([nameof(ControlBase.Border), nameof(ControlBase.ActualBorder)]);
    }

    /// <summary>Verifies resetting a border whose local Sides differs from the resolved theme Sides
    /// invalidates Measure (and its dependents) since the chrome-geometry footprint itself changes.</summary>
    [Fact]
    public void ResetBorder_WhenLocalSidesDifferFromThemeSides_InvalidatesMeasure()
    {
        ThemeCatalog.Dark.Control.Normal.Border.Sides.ShouldNotBe(BorderSide.All);
        var control = new ProbeControl { Border = CreateBorder(Color.Rgb(1, 2, 3)) };
        control.SetTheme(ThemeCatalog.Dark);
        control.Clear(Invalidation.All);

        control.ResetBorder();

        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies resetting an already-theme-owned border is a documented no-op: no
    /// invalidation and no property notification.</summary>
    [Fact]
    public void ResetBorder_WhenNoLocalBorderIsSet_IsNoOp()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);
        control.Clear(Invalidation.All);
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        control.ResetBorder();

        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies resetting a complete local face returns ownership to the semantic
    /// appearance and raises both documented property notifications.</summary>
    [Fact]
    public void ResetFace_WhenLocalFaceIsSet_ReturnsOwnershipToThemeAndNotifies()
    {
        var control = new ProbeControl
        {
            Face = new Face(Color.Rgb(1, 2, 3), Color.Rgb(4, 5, 6), TerminalAttributes.None, Underline.None, Color.Default)
        };
        control.SetTheme(ThemeCatalog.Dark);
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.Clear(Invalidation.All);

        var themeOwnedControl = new ProbeControl();
        themeOwnedControl.SetTheme(ThemeCatalog.Dark);

        control.ResetFace();

        control.Face.ShouldBe(themeOwnedControl.Face);
        control.ActualFace.ShouldBe(themeOwnedControl.ActualFace);
        control.Face.ShouldNotBe(new Face(Color.Rgb(1, 2, 3), Color.Rgb(4, 5, 6), TerminalAttributes.None, Underline.None, Color.Default));
        notifications.ShouldBe([nameof(ControlBase.Face), nameof(ControlBase.ActualFace)]);
        ((control.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies resetting a Face render-invalidates every descendant, mirroring the
    /// direct <see cref="ControlBase.Face"/> setter's subtree-wide ambient-appearance
    /// invalidation.</summary>
    [Fact]
    public void ResetFace_WhenLocalFaceIsSet_RenderInvalidatesEveryDescendant()
    {
        var grandchild = new ProbeControl();
        var child = new ProbeContainer();
        var root = new ProbeContainer { Face = new Face(Color.Rgb(1, 2, 3), Color.Rgb(4, 5, 6), TerminalAttributes.None, Underline.None, Color.Default) };
        child.Children.Add(grandchild);
        root.Children.Add(child);
        root.Clear(Invalidation.All);
        child.Clear(Invalidation.All);
        grandchild.Clear(Invalidation.All);

        root.ResetFace();

        ((root.Pending & Invalidation.Render) != 0).ShouldBeTrue();
        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
        ((grandchild.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies resetting an already-theme-owned face is a documented no-op: no
    /// invalidation and no property notification.</summary>
    [Fact]
    public void ResetFace_WhenNoLocalFaceIsSet_IsNoOp()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);
        control.Clear(Invalidation.All);
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        control.ResetFace();

        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies resetting a complete local shadow returns ownership to the semantic
    /// appearance, render-invalidates, and raises both documented property notifications.</summary>
    [Fact]
    public void ResetShadow_WhenLocalShadowIsSet_ReturnsOwnershipToThemeAndNotifies()
    {
        var control = new ProbeControl
        {
            Shadow = new Shadow(
                isVisible: true,
                ShadowMode.Composite,
                new Point(1, 1),
                new Rune('#'),
                Color.Rgb(1, 2, 3),
                Color.Transparent,
                TerminalAttributes.None)
        };
        control.SetTheme(ThemeCatalog.Dark);
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.Clear(Invalidation.All);

        var themeOwnedControl = new ProbeControl();
        themeOwnedControl.SetTheme(ThemeCatalog.Dark);

        control.ResetShadow();

        control.Shadow.ShouldBe(themeOwnedControl.Shadow);
        control.ActualShadow.ShouldBe(themeOwnedControl.ActualShadow);
        control.Shadow.IsVisible.ShouldBeFalse();
        notifications.ShouldBe([nameof(ControlBase.Shadow), nameof(ControlBase.ActualShadow)]);
        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies resetting an already-theme-owned shadow is a documented no-op: no
    /// invalidation and no property notification.</summary>
    [Fact]
    public void ResetShadow_WhenNoLocalShadowIsSet_IsNoOp()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);
        control.Clear(Invalidation.All);
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        control.ResetShadow();

        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies an explicit state set may intentionally override a complete local value.</summary>
    [Fact]
    public void SetAppearance_WhenLocalStateSetIsPresent_OverridesLocalCompositeMember()
    {
        var control = new ProbeControl { Border = CreateBorder(Color.Rgb(1, 2, 3)) };
        control.SetTheme(ThemeCatalog.Dark);
        control.SetStateAppearance(
            VisualState.IsPointerOver,
            new AppearanceOverlay(border: new BorderOverlay(foreground: Color.Rgb(9, 8, 7))));

        var actual = control.GetActualBorder(VisualState.IsPointerOver);

        actual.Foreground.Literal.ShouldBe(Color.Rgb(9, 8, 7));
        actual.GlyphStyle.ShouldBe(BorderGlyphStyle.Ascii);
    }

    /// <summary>Verifies all actual appearance values contain concrete terminal values.</summary>
    [Fact]
    public void ActualAppearance_WhenThemeValuesAreUsed_ContainsOnlyLiterals()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        var face = control.ActualFace;
        var border = control.ActualBorder;
        var shadow = control.ActualShadow;

        face.Foreground.IsLiteral.ShouldBeTrue();
        face.Attributes.IsLiteral.ShouldBeTrue();
        border.Foreground.IsLiteral.ShouldBeTrue();
        border.Attributes.IsLiteral.ShouldBeTrue();
        shadow.Foreground.IsLiteral.ShouldBeTrue();
        shadow.Attributes.IsLiteral.ShouldBeTrue();
    }

    /// <summary>Verifies a state overlay that changes border sides reruns layout when the state changes.</summary>
    [Fact]
    public void SetPointerOver_WhenStateBorderSidesAreConfigured_InvalidatesMeasure()
    {
        var control = new ProbeControl();
        control.SetStateAppearance(
            VisualState.IsPointerOver,
            new AppearanceOverlay(border: new BorderOverlay(sides: BorderSide.All)));
        control.Clear(Invalidation.All);

        control.SetPointerOver(value: true, directlyOver: true);

        control.Pending.ShouldBe(Invalidation.All);
    }

    private static Border CreateBorder(Color foreground) => new(
        BorderSide.All,
        BorderGlyphStyle.Ascii,
        foreground,
        Color.Transparent,
        TerminalAttributes.None);

    private static ButtonStyle StyleWith(
        Face? face = null,
        BorderSide borderSides = BorderSide.None,
        Color? borderForeground = null,
        bool shadowVisible = false,
        Point shadowOffset = default) => new(
        face ?? AppearanceTestValues.Face(),
        AppearanceTestValues.Border(
            borderSides,
            foreground: borderForeground ?? Color.Default),
        AppearanceTestValues.Shadow(
            visible: shadowVisible,
            offset: shadowOffset),
        new Thickness(horizontal: 1, vertical: 0));

    private static string Hex(Color color) => $"#{color.Red:x2}{color.Green:x2}{color.Blue:x2}";

    /// <summary>Builds a theme whose "input" key's Normal face carries the given literal
    /// foreground, keeping every other member at the theme's ordinary "input" defaults - used to
    /// give ButtonStyle-owned probes a distinguishable Theme-resolved appearance. Uses
    /// <see cref="ThemeJson.Create"/>'s <c>inputExtra</c> insertion point (merged into "input"'s
    /// own "normal" object), never a duplicate top-level "input" key - <c>ThemeCatalog.JsonOptions</c>
    /// sets <c>AllowDuplicateProperties = false</c>, so a second sibling "input"/"control" key
    /// throws <see cref="System.Text.Json.JsonException"/> instead of "last one wins".</summary>
    private static Theme ThemeWithInputFace(Color foreground) => ThemeCatalog.Parse(ThemeJson.Create(
        palette: $"\"inputFace\":\"{Hex(foreground)}\"",
        inputExtra: """, "face": { "foreground": "inputFace" }"""));

    /// <summary>Builds a theme whose "control" key's Normal face carries the given literal
    /// foreground, keeping Border/Shadow (and every other member) fixed at an unrelated color -
    /// unlike <see cref="ThemeJson.Create"/>'s bare <c>foreground</c> parameter, which also drives
    /// "controlBorder" by default, this pins <c>controlBorderForeground</c> to a stable literal so
    /// a Face-only color change never incidentally also changes Border.</summary>
    private static Theme ThemeWithControl(Color foreground) =>
        ThemeCatalog.Parse(ThemeJson.Create(foreground: Hex(foreground), controlBorderForeground: "#888888"));

    /// <summary>Builds a theme whose "control" key authors the ordinary "normal"/"focused"
    /// defaults plus the given extra raw per-state JSON sibling (e.g. a "pointerOver" or
    /// "disabled" override) - see <see cref="ThemeWithInputFace"/>'s remarks on why this merges
    /// into "control" via <see cref="ThemeJson.Create"/>'s <c>controlExtra</c> parameter rather
    /// than a duplicate top-level "control" key.</summary>
    private static Theme ThemeWithControlStateOverride(string stateOverrideJson) =>
        ThemeCatalog.Parse(ThemeJson.Create(
            controlExtra: stateOverrideJson.Length == 0 ? "" : $", {stateOverrideJson}"));

    /// <summary>The regression the <c>CommitStyle</c> half exists to pin: a control whose active
    /// state masks its own Normal change still has to schedule a frame for the descendants that
    /// inherit that Normal.
    ///
    /// <para>The report frames this with a disabled Button, which is not quite right - a Pressable's
    /// ambient face is its ACTIVE state, so a pinned disabled overlay means its caption genuinely
    /// sees no change. The defect needs a control whose ambient face is its Normal while its own
    /// impact is computed from the masked active state, which is what this probe is.</para>
    /// </summary>
    [Fact]
    public void Style_WhenTheActiveStateMasksTheNormalChange_StillRenderInvalidatesDescendants()
    {
        using var owner = new StyledProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Author(
            VisualState.Disabled,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(200, 200, 200))));
        owner.IsEnabled = false;
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Style = owner.ActualStyle with
        {
            Face = owner.ActualStyle.Face with { Foreground = Color.Rgb(1, 2, 3) }
        };

        (owner.Pending & Invalidation.Render).ShouldBe(
            Invalidation.Render,
            "the subtree walk starts at the control itself, whose own ambient face moved");
        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue(
            "the child inherits the changed Normal even though the owner's disabled face is pinned");
    }

    /// <summary>Verifies the same for a plainly-visible change, so the fix is not specific to the
    /// masking case that made it detectable.</summary>
    [Fact]
    public void Style_WhenTheNormalFaceChanges_RenderInvalidatesEveryDescendant()
    {
        using var owner = new StyledProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Style = owner.ActualStyle with
        {
            Face = owner.ActualStyle.Face with { Foreground = Color.Rgb(4, 5, 6) }
        };

        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>The counter-case that keeps the cheap path alive: a style change that leaves the
    /// Normal face alone must not render-invalidate the whole subtree. This is the common case, and
    /// the subtree walk is deliberately unconditional once entered.</summary>
    [Fact]
    public void Style_WhenOnlyPaddingChanges_DoesNotRenderInvalidateDescendants()
    {
        using var owner = new StyledProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Style = owner.ActualStyle with { Padding = new Thickness(3, 1) };

        ((child.Pending & Invalidation.Render) != 0).ShouldBeFalse();
    }

    /// <summary>The counter-case the two pre-existing rationale-bearing tests already state for the
    /// self-only path, restated for the subtree walk: two authored faces that RESOLVE identically
    /// must not invalidate. Comparing the raw values would render-invalidate a whole subtree for a
    /// semantic reference replaced by the literal its theme maps it to.</summary>
    [Fact]
    public void Style_WhenTheNewFaceResolvesIdentically_DoesNotRenderInvalidateDescendants()
    {
        using var owner = new StyledProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.SetTheme(ThemeCatalog.Dark);
        owner.Style = owner.ActualStyle with
        {
            Face = owner.ActualStyle.Face with { Foreground = SemanticColor.ControlText }
        };
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Style = owner.ActualStyle with
        {
            Face = owner.ActualStyle.Face with
            {
                Foreground = ThemeCatalog.Dark.ResolveColor(SemanticColor.ControlText)
            }
        };

        ((child.Pending & Invalidation.Render) != 0).ShouldBeFalse();
    }

    /// <summary>The regression the <c>SetAppearance</c> half exists to pin. A Pressable's ambient
    /// face is its ACTIVE state, so a per-state overlay is folded into the face its caption
    /// inherits - and clearing only the button left the caption holding a resolved appearance built
    /// from the previous overlay.</summary>
    [Fact]
    public void SetAppearance_WhenTheControlsAmbientFaceIsItsActiveState_RenderInvalidatesDescendants()
    {
        using var owner = new AppearanceProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Author(
            VisualState.IsPointerOver,
            new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.Accent)));

        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>Verifies removing that overlay is treated identically - the state it described is
    /// just as gone from the ambient face as it was newly present.</summary>
    [Fact]
    public void SetAppearance_WhenAnOverlayIsRemoved_RenderInvalidatesDescendants()
    {
        using var owner = new AppearanceProbeContainer();
        var child = new ProbeControl();
        owner.Children.Add(child);
        owner.Author(
            VisualState.IsPointerOver,
            new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.Accent)));
        owner.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        owner.Author(VisualState.IsPointerOver, null);

        ((child.Pending & Invalidation.Render) != 0).ShouldBeTrue();
    }

    /// <summary>The counter-case for that half: a control whose ambient face is its Normal keeps the
    /// cheap self-only clear, since no per-state overlay of its can reach a descendant.</summary>
    [Fact]
    public void SetAppearance_WhenTheControlsAmbientFaceIsNormal_DoesNotRenderInvalidateDescendants()
    {
        using var container = new NormalAmbientProbeContainer();
        var child = new ProbeControl();
        container.Children.Add(child);
        container.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        container.Author(
            VisualState.IsPointerOver,
            new AppearanceOverlay(face: new FaceOverlay(foreground: SemanticColor.Accent)));

        ((child.Pending & Invalidation.Render) != 0).ShouldBeFalse();
    }

    // Both concrete controls whose ambient face is their active state - Button, via InputBase's
    // caption capability, and ListItem - are sealed, so the flag is set here directly instead. That
    // keeps these two tests on the branch being changed rather than on which controls happen to set
    // the flag, which InputBaseTests and ListItem's own tests already assert for themselves.

    /// <summary>A container with its own primary style slot, so <c>CommitStyle</c> runs on a
    /// control that actually has descendants. The library controls that own a style slot are either
    /// sealed or Pressables, whose ambient face is their active state.</summary>
    private sealed class StyledProbeContainer: Container
    {
        private readonly StyleSlot<ButtonStyle> _style;

        internal StyledProbeContainer() => _style = InitializeStyle(ButtonStyle.Definition);

        internal ButtonStyle? Style
        {
            get => _style.Local;
            set => _style.Local = value;
        }

        internal ButtonStyle ActualStyle => _style.Actual;

        internal void Author(VisualState state, AppearanceOverlay? appearance) =>
            SetAppearance(state, appearance);

        protected override Size MeasureOverride(Constraint constraint)
        {
            _ = constraint;
            return default;
        }

        protected override void ArrangeOverride(Rect bounds) => _ = bounds;
    }

    /// <summary>Republishes the protected authoring seam on a control whose ambient face is its
    /// active state.</summary>
    private sealed class AppearanceProbeContainer: Container
    {
        internal override bool StateAffectsAmbientAppearance => true;

        internal void Author(VisualState state, AppearanceOverlay? appearance) =>
            SetAppearance(state, appearance);

        protected override Size MeasureOverride(Constraint constraint)
        {
            _ = constraint;
            return default;
        }

        protected override void ArrangeOverride(Rect bounds) => _ = bounds;
    }

    /// <summary>The same seam on a control whose ambient face is its Normal.</summary>
    private sealed class NormalAmbientProbeContainer: Container
    {
        internal void Author(VisualState state, AppearanceOverlay? appearance) =>
            SetAppearance(state, appearance);

        protected override Size MeasureOverride(Constraint constraint)
        {
            _ = constraint;
            return default;
        }

        protected override void ArrangeOverride(Rect bounds) => _ = bounds;
    }

    /// <summary>Verifies a style-owned border change remeasures the complete common box model.</summary>
    [Fact]
    public void Measure_WhenStyleOwnedBorderSidesChange_RecomputesReservedContentInset()
    {
        var control = new StyledProbe
        {
            Style = new ButtonStyle(
                AppearanceTestValues.Face(),
                AppearanceTestValues.Border(BorderSide.None),
                AppearanceTestValues.Shadow(visible: false),
                ButtonStyle.Standard.Padding),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        new LayoutEngine().Layout(control, new Size(20, 10));

        control.Style = new ButtonStyle(
            AppearanceTestValues.Face(),
            AppearanceTestValues.Border(BorderSide.All),
            AppearanceTestValues.Shadow(visible: false),
            ButtonStyle.Standard.Padding);
        new LayoutEngine().Layout(control, new Size(20, 10));

        control.ContentBounds.ShouldBe(new Rect(1, 1, 18, 8));
        control.MeasureCalls.ShouldBe(2);
    }

    /// <summary>Verifies a complete border reserves one cell on every content edge.</summary>
    [Fact]
    public void Arrange_WhenContainerHasBorder_InsetsChildByBorder()
    {
        var child = StretchingChild();
        var container = StretchingContainer(child);
        container.Border = AppearanceTestValues.Border(BorderSide.All);

        new LayoutEngine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(1, 1, 18, 8));
    }

    /// <summary>Verifies border and padding reserve distinct, ordered content insets.</summary>
    [Fact]
    public void Arrange_WhenBorderAndPaddingAreSet_InsetsChildByBoth()
    {
        var child = StretchingChild();
        var container = StretchingContainer(child);
        container.Border = AppearanceTestValues.Border(BorderSide.All);
        container.Padding = new Thickness(1);

        new LayoutEngine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(2, 2, 16, 6));
    }

    /// <summary>Verifies a complete border contributes both physical edges to desired size.</summary>
    [Fact]
    public void Measure_WhenContainerHasBorder_DesiredSizeIncludesBorder()
    {
        var child = new ProbeControl(new Size(4, 2));
        var container = new LayoutProbe { Border = AppearanceTestValues.Border(BorderSide.All) };
        container.Children.Add(child);

        container.Measure(new Constraint(null, null));

        container.DesiredSize.ShouldBe(new Size(6, 4));
    }

    /// <summary>Verifies the zero-border default preserves the complete arranged slot.</summary>
    [Fact]
    public void Arrange_WhenNoBorder_LeavesChildAtFullSlot()
    {
        var child = StretchingChild();
        var container = StretchingContainer(child);

        new LayoutEngine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(0, 0, 20, 10));
    }

    /// <summary>Verifies partial physical edges reserve only their active cells.</summary>
    [Fact]
    public void Arrange_WhenBorderEdgesArePartial_ReservesOnlyActiveEdges()
    {
        var child = StretchingChild();
        var container = StretchingContainer(child);
        container.Border = AppearanceTestValues.Border(BorderSide.Left | BorderSide.Top);

        new LayoutEngine().Layout(container, new Size(20, 10));

        child.Bounds.ShouldBe(new Rect(1, 1, 19, 9));
    }

    /// <summary>Verifies combined geometric insets saturate before constraint subtraction.</summary>
    [Fact]
    public void Measure_WhenCombinedInsetExceedsInteger_SaturatesWithoutThrowing()
    {
        var child = new ProbeControl();
        var container = new LayoutProbe
        {
            Padding = new Thickness(int.MaxValue - 1, 0, 0, 0),
            Border = AppearanceTestValues.Border(BorderSide.Left | BorderSide.Right)
        };
        container.Children.Add(child);

        Should.NotThrow(() => container.Measure(new Constraint(10, 10)));

        child.MeasureConstraints.ShouldHaveSingleItem()
            .ShouldBe(new Constraint(0, 10));
    }

    private static ProbeControl StretchingChild() => new()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch,
        VerticalAlignment = VerticalAlignment.Stretch
    };

    private static LayoutProbe StretchingContainer(ControlBase child)
    {
        var container = new LayoutProbe
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        container.Children.Add(child);
        return container;
    }

    /// <summary>Verifies repeated reads share one cached resolution until the theme changes.</summary>
    [Fact]
    public void ActualFace_WhenReadRepeatedly_UsesStateCacheUntilThemeChanges()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        _ = control.ActualFace;
        _ = control.ActualFace;

        control.UncachedAppearanceResolutionCount.ShouldBe(1);

        control.SetTheme(ThemeCatalog.White);
        _ = control.ActualFace;

        control.UncachedAppearanceResolutionCount.ShouldBe(2);
    }

    /// <summary>Verifies exact visual-state flag sets occupy independent cache entries.</summary>
    [Fact]
    public void GetActualFace_WhenStatesDiffer_CachesEachExactState()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);

        _ = control.GetActualFace(VisualState.Normal);
        _ = control.GetActualFace(VisualState.IsPointerOver);
        _ = control.GetActualFace(VisualState.IsPointerOver);

        control.UncachedAppearanceResolutionCount.ShouldBe(2);
    }

    /// <summary>Verifies the sparse cache grows past its small inline capacity and still resolves
    /// every distinct state exactly once — the cache starts at 4 slots rather than the full
    /// 512-combination VisualState space.</summary>
    [Fact]
    public void GetActualFace_WhenMoreStatesThanInlineCapacityAreUsed_StillCachesEachExactly()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);
        VisualState[] states =
        [
            VisualState.Normal,
            VisualState.IsPointerOver,
            VisualState.Focused,
            VisualState.Selected,
            VisualState.Checked,
            VisualState.Pressed,
            VisualState.Disabled
        ];

        foreach (var state in states)
        {
            _ = control.GetActualFace(state);
        }

        control.UncachedAppearanceResolutionCount.ShouldBe(states.Length);

        foreach (var state in states)
        {
            _ = control.GetActualFace(state);
        }

        control.UncachedAppearanceResolutionCount.ShouldBe(states.Length);
    }

    /// <summary>Verifies changing a local state set clears previously resolved entries.</summary>
    [Fact]
    public void SetAppearance_WhenCacheExists_ClearsResolvedEntries()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);
        _ = control.ActualBorder;

        control.SetStateAppearance(
            VisualState.IsPointerOver,
            new AppearanceOverlay(border: new BorderOverlay(foreground: Color.Rgb(1, 2, 3))));
        _ = control.ActualBorder;

        control.UncachedAppearanceResolutionCount.ShouldBe(2);
    }

    /// <summary>Verifies a complete developer face remains authoritative over ambient inheritance,
    /// regardless of its own transparency — unlike Normal or a state overlay, a LocalFace is a
    /// complete override commonly authored with its own foreground and a left-default transparent
    /// background (e.g. FigletText, Spinner), so it keeps opting out of inheritance entirely.</summary>
    [Fact]
    public void ActualFace_WhenLocalTransparentFaceIsSet_PreservesDeveloperForeground()
    {
        var parent = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3))
        };
        var child = new ProbeControl
        {
            Face = AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6))
        };
        parent.Children.Add(child);
        parent.SetTheme(ThemeCatalog.Dark);

        child.ActualFace.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
    }

    /// <summary>Verifies a partial developer state face is applied after ambient inheritance.</summary>
    [Fact]
    public void GetActualFace_WhenLocalStateFaceIsSet_PreservesDeveloperForeground()
    {
        var parent = new ProbeContainer
        {
            Face = AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3))
        };
        var child = new ProbeControl();
        child.SetStateAppearance(
            VisualState.IsPointerOver,
            new AppearanceOverlay(face: new FaceOverlay(foreground: Color.Rgb(4, 5, 6))));
        parent.Children.Add(child);
        parent.SetTheme(ThemeCatalog.Dark);

        child.GetActualFace(VisualState.IsPointerOver).Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
    }
}
