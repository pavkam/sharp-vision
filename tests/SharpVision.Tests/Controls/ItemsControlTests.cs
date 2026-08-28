// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies the private item-presentation authoring role.</summary>
public sealed class ItemsControlTests
{
    /// <summary>Verifies host initialization is one-shot and owns only the private host.</summary>
    [Fact]
    public void InitializeItemsHost_WhenCalledTwice_PreservesOriginalHost()
    {
        var owner = new ProbeItemsControl(initialize: false);
        var original = new Stack();
        var replacement = new Stack();

        owner.Initialize(original);
        _ = Should.Throw<InvalidOperationException>(() => owner.Initialize(replacement));
        replacement.Children.Add(new ProbeControl());
        original.Children.Add(new ProbeControl());

        owner.Host.ShouldBeSameAs(original);
        owner.GetOwnedOrder().ShouldBe([original]);
        owner.Changes.Count.ShouldBe(1);
        original.Parent.ShouldBeSameAs(owner);
        replacement.Parent.ShouldBeNull();
    }

    /// <summary>Verifies a rejected host does not consume the one allowed initialization.</summary>
    [Fact]
    public void InitializeItemsHost_WhenCandidateIsInvalid_AllowsLaterValidHost()
    {
        var owner = new ProbeItemsControl(initialize: false);
        var other = new ProbeContainer();
        var invalid = new Stack();
        var valid = new Stack();
        other.Children.Add(invalid);

        _ = Should.Throw<ArgumentException>(() => owner.Initialize(invalid));
        owner.Initialize(valid);

        owner.Host.ShouldBeSameAs(valid);
        owner.GetOwnedOrder().ShouldBe([valid]);
        invalid.Parent.ShouldBeSameAs(other);
    }

    /// <summary>Verifies every protected item accessor rejects use before
    /// <see cref="ItemsControl.InitializeItemsHost"/> installs a presentation host, instead of
    /// silently no-op'ing or null-referencing against a host that was never committed.</summary>
    [Fact]
    public void ItemAccessors_WhenHostIsNotInitialized_ThrowInvalidOperationException()
    {
        var owner = new ProbeItemsControl(initialize: false);
        var control = new ProbeControl();

        _ = Should.Throw<InvalidOperationException>(() => owner.Count);
        _ = Should.Throw<InvalidOperationException>(() => owner.At(0));
        _ = Should.Throw<InvalidOperationException>(() => owner.IndexOf(control));
        _ = Should.Throw<InvalidOperationException>(() => owner.Insert(0, control));
        _ = Should.Throw<InvalidOperationException>(() => owner.Remove(control));
        _ = Should.Throw<InvalidOperationException>(() => owner.RemoveAt(0));
        _ = Should.Throw<InvalidOperationException>(() => owner.Replace(0, control));
        _ = Should.Throw<InvalidOperationException>(owner.Clear);
        _ = Should.Throw<InvalidOperationException>(() => owner.ReplaceAll([control]));

        control.Parent.ShouldBeNull();
    }

    /// <summary>Verifies an item owner cannot enter a retained tree before its permanent private
    /// presentation host has committed.</summary>
    [Fact]
    public void Add_WhenItemsHostWasNotInitialized_RejectsBeforeOwnershipMutation()
    {
        var parent = new ProbeContainer();
        var owner = new ProbeItemsControl(initialize: false);

        _ = Should.Throw<InvalidOperationException>(() => parent.Children.Add(owner));

        parent.Children.ShouldBeEmpty();
        owner.Parent.ShouldBeNull();
    }

    /// <summary>Verifies dispatcher attachment rejects an incomplete item owner before assigning
    /// inherited runtime context.</summary>
    [Fact]
    public async Task Attach_WhenItemsHostWasNotInitialized_RejectsBeforeContextMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeItemsControl(initialize: false);

        await dispatcher.InvokeAsync(() =>
        {
            _ = Should.Throw<InvalidOperationException>(() => owner.Attach(dispatcher));

            owner.Dispatcher.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies direct host disposal permanently empties rather than reopening the item
    /// owner's constructor-only initialization slot.</summary>
    [Fact]
    public void Dispose_WhenItemsHostIsDisposedDirectly_PreventsReinitializationAndAttachment()
    {
        var owner = new ProbeItemsControl();
        var host = owner.Host.ShouldNotBeNull();
        var replacement = new Stack();

        host.Dispose();

        host.IsDisposed.ShouldBeTrue();
        host.Parent.ShouldBeNull();
        _ = Should.Throw<InvalidOperationException>(() => owner.Initialize(replacement));
        _ = Should.Throw<InvalidOperationException>(owner.ValidateAttachment);
        replacement.Parent.ShouldBeNull();
    }

    /// <summary>Verifies IndexOfItemControl reports the exact realized position, rejects a null
    /// candidate, and reports -1 for a control this owner never realized.</summary>
    [Fact]
    public void IndexOfItemControl_WhenCalled_ReportsExactPositionOrNegativeOne()
    {
        var owner = new ProbeItemsControl();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var stranger = new ProbeControl();
        owner.Insert(0, first);
        owner.Insert(1, second);

        owner.IndexOf(first).ShouldBe(0);
        owner.IndexOf(second).ShouldBe(1);
        owner.IndexOf(stranger).ShouldBe(-1);
        _ = Should.Throw<ArgumentNullException>(() => owner.IndexOf(null!));
    }

    /// <summary>Verifies GetItemControl rejects an out-of-range position.</summary>
    [Fact]
    public void GetItemControl_WhenIndexIsOutOfRange_ThrowsArgumentOutOfRangeException()
    {
        var owner = new ProbeItemsControl();
        owner.Insert(0, new ProbeControl());

        _ = Should.Throw<ArgumentOutOfRangeException>(() => owner.At(1));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => owner.At(-1));
    }

    /// <summary>Verifies InsertItemControl rejects an out-of-range position before mutating the
    /// realized snapshot.</summary>
    [Fact]
    public void InsertItemControl_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var owner = new ProbeItemsControl();
        var existing = new ProbeControl();
        var rejected = new ProbeControl();
        owner.Insert(0, existing);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => owner.Insert(2, rejected));

        owner.Count.ShouldBe(1);
        rejected.Parent.ShouldBeNull();
    }

    /// <summary>Verifies RemoveItemControlAt rejects an out-of-range position before mutating the
    /// realized snapshot.</summary>
    [Fact]
    public void RemoveItemControlAt_WhenIndexIsOutOfRange_ThrowsBeforeMutation()
    {
        var owner = new ProbeItemsControl();
        var only = new ProbeControl();
        owner.Insert(0, only);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => owner.RemoveAt(1));

        owner.Count.ShouldBe(1);
        only.Parent.ShouldBeSameAs(owner.Host);
    }

    /// <summary>Verifies every protected mutation publishes one complete committed snapshot.</summary>
    [Fact]
    public void ItemControls_WhenMutated_PublishOneCommittedSnapshotPerChange()
    {
        var owner = new ProbeItemsControl();
        var first = new ProbeControl();
        var second = new ProbeControl();
        var replacement = new ProbeControl();

        owner.Insert(0, first);
        owner.Insert(1, second);
        owner.Replace(0, replacement);
        owner.RemoveAt(1);
        owner.Remove(second).ShouldBeFalse();
        owner.Clear();
        owner.Clear();

        owner.Count.ShouldBe(0);
        owner.Changes.Count.ShouldBe(5);
        owner.Changes[0].ShouldBe([first]);
        owner.Changes[1].ShouldBe([first, second]);
        owner.Changes[2].ShouldBe([replacement, second]);
        owner.Changes[3].ShouldBe([replacement]);
        owner.Changes[4].ShouldBeEmpty();
        first.Parent.ShouldBeNull();
        first.IsDisposed.ShouldBeFalse();
        second.Parent.ShouldBeNull();
        second.IsDisposed.ShouldBeFalse();
        replacement.Parent.ShouldBeNull();
        replacement.IsDisposed.ShouldBeFalse();
    }

    /// <summary>Verifies complete batch validation preserves the old snapshot on every rejected candidate.</summary>
    [Fact]
    public void ReplaceItemControls_WhenAnyCandidateIsInvalid_PreservesCompleteOldSnapshot()
    {
        var owner = new ProbeItemsControl();
        var existing = new ProbeControl();
        var valid = new ProbeControl();
        var duplicate = new ProbeControl();
        var other = new ProbeContainer();
        var crossOwned = new ProbeControl();
        other.Children.Add(crossOwned);
        owner.ReplaceAll([existing]);
        owner.Changes.Clear();

        _ = Should.Throw<ArgumentNullException>(() => owner.ReplaceAll([valid, null!]));
        _ = Should.Throw<ArgumentException>(() => owner.ReplaceAll([valid, duplicate, duplicate]));
        _ = Should.Throw<ArgumentException>(() => owner.ReplaceAll([valid, crossOwned]));

        owner.Count.ShouldBe(1);
        owner.At(0).ShouldBeSameAs(existing);
        existing.Parent.ShouldBeSameAs(owner.Host);
        valid.Parent.ShouldBeNull();
        duplicate.Parent.ShouldBeNull();
        crossOwned.Parent.ShouldBeSameAs(other);
        owner.Changes.ShouldBeEmpty();
    }

    /// <summary>Verifies callback failure cannot roll back a complete committed replacement.</summary>
    [Fact]
    public void ReplaceItemControls_WhenChangeHookThrows_PreservesCompleteCommittedSnapshot()
    {
        var owner = new ProbeItemsControl();
        var previous = new ProbeControl();
        var first = new ProbeControl();
        var second = new ProbeControl();
        owner.ReplaceAll([previous]);
        owner.Changes.Clear();
        owner.ThrowOnItemsChanged = true;

        var error = Should.Throw<InvalidOperationException>(() => owner.ReplaceAll([first, second]));

        error.Message.ShouldBe("The item callback failed.");
        owner.Count.ShouldBe(2);
        owner.At(0).ShouldBeSameAs(first);
        owner.At(1).ShouldBeSameAs(second);
        owner.Changes.ShouldBe([[first, second]]);
        previous.Parent.ShouldBeNull();
        previous.IsDisposed.ShouldBeFalse();
        first.Parent.ShouldBeSameAs(owner.Host);
        second.Parent.ShouldBeSameAs(owner.Host);
    }

    /// <summary>Verifies disposing an item directly updates semantic inspection and publishes one change.</summary>
    [Fact]
    public void Dispose_WhenItemDisposesDirectly_RemovesItAndPublishesCommittedSnapshot()
    {
        var owner = new ProbeItemsControl();
        var first = new ProbeControl();
        var second = new ProbeControl();
        owner.ReplaceAll([first, second]);
        owner.Changes.Clear();

        first.Dispose();

        owner.Count.ShouldBe(1);
        owner.At(0).ShouldBeSameAs(second);
        owner.IndexOf(first).ShouldBe(-1);
        owner.Changes.ShouldBe([[second]]);
        first.IsDisposed.ShouldBeTrue();
        first.Parent.ShouldBeNull();
    }

    /// <summary>Verifies passthrough layout, ordinary rendering, and hit testing use the private host.</summary>
    [Fact]
    public void LayoutAndRender_WhenItemsExist_UsePrivateHostTraversal()
    {
        var owner = new ProbeItemsControl();
        var item = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        owner.Insert(0, item);
        new LayoutEngine().Layout(owner, new Size(4, 2));
        using Frame frame = new(new Size(4, 2));

        owner.Render(frame.Canvas);

        owner.Host!.Bounds.ShouldBe(owner.Bounds);
        item.RenderCalls.ShouldBe(1);
        FrameOracle.Get(frame, default).ShouldBe("X");
        owner.HitTest(default).ShouldBeSameAs(item);
    }

    /// <summary>Verifies item controls participate in default focus traversal through the private host.</summary>
    [Fact]
    public async Task MoveNext_WhenItemCanFocus_NavigatesToItemAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeItemsControl();
        var item = new ProbeControl { IsFocusable = true };
        owner.Insert(0, item);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);

            focus.MoveNext().ShouldBeTrue();

            focus.Focused.ShouldBeSameAs(item);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies removing an item clears focus and capture before the post-commit hook.</summary>
    [Fact]
    public async Task RemoveItemControl_WhenItemOwnsInput_ClearsFocusAndCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeItemsControl();
        var item = new ProbeControl { IsFocusable = true };
        owner.Insert(0, item);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);
            using PointerManager capture = new(owner);
            focus.Focus(item).ShouldBeTrue();
            capture.Capture(item).ShouldBeTrue();

            owner.Remove(item).ShouldBeTrue();

            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
            owner.Changes[^1].ShouldBeEmpty();
            item.Parent.ShouldBeNull();
            item.IsDisposed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disposing the semantic owner disposes the private host and remaining items once.</summary>
    [Fact]
    public void Dispose_WhenOwnerDisposes_DisposesHostAndRemainingItems()
    {
        var owner = new ProbeItemsControl();
        var host = owner.Host.ShouldNotBeNull();
        var item = new ProbeControl();
        owner.Insert(0, item);

        owner.Dispose();

        owner.IsDisposed.ShouldBeTrue();
        host.IsDisposed.ShouldBeTrue();
        item.IsDisposed.ShouldBeTrue();
        item.DisposingCalls.ShouldBe(1);
    }

    /// <summary>Verifies a collapsed presentation host contributes nothing to the owner's own
    /// measured desired size, proving ItemsControl's own shared Collapsed-aware MeasureOverride
    /// branch rather than only the leaf mechanics ControlBase already enforces on the host itself.</summary>
    [Fact]
    public void Layout_WhenHostIsCollapsed_ContributesNothingToMeasure()
    {
        var owner = new ProbeItemsControl();
        owner.Insert(0, new ProbeControl(new Size(6, 3)));
        owner.Host!.Visibility = Visibility.Collapsed;

        new LayoutEngine().Layout(owner, new Size(20, 10));

        owner.DesiredSize.ShouldBe(default);
        owner.Host.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies a hidden presentation host still contributes its measured size, the same
    /// contribution a visible host of the same realized content would produce.</summary>
    [Fact]
    public void Layout_WhenHostIsHidden_RetainsMeasuredContribution()
    {
        var owner = new ProbeItemsControl
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        owner.Insert(0, new ProbeControl(new Size(6, 3)));
        owner.Host!.Visibility = Visibility.Hidden;

        new LayoutEngine().Layout(owner, new Size(20, 10));

        owner.DesiredSize.ShouldBe(new Size(6, 3));
        owner.Host.Bounds.ShouldBe(new Rect(0, 0, 6, 3));
    }

    /// <summary>Verifies toggling the presentation host between IsVisible and Collapsed reflows the
    /// owner's own desired size on every transition instead of only on the first measure.</summary>
    [Fact]
    public void Layout_WhenHostVisibilityToggles_ReflowsDesiredSizeEachTime()
    {
        var owner = new ProbeItemsControl();
        owner.Insert(0, new ProbeControl(new Size(6, 3)));
        var engine = new LayoutEngine();

        engine.Layout(owner, new Size(20, 10));
        owner.DesiredSize.ShouldBe(new Size(6, 3));

        owner.Host!.Visibility = Visibility.Collapsed;
        engine.Layout(owner, new Size(20, 10));
        owner.DesiredSize.ShouldBe(default);

        owner.Host.Visibility = Visibility.Visible;
        engine.Layout(owner, new Size(20, 10));
        owner.DesiredSize.ShouldBe(new Size(6, 3));
    }
}
