// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Collections.ObjectModel;

using SharpVision.DataBinding;

using Support;

using UiListView = ListView;

/// <summary>Verifies observable collection snapshots and replacement subscriptions.</summary>
public sealed class BindingCollectionTests
{
    /// <summary>Verifies membership changes project the latest finite snapshot.</summary>
    [Fact]
    public void BindItems_WhenCollectionChanges_UpdatesListSnapshot()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var source = new ObservableCollection<BindingItem> { first };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        source.Add(second);

        target.Items.ShouldBe(new object?[] { first, second });

        _ = source.Remove(first);

        target.Items.ShouldBe(new object?[] { second });
    }

    /// <summary>Verifies collection replacement detaches the old collection.</summary>
    [Fact]
    public void BindItems_WhenCollectionIsReplaced_ObservesOnlyReplacement()
    {
        var oldSource = new ObservableCollection<BindingItem> { new("Old") };
        var replacement = new ObservableCollection<BindingItem> { new("New") };
        var model = new BindingModel { Items = oldSource };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        model.Items = replacement;
        oldSource.Add(new BindingItem("Ignored"));
        replacement.Add(new BindingItem("Current"));

        target.Items.Select(static item => item!.ToString()).ShouldBe(["New", "Current"]);
    }

    /// <summary>Verifies null and reset collections project to an empty snapshot.</summary>
    [Fact]
    public void BindItems_WhenSourceIsNullOrReset_ProjectsEmptySnapshot()
    {
        var model = new BindingModel();
        var target = new UiListView { Items = ["stale"] };
        using var binding = target.BindItems(model, value => value.Items);

        target.Items.ShouldBeEmpty();

        var source = new ObservableCollection<BindingItem> { new("A"), new("B") };
        model.Items = source;
        source.Clear();

        target.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies replace and move actions read the latest complete ordering.</summary>
    [Fact]
    public void BindItems_WhenItemsReplaceAndMove_UsesLatestOrder()
    {
        var source = new ObservableCollection<BindingItem> { new("A"), new("B"), new("C") };
        var model = new BindingModel { Items = source };
        var target = new UiListView();
        using var binding = target.BindItems(model, value => value.Items);

        source[1] = new BindingItem("X");
        source.Move(2, 0);

        target.Items.Select(static item => item!.ToString()).ShouldBe(["C", "A", "X"]);
    }

    /// <summary>Verifies ComboBox receives the same observable snapshot contract.</summary>
    [Fact]
    public void BindItems_WhenComboSourceChanges_UpdatesChoices()
    {
        var source = new ObservableCollection<BindingItem> { new("A") };
        var model = new BindingModel { Items = source };
        var target = new ComboBox();
        using var binding = target.BindItems(model, value => value.Items);

        source.Add(new BindingItem("B"));

        target.Items.Select(static item => item!.ToString()).ShouldBe(["A", "B"]);
    }
}
