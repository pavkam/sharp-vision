// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Reflection;

/// <summary>Verifies the private item-presentation authoring role.</summary>
public sealed class ItemsControlTests
{
    /// <summary>Verifies the role exposes semantic helpers without leaking its presentation container.</summary>
    [Fact]
    public void Type_WhenInspected_ExposesOnePrivateItemPresentationRole()
    {
        var type = typeof(ItemsControl);
        var protectedNames = type.GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(static method => method.DeclaringType == typeof(ItemsControl) && method.IsFamily)
            .Select(static method => method.Name)
            .ToArray();

        type.IsPublic.ShouldBeTrue();
        type.IsAbstract.ShouldBeTrue();
        type.BaseType.ShouldBe(typeof(Control));
        typeof(IStyleScope).IsAssignableFrom(type).ShouldBeTrue();
        type.GetProperty("Children", BindingFlags.Public | BindingFlags.Instance).ShouldBeNull();
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .ShouldNotContain(static property => typeof(Container).IsAssignableFrom(property.PropertyType));
        protectedNames.ShouldContain("InitializeItemsHost");
        protectedNames.ShouldContain("GetItemControl");
        protectedNames.ShouldContain("IndexOfItemControl");
        protectedNames.ShouldContain("InsertItemControl");
        protectedNames.ShouldContain("RemoveItemControl");
        protectedNames.ShouldContain("RemoveItemControlAt");
        protectedNames.ShouldContain("ReplaceItemControl");
        protectedNames.ShouldContain("ClearItemControls");
        protectedNames.ShouldContain("ReplaceItemControls");
        protectedNames.ShouldContain("OnItemControlsChanged");
    }

    /// <summary>Verifies host initialization is one-shot and owns only the private host.</summary>
    [Fact]
    public void InitializeItemsHost_WhenCalledTwice_PreservesOriginalHost()
    {
        var owner = new ProbeItemsControl(initialize: false);
        var original = new Stack();
        var replacement = new Stack();

        owner.Initialize(original);
        _ = Should.Throw<InvalidOperationException>(() => owner.Initialize(replacement));

        owner.Host.ShouldBeSameAs(original);
        owner.GetOwnedOrder().ShouldBe([original]);
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
        new Engine().Layout(owner, new Size(4, 2));
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
        var item = new ProbeControl { CanFocus = true };
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
        var item = new ProbeControl { CanFocus = true };
        owner.Insert(0, item);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);
            using CaptureManager capture = new(owner);
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

    /// <summary>Verifies the semantic item owner is a style scope for its private presentation subtree.</summary>
    [Fact]
    public void Foreground_WhenOwnerStyleDefinesValue_CascadesToItem()
    {
        var owner = new ProbeItemsControl();
        var item = new ProbeControl();
        var style = new ControlStyle<ProbeItemsControl>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(6));
        owner.Style = style;
        owner.Insert(0, item);

        (item.Foreground == Color.Indexed(6)).ShouldBeTrue();
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
}
