// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies validated mutable control properties and invalidation.</summary>
public sealed partial class ControlBaseTests
{
    /// <summary>Verifies control defaults are content-sized and initially dirty.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasDocumentedDefaults()
    {
        var control = new ProbeControl();

        control.Width.ShouldBe(Length.Auto);
        control.Height.ShouldBe(Length.Auto);
        control.MinWidth.ShouldBe(0);
        control.MinHeight.ShouldBe(0);
        control.MaxWidth.ShouldBe(int.MaxValue);
        control.MaxHeight.ShouldBe(int.MaxValue);
        control.Margin.ShouldBe(default);
        control.Padding.ShouldBe(default);
        control.Face.Background.ShouldBe(SemanticColor.Control);
        control.Border.Foreground.ShouldBe(SemanticColor.ControlBorder);
        control.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        control.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        control.Visibility.ShouldBe(Visibility.Visible);
        control.IsEnabled.ShouldBeTrue();
        control.EffectiveIsEnabled.ShouldBeTrue();
        control.IsHitTestVisible.ShouldBeTrue();
        control.CanFocus.ShouldBeFalse();
        control.TabIndex.ShouldBe(0);
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies an automatic-width control defaults to its intrinsic content width.</summary>
    [Fact]
    public void Layout_WhenHorizontalAlignmentIsDefault_UsesIntrinsicContentWidth()
    {
        var control = new ProbeControl(new Size(3, 2));

        new LayoutEngine().Layout(control, new Size(10, 6));

        control.Bounds.ShouldBe(new Rect(0, 0, 3, 6));
    }

    /// <summary>Verifies hit-test transparency does not suppress rendering or focus eligibility.</summary>
    [Fact]
    public void HitTest_WhenControlIsTransparent_RejectsPointerTargetOnly()
    {
        var control = new ProbeControl { Bounds = new Rect(0, 0, 2, 1), IsFocusable = true, IsHitTestVisible = false };

        control.HitTest(default).ShouldBeNull();
        control.CanFocus.ShouldBeTrue();
        control.EffectiveIsVisible.ShouldBeTrue();
    }

    /// <summary>Verifies inconsistent constraints are rejected before property replacement.</summary>
    [Fact]
    public void ConstraintSetter_WhenValueIsInvalid_ThrowsBeforeMutation()
    {
        var control = new ProbeControl { MinWidth = 3, MaxHeight = 8 };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.MinWidth = -1);
        _ = Should.Throw<ArgumentException>(() => control.MaxWidth = 2);
        _ = Should.Throw<ArgumentException>(() => control.MinHeight = 9);

        control.MinWidth.ShouldBe(3);
        control.MaxWidth.ShouldBe(int.MaxValue);
        control.MinHeight.ShouldBe(0);
        control.MaxHeight.ShouldBe(8);
    }

    /// <summary>Verifies each property requests only its required phase closure.</summary>
    [Fact]
    public void PropertySetter_WhenValueChanges_InvalidatesRequiredPhases()
    {
        var control = new ProbeControl();
        control.Clear(Invalidation.All);

        control.Width = Length.Cells(10);
        control.Pending.ShouldBe(Invalidation.All);
        control.Clear(Invalidation.All);

        control.HorizontalAlignment = HorizontalAlignment.Center;
        control.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
        control.Clear(Invalidation.All);

        control.IsEnabled = false;
        control.Pending.ShouldBe(Invalidation.Render);
        control.Clear(Invalidation.All);

        control.Visibility = Visibility.Hidden;
        control.Pending.ShouldBe(Invalidation.Render);
        control.Clear(Invalidation.All);

        control.Visibility = Visibility.Collapsed;
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies property change notification runs once after mutation.</summary>
    [Fact]
    public void Width_WhenChanged_RaisesPropertyChangedOnceAfterMutation()
    {
        var control = new ProbeControl();
        List<(string? Name, Length Width)> observed = [];
        control.PropertyChanged += (_, eventArgs) =>
            observed.Add((eventArgs.PropertyName, control.Width));

        control.Width = Length.Cells(12);
        control.Width = Length.Cells(12);

        observed.ShouldBe([(nameof(ControlBase.Width), Length.Cells(12))]);
    }

    /// <summary>Verifies Name defaults to null and round-trips through PropertyChanged, matching
    /// every other debugging/accessibility identifier that never affects layout or render.</summary>
    [Fact]
    public void Name_WhenSet_RoundTripsAndRaisesPropertyChanged()
    {
        var control = new ProbeControl();
        control.Name.ShouldBeNull();
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        control.Name = "search-box";

        control.Name.ShouldBe("search-box");
        notifications.ShouldBe([nameof(ControlBase.Name)]);
    }

    /// <summary>Verifies Tag defaults to null and round-trips through PropertyChanged, carrying
    /// arbitrary user data without affecting layout or render.</summary>
    [Fact]
    public void Tag_WhenSet_RoundTripsAndRaisesPropertyChanged()
    {
        var control = new ProbeControl();
        control.Tag.ShouldBeNull();
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        var value = new object();

        control.Tag = value;

        control.Tag.ShouldBeSameAs(value);
        notifications.ShouldBe([nameof(ControlBase.Tag)]);
    }

    /// <summary>Verifies TabIndex round-trips through PropertyChanged, complementing the
    /// documented-default assertion above with an explicit set/read proof.</summary>
    [Fact]
    public void TabIndex_WhenSet_RoundTripsAndRaisesPropertyChanged()
    {
        var control = new ProbeControl();
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        control.TabIndex = 5;

        control.TabIndex.ShouldBe(5);
        notifications.ShouldBe([nameof(ControlBase.TabIndex)]);
    }

    /// <summary>Verifies effective enabled state inherits and invalidates descendants.</summary>
    [Fact]
    public void IsEnabled_WhenAncestorChanges_UpdatesDescendantEffectiveState()
    {
        var parent = new ProbeContainer();
        var child = new ProbeControl();
        parent.Children.Add(child);
        parent.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        parent.IsEnabled = false;

        child.EffectiveIsEnabled.ShouldBeFalse();
        child.Pending.ShouldBe(Invalidation.Render);
        parent.IsEnabled = true;
        child.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies an ancestor's IsEnabled change publishes the derived properties it flips
    /// on itself and on every descendant whose derived value actually changed, and stays silent
    /// where nothing changed.</summary>
    [Fact]
    public void IsEnabled_WhenAncestorChanges_PublishesDerivedPropertiesOnAffectedDescendants()
    {
        var parent = new ProbeContainer();
        var focusableChild = new ProbeControl { IsFocusable = true };
        var alreadyDisabledChild = new ProbeControl { IsFocusable = true, IsEnabled = false };
        parent.Children.Add(focusableChild);
        parent.Children.Add(alreadyDisabledChild);
        List<string?> parentEvents = [];
        List<string?> childEvents = [];
        List<string?> alreadyDisabledEvents = [];
        parent.PropertyChanged += (_, eventArgs) => parentEvents.Add(eventArgs.PropertyName);
        focusableChild.PropertyChanged += (_, eventArgs) => childEvents.Add(eventArgs.PropertyName);
        alreadyDisabledChild.PropertyChanged += (_, eventArgs) => alreadyDisabledEvents.Add(eventArgs.PropertyName);

        parent.IsEnabled = false;

        parentEvents.ShouldBe([nameof(ControlBase.IsEnabled), nameof(ControlBase.EffectiveIsEnabled)]);
        childEvents.ShouldBe([
            nameof(ControlBase.EffectiveIsEnabled),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.CanTabStop)
        ]);
        alreadyDisabledEvents.ShouldBeEmpty();
    }

    /// <summary>Verifies a Visibility change publishes the derived properties it flips, leaving
    /// the unrelated EffectiveIsEnabled axis untouched.</summary>
    [Fact]
    public void Visibility_WhenAncestorChanges_PublishesDerivedPropertiesWithoutTouchingEnabledAxis()
    {
        var parent = new ProbeContainer();
        var child = new ProbeControl { IsFocusable = true };
        parent.Children.Add(child);
        List<string?> childEvents = [];
        child.PropertyChanged += (_, eventArgs) => childEvents.Add(eventArgs.PropertyName);

        parent.Visibility = Visibility.Collapsed;

        childEvents.ShouldBe([
            nameof(ControlBase.EffectiveIsVisible),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.CanTabStop)
        ]);
        child.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies both derived properties fold every level of a three-generation chain -
    /// a single disabled or hidden ancestor anywhere on the path, including the control itself,
    /// forces the corresponding derived value false regardless of every other level.</summary>
    [Theory]
    [InlineData(true, true, true, true, true, true, true, true)]
    [InlineData(false, true, true, true, true, true, false, true)]
    [InlineData(true, false, true, true, true, true, true, false)]
    [InlineData(true, true, false, true, true, true, false, true)]
    [InlineData(true, true, true, false, true, true, true, false)]
    [InlineData(true, true, true, true, false, true, false, true)]
    [InlineData(true, true, true, true, true, false, true, false)]
    [InlineData(false, false, false, false, false, false, false, false)]
    public void EffectiveState_WithThreeLevelChain_ComputesFromWholeAncestorPath(
        bool grandparentEnabled,
        bool grandparentVisible,
        bool parentEnabled,
        bool parentVisible,
        bool childEnabled,
        bool childVisible,
        bool expectedEffectiveEnabled,
        bool expectedEffectiveVisible)
    {
        var grandparent = new ProbeContainer
        {
            IsEnabled = grandparentEnabled,
            Visibility = grandparentVisible ? Visibility.Visible : Visibility.Hidden
        };
        var parent = new ProbeContainer
        {
            IsEnabled = parentEnabled,
            Visibility = parentVisible ? Visibility.Visible : Visibility.Hidden
        };
        var child = new ProbeControl
        {
            IsEnabled = childEnabled,
            Visibility = childVisible ? Visibility.Visible : Visibility.Hidden
        };
        grandparent.Children.Add(parent);
        parent.Children.Add(child);

        child.EffectiveIsEnabled.ShouldBe(expectedEffectiveEnabled);
        child.EffectiveIsVisible.ShouldBe(expectedEffectiveVisible);
    }

    /// <summary>Verifies a cached EffectiveIsVisible read before an ancestor's Visibility change
    /// does not strand the descendant on the stale value - the next read after the change must
    /// invalidate and recompute, and toggling back must recompute again rather than sticking.</summary>
    [Fact]
    public void EffectiveIsVisible_WhenAncestorVisibilityToggles_InvalidatesCachedValueOnNextRead()
    {
        var parent = new ProbeContainer();
        var child = new ProbeControl();
        parent.Children.Add(child);

        child.EffectiveIsVisible.ShouldBeTrue();

        parent.Visibility = Visibility.Collapsed;
        child.EffectiveIsVisible.ShouldBeFalse();

        parent.Visibility = Visibility.Visible;
        child.EffectiveIsVisible.ShouldBeTrue();
    }

    /// <summary>Verifies a cached EffectiveIsEnabled read before an ancestor's IsEnabled change
    /// does not strand the descendant on the stale value, mirroring the Visibility proof above
    /// for the independent enabled axis.</summary>
    [Fact]
    public void EffectiveIsEnabled_WhenAncestorIsEnabledToggles_InvalidatesCachedValueOnNextRead()
    {
        var parent = new ProbeContainer();
        var child = new ProbeControl();
        parent.Children.Add(child);

        child.EffectiveIsEnabled.ShouldBeTrue();

        parent.IsEnabled = false;
        child.EffectiveIsEnabled.ShouldBeFalse();

        parent.IsEnabled = true;
        child.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies moving a control (and its own subtree) from one parent to a differently
    /// enabled/visible parent invalidates its cached derived state - a grandchild read before the
    /// move must reflect the new ancestor chain, not the one cached under the old parent.</summary>
    [Fact]
    public void EffectiveState_WhenReparentedToDifferentSubtree_RecomputesFromNewAncestorChain()
    {
        var oldParent = new ProbeContainer { IsEnabled = true, Visibility = Visibility.Visible };
        var newParent = new ProbeContainer { IsEnabled = false, Visibility = Visibility.Hidden };
        var child = new ProbeContainer();
        var grandchild = new ProbeControl();
        child.Children.Add(grandchild);
        oldParent.Children.Add(child);

        grandchild.EffectiveIsEnabled.ShouldBeTrue();
        grandchild.EffectiveIsVisible.ShouldBeTrue();

        oldParent.Children.Remove(child).ShouldBeTrue();
        newParent.Children.Add(child);

        grandchild.EffectiveIsEnabled.ShouldBeFalse();
        grandchild.EffectiveIsVisible.ShouldBeFalse();
    }

    /// <summary>Verifies a change at the root of a several-hundred-level chain still reaches a
    /// deeply nested descendant's next read - the memoized recursive walk must not stop short
    /// partway down, and must not overflow invalidating a deep subtree either.</summary>
    [Fact]
    public void EffectiveIsVisible_WhenRootTogglesInDeepChain_PropagatesToDeepDescendant()
    {
        const int depth = 300;
        var root = new ProbeContainer();
        var current = root;

        for (var i = 0; i < depth - 1; i++)
        {
            var next = new ProbeContainer();
            current.Children.Add(next);
            current = next;
        }

        var leaf = new ProbeControl();
        current.Children.Add(leaf);

        leaf.EffectiveIsVisible.ShouldBeTrue();

        root.Visibility = Visibility.Collapsed;

        leaf.EffectiveIsVisible.ShouldBeFalse();
    }

    /// <summary>Verifies changing IsFocusable directly (not through an ancestor's IsEnabled or
    /// Visibility) also publishes the CanFocus and CanTabStop it flips, matching the ancestor-driven
    /// paths above instead of stopping at IsFocusable and CanFocus.</summary>
    [Fact]
    public void Focusable_WhenChanged_PublishesCanFocusAndIsTabStop()
    {
        var control = new ProbeControl();
        List<string?> events = [];
        control.PropertyChanged += (_, eventArgs) => events.Add(eventArgs.PropertyName);

        control.IsFocusable = true;

        events.ShouldBe([
            nameof(ControlBase.IsFocusable),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.CanTabStop)
        ]);
        control.CanTabStop.ShouldBeTrue();
    }

    /// <summary>Verifies changing IsTabStop publishes the CanTabStop it directly flips, since
    /// CanTabStop = CanFocus &amp;&amp; IsTabStop depends on IsTabStop just as much as on CanFocus.</summary>
    [Fact]
    public void TabStop_WhenChanged_PublishesIsTabStop()
    {
        var control = new ProbeControl { IsFocusable = true };
        List<string?> events = [];
        control.PropertyChanged += (_, eventArgs) => events.Add(eventArgs.PropertyName);

        control.IsTabStop = false;

        events.ShouldBe([nameof(ControlBase.IsTabStop), nameof(ControlBase.CanTabStop)]);
        control.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies a throwing visibility subscriber cannot strand manager ownership.</summary>
    [Fact]
    public async Task Visibility_WhenPropertySubscriberThrows_ReleasesFocusAndCaptureBeforeRethrowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { IsFocusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            focus.Focus(child).ShouldBeTrue();
            child.CaptureProbePointer().ShouldBeTrue();
            var expected = new InvalidOperationException("The visibility subscriber failed.");
            ControlBase? observedFocus = null;
            ControlBase? observedCapture = null;
            child.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(ControlBase.Visibility))
                {
                    return;
                }

                observedFocus = focus.Focused;
                observedCapture = capture.Captured;
                throw expected;
            };

            var exception = Should.Throw<InvalidOperationException>(() => child.Visibility = Visibility.Collapsed);

            exception.ShouldBeSameAs(expected);
            child.Visibility.ShouldBe(Visibility.Collapsed);
            observedFocus.ShouldBeNull();
            observedCapture.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies cleanup failure remains authoritative while enabled-state publication completes.</summary>
    [Fact]
    public async Task IsEnabled_WhenCleanupAndPropertySubscriberThrow_CompletesCleanupAndPreservesFirstFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { IsFocusable = true, ThrowOnPointerCaptureCancellation = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            focus.Focus(child).ShouldBeTrue();
            child.CaptureProbePointer().ShouldBeTrue();
            var propertySubscriberRan = false;
            child.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(ControlBase.IsEnabled))
                {
                    return;
                }

                propertySubscriberRan = true;
                focus.Focused.ShouldBeNull();
                capture.Captured.ShouldBeNull();
                throw new InvalidOperationException("The enabled subscriber failed.");
            };

            var exception = Should.Throw<InvalidOperationException>(() => child.IsEnabled = false);

            exception.Message.ShouldBe("The probe capture-cancellation callback failed.");
            child.IsEnabled.ShouldBeFalse();
            propertySubscriberRan.ShouldBeTrue();
            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies attached property mutation is dispatcher-affine.</summary>
    [Fact]
    public async Task Width_WhenAttachedAndSetOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.Width = Length.Cells(3));

        control.Width.ShouldBe(Length.Auto);
    }

    /// <summary>Verifies hit-test policy mutation is dispatcher-affine.</summary>
    [Fact]
    public async Task IsHitTestVisible_WhenAttachedAndSetOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.IsHitTestVisible = false);

        control.IsHitTestVisible.ShouldBeTrue();
    }

    /// <summary>Verifies the public Invalidate() requests only Render, leaving composition-based
    /// code that does not own a subclass able to request a repaint without the protected
    /// measure/arrange seam.</summary>
    [Fact]
    public void Invalidate_WhenCalled_RequestsRenderOnly()
    {
        var control = new ProbeControl();
        control.Clear(Invalidation.All);

        control.Invalidate();

        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies the public Invalidate() is dispatcher-affine, matching every other public
    /// mutation seam instead of silently deferring.</summary>
    [Fact]
    public async Task Invalidate_WhenAttachedAndCalledOffThread_ThrowsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(control.Invalidate);
    }

    /// <summary>Verifies invalid enums and disposed access fail before mutation.</summary>
    [Fact]
    public void Setter_WhenStateIsInvalid_ThrowsDocumentedException()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Visibility = (Visibility) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.HorizontalAlignment = (HorizontalAlignment) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.VerticalAlignment = (VerticalAlignment) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.TabNavigation = (TabNavigation) int.MaxValue);
        control.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        control.TabNavigation.ShouldBe(TabNavigation.Continue);
        control.Dispose();
        _ = Should.Throw<ObjectDisposedException>(() => control.IsEnabled = false);
    }

    /// <summary>Verifies the extent keeps the natural size when desired size is clamped smaller.</summary>
    [Fact]
    public void ContentExtent_WhenConstraintClampsDesired_KeepsNaturalSize()
    {
        var probe = new ProbeControl(new Size(20, 40));

        probe.Measure(new Constraint(10, 12));

        probe.DesiredSize.ShouldBe(new Size(10, 12));
        probe.ExposedContentExtent.ShouldBe(new Size(20, 40));
    }

    /// <summary>Verifies a shrink-wrapping control ignores stretch and sizes to content.</summary>
    [Fact]
    public void Arrange_WhenShrinkWrapsWidth_SizesToContentDespiteStretch()
    {
        var probe = new ShrinkProbe(new Size(6, 2)) { HorizontalAlignment = HorizontalAlignment.Stretch };

        probe.Measure(new Constraint(20, 20));
        probe.Arrange(new Rect(0, 0, 20, 20));

        probe.Bounds.Width.ShouldBe(6);
    }

    /// <summary>Verifies a direct parent is returned when it matches the requested type.</summary>
    [Fact]
    public void FindAncestor_WhenParentMatchesType_ReturnsParent()
    {
        var probe = new AncestorProbe();
        using var container = new ProbeContainer();
        container.Children.Add(probe);

        probe.ExposedFindAncestor<ProbeContainer>().ShouldBeSameAs(container);
    }

    /// <summary>Verifies a grandparent is returned when the direct parent does not match.</summary>
    [Fact]
    public void FindAncestor_WhenGrandparentMatchesType_ReturnsGrandparent()
    {
        var probe = new AncestorProbe();
        using var inner = new ProbeContainer();
        using var outer = new Stack();
        inner.Children.Add(probe);
        outer.Children.Add(inner);

        probe.ExposedFindAncestor<Stack>().ShouldBeSameAs(outer);
    }

    /// <summary>Verifies null is returned when no ancestor matches the requested type.</summary>
    [Fact]
    public void FindAncestor_WhenNoAncestorMatchesType_ReturnsNull()
    {
        var probe = new AncestorProbe();
        using var container = new ProbeContainer();
        container.Children.Add(probe);

        probe.ExposedFindAncestor<Stack>().ShouldBeNull();
    }

    /// <summary>Verifies null is returned when the control has no parent.</summary>
    [Fact]
    public void FindAncestor_WhenControlHasNoParent_ReturnsNull()
    {
        var probe = new AncestorProbe();

        probe.ExposedFindAncestor<ProbeContainer>().ShouldBeNull();
    }

    /// <summary>Verifies the first matching ancestor is returned when multiple match.</summary>
    [Fact]
    public void FindAncestor_WhenMultipleAncestorsMatch_ReturnsNearest()
    {
        var probe = new AncestorProbe();
        using var inner = new ProbeContainer();
        using var outer = new ProbeContainer();
        inner.Children.Add(probe);
        outer.Children.Add(inner);

        probe.ExposedFindAncestor<ProbeContainer>().ShouldBeSameAs(inner);
    }

    /// <summary>Verifies a configured focusable control becomes ineligible when hidden.</summary>
    [Fact]
    public void CanFocus_WhenFocusableControlIsHidden_IsFalse()
    {
        var control = new ProbeControl { IsFocusable = true };

        control.CanFocus.ShouldBeTrue();
        control.Visibility = Visibility.Hidden;

        control.CanFocus.ShouldBeFalse();
    }

    /// <summary>Verifies tab participation depends on both configuration and effective focus eligibility.</summary>
    [Fact]
    public void IsTabStop_WhenFocusableAndTabStopConfigured_IsTrue()
    {
        var control = new ProbeControl { IsFocusable = true, IsTabStop = true };

        control.CanTabStop.ShouldBeTrue();
        control.IsEnabled = false;
        control.CanTabStop.ShouldBeFalse();
    }

    /// <summary>Verifies that setting a glyph fires PropertyChanged.</summary>
    [Fact]
    public void SetOptionalGlyph_WhenNewValue_FiresPropertyChanged()
    {
        var probe = new GlyphProbe();
        var changed = new List<string>();
        probe.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        probe.TestGlyph = new Rune('X');

        probe.TestGlyph.ShouldBe(new Rune('X'));
        changed.ShouldContain(nameof(GlyphProbe.TestGlyph));
    }

    /// <summary>Verifies that setting the same glyph value twice is a no-op.</summary>
    [Fact]
    public void SetOptionalGlyph_WhenSameValue_IsNoOp()
    {
        var probe = new GlyphProbe { TestGlyph = new Rune('X') };
        var changed = new List<string>();
        probe.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        probe.TestGlyph = new Rune('X');

        changed.ShouldBeEmpty();
    }

    /// <summary>Verifies that resetting a set glyph clears it and notifies.</summary>
    [Fact]
    public void ResetOptionalGlyph_WhenGlyphIsSet_ClearsAndNotifies()
    {
        var probe = new GlyphProbe { TestGlyph = new Rune('X') };
        var changed = new List<string>();
        probe.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        var result = probe.ResetTestGlyph();

        result.ShouldBeTrue();
        probe.RawTestGlyph.ShouldBeNull();
        changed.ShouldContain(nameof(GlyphProbe.TestGlyph));
    }

    /// <summary>Verifies that resetting an already-null glyph returns false without notification.</summary>
    [Fact]
    public void ResetOptionalGlyph_WhenAlreadyNull_ReturnsFalse()
    {
        var probe = new GlyphProbe();
        var changed = new List<string>();
        probe.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        var result = probe.ResetTestGlyph();

        result.ShouldBeFalse();
        changed.ShouldBeEmpty();
    }

    /// <summary>Verifies Control.ResolveColor is reachable from a third-party subclass in another
    /// assembly, which the previous internal accessibility disallowed.</summary>
    [Fact]
    public void ResolveColor_WhenCalledFromThirdPartySubclass_ResolvesLiteralAndThemeValues()
    {
        var literal = Color.Rgb(0xff, 0x00, 0x00);
        AccessibilityPromotionProbe.ProbeResolveColor(literal, ThemeCatalog.Dark).ShouldBe(literal);
        AccessibilityPromotionProbe.ProbeResolveColor(SemanticColor.Accent, ThemeCatalog.Dark)
            .ShouldBe(ThemeCatalog.Dark.ResolveColor(SemanticColor.Accent));
        AccessibilityPromotionProbe.ProbeResolveColor(SemanticColor.Accent, null).ShouldBe(Color.Default);
    }

    /// <summary>Verifies Control.MaximumImpact is reachable from a third-party subclass in another
    /// assembly, which the previous internal accessibility disallowed.</summary>
    [Theory]
    [InlineData(InvalidationImpact.None, InvalidationImpact.Render, InvalidationImpact.Render)]
    [InlineData(InvalidationImpact.Measure, InvalidationImpact.Arrange, InvalidationImpact.Measure)]
    [InlineData(InvalidationImpact.Render, InvalidationImpact.Render, InvalidationImpact.Render)]
    public void MaximumImpact_WhenCalledFromThirdPartySubclass_ReturnsStrongerImpact(
        InvalidationImpact left,
        InvalidationImpact right,
        InvalidationImpact expected) =>
        AccessibilityPromotionProbe.ProbeMaximumImpact(left, right).ShouldBe(expected);

    /// <summary>Verifies a control's MeasureOverride result flows into DesiredSize.</summary>
    [Fact]
    public void MeasureOverride_WhenControlReportsContent_DrivesDesiredSize()
    {
        var control = new FixedContent();

        control.Measure(new Constraint(20, 6));

        control.DesiredSize.ShouldBe(new Size(7, 3));
    }

    /// <summary>Verifies OnRenderAdornment runs after a control's own OnRenderContent, giving a
    /// third party a seam to paint over its own subtree instead of only beneath it - the seam
    /// RenderOverlay already provides internally, now exposed protected.</summary>
    [Fact]
    public void OnRenderAdornment_WhenControlRenders_RunsAfterOnRenderContent()
    {
        var control = new ProbeControl(new Size(4, 1));
        var order = new List<string>();
        control.Rendering = _ => order.Add("content");
        control.RenderingAdornment = _ => order.Add("adornment");
        new LayoutEngine().Layout(control, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        control.Render(frame.Canvas);

        order.ShouldBe(["content", "adornment"]);
        control.AdornmentRenderCalls.ShouldBe(1);
    }

    /// <summary>Verifies OnRenderAdornment runs after a container's owned children render, so an
    /// adornment can paint over content the subtree already committed.</summary>
    [Fact]
    public void OnRenderAdornment_WhenContainerHasChildren_RunsAfterChildRendering()
    {
        var parent = new ProbeContainer();
        var child = new ProbeControl(new Size(2, 1));
        parent.Children.Add(child);
        var order = new List<string>();
        child.Rendering = _ => order.Add("child");
        parent.RenderingAdornment = _ => order.Add("adornment");
        new LayoutEngine().Layout(parent, new Size(4, 1));
        using Frame frame = new(new Size(4, 1));

        parent.Render(frame.Canvas);

        order.ShouldBe(["child", "adornment"]);
    }

    /// <summary>Verifies OnChildrenChanged runs after Children mutations structurally commit -
    /// add, remove, and clear - giving a derived container the same notification ItemsControl
    /// already consumes on its own private presentation host.</summary>
    [Fact]
    public void OnChildrenChanged_WhenChildrenMutate_RunsAfterEachStructuralCommit()
    {
        var container = new ProbeContainer();
        var child = new ProbeControl();

        container.Children.Add(child);
        container.ChildrenChangedCalls.ShouldBe(1);

        _ = container.Children.Remove(child);
        container.ChildrenChangedCalls.ShouldBe(2);

        container.Children.Add(new ProbeControl());
        container.Children.Clear();
        container.ChildrenChangedCalls.ShouldBe(4);
    }

    /// <summary>Verifies a control with its own pressed concept can drive VisualState.Pressed
    /// through the protected IsPressedState seam, independent of the framework's own pointer/
    /// keyboard press tracking - matching IsCheckedState, IsSelectedState, IsCurrentState, and
    /// IsIndeterminateState, the four sibling seams that already exist.</summary>
    [Fact]
    public void IsPressedState_WhenOverriddenTrue_DrivesAppearanceStateIndependentlyOfIsPressed()
    {
        var control = new ProbeControl();

        control.IsPressed.ShouldBeFalse();
        control.ProbeAppearanceState.HasFlag(VisualState.Pressed).ShouldBeFalse();

        control.ForcedPressedState = true;

        control.IsPressed.ShouldBeFalse();
        control.ProbeAppearanceState.HasFlag(VisualState.Pressed).ShouldBeTrue();
    }

    /// <summary>Verifies lifecycle hooks observe the already-committed attachment state.</summary>
    [Fact]
    public async Task Lifecycle_WhenRootAttachesAndDetaches_PublishesCommittedStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new ProbeControl();

            control.Attach(dispatcher);
            control.Detach();

            control.AttachedCalls.ShouldBe(1);
            control.AttachedStateWasCommitted.ShouldBeTrue();
            control.DetachedCalls.ShouldBe(1);
            control.DetachedStateWasCommitted.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a throwing disposal hook cannot prevent terminal cleanup.</summary>
    [Fact]
    public void Dispose_WhenDisposingHookThrows_CompletesCleanupAndRunsHookOnce()
    {
        var control = new ProbeControl { ThrowOnDisposing = true };

        _ = Should.Throw<InvalidOperationException>(control.Dispose);
        control.Dispose();

        control.IsDisposed.ShouldBeTrue();
        control.DisposingCalls.ShouldBe(1);
    }

    /// <summary>Verifies property-kernel arguments are rejected before backing state changes.</summary>
    [Fact]
    public void SetProperty_WhenArgumentsAreInvalid_RejectsBeforeMutation()
    {
        var control = new ProbeControl();
        var notifications = 0;
        control.PropertyChanged += (_, _) => notifications++;

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.SetKernelValue(1, (InvalidationImpact) 99));
        _ = Should.Throw<ArgumentNullException>(() =>
            control.SetKernelValue(2, InvalidationImpact.Render, null));

        control.KernelValue.ShouldBe(0);
        notifications.ShouldBe(0);
    }

    /// <summary>Verifies notification and invalidation seams reject unknown impacts.</summary>
    [Fact]
    public void InvalidationKernel_WhenImpactIsUnknown_RejectsWithoutNotification()
    {
        var control = new ProbeControl();
        var notifications = 0;
        control.PropertyChanged += (_, _) => notifications++;

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.NotifyKernelProperty(nameof(ProbeControl.KernelValue), (InvalidationImpact) 99));
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.InvalidateKernel((InvalidationImpact) 99));

        notifications.ShouldBe(0);
    }

    /// <summary>Verifies LocalBounds reports parent-relative position.</summary>
    [Fact]
    public void LocalBounds_WhenParentHasOffset_ReportsRelativePosition()
    {
        var canvas = new Overlay { Padding = new Thickness(2) };
        var child = new ProbeControl(new Size(3, 2));
        Overlay.SetLeft(child, Length.Cells(1));
        Overlay.SetTop(child, Length.Cells(1));
        canvas.Children.Add(child);

        new LayoutEngine().Layout(canvas, new Size(20, 10));

        child.Bounds.X.ShouldBeGreaterThan(1);
        child.LocalBounds.X.ShouldBe(1);
        child.LocalBounds.Y.ShouldBe(1);
        child.LocalBounds.Width.ShouldBe(3);
        child.LocalBounds.Height.ShouldBe(2);
    }

    /// <summary>Verifies LocalBounds equals Bounds when there is no parent.</summary>
    [Fact]
    public void LocalBounds_WhenNoParent_EqualsBounds()
    {
        var control = new ProbeControl(new Size(5, 3));
        new LayoutEngine().Layout(control, new Size(10, 8));

        control.LocalBounds.ShouldBe(control.Bounds);
    }

    /// <summary>Verifies LocalBounds inside a bordered container accounts for border inset.</summary>
    [Fact]
    public void LocalBounds_WhenParentHasBorder_AccountsForBorderInset()
    {
        var dock = new Dock { Border = AppearanceTestValues.Border(BorderSide.All) };
        var child = new ProbeControl(new Size(3, 2));
        dock.Children.Add(child);

        new LayoutEngine().Layout(dock, new Size(10, 6));

        child.Bounds.X.ShouldBe(1);
        child.LocalBounds.X.ShouldBe(0);
        child.LocalBounds.Y.ShouldBe(0);
    }

    /// <summary>Verifies ContentBounds is publicly accessible.</summary>
    [Fact]
    public void ContentBounds_WhenAccessed_DeflatesBorderAndPadding()
    {
        var dock = new Dock { Border = AppearanceTestValues.Border(BorderSide.All), Padding = new Thickness(1) };
        new LayoutEngine().Layout(dock, new Size(10, 8));

        dock.ContentBounds.X.ShouldBe(2);
        dock.ContentBounds.Y.ShouldBe(2);
        dock.ContentBounds.Width.ShouldBe(6);
        dock.ContentBounds.Height.ShouldBe(4);
    }

    #region Chrome authoring capability

    /// <summary>Verifies the raw chrome-authoring surface throws until the owning control opts in,
    /// on a type that never calls <see cref="ControlBase.EnableChromeAuthoring"/> - the closed hazard
    /// this capability replaces would have silently no-op'd or exposed the member unconditionally.</summary>
    [Fact]
    public void Border_WhenChromeAuthoringIsNotEnabled_ThrowsInvalidOperationException()
    {
        var control = new TabItem();

        _ = Should.Throw<InvalidOperationException>(() => _ = control.Border);
        _ = Should.Throw<InvalidOperationException>(() => control.Border = AppearanceTestValues.Border(BorderSide.All));
        _ = Should.Throw<InvalidOperationException>(control.ResetBorder);
    }

    /// <summary>Verifies the shadow half of the same guard.</summary>
    [Fact]
    public void Shadow_WhenChromeAuthoringIsNotEnabled_ThrowsInvalidOperationException()
    {
        var control = new TabItem();

        _ = Should.Throw<InvalidOperationException>(() => _ = control.Shadow);
        _ = Should.Throw<InvalidOperationException>(() => control.Shadow = AppearanceTestValues.Shadow(visible: true));
        _ = Should.Throw<InvalidOperationException>(control.ResetShadow);
    }

    /// <summary>Verifies enabling the capability twice throws, matching every other <c>Enable*</c>
    /// seam on <see cref="InputBase"/> (press activation, an owned popup, segment editing).</summary>
    [Fact]
    public void EnableChromeAuthoring_WhenCalledTwice_ThrowsInvalidOperationException()
    {
        var control = new ChromeProbe();

        _ = Should.Throw<InvalidOperationException>(control.EnableChromeAuthoringAgain);
    }

    /// <summary>Verifies a migrated chrome-authoring container's Border/Shadow assignment reaches
    /// the actual resolved chrome the renderer reads, mirroring GroupBox's own
    /// Border_WhenAssignedLocally_ReachesTheRenderedChrome proof for the Container branch of the
    /// capability.</summary>
    [Fact]
    public void BorderAndShadow_WhenAssignedLocallyOnDock_ReachTheRenderedChrome()
    {
        var dock = new Dock
        {
            Border = AppearanceTestValues.Border(BorderSide.All, BorderGlyphStyle.Heavy),
            Shadow = AppearanceTestValues.Shadow(visible: true, offset: new Point(1, 1))
        };

        new LayoutEngine().Layout(dock, new Size(20, 6));

        dock.ActualBorder.Sides.ShouldBe(BorderSide.All);
        dock.ActualBorder.GlyphStyle.ShouldBe(BorderGlyphStyle.Heavy);
        dock.ActualShadow.IsVisible.ShouldBeTrue();
        dock.ActualShadow.Offset.ShouldBe(new Point(1, 1));
    }

    #endregion
}
