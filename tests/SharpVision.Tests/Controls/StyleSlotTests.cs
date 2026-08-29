// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies framework-owned complete-style slots.
///
/// <para>Also verifies a theme swap publishes <c>ActualStyle</c> whenever the value a viewer would
/// see actually changed - which is not the same question as whether the two style records compare
/// equal. A raw record comparison sees symbolic references. Two themes that map the same
/// <c>SemanticColor</c> to different literals produce style values that compare equal and render
/// differently, so the slot resolves each side against its own theme first. That reasoning was
/// applied to <c>ControlColor</c> and stopped there - while <c>ControlDecoration</c> has the
/// identical semantic/literal split, and <c>Face</c>, <c>Border</c>, and <c>Shadow</c> attributes
/// are all <c>ControlDecoration</c> that every bundled theme authors symbolically. Rendering stayed
/// correct, because invalidation goes through <c>Compare</c>, which resolves properly; only the
/// binding was left stale, indefinitely.</para>
/// </summary>
public sealed class StyleSlotTests
{
    /// <summary>Verifies the generic base owns the standard slot, facade, and callback.</summary>
    [Fact]
    public void Style_WhenGenericControlAssignsAndResets_UsesFrameworkFacade()
    {
        // Arrange
        var control = new GenericStyleProbe();
        var local = ButtonStyle.Filled with { Padding = new Thickness(3) };

        // Act
        control.Style = local;
        var assigned = control.ActualStyle;
        control.Style = null;

        // Assert
        assigned.ShouldBe(local);
        control.ActualStyle.ShouldBe(ProbeStyle(ThemeCatalog.Dark));
        control.StyleChanges.ShouldBe(2);
    }

    /// <summary>Verifies local assignment and reset use one framework-owned resolution path.</summary>
    [Fact]
    public void Local_WhenAssignedAndReset_ResolvesLocalThenTheme()
    {
        // Arrange
        var control = new StyleSlotProbe();
        var lightInput = ThemeCatalog.Load("default-light").Input.Normal;
        var local = new ButtonStyle(lightInput.Face, lightInput.Border, lightInput.Shadow, new Thickness(3));

        // Act
        control.Style = local;
        var assigned = control.ActualStyle;
        control.Style = null;
        var reset = control.ActualStyle;

        // Assert
        assigned.ShouldBe(local);
        reset.ShouldBe(ProbeStyle(ThemeCatalog.Dark));
    }

    /// <summary>Verifies binding forwards nullable ownership instead of pinning a resolved value.</summary>
    [Fact]
    public void BindStyle_WhenLocalIsAssignedAndReset_ForwardsNullableLocal()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();
        var lightInput = ThemeCatalog.Load("default-light").Input.Normal;
        var local = new ButtonStyle(lightInput.Face, lightInput.Border, lightInput.Shadow, new Thickness(3));

        // Act
        control.ButtonStyle = local;
        var assigned = control.Target.Style;
        control.ButtonStyle = null;

        // Assert
        assigned.ShouldBe(local);
        control.SecondTarget.Style.ShouldBeNull();
        control.Target.Style.ShouldBeNull();
        control.Target.ActualStyle.ShouldBe(ProbeStyle(ThemeCatalog.Dark));
    }

    /// <summary>Verifies a bound target cannot acquire a competing local owner.</summary>
    [Fact]
    public void Local_WhenSlotHasUpstreamOwner_Throws()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();

        // Act
        var darkInput = ThemeCatalog.Dark.Input.Normal;
        var exception = Should.Throw<InvalidOperationException>(() =>
            control.Target.Style = new ButtonStyle(darkInput.Face, darkInput.Border, darkInput.Shadow, new Thickness(3)));

        // Assert
        exception.Message.ShouldContain("upstream");
    }

    private static ButtonStyle ProbeStyle(Theme theme)
    {
        var input = theme.GetStyleSet(InputStyle.Default).Normal;
        return new ButtonStyle(input.Face, input.Border, input.Shadow, ButtonStyle.Standard.Padding);
    }

    /// <summary>Verifies one source fans out to every matching retained target.</summary>
    [Fact]
    public void BindStyle_WhenSourceHasMultipleTargets_ForwardsToAll()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();
        var local = ButtonStyle.Filled;

        // Act
        control.ButtonStyle = local;

        // Assert
        control.Target.Style.ShouldBe(local);
        control.SecondTarget.Style.ShouldBe(local);
    }

    /// <summary>Verifies duplicate, mismatched, and cross-tree bindings are rejected before mutation.</summary>
    [Fact]
    public void BindStyle_WhenGraphIsInvalid_Throws()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();

        // Act and assert
        _ = Should.Throw<InvalidOperationException>(control.BindDuplicate);
        _ = Should.Throw<InvalidOperationException>(control.BindMismatchedType);
        _ = Should.Throw<InvalidOperationException>(() => control.BindCrossTree(new StyleSlotProbe()));
    }

    /// <summary>Verifies target disposal releases its upstream binding edge.</summary>
    [Fact]
    public void Dispose_WhenBoundTargetIsRemoved_SourceRemainsMutable()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();
        control.Target.Dispose();

        // Act
        control.ButtonStyle = ButtonStyle.Filled;

        // Assert
        control.ButtonStyle.ShouldBe(ButtonStyle.Filled);
        control.SecondTarget.Style.ShouldBe(ButtonStyle.Filled);
    }

    /// <summary>Verifies removing a retained target releases its edge without disposal, so the old
    /// owner stops writing it and a new owner can reuse it normally.</summary>
    [Fact]
    public void BindStyle_WhenTargetIsDetached_ReleasesTheBindingEdge()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();

        // Act
        control.DetachTarget();
        control.ButtonStyle = ButtonStyle.Filled;
        control.Target.Style = ButtonStyle.Standard;
        var newParent = new Overlay { Children = { control.Target } };

        // Assert
        control.Target.Style.ShouldBe(ButtonStyle.Standard);
        control.SecondTarget.Style.ShouldBe(ButtonStyle.Filled);
        control.Target.Parent.ShouldBeSameAs(newParent);
    }

    /// <summary>Verifies reentrant source publication supersedes the outer value across the entire
    /// graph and prevents stale resolved-style notifications from resuming afterward.</summary>
    [Fact]
    public void Local_WhenSourceNotificationReenters_CommitsOnlyTheNewestGraphValue()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();
        var actualNotifications = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(StyleSlotBindingProbe.ButtonStyle) &&
                control.ButtonStyle == ButtonStyle.Standard)
            {
                control.ButtonStyle = ButtonStyle.Filled;
            }

            if (eventArgs.PropertyName == nameof(StyleSlotBindingProbe.ActualButtonStyle))
            {
                actualNotifications++;
            }
        };

        // Act
        control.ButtonStyle = ButtonStyle.Standard;

        // Assert
        control.ButtonStyle.ShouldBe(ButtonStyle.Filled);
        control.Target.Style.ShouldBe(ButtonStyle.Filled);
        control.SecondTarget.Style.ShouldBe(ButtonStyle.Filled);
        actualNotifications.ShouldBe(1);
    }

    /// <summary>Verifies one throwing target observer cannot strand later targets on an older
    /// source value; the coherent graph commits before the first publication failure escapes.</summary>
    [Fact]
    public void Local_WhenBoundTargetNotificationThrows_CommitsEveryTargetBeforeRethrowing()
    {
        // Arrange
        var control = new StyleSlotBindingProbe();
        control.Target.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(StyleSlotProbe.Style))
            {
                throw new InvalidOperationException("target publication");
            }
        };

        // Act
        var exception = Should.Throw<InvalidOperationException>(() => control.ButtonStyle = ButtonStyle.Filled);

        // Assert
        exception.Message.ShouldBe("target publication");
        control.ButtonStyle.ShouldBe(ButtonStyle.Filled);
        control.Target.Style.ShouldBe(ButtonStyle.Filled);
        control.SecondTarget.Style.ShouldBe(ButtonStyle.Filled);
    }

    /// <summary>Verifies a new binding is fully committed even when its publication throws, leaving
    /// one coherent edge and value rather than a partially initialized graph.</summary>
    [Fact]
    public void BindStyle_WhenInitialTargetPublicationThrows_CommitsOneCoherentBinding()
    {
        // Arrange
        var control = new StyleSlotBindingProbe { ButtonStyle = ButtonStyle.Filled };
        var throwPublication = true;
        control.UnboundTarget.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(StyleSlotProbe.Style) && throwPublication)
            {
                throwPublication = false;
                throw new InvalidOperationException("initial publication");
            }
        };

        // Act
        var exception = Should.Throw<InvalidOperationException>(control.BindUnboundTarget);
        control.ButtonStyle = ButtonStyle.Standard;

        // Assert
        exception.Message.ShouldBe("initial publication");
        control.UnboundTarget.Style.ShouldBe(ButtonStyle.Standard);
        _ = Should.Throw<InvalidOperationException>(() => control.UnboundTarget.Style = ButtonStyle.Filled);
    }

    /// <summary>Verifies a local style installed by a Theme notification supersedes the outer
    /// transition, which must not resume stale ActualStyle or appearance publication afterward.</summary>
    [Fact]
    public void PropagateTheme_WhenThemeNotificationAssignsLocalStyle_AbandonsStaleThemePublication()
    {
        // Arrange
        using var probe = new StyleSlotProbe();
        var actualStyleNotifications = 0;
        probe.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.Theme))
            {
                probe.Style = ButtonStyle.Filled;
            }

            if (eventArgs.PropertyName == nameof(StyleSlotProbe.ActualStyle))
            {
                actualStyleNotifications++;
            }
        };

        // Act
        probe.PropagateTheme(ThemeCatalog.White);

        // Assert
        probe.Style.ShouldBe(ButtonStyle.Filled);
        probe.ActualStyle.ShouldBe(ButtonStyle.Filled);
        actualStyleNotifications.ShouldBe(1);
    }

    /// <summary>The local-override counterpart to the file's Theme-owned regression: a slot with a
    /// local style still delegates entirely to its Theme for every <see cref="SemanticColor"/>
    /// member that style carries (here, <see cref="ButtonStyle.Filled"/>'s border color), so a swap
    /// that changes what the Theme resolves that role to must still publish, even though the slot
    /// never lost local ownership and <c>LocalValue</c> never changed.</summary>
    [Fact]
    public void PropagateTheme_WhenLocalOverrideResolvesDifferently_PublishesActualStyle()
    {
        using var probe = new StyleSlotProbe();
        probe.Style = ButtonStyle.Filled;
        var notifications = Observe(probe);
        probe.PropagateTheme(ThemeCatalog.Parse(ThemeJson.Create(controlBorderForeground: "#111111")));
        notifications.Clear();

        probe.PropagateTheme(ThemeCatalog.Parse(ThemeJson.Create(controlBorderForeground: "#eeeeee")));

        notifications.ShouldContain(nameof(StyleSlotProbe.ActualStyle));
    }

    /// <summary>The counter-case: a local override swapped between two themes that resolve every
    /// semantic member identically must not publish, so this did not simply make every swap notify
    /// while a local style is installed.</summary>
    [Fact]
    public void PropagateTheme_WhenLocalOverrideResolvesEqually_DoesNotPublish()
    {
        using var probe = new StyleSlotProbe();
        probe.Style = ButtonStyle.Filled;
        var notifications = Observe(probe);
        probe.PropagateTheme(ThemeCatalog.Parse(ThemeJson.Create()));
        notifications.Clear();

        probe.PropagateTheme(ThemeCatalog.Parse(ThemeJson.Create()));

        notifications.ShouldNotContain(nameof(StyleSlotProbe.ActualStyle));
    }

    /// <summary>Verifies repeated resolved reads use one cached value without allocating.</summary>
    [Fact]
    public void Actual_WhenReadRepeatedly_UsesCachedAllocationFreeValue()
    {
        // Arrange
        var control = new StyleSlotProbe();
        _ = control.ActualStyle;
        var minimum = long.MaxValue;

        // Act
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 1_000; index++)
            {
                _ = control.ActualStyle;
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // Assert
        control.CompletionCalls.ShouldBe(1);
        minimum.ShouldBe(0);
    }

    /// <summary>The regression this file exists to pin: two themes differing only in what their
    /// attributes table maps a symbolic decoration to must publish.</summary>
    [Fact]
    public void PropagateTheme_WhenOnlyTheAttributesTableDiffers_PublishesActualStyle()
    {
        using var probe = new StyleSlotProbe();
        var notifications = Observe(probe);
        probe.PropagateTheme(ThemeWithNormalTextAttributes("[]"));
        notifications.Clear();

        probe.PropagateTheme(ThemeWithNormalTextAttributes("\"bold\""));

        notifications.ShouldContain(nameof(StyleSlotProbe.ActualStyle));
    }

    /// <summary>The counter-case: two themes with identical tables must not publish, so this did not
    /// simply make every swap notify.</summary>
    [Fact]
    public void PropagateTheme_WhenTheAttributesTableMatches_DoesNotPublish()
    {
        using var probe = new StyleSlotProbe();
        var notifications = Observe(probe);
        probe.PropagateTheme(ThemeWithNormalTextAttributes("\"bold\""));
        notifications.Clear();

        probe.PropagateTheme(ThemeWithNormalTextAttributes("\"bold\""));

        notifications.ShouldNotContain(nameof(StyleSlotProbe.ActualStyle));
    }

    /// <summary>Verifies the color half - the case that was already handled - still works, since the
    /// decoration branch sits directly above it and shares the recursive walk.</summary>
    [Fact]
    public void PropagateTheme_WhenOnlyThePaletteDiffers_StillPublishesActualStyle()
    {
        using var probe = new StyleSlotProbe();
        var notifications = Observe(probe);
        probe.PropagateTheme(ThemeCatalog.Parse(ThemeJson.Create(foreground: "#e0e0e0")));
        notifications.Clear();

        probe.PropagateTheme(ThemeCatalog.Parse(ThemeJson.Create(foreground: "#c0c0c0")));

        notifications.ShouldContain(nameof(StyleSlotProbe.ActualStyle));
    }

    /// <summary>Verifies the slot owns semantic-color invalidation even when a third-party
    /// aggregate comparer returns None for an unchanged raw token.</summary>
    [Fact]
    public void PropagateTheme_WhenCustomSemanticColorResolvesDifferently_InvalidatesRender()
    {
        using var probe = new SemanticColorStyleProbe();
        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create(accent: "#112233")));
        _ = probe.ActualStyle;
        probe.Clear(Invalidation.All);

        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create(accent: "#aabbcc")));

        probe.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies semantic members inside ordinary immutable nested values participate in
    /// resolved ActualStyle notification without invoking computed, cyclic, or indexed getters.</summary>
    [Fact]
    public void PropagateTheme_WhenNestedOrdinaryValueResolvesDifferently_PublishesActualStyle()
    {
        using var probe = new NestedSemanticStyleProbe();
        var notifications = Observe(probe);
        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create(accent: "#112233")));
        notifications.Clear();
        probe.Clear(Invalidation.All);

        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create(accent: "#aabbcc")));

        notifications.ShouldContain(nameof(NestedSemanticStyleProbe.ActualStyle));
        probe.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies a Theme-owned resolution invokes the same projection callback as a local
    /// style commit after the new Theme is observable.</summary>
    [Fact]
    public void PropagateTheme_WhenResolvedStyleChanges_InvokesChangedCallbackWithCurrentTheme()
    {
        using var probe = new SemanticColorStyleProbe();

        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create(accent: "#112233")));
        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create(accent: "#aabbcc")));

        probe.StyleChanges.ShouldBe(2);
        probe.ProjectedColor.ShouldBe(Color.Rgb(0xaa, 0xbb, 0xcc));
    }

    /// <summary>Verifies an <c>ImmutableArray</c> member with equal contents stops reporting a
    /// change. Boxed, it compares by the underlying array reference, so two structurally identical
    /// resolutions of <c>SpinnerStyle.Frames</c> looked different every single time - the
    /// over-notifying mirror of the two gaps above. Spinner is one of the six controls
    /// <c>theme.Glyphs</c> shapes, and <c>SpinnerStyle.Frames</c>'s own init accessor always
    /// defensively copies its source (see <c>SpinnerStyle.CopyFrames</c>), so two independently
    /// parsed themes selecting the SAME glyph family still resolve two distinct, equal-by-content
    /// array instances - a leaf no longer has a theme section of its own to source frames from
    /// directly, but the value-vs-reference distinction this test exists to pin is otherwise
    /// unchanged.</summary>
    [Fact]
    public void PropagateTheme_WhenAFrameArrayResolvesEqually_DoesNotPublish()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create());
        var current = ThemeCatalog.Parse(ThemeJson.Create());

        SpinnerStyle.Definition.Resolve(null, previous).Frames
            .ShouldBe(SpinnerStyle.Definition.Resolve(null, current).Frames);
        ReportsThemeChange(previous, current).ShouldBeFalse();
    }

    /// <summary>The counter-case for that member: genuinely different frames - here, a different
    /// theme-wide glyph family - must still report a change, so the sequence comparison did not
    /// silence the notification altogether.</summary>
    [Fact]
    public void PropagateTheme_WhenAFrameArrayDiffers_ReportsAChange()
    {
        var previous = ThemeCatalog.Parse(ThemeJson.Create());
        var current = ThemeCatalog.Parse(ThemeJson.Create(glyphs: "ascii"));

        ReportsThemeChange(previous, current).ShouldBeTrue();
    }

    /// <summary>Verifies content equality applies to arbitrary ordered collection members, not
    /// only the framework's built-in Rune frame array.</summary>
    [Fact]
    public void PropagateTheme_WhenCustomCollectionContentsMatch_DoesNotPublish()
    {
        using var probe = new SequenceStyleProbe();
        var notifications = Observe(probe);
        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create()));
        notifications.Clear();

        probe.ApplyTheme(ThemeCatalog.Parse(ThemeJson.Create()));

        notifications.ShouldNotContain(nameof(SequenceStyleProbe.ActualStyle));
    }

    private static bool ReportsThemeChange(Theme previous, Theme current)
    {
        using var spinner = new Spinner();
        var notifications = Observe(spinner);
        spinner.PropagateTheme(previous);
        notifications.Clear();

        spinner.PropagateTheme(current);

        return notifications.Contains(nameof(Spinner.ActualStyle));
    }

    private static List<string?> Observe(ControlBase control)
    {
        var notifications = new List<string?>();
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        return notifications;
    }

    // Differs from its sibling ONLY in the attributes table. Every styles.* section keeps referring
    // to "normalText" symbolically, which is exactly the shape that compared equal.
    private static Theme ThemeWithNormalTextAttributes(string attributes) =>
        ThemeCatalog.Parse(ThemeJson.Create().Replace("\"normalText\":[]", $"\"normalText\":{attributes}", StringComparison.Ordinal));
}
