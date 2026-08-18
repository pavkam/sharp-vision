// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Drives every selection-owning control through one shared table of operations, so a
/// contract that holds for one of them is proven to hold for all six - and, where it deliberately
/// does not, the divergence is written down here instead of being discovered by a bug.
///
/// This was identified as the single highest-value missing test: the suite exercised each
/// control alone, so a selection invariant fixed in one place was never checked against its
/// siblings. There is no shared selection interface to test through - <see cref="Menu"/>
/// publishes no selection event at all, only three of the six expose an index, and the
/// removal-repair policies genuinely differ - so each control is projected into the small
/// <see cref="Subject"/> shape below and the divergences become declared data
/// (<see cref="Repair"/>, <see cref="Subject.IndexApi"/>,
/// <see cref="Subject.AutoSelectsFirstEntry"/>) that every case asserts against.</summary>
public sealed class SelectionParityTests
{
    /// <summary>Gets every control that owns a selection.</summary>
    public static TheoryData<string> AllControls =>
        ["TabControl", "Menu", "NavigationView", "ListView", "TreeView", "Table"];

    /// <summary>Gets the controls that expose an index-based selection API.</summary>
    public static TheoryData<string> IndexedControls => ["TabControl", "Menu", "ListView"];

    /// <summary>Verifies selecting an entry commits exactly that entry, in every control.</summary>
    /// <param name="name">The control under test.</param>
    [Theory]
    [MemberData(nameof(AllControls))]
    public void Select_WhenEntryIsValid_CommitsThatExactEntry(string name)
    {
        var subject = Create(name);

        subject.SelectAt(2);

        subject.IsSelected().ShouldBe(subject.Entries[2], $"{name} must commit the requested entry");
    }

    /// <summary>Verifies moving the selection reports the newly selected entry and leaves no other
    /// entry selected, so no control quietly accumulates a second selection under its default
    /// single-selection mode.</summary>
    /// <param name="name">The control under test.</param>
    [Theory]
    [MemberData(nameof(AllControls))]
    public void Select_WhenSelectionMoves_ReplacesRatherThanAccumulates(string name)
    {
        var subject = Create(name);

        subject.SelectAt(1);
        subject.SelectAt(3);

        subject.IsSelected().ShouldBe(subject.Entries[3], $"{name} must move the selection");
        subject.SelectedCount().ShouldBe(1, $"{name} must hold exactly one selection by default");
    }

    /// <summary>The invariant this file exists to generalize. A rejected selection must leave the
    /// previous one intact - a control that clears first and validates second loses the user's
    /// selection on a mistake. It was fixed in Menu and already held in TabControl; this proves it
    /// for every control that has an index at all.</summary>
    /// <param name="name">The control under test.</param>
    [Theory]
    [MemberData(nameof(IndexedControls))]
    public void SelectIndex_WhenIndexIsOutOfRange_ThrowsAndPreservesThePriorSelection(string name)
    {
        var subject = Create(name);
        subject.SelectAt(2);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => subject.IndexApi!(subject.Entries.Count));
        _ = Should.Throw<ArgumentOutOfRangeException>(() => subject.IndexApi!(-2));

        subject.IsSelected().ShouldBe(subject.Entries[2], $"{name} must not lose its selection to a rejected assignment");
    }

    /// <summary>Verifies -1 is the accepted "no selection" index everywhere an index exists, rather
    /// than one control treating it as out of range.</summary>
    /// <param name="name">The control under test.</param>
    [Theory]
    [MemberData(nameof(IndexedControls))]
    public void SelectIndex_WhenIndexIsNegativeOne_ClearsTheSelection(string name)
    {
        var subject = Create(name);
        subject.SelectAt(2);

        subject.IndexApi!(-1);

        subject.IsSelected().ShouldBeNull($"{name} must treat -1 as no selection");
    }

    /// <summary>Pins the removal-repair policy per control. The six genuinely disagree, and that is
    /// the point: three repair to a neighbour so a menu or tab strip is never left with nothing
    /// current, and three drop the selection so a data view never reports a row the caller did not
    /// choose. Asserting the declared policy makes a silent change to either group fail here.</summary>
    /// <param name="name">The control under test.</param>
    [Theory]
    [MemberData(nameof(AllControls))]
    public void Remove_WhenSelectedEntryIsRemoved_FollowsTheDeclaredRepairPolicy(string name)
    {
        var subject = Create(name);
        subject.SelectAt(1);

        subject.RemoveAt(1);

        if (subject.Repair == Repair.Neighbour)
        {
            subject.IsSelected().ShouldBe(
                subject.Entries[2],
                $"{name} repairs to the following entry when the selected one is removed");
        }
        else
        {
            subject.IsSelected().ShouldBeNull(
                $"{name} drops the selection when the selected entry is removed");
        }
    }

    /// <summary>Verifies removing an unselected entry never disturbs the selection, in every
    /// control - the case both repair policies must agree on.</summary>
    /// <param name="name">The control under test.</param>
    [Theory]
    [MemberData(nameof(AllControls))]
    public void Remove_WhenAnotherEntryIsRemoved_LeavesTheSelectionOnTheSameEntry(string name)
    {
        var subject = Create(name);
        subject.SelectAt(2);
        var selected = subject.Entries[2];

        subject.RemoveAt(0);

        subject.IsSelected().ShouldBe(selected, $"{name} must keep the same entry selected");
    }

    /// <summary>Verifies emptying a control clears its selection rather than leaving a dangling
    /// reference to a removed entry.</summary>
    /// <param name="name">The control under test.</param>
    [Theory]
    [MemberData(nameof(AllControls))]
    public void Remove_WhenEveryEntryIsRemoved_LeavesNoSelection(string name)
    {
        var subject = Create(name);
        subject.SelectAt(1);

        for (var index = subject.Entries.Count - 1; index >= 0; index--)
        {
            subject.RemoveAt(index);
        }

        subject.IsSelected().ShouldBeNull($"{name} must hold no selection once empty");
        subject.SelectedCount().ShouldBe(0, $"{name} must report no selected entries once empty");
    }

    /// <summary>Pins whether a control adopts its first entry automatically. TabControl and Menu do -
    /// a tab strip with nothing current, or a menu with no highlight, is not a usable state - while
    /// the four data-shaped controls start empty and wait to be told. This is the divergence most
    /// likely to surprise a caller wiring up a selection event, because those two publish (or
    /// notify) once during construction.</summary>
    /// <param name="name">The control under test.</param>
    [Theory]
    [MemberData(nameof(AllControls))]
    public void Construct_WhenEntriesAreAdded_MatchesTheDeclaredAutoSelectionPolicy(string name)
    {
        var subject = Create(name);

        if (subject.AutoSelectsFirstEntry)
        {
            subject.IsSelected().ShouldBe(
                subject.Entries[0],
                $"{name} adopts its first entry so it is never left with nothing current");
        }
        else
        {
            subject.IsSelected().ShouldBeNull($"{name} starts with no selection");
        }
    }

    private enum Repair
    {
        Neighbour,
        Drop
    }

    private sealed class Subject
    {
        public required IReadOnlyList<object> Entries { get; init; }

        public required Func<object?> IsSelected { get; init; }

        public required Func<int> SelectedCount { get; init; }

        public required Action<int> SelectAt { get; init; }

        public required Action<int> RemoveAt { get; init; }

        public required Repair Repair { get; init; }

        public required bool AutoSelectsFirstEntry { get; init; }

        /// <summary>Gets the raw index assignment, or null for a control with no index API.</summary>
        public Action<int>? IndexApi { get; init; }
    }

    private static Subject Create(string name) => name switch
    {
        "TabControl" => CreateTabControl(),
        "Menu" => CreateMenu(),
        "NavigationView" => CreateNavigationView(),
        "ListView" => CreateListView(),
        "TreeView" => CreateTreeView(),
        "Table" => CreateTable(),
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown control.")
    };

    private static string[] Labels => ["A", "B", "C", "D"];

    private static Subject CreateTabControl()
    {
        var control = new TabControl();
        var entries = Labels
            .Select(label => new TabItem { HeaderText = label, Content = new ControlText(label) })
            .ToArray();

        foreach (var entry in entries)
        {
            control.Items.Add(entry);
        }

        return new Subject
        {
            Entries = entries,
            IsSelected = () => control.SelectedItem,
            SelectedCount = () => control.SelectedItem is null ? 0 : 1,
            SelectAt = index => control.SelectedIndex = index,
            RemoveAt = control.Items.RemoveAt,
            IndexApi = index => control.SelectedIndex = index,
            Repair = Repair.Neighbour,
            AutoSelectsFirstEntry = true
        };
    }

    private static Subject CreateMenu()
    {
        var control = new Menu();
        var entries = Labels.Select(label => new MenuItem { Text = label }).ToArray();

        foreach (var entry in entries)
        {
            control.Items.Add(entry);
        }

        return new Subject
        {
            Entries = entries,
            IsSelected = () => control.SelectedItem,
            SelectedCount = () => control.SelectedItem is null ? 0 : 1,
            SelectAt = index => control.SelectedIndex = index,
            RemoveAt = control.Items.RemoveAt,
            IndexApi = index => control.SelectedIndex = index,
            Repair = Repair.Neighbour,
            AutoSelectsFirstEntry = true
        };
    }

    private static Subject CreateNavigationView()
    {
        var control = new NavigationView();
        var entries = Labels.Select(label => new NavigationViewItem { Text = label }).ToArray();

        foreach (var entry in entries)
        {
            control.Items.Add(entry);
        }

        return new Subject
        {
            Entries = entries,
            IsSelected = () => control.SelectedItem,
            SelectedCount = () => control.SelectedItem is null ? 0 : 1,
            SelectAt = index => control.SelectItem(entries[index]),
            RemoveAt = control.Items.RemoveAt,
            Repair = Repair.Neighbour,
            AutoSelectsFirstEntry = false
        };
    }

    private static Subject CreateListView()
    {
        var control = new UiListView { Items = [.. Labels] };

        // ListView's only public mutation surface is a whole-list assignment, so removal is modelled
        // by reassigning without the entry. The working list is deliberately separate from Entries -
        // sharing one would shift the very indices the assertions address.
        var remaining = Labels.Cast<object>().ToList();
        var entries = Labels.Cast<object>().ToArray();

        return new Subject
        {
            Entries = entries,
            IsSelected = () => control.SelectedItem,
            SelectedCount = () => control.SelectedItem is null ? 0 : 1,
            SelectAt = index => control.SelectedIndex = index,
            RemoveAt = index =>
            {
                remaining.RemoveAt(index);
                control.Items = [.. remaining];
            },
            IndexApi = index => control.SelectedIndex = index,
            Repair = Repair.Drop,
            AutoSelectsFirstEntry = false
        };
    }

    private static Subject CreateTreeView()
    {
        var control = new TreeView();
        var entries = Labels.Select(label => new TreeViewItem { Header = label }).ToArray();

        foreach (var entry in entries)
        {
            control.Items.Add(entry);
        }

        return new Subject
        {
            Entries = entries,
            IsSelected = () => control.SelectedItem,
            SelectedCount = () => control.SelectedItem is null ? 0 : 1,
            SelectAt = index => control.SelectItem(entries[index]),
            RemoveAt = control.Items.RemoveAt,
            Repair = Repair.Drop,
            AutoSelectsFirstEntry = false
        };
    }

    private static Subject CreateTable()
    {
        var control = new Table();
        control.Columns.Add(TableColumn.Auto("Value"));
        var entries = Labels.Select(label => new TableRow([new ControlText(label)])).ToArray();

        foreach (var entry in entries)
        {
            control.Rows.Add(entry);
        }

        return new Subject
        {
            Entries = entries,
            // Table is the one control with no single SelectedItem - it owns a row list - so the
            // parity shape projects "the one selected row, or none".
            IsSelected = () => control.SelectedRows.Count == 1 ? control.SelectedRows[0] : null,
            SelectedCount = () => control.SelectedRows.Count,
            SelectAt = index => control.SelectRow(entries[index]),
            RemoveAt = control.Rows.RemoveAt,
            Repair = Repair.Drop,
            AutoSelectsFirstEntry = false
        };
    }
}
