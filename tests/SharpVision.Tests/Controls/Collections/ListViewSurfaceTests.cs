// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

/// <summary>Verifies ListView selection, focus, invocation, modifiers, scrolling, mutation, and cells through mounted surfaces.</summary>
public sealed class ListViewSurfaceTests
{
    /// <summary>Verifies default idle rows compose over one continuous borderless ListView background.</summary>
    [Fact]
    public async Task Render_WhenDefaultChromeIsUsed_DrawsBorderlessSurfaceBehindIdleRowsAsync()
    {
        // Arrange
        var list = new UiListView
        {
            Items = ["One", "Two", "Three"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        // Act
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(10, 5),
            TestContext.Current.CancellationToken);

        // Assert
        surface.ShouldRender("""
                             One
                             Two
                             Three


                             """);
        surface.Cell(default).Style.Background.ShouldBe(
            ThemeColorHelper.Background(list.Theme.ShouldNotBeNull()));
    }

    /// <summary>Verifies keyboard selection highlights the complete active row while the ListView owns focus.</summary>
    [Fact]
    public async Task Keyboard_WhenFocusedSelectionChanges_HighlightsTheCompleteSelectedRowAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "Two"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        var selectionBackground = ThemeColorHelper.SelectionBackground(list.Theme.ShouldNotBeNull());

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        surface.ShouldHaveFocus(list);
        list.SelectedIndex.ShouldBe(1);
        var selectedRow = realized[1].Parent.ShouldNotBeNull();
        (selectedRow.GetAppearanceState() & VisualState.Selected).ShouldBe(VisualState.Selected);
        selectedRow.ActualFace.Background.Literal.ShouldBe(selectionBackground);
        for (var x = selectedRow.Bounds.X; x < selectedRow.Bounds.Right; x++)
        {
            surface.Cell(new Point(x, selectedRow.Bounds.Y)).Style.Background.ShouldBe(selectionBackground);
        }
    }

    /// <summary>Verifies the current row renders an underline cue independent of both theme
    /// authoring and real keyboard focus - the mechanism a ComboBox's owned list (and any other
    /// browse-before-commit consumer) relies on, since it deliberately keeps real focus on the
    /// owning field and drives Current purely through <c>MoveCurrent</c> while never selecting.
    /// SelectionMode.None isolates Current from Selected: keyboard navigation here can only ever
    /// move Current, so it proves the cue does not depend on selection either.</summary>
    [Fact]
    public async Task Keyboard_WhenCurrentMovesWithoutSelection_UnderlinesOnlyTheCurrentRowAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "Two"],
            SelectionMode = ListSelectionMode.None,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        var firstRow = realized[0].Parent.ShouldNotBeNull();
        var secondRow = realized[1].Parent.ShouldNotBeNull();

        // Act
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        list.SelectedIndex.ShouldBe(-1, "SelectionMode.None never selects; only Current moved");
        list.ActiveIndex.ShouldBe(1);
        (surface.Cell(new Point(secondRow.Bounds.X, secondRow.Bounds.Y)).Style.Attributes &
            TerminalAttributes.Underline).ShouldBe(TerminalAttributes.Underline);
        (surface.Cell(new Point(firstRow.Bounds.X, firstRow.Bounds.Y)).Style.Attributes &
            TerminalAttributes.Underline).ShouldBe(TerminalAttributes.None);
    }

    /// <summary>Verifies a hovered row changes foreground without covering the ListView background.</summary>
    [Fact]
    public async Task Pointer_WhenMovedOverItem_PreservesListBackgroundAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "Two", "Three"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(10, 5),
            TestContext.Current.CancellationToken);
        var target = realized[1].Parent.ShouldNotBeNull();
        var theme = list.Theme.ShouldNotBeNull();
        var listBackground = ThemeColorHelper.Background(theme);
        var hoveredForeground = ThemeColorHelper.HoveredForeground(theme);

        // Act
        await surface.Pointer.MoveToAsync(target);

        // Assert
        list.IsPointerOver.ShouldBeTrue();
        target.IsPointerOver.ShouldBeTrue();
        target.GetResolvedAppearance(target.GetAppearanceState()).BackgroundMode.ShouldBe(BackgroundMode.Opaque);
        var targetOrigin = new Point(target.Bounds.X, target.Bounds.Y);
        surface.Cell(targetOrigin).Style.Background.ShouldBe(listBackground);
        surface.Cell(targetOrigin).Style.Foreground.ShouldBe(
            TerminalPalette.Project(hoveredForeground, ColorDepth.Basic16));

        // Pin the mechanism, not just the pixels: the templated caption is transparent, so it
        // inherits the hovered item's state-ambient face rather than resolving its own normal.
        realized[1].ActualFace.Foreground.Literal.ShouldBe(
            theme.ResolveColor(SemanticColor.ActiveText));
    }

    /// <summary>Verifies a disabled realized item remains visible but cannot select or invoke.</summary>
    [Fact]
    public async Task Pointer_WhenItemIsDisabled_DoesNotSelectOrInvokeAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var invoked = 0;
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["Enabled", "Disabled"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        realized[1].IsEnabled = false;
        list.ItemInvoked += (_, _) => invoked++;
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(10, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(realized[1].Parent.ShouldNotBeNull());

        // Assert
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
        invoked.ShouldBe(0);
        surface.ShouldRender("""
                             Enabled
                             Disabled
                             """);
    }

    /// <summary>Verifies collapsing the active row's own content eagerly repairs ActiveIndex and
    /// drops that row from selection, and the rendered output reflows the remaining rows into its
    /// place - matching removal's own rendered repair rather than leaving a stale gap.</summary>
    [Fact]
    public async Task Visibility_WhenActiveRowContentCollapses_RepairsActiveIndexAndRendersRemainingRowsAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "Two", "Three"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(10, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Home);
        list.SelectedIndex.ShouldBe(0);

        // Act
        await surface.UpdateAsync(() => realized[0].Visibility = Visibility.Collapsed, "collapse active row content");

        // Assert
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(1);
        list.Items[list.ActiveIndex].ShouldBe("Two");
        surface.ShouldRender("""
                             Two
                             Three

                             """);
    }

    /// <summary>Verifies an empty ListView renders its background without a phantom item or active row.</summary>
    [Fact]
    public async Task Render_WhenItemsAreEmpty_ShowsOnlyEmptySurfaceAsync()
    {
        // Arrange and act
        var list = new UiListView
        {
            Items = [],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Assert
        list.Items.ShouldBeEmpty();
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
        surface.ShouldRender("""


                             """);
    }

    /// <summary>Verifies pointer and keyboard paths commit selection, focus, invocation, Unicode
    /// appearance, and the full disabled contract: direct and ancestor-inherited disabled state,
    /// stable geometry across a genuine resize, and re-enable recovery.</summary>
    [Fact]
    public async Task Input_WhenPointerAndKeyboardNavigate_SelectsAndInvokesExactRowsAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var selectionOrder = new List<string>();
        var invoked = new List<(int Index, ActivationCause Cause)>();
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "界", "Three"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.SelectionChanging += (_, eventArgs) =>
            selectionOrder.Add($"changing:{Join(eventArgs.AddedIndexes)}:{Join(eventArgs.RemovedIndexes)}");
        list.SelectionChanged += (_, eventArgs) =>
            selectionOrder.Add($"changed:{Join(eventArgs.AddedIndexes)}:{Join(eventArgs.RemovedIndexes)}");
        list.ItemInvoked += (_, eventArgs) => invoked.Add((eventArgs.Index, eventArgs.Cause));
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        surface.ShouldRender("""
                             One
                             界
                             Three
                             """);
        list.SelectedIndex.ShouldBe(-1);

        // Act focus traversal
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert focus entry
        surface.ShouldHaveState(list, VisualState.Focused);

        // Act pointer
        await surface.Pointer.ClickAsync(realized[1].Parent.ShouldNotBeNull());

        // Assert pointer
        list.SelectedIndex.ShouldBe(1);
        list.ActiveIndex.ShouldBe(1);
        surface.ShouldHaveState(list, VisualState.IsPointerOver | VisualState.Focused);
        surface.ShouldHaveState(realized[1].Parent.ShouldNotBeNull(), VisualState.IsPointerOver);
        (realized[1].Parent.ShouldNotBeNull().GetAppearanceState() & VisualState.Selected)
            .ShouldBe(VisualState.Selected);
        surface.Cell(new Point(1, 1)).Continuation.ShouldBeTrue();
        invoked.ShouldBe([(1, ActivationCause.Pointer)]);

        // Act keyboard
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert navigation selection
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);

        // Act keyboard activation and navigation
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.End);

        // Assert keyboard
        list.SelectedIndex.ShouldBe(2);
        list.ActiveIndex.ShouldBe(2);
        surface.ShouldHaveState(list, VisualState.IsPointerOver | VisualState.Focused);
        (realized[2].Parent.ShouldNotBeNull().GetAppearanceState() & VisualState.Selected)
            .ShouldBe(VisualState.Selected);
        invoked.ShouldBe([(1, ActivationCause.Pointer), (0, ActivationCause.Keyboard)]);
        selectionOrder.ShouldBe([
            "changing:1:",
            "changed:1:",
            "changing:0:1",
            "changed:0:1",
            "changing:2:0",
            "changed:2:0"
        ]);

        // Act unavailable while focused
        await surface.UpdateAsync(() => list.IsEnabled = false, "disable focused ListView");

        // Assert cleanup without semantic press state and disabled appearance
        list.IsPressed.ShouldBeFalse();
        list.IsFocused.ShouldBeFalse();
        surface.ShouldHaveFocus(null);
        surface.ShouldHaveCapture(null);
        surface.ShouldHaveState(list, VisualState.Disabled);

        // Act re-enable
        await surface.UpdateAsync(() => list.IsEnabled = true, "re-enable ListView");

        // Assert re-enable recovery: pointer selection resumes exactly as before disabling
        surface.ShouldHaveState(list, VisualState.Normal);
        await surface.Pointer.ClickAsync(realized[0].Parent.ShouldNotBeNull());
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
        surface.ShouldHaveFocus(list);

        // Arrange a disabled ListView and an independently-mounted enabled twin at the same size
        var disabledTwin = new UiListView
        {
            Items = ["One", "Two", "Three"],
            IsEnabled = false,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var disabledSurface = await ComponentSurface.MountAsync(
            disabledTwin,
            new Size(6, 4),
            TestContext.Current.CancellationToken);
        var enabledTwin = new UiListView
        {
            Items = ["One", "Two", "Three"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var enabledSurface = await ComponentSurface.MountAsync(
            enabledTwin,
            new Size(6, 4),
            TestContext.Current.CancellationToken);

        // Act genuine resize of both twins to a different shared size
        await disabledSurface.ResizeAsync(new Size(4, 6));
        await enabledSurface.ResizeAsync(new Size(4, 6));

        // Assert stable geometry: disabling never perturbs layout
        disabledTwin.Bounds.ShouldBe(enabledTwin.Bounds);
        disabledTwin.DesiredSize.ShouldBe(enabledTwin.DesiredSize);

        // Arrange an ancestor container that owns a ListView
        var ancestorList = new UiListView
        {
            Items = ["A", "B"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var ancestor = new Overlay { Children = { ancestorList } };
        await using var ancestorSurface = await ComponentSurface.MountAsync(
            ancestor,
            new Size(6, 3),
            TestContext.Current.CancellationToken);

        // Act disable the ancestor container
        await ancestorSurface.UpdateAsync(() => ancestor.IsEnabled = false, "disable ancestor container");

        // Assert the owned ListView inherits Disabled without being disabled itself
        ancestorList.IsEnabled.ShouldBeTrue();
        ancestorSurface.ShouldHaveState(ancestorList, VisualState.Disabled);
    }

    /// <summary>Verifies modified pointer selection and arrow navigation skip an unavailable realized item.</summary>
    [Fact]
    public async Task Input_WhenMultipleSelectionUsesModifiers_PreservesRangeAndSkipsDisabledItemAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C", "D"],
            SelectionMode = ListSelectionMode.Multiple,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        realized[2].IsEnabled = false;
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(4, 4),
            TestContext.Current.CancellationToken);

        // Act modified selection
        await surface.Pointer.ClickAsync(realized[1].Parent.ShouldNotBeNull(), Modifiers.Control);
        await surface.Pointer.ClickAsync(realized[3].Parent.ShouldNotBeNull(), Modifiers.Shift);

        // Assert modified selection
        list.SelectedItems.ShouldBe(new object?[] { "B", "D" });
        list.ActiveIndex.ShouldBe(3);

        // Act disabled skip
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert disabled skip
        list.ActiveIndex.ShouldBe(1);
        list.SelectedItems.ShouldBe(new object?[] { "B" });
        surface.ShouldHaveState(list, VisualState.IsPointerOver | VisualState.Focused);
        realized[2].EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies cancelled keyboard selection keeps the active row synchronized with committed selection.</summary>
    [Fact]
    public async Task Input_WhenKeyboardSelectionIsCancelled_KeepsActiveRowSelectedAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B"],
            SelectedIndex = 0,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.SelectionChanging += (_, eventArgs) => eventArgs.Cancel = true;
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(0);
        surface.ShouldHaveFocus(list);
        (realized[0].Parent.ShouldNotBeNull().GetAppearanceState() & VisualState.Selected)
            .ShouldBe(VisualState.Selected);
        (realized[1].Parent.ShouldNotBeNull().GetAppearanceState() & VisualState.Selected)
            .ShouldBe(default);
    }

    /// <summary>Verifies reentrant keyboard selection keeps the reentrant commit active.</summary>
    [Fact]
    public async Task Input_WhenKeyboardSelectionReenters_PreservesCommittedSelectionAsActiveRowAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["A", "B", "C"],
            SelectedIndex = 0,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.SelectionChanging += (_, eventArgs) =>
        {
            if (eventArgs.AddedIndexes.Span.Contains(1))
            {
                list.SelectedIndex = 2;
            }
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(4, 3),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        list.SelectedIndex.ShouldBe(2);
        list.ActiveIndex.ShouldBe(2);
        surface.ShouldHaveFocus(list);
        (realized[2].Parent.ShouldNotBeNull().GetAppearanceState() & VisualState.Selected)
            .ShouldBe(VisualState.Selected);
    }

    /// <summary>Verifies a reentrant None mode change preserves navigation without committing selection.</summary>
    [Fact]
    public async Task Input_WhenSelectionChangingSwitchesToNone_NavigatesWithoutSelectionAsync()
    {
        // Arrange
        var list = new UiListView
        {
            Items = ["A", "B"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.SelectionChanging += (_, _) => list.SelectionMode = ListSelectionMode.None;
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        list.SelectionMode.ShouldBe(ListSelectionMode.None);
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(1);
        surface.ShouldHaveFocus(list);
    }

    /// <summary>Verifies item replacement invalidates a pending keyboard navigation target.</summary>
    [Fact]
    public async Task Input_WhenSelectionChangingReplacesItems_DropsStaleNavigationTargetAsync()
    {
        // Arrange
        var list = new UiListView
        {
            Items = ["A", "B"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.SelectionChanging += (_, _) => list.Items = [];
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        list.Items.ShouldBeEmpty();
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
        surface.ShouldHaveFocus(list);
        surface.ShouldRender("""


                             """);
    }

    /// <summary>Verifies combined mode and item replacement cannot commit a disposed navigation target.</summary>
    [Fact]
    public async Task Input_WhenSelectionChangingClearsModeAndItems_DropsStaleNavigationTargetAsync()
    {
        // Arrange
        var list = new UiListView
        {
            Items = ["A", "B"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.SelectionChanging += (_, _) =>
        {
            list.SelectionMode = ListSelectionMode.None;
            list.Items = [];
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(4, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        list.SelectionMode.ShouldBe(ListSelectionMode.None);
        list.Items.ShouldBeEmpty();
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(-1);
        surface.ShouldHaveFocus(list);
        surface.ShouldRender("""


                             """);
    }

    /// <summary>Verifies page navigation scrolls, resize clamps, and replacement removes selected and stale rows.</summary>
    [Fact]
    public async Task ResizeAsync_WhenPagedListIsReplaced_RepairsOffsetsSelectionAndCellsAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(realized[0].Parent.ShouldNotBeNull());

        // Act page and select
        await surface.Keyboard.PressAsync(Code.PageDown);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));

        // Assert paged viewport
        list.ActiveIndex.ShouldBe(3);
        list.SelectedIndex.ShouldBe(3);
        list.VerticalOffset.ShouldBe(1);
        surface.ShouldRender("""
                             Item 1
                             Item 2
                             Item 3
                             """);

        // Act resize and replace
        await surface.ResizeAsync(new Size(8, 5));
        await surface.UpdateAsync(() => list.Items = new object?[] { "A", "界" }, "replace ListView items");

        // Assert repaired state and stale clearing
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(1);
        list.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
                             A
                             界



                             """);
        surface.Cell(new Point(1, 1)).Continuation.ShouldBeTrue();
    }

    /// <summary>Verifies PageDown accumulates realized row heights rather than applying the
    /// viewport's cell height directly as an item-index delta. A 9-cell viewport holding 3-cell
    /// rows shows exactly 3 rows per page, so one PageDown must land on item 3 - the old bug
    /// applied 9 as an item count and skipped items 3 through 8 entirely.</summary>
    [Fact]
    public async Task PageDown_WhenRowsAreMultiCell_AdvancesByRealizedHeightNotViewportCellsAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!) { Height = Length.Cells(3) }),
            Items = Enumerable.Range(0, 12).Select(value => (object?) $"Item {value}").ToArray(),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 9),
            TestContext.Current.CancellationToken);
        await surface.Pointer.ClickAsync(realized[0].Parent.ShouldNotBeNull());

        // Act
        await surface.Keyboard.PressAsync(Code.PageDown);

        // Assert - BringIntoView reveals item 3 with the minimal offset that shows it, distinct
        // from ActiveIndex, which is the actual bug this test locks in.
        list.ActiveIndex.ShouldBe(3);
        list.VerticalOffset.ShouldBe(3);
    }

    /// <summary>Verifies a wrapping item's trailing row is reachable at the maximum offset: an
    /// automatically-added vertical bar reserves a column, which reflows the wrapped item into an
    /// extra row. A stale unreserved measurement would commit a shorter Extent, making the reflowed
    /// row unreachable at any offset and clipped at the item's own arranged height.</summary>
    [Fact]
    public async Task Render_WhenAutoBarReflowsWrappedItem_TrailingRowIsReachableAtMaximumOffsetAsync()
    {
        // Arrange
        var list = new UiListView
        {
            Items = new object?[] { "one two three" },
            ItemTemplate = item => new ControlText((string) item!) { Overflow = Overflow.Wrap },
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(5, 2),
            TestContext.Current.CancellationToken);

        // Act
        for (var wheel = 0; wheel < 4; wheel++)
        {
            await surface.Pointer.WheelAsync(list, new Point(0, 0), wheelY: -1);
        }

        // Assert - the reserved column narrows wrapping to 4 rows ("one"/"two"/"thre"/"e"); the
        // last row only exists, and is only reachable, once the reservation is fed back into the
        // content measurement that decides Extent.
        list.Extent.ShouldBe(new Size(4, 4));
        list.VerticalOffset.ShouldBe(2);
        surface.ShouldRender("""
                             thre▲
                             e   ▼
                             """);
    }

    /// <summary>Verifies the SingleClick default still raises ItemInvoked from one pointer click.</summary>
    [Fact]
    public async Task Input_WhenItemInvocationIsSingleClick_InvokesFromOneClickAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var invoked = new List<int>();
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "Two"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 2),
            TestContext.Current.CancellationToken);

        // Act
        await surface.Pointer.ClickAsync(realized[1].Parent.ShouldNotBeNull());

        // Assert
        list.ItemInvocation.ShouldBe(ListItemInvocation.SingleClick);
        list.SelectedIndex.ShouldBe(1);
        invoked.ShouldBe([1]);
    }

    /// <summary>Verifies DoubleClick invocation selects on the first pointer click without raising
    /// ItemInvoked, then raises it once a second click lands within the multi-click window.</summary>
    [Fact]
    public async Task Input_WhenItemInvocationIsDoubleClick_SelectsThenInvokesOnSecondClickAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var invoked = new List<int>();
        var clock = new ManualTimeProvider();
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "Two"],
            ItemInvocation = ListItemInvocation.DoubleClick,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 2),
            clock,
            TestContext.Current.CancellationToken);
        var row = realized[1].Parent.ShouldNotBeNull();

        // Act first click
        await surface.Pointer.ClickAsync(row);

        // Assert first click selects without invoking
        list.SelectedIndex.ShouldBe(1);
        invoked.ShouldBeEmpty();

        // Act second click within the multi-click window
        await surface.Pointer.ClickAsync(row);

        // Assert second click invokes
        invoked.ShouldBe([1]);
    }

    /// <summary>Verifies DoubleClick invocation still raises ItemInvoked from Enter, matching the
    /// documented Enter-always-commits contract.</summary>
    [Fact]
    public async Task Input_WhenItemInvocationIsDoubleClick_EnterStillInvokesAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var invoked = new List<int>();
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "Two"],
            ItemInvocation = ListItemInvocation.DoubleClick,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 2),
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);

        // Act
        await surface.Keyboard.PressAsync(Code.Enter);

        // Assert
        invoked.ShouldBe([1]);
    }

    /// <summary>Verifies a DoubleClick, Multiple-selection ListView does not raise ItemInvoked from
    /// two rapid Control-held clicks on the same row - PointerManager's click-count chain tracks
    /// target, cell, and buttons only, never modifiers, so two rapid Control-clicks reach the same
    /// multi-click count a plain double-click would, even though the user only meant to toggle
    /// selection twice. A plain double-click on the same row still invokes.</summary>
    [Fact]
    public async Task Input_WhenItemInvocationIsDoubleClickAndMultipleSelectionModifierClicksRepeat_SuppressesInvokeAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var invoked = new List<int>();
        var clock = new ManualTimeProvider();
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One", "Two"],
            SelectionMode = ListSelectionMode.Multiple,
            ItemInvocation = ListItemInvocation.DoubleClick,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        list.ItemInvoked += (_, eventArgs) => invoked.Add(eventArgs.Index);
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 2),
            clock,
            TestContext.Current.CancellationToken);
        var row = realized[1].Parent.ShouldNotBeNull();

        // Act first Control-held click
        await surface.Pointer.ClickAsync(row, Modifiers.Control);

        // Assert first click toggles selection on
        list.SelectedItems.ShouldBe(new object?[] { "Two" });
        invoked.ShouldBeEmpty();

        // Act second Control-held click within the multi-click window
        await surface.Pointer.ClickAsync(row, Modifiers.Control);

        // Assert second click toggles selection back off, without invoking despite reaching a
        // multi-click count
        list.SelectedItems.ShouldBeEmpty();
        invoked.ShouldBeEmpty();

        // Act a plain double-click, once the modified chain above has expired, still invokes
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(600), "break the multi-click chain");
        await surface.Pointer.ClickAsync(row);
        await surface.Pointer.ClickAsync(row);

        // Assert
        invoked.ShouldBe([1]);
    }

    /// <summary>Verifies every command modifier prevents a multi-click from becoming an invocation.</summary>
    [Theory]
    [InlineData(Modifiers.Control)]
    [InlineData(Modifiers.Shift)]
    [InlineData(Modifiers.Alt)]
    public async Task Input_WhenDoubleClickCarriesCommandModifier_DoesNotInvokeAsync(Modifiers modifiers)
    {
        List<ControlText> realized = [];
        var invocations = 0;
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)),
            Items = ["One"],
            ItemInvocation = ListItemInvocation.DoubleClick,
            ScrollBars = ScrollBars.None
        };
        list.ItemInvoked += (_, _) => invocations++;
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 1),
            TestContext.Current.CancellationToken);
        var row = realized[0].Parent.ShouldNotBeNull();

        await surface.Pointer.ClickAsync(row, modifiers);
        await surface.Pointer.ClickAsync(row, modifiers);

        invocations.ShouldBe(0);
    }

    /// <summary>Verifies a runtime IsVisible -&gt; Collapsed -&gt; IsVisible transition on a mounted eager
    /// (no RowHeight) item leaves no stale committed cell behind - the surviving sibling moves up to
    /// occupy the collapsed row's former position with no leftover glyph or background, and pointer
    /// hit-testing tracks the live row at every step, both immediately after the collapse and again
    /// once the item is restored. See
    /// <see cref="Pointer_WhenRowHeightItemContentTogglesCollapsedThenVisible_LeavesArithmeticGapAndHitTargetsAsync"/>
    /// for the RowHeight (fixed, virtualized) counterpart, which keeps the collapsed row's
    /// arithmetic slot blank instead of reflowing siblings up into it.</summary>
    [Fact]
    public async Task Pointer_WhenItemContentTogglesCollapsedThenVisible_ClearsStaleCellsAndHitTargetsAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            ItemTemplate = item => Add(realized, new ControlText((string) item!)
            {
                Face = AppearanceTestValues.Face(background: Color.Rgb(0x10, 0x20, 0x30))
            }),
            Items = ["AAA", "BBB", "CCC"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(3, 3),
            TestContext.Current.CancellationToken);
        var middle = realized[1];
        var last = realized[2];

        // Assert baseline
        surface.ShouldRender("""
                             AAA
                             BBB
                             CCC
                             """);

        // Act - collapse the middle item's template content.
        await surface.UpdateAsync(() => middle.Visibility = Visibility.Collapsed, "collapse middle list item content");

        // Assert - "CCC" moved up to occupy "BBB"'s former row with no stale glyph left behind,
        // and the moved-up sibling is the live hit target at that position.
        surface.ShouldRender("""
                             AAA
                             CCC

                             """);
        ((ListItem) list.HitTest(new Point(0, 1)).ShouldNotBeNull()).Content.ShouldBeSameAs(last);

        // Act - restore visibility.
        await surface.UpdateAsync(() => middle.Visibility = Visibility.Visible, "restore middle list item content");

        // Assert - the original three-row layout is exactly restored and hittable again.
        surface.ShouldRender("""
                             AAA
                             BBB
                             CCC
                             """);
        ((ListItem) list.HitTest(new Point(0, 1)).ShouldNotBeNull()).Content.ShouldBeSameAs(middle);
    }

    /// <summary>Verifies a runtime IsVisible -&gt; Collapsed -&gt; IsVisible transition on a mounted RowHeight
    /// (fixed, virtualized) item zero-arranges its row instead of reflowing siblings - the collapsed
    /// row's arithmetic slot stays reserved and blank, matching the documented contract that fixed
    /// extent is purely ItemCount * RowHeight regardless of any row's visibility. Pointer hit-testing
    /// stops resolving to the collapsed row at its former position, and Extent, VerticalOffset, and
    /// the composed scrollbar's own geometry stay exactly where they started throughout, since none
    /// of them are a function of which rows currently render content.</summary>
    [Fact]
    public async Task Pointer_WhenRowHeightItemContentTogglesCollapsedThenVisible_LeavesArithmeticGapAndHitTargetsAsync()
    {
        // Arrange
        List<ControlText> realized = [];
        var list = new UiListView
        {
            RowHeight = 1,
            ItemTemplate = item => Add(realized, new ControlText((string) item!)
            {
                Face = AppearanceTestValues.Face(background: Color.Rgb(0x10, 0x20, 0x30)),
                Height = Length.Cells(1)
            }),
            Items = ["A", "B", "C", "D"],
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Always,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(2, 2),
            TestContext.Current.CancellationToken);
        var middle = realized[1];
        var railColumn = list.Bounds.Width - 1;
        var baselineExtent = list.Extent;
        var baselineOffset = list.VerticalOffset;
        var baselineRail = list.HitTest(new Point(railColumn, 0)).ShouldBeOfType<ScrollBar>();
        var baselineRailBounds = baselineRail.Bounds;

        // Assert baseline
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("B");
        ((ListItem) list.HitTest(new Point(0, 1)).ShouldNotBeNull()).Content.ShouldBeSameAs(middle);

        // Act - collapse the second item's template content.
        await surface.UpdateAsync(() => middle.Visibility = Visibility.Collapsed, "collapse RowHeight list item content");

        // Assert - row 1 is left blank rather than reflowed, since fixed-mode placement is purely
        // arithmetic; nothing hit-tests to the collapsed item at its former position; and extent,
        // offset, and the composed scrollbar's own geometry are all unaffected by the collapse.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(0, 1)).Text.ShouldBe(" ");
        ((list.HitTest(new Point(0, 1)) as ListItem)?.Content).ShouldNotBeSameAs(middle);
        list.Extent.ShouldBe(baselineExtent);
        list.VerticalOffset.ShouldBe(baselineOffset);
        var collapsedRail = list.HitTest(new Point(railColumn, 0)).ShouldBeOfType<ScrollBar>();
        collapsedRail.Bounds.ShouldBe(baselineRailBounds);

        // Act - restore visibility.
        await surface.UpdateAsync(() => middle.Visibility = Visibility.Visible, "restore RowHeight list item content");

        // Assert - the row re-occupies its slot exactly, both rendered and hit-testable, and the
        // geometry invariants held throughout still hold now that the item is back.
        surface.Cell(new Point(0, 0)).Text.ShouldBe("A");
        surface.Cell(new Point(0, 1)).Text.ShouldBe("B");
        ((ListItem) list.HitTest(new Point(0, 1)).ShouldNotBeNull()).Content.ShouldBeSameAs(middle);
        list.Extent.ShouldBe(baselineExtent);
        list.VerticalOffset.ShouldBe(baselineOffset);
        var restoredRail = list.HitTest(new Point(railColumn, 0)).ShouldBeOfType<ScrollBar>();
        restoredRail.Bounds.ShouldBe(baselineRailBounds);
    }

    private static ControlText Add(List<ControlText> controls, ControlText control)
    {
        controls.Add(control);
        return control;
    }

    private static string Join(ReadOnlyMemory<int> values) => string.Join(',', values.ToArray());
}
