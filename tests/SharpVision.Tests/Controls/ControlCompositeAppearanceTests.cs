// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies complete local appearance values and resolved theme ownership.</summary>
public sealed class ControlCompositeAppearanceTests
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
    public void Style_WhenNestedTransparentProfilesResolveToSameAmbientFace_DoesNotInvalidateAppearance()
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
    /// previous-frame cells that never reflect the freshly cleared cache (see #239).</summary>
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

    /// <summary>Verifies a local style suppresses unchanged resolved notifications across Theme identity changes.</summary>
    [Fact]
    public void SetTheme_WhenExplicitStyleKeepsOutputEqual_NotifiesOnlyTheme()
    {
        var previousTheme = ThemeWithInputProfile(ButtonStyle.Standard.Appearance);
        var currentTheme = ThemeWithInputProfile(ButtonStyle.Filled.Appearance);
        var local = StyleWith(face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)));
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
        var previousStyle = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)));
        var currentStyle = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6)));
        var previousTheme = ThemeWithInputProfile(previousStyle.Appearance);
        var currentTheme = ThemeWithInputProfile(currentStyle.Appearance);
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
    public void SetTheme_WhenRoleProfileChanges_PublishesExactAppearanceWithoutWildcard()
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
        var previousStyle = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)));
        var currentStyle = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6)));
        var previousTheme = ThemeWithInputProfile(previousStyle.Appearance);
        var currentTheme = ThemeWithInputProfile(currentStyle.Appearance);
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

    /// <summary>Verifies a style-owned protected profile supplies normal and active resolved appearance.</summary>
    [Fact]
    public void ActualAppearance_WhenProtectedProfileChanges_UsesStyleOwnedNormalAndActiveValues()
    {
        var normalFace = AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3));
        var normalBorder = AppearanceTestValues.Border(BorderSide.All, foreground: Color.Rgb(4, 5, 6));
        var normalShadow = AppearanceTestValues.Shadow(visible: false);
        var profile = new ThemeProfile(
            new ThemeAppearance(normalFace, normalBorder, normalShadow),
            pointerOver: new AppearanceSet(
                face: new FaceSet(foreground: Color.Rgb(7, 8, 9)),
                border: new BorderSet(foreground: Color.Rgb(10, 11, 12)),
                shadow: new ShadowSet(background: Color.Rgb(13, 14, 15))));
        var control = new StyledProbe
        {
            Style = new ButtonStyle(new Thickness(horizontal: 1, vertical: 0), profile)
        };

        var normalFaceActual = control.ActualFace;
        var normalBorderActual = control.ActualBorder;
        var normalShadowActual = control.ActualShadow;
        var activeFace = control.GetActualFace(VisualState.PointerOver);
        var activeBorder = control.GetActualBorder(VisualState.PointerOver);
        var activeShadow = control.GetActualShadow(VisualState.PointerOver);

        control.Profile.ShouldBeSameAs(profile);
        normalFaceActual.Foreground.Literal.ShouldBe(Color.Rgb(1, 2, 3));
        normalBorderActual.Foreground.Literal.ShouldBe(Color.Rgb(4, 5, 6));
        normalShadowActual.IsVisible.ShouldBeFalse();
        normalShadowActual.Offset.ShouldBe(normalShadow.Offset);
        activeFace.Foreground.Literal.ShouldBe(Color.Rgb(7, 8, 9));
        activeBorder.Foreground.Literal.ShouldBe(Color.Rgb(10, 11, 12));
        activeShadow.Background.Literal.ShouldBe(Color.Rgb(13, 14, 15));
    }

    /// <summary>Verifies the protected virtual profile property owns current resolved appearance.</summary>
    [Fact]
    public void ActualAppearance_WhenDerivedPropertyOverridesProfile_UsesEveryOverriddenMember()
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
        var profile = new ThemeProfile(
            new ThemeAppearance(expectedFace, expectedBorder, expectedShadow));
        var control = new StyledProbe { AppearanceProfileOverride = profile };

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
        var themed = ButtonStyle.Filled;
        var theme = ThemeWithInputProfile(themed.Appearance);
        var control = new StyledProbe();

        control.ActualStyle.ShouldBe(InputProfileStyle(Themes.Dark.Input));
        control.SetTheme(theme);
        control.ActualStyle.ShouldBe(ThemeOwnedStyle(themed));

        control.Style = ButtonStyle.Standard;
        control.ActualStyle.ShouldBe(ButtonStyle.Standard);

        control.Style = null;
        control.Style.ShouldBeNull();
        control.ActualStyle.ShouldBe(ThemeOwnedStyle(themed));
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
            ButtonStyle.Standard.Padding,
            Copy(ButtonStyle.Standard.Appearance));

        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies visual-only and inactive-state changes request rendering without layout.</summary>
    [Fact]
    public void Style_WhenOnlyVisualOrStateAppearanceChanges_InvalidatesRender()
    {
        var baseline = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(1, 2, 3)),
            pointerOver: default);
        var changed = StyleWith(
            face: AppearanceTestValues.Face(foreground: Color.Rgb(4, 5, 6)),
            pointerOver: new AppearanceSet(face: new FaceSet(background: Color.Rgb(7, 8, 9))));
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
        var theme = ThemeWithInputProfile(ButtonStyle.Standard.Appearance);
        var control = new StyledProbe();
        control.SetTheme(theme);
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.Clear(Invalidation.All);

        control.Style = ButtonStyle.Standard;

        control.Pending.ShouldBe(Invalidation.None);
        notifications.ShouldBe([nameof(StyledProbe.Style)]);
    }

    /// <summary>Verifies distinct semantic authoring that resolves identically requests no phase work.</summary>
    [Fact]
    public void Style_WhenStructuralAndResolvedVisualValuesAreEqual_DoesNotInvalidate()
    {
        var theme = Themes.Dark;
        var semantic = StyleWith(
            face: AppearanceTestValues.Face(foreground: ThemeColor.ControlText));
        var literal = StyleWith(
            face: AppearanceTestValues.Face(
                foreground: theme.ResolveColor(ThemeColor.ControlText)));
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

    /// <summary>Verifies prospective Theme impact is calculated while the inherited Theme remains unchanged.</summary>
    [Fact]
    public void SetTheme_WhenResolvedStyleChanges_CommitsOnlyAfterCalculatingImpact()
    {
        var previous = ThemeWithInputProfile(ButtonStyle.Standard.Appearance);
        var current = ThemeWithInputProfile(ButtonStyle.Filled.Appearance);
        var control = new StyledProbe();
        control.SetTheme(previous);
        control.Clear(Invalidation.All);

        control.SetTheme(current);

        control.ThemeObservedDuringImpact.ShouldBeSameAs(previous);
        control.Theme.ShouldBeSameAs(current);
        control.ActualStyle.ShouldBe(ThemeOwnedStyle(ButtonStyle.Filled));
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies an explicit literal local style ignores unrelated Theme style replacement.</summary>
    [Fact]
    public void SetTheme_WhenExplicitStyleMakesThemeIrrelevant_DoesNotInvalidateResolvedValues()
    {
        var previous = ThemeWithInputProfile(ButtonStyle.Standard.Appearance);
        var current = ThemeWithInputProfile(ButtonStyle.Filled.Appearance);
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
        var normal = Theme.CreateDefaultAppearance(ThemeRole.Control);
        var previous = ThemeWithControlProfile(new ThemeProfile(normal));
        var current = ThemeWithControlProfile(
            new ThemeProfile(
                normal,
                pointerOver: new AppearanceSet(border: new BorderSet(sides: BorderSide.All))));
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
    /// way (see #220).</summary>
    [Fact]
    public void IsEnabled_WhenDisabledStateChangesBorderSides_RequestsMeasureNotOnlyRender()
    {
        var normal = Theme.CreateDefaultAppearance(ThemeRole.Control);
        var previous = ThemeWithControlProfile(new ThemeProfile(normal));
        var current = ThemeWithControlProfile(
            new ThemeProfile(
                normal,
                disabled: new AppearanceSet(border: new BorderSet(sides: BorderSide.All))));
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
    /// as if that descendant had reached the state directly (see #220).</summary>
    [Fact]
    public void IsEnabled_WhenDescendantInheritsDisabledAndBorderSidesChange_RequestsMeasureForDescendant()
    {
        var normal = Theme.CreateDefaultAppearance(ThemeRole.Control);
        var theme = ThemeWithControlProfile(
            new ThemeProfile(
                normal,
                disabled: new AppearanceSet(border: new BorderSet(sides: BorderSide.All))));
        var child = new ProbeControl();
        var parent = new ProbeContainer();
        parent.Children.Add(child);
        parent.PropagateTheme(theme);
        parent.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        parent.IsEnabled = false;

        child.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies non-specialized controls retain ThemeRole-based appearance resolution.</summary>
    [Fact]
    public void ActualAppearance_WhenControlIsNotSpecialized_UsesExistingThemeRoleProfile()
    {
        var control = new ProbeControl();
        control.SetTheme(Themes.Dark);

        control.ActualFace.ShouldBe(control.GetActualFace(VisualState.Normal));
        control.ActualBorder.Sides.ShouldBe(Themes.Dark.Control.Normal.Border.Sides);
        control.ActualBorder.Foreground.Literal.ShouldBe(
            Themes.Dark.ResolveColor(ThemeColor.ControlBorder));
    }

    /// <summary>Verifies an explicit complete border wins over every theme state.</summary>
    [Fact]
    public void ActualBorder_WhenDeveloperBorderIsSet_WinsOverFocusedThemeBorder()
    {
        var expected = CreateBorder(Color.Rgb(1, 2, 3));
        var control = new ProbeControl { Border = expected };
        control.SetTheme(Themes.Dark);

        var actual = control.GetActualBorder(VisualState.Focused);

        actual.ShouldBe(expected);
    }

    /// <summary>Verifies resetting a complete border returns ownership to the semantic profile.</summary>
    [Fact]
    public void ResetBorder_WhenThemeIsActive_ReturnsOwnershipToTheme()
    {
        var control = new ProbeControl { Border = CreateBorder(Color.Rgb(1, 2, 3)) };
        control.SetTheme(Themes.Dark);

        control.ResetBorder();

        var actual = control.ActualBorder;
        actual.Sides.ShouldBe(Themes.Dark.Control.Normal.Border.Sides);
        actual.Foreground.Literal.ShouldBe(Themes.Dark.ResolveColor(ThemeColor.ControlBorder));
    }

    /// <summary>Verifies an explicit state set may intentionally override a complete local value.</summary>
    [Fact]
    public void SetAppearance_WhenLocalStateSetIsPresent_OverridesLocalCompositeMember()
    {
        var control = new ProbeControl { Border = CreateBorder(Color.Rgb(1, 2, 3)) };
        control.SetTheme(Themes.Dark);
        control.SetStateAppearance(
            VisualState.PointerOver,
            new AppearanceSet(border: new BorderSet(foreground: Color.Rgb(9, 8, 7))));

        var actual = control.GetActualBorder(VisualState.PointerOver);

        actual.Foreground.Literal.ShouldBe(Color.Rgb(9, 8, 7));
        actual.GlyphStyle.ShouldBe(BorderGlyphStyle.Ascii);
    }

    /// <summary>Verifies all actual appearance values contain concrete terminal values.</summary>
    [Fact]
    public void ActualAppearance_WhenThemeValuesAreUsed_ContainsOnlyLiterals()
    {
        var control = new ProbeControl();
        control.SetTheme(Themes.Dark);

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
            VisualState.PointerOver,
            new AppearanceSet(border: new BorderSet(sides: BorderSide.All)));
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
        Point shadowOffset = default,
        AppearanceSet pointerOver = default)
    {
        var appearance = new ThemeAppearance(
            face ?? AppearanceTestValues.Face(),
            AppearanceTestValues.Border(
                borderSides,
                foreground: borderForeground ?? Color.Default),
            AppearanceTestValues.Shadow(
                visible: shadowVisible,
                offset: shadowOffset));
        return new ButtonStyle(
            new Thickness(horizontal: 1, vertical: 0),
            new ThemeProfile(appearance, pointerOver: pointerOver));
    }

    private static Theme ThemeWithInputProfile(ThemeProfile input)
    {
        var theme = new Theme();
        theme.SetProfiles(theme.Control, input, theme.Container, theme.Window, theme.Popup);
        theme.Freeze();
        return theme;
    }

    private static ButtonStyle ThemeOwnedStyle(ButtonStyle style) =>
        new(ButtonStyle.Standard.Padding, style.Appearance);

    private static ButtonStyle InputProfileStyle(ThemeProfile input) =>
        new(ButtonStyle.Standard.Padding, input);

    private static Theme ThemeWithControl(Color foreground)
    {
        var theme = new Theme();
        var appearance = Theme.CreateDefaultAppearance(ThemeRole.Control);
        theme.SetProfiles(
            new ThemeProfile(
                new ThemeAppearance(
                    new Face(
                        foreground,
                        appearance.Face.Background,
                        appearance.Face.Attributes,
                        appearance.Face.Underline,
                        appearance.Face.UnderlineColor),
                    appearance.Border,
                    appearance.Shadow)),
            theme.Input,
            theme.Container,
            theme.Window,
            theme.Popup);
        theme.Freeze();
        return theme;
    }

    private static Theme ThemeWithControlProfile(ThemeProfile profile)
    {
        var theme = new Theme();
        theme.SetProfiles(profile, theme.Input, theme.Container, theme.Window, theme.Popup);
        theme.Freeze();
        return theme;
    }

    private static ThemeProfile Copy(ThemeProfile profile) => new(
        profile.Normal,
        profile.PointerOver,
        profile.FocusWithin,
        profile.Focused,
        profile.Current,
        profile.Selected,
        profile.Checked,
        profile.Indeterminate,
        profile.Pressed,
        profile.Disabled);
}
