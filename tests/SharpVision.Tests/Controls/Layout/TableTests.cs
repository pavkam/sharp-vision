// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

using SharpVision.Terminal.Input;

using BindingFlags = System.Reflection.BindingFlags;

/// <summary>Verifies table ownership, track geometry, headers, grid cells, and row validation.</summary>
public sealed class TableTests
{
    /// <summary>Verifies header and grid-line color overrides default to null and reject transparent values.</summary>
    [Fact]
    public void ColorProperties_WhenDefaultOrTransparentIsAssigned_UsesDocumentedDefaultsAndThrowsBeforeMutation()
    {
        // Arrange
        var table = new Table();

        // Assert defaults. Null still means "inherit the table's own resolved face" - the one
        // meaning no fixed ControlColor can express, which is why these three stayed nullable.
        table.ActualStyle.HeaderForeground.ShouldBeNull();
        table.ActualStyle.HeaderBackground.ShouldBeNull();
        table.ActualStyle.GridLineColor.ShouldBeNull();

        // Act and assert transparent rejection, now by the style's own init accessors
        _ = Should.Throw<ArgumentException>(
            () => table.Style = TableStyle.Default with { HeaderForeground = Color.Transparent });
        _ = Should.Throw<ArgumentException>(
            () => table.Style = TableStyle.Default with { GridLineColor = Color.Transparent });
        table.ActualStyle.HeaderForeground.ShouldBeNull();
        table.ActualStyle.GridLineColor.ShouldBeNull();

        // Act and assert HeaderBackground allows transparent, matching its existing unvalidated asymmetry.
        table.Style = TableStyle.Default with { HeaderBackground = Color.Transparent };
        table.ActualStyle.HeaderBackground.ShouldBe((ControlColor?) Color.Transparent);
    }

    /// <summary>Verifies header and grid-line color overrides accept a theme-role reference, not only a literal.</summary>
    [Fact]
    public void ColorProperties_WhenSetToThemeColor_RoundTripsTheToken()
    {
        // Arrange
        var table = new Table
        {
            Style = TableStyle.Default with
            {
                HeaderForeground = SemanticColor.Accent,
                HeaderBackground = SemanticColor.Surface,
                GridLineColor = SemanticColor.Muted
            }
        };

        // Assert
        table.ActualStyle.HeaderForeground.ShouldBe((ControlColor?) SemanticColor.Accent);
        table.ActualStyle.HeaderBackground.ShouldBe((ControlColor?) SemanticColor.Surface);
        table.ActualStyle.GridLineColor.ShouldBe((ControlColor?) SemanticColor.Muted);
    }

    /// <summary>Verifies the placeholder foreground colors default to the documented Muted/Error
    /// semantic roles and reject a transparent replacement before mutating the style (regression
    /// for the placeholder foreground previously being a hardcoded, non-theme-routed constant).</summary>
    [Fact]
    public void PlaceholderColors_WhenDefaultOrTransparentIsAssigned_UsesDocumentedDefaultsAndThrowsBeforeMutation()
    {
        // Arrange
        var table = new Table();

        // Assert defaults, unlike HeaderForeground/GridLineColor these are required non-nullable
        // members: a synthetic status row has no table face to inherit, so there is no null case.
        table.ActualStyle.PlaceholderForeground.ShouldBe((ControlColor) SemanticColor.Muted);
        table.ActualStyle.PlaceholderErrorForeground.ShouldBe((ControlColor) SemanticColor.Error);

        // Act and assert transparent rejection, by the style's own init accessors.
        _ = Should.Throw<ArgumentException>(
            () => table.Style = TableStyle.Default with { PlaceholderForeground = Color.Transparent });
        _ = Should.Throw<ArgumentException>(
            () => table.Style = TableStyle.Default with { PlaceholderErrorForeground = Color.Transparent });
        table.ActualStyle.PlaceholderForeground.ShouldBe((ControlColor) SemanticColor.Muted);
        table.ActualStyle.PlaceholderErrorForeground.ShouldBe((ControlColor) SemanticColor.Error);
    }

    /// <summary>Verifies the placeholder foreground colors accept a theme-role reference, not only a literal.</summary>
    [Fact]
    public void PlaceholderColors_WhenSetToThemeColor_RoundTripsTheToken()
    {
        // Arrange
        var table = new Table
        {
            Style = TableStyle.Default with
            {
                PlaceholderForeground = SemanticColor.Accent,
                PlaceholderErrorForeground = SemanticColor.Warning
            }
        };

        // Assert
        table.ActualStyle.PlaceholderForeground.ShouldBe((ControlColor) SemanticColor.Accent);
        table.ActualStyle.PlaceholderErrorForeground.ShouldBe((ControlColor) SemanticColor.Warning);
    }

    /// <summary>Verifies private rail local mechanics publish exact resolved-style notifications.</summary>
    [Fact]
    public void ScrollBarStyle_WhenOwnershipChanges_PublishesLocalAndActualNotifications()
    {
        var table = new Table();
        List<string?> notifications = [];
        table.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName is nameof(Table.ScrollBarStyle) or nameof(Table.ActualScrollBarStyle))
            {
                notifications.Add(eventArgs.PropertyName);
            }
        };
        table.ScrollBarStyle = ScrollBarStyle.ThinLine;
        table.ScrollBarStyle = null;
        notifications.ShouldBe([
            nameof(Table.ScrollBarStyle),
            nameof(Table.ActualScrollBarStyle),
            nameof(Table.ScrollBarStyle),
            nameof(Table.ActualScrollBarStyle)
        ]);
        notifications.Clear();

        // The framework's code-owned fallback (used while no theme is attached) resolves against
        // ThemeCatalog.Dark, not ThemeCatalog.White (per StyleDefinitions.Control), so switching to a
        // genuinely different theme is expected to change the resolved ActualScrollBarStyle and
        // publish exactly the one notification a value change produces - matching
        // ActualScrollBarStyleThemeTests, which explicitly asserts this same divergence.
        table.SetTheme(ThemeCatalog.White);
        notifications.ShouldBe([nameof(Table.ActualScrollBarStyle)]);
    }

    /// <summary>Verifies disabling a Table makes it ineligible for focus and re-enabling restores
    /// eligibility, matching the framework's generic disabled-input gate that the mounted evidence
    /// exercises end-to-end through the real hit-test and focus pipeline.</summary>
    [Fact]
    public void Enabled_WhenToggled_ChangesEffectiveIsEnabledAndFocusEligibility()
    {
        // Arrange
        using var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Rows.Add(new TableRow([new ControlText("One")]));
        table.CanFocus.ShouldBeTrue();

        // Act
        table.IsEnabled = false;

        // Assert
        table.EffectiveIsEnabled.ShouldBeFalse();
        table.CanFocus.ShouldBeFalse();

        // Act — re-enable recovery
        table.IsEnabled = true;

        // Assert
        table.EffectiveIsEnabled.ShouldBeTrue();
        table.CanFocus.ShouldBeTrue();
    }

    /// <summary>Verifies selecting rows/cells, moving the active cell, sorting, and editing each
    /// publish PropertyChanged for their own state properties - not only their domain event - so a
    /// generic binding fallback observes them instead of desyncing silently.</summary>
    [Fact]
    public void StateProperties_WhenCommitted_PublishPropertyChanged()
    {
        var first = new TableRow([new TextInput { Text = "A" }]);
        var second = new TableRow([new TextInput { Text = "B" }]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        var notifications = new List<string?>();
        table.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        table.SelectRow(first);

        notifications.ShouldContain(nameof(Table.SelectedRows));
        notifications.ShouldContain(nameof(Table.SelectedCells));
        notifications.ShouldContain(nameof(Table.ActiveRow));
        notifications.ShouldContain(nameof(Table.ActiveColumnIndex));
        notifications.ShouldContain(nameof(Table.ActiveCell));
        notifications.Clear();

        table.SelectRow(second, Modifiers.Control);

        notifications.ShouldContain(nameof(Table.ActiveRow));
        table.ActiveRow.ShouldBe(second);
        notifications.Clear();

        table.SetSort(0, TableSortDirection.Ascending);

        notifications.ShouldContain(nameof(Table.SortColumnIndex));
        notifications.ShouldContain(nameof(Table.SortDirection));
        notifications.Clear();

        table.ResetSort();

        notifications.ShouldContain(nameof(Table.SortColumnIndex));
        notifications.ShouldContain(nameof(Table.SortDirection));
        notifications.Clear();

        table.BeginEdit(first, 0).ShouldBeTrue();

        notifications.ShouldContain(nameof(Table.IsEditing));
        table.IsEditing.ShouldBeTrue();
        notifications.Clear();

        table.CommitEdit().ShouldBeTrue();

        notifications.ShouldContain(nameof(Table.IsEditing));
        table.IsEditing.ShouldBeFalse();
        notifications.Clear();

        table.BeginEdit(first, 0).ShouldBeTrue();
        notifications.Clear();
        table.CancelEdit().ShouldBeTrue();

        notifications.ShouldContain(nameof(Table.IsEditing));
        table.IsEditing.ShouldBeFalse();
    }

    /// <summary>Verifies the presenter-forwarding scroll properties publish PropertyChanged only
    /// when the committed value actually changes.</summary>
    [Fact]
    public void PresenterProperties_WhenChanged_PublishPropertyChangedOnce()
    {
        var table = new Table();
        var notifications = new List<string?>();
        table.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        table.ScrollBars = ScrollBars.Horizontal;
        table.ScrollBars = ScrollBars.Horizontal;
        table.ShowScrollBars = ShowScrollBars.Always;
        table.ShowScrollBars = ShowScrollBars.Always;
        table.LineSize = 3;
        table.LineSize = 3;
        table.PageOverlap = 2;
        table.PageOverlap = 2;

        notifications.Count(name => name == nameof(Table.ScrollBars)).ShouldBe(1);
        notifications.Count(name => name == nameof(Table.ShowScrollBars)).ShouldBe(1);
        notifications.Count(name => name == nameof(Table.LineSize)).ShouldBe(1);
        notifications.Count(name => name == nameof(Table.PageOverlap)).ShouldBe(1);
    }

    /// <summary>Verifies every table-declared property starts at its documented default.</summary>
    [Fact]
    public void Constructor_WhenCreated_UsesDocumentedDefaults()
    {
        var table = new Table();

        table.SelectionMode.ShouldBe(TableSelectionMode.Row);
        table.ShowHeader.ShouldBeTrue();
        table.ShowGridLines.ShouldBeTrue();
        table.Style.ShouldBeNull();
        table.RowSpacing.ShouldBe(0);
        table.ColumnSpacing.ShouldBe(0);
        table.ScrollBars.ShouldBe(ScrollBars.Vertical);
        table.ShowScrollBars.ShouldBe(ShowScrollBars.WhenNeeded);
        table.ScrollBarStyle.ShouldBeNull();
        table.LineSize.ShouldBe(1);
        table.PageOverlap.ShouldBe(0);
        table.HorizontalOffset.ShouldBe(0);
        table.VerticalOffset.ShouldBe(0);
        table.Extent.ShouldBe(default);
        table.Viewport.ShouldBe(default);
        table.ActiveRow.ShouldBeNull();
        table.ActiveColumnIndex.ShouldBe(-1);
        table.ActiveCell.ShouldBeNull();
        table.IsEditing.ShouldBeFalse();
        table.SortColumnIndex.ShouldBe(-1);
        table.SortDirection.ShouldBe(TableSortDirection.None);
        table.SelectedRows.ShouldBeEmpty();
        table.SelectedCells.ShouldBeEmpty();
        table.IsProgressive.ShouldBeFalse();
        table.LoadState.ShouldBe(TableLoadState.Idle);
        table.ActiveIndex.ShouldBe(-1);
        table.ActiveKey.ShouldBeNull();
        table.SelectedKeys.ShouldBeEmpty();
    }

    /// <summary>Verifies the progressive-only navigation members reject use before a data source
    /// is bound, directing callers to call <see cref="Table.SetDataSource{T}"/> first, and that
    /// the rejection leaves the table in its untouched eager default state.</summary>
    [Fact]
    public void ProgressiveOnlyMembers_WhenNotProgressive_ThrowInvalidOperationException()
    {
        var table = new Table();

        var reloadException = Should.Throw<InvalidOperationException>(table.Reload);
        var selectIndexException = Should.Throw<InvalidOperationException>(() => table.SelectIndex(0));
        var selectKeyException = Should.Throw<InvalidOperationException>(() => table.SelectKey("key"));

        reloadException.Message.ShouldContain("requires a progressive table");
        selectIndexException.Message.ShouldContain("requires a progressive table");
        selectKeyException.Message.ShouldContain("requires a progressive table");
        table.IsProgressive.ShouldBeFalse();
        table.ActiveIndex.ShouldBe(-1);
    }

    /// <summary>Verifies every table-declared setter with documented argument validation rejects
    /// an invalid value and leaves the property at its previous value.</summary>
    [Fact]
    public void Setter_WhenValueIsInvalid_ThrowsBeforeMutation()
    {
        var table = new Table();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.SelectionMode = (TableSelectionMode) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.RowSpacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.ColumnSpacing = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.ScrollBars = (ScrollBars) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.ShowScrollBars = (ShowScrollBars) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.LineSize = -1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.PageOverlap = -1);
        // A freshly constructed table has zero Extent, so any positive offset is out of range.
        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.HorizontalOffset = 1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.VerticalOffset = 1);

        table.SelectionMode.ShouldBe(TableSelectionMode.Row);
        table.RowSpacing.ShouldBe(0);
        table.ColumnSpacing.ShouldBe(0);
        table.ScrollBars.ShouldBe(ScrollBars.Vertical);
        table.ShowScrollBars.ShouldBe(ShowScrollBars.WhenNeeded);
        table.LineSize.ShouldBe(1);
        table.PageOverlap.ShouldBe(0);
        table.HorizontalOffset.ShouldBe(0);
        table.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies disposing the table prevents every table-declared property from mutating.</summary>
    [Fact]
    public void Dispose_WhenCalled_PreventsMutation()
    {
        var table = new Table();

        table.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => table.SelectionMode = TableSelectionMode.MultipleRows);
        _ = Should.Throw<ObjectDisposedException>(() => table.ShowHeader = false);
        _ = Should.Throw<ObjectDisposedException>(() => table.Style = TableStyle.Default);
        _ = Should.Throw<ObjectDisposedException>(() => table.RowSpacing = 1);
        _ = Should.Throw<ObjectDisposedException>(() => table.ColumnSpacing = 1);
        _ = Should.Throw<ObjectDisposedException>(() => table.ShowGridLines = false);
        _ = Should.Throw<ObjectDisposedException>(() => table.ScrollBars = ScrollBars.Both);
        _ = Should.Throw<ObjectDisposedException>(() => table.ShowScrollBars = ShowScrollBars.Always);
        _ = Should.Throw<ObjectDisposedException>(() => table.ScrollBarStyle = ScrollBarStyle.ThinLine);
        _ = Should.Throw<ObjectDisposedException>(() => table.LineSize = 2);
        _ = Should.Throw<ObjectDisposedException>(() => table.PageOverlap = 2);
        _ = Should.Throw<ObjectDisposedException>(() => table.HorizontalOffset = 0);
        _ = Should.Throw<ObjectDisposedException>(() => table.VerticalOffset = 0);
    }

    /// <summary>Verifies every table-declared setter requires dispatcher affinity once attached.</summary>
    [Fact]
    public async Task PropertySetter_WhenAttachedOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var table = new Table();
        await dispatcher.InvokeAsync(
            () => table.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => table.SelectionMode = TableSelectionMode.MultipleRows);
        _ = Should.Throw<InvalidOperationException>(() => table.ShowHeader = false);
        _ = Should.Throw<InvalidOperationException>(() => table.Style = TableStyle.Default);
        _ = Should.Throw<InvalidOperationException>(() => table.RowSpacing = 1);
        _ = Should.Throw<InvalidOperationException>(() => table.ColumnSpacing = 1);
        _ = Should.Throw<InvalidOperationException>(() => table.ShowGridLines = false);
        _ = Should.Throw<InvalidOperationException>(() => table.ScrollBars = ScrollBars.Both);
        _ = Should.Throw<InvalidOperationException>(() => table.ShowScrollBars = ShowScrollBars.Always);
        _ = Should.Throw<InvalidOperationException>(() => table.ScrollBarStyle = ScrollBarStyle.ThinLine);
        _ = Should.Throw<InvalidOperationException>(() => table.LineSize = 2);
        _ = Should.Throw<InvalidOperationException>(() => table.PageOverlap = 2);
        _ = Should.Throw<InvalidOperationException>(() => table.HorizontalOffset = 0);
        _ = Should.Throw<InvalidOperationException>(() => table.VerticalOffset = 0);

        table.SelectionMode.ShouldBe(TableSelectionMode.Row);
    }

    /// <summary>Verifies the row-mutation repair paths that move the active cell outside SetActive —
    /// removing the active row, replacing it, and cancelling an edit while rows disappear — publish
    /// PropertyChanged for the active-cell properties they commit.</summary>
    [Fact]
    public void ActiveCell_WhenRepairedByRowMutation_PublishesPropertyChanged()
    {
        var first = new TableRow([new TextInput { Text = "A" }]);
        var second = new TableRow([new TextInput { Text = "B" }]);
        var third = new TableRow([new TextInput { Text = "C" }]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.Rows.Add(third);
        table.SelectRow(first);
        var notifications = new List<string?>();
        table.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Removing the active row repairs the active cell to the next row.
        table.Rows.RemoveAt(0);

        table.ActiveRow.ShouldBe(second);
        notifications.ShouldContain(nameof(Table.ActiveRow));
        notifications.ShouldContain(nameof(Table.ActiveCell));
        notifications.Clear();

        // Replacing the active row moves the active cell to the replacement.
        var replacement = new TableRow([new TextInput { Text = "R" }]);
        table.Rows[0] = replacement;

        table.ActiveRow.ShouldBe(replacement);
        notifications.ShouldContain(nameof(Table.ActiveRow));
        notifications.ShouldContain(nameof(Table.ActiveCell));
        notifications.Clear();

        // Clearing rows while editing cancels the edit and resets the active cell to null.
        table.BeginEdit(replacement, 0).ShouldBeTrue();
        notifications.Clear();
        table.Rows.Clear();

        table.ActiveRow.ShouldBeNull();
        table.ActiveColumnIndex.ShouldBe(-1);
        notifications.ShouldContain(nameof(Table.IsEditing));
        notifications.ShouldContain(nameof(Table.ActiveRow));
        notifications.ShouldContain(nameof(Table.ActiveColumnIndex));
        notifications.ShouldContain(nameof(Table.ActiveCell));
    }

    /// <summary>Verifies the column-mutation sort remap publishes SortColumnIndex, and SetSort
    /// publishes each sort property only when its committed value actually changed.</summary>
    [Fact]
    public void SortProperties_WhenRemappedOrRepeated_PublishOnlyActualChanges()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("First"));
        table.Columns.Add(TableColumn.Auto("Sorted"));
        table.SetSort(1, TableSortDirection.Ascending);
        var notifications = new List<string?>();
        table.PropertyChanged += (_, eventArgs) => notifications.Add(eventArgs.PropertyName);

        // Inserting a column before the sorted one remaps SortColumnIndex 1 -> 2.
        table.Columns.Insert(0, TableColumn.Auto("Inserted"));

        table.SortColumnIndex.ShouldBe(2);
        notifications.ShouldContain(nameof(Table.SortColumnIndex));
        notifications.ShouldNotContain(nameof(Table.SortDirection));
        notifications.Clear();

        // Re-committing the identical sort publishes neither property.
        table.SetSort(2, TableSortDirection.Ascending);

        notifications.ShouldNotContain(nameof(Table.SortColumnIndex));
        notifications.ShouldNotContain(nameof(Table.SortDirection));

        // Changing only the direction publishes only the direction.
        table.SetSort(2, TableSortDirection.Descending);

        notifications.ShouldNotContain(nameof(Table.SortColumnIndex));
        notifications.ShouldContain(nameof(Table.SortDirection));
        notifications.Clear();

        // Resetting publishes both; a second reset publishes nothing further.
        table.ResetSort();

        notifications.ShouldContain(nameof(Table.SortColumnIndex));
        notifications.ShouldContain(nameof(Table.SortDirection));
        notifications.Clear();

        table.ResetSort();

        notifications.ShouldBeEmpty();
    }

    /// <summary>Verifies stable row selection, active cell tracking, clearing, and deterministic copy.</summary>
    [Fact]
    public void Selection_WhenRowsAndCellsAreSelected_TracksActiveStateAndCopiesTabSeparatedText()
    {
        var first = new TableRow([new ControlText("Alice"), new ControlText("Ready")]);
        var second = new TableRow([new ControlText("Bob"), new ControlText("Busy")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Columns.Add(TableColumn.Auto("State"));
        table.Rows.Add(first);
        table.Rows.Add(second);

        table.SelectRow(first);
        table.SelectRow(second, Modifiers.Control);

        table.SelectedRows.ShouldBe([first, second]);
        table.ActiveRow.ShouldBe(second);
        table.ActiveColumnIndex.ShouldBe(0);
        table.CopySelection().ShouldBe("Alice\tReady\nBob\tBusy");

        table.ClearSelection();

        table.SelectedRows.ShouldBeEmpty();
        table.SelectedCells.ShouldBeEmpty();
        table.CopySelection().ShouldBeEmpty();
    }

    /// <summary>Verifies arrow-key navigation moves the active cell and updates row selection to
    /// exactly the affected rows - covering the case where the active row changes and
    /// simultaneously becomes the sole selected row (Row selection mode collapses the previous
    /// selection the moment a new row becomes active). Table used to locate the active row via an
    /// O(rows) Rows.IndexOf scan and then repaint the entire table twice - once from
    /// CommitActiveCell, once more from CommitSelection - on every keystroke; this pins the final
    /// Current/Selected state per cell as identical to before that change, while only the actually
    /// affected rows' cells are exercised.</summary>
    [Fact]
    public void Navigate_WhenDownArrowMovesActiveRowUnderRowSelection_UpdatesExactlyTheAffectedCells()
    {
        var first = new TableRow([new ControlText("first"), new ControlText("1")]);
        var second = new TableRow([new ControlText("second"), new ControlText("2")]);
        var third = new TableRow([new ControlText("third"), new ControlText("3")]);
        var table = new Table { SelectionMode = TableSelectionMode.Row };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Columns.Add(TableColumn.Auto("Index"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.Rows.Add(third);
        table.SelectRow(first);

        var eventArgs = Key(table, Code.Down);

        eventArgs.IsHandled.ShouldBeTrue();
        table.ActiveRow.ShouldBeSameAs(second);
        table.ActiveColumnIndex.ShouldBe(0);
        table.SelectedRows.ShouldBe([second]);

        // The row that lost active and selected state.
        first.Cells[0].GetAppearanceState().HasFlag(VisualState.Current).ShouldBeFalse();
        first.Cells[0].GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeFalse();
        first.Cells[1].GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeFalse();

        // The row that gained active and selected state.
        second.Cells[0].GetAppearanceState().HasFlag(VisualState.Current).ShouldBeTrue();
        second.Cells[0].GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeTrue();
        second.Cells[1].GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeTrue();

        // The row never touched by the move.
        third.Cells[0].GetAppearanceState().HasFlag(VisualState.Current).ShouldBeFalse();
        third.Cells[0].GetAppearanceState().HasFlag(VisualState.Selected).ShouldBeFalse();
    }

    /// <summary>Verifies the row-index cache backing arrow-key navigation stays correct across row
    /// insertion, removal, and replacement interleaved with navigation - regression for the cache
    /// resolving a stale position after a mutation it should have invalidated or patched.</summary>
    [Fact]
    public void Navigate_WhenRowsMutateBetweenNavigations_ResolvesCorrectRowAfterEachMutation()
    {
        var first = new TableRow([new ControlText("first")]);
        var second = new TableRow([new ControlText("second")]);
        var third = new TableRow([new ControlText("third")]);
        var table = new Table { SelectionMode = TableSelectionMode.Row };
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.Rows.Add(third);
        table.SelectRow(first);

        // Warms the row-index cache against [first, second, third].
        _ = Key(table, Code.Down);
        table.ActiveRow.ShouldBeSameAs(second);

        // Inserting ahead of every cached row shifts every position by one; a cache that was not
        // invalidated would still report second at its old index.
        var inserted = new TableRow([new ControlText("inserted")]);
        table.Rows.Insert(0, inserted);

        _ = Key(table, Code.Down);

        table.ActiveRow.ShouldBeSameAs(third);

        // Removing a row ahead of the active row shifts every later position down by one; a stale
        // cache here would land back on the row navigation just left.
        table.Rows.RemoveAt(0);
        _ = Key(table, Code.Up);

        table.ActiveRow.ShouldBeSameAs(second);

        // Replacing the active row itself does not move any index, including its own new one -
        // the replacement takes over the same slot instead of the whole cache being discarded.
        var replacement = new TableRow([new ControlText("replacement")]);
        table.Rows[table.Rows.IndexOf(second)] = replacement;

        table.ActiveRow.ShouldBeSameAs(replacement);

        _ = Key(table, Code.Down);

        table.ActiveRow.ShouldBeSameAs(third);
    }

    /// <summary>Verifies published row and cell selection snapshots reject consumer mutation.</summary>
    [Fact]
    public void SelectionSnapshots_WhenConsumerAttemptsMutation_RejectTheChanges()
    {
        var row = new TableRow([new ControlText("value")]);
        var replacement = new TableRow([new ControlText("replacement")]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(row);
        table.SelectRow(row);

        var selectedRows = (IList<TableRow>) table.SelectedRows;

        _ = Should.Throw<NotSupportedException>(() => selectedRows[0] = replacement);
        selectedRows.ShouldBe([row]);

        table.SelectionMode = TableSelectionMode.MultipleCells;
        table.SelectCell(row, 0);
        var selectedCells = (IList<TableCellReference>) table.SelectedCells;

        _ = Should.Throw<NotSupportedException>(() => selectedCells[0] = new TableCellReference(replacement, 0));
        selectedCells.ShouldBe([new TableCellReference(row, 0)]);
    }

    /// <summary>Verifies cell selection and select-all preserve row and column order.</summary>
    [Fact]
    public void SelectAll_WhenCellSelectionIsActive_SelectsEveryCellInDeterministicOrder()
    {
        var first = new TableRow([new ControlText("A1"), new ControlText("B1")]);
        var second = new TableRow([new ControlText("A2"), new ControlText("B2")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleCells };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Rows.Add(first);
        table.Rows.Add(second);

        table.SelectAll();

        table.SelectedCells.ShouldBe([
            new TableCellReference(first, 0),
            new TableCellReference(first, 1),
            new TableCellReference(second, 0),
            new TableCellReference(second, 1)
        ]);
        table.CopySelection().ShouldBe("A1\tB1\nA2\tB2");
    }

    /// <summary>
    /// Verifies a Shift-extended cell-range selection spanning more than one row selects the same
    /// column band on every row (a rectangle), not the entire row for every row strictly between
    /// the anchor and the target. Only the anchor and target rows themselves were previously
    /// clamped to the anchor/target column; every interior row fell back to the whole row.
    /// </summary>
    [Fact]
    public void SelectCell_WhenShiftExtendedRangeSpansMultipleRows_SelectsOnlyTheColumnBand()
    {
        var rows = new[]
        {
            new TableRow([new ControlText("A0"), new ControlText("B0"), new ControlText("C0")]),
            new TableRow([new ControlText("A1"), new ControlText("B1"), new ControlText("C1")]),
            new TableRow([new ControlText("A2"), new ControlText("B2"), new ControlText("C2")]),
            new TableRow([new ControlText("A3"), new ControlText("B3"), new ControlText("C3")])
        };
        var table = new Table { SelectionMode = TableSelectionMode.MultipleCells };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Columns.Add(TableColumn.Auto("C"));

        foreach (var row in rows)
        {
            table.Rows.Add(row);
        }

        table.SelectCell(rows[0], 1);
        table.SelectCell(rows[3], 1, Modifiers.Shift);

        table.SelectedCells.ShouldBe(
        [
            new TableCellReference(rows[0], 1),
            new TableCellReference(rows[1], 1),
            new TableCellReference(rows[2], 1),
            new TableCellReference(rows[3], 1)
        ]);
    }

    /// <summary>
    /// Verifies a Shift range-select whose anchor points at a row that has left the table leaves
    /// the row or cell selection unchanged instead of clearing it. Table's own row-removal paths
    /// always repair a live anchor, so the anchor is forced stale via reflection here to pin the
    /// contract SelectionGesture&lt;TKey&gt;'s eligible-range delegate documents: returning null
    /// signals an unresolvable range and must leave the caller's selection untouched.
    /// </summary>
    [Fact]
    public void ShiftSelect_WhenAnchorRowIndexLookupFails_LeavesSelectionUnchanged()
    {
        var anchorField = typeof(Table).GetField(
            "_selectionAnchorRow", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var anchorColumnField = typeof(Table).GetField(
            "_selectionAnchorColumn", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var firstRow = new TableRow([new ControlText("A1")]);
        var secondRow = new TableRow([new ControlText("A2")]);
        var staleRow = new TableRow([new ControlText("Stale")]);
        var rowTable = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        rowTable.Columns.Add(TableColumn.Auto("A"));
        rowTable.Rows.Add(firstRow);
        rowTable.Rows.Add(secondRow);
        rowTable.SelectRow(firstRow);
        anchorField.SetValue(rowTable, staleRow);

        rowTable.SelectRow(secondRow, Modifiers.Shift);

        rowTable.SelectedRows.ShouldBe([firstRow]);

        var firstCellRow = new TableRow([new ControlText("A1"), new ControlText("B1")]);
        var secondCellRow = new TableRow([new ControlText("A2"), new ControlText("B2")]);
        var staleCellRow = new TableRow([new ControlText("X"), new ControlText("Y")]);
        var cellTable = new Table { SelectionMode = TableSelectionMode.MultipleCells };
        cellTable.Columns.Add(TableColumn.Auto("A"));
        cellTable.Columns.Add(TableColumn.Auto("B"));
        cellTable.Rows.Add(firstCellRow);
        cellTable.Rows.Add(secondCellRow);
        cellTable.SelectCell(firstCellRow, 0);
        anchorField.SetValue(cellTable, staleCellRow);
        anchorColumnField.SetValue(cellTable, 0);

        cellTable.SelectCell(secondCellRow, 1, Modifiers.Shift);

        cellTable.SelectedCells.ShouldBe([new TableCellReference(firstCellRow, 0)]);
    }

    /// <summary>Verifies SelectAll respects the single-selection Row mode instead of selecting
    /// every row, honoring the mode's own multiplicity contract.</summary>
    [Fact]
    public void SelectAll_WhenSelectionModeIsSingleRow_SelectsOnlyTheFirstRow()
    {
        var first = new TableRow([new ControlText("A1")]);
        var second = new TableRow([new ControlText("A2")]);
        var third = new TableRow([new ControlText("A3")]);
        var table = new Table { SelectionMode = TableSelectionMode.Row };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.Rows.Add(third);

        table.SelectAll();

        table.SelectedRows.Count.ShouldBe(1);
        table.SelectedRows.ShouldBe([first]);
    }

    /// <summary>Verifies SelectAll respects the single-selection Cell mode instead of selecting
    /// every cell.</summary>
    [Fact]
    public void SelectAll_WhenSelectionModeIsSingleCell_SelectsOnlyTheFirstCell()
    {
        var first = new TableRow([new ControlText("A1"), new ControlText("B1")]);
        var second = new TableRow([new ControlText("A2"), new ControlText("B2")]);
        var table = new Table { SelectionMode = TableSelectionMode.Cell };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Rows.Add(first);
        table.Rows.Add(second);

        table.SelectAll();

        table.SelectedCells.Count.ShouldBe(1);
        table.SelectedCells.ShouldBe([new TableCellReference(first, 0)]);
    }

    /// <summary>Verifies widening from Row to MultipleRows retains every selected row instead of
    /// clearing it - nothing about widening the mode invalidates an existing selection, matching
    /// ListView and TreeView.</summary>
    [Fact]
    public void SelectionMode_WhenWidenedFromRowToMultipleRows_RetainsSelection()
    {
        var first = new TableRow([new ControlText("A1")]);
        var second = new TableRow([new ControlText("A2")]);
        var table = new Table { SelectionMode = TableSelectionMode.Row };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.SelectRow(first);

        table.SelectionMode = TableSelectionMode.MultipleRows;

        table.SelectedRows.ShouldBe([first]);
    }

    /// <summary>Verifies narrowing from MultipleRows to Row keeps the first selected row in
    /// display order instead of clearing the whole selection.</summary>
    [Fact]
    public void SelectionMode_WhenNarrowedFromMultipleRowsToRow_KeepsFirstSelectedRow()
    {
        var first = new TableRow([new ControlText("A1")]);
        var second = new TableRow([new ControlText("A2")]);
        var third = new TableRow([new ControlText("A3")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.Rows.Add(third);
        table.SelectRow(second);
        table.SelectRow(third, Modifiers.Control);

        table.SelectionMode = TableSelectionMode.Row;

        table.SelectedRows.ShouldBe([second]);
    }

    /// <summary>Verifies widening from Cell to MultipleCells retains every selected cell.</summary>
    [Fact]
    public void SelectionMode_WhenWidenedFromCellToMultipleCells_RetainsSelection()
    {
        var first = new TableRow([new ControlText("A1"), new ControlText("B1")]);
        var table = new Table { SelectionMode = TableSelectionMode.Cell };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Rows.Add(first);
        table.SelectCell(first, 1);

        table.SelectionMode = TableSelectionMode.MultipleCells;

        table.SelectedCells.ShouldBe([new TableCellReference(first, 1)]);
    }

    /// <summary>Verifies narrowing from MultipleCells to Cell keeps the first selected cell in
    /// display (row-major) order.</summary>
    [Fact]
    public void SelectionMode_WhenNarrowedFromMultipleCellsToCell_KeepsFirstSelectedCell()
    {
        var first = new TableRow([new ControlText("A1"), new ControlText("B1")]);
        var second = new TableRow([new ControlText("A2"), new ControlText("B2")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleCells };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.SelectCell(first, 1);
        table.SelectCell(second, 0, Modifiers.Control);

        table.SelectionMode = TableSelectionMode.Cell;

        table.SelectedCells.ShouldBe([new TableCellReference(first, 1)]);
    }

    /// <summary>Verifies crossing the row/cell boundary always clears selection, since a selected
    /// row has no meaning as a cell selection and vice versa.</summary>
    [Fact]
    public void SelectionMode_WhenCrossingRowAndCellBoundary_ClearsSelection()
    {
        var first = new TableRow([new ControlText("A1"), new ControlText("B1")]);
        var table = new Table { SelectionMode = TableSelectionMode.Row };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Columns.Add(TableColumn.Auto("B"));
        table.Rows.Add(first);
        table.SelectRow(first);

        table.SelectionMode = TableSelectionMode.Cell;

        table.SelectedRows.ShouldBeEmpty();
        table.SelectedCells.ShouldBeEmpty();
    }

    /// <summary>Verifies moving to None still clears selection, the one transition that must
    /// always discard it.</summary>
    [Fact]
    public void SelectionMode_WhenChangedToNone_ClearsSelection()
    {
        var first = new TableRow([new ControlText("A1")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Rows.Add(first);
        table.SelectRow(first);

        table.SelectionMode = TableSelectionMode.None;

        table.SelectedRows.ShouldBeEmpty();
    }

    /// <summary>Verifies the single-row selection SelectAll produces stays correct after a sort
    /// and a row removal, rather than reverting to a multi-row selection.</summary>
    [Fact]
    public void SelectAll_WhenSelectionModeIsSingleRow_StaysSingleAfterSortAndRemoval()
    {
        var first = new TableRow([new ControlText("B")]);
        var second = new TableRow([new ControlText("A")]);
        var table = new Table { SelectionMode = TableSelectionMode.Row };
        table.Columns.Add(TableColumn.Auto("A"));
        table.Rows.Add(first);
        table.Rows.Add(second);

        table.SelectAll();
        table.SortBy(0);
        _ = table.Rows.Remove(second);

        table.SelectedRows.Count.ShouldBe(1);
    }

    /// <summary>Verifies sorting preserves row selection instead of silently clearing it — the
    /// reorder relocates the exact same row instances rather than removing and re-adding new
    /// ones, so selection referencing those instances must survive.</summary>
    [Fact]
    public void SortBy_WhenRowsAreSelected_PreservesSelection()
    {
        var first = new TableRow([new ControlText("B")]);
        var second = new TableRow([new ControlText("A")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.SelectRow(first);
        table.SelectRow(second, Modifiers.Control);

        table.SortBy(0);

        table.Rows.ShouldBe([second, first]);
        table.SelectedRows.ShouldBe([second, first]);
    }

    /// <summary>Verifies sorting does not cancel an in-progress edit on a row that survives the
    /// reorder — the row instance is only relocated, not removed.</summary>
    [Fact]
    public void SortBy_WhenRowIsBeingEdited_DoesNotCancelEdit()
    {
        var editor = new TextInput { Text = "one" };
        var other = new TableRow([new ControlText("two")]);
        var edited = new TableRow([editor]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(other);
        table.Rows.Add(edited);
        table.BeginEdit(edited, 0).ShouldBeTrue();
        editor.Text = "changed";

        table.SortBy(0);

        table.IsEditing.ShouldBeTrue();
        editor.Text.ShouldBe("changed");
    }

    /// <summary>Verifies a column's SortKey overrides the default cell-text comparison - a numeric
    /// key sorts "9" before "10" where the default ordinal text comparison would order "10" before
    /// "9".</summary>
    [Fact]
    public void SortBy_WhenColumnHasSortKey_OrdersByTheSelectedKeyInsteadOfCellText()
    {
        var nine = new TableRow([new ControlText("9")]);
        var ten = new TableRow([new ControlText("10")]);
        var table = new Table();
        table.Columns.Add(new TableColumn(
            "Value",
            Length.Auto,
            sortKey: cell => int.Parse(((ControlText) cell).Content, CultureInfo.InvariantCulture)));
        table.Rows.Add(ten);
        table.Rows.Add(nine);

        table.SortBy(0);

        table.Rows.ShouldBe([nine, ten]);
    }

    /// <summary>Verifies default text sorting is ordinal and stable across the direction cycle.</summary>
    [Fact]
    public void SortBy_WhenDefaultTextKeysAreComparedUnderCulture_PreservesOrdinalStableOrder()
    {
        var originalCulture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var first = new TableRow([new ControlText("z"), new ControlText("first")]);
            var second = new TableRow([new ControlText("same"), new ControlText("second")]);
            var third = new TableRow([new ControlText("same"), new ControlText("third")]);
            var fourth = new TableRow([new ControlText("ä"), new ControlText("fourth")]);
            var table = new Table();
            table.Columns.Add(TableColumn.Auto("Key"));
            table.Columns.Add(TableColumn.Auto("Value"));
            table.Rows.Add(first);
            table.Rows.Add(second);
            table.Rows.Add(third);
            table.Rows.Add(fourth);
            var changes = new List<(int Column, TableSortDirection Direction)>();
            table.SortChanged += (_, args) => changes.Add((args.ColumnIndex, args.Direction));

            table.SortBy(0);
            table.Rows.ShouldBe([second, third, first, fourth]);
            table.SortBy(0);
            table.Rows.ShouldBe([fourth, first, second, third]);
            table.SortBy(0);
            table.Rows.ShouldBe([first, second, third, fourth]);
            table.SortDirection.ShouldBe(TableSortDirection.None);
            changes.ShouldBe([
                (0, TableSortDirection.Ascending),
                (0, TableSortDirection.Descending),
                (-1, TableSortDirection.None)
            ]);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    /// <summary>Verifies a row inserted while sorted lands at its correct sorted position via
    /// binary-search splicing rather than a full resort, for every insertion point (before every
    /// existing row, after every existing row, and each position between).</summary>
    [Theory]
    [InlineData("0", 0)]
    [InlineData("15", 1)]
    [InlineData("25", 2)]
    [InlineData("35", 3)]
    [InlineData("99", 3)]
    public void Rows_WhenInsertedWhileSorted_LandsAtSortedPosition(string value, int expectedIndex)
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(new TableRow([new ControlText("10")]));
        table.Rows.Add(new TableRow([new ControlText("20")]));
        table.Rows.Add(new TableRow([new ControlText("30")]));
        table.SortBy(0);
        var changes = new List<(int Column, TableSortDirection Direction)>();
        table.SortChanged += (_, args) => changes.Add((args.ColumnIndex, args.Direction));

        var inserted = new TableRow([new ControlText(value)]);
        table.Rows.Add(inserted);

        var expected = new List<string> { "10", "20", "30" };
        expected.Insert(expectedIndex, value);
        table.Rows.IndexOf(inserted).ShouldBe(expectedIndex);
        table.Rows.Select(static row => ((ControlText) row.Cells[0]).Content).ShouldBe(expected);

        // Splicing an inserted row into the active sorted order does not itself change
        // SortColumnIndex or SortDirection, so it no longer raises SortChanged.
        changes.ShouldBeEmpty();
    }

    /// <summary>Verifies a no-op SetSort/ResetSort call - one that re-applies the column and
    /// direction already active - raises no SortChanged, even though the property setters
    /// beneath it are already correctly gated on real change. Reproduces a known repro:
    /// SetSort twice then ResetSort twice raises the event exactly twice, not four
    /// times, since only the first of each pair is a real change.</summary>
    [Fact]
    public void SetSort_WhenReapplyingTheCurrentColumnAndDirection_RaisesNoSortChanged()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(new TableRow([new ControlText("10")]));
        table.Rows.Add(new TableRow([new ControlText("20")]));
        var changes = new List<(int Column, TableSortDirection Direction)>();
        table.SortChanged += (_, args) => changes.Add((args.ColumnIndex, args.Direction));

        table.SetSort(0, TableSortDirection.Ascending);
        table.SetSort(0, TableSortDirection.Ascending);
        table.ResetSort();
        table.ResetSort();

        changes.ShouldBe([(0, TableSortDirection.Ascending), (-1, TableSortDirection.None)]);
    }

    /// <summary>Verifies inserting two equal-keyed rows while sorted places each new row after
    /// every existing row sharing its key, matching SetSort's own insertion-order tie-break.</summary>
    [Fact]
    public void Rows_WhenInsertedWithDuplicateKeyWhileSorted_SortsAfterExistingTies()
    {
        var first = new TableRow([new ControlText("dup")]);
        var second = new TableRow([new ControlText("dup")]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(first);
        table.SortBy(0);

        table.Rows.Add(second);

        table.Rows.ShouldBe([first, second]);
    }

    /// <summary>Verifies replacing a row while sorted re-splices only the replacement into its
    /// correct sorted position.</summary>
    [Fact]
    public void Rows_WhenReplacedWhileSorted_LandsAtSortedPosition()
    {
        var first = new TableRow([new ControlText("10")]);
        var second = new TableRow([new ControlText("20")]);
        var third = new TableRow([new ControlText("30")]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.Rows.Add(third);
        table.SortBy(0);

        var replacement = new TableRow([new ControlText("25")]);
        table.Rows[0] = replacement;

        table.Rows.ShouldBe([second, replacement, third]);
    }

    /// <summary>Verifies removing or replacing an edited row cancels and detaches its editor callback.</summary>
    [Fact]
    public void Rows_WhenEditedRowIsRemovedOrReplaced_CancelsBeforeOwnershipChanges()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        var removedEditor = new TextInput { Text = "removed" };
        var removedRow = new TableRow([removedEditor]);
        table.Rows.Add(removedRow);

        table.BeginEdit(removedRow, 0).ShouldBeTrue();
        removedEditor.Text = "changed";
        _ = table.Rows.Remove(removedRow);

        table.IsEditing.ShouldBeFalse();
        removedEditor.Text.ShouldBe("removed");
        Should.NotThrow(() => Key(removedEditor, Code.Enter));

        var replacedEditor = new TextInput { Text = "replaced" };
        var previousRow = new TableRow([replacedEditor]);
        var replacement = new TableRow([new TextInput { Text = "new" }]);
        table.Rows.Add(previousRow);

        table.BeginEdit(previousRow, 0).ShouldBeTrue();
        replacedEditor.Text = "changed";
        table.Rows[0] = replacement;

        table.IsEditing.ShouldBeFalse();
        replacedEditor.Text.ShouldBe("replaced");
        Should.NotThrow(() => Key(replacedEditor, Code.Enter));
        table.Rows.ShouldBe([replacement]);
    }

    /// <summary>Verifies replacing a selected row removes stale references and preserves a valid range anchor.</summary>
    [Fact]
    public void Rows_WhenSelectedRowIsReplaced_ClearsOldSelectionAndRepairsAnchor()
    {
        var previous = new TableRow([new ControlText("previous")]);
        var middle = new TableRow([new ControlText("middle")]);
        var last = new TableRow([new ControlText("last")]);
        var replacement = new TableRow([new ControlText("replacement")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(previous);
        table.Rows.Add(middle);
        table.Rows.Add(last);
        table.SelectRow(previous);
        TableSelectionChangedEventArgs? change = null;
        table.SelectionChanged += (_, args) => change = args;

        table.Rows[0] = replacement;

        table.SelectedRows.ShouldBeEmpty();
        var snapshot = change.ShouldNotBeNull();
        snapshot.RemovedRows.ShouldBe([previous]);
        table.SelectRow(last, Modifiers.Shift);
        table.SelectedRows.ShouldBe([replacement, middle, last]);
    }

    /// <summary>Verifies row removal publishes one selection change and leaves shift selection anchored safely.</summary>
    [Fact]
    public void Rows_WhenSelectedAnchorIsRemoved_PublishesOneChangeAndRepairsShiftSelection()
    {
        var first = new TableRow([new ControlText("first")]);
        var middle = new TableRow([new ControlText("middle")]);
        var last = new TableRow([new ControlText("last")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(first);
        table.Rows.Add(middle);
        table.Rows.Add(last);
        table.SelectRow(first);
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);

        table.Rows.RemoveAt(0);

        changes.Count.ShouldBe(1);
        changes[0].RemovedRows.ShouldBe([first]);
        table.SelectedRows.ShouldBeEmpty();
        table.SelectRow(last, Modifiers.Shift);
        table.SelectedRows.ShouldBe([middle, last]);
    }

    /// <summary>Verifies removing the selected row under single-row SelectionMode clears selection
    /// the same way MultipleRows already does, and repairs the active row to the row that slid into
    /// the vacated slot instead of leaving a stale reference to the detached row.</summary>
    [Fact]
    public void Rows_WhenSelectedRowIsRemovedUnderSingleRowMode_ClearsSelectionAndRepairsActiveRow()
    {
        var first = new TableRow([new ControlText("first")]);
        var second = new TableRow([new ControlText("second")]);
        var third = new TableRow([new ControlText("third")]);
        var fourth = new TableRow([new ControlText("fourth")]);
        var table = new Table { SelectionMode = TableSelectionMode.Row };
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.Rows.Add(third);
        table.Rows.Add(fourth);
        table.SelectRow(third);
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);

        table.Rows.Remove(third).ShouldBeTrue();

        changes.Count.ShouldBe(1);
        changes[0].RemovedRows.ShouldBe([third]);
        table.SelectedRows.ShouldBeEmpty();
        table.ActiveRow.ShouldBeSameAs(fourth);
        table.Rows.ShouldBe([first, second, fourth]);
    }

    /// <summary>Verifies clearing rows publishes one coherent selection change for all selected rows and cells.</summary>
    [Fact]
    public void Rows_WhenCleared_PublishesOneSelectionChangeAndClearsSelection()
    {
        var first = new TableRow([new ControlText("first"), new ControlText("one")]);
        var second = new TableRow([new ControlText("second"), new ControlText("two")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.SelectRow(first);
        table.SelectRow(second, Modifiers.Control);
        var changes = new List<TableSelectionChangedEventArgs>();
        table.SelectionChanged += (_, args) => changes.Add(args);

        table.Rows.Clear();

        changes.Count.ShouldBe(1);
        changes[0].RemovedRows.ShouldBe([first, second]);
        table.SelectedRows.ShouldBeEmpty();
        table.SelectedCells.ShouldBeEmpty();
        table.Rows.ShouldBeEmpty();
    }

    /// <summary>Verifies removing a sorted zero-row column resets sort before later row insertion.</summary>
    [Fact]
    public void Columns_WhenSortedColumnIsRemovedWithNoRows_ResetsSortBeforeRowsAreAdded()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("First"));
        table.Columns.Add(TableColumn.Auto("Second"));
        table.SetSort(1, TableSortDirection.Ascending);

        table.Columns.RemoveAt(1);

        table.SortColumnIndex.ShouldBe(-1);
        table.SortDirection.ShouldBe(TableSortDirection.None);
        Should.NotThrow(() => table.Rows.Add(new TableRow([new ControlText("value")])));
    }

    /// <summary>Verifies clearing columns resets sorting before later row insertion.</summary>
    [Fact]
    public void Columns_WhenClearedWhileSorted_ResetsSortBeforeRowsAreAdded()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("First"));
        table.Columns.Add(TableColumn.Auto("Second"));
        table.SetSort(1, TableSortDirection.Ascending);

        table.Columns.Clear();

        table.SortColumnIndex.ShouldBe(-1);
        table.SortDirection.ShouldBe(TableSortDirection.None);
        table.Columns.Add(TableColumn.Auto("Replacement"));
        Should.NotThrow(() => table.Rows.Add(new TableRow([new ControlText("value")])));
    }

    /// <summary>Verifies inserting before a sorted column preserves its identity and remaps its index.</summary>
    [Fact]
    public void Columns_WhenInsertedBeforeSortedColumn_PreservesSortIdentity()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("First"));
        table.Columns.Add(TableColumn.Auto("Sorted"));
        table.SetSort(1, TableSortDirection.Descending);

        table.Columns.Insert(0, TableColumn.Auto("Inserted"));

        table.SortColumnIndex.ShouldBe(2);
        table.SortDirection.ShouldBe(TableSortDirection.Descending);
        Should.NotThrow(() => table.Rows.Add(new TableRow([
            new ControlText("inserted"),
            new ControlText("first"),
            new ControlText("sorted")
        ])));
    }

    /// <summary>Verifies the public column collection rejects the invalid default value.</summary>
    [Fact]
    public void Columns_WhenDefaultValueIsAdded_RejectsMissingHeader()
    {
        var table = new Table();

        _ = Should.Throw<ArgumentException>(() => table.Columns.Add(default));
        table.Columns.ShouldBeEmpty();
    }

    /// <summary>Verifies the TableColumn constructor itself - not only Table's own re-validation -
    /// rejects an empty or whitespace header for a directly constructed value and for every
    /// convenience factory that forwards to it.</summary>
    [Fact]
    public void TableColumn_WhenHeaderIsEmptyOrWhitespace_ThrowsArgumentException()
    {
        _ = Should.Throw<ArgumentException>(() => new TableColumn(string.Empty, Length.Auto));
        _ = Should.Throw<ArgumentException>(() => new TableColumn("   ", Length.Auto));
        _ = Should.Throw<ArgumentException>(() => TableColumn.Auto(string.Empty));
        _ = Should.Throw<ArgumentException>(() => TableColumn.Fixed(string.Empty, 5));
        _ = Should.Throw<ArgumentException>(() => TableColumn.Percent(string.Empty, 50));
        _ = Should.Throw<ArgumentException>(() => TableColumn.Fill(string.Empty));
        _ = Should.Throw<ArgumentNullException>(() => new TableColumn(null!, Length.Auto));
    }

    /// <summary>Verifies each convenience factory produces the documented Width kind, and that the
    /// shared IsReadOnly/SortKey parameters flow through every factory identically to the
    /// constructor itself.</summary>
    [Fact]
    public void TableColumn_WhenCreatedThroughFactories_ProducesDocumentedWidthKindAndSharedOptions()
    {
        Func<ControlBase, IComparable?> key = static cell => ((ControlText) cell).Content;

        var auto = TableColumn.Auto("Auto", isReadOnly: true, sortKey: key);
        var fixedWidth = TableColumn.Fixed("Fixed", 7, isReadOnly: true, sortKey: key);
        var percent = TableColumn.Percent("Percent", 40, isReadOnly: true, sortKey: key);
        var fill = TableColumn.Fill("Fill", weight: 2, isReadOnly: true, sortKey: key);

        auto.Width.Kind.ShouldBe(LengthKind.Auto);
        fixedWidth.Width.ShouldBe(Length.Cells(7));
        percent.Width.ShouldBe(Length.Percent(40));
        fill.Width.ShouldBe(Length.Star(2));

        foreach (var column in new[] { auto, fixedWidth, percent, fill })
        {
            column.IsReadOnly.ShouldBeTrue();
            column.SortKey.ShouldBeSameAs(key);
        }

        // The default weight and IsReadOnly/SortKey values match the constructor's own defaults.
        var defaultFill = TableColumn.Fill("Default");
        defaultFill.Width.ShouldBe(Length.Star(1));
        defaultFill.IsReadOnly.ShouldBeFalse();
        defaultFill.SortKey.ShouldBeNull();
    }

    /// <summary>Verifies TableColumnCollection.Remove locates and removes an existing column,
    /// reports false for a column that is not present, and repairs sort identity exactly the way
    /// RemoveAt already does for a directly indexed removal.</summary>
    [Fact]
    public void Columns_WhenRemoved_ReturnsWhetherFoundAndRepairsSort()
    {
        var first = TableColumn.Auto("First");
        var sorted = TableColumn.Auto("Sorted");
        var absent = TableColumn.Auto("Absent");
        var table = new Table();
        table.Columns.Add(first);
        table.Columns.Add(sorted);
        table.SetSort(1, TableSortDirection.Ascending);

        table.Columns.Remove(absent).ShouldBeFalse();
        table.Columns.ShouldBe([first, sorted]);

        table.Columns.Remove(sorted).ShouldBeTrue();

        table.Columns.ShouldBe([first]);
        table.SortColumnIndex.ShouldBe(-1);
        table.SortDirection.ShouldBe(TableSortDirection.None);
    }

    /// <summary>Verifies selection event arguments retain an immutable snapshot of caller lists.</summary>
    [Fact]
    public void SelectionChangedEventArgs_WhenSourceListsMutate_RetainsOriginalSnapshot()
    {
        var row = new TableRow([new ControlText("value")]);
        var addedRows = new List<TableRow> { row };
        var addedCells = new List<TableCellReference> { new(row, 0) };
        var args = new TableSelectionChangedEventArgs(addedRows, [], addedCells, []);

        addedRows.Clear();
        addedCells.Clear();

        args.AddedRows.ShouldBe([row]);
        args.AddedCells.ShouldBe([new TableCellReference(row, 0)]);
        _ = Should.Throw<NotSupportedException>(() => ((IList<TableRow>) args.AddedRows).Clear());
        _ = Should.Throw<NotSupportedException>(() => ((IList<TableCellReference>) args.AddedCells).Clear());
    }

    /// <summary>Verifies TextInput editing commits, cancels, and rejects read-only columns.</summary>
    [Fact]
    public void Edit_WhenCellIsTextInput_CommitsAndCancelsWithoutOpeningReadOnlyColumn()
    {
        var editable = new TextInput { Text = "before" };
        var locked = new TextInput { Text = "locked" };
        var row = new TableRow([editable, locked]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Editable"));
        table.Columns.Add(TableColumn.Auto("Locked", isReadOnly: true));
        table.Rows.Add(row);

        table.BeginEdit(row, 0).ShouldBeTrue();
        editable.Text = "after";
        table.CommitEdit().ShouldBeTrue();
        editable.Text.ShouldBe("after");

        table.BeginEdit(row, 0).ShouldBeTrue();
        editable.Text = "discarded";
        table.CancelEdit().ShouldBeTrue();
        editable.Text.ShouldBe("after");
        table.BeginEdit(row, 1).ShouldBeFalse();
    }

    /// <summary>Verifies a synchronous focus callback may dispose the table without the edit path
    /// selecting or publishing a transaction against the obsolete editor afterward.</summary>
    [Fact]
    public async Task BeginEdit_WhenGotFocusDisposesTable_StopsEditContinuationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var editor = new TextInput { Text = "before" };
            var row = new TableRow([editor]);
            var table = new Table();
            table.Columns.Add(TableColumn.Auto("Editable"));
            table.Rows.Add(row);
            table.Attach(dispatcher);
            using FocusManager focus = new(table);
            editor.GotFocus += (_, _) => table.Dispose();

            table.BeginEdit(row, 0).ShouldBeFalse();

            table.IsDisposed.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies edit-completion observers may remove, clear, replace, or dispose the
    /// captured row without the completed transaction trying to activate that obsolete object.</summary>
    [Theory]
    [InlineData("remove", false)]
    [InlineData("clear", false)]
    [InlineData("replace", false)]
    [InlineData("dispose", false)]
    [InlineData("remove", true)]
    [InlineData("clear", true)]
    [InlineData("replace", true)]
    [InlineData("dispose", true)]
    public void CommitEdit_WhenCompletionCallbackInvalidatesRow_StopsAfterCommittedEdit(
        string mutation,
        bool submitWithEnter)
    {
        var editor = new TextInput { Text = "before" };
        var row = new TableRow([editor]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Editable"));
        table.Rows.Add(row);
        table.BeginEdit(row, 0).ShouldBeTrue();
        table.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName != nameof(Table.IsEditing) || table.IsEditing)
            {
                return;
            }

            switch (mutation)
            {
                case "remove":
                    _ = table.Rows.Remove(row);
                    break;
                case "clear":
                    table.Rows.Clear();
                    break;
                case "replace":
                    table.Rows[0] = new TableRow([new ControlText("replacement")]);
                    break;
                case "dispose":
                    editor.Dispose();
                    break;
                default:
                    throw new InvalidOperationException($"Unknown mutation '{mutation}'.");
            }
        };

        if (submitWithEnter)
        {
            var key = new KeyEventArgs(new Stroke(Code.Enter, default, 0, Modifiers.None, KeyAction.Press));
            _ = Should.NotThrow(() => Router.Route(editor, Events.Key, key));
        }
        else
        {
            table.CommitEdit().ShouldBeTrue();
        }

        table.IsEditing.ShouldBeFalse();
    }

    /// <summary>Verifies an incidental Control modifier on Enter does not activate the current row,
    /// and leaves the stroke unhandled so a shortcut bound to the modified combination still sees it.</summary>
    [Fact]
    public void Dispatch_WhenEnterHasControlModifier_DoesNotActivateAndLeavesUnhandled()
    {
        var row = new TableRow([new ControlText("value")]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(row);
        var invoked = new List<TableRow>();
        table.RowInvoked += (_, args) => invoked.Add(args.Row);

        var eventArgs = Key(table, Code.Enter, Modifiers.Control);

        eventArgs.IsHandled.ShouldBeFalse();
        invoked.ShouldBeEmpty();
    }

    /// <summary>Verifies plain Enter still activates the current row.</summary>
    [Fact]
    public void Dispatch_WhenEnterHasNoModifiers_StillActivates()
    {
        var row = new TableRow([new ControlText("value")]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(row);
        var invoked = new List<TableRow>();
        table.RowInvoked += (_, args) => invoked.Add(args.Row);

        var eventArgs = Key(table, Code.Enter);

        eventArgs.IsHandled.ShouldBeTrue();
        invoked.ShouldBe([row]);
    }

    /// <summary>Verifies held Enter does not invoke the active row more than once.</summary>
    [Fact]
    public void Dispatch_WhenEnterRepeats_InvokesOnlyForTheInitialKeyDown()
    {
        var row = new TableRow([new ControlText("value")]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(row);
        var invoked = new List<TableRow>();
        table.RowInvoked += (_, args) => invoked.Add(args.Row);

        _ = Key(table, Code.Enter);
        _ = Key(table, Code.Enter, action: KeyAction.Repeat);

        invoked.ShouldBe([row]);
    }

    /// <summary>Verifies a repeat after Enter commits an edit cannot reopen editing or invoke the
    /// row during the same physical key hold.</summary>
    [Fact]
    public void Dispatch_WhenEditCommitEnterRepeats_CommitsOnceWithoutReopeningOrInvoking()
    {
        // Arrange
        var input = new TextInput { Text = "Before" };
        var row = new TableRow([input]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(row);
        table.BeginEdit(row, 0).ShouldBeTrue();
        input.Text = "After";
        var invoked = 0;
        table.RowInvoked += (_, _) => invoked++;

        // Act
        _ = Key(table, Code.Enter);
        var repeat = Key(table, Code.Enter, action: KeyAction.Repeat);

        // Assert
        table.IsEditing.ShouldBeFalse();
        input.Text.ShouldBe("After");
        invoked.ShouldBe(0);
        repeat.IsHandled.ShouldBeTrue();
    }

    /// <summary>Verifies Shift-held Enter (a common terminal chord) still activates the current row.</summary>
    [Fact]
    public void Dispatch_WhenEnterHasShiftModifier_StillActivates()
    {
        var row = new TableRow([new ControlText("value")]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(row);
        var invoked = new List<TableRow>();
        table.RowInvoked += (_, args) => invoked.Add(args.Row);

        var eventArgs = Key(table, Code.Enter, Modifiers.Shift);

        eventArgs.IsHandled.ShouldBeTrue();
        invoked.ShouldBe([row]);
    }

    /// <summary>Verifies Enter still commits an in-progress cell edit even with an incidental Control
    /// modifier held - the edit-commit path is a deliberately ungated boundary, matching TextInput's
    /// own unconditional Enter-submits contract.</summary>
    [Fact]
    public void Dispatch_WhenEnterHasControlModifierDuringEdit_StillCommits()
    {
        var editable = new TextInput { Text = "before" };
        var row = new TableRow([editable]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(row);
        table.BeginEdit(row, 0).ShouldBeTrue();
        editable.Text = "after";

        var eventArgs = Key(table, Code.Enter, Modifiers.Control);

        eventArgs.IsHandled.ShouldBeTrue();
        table.IsEditing.ShouldBeFalse();
        editable.Text.ShouldBe("after");
    }

    /// <summary>Verifies an ancestor can suppress every Table preview default while a deliberate
    /// handled-events observer still receives the routed records.</summary>
    [Fact]
    public void Dispatch_WhenAncestorHandlesPreviewInput_DoesNotMutateTableButStillNotifiesHandledObserver()
    {
        // Arrange
        var first = new TableRow([new ControlText("Alpha")]);
        var second = new TableRow([new ControlText("Beta")]);
        var table = new Table { SelectionMode = TableSelectionMode.Row };
        table.Columns.Add(TableColumn.Fixed("Name", 10));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.SelectRow(first);
        var root = new Overlay { Children = { table } };
        new LayoutEngine().Layout(root, new Size(20, 8));
        var invoked = 0;
        var observedKeys = 0;
        var observedPointers = 0;
        table.RowInvoked += (_, _) => invoked++;
        _ = root.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview)
            {
                eventArgs.IsHandled = true;
            }
        });
        _ = root.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview)
            {
                eventArgs.IsHandled = true;
            }
        });
        _ = table.AddHandler(Events.Key, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview)
            {
                observedKeys++;
            }
        }, handledEventsToo: true);
        _ = table.AddHandler(Events.Pointer, (_, eventArgs) =>
        {
            if (eventArgs.Phase == RoutingPhase.Preview)
            {
                observedPointers++;
            }
        }, handledEventsToo: true);

        // Act - cover navigation, activation, header sorting, cell selection, and double press.
        _ = Router.Route(table, Events.Key, new KeyEventArgs(new Stroke(
            Code.Down,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
        _ = Router.Route(table, Events.Key, new KeyEventArgs(new Stroke(
            Code.Enter,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press)));
        _ = Router.Route(table, Events.Pointer, PointerPress(new Point(1, 0), clickCount: 1));
        _ = Router.Route(table, Events.Pointer, PointerPress(new Point(1, 3), clickCount: 1));
        _ = Router.Route(table, Events.Pointer, PointerPress(new Point(1, 3), clickCount: 2));

        // Assert
        table.ActiveRow.ShouldBe(first);
        table.SelectedRows.ShouldBe([first]);
        table.SortColumnIndex.ShouldBe(-1);
        table.SortDirection.ShouldBe(TableSortDirection.None);
        table.IsEditing.ShouldBeFalse();
        invoked.ShouldBe(0);
        observedKeys.ShouldBe(2);
        observedPointers.ShouldBe(3);
    }

    /// <summary>Verifies select-all normalizes character case and lock state but rejects larger
    /// application-command chords.</summary>
    [Theory]
    [InlineData('a', Modifiers.Control, true)]
    [InlineData('A', Modifiers.Control | Modifiers.CapsLock, true)]
    [InlineData('a', Modifiers.Control | Modifiers.NumLock, true)]
    [InlineData('A', Modifiers.Control | Modifiers.Shift, false)]
    [InlineData('a', Modifiers.Control | Modifiers.Alt, false)]
    [InlineData('a', Modifiers.Control | Modifiers.Super, false)]
    public void Dispatch_WhenSelectAllCharacterCarriesModifiers_MatchesExactNormalizedCommand(
        char character,
        Modifiers modifiers,
        bool expectedSelection)
    {
        // Arrange
        var first = new TableRow([new ControlText("First")]);
        var second = new TableRow([new ControlText("Second")]);
        var table = new Table { SelectionMode = TableSelectionMode.MultipleRows };
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(first);
        table.Rows.Add(second);
        table.SelectRow(first);
        var key = new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune(character),
            nativeCode: 0,
            modifiers,
            KeyAction.Press));

        // Act
        _ = Router.Route(table, Events.Key, key);

        // Assert
        table.SelectedRows.ShouldBe(expectedSelection ? [first, second] : [first]);
        key.IsHandled.ShouldBe(expectedSelection);
    }

    /// <summary>Verifies pointer selection revalidates exact row ownership after a synchronous
    /// selection callback removes, replaces, clears, reorders, or disposes the target.</summary>
    [Theory]
    [InlineData(TableSelectionMode.Row, "Remove", 1)]
    [InlineData(TableSelectionMode.Row, "Remove", 2)]
    [InlineData(TableSelectionMode.Row, "Replace", 1)]
    [InlineData(TableSelectionMode.Row, "Replace", 2)]
    [InlineData(TableSelectionMode.Row, "Clear", 1)]
    [InlineData(TableSelectionMode.Row, "Clear", 2)]
    [InlineData(TableSelectionMode.Row, "Reorder", 1)]
    [InlineData(TableSelectionMode.Row, "Reorder", 2)]
    [InlineData(TableSelectionMode.Row, "Dispose", 1)]
    [InlineData(TableSelectionMode.Row, "Dispose", 2)]
    [InlineData(TableSelectionMode.Cell, "Remove", 1)]
    [InlineData(TableSelectionMode.Cell, "Remove", 2)]
    [InlineData(TableSelectionMode.Cell, "Replace", 1)]
    [InlineData(TableSelectionMode.Cell, "Replace", 2)]
    [InlineData(TableSelectionMode.Cell, "Clear", 1)]
    [InlineData(TableSelectionMode.Cell, "Clear", 2)]
    [InlineData(TableSelectionMode.Cell, "Reorder", 1)]
    [InlineData(TableSelectionMode.Cell, "Reorder", 2)]
    [InlineData(TableSelectionMode.Cell, "Dispose", 1)]
    [InlineData(TableSelectionMode.Cell, "Dispose", 2)]
    public void Dispatch_WhenSelectionCallbackMutatesHitRow_ContinuesOnlyForSameOwnedIdentity(
        TableSelectionMode selectionMode,
        string mutation,
        int clickCount)
    {
        // Arrange
        var target = new TableRow([new TextInput { Text = "Target" }]);
        var remaining = new TableRow([new TextInput { Text = "Remaining" }]);
        var replacement = new TableRow([new TextInput { Text = "Replacement" }]);
        var table = new Table { SelectionMode = selectionMode };
        table.Columns.Add(TableColumn.Fixed("Name", 12));
        table.Rows.Add(target);
        table.Rows.Add(remaining);
        new LayoutEngine().Layout(table, new Size(16, 8));
        var mutated = false;
        var invocations = new List<TableRowInvokedEventArgs>();
        table.RowInvoked += (_, eventArgs) => invocations.Add(eventArgs);
        table.SelectionChanged += (_, _) =>
        {
            if (mutated)
            {
                return;
            }

            mutated = true;

            switch (mutation)
            {
                case "Remove":
                    _ = table.Rows.Remove(target);
                    break;
                case "Replace":
                    table.Rows[0] = replacement;
                    break;
                case "Clear":
                    table.Rows.Clear();
                    break;
                case "Reorder":
                    _ = table.Rows.Remove(target);
                    table.Rows.Add(target);
                    break;
                case "Dispose":
                    table.Dispose();
                    break;
                default:
                    throw new UnreachableException();
            }
        };
        var pointer = PointerPress(new Point(1, 2), clickCount);

        // Act and assert
        _ = Should.NotThrow(() => Router.Route(table, Events.Pointer, pointer));
        mutated.ShouldBeTrue();

        if (mutation == "Reorder")
        {
            var invoked = invocations.ShouldHaveSingleItem();
            invoked.Row.ShouldBeSameAs(target);
            invoked.RowIndex.ShouldBe(1);
            table.IsEditing.ShouldBe(clickCount == 2);
        }
        else
        {
            invocations.ShouldBeEmpty();

            if (!table.IsDisposed)
            {
                table.IsEditing.ShouldBeFalse();
            }
        }
    }

    private static void Key(TextInput control, Code code) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(code, null, nativeCode: 0, Modifiers.None, KeyAction.Press)));

    private static KeyEventArgs Key(
        Table table,
        Code code,
        Modifiers modifiers = Modifiers.None,
        KeyAction action = KeyAction.Press)
    {
        var eventArgs = new KeyEventArgs(new Stroke(code, null, nativeCode: 0, modifiers, action));
        _ = Router.Route(table, Events.Key, eventArgs);
        return eventArgs;
    }

    private static PointerEventArgs PointerPress(Point cells, int clickCount) => new(
        new Pointer(
            cells,
            pixels: null,
            Buttons.Primary,
            PointerAction.Press,
            wheelX: 0,
            wheelY: 0,
            Modifiers.None,
            isMotion: false,
            isCellPositionInferred: false),
        clickCount);

    /// <summary>Verifies every public row insertion boundary reports its own null parameter.</summary>
    [Fact]
    public void Rows_WhenNullIsInserted_ReportsPublicParameterName()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));

        var add = Should.Throw<ArgumentNullException>(() => table.Rows.Add(null!));
        var insert = Should.Throw<ArgumentNullException>(() => table.Rows.Insert(0, null!));

        add.ParamName.ShouldBe("item");
        insert.ParamName.ShouldBe("item");
        table.Rows.ShouldBeEmpty();
    }

    /// <summary>Verifies row replacement reports the public indexer value parameter before mutation.</summary>
    [Fact]
    public void Rows_WhenNullReplacesRow_ReportsValueParameterWithoutMutation()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        var original = new TableRow([new ControlText("Original")]);
        table.Rows.Add(original);

        var exception = Should.Throw<ArgumentNullException>(() => table.Rows[0] = null!);

        exception.ParamName.ShouldBe("value");
        table.Rows.ShouldBe([original]);
    }

    /// <summary>Verifies the non-structural IList query members reject a null row argument.</summary>
    [Fact]
    public void Rows_WhenQueryMemberReceivesNull_ThrowsArgumentNullException()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(new TableRow([new ControlText("Value")]));

        _ = Should.Throw<ArgumentNullException>(() => table.Rows.Contains(null!));
        _ = Should.Throw<ArgumentNullException>(() => table.Rows.IndexOf(null!));
        _ = Should.Throw<ArgumentNullException>(() => table.Rows.Remove(null!));
    }

    /// <summary>Verifies both owned collections reject a null CopyTo destination array.</summary>
    [Fact]
    public void ColumnsAndRows_WhenCopyToReceivesNullArray_ThrowsArgumentNullException()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));
        table.Rows.Add(new TableRow([new ControlText("Value")]));

        _ = Should.Throw<ArgumentNullException>(() => table.Columns.CopyTo(null!, 0));
        _ = Should.Throw<ArgumentNullException>(() => table.Rows.CopyTo(null!, 0));
    }

    /// <summary>Verifies fixed, percentage, and fill columns resolve exact contained cell slots.</summary>
    [Fact]
    public void Layout_WhenColumnsMixFixedPercentAndFill_ResolvesContainedCellSlots()
    {
        var first = new ControlText("Alpha");
        var second = new ControlText("Ready");
        var third = new ControlText("Details");
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Percent("Status", 50));
        table.Columns.Add(TableColumn.Fill("Details"));
        table.Rows.Add(new TableRow([first, second, third]));

        new LayoutEngine().Layout(table, new Size(20, 4));

        first.Bounds.ShouldBe(new Rect(0, 2, 5, 1));
        second.Bounds.ShouldBe(new Rect(6, 2, 5, 1));
        third.Bounds.ShouldBe(new Rect(16, 2, 4, 1));
        table.DesiredSize.ShouldBe(new Size(20, 3));
    }

    /// <summary>Verifies an ordinary interactive cell keeps its measured size inside a larger row slot.</summary>
    [Fact]
    public void Layout_WhenCellUsesIntrinsicAlignment_KeepsMeasuredBounds()
    {
        var option = new CheckBox
        {
            Text = "Include integration tests",
            VerticalAlignment = VerticalAlignment.Top
        };
        var table = new Table { Width = Length.Cells(48), Style = TableStyle.Default with { CellPadding = new Thickness(1, 0) } };
        table.Columns.Add(TableColumn.Fixed("Action", 16));
        table.Columns.Add(TableColumn.Fill("Configuration"));
        table.Rows.Add(new TableRow([
            new Button { Text = "Run checks" },
            option
        ]));

        new LayoutEngine().Layout(table, new Size(48, 8));

        option.Bounds.Width.ShouldBe(option.DesiredSize.Width);
        option.Bounds.Height.ShouldBe(option.DesiredSize.Height);
    }

    /// <summary>Verifies an explicitly stretched cell continues to consume its complete resolved track slot.</summary>
    [Fact]
    public void Layout_WhenCellExplicitlyStretches_FillsResolvedTrackSlot()
    {
        var option = new CheckBox
        {
            Text = "Option",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var table = new Table { Width = Length.Cells(20), ShowHeader = false, ShowGridLines = false };
        table.Columns.Add(TableColumn.Fixed("Action", 10));
        table.Columns.Add(TableColumn.Fixed("Choice", 10));
        table.Rows.Add(new TableRow([
            new Button { Text = "Run" },
            option
        ]));

        new LayoutEngine().Layout(table, new Size(20, 3));

        option.Bounds.ShouldBe(new Rect(10, 0, 10, 3));
    }

    /// <summary>Verifies horizontally scrolled headers, grid lines, row cells, hit testing, and rail chrome stay aligned.</summary>
    [Fact]
    public void Render_WhenHorizontallyScrolled_TranslatesCompleteTableContent()
    {
        var first = new ControlText("12345678");
        var table = new Table { ScrollBars = ScrollBars.Both };
        table.Columns.Add(TableColumn.Fixed("ABCDEFGH", 8));
        table.Columns.Add(TableColumn.Fixed("IJKLMNOP", 8));
        table.Rows.Add(new TableRow([first, new ControlText("abcdefgh")]));
        var size = new Size(10, 4);
        var engine = new LayoutEngine();
        engine.Layout(table, size);
        table.HorizontalOffset = 3;

        engine.Layout(table, size);
        using Frame frame = new(size);
        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("D");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("4");
        table.HitTest(new Point(0, 2)).ShouldBeSameAs(first);
        _ = table.HitTest(new Point(0, 3)).ShouldBeOfType<ScrollBar>();
    }

    /// <summary>Verifies simultaneous offsets may move the content origin above and left of the viewport.</summary>
    [Fact]
    public void Layout_WhenBothAxesScroll_AllowsSignedContentOrigin()
    {
        var table = new Table { ScrollBars = ScrollBars.Both };
        table.Columns.Add(TableColumn.Fixed("First", 8));
        table.Columns.Add(TableColumn.Fixed("Second", 8));

        for (var index = 0; index < 8; index++)
        {
            table.Rows.Add(new TableRow([
                new ControlText($"A{index}"),
                new ControlText($"B{index}")
            ]));
        }

        var engine = new LayoutEngine();
        var size = new Size(10, 5);
        engine.Layout(table, size);
        table.HorizontalOffset = 3;
        table.VerticalOffset = 3;

        engine.Layout(table, size);

        table.HorizontalOffset.ShouldBe(3);
        table.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies a pure scroll-origin arrangement neither remeasures cells nor remains invalidated.</summary>
    [Fact]
    public void Layout_WhenOnlyScrollOriginChanges_DoesNotRemeasureCellsOrRemainArrangeInvalidated()
    {
        var first = new ProbeControl(new Size(2, 1));
        var table = new Table { ScrollBars = ScrollBars.Both, ShowScrollBars = ShowScrollBars.Never };
        table.Columns.Add(TableColumn.Fixed("First", 8));
        table.Columns.Add(TableColumn.Fixed("Second", 8));
        table.Rows.Add(new TableRow([first, new ProbeControl(new Size(2, 1))]));
        var engine = new LayoutEngine();
        var size = new Size(10, 3);
        engine.Layout(table, size);
        var measurements = first.MeasureConstraints.Count;

        table.HorizontalOffset = 1;
        engine.Layout(table, size);

        first.MeasureConstraints.Count.ShouldBe(measurements);
        table.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies headers and light grid lines render around ordinary owned cell controls.</summary>
    [Fact]
    public void Render_WhenHeaderAndGridLinesAreEnabled_WritesHeaderCellsAndIntersections()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        var size = new Size(14, 4);
        new LayoutEngine().Layout(table, size);
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        FrameOracle.Get(frame, new Point(6, 0)).ShouldBe("V");
        FrameOracle.Get(frame, new Point(5, 0)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 1)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(0, 2)).ShouldBe("A");
        FrameOracle.Get(frame, new Point(6, 2)).ShouldBe("B");
    }

    /// <summary>Verifies an offset table keeps its header divider in the table's absolute coordinate space.</summary>
    [Fact]
    public void Render_WhenTableIsOffset_DrawsHeaderDividerBelowItsOwnHeader()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fill("Value"));
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        table.Measure(new Constraint(width: 14, height: 4));
        table.Arrange(new Rect(2, 3, 14, 4));
        using Frame frame = new(new Size(20, 10));

        table.Render(frame.Canvas);

        FrameOracle.Get(frame, new Point(2, 4)).ShouldNotBeEmpty();
        FrameOracle.Get(frame, new Point(2, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a header-only table has no phantom row gap or divider beneath its header.</summary>
    [Fact]
    public void Layout_WhenTableHasNoRows_UsesOnlyTheHeaderHeight()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Fixed("Name", 5));
        table.Columns.Add(TableColumn.Fixed("Value", 5));
        var size = new Size(12, 4);
        new LayoutEngine().Layout(table, size);
        using Frame frame = new(size);

        table.Render(frame.Canvas);

        table.DesiredSize.ShouldBe(new Size(11, 1));
        FrameOracle.Get(frame, new Point(0, 0)).ShouldBe("N");
        FrameOracle.Get(frame, new Point(0, 1)).ShouldBeEmpty();
    }

    /// <summary>Verifies a table taller than its viewport exposes vertical scroll via the intrinsic scroll surface.</summary>
    [Fact]
    public void Extent_WhenRowsExceedViewport_ExposesVerticalScroll()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Columns.Add(TableColumn.Fill("Value"));

        for (var index = 0; index < 40; index++)
        {
            table.Rows.Add(new TableRow([new ControlText($"Row {index}"), new ControlText("Value")]));
        }

        new LayoutEngine().Layout(table, new Size(30, 10));

        table.Extent.Height.ShouldBeGreaterThan(table.Viewport.Height);
    }

    /// <summary>Verifies ScrollBy moves the vertical offset by the requested delta, clamps at the
    /// committed extent instead of overshooting it, and reports false once further scrolling in
    /// the same direction is already pinned at that boundary.</summary>
    [Fact]
    public void ScrollBy_WhenDeltaIsRequested_MovesOffsetClampsAtExtentAndReportsChange()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));

        for (var index = 0; index < 40; index++)
        {
            table.Rows.Add(new TableRow([new ControlText($"Row {index}")]));
        }

        new LayoutEngine().Layout(table, new Size(20, 10));

        table.ScrollBy(0, 5).ShouldBeTrue();

        table.VerticalOffset.ShouldBe(5);

        var maximumOffset = table.Extent.Height - table.Viewport.Height;

        table.ScrollBy(0, int.MaxValue).ShouldBeTrue();

        table.VerticalOffset.ShouldBe(maximumOffset);

        // Already pinned at the maximum: a further positive delta is a genuine no-op.
        table.ScrollBy(0, 1).ShouldBeFalse();
        table.VerticalOffset.ShouldBe(maximumOffset);
    }

    /// <summary>Verifies ScrollBy rejects an undefined cause before mutating either committed
    /// offset.</summary>
    [Fact]
    public void ScrollBy_WhenCauseIsUndefined_ThrowsArgumentOutOfRangeExceptionBeforeMutation()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(new TableRow([new ControlText("Row")]));
        new LayoutEngine().Layout(table, new Size(20, 10));

        _ = Should.Throw<ArgumentOutOfRangeException>(() => table.ScrollBy(1, 1, (ScrollCause) 99));

        table.HorizontalOffset.ShouldBe(0);
        table.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies BringIntoView rejects a null descendant.</summary>
    [Fact]
    public void BringIntoView_WhenDescendantIsNull_ThrowsArgumentNullException() =>
        _ = Should.Throw<ArgumentNullException>(() => new Table().BringIntoView(null!));

    /// <summary>Verifies BringIntoView rejects a control that is not a realized cell owned by
    /// this table's private cell presenter.</summary>
    [Fact]
    public void BringIntoView_WhenDescendantIsNotARealizedCell_ThrowsArgumentException()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));
        table.Rows.Add(new TableRow([new ControlText("Row")]));
        var foreign = new ProbeControl();

        _ = Should.Throw<ArgumentException>(() => table.BringIntoView(foreign));

        table.VerticalOffset.ShouldBe(0);
    }

    /// <summary>Verifies BringIntoView scrolls the vertical offset just far enough to reveal a
    /// cell below the current viewport, returns true once fully revealed, and becomes a no-op
    /// once the cell is already visible.</summary>
    [Fact]
    public void BringIntoView_WhenCellIsBelowViewport_ScrollsMinimallyToRevealIt()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));
        TableRow target = null!;

        for (var index = 0; index < 40; index++)
        {
            var row = new TableRow([new ControlText($"Row {index}")]);
            table.Rows.Add(row);

            if (index == 30)
            {
                target = row;
            }
        }

        var engine = new LayoutEngine();
        var size = new Size(20, 10);
        engine.Layout(table, size);
        table.VerticalOffset.ShouldBe(0);

        table.BringIntoView(target.Cells[0]).ShouldBeTrue();

        table.VerticalOffset.ShouldBeGreaterThan(0);
        var revealedOffset = table.VerticalOffset;

        // A fresh layout pass is required for the cell's own arranged Bounds to reflect the
        // committed offset - scrolling alone only invalidates Render, not Arrange (see
        // Layout_WhenOnlyScrollOriginChanges_DoesNotRemeasureCellsOrRemainArrangeInvalidated).
        engine.Layout(table, size);

        // The cell is now already fully visible, so a repeated call is a genuine no-op: the
        // offset does not move again, and BringIntoView still reports success since the cell
        // ends up contained within the viewport either way.
        table.BringIntoView(target.Cells[0]).ShouldBeTrue();
        table.VerticalOffset.ShouldBe(revealedOffset);
    }

    /// <summary>
    /// Pins Table's actual Visibility contract for a cell: TablePresenter, TableRow, and
    /// TableRowCollection contain zero Visibility/EffectiveIsVisible references of their own
    /// (verified by inspection) - MeasureCells calls the ordinary MeasureChild on every cell
    /// unconditionally, with no per-cell Visibility branch anywhere in Table's own code. A
    /// Collapsed cell's zero contribution to its column's automatic width is therefore entirely
    /// ControlBase's own generic Measure/Arrange enforcement (Visibility == Collapsed forces
    /// DesiredSize to default before MeasureOverride/ArrangeOverride ever runs) flowing
    /// transparently through Table's real automatic column-sizing pipeline - not a Table-specific
    /// parent algorithm the way Stack's spacing or Dock's slot allocation are. This is an
    /// integration proof of that transparent pass-through, not a claim that Table owns any
    /// Visibility-aware logic. A whole TableRow has no Visibility property at all - hiding an
    /// entire row is structurally impossible short of removing it from Table.Rows.
    /// </summary>
    [Fact]
    public void MeasureCells_WhenACellIsCollapsed_ExcludesItsAutomaticWidthContribution()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Name"));
        var wide = new ControlText("A very long cell value indeed");
        table.Rows.Add(new TableRow([wide]));
        var engine = new LayoutEngine();
        var size = new Size(60, 4);
        engine.Layout(table, size);
        var baselineWidth = table.DesiredSize.Width;

        wide.Visibility = Visibility.Collapsed;
        engine.Layout(table, size);

        // The column shrinks to fit only the "Name" header once its sole cell no longer
        // contributes any width - proving the generic leaf mechanics reach through unmodified.
        table.DesiredSize.Width.ShouldBeLessThan(baselineWidth);
        wide.DesiredSize.ShouldBe(default);
        wide.Bounds.ShouldBe(default);
    }

    /// <summary>Verifies a row must match the complete column count before any cells are attached.</summary>
    [Fact]
    public void Rows_WhenCellCountDiffersFromColumns_RejectsRowWithoutOwnershipTransfer()
    {
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("One"));
        table.Columns.Add(TableColumn.Auto("Two"));
        var cell = new ControlText("Only one");
        var row = new TableRow([cell]);

        _ = Should.Throw<ArgumentException>(() => table.Rows.Add(row));

        table.Rows.Count.ShouldBe(0);
        table.GetType().GetProperty("Children").ShouldBeNull();
        cell.Parent.ShouldBeNull();
    }

    /// <summary>Verifies column list operations preserve row shape before committing mutation.</summary>
    [Fact]
    public void Columns_WhenRowsExist_RejectCountChangesBeforeMutation()
    {
        // Arrange
        var name = TableColumn.Auto("Name");
        var value = TableColumn.Fill("Value");
        var replacement = TableColumn.Fixed("Replacement", 8);
        var table = new Table();
        table.Columns.Add(name);
        table.Columns.Insert(1, value);
        table.Rows.Add(new TableRow([new ControlText("A"), new ControlText("B")]));
        var copy = new TableColumn[2];

        // Act and assert the non-structural IList surface
        table.Columns.CopyTo(copy, 0);
        copy.ShouldBe([name, value]);
        table.Columns.Contains(value).ShouldBeTrue();
        table.Columns.IndexOf(value).ShouldBe(1);
        table.Columns[1] = replacement;
        table.Columns[1].ShouldBe(replacement);

        // Act and assert rejected structural changes
        _ = Should.Throw<ArgumentException>(() => table.Columns.Add(value));
        _ = Should.Throw<ArgumentException>(() => table.Columns.Insert(0, value));
        _ = Should.Throw<ArgumentException>(() => table.Columns.RemoveAt(0));
        _ = Should.Throw<ArgumentException>(table.Columns.Clear);

        // Assert validation happened before mutation
        table.Columns.ShouldBe([name, replacement]);
    }

    /// <summary>Verifies row list operations atomically transfer every cell's retained ownership.</summary>
    [Fact]
    public void Rows_WhenListIsMutated_TransfersAndReleasesCellOwnership()
    {
        // Arrange
        var firstCell = new ControlText("First");
        var secondCell = new ControlText("Second");
        var replacementCell = new ControlText("Replacement");
        var first = new TableRow([firstCell]);
        var second = new TableRow([secondCell]);
        var replacement = new TableRow([replacementCell]);
        var table = new Table();
        table.Columns.Add(TableColumn.Auto("Value"));

        // Act insertion and inspection
        table.Rows.Add(second);
        table.Rows.Insert(0, first);
        var copy = new TableRow[2];
        table.Rows.CopyTo(copy, 0);

        // Assert attached list contract
        copy.ShouldBe([first, second]);
        table.Rows.Contains(second).ShouldBeTrue();
        table.Rows.IndexOf(second).ShouldBe(1);
        _ = firstCell.Parent.ShouldNotBeNull();
        _ = secondCell.Parent.ShouldNotBeNull();

        // Act replacement, removal, and clear
        table.Rows[0] = replacement;
        table.Rows.Remove(second).ShouldBeTrue();
        table.Rows.Remove(second).ShouldBeFalse();
        table.Rows.Clear();

        // Assert every former cell is detached
        table.Rows.ShouldBeEmpty();
        firstCell.Parent.ShouldBeNull();
        secondCell.Parent.ShouldBeNull();
        replacementCell.Parent.ShouldBeNull();
    }


    /// <summary>Verifies Percent columns resolve against the axis left after ColumnSpacing, not the
    /// full one. Over 40 cells with a 4-cell gap the two halves are 18 each, not the 20 they would
    /// be if the table resolved percentages against the incoming axis the way Stack does.</summary>
    [Fact]
    public void Layout_WhenPercentColumnsHaveColumnSpacing_ResolveAgainstTheSpacingReducedAxis()
    {
        var (table, first, second) = CreateTable(columnSpacing: 4, showGridLines: false);

        new LayoutEngine().Layout(table, new Size(40, 4));

        // available = 40 - 4 = 36; cumulative edges at 50% and 100% of 36 give 18 and 36.
        first.Bounds.Width.ShouldBe(18);
        second.Bounds.Width.ShouldBe(18);
        second.Bounds.X.ShouldBe(22);
        ShouldExactlyTile(first, second, gap: 4, axis: 40);
    }

    /// <summary>Verifies grid lines reserve an axis gap of their own with no ColumnSpacing set, and
    /// that the odd remainder tiles rather than being lost: 39 cells split 20 and 19.</summary>
    [Fact]
    public void Layout_WhenPercentColumnsShowGridLines_ReserveTheGridLineGap()
    {
        var (table, first, second) = CreateTable(columnSpacing: 0, showGridLines: true);

        new LayoutEngine().Layout(table, new Size(40, 4));

        // available = 40 - 1 = 39; RoundPercent is AwayFromZero, so the 19.5 edge lands on 20 and
        // the second column takes the rest of the axis rather than rounding independently.
        first.Bounds.Width.ShouldBe(20);
        second.Bounds.Width.ShouldBe(19);
        second.Bounds.X.ShouldBe(21);
        ShouldExactlyTile(first, second, gap: 1, axis: 40);
    }

    /// <summary>The distinguishing case. With both set, the gap is the larger of the two and not
    /// their sum, so 37 cells remain rather than 36 - which is what separates a correct
    /// <c>max</c> from a plausible-looking <c>+</c>.</summary>
    [Fact]
    public void Layout_WhenPercentColumnsHaveBothSpacingAndGridLines_ReserveTheLargerGapNotTheSum()
    {
        var (table, first, second) = CreateTable(columnSpacing: 3, showGridLines: true);

        new LayoutEngine().Layout(table, new Size(40, 4));

        // available = 40 - max(3, 1) = 37, not 40 - (3 + 1) = 36. Summing would give 18 and 18.
        first.Bounds.Width.ShouldBe(19);
        second.Bounds.Width.ShouldBe(18);
        second.Bounds.X.ShouldBe(22);
        ShouldExactlyTile(first, second, gap: 3, axis: 40);
    }

    /// <summary>Pins the divergence itself. Identical Percent(50) participants and identical
    /// spacing over an identical axis give the table narrower columns than Stack, because only
    /// Stack resolves percentages against the pre-spacing axis. This is intentional; the point of
    /// asserting it is that changing either side becomes a deliberate act.</summary>
    [Fact]
    public void Layout_WhenTableAndStackShareParticipantsAndSpacing_TablePercentsAreNarrower()
    {
        var (table, tableFirst, _) = CreateTable(columnSpacing: 4, showGridLines: false);
        var stackFirst = new ControlText("Alpha") { Width = Length.Percent(50), HorizontalAlignment = HorizontalAlignment.Stretch };
        var stackSecond = new ControlText("Ready") { Width = Length.Percent(50), HorizontalAlignment = HorizontalAlignment.Stretch };
        var stack = new Stack
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            Children = { stackFirst, stackSecond }
        };

        new LayoutEngine().Layout(table, new Size(40, 4));
        new LayoutEngine().Layout(stack, new Size(40, 4));

        tableFirst.Bounds.Width.ShouldBe(18, "the table resolves 50% of the 36 cells left after spacing");
        stackFirst.Bounds.Width.ShouldBe(20, "Stack resolves 50% of the full 40-cell axis");
        tableFirst.Bounds.Width.ShouldBeLessThan(stackFirst.Bounds.Width);
    }

    // Every cell of the axis is either a column or the single gap between them - no cell is
    // double-counted and none is dropped, which a per-column rounding bug would break even when the
    // individual widths still looked plausible.
    private static void ShouldExactlyTile(ControlBase first, ControlBase second, int gap, int axis)
    {
        second.Bounds.X.ShouldBe(first.Bounds.X + first.Bounds.Width + gap);
        (first.Bounds.Width + gap + second.Bounds.Width).ShouldBe(axis);
    }

    // ShowHeader and CellPadding are switched off so the assertions above read the column
    // allocation itself rather than a header row or a deflated content box.
    private static (Table Table, ControlText First, ControlText Second) CreateTable(
        int columnSpacing,
        bool showGridLines)
    {
        // Stretched so each cell's arranged width IS its column's width; a content-sized cell would
        // report its own text length instead and the assertions would stop reading the allocation.
        var first = new ControlText("Alpha") { HorizontalAlignment = HorizontalAlignment.Stretch };
        var second = new ControlText("Ready") { HorizontalAlignment = HorizontalAlignment.Stretch };
        var table = new Table
        {
            ShowHeader = false,
            Style = TableStyle.Default with { CellPadding = default },
            ColumnSpacing = columnSpacing,
            ShowGridLines = showGridLines
        };

        table.Columns.Add(TableColumn.Percent("Name", 50));
        table.Columns.Add(TableColumn.Percent("Status", 50));
        table.Rows.Add(new TableRow([first, second]));
        return (table, first, second);
    }
}
