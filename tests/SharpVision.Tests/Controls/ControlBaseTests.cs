// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using GraphicsImage = Terminal.Graphics.ImageSource;

/// <summary>Verifies validated mutable control properties and invalidation.</summary>
public sealed class ControlBaseTests
{
    /// <summary>Verifies a failing component pointer-over hook observes committed state and cannot
    /// prevent the public exit event from observing the same transition.</summary>
    [Fact]
    public void SetPointerOver_WhenComponentHookThrows_CompletesCurrentPublicExitBeforeRethrow()
    {
        var probe = new ProbeControl();
        probe.SetPointerOver(value: true, directlyOver: true);
        probe.ThrowOnPointerOverChanged = true;
        var exits = 0;
        probe.PointerExited += (_, _) =>
        {
            exits++;
            probe.IsPointerOver.ShouldBeFalse();
            probe.IsPointerDirectlyOver.ShouldBeFalse();
            probe.PointerOverStateWasCommitted.ShouldBeTrue();
        };

        var exception = Should.Throw<InvalidOperationException>(() =>
            probe.SetPointerOver(value: false, directlyOver: false));

        exception.Message.ShouldBe("The probe pointer-over callback failed.");
        probe.PointerOverChangedCalls.ShouldBe(2);
        exits.ShouldBe(1);
        probe.IsPointerOver.ShouldBeFalse();
        probe.IsPointerDirectlyOver.ShouldBeFalse();
    }

    /// <summary>Verifies a hook that commits a newer away-and-back transition suppresses the
    /// superseded outer exit even though the final pointer values equal the original entry.</summary>
    [Fact]
    public void SetPointerOver_WhenComponentHookReentersAwayAndBack_SuppressesOuterExit()
    {
        var probe = new ProbeControl();
        var entries = 0;
        var exits = 0;
        probe.PointerEntered += (_, _) => entries++;
        probe.PointerExited += (_, _) => exits++;
        probe.SetPointerOver(value: true, directlyOver: true);
        probe.PointerOverChanging = (control, isPointerOver, _) =>
        {
            if (isPointerOver)
            {
                return;
            }

            control.PointerOverChanging = null;
            control.SetPointerOver(value: true, directlyOver: true);
        };

        probe.SetPointerOver(value: false, directlyOver: false);

        probe.IsPointerOver.ShouldBeTrue();
        probe.IsPointerDirectlyOver.ShouldBeTrue();
        probe.PointerOverChangedCalls.ShouldBe(3);
        entries.ShouldBe(2);
        exits.ShouldBe(0);
    }

    /// <summary>Verifies a newer value committed from owner publication leaves the retained
    /// projection aligned instead of letting the outer setter forward its captured candidate.</summary>
    [Fact]
    public void SetPropertyAndSynchronize_WhenPropertyObserverCommitsNewerValue_PreservesNewerProjection()
    {
        var probe = new SynchronizedPropertyProbe();
        probe.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SynchronizedPropertyProbe.Value) && probe.Value == 1)
            {
                probe.Value = 2;
            }
        };

        probe.Value = 1;

        probe.Value.ShouldBe(2);
        probe.ForwardedValue.ShouldBe(2);
    }

    /// <summary>Verifies reentry from retained-state synchronization itself supersedes the outer
    /// property publication, including an away-and-back generation.</summary>
    [Fact]
    public void SetPropertyAndSynchronize_WhenSynchronizationReenters_SuppressesOuterPublication()
    {
        var probe = new SynchronizedPropertyProbe();
        var reentered = false;
        probe.Synchronizing = () =>
        {
            if (!reentered && probe.Value == 1)
            {
                reentered = true;
                probe.Value = 2;
                probe.Value = 1;
            }
        };
        var notifications = 0;
        probe.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SynchronizedPropertyProbe.Value))
            {
                notifications++;
            }
        };

        probe.Value = 1;

        probe.Value.ShouldBe(1);
        probe.ForwardedValue.ShouldBe(1);
        notifications.ShouldBe(2);
    }

    /// <summary>Verifies a throwing property observer cannot prevent already-required retained
    /// synchronization from completing.</summary>
    [Fact]
    public void SetPropertyAndSynchronize_WhenPropertyObserverThrows_PreservesProjectionBeforeRethrow()
    {
        var probe = new SynchronizedPropertyProbe();
        probe.PropertyChanged += (_, _) => throw new InvalidOperationException("observer failure");

        _ = Should.Throw<InvalidOperationException>(() => probe.Value = 3);

        probe.Value.ShouldBe(3);
        probe.ForwardedValue.ShouldBe(3);
    }

    /// <summary>Verifies a caller-selected reference comparer commits equal mutable instances but
    /// keeps an identical instance silent.</summary>
    [Fact]
    public void SetPropertyAndSynchronize_WhenReferenceComparerReceivesEqualClone_UsesIdentity()
    {
        var baseline = new CultureInfo("en-US");
        var clone = (CultureInfo) baseline.Clone();
        var probe = new SynchronizedPropertyProbe { ReferenceValue = baseline };
        var notifications = 0;
        probe.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SynchronizedPropertyProbe.ReferenceValue))
            {
                notifications++;
            }
        };

        probe.ReferenceValue = clone;
        probe.ReferenceValue = clone;

        probe.ReferenceValue.ShouldBeSameAs(clone);
        probe.ForwardedReferenceValue.ShouldBeSameAs(clone);
        notifications.ShouldBe(1);
    }

    /// <summary>Verifies identity comparison participates in generation ownership when dependent
    /// synchronization reenters with another equal mutable instance.</summary>
    [Fact]
    public void SetPropertyAndSynchronize_WhenReferenceSynchronizationReenters_PreservesNewestIdentity()
    {
        var outer = new CultureInfo("en-US");
        var nested = (CultureInfo) outer.Clone();
        var probe = new SynchronizedPropertyProbe();
        probe.SynchronizingReference = () =>
        {
            if (ReferenceEquals(probe.ReferenceValue, outer))
            {
                probe.ReferenceValue = nested;
            }
        };

        probe.ReferenceValue = outer;

        probe.ReferenceValue.ShouldBeSameAs(nested);
        probe.ForwardedReferenceValue.ShouldBeSameAs(nested);
    }

    /// <summary>Verifies control defaults are content-sized and initially dirty.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasDocumentedDefaults()
    {
        var control = new ProbeControl();

        control.Width.ShouldBe(Length.Auto);
        control.Height.ShouldBe(Length.Auto);
        control.MinWidth.ShouldBe(Length.Cells(0));
        control.MinHeight.ShouldBe(Length.Cells(0));
        control.MaxWidth.ShouldBeNull();
        control.MaxHeight.ShouldBeNull();
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
        var control = new ProbeControl { MinWidth = Length.Cells(3), MaxHeight = Length.Cells(8) };

        _ = Should.Throw<ArgumentException>(() => control.MinWidth = Length.Auto);
        _ = Should.Throw<ArgumentException>(() => control.MaxWidth = Length.Star(1));
        _ = Should.Throw<ArgumentException>(() => control.MinHeight = Length.Cells(9));

        control.MinWidth.ShouldBe(Length.Cells(3));
        control.MaxWidth.ShouldBeNull();
        control.MinHeight.ShouldBe(Length.Cells(0));
        control.MaxHeight.ShouldBe(Length.Cells(8));
    }

    /// <summary>Verifies every limit rejects kinds whose intrinsic or proportional meaning would
    /// depend on the very size the limit is trying to constrain.</summary>
    [Fact]
    public void ConstraintSetter_WhenKindIsAutoOrStar_RejectsEveryLimitBeforeMutation()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentException>(() => control.MinWidth = Length.Auto);
        _ = Should.Throw<ArgumentException>(() => control.MinHeight = Length.Star(1));
        _ = Should.Throw<ArgumentException>(() => control.MaxWidth = Length.Auto);
        _ = Should.Throw<ArgumentException>(() => control.MaxHeight = Length.Star(1));

        control.MinWidth.ShouldBe(Length.Cells(0));
        control.MinHeight.ShouldBe(Length.Cells(0));
        control.MaxWidth.ShouldBeNull();
        control.MaxHeight.ShouldBeNull();
    }

    /// <summary>Verifies percentage limits re-resolve against each containing viewport and a
    /// resolved minimum wins when unlike authored limits cross after resizing.</summary>
    [Theory]
    [InlineData(20, 16)]
    [InlineData(40, 20)]
    public void Layout_WhenLimitsAreRelative_ResolvesAgainstCurrentContainingExtent(
        int viewportWidth,
        int expectedWidth)
    {
        var control = new ProbeControl(new Size(100, 1))
        {
            MinWidth = Length.Percent(50),
            MaxWidth = Length.Cells(16),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        new LayoutEngine().Layout(control, new Size(viewportWidth, 3));

        control.Bounds.Width.ShouldBe(expectedWidth);
    }

    /// <summary>Verifies an unbounded measurement does not turn a percentage maximum into a
    /// fabricated cell ceiling before a containing extent exists.</summary>
    [Fact]
    public void Measure_WhenPercentageMaximumIsUnbounded_PreservesIntrinsicDesiredSize()
    {
        var control = new ProbeControl(new Size(30, 4))
        {
            MaxWidth = Length.Percent(50),
            MaxHeight = Length.Percent(50)
        };

        control.Measure(new Constraint(null, null));

        control.DesiredSize.ShouldBe(new Size(30, 4));
    }

    /// <summary>Verifies arrange resolves percentages from its final slot instead of retaining
    /// the earlier measure constraint's result.</summary>
    [Fact]
    public void Arrange_WhenSlotChangesAfterMeasure_ReresolvesPercentageMaximum()
    {
        var control = new ProbeControl(new Size(100, 4))
        {
            MaxWidth = Length.Percent(50),
            MaxHeight = Length.Percent(50)
        };
        control.Measure(new Constraint(80, 20));

        control.Arrange(new Rect(0, 0, 20, 6));

        control.Bounds.ShouldBe(new Rect(0, 0, 10, 3));
    }

    /// <summary>Verifies percentage constraints remain deterministic and contained across tiny
    /// and ordinary bounds, including margin and inset geometry.</summary>
    [Fact]
    public void Layout_WhenPercentageLimitsMeetBoxGeometry_RemainsContainedAndDeterministic()
    {
        for (var viewportWidth = 0; viewportWidth <= 64; viewportWidth++)
        {
            var control = new ProbeControl(new Size(100, 1))
            {
                MinWidth = Length.Percent(25),
                MaxWidth = Length.Percent(60),
                Margin = new Thickness(1),
                Padding = new Thickness(1),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            new LayoutEngine().Layout(control, new Size(viewportWidth, 5));
            var first = control.Bounds;
            new LayoutEngine().Layout(control, new Size(viewportWidth, 5));

            control.Bounds.ShouldBe(first);
            control.Bounds.X.ShouldBeGreaterThanOrEqualTo(0);
            control.Bounds.Right.ShouldBeLessThanOrEqualTo(viewportWidth);
        }
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

    /// <summary>Verifies the public Focus() returns false without throwing for a detached control -
    /// the documented "false when detached" branch, reachable only because a detached control's
    /// inherited FocusOwner is always null.</summary>
    [Fact]
    public void Focus_WhenDetached_ReturnsFalse()
    {
        var control = new ProbeControl { IsFocusable = true };

        control.Focus().ShouldBeFalse();
        control.IsFocused.ShouldBeFalse();
    }

    /// <summary>Verifies the public Focus() acquires focus through the inherited manager and
    /// reports true, exercising the delegation this method exists to provide over calling the
    /// manager directly.</summary>
    [Fact]
    public async Task Focus_WhenAttachedAndEligible_AcquiresFocusAndReturnsTrueAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { IsFocusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);

            var acquired = child.Focus();

            acquired.ShouldBeTrue();
            child.IsFocused.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies calling the public Focus() on an already focused control is idempotent:
    /// it returns true again and leaves focus ownership unchanged, matching "already owned" in its
    /// documented return contract.</summary>
    [Fact]
    public async Task Focus_WhenAlreadyFocused_ReturnsTrueAndIsIdempotentAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { IsFocusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            child.Focus().ShouldBeTrue();

            var reacquired = child.Focus();

            reacquired.ShouldBeTrue();
            child.IsFocused.ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(child);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies the public Focus() is dispatcher-affine, matching every other public
    /// mutation seam instead of silently deferring or acquiring focus off-thread.</summary>
    [Fact]
    public async Task Focus_WhenAttachedAndCalledOffThread_ThrowsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl { IsFocusable = true };
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.Focus());

        control.IsFocused.ShouldBeFalse();
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

    #region Appearance

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
    /// appearance, measure-invalidates a changed footprint, and raises both notifications.</summary>
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
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies direct shadow assignment measure-invalidates when its footprint appears.</summary>
    [Fact]
    public void Shadow_WhenFootprintChanges_InvalidatesMeasure()
    {
        var control = new ProbeControl();
        control.SetTheme(ThemeCatalog.Dark);
        control.Clear(Invalidation.All);

        control.Shadow = new Shadow(
            isVisible: true,
            ShadowMode.Composite,
            new Point(1, 1),
            new Rune('#'),
            Color.Rgb(1, 2, 3),
            Color.Transparent,
            TerminalAttributes.None);

        control.Pending.ShouldBe(Invalidation.All);
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
    /// every distinct state exactly once — the cache starts at 2 slots rather than the full
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

    #endregion

    #region Appearance overlays

    /// <summary>Verifies live and prospective resolution compose the registered overlay once
    /// against their respective Themes.</summary>
    [Fact]
    public void ResolveAppearance_WhenOverlayIsRegistered_MatchesLiveAndProspectiveThemes()
    {
        var first = ThemeCatalog.Parse(ThemeJson.Create(accent: "#ff0000"));
        var second = ThemeCatalog.Parse(ThemeJson.Create(accent: "#00ff00"));
        var overlay = new AppearanceStatesOverlay(
            normal: new AppearanceOverlay(
                face: new FaceOverlay(foreground: SemanticColor.Accent, background: Color.Transparent)));
        var probe = new AppearanceOverlayProbe(overlay);
        probe.SetTheme(first);

        var live = probe.GetActualFace(VisualState.Normal);
        var sameTheme = probe.ResolveAppearance(first).Face;
        var prospective = probe.ResolveAppearance(second).Face;

        live.ShouldBe(sameTheme);
        live.Foreground.ShouldBe(first.ResolveColor(SemanticColor.Accent));
        prospective.Foreground.ShouldBe(second.ResolveColor(SemanticColor.Accent));
    }

    /// <summary>Verifies a complete local Face remains authoritative over the registered overlay.</summary>
    [Fact]
    public void ResolveAppearance_WhenLocalFaceIsSet_LocalValueRetainsPrecedence()
    {
        var overlay = new AppearanceStatesOverlay(
            normal: new AppearanceOverlay(
                face: new FaceOverlay(foreground: SemanticColor.Accent, background: Color.Transparent)));
        var probe = new AppearanceOverlayProbe(overlay)
        {
            Face = AppearanceTestValues.Face(foreground: ReferenceColors.Get(3))
        };

        probe.ResolveAppearance(ThemeCatalog.Dark).Face.Foreground.ShouldBe(ReferenceColors.Get(3));
    }

    /// <summary>Verifies registering a second immutable overlay is rejected before state changes.</summary>
    [Fact]
    public void InitializeAppearanceOverlay_WhenCalledTwice_Throws()
    {
        var probe = new AppearanceOverlayProbe(default);

        _ = Should.Throw<InvalidOperationException>(probe.RegisterAgain);
    }

    /// <summary>Verifies a registered state overlay that changes chrome geometry keeps ordinary
    /// visual-state invalidation classification.</summary>
    [Fact]
    public void VisualState_WhenRegisteredOverlayChangesBorderFootprint_InvalidatesMeasure()
    {
        var overlay = new AppearanceStatesOverlay(
            selected: new AppearanceOverlay(
                border: new BorderOverlay(sides: BorderSide.All)));
        var probe = new AppearanceOverlayProbe(overlay);
        probe.Clear(Invalidation.All);

        probe.CommitSelection(true);

        probe.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies <c>GetThemeChangeImpact</c>'s no-primary-style branch composes the
    /// registered overlay before comparing prospective Themes.</summary>
    /// <remarks>
    /// The two Themes deliberately resolve "control"'s own Normal face byte-identically - only
    /// "accent" differs - so the raw, uncomposed default appearance states this branch used to
    /// compare are indistinguishable and would report <see cref="InvalidationImpact.None"/>. The
    /// registered overlay's foreground references that same "accent" role, so only composing it
    /// (as <c>ApplyAppearanceOverlay</c> does) can make the two prospective states differ.
    /// </remarks>
    [Fact]
    public void GetThemeChangeImpact_WhenOverlayDependsOnChangedRole_ReportsNonNoneImpact()
    {
        var overlay = new AppearanceStatesOverlay(
            normal: new AppearanceOverlay(
                face: new FaceOverlay(foreground: SemanticColor.Accent, background: Color.Transparent)));
        var probe = new AppearanceOverlayProbe(overlay);
        var previous = ThemeCatalog.Parse(ThemeJson.Create(accent: "#ff0000"));
        var current = ThemeCatalog.Parse(ThemeJson.Create(accent: "#00ff00"));

        var impact = probe.GetThemeImpact(previous, current);

        impact.ShouldNotBe(InvalidationImpact.None);
    }

    /// <summary>Verifies <c>GetStyleThemeImpact</c>'s <c>OwnsAppearance</c> branch composes the
    /// registered overlay before comparing prospective Themes.</summary>
    /// <remarks>
    /// Mirrors <see cref="GetThemeChangeImpact_WhenOverlayDependsOnChangedRole_ReportsNonNoneImpact"/>
    /// one level deeper: the primary style slot's own resolved <see cref="TextStyle"/> and appearance
    /// never reference "accent", so only composing the registered overlay - whose foreground does -
    /// can make the two prospective appearance states differ. Neither current
    /// <c>InitializeAppearanceOverlay</c> consumer (<c>TablePresenter</c>, <c>Prism</c>) owns a
    /// primary style, so this branch has no existing production control to exercise it.
    /// </remarks>
    [Fact]
    public void GetStyleThemeImpact_WhenOverlayDependsOnChangedRole_ReportsNonNoneImpact()
    {
        var overlay = new AppearanceStatesOverlay(
            normal: new AppearanceOverlay(
                face: new FaceOverlay(foreground: SemanticColor.Accent, background: Color.Transparent)));
        var probe = new StyledAppearanceOverlayProbe(overlay);
        var previous = ThemeCatalog.Parse(ThemeJson.Create(accent: "#ff0000"));
        var current = ThemeCatalog.Parse(ThemeJson.Create(accent: "#00ff00"));

        var impact = probe.Slot.GetThemeImpact(previous, current, previousParentAmbientFace: null, currentParentAmbientFace: null);

        impact.ShouldNotBe(InvalidationImpact.None);
    }

    #endregion

    #region Attachment participants

    /// <summary>Verifies same- and cross-dispatcher reattachment and final disposal.</summary>
    [Fact]
    public async Task AttachmentParticipant_WhenOwnerReattaches_FollowsEveryCommittedLifetimeAsync()
    {
        await using var first = Dispatcher.Start();
        await using var second = Dispatcher.Start();
        var owner = new AttachmentParticipantOwner();
        var participant = new AttachmentParticipantProbe();
        owner.Register(participant);

        await first.InvokeAsync(() =>
        {
            owner.Attach(first);
            owner.Detach();
            owner.Attach(first);
            owner.Detach();
        }, TestContext.Current.CancellationToken);
        await second.InvokeAsync(() =>
        {
            owner.Attach(second);
            owner.Dispose();
        }, TestContext.Current.CancellationToken);

        participant.Attachments.ShouldBe([first, first, second]);
        participant.DetachCalls.ShouldBe(2);
        participant.DisposeCalls.ShouldBe(1);
    }

    /// <summary>Verifies one participant failure does not skip later registration-order callbacks.</summary>
    [Fact]
    public async Task AttachmentParticipant_WhenEarlierAttachThrows_StillNotifiesLaterParticipantAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new AttachmentParticipantOwner();
        var events = new List<string>();
        var failing = new AttachmentParticipantProbe("failing", events) { ThrowOnAttach = true };
        var later = new AttachmentParticipantProbe("later", events);
        owner.Register(failing);
        owner.Register(later);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.InvokeAsync(
                () => owner.Attach(dispatcher),
                TestContext.Current.CancellationToken));

        later.Attachments.ShouldBe([dispatcher]);
        events.ShouldBe(["failing:attach", "later:attach"]);
        await dispatcher.InvokeAsync(owner.Dispose, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies final disposal is exact-once, ordered, and exhaustive after a failure.</summary>
    [Fact]
    public async Task AttachmentParticipant_WhenDetachedOwnerDisposes_CleansEveryParticipantOnceAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new AttachmentParticipantOwner();
        var events = new List<string>();
        var failing = new AttachmentParticipantProbe("failing", events) { ThrowOnDispose = true };
        var later = new AttachmentParticipantProbe("later", events);
        owner.Register(failing);
        owner.Register(later);
        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            owner.Detach();
        }, TestContext.Current.CancellationToken);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await dispatcher.InvokeAsync(owner.Dispose, TestContext.Current.CancellationToken));
        owner.Dispose();

        failing.DisposeCalls.ShouldBe(1);
        later.DisposeCalls.ShouldBe(1);
        events.ShouldBe([
            "failing:attach",
            "later:attach",
            "failing:detach",
            "later:detach",
            "failing:dispose",
            "later:dispose",
        ]);
    }

    /// <summary>Verifies duplicate identity registration is rejected before attachment.</summary>
    [Fact]
    public void RegisterAttachmentParticipant_WhenIdentityRepeats_Throws()
    {
        var owner = new AttachmentParticipantOwner();
        var participant = new AttachmentParticipantProbe();
        owner.Register(participant);

        _ = Should.Throw<ArgumentException>(() => owner.Register(participant));
    }

    #endregion

    #region Culture identity

    /// <summary>Verifies DateInput commits and renders a customized equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenDateInputReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.DateTimeFormat.ShortDatePattern = "yyyy~MM~dd";
        customized.DateTimeFormat.DateSeparator = "~";
        using var control = new DateInput { Value = new DateOnly(2026, 8, 28), Culture = baseline };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        RenderRow(control, 24).ShouldContain("2026~08~28");
    }

    /// <summary>Verifies TimeInput refreshes separator and designator segments from a customized
    /// equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenTimeInputReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.DateTimeFormat.TimeSeparator = "~";
        customized.DateTimeFormat.PMDesignator = "post";
        using var control = new TimeInput
        {
            Value = new TimeOnly(14, 30),
            Use24HourFormat = false,
            Culture = baseline
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        var row = RenderRow(control, 24);
        row.ShouldContain("02~30");
        row.ShouldContain("post");
    }

    /// <summary>Verifies DateTimeInput refreshes its segments and retained Calendar from a
    /// customized equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenDateTimeInputReceivesCustomizedEqualNamedClone_SynchronizesAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.DateTimeFormat.ShortDatePattern = "yyyy~MM~dd";
        customized.DateTimeFormat.DateSeparator = "~";
        customized.DateTimeFormat.TimeSeparator = "!";
        customized.DateTimeFormat.PMDesignator = "post";
        using var control = new DateTimeInput
        {
            Value = new DateTime(2026, 8, 28, 14, 30, 0),
            Use24HourFormat = false,
            Culture = baseline
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        control.OwnedCalendar.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        var row = RenderRow(control, 36);
        row.ShouldContain("2026~08~28");
        row.ShouldContain("02!30");
        row.ShouldContain("post");
    }

    /// <summary>Verifies NumberInput refreshes grouping and decimal tokens from a customized
    /// equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenNumberInputReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.NumberFormat.NumberGroupSeparator = "_";
        customized.NumberFormat.NumberDecimalSeparator = "~";
        using var control = new NumberInput
        {
            Value = 1234.5m,
            DecimalPlaces = 1,
            Culture = baseline
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        RenderRow(control, 24).ShouldContain("1_234~5");
    }

    /// <summary>Verifies CurrencyInput refreshes its symbol, grouping, and decimal tokens from a
    /// customized equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenCurrencyInputReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.NumberFormat.CurrencySymbol = "USD$";
        customized.NumberFormat.CurrencyGroupSeparator = "_";
        customized.NumberFormat.CurrencyDecimalSeparator = "~";
        using var control = new CurrencyInput
        {
            Value = 1234.5m,
            DecimalPlaces = 1,
            Culture = baseline
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        changes().ShouldBe(1);
        var row = RenderRow(control, 28);
        row.ShouldContain("USD$");
        row.ShouldContain("1_234~5");
    }

    /// <summary>Verifies Calendar refreshes first-day and weekday presentation from a customized
    /// equal-named culture clone.</summary>
    [Fact]
    public void Culture_WhenCalendarReceivesCustomizedEqualNamedClone_CommitsOnceAndRendersClone()
    {
        var baseline = new CultureInfo("en-US");
        var customized = (CultureInfo) baseline.Clone();
        customized.DateTimeFormat.FirstDayOfWeek = DayOfWeek.Monday;
        customized.DateTimeFormat.AbbreviatedDayNames = ["Su", "M1", "T2", "W3", "T4", "F5", "S6"];
        using var control = new UiCalendar
        {
            Culture = baseline,
            DisplayMonth = new DateOnly(2026, 8, 1)
        };
        var changes = CountCultureChanges(control);

        control.Culture = customized;
        control.Culture = customized;

        control.Culture.ShouldBeSameAs(customized);
        control.FirstDayOfWeek.ShouldBe(DayOfWeek.Monday);
        changes().ShouldBe(1);
        RenderRow(control, 32, 10, 2).ShouldBe("┃  M1  T2  W3  T4  F5  S6  Su  ┃");
    }

    private static Func<int> CountCultureChanges(ControlBase control)
    {
        var changes = 0;
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == "Culture")
            {
                changes++;
            }
        };
        return () => changes;
    }

    private static string RenderRow(ControlBase control, int width, int height = 3, int row = 1)
    {
        new LayoutEngine().Layout(control, new Size(width, height));
        using Frame frame = new(new Size(width, height));
        control.Render(frame.Canvas);
        var result = new StringBuilder(width);

        for (var x = 0; x < width; x++)
        {
            var text = FrameOracle.Get(frame, new Point(x, row));
            _ = result.Append(text.Length == 0 ? " " : text);
        }

        return result.ToString();
    }

    #endregion

    #region Lifecycle participants

    /// <summary>Verifies one participant failure cannot skip a later focus cancellation.</summary>
    [Fact]
    public void LifecycleParticipant_WhenEarlierFocusCallbackThrows_NotifiesLaterParticipantInOrder()
    {
        var owner = new LifecycleParticipantOwner();
        var events = new List<string>();
        owner.Register(new LifecycleParticipantProbe("first", events) { ThrowOnFocus = true });
        owner.Register(new LifecycleParticipantProbe("second", events));

        var exception = Should.Throw<InvalidOperationException>(() => owner.CommitFocus(true));

        exception.Message.ShouldBe("first focus failed.");
        events.ShouldBe(["first:focus:True", "second:focus:True"]);
    }

    /// <summary>Verifies duplicate identity registration is rejected.</summary>
    [Fact]
    public void RegisterLifecycleParticipant_WhenIdentityRepeats_ThrowsInvalidOperationException()
    {
        var owner = new LifecycleParticipantOwner();
        var participant = new LifecycleParticipantProbe("participant", []);
        owner.Register(participant);

        _ = Should.Throw<InvalidOperationException>(() => owner.Register(participant));
    }

    /// <summary>Verifies capture loss and disposal preserve registration order and precise reasons.</summary>
    [Fact]
    public void LifecycleParticipant_WhenCaptureEndsThenOwnerDisposes_ReceivesBothTransitionsInOrder()
    {
        var owner = new LifecycleParticipantOwner();
        var events = new List<string>();
        owner.Register(new LifecycleParticipantProbe("first", events));
        owner.Register(new LifecycleParticipantProbe("second", events));

        owner.LoseCapture(PointerCaptureLossReason.Transferred);
        owner.Dispose();

        events.ShouldBe([
            "first:capture:Transferred",
            "second:capture:Transferred",
            "first:unavailable:Disposed",
            "second:unavailable:Disposed",
        ]);
        _ = Should.Throw<ObjectDisposedException>(() =>
            owner.Register(new LifecycleParticipantProbe("late", events)));
    }

    /// <summary>Verifies participant-triggered disposal completes disposal cancellation but does
    /// not resume the superseded focus notification against later participants.</summary>
    [Fact]
    public void LifecycleParticipant_WhenFocusCallbackDisposesOwner_DoesNotResumeStaleFocusFanOut()
    {
        var owner = new LifecycleParticipantOwner();
        var events = new List<string>();
        var first = new LifecycleParticipantProbe("first", events) { FocusAction = owner.Dispose };
        owner.Register(first);
        owner.Register(new LifecycleParticipantProbe("second", events));

        owner.CommitFocus(true);

        owner.IsDisposed.ShouldBeTrue();
        events.ShouldBe([
            "first:focus:True",
            "first:unavailable:Disposed",
            "second:unavailable:Disposed",
        ]);
    }

    /// <summary>Verifies a nested unavailability publication takes its own stable snapshot without
    /// corrupting the outer registration-order walk.</summary>
    [Fact]
    public void LifecycleParticipant_WhenUnavailableReenters_PreservesBothOrderedSnapshots()
    {
        var owner = new LifecycleParticipantOwner();
        var events = new List<string>();
        var first = new LifecycleParticipantProbe("first", events);
        first.UnavailableAction = () =>
        {
            first.UnavailableAction = null;
            owner.BecomeUnavailable(ReleaseReason.Disabled);
        };
        owner.Register(first);
        owner.Register(new LifecycleParticipantProbe("second", events));

        owner.BecomeUnavailable(ReleaseReason.Hidden);

        events.ShouldBe([
            "first:unavailable:Hidden",
            "first:unavailable:Disabled",
            "second:unavailable:Disabled",
            "second:unavailable:Hidden",
        ]);
    }

    #endregion

    #region Render reuse

    /// <summary>Verifies a clean sibling's render extension point is skipped while a dirty
    /// sibling still renders, and both produce correct final cell content.</summary>
    [Fact]
    public async Task Render_WhenSiblingIsDirty_SkipsCleanLeafRenderCallsAsync()
    {
        var dirty = new ProbeControl(new Size(4, 1)) { Content = "AAAA".AsMemory() };
        var clean = new ProbeControl(new Size(4, 1)) { Content = "BBBB".AsMemory() };
        var stack = new Stack { Children = { dirty, clean } };
        var size = new Size(4, 2);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        stack.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        dirty.RenderCalls.ShouldBe(1);
        clean.RenderCalls.ShouldBe(1);

        dirty.Invalidate(Invalidation.Render);
        using var second = new Frame(size);
        var attached = renderer.AttachCommittedFrame(second);

        stack.Render(second.Canvas);

        attached.ShouldBeTrue();
        dirty.RenderCalls.ShouldBe(2);
        clean.RenderCalls.ShouldBe(1);
        Row(second, 0).ShouldBe("AAAA");
        Row(second, 1).ShouldBe("BBBB");
    }

    /// <summary>Verifies every clean leaf still runs its complete paint sequence when no previous
    /// frame is attached, matching what happens after a layout pass (see Application.StartRender,
    /// which never attaches when layout ran since the last render).</summary>
    [Fact]
    public async Task Render_WhenNoPreviousFrameIsAttached_RendersEveryCleanLeafAsync()
    {
        var first = new ProbeControl(new Size(2, 1)) { Content = "AA".AsMemory() };
        var second = new ProbeControl(new Size(2, 1)) { Content = "BB".AsMemory() };
        var stack = new Stack { Children = { first, second } };
        var size = new Size(2, 2);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var firstFrame = new Frame(size);
        stack.Render(firstFrame.Canvas);
        _ = await renderer.RenderAsync(firstFrame, transport, profile, TestContext.Current.CancellationToken);

        using var secondFrame = new Frame(size);
        secondFrame.Canvas.HasPreviousFrame.ShouldBeFalse();
        stack.Render(secondFrame.Canvas);

        first.RenderCalls.ShouldBe(2);
        second.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf casting the default Composite-mode, transparent-
    /// background shadow never takes the copy path. Composite mode's own contract is to preserve
    /// the underlying grapheme and replace only its style (<see cref="TerminalCanvas.ApplyStyle"/> calls
    /// only <c>TrySetOwnerStyle</c>), so a copied Composite shadow cell would always carry forward
    /// whatever character was underneath in the copied frame rather than this frame's - a copy is
    /// never provably identical to a fresh paint for this mode, regardless of background opacity.</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleShadow_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1)) { Shadow = AppearanceTestValues.Shadow(visible: true) };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf casting a Composite-mode shadow never takes the copy
    /// path even when its resolved background is opaque - opacity only changes whether the
    /// background channel blends, not whether the grapheme does, and Composite never replaces the
    /// grapheme regardless.</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleCompositeShadowWithOpaqueBackground_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1))
        {
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.Composite,
                offset: new Point(1, 0),
                background: Color.Rgb(20, 20, 20))
        };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf casting a FractionalBlock shadow never takes the copy
    /// path regardless of its configured background - <c>DrawFractionalShadow</c> hardcodes
    /// <see cref="BackgroundMode.Transparent"/> unconditionally, so this mode always blends with
    /// the destination.</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleFractionalBlockShadow_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1))
        {
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.FractionalBlock,
                offset: new Point(0, 1),
                background: Color.Rgb(20, 20, 20))
        };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf casting a BlockGlyph shadow with an opaque resolved
    /// background does take the copy path - <c>DrawRune</c> with an opaque background replaces
    /// grapheme, style, and background together, so the copied cells are provably identical to a
    /// fresh paint.</summary>
    [Fact]
    public async Task Render_WhenLeafHasVisibleBlockGlyphShadowWithOpaqueBackground_SkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1))
        {
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 0),
                background: Color.Rgb(20, 20, 20))
        };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(1);
    }

    /// <summary>Verifies a BlockGlyph, opaque-background shadow whose footprint overlaps a
    /// changing sibling still produces cell content identical to a fully fresh render - proving
    /// the reuse extension is safe even when the copied region is not exclusively owned by the
    /// reused control, because paint order (not paint source) determines the final cell and the
    /// shadow-casting control's own copied output never changes frame to frame.</summary>
    [Fact]
    public async Task Render_WhenShadowOverlapsChangingSibling_MatchesFullRenderEveryFrameAsync()
    {
        var caster = new ProbeControl(new Size(2, 1))
        {
            Content = "AA".AsMemory(),
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 0),
                background: Color.Rgb(20, 20, 20))
        };
        var sibling = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var overlay = new Overlay { Children = { caster, sibling } };
        Overlay.SetLeft(sibling, Length.Cells(2));
        var size = new Size(3, 1);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        caster.RenderCalls.ShouldBe(1);

        sibling.Content = "Y".AsMemory();
        sibling.Invalidate(Invalidation.Render);

        using var reused = new Frame(size);
        _ = renderer.AttachCommittedFrame(reused);
        overlay.Render(reused.Canvas);

        caster.RenderCalls.ShouldBe(1);
        sibling.RenderCalls.ShouldBe(2);

        // No previous frame is attached, so this independent render of the exact same current
        // state always takes the complete paint path for every leaf - the ground truth the
        // optimized render above must match cell-for-cell, including whichever leaf's paint owns
        // the contested cell where the shadow's footprint and the sibling's bounds overlap.
        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        Row(reused, 0).ShouldBe(Row(reference, 0));
    }

    /// <summary>Verifies the harder ordering of the previous test - the shadow-casting control
    /// paints AFTER the sibling it overlaps, so its (possibly copied) shadow cells are the last
    /// write at the contested position. Still matches a fully fresh render cell-for-cell, because
    /// the copied shadow bytes are provably identical to what caster would freshly paint - they
    /// depend only on caster's own unchanged appearance, never on what the sibling drew
    /// underneath.</summary>
    [Fact]
    public async Task Render_WhenShadowPaintsOverAChangingSibling_MatchesFullRenderEveryFrameAsync()
    {
        var sibling = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var caster = new ProbeControl(new Size(2, 1))
        {
            Content = "AA".AsMemory(),
            Shadow = AppearanceTestValues.Shadow(
                visible: true,
                mode: ShadowMode.BlockGlyph,
                offset: new Point(1, 0),
                background: Color.Rgb(20, 20, 20))
        };
        var overlay = new Overlay { Children = { sibling, caster } };
        Overlay.SetLeft(sibling, Length.Cells(2));
        var size = new Size(3, 1);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        caster.RenderCalls.ShouldBe(1);

        sibling.Content = "Y".AsMemory();
        sibling.Invalidate(Invalidation.Render);

        using var reused = new Frame(size);
        _ = renderer.AttachCommittedFrame(reused);
        overlay.Render(reused.Canvas);

        caster.RenderCalls.ShouldBe(1);
        sibling.RenderCalls.ShouldBe(2);

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        Row(reused, 0).ShouldBe(Row(reference, 0));
    }

    /// <summary>Verifies a render-clean leaf that owns a control of its own (a context menu, even
    /// while closed) never takes the copy path - <c>OwnedControlCount</c> covers both the normal
    /// and popup layers, so this is the leaf's own conservative "popup-free" requirement.</summary>
    [Fact]
    public async Task Render_WhenLeafOwnsAControl_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1)) { ContextMenu = new ContextMenu() };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean leaf whose resolved background is transparent never takes
    /// the copy path. A transparent underlay never authors its own uncovered cells - they hold
    /// whatever the parent painted underneath, which the copy path would resurrect as stale content
    /// from the frame it copies rather than the parent's current-frame paint.</summary>
    [Fact]
    public async Task Render_WhenLeafBackgroundIsTransparent_NeverSkipsRenderAsync()
    {
        var control = new ProbeControl(new Size(2, 1)) { Content = "AA".AsMemory() };
        var defaultFace = control.Face;
        control.Face = new Face(
            defaultFace.Foreground,
            Color.Transparent,
            defaultFace.Attributes,
            defaultFace.Underline,
            defaultFace.UnderlineColor);
        var size = new Size(4, 3);
        new LayoutEngine().Layout(control, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        control.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        control.RenderCalls.ShouldBe(1);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        control.Render(second.Canvas);

        control.RenderCalls.ShouldBe(2);
    }

    /// <summary>Verifies a render-clean Image with an assigned source still records its semantic
    /// placement through the copy path: copying cells alone cannot replay
    /// <see cref="TerminalCanvas.DrawImage"/>, so <c>Image.OnReuseCleanRender</c> re-asserts an
    /// identical placement instead of silently dropping it.</summary>
    [Fact]
    public async Task Render_WhenImageIsRenderClean_PreservesPlacementThroughTheCopyPathAsync()
    {
        var image = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), new byte[4]),
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var size = new Size(1, 1);
        new LayoutEngine().Layout(image, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        image.Render(first.Canvas);
        first.PlacementCount.ShouldBe(1);
        var firstPlacement = first.GetPlacement(0);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        second.Canvas.HasPreviousFrame.ShouldBeTrue();
        image.Render(second.Canvas);

        second.PlacementCount.ShouldBe(1);
        var secondPlacement = second.GetPlacement(0);
        secondPlacement.Image.ShouldBeSameAs(image.Source);
        secondPlacement.Source.ShouldBe(firstPlacement.Source);
        secondPlacement.Destination.ShouldBe(firstPlacement.Destination);
        secondPlacement.Mode.ShouldBe(firstPlacement.Mode);
    }

    /// <summary>
    /// Verifies a render-clean Image actually takes the copy path rather than merely producing
    /// output consistent with either path. <see cref="Image"/> is sealed, so it cannot carry a
    /// <see cref="ProbeControl.RenderCalls"/>-style counter the way every other exclusion
    /// dimension in this file is proven; instead, this poisons the committed frame's fallback-cell
    /// content directly (bypassing <see cref="Image"/> entirely) after it paints and before the
    /// frame commits, then renders again with no further mutation. A full fresh paint would
    /// overwrite the poison with the recomputed fallback glyph; only <c>CopyFromPrevious</c>
    /// reproduces it verbatim.
    /// </summary>
    [Fact]
    public async Task Render_WhenImageIsRenderClean_ReallyTakesTheCopyPathAsync()
    {
        var image = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), new byte[4]),
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var size = new Size(1, 1);
        new LayoutEngine().Layout(image, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        image.Render(first.Canvas);
        var freshFallbackGlyph = Row(first, 0);
        freshFallbackGlyph.ShouldNotBe("Z", "the poison marker must differ from a genuine fresh paint");
        _ = first.Canvas.Draw("Z", new Point(0, 0));
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        image.Render(second.Canvas);

        Row(second, 0).ShouldBe("Z");
    }

    /// <summary>Verifies a Source change invalidates render and produces a fresh, updated
    /// placement instead of one carried forward through reuse - a regression guard that removing
    /// Image's unconditional full-render requirement did not weaken ordinary invalidation.</summary>
    [Fact]
    public async Task Render_WhenImageSourceChangesBetweenFrames_RecordsTheNewPlacementAsync()
    {
        var firstSource = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 4]);
        var secondSource = GraphicsImage.FromRgba(new Size(1, 1), [5, 6, 7, 8]);
        var image = new Image
        {
            Source = firstSource,
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var size = new Size(1, 1);
        new LayoutEngine().Layout(image, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        image.Render(first.Canvas);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        image.Source = secondSource;
        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);
        image.Render(second.Canvas);

        second.PlacementCount.ShouldBe(1);
        second.GetPlacement(0).Image.ShouldBeSameAs(secondSource);
    }

    /// <summary>Verifies a fixed sequence of frames mixing an image-bearing leaf with dirty and
    /// clean plain siblings always produces cell content AND placement snapshots identical to a
    /// fully fresh render of the same state, proving the newly enabled image copy path never
    /// diverges from the full paint path.</summary>
    [Fact]
    public async Task Render_WhenImageLeafIsMixedWithChangingSiblings_MatchesFullRenderEveryFrameAsync()
    {
        var image = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), new byte[4]),
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var before = new ProbeControl(new Size(4, 1)) { Content = "before".AsMemory() };
        var after = new ProbeControl(new Size(4, 1)) { Content = "after-".AsMemory() };
        var stack = new Stack { Children = { before, image, after } };
        var size = new Size(4, 3);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        stack.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);

        bool[][] dirtySequence =
        [
            [false, false], // neither sibling dirty: image is the only render-clean leaf either way
            [true, false],
            [false, true],
            [true, true]
        ];

        foreach (var dirty in dirtySequence)
        {
            if (dirty[0])
            {
                before.Invalidate(Invalidation.Render);
            }

            if (dirty[1])
            {
                after.Invalidate(Invalidation.Render);
            }

            using var reused = new Frame(size);
            _ = renderer.AttachCommittedFrame(reused);
            stack.Render(reused.Canvas);

            using var fresh = new Frame(size);
            stack.Render(fresh.Canvas);

            reused.PlacementCount.ShouldBe(fresh.PlacementCount);

            for (var index = 0; index < fresh.PlacementCount; index++)
            {
                reused.GetPlacement(index).ShouldBe(fresh.GetPlacement(index));
            }

            for (var row = 0; row < size.Height; row++)
            {
                Row(reused, row).ShouldBe(Row(fresh, row));
            }

            _ = await renderer.RenderAsync(reused, transport, profile, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies the render-clean reuse extension point itself is invoked exactly when the
    /// copy path is taken and never on an ordinary full render, proving the general wiring added
    /// for image reuse is correct independent of which concrete control exercises it.</summary>
    [Fact]
    public async Task Render_WhenLeafIsReused_InvokesReuseHookInsteadOfFullRenderAsync()
    {
        var dirty = new ProbeControl(new Size(4, 1)) { Content = "AAAA".AsMemory() };
        var clean = new ProbeControl(new Size(4, 1)) { Content = "BBBB".AsMemory() };
        var stack = new Stack { Children = { dirty, clean } };
        var size = new Size(4, 2);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(size);
        stack.Render(first.Canvas);
        dirty.RenderCalls.ShouldBe(1);
        dirty.ReuseCleanRenderCalls.ShouldBe(0);
        clean.RenderCalls.ShouldBe(1);
        clean.ReuseCleanRenderCalls.ShouldBe(0);
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);

        dirty.Invalidate(Invalidation.Render);
        using var second = new Frame(size);
        _ = renderer.AttachCommittedFrame(second);

        stack.Render(second.Canvas);

        dirty.RenderCalls.ShouldBe(2);
        dirty.ReuseCleanRenderCalls.ShouldBe(0);
        clean.RenderCalls.ShouldBe(1);
        clean.ReuseCleanRenderCalls.ShouldBe(1);
    }

    /// <summary>Verifies a fixed sequence of frames mixing clean and dirty leaves - none dirty,
    /// all dirty, and various subsets - always produces cell content identical to a fully fresh
    /// render of the same state, proving the copy path never diverges from the full paint path.</summary>
    [Fact]
    public async Task Render_WhenDirtyLeavesVaryAcrossFrames_MatchesFullRenderEveryFrameAsync()
    {
        var leaves = Enumerable.Range(0, 5)
            .Select(index => new ProbeControl(new Size(4, 1)) { Content = $"L{index}--".AsMemory() })
            .ToArray();
        var stack = new Stack { Children = { leaves[0], leaves[1], leaves[2], leaves[3], leaves[4] } };
        var size = new Size(4, 5);
        new LayoutEngine().Layout(stack, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        stack.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);

        int[][] dirtyIndexSets =
        [
            [],
            [0, 1, 2, 3, 4],
            [2],
            [0, 4],
            [],
            [1, 2, 3],
            []
        ];

        foreach (var dirtyIndices in dirtyIndexSets)
        {
            foreach (var index in dirtyIndices)
            {
                leaves[index].Content = $"U{index}--".AsMemory();
                leaves[index].Invalidate(Invalidation.Render);
            }

            using var reused = new Frame(size);
            _ = renderer.AttachCommittedFrame(reused);
            stack.Render(reused.Canvas);

            // No previous frame is attached, so this independent render of the exact same
            // current state always takes the complete paint path for every leaf - the ground
            // truth the optimized render above must match cell-for-cell.
            using var reference = new Frame(size);
            stack.Render(reference.Canvas);

            for (var row = 0; row < size.Height; row++)
            {
                Row(reused, row).ShouldBe(Row(reference, row));
            }

            _ = await renderer.RenderAsync(reused, transport, profile, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Verifies three render-clean rows sitting underneath a stationary, unchanged open
    /// popup still match a fully fresh render cell-for-cell after taking the copy path - the
    /// popup layer repaints unconditionally on every frame (<c>RenderOwnedPopupDescendants</c>
    /// runs from Root every call, never gated by any clean check), so whichever byte a contested
    /// cell ends up with is always the popup's current-frame paint, regardless of whether the row
    /// underneath copied or freshly painted its own now-overwritten contribution.</summary>
    [Fact]
    public async Task Render_WhenStationaryOpenPopupOverlapsCleanRows_MatchesFullRenderEveryFrameAsync()
    {
        var (overlay, rowA, rowB, rowC, _) = BuildOverlappedFixture(popupOpen: true);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        // Nothing changed - the popup stays open at the same origin and every row is still clean -
        // so the copy path is available underneath the popup's frame and body.
        using var reused = new Frame(size);
        _ = renderer.AttachCommittedFrame(reused);
        overlay.Render(reused.Canvas);

        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        // No previous frame is attached, so this independent render of the exact same current
        // state always takes the complete paint path for every leaf - the ground truth the
        // optimized render above must match cell-for-cell, including every cell the popup's frame
        // and body contest against the rows underneath.
        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(reused, row).ShouldBe(Row(reference, row));
        }
    }

    /// <summary>Verifies a popup with a transparent resolved background still produces a
    /// cell-for-cell match with a fully fresh render while overlapping stationary, reused clean
    /// rows - the popup's own opacity only changes what pixels the blend produces, never whether
    /// the row underneath's reused output was safe to reuse in the first place, because that row's
    /// own paint is unaffected by the popup's existence either way.</summary>
    [Fact]
    public async Task Render_WhenTransparentPopupOverlapsCleanRows_MatchesFullRenderEveryFrameAsync()
    {
        var (overlay, rowA, rowB, rowC, popup) = BuildOverlappedFixture(popupOpen: true);
        var defaultFace = popup.Face;
        popup.Face = new Face(
            defaultFace.Foreground,
            Color.Transparent,
            defaultFace.Attributes,
            defaultFace.Underline,
            defaultFace.UnderlineColor);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        using var reused = new Frame(size);
        _ = renderer.AttachCommittedFrame(reused);
        overlay.Render(reused.Canvas);

        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(reused, row).ShouldBe(Row(reference, row));
        }
    }

    /// <summary>Verifies closing a popup that previously overlapped three render-clean rows leaves
    /// no stale popup pixels behind - closing always routes through
    /// <c>InvalidationImpact.Measure</c> (<c>Popup.SetOpen</c>'s every <c>_isOpen</c> transition
    /// pairs with a Measure notification), so production never attaches a previous frame for this
    /// render; this test matches that same no-previous-frame condition rather than the shortcut of
    /// asserting against <c>CanReuseCleanRender</c> directly.</summary>
    [Fact]
    public async Task Render_WhenPopupClosesBetweenFrames_LeavesNoStalePopupPixelsAsync()
    {
        var (overlay, rowA, rowB, rowC, popup) = BuildOverlappedFixture(popupOpen: true);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        popup.IsOpen = false;
        new LayoutEngine().Layout(overlay, size);

        // Matches production: closing invalidated Measure, so Application never attaches a
        // previous frame for this render - no copy path is even considered anywhere this frame.
        using var afterClose = new Frame(size);
        overlay.Render(afterClose.Canvas);

        Row(afterClose, 0).ShouldBe("AAAAAAAAAA");
        Row(afterClose, 1).ShouldBe("BBBBBBBBBB");
        Row(afterClose, 2).ShouldBe("CCCCCCCCCC");

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(afterClose, row).ShouldBe(Row(reference, row));
        }
    }

    /// <summary>Verifies opening a popup over three previously popup-free, render-clean rows
    /// produces the identical result a fully fresh render would - the mirror of the closing case,
    /// covering the other direction of the same footprint-change invariant.</summary>
    [Fact]
    public async Task Render_WhenPopupOpensBetweenFrames_MatchesFullRenderImmediatelyAsync()
    {
        var (overlay, rowA, rowB, rowC, popup) = BuildOverlappedFixture(popupOpen: false);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);
        Row(warm, 0).ShouldBe("AAAAAAAAAA");

        popup.IsOpen = true;
        new LayoutEngine().Layout(overlay, size);

        // Matches production: opening invalidated Measure, so no previous frame is attached here
        // either.
        using var afterOpen = new Frame(size);
        overlay.Render(afterOpen.Canvas);

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(afterOpen, row).ShouldBe(Row(reference, row));
        }

        // The popup's frame now visibly contests the rows it overlaps - sanity-checks the fixture
        // actually exercises overlap rather than two coincidentally identical blank renders.
        Row(afterOpen, 0).ShouldNotBe("AAAAAAAAAA");
    }

    /// <summary>Verifies moving an open popup between frames - without closing it - leaves no
    /// pixels from its old footprint behind and matches a fully fresh render at its new one,
    /// covering the third footprint-change case (open/close/move) alongside the two above.</summary>
    [Fact]
    public async Task Render_WhenOpenPopupMovesBetweenFrames_MatchesFullRenderAtNewPositionAsync()
    {
        var (overlay, rowA, rowB, rowC, popup) = BuildOverlappedFixture(popupOpen: true);
        var size = new Size(10, 3);
        new LayoutEngine().Layout(overlay, size);
        using var renderer = new Renderer();
        var transport = new ConsoleApplicationTransport();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var warm = new Frame(size);
        overlay.Render(warm.Canvas);
        _ = await renderer.RenderAsync(warm, transport, profile, TestContext.Current.CancellationToken);
        rowA.RenderCalls.ShouldBe(1);
        rowB.RenderCalls.ShouldBe(1);
        rowC.RenderCalls.ShouldBe(1);

        popup.FixedOrigin = new Point(0, 0);

        // FixedOrigin itself carries no change notification (Popup.cs), so force the Arrange
        // invalidation a real reposition API (Anchor, Placement) would trigger on its own -
        // FixedOrigin's branch in ArrangeOverride ignores Anchor's bounds regardless, so this only
        // supplies the invalidation, not the new position.
        popup.Anchor = new ProbeControl();
        new LayoutEngine().Layout(overlay, size);

        // Matches production: repositioning a popup only ever happens inside an Arrange pass, so
        // no previous frame is attached here either.
        using var afterMove = new Frame(size);
        overlay.Render(afterMove.Canvas);

        using var reference = new Frame(size);
        overlay.Render(reference.Canvas);

        for (var row = 0; row < size.Height; row++)
        {
            Row(afterMove, row).ShouldBe(Row(reference, row));
        }

        // Column 6 sat under the popup's old right border (origin (3, 0), width 4) and is outside
        // its new footprint (origin (0, 0), width 4) - it must show the row's own content again,
        // not a leftover border glyph from the previous origin.
        Row(afterMove, 0)[6].ShouldBe('A');
    }

    private static (Overlay Overlay, ProbeControl RowA, ProbeControl RowB, ProbeControl RowC, Popup Popup)
        BuildOverlappedFixture(bool popupOpen)
    {
        var rowA = new ProbeControl(new Size(10, 1)) { Content = "AAAAAAAAAA".AsMemory() };
        var rowB = new ProbeControl(new Size(10, 1)) { Content = "BBBBBBBBBB".AsMemory() };
        var rowC = new ProbeControl(new Size(10, 1)) { Content = "CCCCCCCCCC".AsMemory() };
        Overlay.SetTop(rowB, Length.Cells(1));
        Overlay.SetTop(rowC, Length.Cells(2));
        var popupChild = new ProbeControl(new Size(2, 1)) { Content = "PP".AsMemory() };
        var popup = new Popup { Content = popupChild, IsOpen = popupOpen, FixedOrigin = new Point(3, 0) };
        var overlay = new Overlay { Children = { rowA, rowB, rowC, popup } };
        return (overlay, rowA, rowB, rowC, popup);
    }

    private static string Row(Frame frame, int row)
    {
        var value = new StringBuilder();

        for (var column = 0; column < frame.Size.Width; column++)
        {
            _ = value.Append(FrameOracle.Get(frame, new Point(column, row)));
        }

        return value.ToString();
    }

    /// <summary>Verifies EnabledChanged fires when IsEnabled changes.</summary>
    [Fact]
    public void EnabledChanged_WhenDisabled_Fires()
    {
        // Arrange
        var fired = 0;
        var control = new ProbeControl();
        control.EnabledChanged += (_, _) => fired++;

        // Act
        control.IsEnabled = false;

        // Assert
        fired.ShouldBe(1);
    }

    /// <summary>Verifies VisibilityChanged fires when Visibility changes.</summary>
    [Fact]
    public void VisibilityChanged_WhenCollapsed_Fires()
    {
        // Arrange
        var fired = 0;
        var control = new ProbeControl();
        control.VisibilityChanged += (_, _) => fired++;

        // Act
        control.Visibility = Visibility.Collapsed;

        // Assert
        fired.ShouldBe(1);
    }

    /// <summary>Verifies restoring visibility from the unavailability seam suppresses the stale
    /// outer publications after the nested restoration has already published its own state.</summary>
    [Fact]
    public void Visibility_WhenUnavailableCallbackRestoresVisible_SuppressesOuterNotifications()
    {
        var control = new OwnershipObserverControl
        {
            BecomingUnavailable = static (current, reason) =>
            {
                if (reason == ReleaseReason.Hidden)
                {
                    current.Visibility = Visibility.Visible;
                }
            }
        };
        var propertyValues = new List<Visibility>();
        var changedValues = new List<Visibility>();
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.Visibility))
            {
                propertyValues.Add(control.Visibility);
            }
        };
        control.VisibilityChanged += (_, _) => changedValues.Add(control.Visibility);

        control.Visibility = Visibility.Hidden;

        control.Visibility.ShouldBe(Visibility.Visible);
        propertyValues.ShouldBe([Visibility.Visible]);
        changedValues.ShouldBe([Visibility.Visible]);
    }

    /// <summary>Verifies restoring enabled state from the unavailability seam suppresses the stale
    /// outer publications after the nested restoration has already published its own state.</summary>
    [Fact]
    public void IsEnabled_WhenUnavailableCallbackRestoresEnabled_SuppressesOuterNotifications()
    {
        var control = new OwnershipObserverControl
        {
            BecomingUnavailable = static (current, reason) =>
            {
                if (reason == ReleaseReason.Disabled)
                {
                    current.IsEnabled = true;
                }
            }
        };
        var propertyValues = new List<bool>();
        var changedValues = new List<bool>();
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.IsEnabled))
            {
                propertyValues.Add(control.IsEnabled);
            }
        };
        control.EnabledChanged += (_, _) => changedValues.Add(control.IsEnabled);

        control.IsEnabled = false;

        control.IsEnabled.ShouldBeTrue();
        propertyValues.ShouldBe([true]);
        changedValues.ShouldBe([true]);
    }

    /// <summary>Verifies ParentChanged fires when added to a container.</summary>
    [Fact]
    public void ParentChanged_WhenAddedToContainer_Fires()
    {
        // Arrange
        var fired = 0;
        var child = new ProbeControl();
        child.ParentChanged += (_, _) => fired++;

        // Act
        _ = new Stack { Children = { child } };

        // Assert
        fired.ShouldBe(1);
    }

    #endregion

    #region Text selection

    /// <summary>Verifies every control starts without active text-selection behavior.</summary>
    [Fact]
    public void Constructor_WhenCreated_DisablesTextSelection()
    {
        var control = new Stack { Children = { new ControlText("text") } };

        control.IsTextSelectionEnabled.ShouldBeFalse();
        control.TextSelection.ShouldBe(default);
        control.SelectedText.ShouldBeEmpty();
        _ = Should.Throw<InvalidOperationException>(
            () => control.SetTextSelection(new Selection(0, 1)));
    }

    /// <summary>Verifies one enabled aggregate validates and publishes a directional range.</summary>
    [Fact]
    public void SetTextSelection_WhenEnabled_PublishesOneDirectionalChange()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("A e\u0301") }
        };
        TextSelectionChangedEventArgs? observed = null;
        control.TextSelectionChanged += (_, eventArgs) => observed = eventArgs;

        control.SetTextSelection(new Selection(4, 2));

        control.TextSelection.ShouldBe(new Selection(4, 2));
        control.SelectedText.ShouldBe("e\u0301");
        control.CopySelectedText().ShouldBe("e\u0301");
        observed.ShouldNotBeNull().PreviousSelection.ShouldBe(default);
        observed.Selection.ShouldBe(new Selection(4, 2));
    }

    /// <summary>Verifies reentry from an earlier common-event subscriber prevents later subscribers
    /// from receiving the obsolete outer transition.</summary>
    [Fact]
    public void TextSelectionChanged_WhenSubscriberReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("abcd") }
        };
        var observed = new List<(Selection EventSelection, Selection LiveSelection)>();
        control.TextSelectionChanged += (_, eventArgs) =>
        {
            if (eventArgs.Selection == new Selection(0, 1))
            {
                control.SetTextSelection(new Selection(0, 2));
            }
        };
        control.TextSelectionChanged += (_, eventArgs) =>
            observed.Add((eventArgs.Selection, control.TextSelection));

        // Act
        control.SetTextSelection(new Selection(0, 1));

        // Assert
        observed.ShouldBe([(new Selection(0, 2), new Selection(0, 2))]);
    }

    /// <summary>Verifies invalid grapheme endpoints are rejected without observable mutation.</summary>
    [Fact]
    public void SetTextSelection_WhenEndpointSplitsGrapheme_ThrowsBeforeMutation()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("e\u0301") }
        };
        var raised = 0;
        control.TextSelectionChanged += (_, _) => raised++;

        _ = Should.Throw<ArgumentException>(
            () => control.SetTextSelection(new Selection(0, 1)));

        control.TextSelection.ShouldBe(default);
        raised.ShouldBe(0);
    }

    /// <summary>Verifies disabling commits capability state, cancels the range, and publishes once.</summary>
    [Fact]
    public void IsTextSelectionEnabled_WhenDisabled_ClearsSelectionOnce()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("text") }
        };
        control.SetTextSelection(new Selection(0, 4));
        var raised = 0;
        control.TextSelectionChanged += (_, _) => raised++;

        control.IsTextSelectionEnabled = false;

        control.IsTextSelectionEnabled.ShouldBeFalse();
        control.TextSelection.ShouldBe(default);
        control.SelectedText.ShouldBeEmpty();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies a newer reentrant enable transition retains its gesture and selection
    /// instead of letting obsolete outer disable cleanup erase them.</summary>
    [Fact]
    public void IsTextSelectionEnabled_WhenDisableObserverReenables_PreservesNewestState()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("text") }
        };
        control.SetTextSelection(new Selection(0, 4));
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.IsTextSelectionEnabled) &&
                !control.IsTextSelectionEnabled)
            {
                control.IsTextSelectionEnabled = true;
            }
        };

        control.IsTextSelectionEnabled = false;

        control.IsTextSelectionEnabled.ShouldBeTrue();
        control.TextSelection.ShouldBe(new Selection(0, 4));
        control.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
    }

    /// <summary>Verifies a newer reentrant disable transition owns final cleanup when an enable
    /// notification is reversed.</summary>
    [Fact]
    public void IsTextSelectionEnabled_WhenEnableObserverDisables_PreservesNewestState()
    {
        var control = new Stack { Children = { new ControlText("text") } };
        control.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ControlBase.IsTextSelectionEnabled) &&
                control.IsTextSelectionEnabled)
            {
                control.IsTextSelectionEnabled = false;
            }
        };

        control.IsTextSelectionEnabled = true;

        control.IsTextSelectionEnabled.ShouldBeFalse();
        control.TextSelection.ShouldBe(default);
        control.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
    }

    /// <summary>Verifies replacing a semantic source clears a range even when its text is identical.</summary>
    [Fact]
    public void TextSelection_WhenSourceIdentityChanges_ClearsStaleRangeOnce()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("same") }
        };
        control.SetTextSelection(new Selection(0, 4));
        var raised = 0;
        control.TextSelectionChanged += (_, _) => raised++;

        control.Children.Clear();
        control.Children.Add(new ControlText("same"));
        var selection = control.TextSelection;

        selection.ShouldBe(default);
        control.SelectedText.ShouldBeEmpty();
        raised.ShouldBe(1);
    }

    /// <summary>Verifies a nonzero collapsed caret is stale when its source identity changes.</summary>
    [Fact]
    public void TextSelection_WhenCollapsedCaretSourceChanges_ClearsToDefault()
    {
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("same") }
        };
        control.SetTextSelection(new Selection(4, 4));

        control.Children.Clear();
        control.Children.Add(new ControlText("same"));

        control.TextSelection.ShouldBe(default);
    }

    /// <summary>Verifies the lazy reconciliation path that clears a stale selection cannot publish
    /// its obsolete transition after a subscriber reentrantly commits a newer selection through the
    /// same shared event.</summary>
    [Fact]
    public void TextSelectionChanged_WhenReconcileSubscriberReenters_PublishesOnlyCurrentTransition()
    {
        // Arrange
        var control = new Stack
        {
            IsTextSelectionEnabled = true,
            Children = { new ControlText("same") }
        };
        control.SetTextSelection(new Selection(0, 4));
        control.Children.Clear();
        control.Children.Add(new ControlText("same"));
        var observed = new List<Selection>();
        control.TextSelectionChanged += (_, eventArgs) =>
        {
            if (eventArgs.Selection == default)
            {
                control.SetTextSelection(new Selection(0, 2));
            }
        };
        control.TextSelectionChanged += (_, eventArgs) => observed.Add(eventArgs.Selection);

        // Act
        var selection = control.TextSelection;

        // Assert
        selection.ShouldBe(new Selection(0, 2));
        observed.ShouldBe([new Selection(0, 2)]);
    }

    #endregion
}
