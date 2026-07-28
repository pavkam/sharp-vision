// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using SharpVision.DataBinding;

using Support;

using UiListView = ListView;

/// <summary>Verifies scalar selected-value binding and item-refresh coordination.</summary>
public sealed class BindingSelectionTests
{
    /// <summary>Verifies selected values synchronize by item equality in both directions.</summary>
    [Fact]
    public void BindSelection_WhenEitherEndpointChanges_SynchronizesItem()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var model = new BindingModel
        {
            Items = [first, second],
            SelectedItem = second
        };
        var target = new UiListView();
        using var items = target.BindItems(model, value => value.Items);
        using var selection = target.BindSelection(model, value => value.SelectedItem);

        target.SelectedIndex.ShouldBe(1);

        target.SelectedIndex = 0;

        model.SelectedItem.ShouldBeSameAs(first);

        target.SelectedIndex = -1;

        model.SelectedItem.ShouldBeNull();
    }

    /// <summary>Verifies item replacement cannot write a transient empty selection to the model.</summary>
    [Fact]
    public void BindItems_WhenItemsAreReplaced_PreservesModelSelection()
    {
        var selected = new BindingItem("Selected");
        var model = new BindingModel
        {
            Items = [selected],
            SelectedItem = selected
        };
        var target = new UiListView();
        using var items = target.BindItems(model, value => value.Items);
        using var selection = target.BindSelection(model, value => value.SelectedItem);

        model.Items =
        [
            new BindingItem("Other"),
            selected
        ];

        model.SelectedItem.ShouldBeSameAs(selected);
        target.SelectedIndex.ShouldBe(1);
    }

    /// <summary>Verifies an unmatched model value clears target selection without changing the model.</summary>
    [Fact]
    public void BindSelection_WhenValueIsUnmatched_ClearsTarget()
    {
        var missing = new BindingItem("Missing");
        var model = new BindingModel
        {
            Items = [new BindingItem("A")],
            SelectedItem = missing
        };
        var target = new UiListView();
        using var items = target.BindItems(model, value => value.Items);
        using var selection = target.BindSelection(model, value => value.SelectedItem);

        target.SelectedIndex.ShouldBe(-1);
        model.SelectedItem.ShouldBeSameAs(missing);
    }

    /// <summary>Verifies unsupported ListView selection modes fail before binding registration.</summary>
    [Fact]
    public void BindSelection_WhenListAllowsMany_Throws()
    {
        var model = new BindingModel { Items = [] };
        var target = new UiListView { SelectionMode = ListSelectionMode.Multiple };

        _ = Should.Throw<ArgumentException>(() =>
            target.BindSelection(model, value => value.SelectedItem));
    }

    /// <summary>Verifies ComboBox selected-value binding follows its item snapshot.</summary>
    [Fact]
    public void BindSelection_WhenComboChanges_SynchronizesItem()
    {
        var first = new BindingItem("A");
        var second = new BindingItem("B");
        var model = new BindingModel
        {
            Items = [first, second],
            SelectedItem = first
        };
        var target = new ComboBox();
        using var items = target.BindItems(model, value => value.Items);
        using var selection = target.BindSelection(model, value => value.SelectedItem);

        target.SelectedIndex = 1;

        model.SelectedItem.ShouldBeSameAs(second);
    }
}
