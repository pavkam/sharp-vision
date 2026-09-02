// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Collections;

using System.Text.Json;

/// <summary>Verifies JsonView navigation through mounted surfaces: every arrow and endpoint key
/// with its selection event, parent/child traversal, selection normalization after a collapse,
/// empty containers, horizontal reveal of wide labels, large documents, pointer targets, wheel,
/// runtime glyph changes, malformed replacement, and disposal mid-keystroke.</summary>
public sealed class JsonViewInteractionTests
{
    private const string _nested = /*lang=json,strict*/
        "{\"a\":1,\"b\":{\"c\":2,\"d\":{\"e\":3}},\"f\":[10,20]}";

    /// <summary>Verifies Down, End, Home, and Up walk the visible entries in order, clamp at both
    /// ends, and publish each committed pointer transition exactly once.</summary>
    [Fact]
    public async Task Keyboard_WhenVerticalKeysArePressed_WalkVisibleEntriesAndClampAsync()
    {
        // Arrange
        var view = new JsonView { Json = _nested };
        var transitions = new List<string>();
        view.SelectionChanged += (_, eventArgs) => transitions.Add($"{eventArgs.PreviousPath}>{eventArgs.Path}");
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(30, 14),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        view.SelectedPath.ShouldBe("/a");

        // Act and assert
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedPath.ShouldBe("/b");
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedPath.ShouldBe("/b/c");
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedPath.ShouldBe("/b/d");
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedPath.ShouldBe("/b/d/e");
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedPath.ShouldBe("/f");
        await surface.Keyboard.PressAsync(Code.End);
        view.SelectedPath.ShouldBe("/f/1");
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedPath.ShouldBe("/f/1");
        await surface.Keyboard.PressAsync(Code.Home);
        view.SelectedPath.ShouldBe("/a");
        await surface.Keyboard.PressAsync(Code.Up);
        view.SelectedPath.ShouldBe("/a");
        transitions.ShouldBe(["/a>/b", "/b>/b/c", "/b/c>/b/d", "/b/d>/b/d/e", "/b/d/e>/f", "/f>/f/1", "/f/1>/a"]);
    }

    /// <summary>Verifies Left moves a leaf to its parent, collapses an expanded container, and is
    /// inert at the top level, while Right expands a collapsed container, enters the first child
    /// of an expanded one, and is inert on a leaf - as is Enter.</summary>
    [Fact]
    public async Task Keyboard_WhenHorizontalKeysArePressed_TraverseParentsChildrenAndDisclosureAsync()
    {
        // Arrange
        var view = new JsonView { Json = _nested };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(30, 14),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        for (var step = 0; step < 4; step++)
        {
            await surface.Keyboard.PressAsync(Code.Down);
        }

        view.SelectedPath.ShouldBe("/b/d/e");
        view.VisibleEntryCount.ShouldBe(8);

        // Act and assert Left
        await surface.Keyboard.PressAsync(Code.Left);
        view.SelectedPath.ShouldBe("/b/d");
        view.VisibleEntryCount.ShouldBe(8);
        await surface.Keyboard.PressAsync(Code.Left);
        view.SelectedPath.ShouldBe("/b/d");
        view.VisibleEntryCount.ShouldBe(7);
        await surface.Keyboard.PressAsync(Code.Left);
        view.SelectedPath.ShouldBe("/b");
        await surface.Keyboard.PressAsync(Code.Left);
        view.SelectedPath.ShouldBe("/b");
        view.VisibleEntryCount.ShouldBe(5);
        await surface.Keyboard.PressAsync(Code.Left);
        view.SelectedPath.ShouldBe("/b");
        view.VisibleEntryCount.ShouldBe(5);

        // Act and assert Right
        await surface.Keyboard.PressAsync(Code.Right);
        view.SelectedPath.ShouldBe("/b");
        view.VisibleEntryCount.ShouldBe(7);
        await surface.Keyboard.PressAsync(Code.Right);
        view.SelectedPath.ShouldBe("/b/c");
        await surface.Keyboard.PressAsync(Code.Right);
        view.SelectedPath.ShouldBe("/b/c");
        view.VisibleEntryCount.ShouldBe(7);
        await surface.Keyboard.PressAsync(Code.Enter);
        view.SelectedPath.ShouldBe("/b/c");
        view.VisibleEntryCount.ShouldBe(7);
    }

    /// <summary>Verifies collapsing every container while a deep entry is selected moves the
    /// selection to its nearest visible ancestor and publishes that transition, and expanding
    /// again keeps it there.</summary>
    [Fact]
    public async Task CollapseAll_WhenDeepEntryIsSelected_MovesSelectionToNearestVisibleAncestorAsync()
    {
        // Arrange
        var view = new JsonView { Json = _nested };
        var transitions = new List<string>();
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(30, 14),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        for (var step = 0; step < 4; step++)
        {
            await surface.Keyboard.PressAsync(Code.Down);
        }

        view.SelectedPath.ShouldBe("/b/d/e");
        view.SelectionChanged += (_, eventArgs) => transitions.Add($"{eventArgs.PreviousPath}>{eventArgs.Path}");

        // Act
        await surface.UpdateAsync(view.CollapseAll, "collapse all");

        // Assert
        view.SelectedPath.ShouldBe("/b");
        view.VisibleEntryCount.ShouldBe(3);
        transitions.ShouldBe(["/b/d/e>/b"]);
        surface.Cell(new Point(0, 2)).Text.ShouldBe("▶");

        // Act
        await surface.UpdateAsync(view.ExpandAll, "expand all");

        // Assert
        view.SelectedPath.ShouldBe("/b");
        view.VisibleEntryCount.ShouldBe(8);
        transitions.Count.ShouldBe(1);
    }

    /// <summary>Verifies an empty root container renders its brackets with no selection and every
    /// key is inert, and empty nested containers are selectable leaves that never expand.</summary>
    [Fact]
    public async Task Keyboard_WhenContainersAreEmpty_RendersBracketsAndStaysInertAsync()
    {
        // Arrange
        var view = new JsonView { Json = "{}" };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(16, 4),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Assert and act on the empty root
        surface.ShouldRender("{}");
        view.SelectedPath.ShouldBeNull();
        await surface.Keyboard.PressAsync(Code.Down);
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Enter);
        await surface.Keyboard.PressAsync(Code.End);
        view.SelectedPath.ShouldBeNull();
        view.VisibleEntryCount.ShouldBe(0);

        // Act nested empties
        await surface.UpdateAsync(() => view.Json = /*lang=json,strict*/ "{\"a\":{},\"b\":[]}", "nested empties");

        // Assert
        surface.ShouldRender("""
                             {
                               "a": {},
                               "b": []
                             }
                             """);
        view.SelectedPath.ShouldBe("/a");
        await surface.Keyboard.PressAsync(Code.Down);
        view.SelectedPath.ShouldBe("/b");
        await surface.Keyboard.PressAsync(Code.Right);
        await surface.Keyboard.PressAsync(Code.Enter);
        view.SelectedPath.ShouldBe("/b");
        view.VisibleEntryCount.ShouldBe(2);
        surface.Cell(new Point(7, 2)).Text.ShouldBe("[");
    }

    /// <summary>Verifies selecting an entry whose label lies past the viewport scrolls
    /// horizontally just far enough to expose it, and selecting a label left of the offset scrolls
    /// back to it.</summary>
    [Fact]
    public async Task Keyboard_WhenSelectedLabelIsOutsideTheViewportWidth_RevealsItHorizontallyAsync()
    {
        // Arrange
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"k\":{\"averyveryverylongkey\":1}}",
            ShowScrollBars = ShowScrollBars.Never
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(12, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        view.Viewport.Width.ShouldBe(12);
        await surface.Keyboard.PressAsync(Code.Tab);
        view.HorizontalOffset.ShouldBe(0);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert the label's right edge is now the viewport's right edge
        view.SelectedPath.ShouldBe("/k/averyveryverylongkey");
        view.HorizontalOffset.ShouldBe(14);
        surface.Cell(new Point(11, 2)).Text.ShouldBe("\"");
        surface.Cell(new Point(10, 2)).Text.ShouldBe("y");

        // Act
        await surface.Keyboard.PressAsync(Code.Up);

        // Assert the parent's label start is now the viewport's left edge
        view.SelectedPath.ShouldBe("/k");
        view.HorizontalOffset.ShouldBe(2);
        surface.Cell(new Point(0, 1)).Text.ShouldBe("\"");
    }

    /// <summary>Verifies a three-thousand-entry array is reachable by End, paged back by PageUp,
    /// and revealed minimally by Home, with the rendered bottom row matching the offset.</summary>
    [Fact]
    public async Task Keyboard_WhenDocumentIsLarge_EndPageUpAndHomeScrollExactlyAsync()
    {
        // Arrange
        var view = new JsonView
        {
            Json = "[" + string.Join(',', Enumerable.Range(0, 3_000)) + "]",
            ShowScrollBars = ShowScrollBars.Never
        };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(20, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act End
        await surface.Keyboard.PressAsync(Code.End);

        // Assert
        view.SelectedPath.ShouldBe("/2999");
        view.VerticalOffset.ShouldBe(2_995);
        view.Extent.Height.ShouldBe(3_002);
        RowText(surface, 5, 14).ShouldBe("  [2999]: 2999");

        // Act PageUp
        await surface.Keyboard.PressAsync(Code.PageUp);

        // Assert
        view.SelectedPath.ShouldBe("/2993");
        view.VerticalOffset.ShouldBe(2_994);
        RowText(surface, 0, 14).ShouldBe("  [2993]: 2993");

        // Act Home
        await surface.Keyboard.PressAsync(Code.Home);

        // Assert the first entry's own line becomes the top row
        view.SelectedPath.ShouldBe("/0");
        view.VerticalOffset.ShouldBe(1);
        RowText(surface, 0, 8).ShouldBe("  [0]: 0");
    }

    /// <summary>Verifies pointer presses on a value, a closing bracket, or empty space change
    /// nothing, a press on a label selects and focuses, and the wheel scrolls by LineSize.</summary>
    [Fact]
    public async Task Pointer_WhenPressingValuesBracketsLabelsAndWheeling_TargetsOnlyLabelsAsync()
    {
        // Arrange
        var view = new JsonView
        {
            Json = _nested,
            LineSize = 3,
            ShowScrollBars = ShowScrollBars.Never,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var changes = 0;
        view.SelectionChanged += (_, _) => changes++;
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(30, 6),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        view.SelectedPath.ShouldBe("/a");

        // Act presses that must not select
        await surface.Pointer.ClickAsync(view, new Point(9, 3));
        await surface.Pointer.ClickAsync(view, new Point(0, 0));
        await surface.Pointer.ClickAsync(view, new Point(20, 1));

        // Assert the presses focused the view without moving the selection
        view.SelectedPath.ShouldBe("/a");
        changes.ShouldBe(0);
        surface.ShouldHaveFocus(view);

        // Act a label press
        await surface.Pointer.ClickAsync(view, new Point(5, 3));

        // Assert
        view.SelectedPath.ShouldBe("/b/c");
        changes.ShouldBe(1);
        surface.ShouldHaveFocus(view);

        // Act wheel
        await surface.Pointer.WheelAsync(view, new Point(2, 2), wheelY: -1);

        // Assert
        view.VerticalOffset.ShouldBe(3);
        view.SelectedPath.ShouldBe("/b/c");
        RowText(surface, 0, 12).ShouldBe("    \"c\": 2, ");
    }

    /// <summary>Verifies replacing the disclosure glyphs through Style while mounted rebuilds the
    /// projected lines and keeps the new glyph cell as the toggle target.</summary>
    [Fact]
    public async Task Style_WhenDisclosureGlyphsChangeWhileMounted_RebuildsLinesAndHitTargetsAsync()
    {
        // Arrange
        var view = new JsonView { Json = _nested };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(30, 14),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        surface.Cell(new Point(0, 2)).Text.ShouldBe("▼");

        // Act
        await surface.UpdateAsync(
            () => view.Style = view.ActualStyle with { CollapsedGlyph = new Rune('+'), ExpandedGlyph = new Rune('-') },
            "replace glyphs");

        // Assert
        surface.Cell(new Point(0, 2)).Text.ShouldBe("-");
        surface.Cell(new Point(2, 4)).Text.ShouldBe("-");

        // Act toggle through the new glyph
        await surface.Pointer.ClickAsync(view, new Point(0, 2));

        // Assert
        view.SelectedPath.ShouldBe("/b");
        view.VisibleEntryCount.ShouldBe(5);
        surface.Cell(new Point(0, 2)).Text.ShouldBe("+");
        RowText(surface, 2, 12).ShouldBe("+ \"b\": {…}, ");
    }

    /// <summary>Verifies a malformed replacement assigned while mounted throws before any state
    /// changes, leaving the rendered document, selection, and offsets intact.</summary>
    [Fact]
    public async Task Json_WhenMalformedWhileMounted_ThrowsAndKeepsTheRenderedDocumentAsync()
    {
        // Arrange
        var view = new JsonView { Json = _nested };
        await using var surface = await ComponentSurface.MountAsync(
            view,
            new Size(30, 14),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);
        await surface.Keyboard.PressAsync(Code.Down);
        var changes = 0;
        view.SelectionChanged += (_, _) => changes++;

        // Act
        await surface.UpdateAsync(
            () => Should.Throw<JsonException>(() => view.Json = "{\"a\":"),
            "assign malformed document");

        // Assert
        view.Json.ShouldBe(_nested);
        view.SelectedPath.ShouldBe("/b");
        changes.ShouldBe(0);
        RowText(surface, 2, 8).ShouldBe("▼ \"b\": {");
    }

    /// <summary>Verifies a SelectionChanged handler that disposes the view mid-keystroke completes
    /// without throwing.</summary>
    [Fact]
    public async Task SelectionChanged_WhenHandlerDisposesView_CompletesWithoutThrowingAsync()
    {
        // Arrange
        var view = new JsonView { Json = _nested };
        var host = new Overlay { Children = { view } };
        view.SelectionChanged += (_, _) => view.Dispose();
        await using var surface = await ComponentSurface.MountAsync(
            host,
            new Size(30, 14),
            TestThemes.BorderlessContainer,
            TestContext.Current.CancellationToken);
        await surface.Keyboard.PressAsync(Code.Tab);

        // Act
        await surface.Keyboard.PressAsync(Code.Down);

        // Assert
        view.IsDisposed.ShouldBeTrue();
        view.Parent.ShouldBeNull();
        surface.Application.Focus.Focused.ShouldNotBeSameAs(view);
        surface.ShouldRender("");
    }

    /// <summary>Verifies the horizontal offset round-trips inside the extent and rejects a value
    /// past it without moving.</summary>
    [Fact]
    public void HorizontalOffset_WhenAssigned_RoundTripsAndRejectsValuesPastTheExtent()
    {
        var view = new JsonView
        {
            Json = /*lang=json,strict*/ "{\"k\":{\"averyveryverylongkey\":1}}",
            ShowScrollBars = ShowScrollBars.Never
        };
        new LayoutEngine().Layout(view, new Size(12, 6));
        var maximum = view.Extent.Width - view.Viewport.Width;
        maximum.ShouldBeGreaterThan(0);

        view.HorizontalOffset = 5;
        view.HorizontalOffset.ShouldBe(5);
        view.HorizontalOffset = maximum;
        view.HorizontalOffset.ShouldBe(maximum);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.HorizontalOffset = maximum + 1);
        _ = Should.Throw<ArgumentOutOfRangeException>(() => view.HorizontalOffset = -1);
        view.HorizontalOffset.ShouldBe(maximum);
    }

    private static string RowText(ComponentSurface surface, int y, int width)
    {
        var text = new StringBuilder();

        for (var x = 0; x < width; x++)
        {
            _ = text.Append(surface.Cell(new Point(x, y)).Text);
        }

        return text.ToString();
    }
}
