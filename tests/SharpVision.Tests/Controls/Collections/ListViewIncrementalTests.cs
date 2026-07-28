// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using Label = ControlText;
using UiListView = ListView;

/// <summary>Verifies incremental insert, remove, and replace operations on ListView.</summary>
public sealed class ListViewIncrementalTests
{
    /// <summary>Verifies inserting at the end appends one item and realizes one control.</summary>
    [Fact]
    public void InsertItem_WhenAppended_AddsOneItemAndControl()
    {
        var control = Create("A", "B");

        control.InsertItem(2, "C");

        control.Items.ShouldBe(new object?[] { "A", "B", "C" });
    }

    /// <summary>Verifies inserting at the beginning shifts existing items.</summary>
    [Fact]
    public void InsertItem_WhenPrepended_ShiftsExistingItems()
    {
        var control = Create("B", "C");

        control.InsertItem(0, "A");

        control.Items.ShouldBe(new object?[] { "A", "B", "C" });
    }

    /// <summary>Verifies inserting in the middle maintains correct ordering.</summary>
    [Fact]
    public void InsertItem_WhenInsertedInMiddle_MaintainsOrdering()
    {
        var control = Create("A", "C");

        control.InsertItem(1, "B");

        control.Items.ShouldBe(new object?[] { "A", "B", "C" });
    }

    /// <summary>Verifies inserting shifts the selected index when it follows the insertion point.</summary>
    [Fact]
    public void InsertItem_WhenSelectionFollowsInsertionPoint_ShiftsSelectedIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        control.InsertItem(0, "Z");

        control.SelectedIndex.ShouldBe(2);
        control.Items[control.SelectedIndex].ShouldBe("B");
    }

    /// <summary>Verifies inserting before the active index shifts it correctly.</summary>
    [Fact]
    public void InsertItem_WhenActiveIndexFollowsInsertionPoint_ShiftsActiveIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 2;

        control.InsertItem(0, "Z");

        control.ActiveIndex.ShouldBe(3);
    }

    /// <summary>Verifies inserting before the active row reports the shifted active index.</summary>
    [Fact]
    public void InsertItem_WhenActiveIndexShifts_NotifiesActiveIndexObservers()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 2;
        List<string?> notifications = [];
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        control.InsertItem(0, "Z");

        control.ActiveIndex.ShouldBe(3);
        notifications.Count(name => name == nameof(UiListView.ActiveIndex)).ShouldBe(1);
    }

    /// <summary>Verifies shifting selection indexes notifies selected properties without raising a selection event.</summary>
    [Fact]
    public void InsertItem_WhenSelectionFollowsInsertionPoint_NotifiesSelectedPropertyObservers()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 2;
        List<string?> notifications = [];
        var selectionChanged = 0;
        control.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);
        control.SelectionChanged += (_, _) => selectionChanged++;

        control.InsertItem(0, "Z");

        control.SelectedIndex.ShouldBe(3);
        control.SelectedItem.ShouldBe("C");
        control.SelectedItems.ShouldBe(new object?[] { "C" });
        notifications.ShouldContain(nameof(UiListView.ActiveIndex));
        notifications.ShouldContain(nameof(UiListView.SelectedIndex));
        notifications.ShouldContain(nameof(UiListView.SelectedItem));
        notifications.ShouldContain(nameof(UiListView.SelectedItems));
        selectionChanged.ShouldBe(0);
    }

    /// <summary>Verifies inserting after the selection does not shift it.</summary>
    [Fact]
    public void InsertItem_WhenInsertedAfterSelection_PreservesSelectedIndex()
    {
        var control = Create("A", "B");
        control.SelectedIndex = 0;

        control.InsertItem(2, "C");

        control.SelectedIndex.ShouldBe(0);
        control.Items[control.SelectedIndex].ShouldBe("A");
    }

    /// <summary>Verifies inserting into an empty list produces one item.</summary>
    [Fact]
    public void InsertItem_WhenListIsEmpty_ProducesOneItem()
    {
        var control = Create();

        control.InsertItem(0, "A");

        control.Items.ShouldBe(new object?[] { "A" });
    }

    /// <summary>Verifies removing the last item produces an empty list.</summary>
    [Fact]
    public void RemoveItem_WhenSingleItemRemoved_ProducesEmptyList()
    {
        var control = Create("A");

        control.RemoveItem(0);

        control.Items.ShouldBeEmpty();
    }

    /// <summary>Verifies removing the first item shifts remaining items down.</summary>
    [Fact]
    public void RemoveItem_WhenFirstItemRemoved_ShiftsRemainingItems()
    {
        var control = Create("A", "B", "C");

        control.RemoveItem(0);

        control.Items.ShouldBe(new object?[] { "B", "C" });
    }

    /// <summary>Verifies removing a middle item preserves surrounding items.</summary>
    [Fact]
    public void RemoveItem_WhenMiddleItemRemoved_PreservesSurroundingItems()
    {
        var control = Create("A", "B", "C");

        control.RemoveItem(1);

        control.Items.ShouldBe(new object?[] { "A", "C" });
    }

    /// <summary>Verifies removing a selected item clears the selection.</summary>
    [Fact]
    public void RemoveItem_WhenSelectedItemRemoved_ClearsSelection()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        control.RemoveItem(1);

        control.SelectedIndex.ShouldBe(-1);
    }

    /// <summary>Verifies removing the active row never leaves a disabled row active.</summary>
    [Fact]
    public void RemoveItem_WhenNextActiveRowIsDisabled_FallsBackToNoActiveRow()
    {
        List<Label> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item =>
            {
                var label = new Label((string) item!);

                if (Equals(item, "B"))
                {
                    label.IsEnabled = false;
                }

                realized.Add(label);
                return label;
            },
            Items = ["A", "B"],
            SelectedIndex = 0
        };

        control.RemoveItem(0);

        control.ActiveIndex.ShouldBe(-1);
        realized[1].EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies removing before the selection shifts the selected index down.</summary>
    [Fact]
    public void RemoveItem_WhenRemovedBeforeSelection_ShiftsSelectedIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 2;

        control.RemoveItem(0);

        control.SelectedIndex.ShouldBe(1);
        control.Items[control.SelectedIndex].ShouldBe("C");
    }

    /// <summary>Verifies removing one of shifted multiple selections reports the stable removed occurrence.</summary>
    [Fact]
    public void RemoveItem_WhenSelectedItemsShift_ReportsStableSelectionDelta()
    {
        var control = Create("A", "B", "C");
        control.SelectionMode = ListSelectionMode.Multiple;
        _ = control.SetSelected(1, true);
        _ = control.SetSelected(2, true);
        ListSelectionChangedEventArgs? changed = null;
        control.SelectionChanged += (_, eventArgs) => changed = eventArgs;

        control.RemoveItem(1);

        _ = changed.ShouldNotBeNull();
        changed.RemovedIndexes.ToArray().ShouldBe([1]);
        changed.AddedIndexes.ToArray().ShouldBeEmpty();
        control.SelectedItems.ShouldBe(new object?[] { "C" });
    }

    /// <summary>Verifies removing after the selection preserves it.</summary>
    [Fact]
    public void RemoveItem_WhenRemovedAfterSelection_PreservesSelectedIndex()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 0;

        control.RemoveItem(2);

        control.SelectedIndex.ShouldBe(0);
        control.Items[control.SelectedIndex].ShouldBe("A");
    }

    /// <summary>Verifies replacing an item swaps the value and realized control.</summary>
    [Fact]
    public void ReplaceItem_WhenCalled_SwapsItemValue()
    {
        var control = Create("A", "B", "C");

        control.ReplaceItem(1, "X");

        control.Items.ShouldBe(new object?[] { "A", "X", "C" });
    }

    /// <summary>Verifies replacing a selected item with an equal value preserves the selection.</summary>
    [Fact]
    public void ReplaceItem_WhenSelectedItemIsEqual_PreservesSelection()
    {
        var control = Create("A", "B", "C");
        control.SelectedIndex = 1;

        control.ReplaceItem(1, "B");

        control.SelectedIndex.ShouldBe(1);
        control.Items[control.SelectedIndex].ShouldBe("B");
    }

    /// <summary>Verifies replacing one selected item with an unequal value narrows selection and keeps the active row valid.</summary>
    [Fact]
    public void ReplaceItem_WhenSelectedItemIsUnequal_ClearsOnlyReplacedSelection()
    {
        var control = Create("A", "B", "C");
        control.SelectionMode = ListSelectionMode.Multiple;
        _ = control.SetSelected(1, true);
        _ = control.SetSelected(2, true);

        control.ReplaceItem(1, "X");

        control.SelectedItems.ShouldBe(new object?[] { "C" });
        control.SelectedIndex.ShouldBe(2);
        control.ActiveIndex.ShouldBe(2);
        control.Items[control.ActiveIndex].ShouldBe("C");
    }

    /// <summary>Verifies replacing the active row with a disabled row selects the nearest available fallback.</summary>
    [Fact]
    public void ReplaceItem_WhenReplacementIsDisabled_FallsBackToAvailableActiveIndex()
    {
        List<ControlText> realized = [];
        var control = new UiListView
        {
            ItemTemplate = item =>
            {
                var text = new ControlText((string) item!);

                if (Equals(item, "Disabled"))
                {
                    text.IsEnabled = false;
                }

                realized.Add(text);
                return text;
            },
            Items = ["A", "B"],
            SelectedIndex = 1
        };

        control.ReplaceItem(1, "Disabled");

        control.ActiveIndex.ShouldBe(0);
        control.Items[control.ActiveIndex].ShouldBe("A");
        realized[^1].EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies multiple sequential inserts produce the correct final state.</summary>
    [Fact]
    public void InsertItem_WhenMultipleSequentialInserts_ProducesCorrectState()
    {
        var control = Create();

        control.InsertItem(0, "C");
        control.InsertItem(0, "A");
        control.InsertItem(1, "B");

        control.Items.ShouldBe(new object?[] { "A", "B", "C" });
    }

    /// <summary>Verifies interleaved insert and remove produces correct state.</summary>
    [Fact]
    public void InsertAndRemove_WhenInterleaved_ProducesCorrectState()
    {
        var control = Create("A", "B", "C");

        control.InsertItem(1, "X");
        control.RemoveItem(3);
        control.InsertItem(3, "D");

        control.Items.ShouldBe(new object?[] { "A", "X", "B", "D" });
    }

    /// <summary>Verifies removing the last item when it is active clamps the active index.</summary>
    [Fact]
    public void RemoveItem_WhenLastItemIsActive_ClampsActiveIndex()
    {
        var control = Create("A", "B");
        control.SelectedIndex = 1;

        control.RemoveItem(1);

        control.ActiveIndex.ShouldBeLessThan(control.Items.Count);
    }

    /// <summary>Verifies multiple selections shift correctly on insert in Multiple mode.</summary>
    [Fact]
    public void InsertItem_WhenMultipleSelectionMode_ShiftsAllSelectedIndices()
    {
        var control = Create("A", "B", "C", "D");
        control.SelectionMode = ListSelectionMode.Multiple;
        _ = control.SetSelected(1, true);
        _ = control.SetSelected(3, true);

        control.InsertItem(0, "Z");

        control.SelectedItems.ShouldBe(new object?[] { "B", "D" });
    }

    private static UiListView Create(params object?[] items) => new() { Items = items };
}
