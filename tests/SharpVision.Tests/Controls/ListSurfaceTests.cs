// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using Label = ControlText;
using UiList = List;

/// <summary>Verifies List selection, focus, invocation, modifiers, scrolling, mutation, and cells through mounted surfaces.</summary>
public sealed class ListSurfaceTests
{
    /// <summary>Verifies hover paints only the targeted row while the focus-owning List stays neutral.</summary>
    [Fact]
    public async Task Pointer_WhenMovedOverItem_HighlightsOnlyTargetedRowAsync()
    {
        // Arrange
        List<Label> realized = [];
        var list = new UiList
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = ["One", "Two", "Three"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var surface = await ComponentSurface.MountAsync(
            list,
            new Size(8, 3),
            TestContext.Current.CancellationToken);
        var target = realized[1].Parent.ShouldNotBeNull();
        var untouchedBackground = surface.Cell(default).Style.Background;
        var hoverBackground = list.Theme.ShouldNotBeNull().Resolve(ThemeColor.From(ColorRole.Surface));

        // Act
        await surface.Pointer.MoveToAsync(target);

        // Assert
        list.IsPointerOver.ShouldBeTrue();
        target.IsPointerOver.ShouldBeTrue();
        surface.Cell(default).Style.Background.ShouldBe(untouchedBackground);
        surface.Cell(new Point(0, 1)).Style.Background.ShouldBe(hoverBackground);
        surface.Cell(new Point(0, 2)).Style.Background.ShouldBe(untouchedBackground);
    }

    /// <summary>Verifies pointer and keyboard paths commit selection, focus, invocation, and Unicode appearance.</summary>
    [Fact]
    public async Task Input_WhenPointerAndKeyboardNavigate_SelectsAndInvokesExactRowsAsync()
    {
        // Arrange
        List<Label> realized = [];
        var selectionOrder = new List<string>();
        var invoked = new List<(int Index, ActivationCause Cause)>();
        var list = new UiList
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = ["One", "界", "Three"],
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
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
        surface.ShouldHaveState(realized[1].Parent.ShouldNotBeNull(), VisualState.PointerOver | VisualState.Focused);
        (realized[1].Parent.ShouldNotBeNull().GetAppearanceState() & VisualState.Selected)
            .ShouldBe(VisualState.Selected);
        surface.Cell(new Point(1, 1)).IsContinuation.ShouldBeTrue();
        invoked.ShouldBe([(1, ActivationCause.Pointer)]);

        // Act keyboard
        await surface.Keyboard.PressAsync(Code.Up);
        await surface.Keyboard.CompleteCharacterAsync(new Rune(' '));
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.End);

        // Assert keyboard
        list.SelectedIndex.ShouldBe(0);
        list.ActiveIndex.ShouldBe(2);
        surface.ShouldHaveState(realized[2].Parent.ShouldNotBeNull(), VisualState.Focused);
        invoked.ShouldBe([(1, ActivationCause.Pointer), (0, ActivationCause.Keyboard)]);
        selectionOrder.ShouldBe([
            "changing:1:",
            "changed:1:",
            "changing:0:1",
            "changed:0:1",
        ]);
    }

    /// <summary>Verifies modified pointer selection and arrow navigation skip an unavailable realized item.</summary>
    [Fact]
    public async Task Input_WhenMultipleSelectionUsesModifiers_PreservesRangeAndSkipsDisabledItemAsync()
    {
        // Arrange
        List<Label> realized = [];
        var list = new UiList
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = ["A", "B", "C", "D"],
            SelectionMode = SelectionMode.Multiple,
            ScrollBars = ScrollBars.None,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
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
        list.SelectedItems.ShouldBe(new object?[] { "B", "C", "D" });
        list.ActiveIndex.ShouldBe(3);

        // Act disabled skip
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert disabled skip
        list.ActiveIndex.ShouldBe(1);
        surface.ShouldHaveState(realized[1].Parent.ShouldNotBeNull(), VisualState.Focused);
        realized[2].EffectiveIsEnabled.ShouldBeFalse();
    }

    /// <summary>Verifies page navigation scrolls, resize clamps, and replacement removes selected and stale rows.</summary>
    [Fact]
    public async Task ResizeAsync_WhenPagedListIsReplaced_RepairsOffsetsSelectionAndCellsAsync()
    {
        // Arrange
        List<Label> realized = [];
        var list = new UiList
        {
            ItemTemplate = item => Add(realized, new Label((string) item!)),
            Items = Enumerable.Range(0, 8).Select(value => (object?) $"Item {value}").ToArray(),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
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
        await surface.UpdateAsync(() => list.Items = new object?[] { "A", "界" }, "replace List items");

        // Assert repaired state and stale clearing
        list.SelectedIndex.ShouldBe(-1);
        list.ActiveIndex.ShouldBe(1);
        list.VerticalOffset.ShouldBe(0);
        surface.ShouldRender("""
            A
            界



            """);
        surface.Cell(new Point(1, 1)).IsContinuation.ShouldBeTrue();
    }

    private static Label Add(List<Label> controls, Label control)
    {
        controls.Add(control);
        return control;
    }

    private static string Join(ReadOnlyMemory<int> values) => string.Join(',', values.ToArray());
}
