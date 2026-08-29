// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the public single-content authoring role.</summary>
public sealed class ContentControlTests
{
    /// <summary>Verifies assignment, replacement, equivalence, and clear publish exactly one committed change.</summary>
    [Fact]
    public void Content_WhenAssignedReplacedAndCleared_PublishesExactlyOncePerChange()
    {
        var owner = new ProbeContentControl();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var notifications = new List<string?>();
        owner.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        owner.Content = first;
        owner.Content = first;
        owner.Content = second;
        owner.Content = null;
        owner.Content = null;

        notifications.ShouldBe([
            nameof(ContentControl.Content),
            nameof(ContentControl.Content),
            nameof(ContentControl.Content)
        ]);
        owner.ContentChanges.Count.ShouldBe(3);
        owner.ContentChanges[0].ShouldBe((null, first));
        owner.ContentChanges[1].ShouldBe((first, second));
        owner.ContentChanges[2].ShouldBe((second, null));
        first.Parent.ShouldBeNull();
        first.IsDisposed.ShouldBeFalse();
        second.Parent.ShouldBeNull();
        second.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies content precedes private parts because the base slot registers first.</summary>
    [Fact]
    public void Content_WhenDerivedControlRegistersPart_PrecedesPartInOwnedOrder()
    {
        var owner = new ProbeContentControl();
        var part = new ProbeControl();
        var content = new ProbeControl();
        owner.AddPart(part);

        owner.Content = content;

        owner.GetOwnedOrder().ShouldBe([content, part]);
    }

    /// <summary>Verifies every invalid candidate preserves the complete existing edge.</summary>
    [Fact]
    public void Content_WhenReplacementIsInvalid_PreservesExistingContent()
    {
        var owner = new ProbeContentControl();
        var existing = new ProbeControl();
        var part = new ProbeControl();
        var other = new ProbeContentControl();
        var crossOwned = new ProbeControl();
        var disposed = new ProbeControl();
        owner.AddPart(part);
        owner.Content = existing;
        other.Content = crossOwned;
        disposed.Dispose();
        owner.ContentChanges.Clear();
        var notifications = 0;
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                notifications++;
            }
        };

        _ = Should.Throw<ArgumentException>(() => owner.Content = owner);
        _ = Should.Throw<ArgumentException>(() => owner.Content = part);
        _ = Should.Throw<ArgumentException>(() => owner.Content = crossOwned);
        _ = Should.Throw<ObjectDisposedException>(() => owner.Content = disposed);

        owner.Content.ShouldBeSameAs(existing);
        existing.Parent.ShouldBeSameAs(owner);
        owner.ContentChanges.ShouldBeEmpty();
        notifications.ShouldBe(0);
        crossOwned.Parent.ShouldBeSameAs(other);
    }

    /// <summary>Verifies rejected attached replacement preserves ownership, context, focus, and capture.</summary>
    [Fact]
    public async Task Content_WhenAttachedReplacementIsCrossOwned_PreservesCompleteOldStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeContentControl();
        var existing = new ProbeControl { IsFocusable = true };
        var other = new ProbeContentControl();
        var invalid = new ProbeControl();
        owner.Content = existing;
        other.Content = invalid;

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);
            using PointerManager capture = new(owner);
            focus.Focus(existing).ShouldBeTrue();
            capture.Capture(existing).ShouldBeTrue();

            _ = Should.Throw<ArgumentException>(() => owner.Content = invalid);

            owner.Content.ShouldBeSameAs(existing);
            existing.Parent.ShouldBeSameAs(owner);
            existing.Dispatcher.ShouldBeSameAs(dispatcher);
            focus.Focused.ShouldBeSameAs(existing);
            capture.Captured.ShouldBeSameAs(existing);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies assigning content to an attached owner publishes complete inherited context.</summary>
    [Fact]
    public async Task Content_WhenAssignedToAttachedOwner_AttachesCommittedSubtreeAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeContentControl();
        var content = new ProbeControl();

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);

            owner.Content = content;

            owner.Content.ShouldBeSameAs(content);
            content.Parent.ShouldBeSameAs(owner);
            content.Dispatcher.ShouldBeSameAs(dispatcher);
            content.AttachedCalls.ShouldBe(1);
            content.AttachedStateWasCommitted.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an independently attached candidate is rejected without replacing old content.</summary>
    [Fact]
    public async Task Content_WhenCandidateIsIndependentlyAttached_PreservesExistingContentAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeContentControl();
        var existing = new ProbeControl();
        var attached = new ProbeControl();
        owner.Content = existing;
        await dispatcher.InvokeAsync(
            () => attached.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<ArgumentException>(() => owner.Content = attached);

        owner.Content.ShouldBeSameAs(existing);
        existing.Parent.ShouldBeSameAs(owner);
        attached.Parent.ShouldBeNull();
        attached.Dispatcher.ShouldBeSameAs(dispatcher);
    }

    /// <summary>Verifies attached equivalent, replacement, and clear all check dispatcher access first.</summary>
    [Fact]
    public async Task Content_WhenAttachedMutationRunsOffDispatcher_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeContentControl();
        var existing = new ProbeControl();
        owner.Content = existing;
        await dispatcher.InvokeAsync(
            () => owner.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => owner.Content = existing);
        _ = Should.Throw<InvalidOperationException>(() => owner.Content = new ProbeControl());
        _ = Should.Throw<InvalidOperationException>(() => owner.Content = null);

        owner.Content.ShouldBeSameAs(existing);
        existing.Parent.ShouldBeSameAs(owner);
        existing.Dispatcher.ShouldBeSameAs(dispatcher);
    }

    /// <summary>Verifies the hook observes committed structure and its failure cannot suppress notification.</summary>
    [Fact]
    public void Content_WhenChangeHookThrows_CommitsNotifiesAndRethrowsHookFailure()
    {
        var owner = new ProbeContentControl();
        var previous = new ProbeControl();
        var current = new ProbeControl();
        owner.Content = previous;
        owner.ContentChanges.Clear();
        var notifications = 0;
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                notifications++;
            }
        };
        owner.ContentChanging = (control, oldContent, newContent) =>
        {
            oldContent.ShouldBeSameAs(previous);
            newContent.ShouldBeSameAs(current);
            control.Content.ShouldBeSameAs(current);
            previous.Parent.ShouldBeNull();
            current.Parent.ShouldBeSameAs(control);
        };
        owner.ThrowOnContentChanged = true;

        var exception = Should.Throw<InvalidOperationException>(() => owner.Content = current);

        exception.Message.ShouldBe("The content callback failed.");
        notifications.ShouldBe(1);
        owner.Content.ShouldBeSameAs(current);
        owner.ContentChanges.ShouldBe([(previous, current)]);
    }

    /// <summary>Verifies a hook failure remains authoritative after a throwing property subscriber observes the commit.</summary>
    [Fact]
    public void Content_WhenHookAndPropertySubscriberThrow_PreservesHookFailureAfterSubscriberRuns()
    {
        var owner = new ProbeContentControl();
        var previous = new ProbeControl();
        var current = new ProbeControl();
        owner.Content = previous;
        owner.ThrowOnContentChanged = true;
        var subscriberRan = false;
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(ContentControl.Content))
            {
                return;
            }

            subscriberRan = true;
            owner.Content.ShouldBeSameAs(current);
            previous.Parent.ShouldBeNull();
            current.Parent.ShouldBeSameAs(owner);
            throw new InvalidOperationException("The property subscriber failed.");
        };

        var exception = Should.Throw<InvalidOperationException>(() => owner.Content = current);

        exception.Message.ShouldBe("The content callback failed.");
        subscriberRan.ShouldBeTrue();
        owner.Content.ShouldBeSameAs(current);
    }

    /// <summary>Verifies a failed hook does not corrupt the previous value supplied to a later change.</summary>
    [Fact]
    public void Content_WhenChangeAfterHookFailure_SuppliesPriorCommittedContentAsPrevious()
    {
        var owner = new ProbeContentControl();
        var first = new ProbeControl();
        var committedDespiteFailure = new ProbeControl();
        var final = new ProbeControl();
        owner.Content = first;
        owner.ContentChanges.Clear();
        owner.ThrowOnContentChanged = true;
        _ = Should.Throw<InvalidOperationException>(() => owner.Content = committedDespiteFailure);

        owner.ThrowOnContentChanged = false;
        owner.Content = final;

        owner.ContentChanges.ShouldBe([
            (first, committedDespiteFailure),
            (committedDespiteFailure, final)
        ]);
        owner.Content.ShouldBeSameAs(final);
    }

    /// <summary>Verifies an earlier structural callback remains the transaction's first failure.</summary>
    [Fact]
    public void Content_WhenEarlierStructuralCallbackThrows_StillPublishesContentAndPreservesFirstFailure()
    {
        var owner = new ProbeContentControl();
        var previous = new OwnershipObserverControl();
        var current = new ProbeControl();
        owner.Content = previous;
        owner.ContentChanges.Clear();
        owner.ThrowOnContentChanged = true;
        previous.BecomingUnavailable = (_, _) =>
            throw new InvalidOperationException("The earlier structural callback failed.");
        var notifications = 0;
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                notifications++;
            }
        };

        var exception = Should.Throw<InvalidOperationException>(() => owner.Content = current);

        exception.Message.ShouldBe("The earlier structural callback failed.");
        owner.Content.ShouldBeSameAs(current);
        previous.Parent.ShouldBeNull();
        current.Parent.ShouldBeSameAs(owner);
        owner.ContentChanges.ShouldBe([(previous, current)]);
        notifications.ShouldBe(1);
    }

    /// <summary>Verifies the protected content hook completes before public property notification.</summary>
    [Fact]
    public void Content_WhenChanged_PublishesHookBeforePropertyChanged()
    {
        var owner = new ProbeContentControl();
        var content = new ProbeControl();
        var order = new List<string>();
        owner.ContentChanging = (control, previous, current) =>
        {
            previous.ShouldBeNull();
            current.ShouldBeSameAs(content);
            control.Content.ShouldBeSameAs(content);
            content.Parent.ShouldBeSameAs(control);
            order.Add("hook");
        };
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                order.Add("property");
            }
        };

        owner.Content = content;

        order.ShouldBe(["hook", "property"]);
    }

    /// <summary>Verifies direct child disposal clears content and publishes one property change.</summary>
    [Fact]
    public void Dispose_WhenContentIsDisposedDirectly_ClearsAndNotifiesContent()
    {
        var owner = new ProbeContentControl();
        var content = new ProbeControl();
        owner.Content = content;
        owner.ContentChanges.Clear();
        var notifications = 0;
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                notifications++;
            }
        };

        content.Dispose();

        owner.Content.ShouldBeNull();
        notifications.ShouldBe(1);
        owner.ContentChanges.ShouldBe([(content, null)]);
        content.IsDisposed.ShouldBeTrue();
    }

    /// <summary>Verifies owner disposal completes child disposal after a content callback failure.</summary>
    [Fact]
    public void Dispose_WhenContentCallbackThrows_DisposesOwnerAndContent()
    {
        var owner = new ProbeContentControl();
        var content = new ProbeControl();
        owner.Content = content;
        owner.ThrowOnContentChanged = true;
        var notifications = 0;
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                notifications++;
            }
        };

        var exception = Should.Throw<InvalidOperationException>(owner.Dispose);

        exception.Message.ShouldBe("The content callback failed.");
        owner.IsDisposed.ShouldBeTrue();
        content.IsDisposed.ShouldBeTrue();
        content.DisposingCalls.ShouldBe(1);
        owner.Content.ShouldBeNull();
        notifications.ShouldBe(1);
    }

    /// <summary>Verifies a throwing content property handler cannot interrupt owner or child disposal.</summary>
    [Fact]
    public void Dispose_WhenContentPropertyHandlerThrows_DisposesOwnerAndContent()
    {
        var owner = new ProbeContentControl();
        var content = new ProbeControl();
        owner.Content = content;
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(ContentControl.Content))
            {
                throw new InvalidOperationException("The property callback failed.");
            }
        };

        var exception = Should.Throw<InvalidOperationException>(owner.Dispose);

        exception.Message.ShouldBe("The property callback failed.");
        owner.IsDisposed.ShouldBeTrue();
        content.IsDisposed.ShouldBeTrue();
        content.DisposingCalls.ShouldBe(1);
        owner.Content.ShouldBeNull();
    }

    /// <summary>Verifies a committed content assignment invalidates measure for the next unchanged viewport.</summary>
    [Fact]
    public void Content_WhenAssignedAfterLayout_RemeasuresAtSameViewport()
    {
        var owner = new ProbeContentControl();
        var engine = new LayoutEngine();
        engine.Layout(owner, new Size(8, 2));
        var content = new ProbeControl(new Size(3, 1));

        owner.Content = content;
        engine.Layout(owner, new Size(8, 2));

        content.MeasureConstraints.Count.ShouldBe(1);
        owner.DesiredSize.ShouldBe(new Size(3, 1));
    }

    /// <summary>Verifies MaxWidth/MaxHeight bound the constraint handed to MeasureOverride, not only the
    /// final resolved size - otherwise wrap-capable content measures against the unclamped slot and the
    /// later arrange-time clamp silently clips the surplus it never accounted for.</summary>
    [Fact]
    public void Measure_WhenMaxWidthAndMaxHeightAreSet_ClampsTheContentConstraint()
    {
        var probe = new ProbeControl { MaxWidth = Length.Cells(10), MaxHeight = Length.Cells(3) };

        probe.Measure(new Constraint(40, 40));

        _ = probe.MeasureConstraints.ShouldHaveSingleItem();
        probe.MeasureConstraints[0].Width.ShouldBe(10);
        probe.MeasureConstraints[0].Height.ShouldBe(3);
    }

    /// <summary>Verifies MinWidth/MinHeight bound the content constraint the same way, so a control with
    /// an explicit floor does not measure its content narrower than it will ultimately be arranged.</summary>
    [Fact]
    public void Measure_WhenMinWidthAndMinHeightExceedAnExplicitCellsSize_RaisesTheContentConstraint()
    {
        var probe = new ProbeControl
        {
            Width = Length.Cells(4),
            Height = Length.Cells(2),
            MinWidth = Length.Cells(10),
            MinHeight = Length.Cells(5)
        };

        probe.Measure(new Constraint(40, 40));

        _ = probe.MeasureConstraints.ShouldHaveSingleItem();
        probe.MeasureConstraints[0].Width.ShouldBe(10);
        probe.MeasureConstraints[0].Height.ShouldBe(5);
    }

    /// <summary>Verifies content arranged with both axes already resolved by the owner - the shape every
    /// ContentControl uses under Stretch alignment - still honors MaxWidth/MaxHeight instead of silently
    /// discarding them, matching the two sibling arrange branches that already clamp.</summary>
    [Fact]
    public void Arrange_WhenBothAxesAreResolvedAndContentHasMaxWidthAndMaxHeight_ClampsArrangedBounds()
    {
        var owner = new ProbeContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var content = new ProbeControl(new Size(30, 6)) { MaxWidth = Length.Cells(10), MaxHeight = Length.Cells(3) };
        owner.Content = content;

        new LayoutEngine().Layout(owner, new Size(40, 12));

        content.Bounds.ShouldBe(new Rect(0, 0, 10, 3));
    }

    /// <summary>Verifies a MinWidth/MinHeight that exceeds the owner's resolved slot never grows the
    /// arranged bounds past that slot - the documented order clamps to min/max first, but the resolved
    /// slot is always the final cap, "so tiny viewports always produce contained non-negative rectangles"
    /// (docs/concepts/layout.md).</summary>
    [Fact]
    public void Arrange_WhenBothAxesAreResolvedAndMinWidthExceedsTheSlot_NeverExceedsResolvedBounds()
    {
        var owner = new ProbeContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var content = new ProbeControl(new Size(2, 1)) { MinWidth = Length.Cells(15), MinHeight = Length.Cells(4) };
        owner.Content = content;

        new LayoutEngine().Layout(owner, new Size(6, 2));

        content.Bounds.ShouldBe(new Rect(0, 0, 6, 2));
    }

    /// <summary>Verifies a property subscriber can consume the committed layout without leaving a redundant pass.</summary>
    [Fact]
    public void Content_WhenPropertySubscriberLayoutsAtSameViewport_ConsumesCurrentInvalidationOnce()
    {
        var owner = new ProbeContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var engine = new LayoutEngine();
        var viewport = new Size(8, 2);
        var observed = new List<ControlBase>();
        engine.Layout(owner, viewport);
        owner.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(ContentControl.Content) ||
                owner.Content is not { } current)
            {
                return;
            }

            engine.Layout(owner, viewport);
            current.DesiredSize.ShouldNotBe(default);
            current.Bounds.ShouldBe(new Rect(0, 0, 8, 2));
            observed.Add(current);
        };
        var first = new ProbeControl(new Size(2, 1));
        var second = new ProbeControl(new Size(3, 1));

        owner.Content = first;
        first.MeasureConstraints.Count.ShouldBe(1);
        first.ArrangeBounds.Count.ShouldBe(1);
        (owner.Pending & (Invalidation.Measure | Invalidation.Arrange))
            .ShouldBe(Invalidation.None);
        engine.Layout(owner, viewport);
        first.MeasureConstraints.Count.ShouldBe(1);
        first.ArrangeBounds.Count.ShouldBe(1);

        owner.Content = second;
        second.MeasureConstraints.Count.ShouldBe(1);
        second.ArrangeBounds.Count.ShouldBe(1);
        (owner.Pending & (Invalidation.Measure | Invalidation.Arrange))
            .ShouldBe(Invalidation.None);
        engine.Layout(owner, viewport);

        observed.ShouldBe([first, second]);
        second.MeasureConstraints.Count.ShouldBe(1);
        second.ArrangeBounds.Count.ShouldBe(1);
    }

    /// <summary>Verifies the committed content callback cannot reenter affected structural state.</summary>
    [Fact]
    public void Content_WhenChangeCallbackMutatesStructureOrLifetime_RejectsReentrancy()
    {
        var owner = new ProbeContentControl();
        var previous = new ProbeControl();
        var current = new ProbeControl();
        var part = new ProbeControl();
        var replacement = new ProbeControl();
        var failures = new List<InvalidOperationException>();
        owner.Content = previous;
        owner.ContentChanging = (control, oldContent, newContent) =>
        {
            oldContent.ShouldBeSameAs(previous);
            newContent.ShouldBeSameAs(current);
            failures.Add(Should.Throw<InvalidOperationException>(() => control.AddPart(part)));
            failures.Add(Should.Throw<InvalidOperationException>(() => control.Content = replacement));
            failures.Add(Should.Throw<InvalidOperationException>(() => control.Content = null));
            failures.Add(Should.Throw<InvalidOperationException>(control.Dispose));
            failures.Add(Should.Throw<InvalidOperationException>(previous.Dispose));
            failures.Add(Should.Throw<InvalidOperationException>(current.Dispose));
        };

        owner.Content = current;

        failures.Count.ShouldBe(6);

        foreach (var failure in failures)
        {
            failure.Message.ShouldBe("Owned-control mutation cannot be reentered.");
        }

        owner.Content.ShouldBeSameAs(current);
        owner.IsDisposed.ShouldBeFalse();
        previous.Parent.ShouldBeNull();
        previous.IsDisposed.ShouldBeFalse();
        current.Parent.ShouldBeSameAs(owner);
        current.IsDisposed.ShouldBeFalse();
        part.Parent.ShouldBeNull();
        replacement.Parent.ShouldBeNull();
    }

    /// <summary>Verifies collapsed content contributes neither size nor margin and enters no child layout pass.</summary>
    [Fact]
    public void Layout_WhenContentIsCollapsed_ExcludesContentAndMargin()
    {
        var content = new ProbeControl(new Size(4, 2)) { Margin = new Thickness(5), Visibility = Visibility.Collapsed };
        var owner = new ProbeContentControl { Content = content };

        new LayoutEngine().Layout(owner, new Size(20, 10));

        owner.DesiredSize.ShouldBe(default);
        content.MeasureConstraints.ShouldBeEmpty();
        content.ArrangeBounds.ShouldBeEmpty();
        content.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies the shared arrange completion step clears stale bounds even when the
    /// content owner already visited the collapsed child, without publishing duplicate bounds
    /// notifications.</summary>
    [Fact]
    public void Arrange_WhenPreviouslyArrangedContentCollapses_ClearsBoundsWithoutDuplicateNotification()
    {
        var content = new ProbeControl(new Size(4, 2));
        var owner = new ProbeContentControl { Content = content };
        var engine = new LayoutEngine();
        engine.Layout(owner, new Size(8, 4));
        content.Bounds.ShouldNotBe(default);
        var boundsChanged = 0;
        content.BoundsChanged += (_, _) => boundsChanged++;

        content.Visibility = Visibility.Collapsed;
        engine.Layout(owner, new Size(8, 4));

        content.Bounds.ShouldBe(default);
        boundsChanged.ShouldBe(0);
    }

    /// <summary>Verifies hidden content retains its slot and margin contribution to the owner's
    /// desired size while still excluding rendering and hit-testing - the shared base contract
    /// concrete single-content hosts such as GroupBox and Expander compose their own chrome
    /// around, so a host-specific test only needs to add what the base does not already prove.</summary>
    [Fact]
    public void Layout_WhenContentIsHidden_RetainsSlotAndMarginButExcludesRenderingAndHitTesting()
    {
        var content = new ProbeControl(new Size(1, 1))
        {
            Margin = new Thickness(1),
            Content = "X".AsMemory(),
            Visibility = Visibility.Hidden
        };
        var owner = new ProbeContentControl { Content = content };

        new LayoutEngine().Layout(owner, new Size(3, 3));

        owner.DesiredSize.ShouldBe(new Size(3, 3));
        content.Bounds.ShouldBe(new Rect(1, 1, 1, 1));

        using Frame frame = new(new Size(3, 3));
        owner.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(1, 1)).ShouldNotBe("X");
        owner.HitTest(new Point(1, 1)).ShouldNotBeSameAs(content);
    }

    /// <summary>Verifies collapse clears prior geometry without re-entering child layout overrides.</summary>
    [Fact]
    public void Layout_WhenContentBecomesCollapsed_ClearsCommittedChildGeometry()
    {
        var content = new ProbeControl(new Size(4, 2)) { Margin = new Thickness(1) };
        var owner = new ProbeContentControl { Content = content };
        var engine = new LayoutEngine();
        engine.Layout(owner, new Size(10, 4));
        var measureCalls = content.MeasureConstraints.Count;
        var arrangeCalls = content.ArrangeBounds.Count;

        content.Visibility = Visibility.Collapsed;
        engine.Layout(owner, new Size(10, 4));

        owner.DesiredSize.ShouldBe(default);
        content.DesiredSize.ShouldBe(default);
        content.Bounds.ShouldBe(default);
        content.MeasureConstraints.Count.ShouldBe(measureCalls);
        content.ArrangeBounds.Count.ShouldBe(arrangeCalls);
    }

    /// <summary>Verifies visible content margin contributes with saturating arithmetic.</summary>
    [Fact]
    public void Measure_WhenContentHasMargin_IncludesMarginWithSaturation()
    {
        var content = new ProbeControl(new Size(int.MaxValue, 2))
        {
            Margin = new Thickness(left: 1, top: 2, right: 1, bottom: 3)
        };
        var owner = new ProbeContentControl { Content = content };

        owner.Measure(new Constraint(width: null, height: null));

        owner.DesiredSize.ShouldBe(new Size(int.MaxValue, 7));
        content.MeasureConstraints.ShouldBe([new Constraint(width: null, height: null)]);
    }

    /// <summary>Verifies arrangement resolves both axes while preserving child margin.</summary>
    [Fact]
    public void Arrange_WhenContentHasExplicitSize_StretchesBothResolvedAxesInsideMargin()
    {
        var content = new ProbeControl(new Size(2, 1))
        {
            Width = Length.Cells(2),
            Height = Length.Cells(1),
            Margin = new Thickness(left: 1, top: 2, right: 3, bottom: 4)
        };
        var owner = new ProbeContentControl
        {
            Content = content,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        new LayoutEngine().Layout(owner, new Size(20, 10));

        content.Bounds.ShouldBe(new Rect(1, 2, 16, 4));
        content.ArrangeBounds.ShouldBe([content.Bounds]);
    }

    /// <summary>Verifies ordinary registry traversal renders and hit-tests content.</summary>
    [Fact]
    public void RenderAndHitTest_WhenContentIsVisible_UsesNormalOwnedTraversal()
    {
        var content = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var owner = new ProbeContentControl { Content = content };
        new LayoutEngine().Layout(owner, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        owner.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("X");
        content.RenderCalls.ShouldBe(1);
        owner.HitTest(default).ShouldBeSameAs(content);
    }

    /// <summary>Verifies content participates in focus navigation through slot metadata.</summary>
    [Fact]
    public async Task MoveNext_WhenContentCanFocus_NavigatesToContentAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var content = new ProbeControl { IsFocusable = true };
        var owner = new ProbeContentControl { Content = content };

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);

            focus.MoveNext().ShouldBeTrue();

            focus.Focused.ShouldBeSameAs(content);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies popup discovery descends through the content slot.</summary>
    [Fact]
    public void HitTest_WhenContentContainsPopupBranch_UsesPopupTraversal()
    {
        var popup = new PopupHitProbe();
        var owner = new ProbeContentControl { Content = popup };

        var hit = owner.HitTest(default);

        hit.ShouldBeSameAs(popup);
        popup.PopupHitTestCalls.ShouldBe(1);
    }
}
