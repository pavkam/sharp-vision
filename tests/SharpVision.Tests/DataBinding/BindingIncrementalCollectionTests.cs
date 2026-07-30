// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Collections.ObjectModel;

using SharpVision.DataBinding;

using Support;

using UiListView = ListView;

/// <summary>Verifies incremental collection change support through the binding pipeline.</summary>
public sealed class BindingIncrementalCollectionTests
{
    /// <summary>Verifies a single Add to ObservableCollection applies incrementally to the list.</summary>
    [Fact]
    public void BindItems_WhenSingleItemAdded_AppliesIncrementally()
    {
        var source = new ObservableCollection<BindingItem> { new("A"), new("B") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        var third = new BindingItem("C");

        source.Add(third);

        target.Items.ShouldBe(new object?[] { source[0], source[1], third });
    }

    /// <summary>Verifies a single Remove from ObservableCollection applies incrementally.</summary>
    [Fact]
    public void BindItems_WhenSingleItemRemoved_AppliesIncrementally()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var third = new BindingItem("C");
        var source = new ObservableCollection<BindingItem> { first, second, third };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        _ = source.Remove(second);

        target.Items.ShouldBe(new object?[] { first, third });
    }

    /// <summary>Verifies a single Replace in ObservableCollection applies incrementally.</summary>
    [Fact]
    public void BindItems_WhenSingleItemReplaced_AppliesIncrementally()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var source = new ObservableCollection<BindingItem> { first, second };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        var replacement = new BindingItem("X");

        source[1] = replacement;

        target.Items.ShouldBe(new object?[] { first, replacement });
    }

    /// <summary>Verifies Insert at a specific position updates the list correctly.</summary>
    [Fact]
    public void BindItems_WhenInsertedAtPosition_InsertsAtCorrectIndex()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("C");
        var source = new ObservableCollection<BindingItem> { first, second };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        var inserted = new BindingItem("B");

        source.Insert(1, inserted);

        target.Items.ShouldBe(new object?[] { first, inserted, second });
    }

    /// <summary>Verifies Move falls back to full snapshot since it is not incrementally handled.</summary>
    [Fact]
    public void BindItems_WhenItemMoved_FallsBackToFullSnapshot()
    {
        var source = new ObservableCollection<BindingItem> { new("A"), new("B"), new("C") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        source.Move(2, 0);

        target.Items.Select(static item => item!.ToString()).ShouldBe(["C", "A", "B"]);
    }

    /// <summary>Verifies Clear falls back to full snapshot.</summary>
    [Fact]
    public void BindItems_WhenCleared_FallsBackToFullSnapshot()
    {
        var source = new ObservableCollection<BindingItem> { new("A"), new("B") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        source.Clear();

        target.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies selection is preserved when inserting before the selected index.</summary>
    [Fact]
    public void BindItems_WhenInsertedBeforeSelection_ShiftsSelection()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var source = new ObservableCollection<BindingItem> { first, second };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        target.SelectedIndex = 1;

        source.Insert(0, new BindingItem("Z"));

        target.SelectedIndex.ShouldBe(2);
        target.Items[target.SelectedIndex].ShouldBe(second);
    }

    /// <summary>Verifies removing a selected item clears the selection through binding.</summary>
    [Fact]
    public void BindItems_WhenSelectedItemRemoved_ClearsSelection()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var third = new BindingItem("C");
        var source = new ObservableCollection<BindingItem> { first, second, third };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);
        target.SelectedIndex = 1;

        _ = source.Remove(second);

        target.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies collection replacement after incremental changes works correctly.</summary>
    [Fact]
    public void BindItems_WhenCollectionReplacedAfterIncrementalChanges_UsesNewCollection()
    {
        var first = new ObservableCollection<BindingItem> { new("A") };
        var model = new BindingModel { Items = first };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        first.Add(new BindingItem("B"));
        target.Items.Count.ShouldBe(2);

        var second = new ObservableCollection<BindingItem> { new("X"), new("Y"), new("Z") };
        model.Items = second;

        target.Items.Select(static item => item!.ToString()).ShouldBe(["X", "Y", "Z"]);

        second.Add(new BindingItem("W"));

        target.Items.Select(static item => item!.ToString()).ShouldBe(["X", "Y", "Z", "W"]);
    }

    /// <summary>
    /// Verifies a collection-changed notification from a source the binding has
    /// already moved past does not apply to the replacement target. The
    /// interleaving is deterministic: an application handler registered on the
    /// original collection before the binding subscribes replaces the bound
    /// source from within its own callback, so .NET's already-snapshotted
    /// invocation list still delivers the stale notification to the binding
    /// after it has re-observed the new source.
    /// </summary>
    [Fact]
    public void BindItems_WhenStaleAddArrivesAfterSourceReplacement_KeepsOnlyReplacementItems()
    {
        var first = new ObservableCollection<BindingItem> { new("A") };
        var replacementItem = new BindingItem("X");
        var second = new ObservableCollection<BindingItem> { replacementItem };
        var model = new BindingModel { Items = first };
        var target = new UiListView();

        first.CollectionChanged += (_, _) => model.Items = second;

        using var binding = target.BindItems(model, value => value.Items);

        first.Add(new BindingItem("stale"));

        target.Items.ShouldBe(new object?[] { replacementItem });
    }

    /// <summary>
    /// Verifies an Add notification whose reported index exceeds the target's current item
    /// count is ignored instead of throwing, matching the guard Remove and Replace already
    /// apply. Ordinary <see cref="ObservableCollection{T}"/> mutations always report a
    /// consistent index, so this uses a spoofable source to simulate the hand-rolled
    /// <see cref="System.Collections.Specialized.INotifyCollectionChanged"/> source, coalesced/batched
    /// event, or diverged-count timing the underlying bug is actually reachable from.
    /// </summary>
    [Fact]
    public void BindItems_WhenAddIndexExceedsTargetCount_IsIgnoredWithoutThrowing()
    {
        var source = new SpoofableAddCollection<BindingItem> { new("A") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        Should.NotThrow(() => source.RaiseSpoofedAdd(new BindingItem("out-of-range"), 5));

        target.Items.ShouldBe(new object?[] { source[0] });
    }

    /// <summary>
    /// Verifies the same stale-source interleaving does not mutate the target
    /// after the binding has been disposed.
    /// </summary>
    [Fact]
    public void BindItems_WhenStaleAddArrivesAfterDisposal_DoesNotMutateTarget()
    {
        var first = new ObservableCollection<BindingItem> { new("A") };
        var model = new BindingModel { Items = first };
        var target = new UiListView();
        Binding? binding = null;

        // Registered before the binding subscribes, so it runs first in the
        // snapshotted invocation list and disposes before the binding's own
        // handler (later in that same already-captured list) is reached.
        first.CollectionChanged += (_, _) => binding!.Dispose();
        binding = target.BindItems(model, value => value.Items);

        first.Add(new BindingItem("stale"));

        target.Items.Count.ShouldBe(1);
    }
}
