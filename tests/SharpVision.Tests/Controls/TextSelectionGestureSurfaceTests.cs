// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies inherited cross-child text selection through mounted input and final cells.</summary>
public sealed class TextSelectionGestureSurfaceTests
{
    /// <summary>Verifies one ordinary container can drag a range across sibling text controls.</summary>
    [Fact]
    public async Task Pointer_WhenEnabled_DragsSelectionAcrossSiblingControlsAsync()
    {
        var first = new ControlText("ab");
        var second = new ControlText("cd");
        var owner = new Stack
        {
            Orientation = Orientation.Horizontal,
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { first, second }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(5, 1),
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(first, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(4, 0));
        await surface.Pointer.ReleaseAsync();

        var (selection, selectedText) = await surface.Application.Dispatcher.InvokeAsync(
            () => (owner.TextSelection, owner.SelectedText),
            TestContext.Current.CancellationToken);
        selection.ShouldBe(new Selection(0, 4));
        selectedText.ShouldBe("abcd");
        surface.ShouldHaveFocus(owner);
        surface.ShouldHaveCapture(null);
        var selectedForeground = TerminalPalette.Project(
            surface.Application.Theme.ResolveColor(SemanticColor.SelectedText),
            ColorDepth.Basic16);
        var selectedBackground = TerminalPalette.Project(
            surface.Application.Theme.ResolveColor(SemanticColor.SelectedControl),
            ColorDepth.Basic16);
        for (var x = 0; x < 4; x++)
        {
            surface.Cell(new Point(x, 0)).Style.Foreground.ShouldBe(selectedForeground);
            surface.Cell(new Point(x, 0)).Style.Background.ShouldBe(selectedBackground);
        }
    }

    /// <summary>Verifies a new primary press clears a committed range before any movement or release.</summary>
    [Fact]
    public async Task PointerPress_WhenRangeExists_CollapsesImmediatelyAtPressedCaretAsync()
    {
        var text = new ControlText("abcd");
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { text }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () => owner.SetTextSelection(new Selection(0, 4)),
            "establish selection before a new pointer gesture");
        var changes = 0;
        owner.TextSelectionChanged += (_, _) => changes++;

        await surface.Pointer.MoveToAsync(text, new Point(2, 0));
        await surface.Pointer.PressAsync();

        var (selection, selectedText) = await surface.Application.Dispatcher.InvokeAsync(
            () => (owner.TextSelection, owner.SelectedText),
            TestContext.Current.CancellationToken);
        selection.ShouldBe(new Selection(2, 2));
        selectedText.ShouldBeEmpty();
        changes.ShouldBe(1);
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies word and visual-line click expansion belong to the common controller.</summary>
    [Fact]
    public async Task Pointer_WhenCommonOwnerIsClickedRepeatedly_SelectsWordThenVisualLineAsync()
    {
        var text = new ControlText("alpha beta gamma");
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { text }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(16, 1),
            TestContext.Current.CancellationToken);

        await surface.Pointer.ClickAsync(text, new Point(7, 0));
        await surface.Pointer.ClickAsync(text, new Point(7, 0));

        var selectedWord = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.SelectedText,
            TestContext.Current.CancellationToken);
        selectedWord.ShouldBe("beta");

        await surface.Pointer.ClickAsync(text, new Point(7, 0));

        var selectedLine = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.SelectedText,
            TestContext.Current.CancellationToken);
        selectedLine.ShouldBe("alpha beta gamma");
    }

    /// <summary>Verifies an opt-out ancestor does not intercept an ordinary child press route.</summary>
    [Fact]
    public async Task Pointer_WhenDisabled_DoesNotCreateSelectionOrCaptureAsync()
    {
        var text = new ControlText("text");
        var owner = new Stack { IsFocusable = true, Children = { text } };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(4, 1),
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(text, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(3, 0));
        await surface.Pointer.ReleaseAsync();

        owner.TextSelection.ShouldBe(default);
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies Ctrl+A and Shift+Left use the common grapheme-safe keyboard path.</summary>
    [Fact]
    public async Task Keyboard_WhenOwnerContainsFocus_SelectsAndExtendsByGraphemeAsync()
    {
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { new ControlText("Ae\u0301") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(3, 1),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(() => owner.Focus().ShouldBeTrue(), "focus selection owner");

        var controlA = new KeyEventArgs(new Stroke(
            Code.Character,
            new Rune('a'),
            nativeCode: 0,
            Modifiers.Control,
            KeyAction.Press));
        await surface.UpdateAsync(
            () => _ = Router.Route(owner, Events.Key, controlA),
            "press Ctrl+A");
        await surface.Keyboard.PressAsync(Code.Left, Modifiers.Shift);

        var (selection, selectedText) = await surface.Application.Dispatcher.InvokeAsync(
            () => (owner.TextSelection, owner.SelectedText),
            TestContext.Current.CancellationToken);
        selection.ShouldBe(new Selection(0, 1));
        selectedText.ShouldBe("A");
    }

    /// <summary>Verifies ordinary and word navigation are shared selection commands too.</summary>
    [Fact]
    public async Task Keyboard_WhenCommonOwnerMovesWithoutShift_CollapsesThenMovesByWordAsync()
    {
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { new ControlText("alpha beta") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(10, 1),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                owner.Focus().ShouldBeTrue();
                owner.SetTextSelection(new Selection(6, 10));
            },
            "focus owner with selected word");

        await RouteKeyAsync(surface, owner, Code.Left, Modifiers.None);
        var collapsed = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.TextSelection,
            TestContext.Current.CancellationToken);
        collapsed.ShouldBe(new Selection(6, 6));

        await RouteKeyAsync(surface, owner, Code.Left, Modifiers.Control);
        var moved = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.TextSelection,
            TestContext.Current.CancellationToken);
        moved.ShouldBe(new Selection(0, 0));
    }

    /// <summary>Verifies common vertical navigation follows projected rows while preserving the anchor.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftUpIsPressed_ExtendsAtTheSameVisualColumnAsync()
    {
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { new ControlText("ab"), new ControlText("cd") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(2, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                owner.Focus().ShouldBeTrue();
                owner.SetTextSelection(new Selection(3, 3));
            },
            "focus and place selection caret");

        await RouteKeyAsync(surface, owner, Code.Up, Modifiers.Shift);

        var (selection, selectedText) = await surface.Application.Dispatcher.InvokeAsync(
            () => (owner.TextSelection, owner.SelectedText),
            TestContext.Current.CancellationToken);
        selection.ShouldBe(new Selection(3, 1));
        selectedText.ShouldBe("bc");
    }

    /// <summary>Verifies common Home and End selection commands use visual rather than semantic lines.</summary>
    [Fact]
    public async Task Keyboard_WhenShiftHomeIsPressed_ExtendsToVisualLineStartAsync()
    {
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { new ControlText("ab"), new ControlText("cd") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(2, 2),
            TestContext.Current.CancellationToken);
        await surface.UpdateAsync(
            () =>
            {
                owner.Focus().ShouldBeTrue();
                owner.SetTextSelection(new Selection(3, 3));
            },
            "focus and place selection caret");

        await RouteKeyAsync(surface, owner, Code.Home, Modifiers.Shift);

        var selection = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.TextSelection,
            TestContext.Current.CancellationToken);
        selection.ShouldBe(new Selection(3, 2));
    }

    /// <summary>Verifies an ordinary selection-enabled container owns bounded edge autoscroll.</summary>
    [Fact]
    public async Task Pointer_WhenHeldBelowCommonSelectionOwner_AutoScrollsAfterIntervalAsync()
    {
        var owner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            AutoScroll = true,
            ScrollBars = ScrollBars.Vertical,
            Height = Length.Cells(2),
            Children =
            {
                new ControlText("aa"),
                new ControlText("bb"),
                new ControlText("cc"),
                new ControlText("dd")
            }
        };
        var time = new ManualTimeProvider();
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(4, 2),
            time,
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(owner.Children[0], new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(1, 0));
        await surface.Pointer.MovePressedToAsync(new Point(1, 2));

        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(49), "wait below common selection viewport");
        owner.VerticalOffset.ShouldBe(0);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(1), "complete common selection autoscroll interval");

        owner.VerticalOffset.ShouldBe(1);
        await surface.Pointer.ReleaseAsync();
    }

    /// <summary>Verifies the closest enabled owner wins without mutating an enabled ancestor.</summary>
    [Fact]
    public async Task Pointer_WhenEnabledOwnersAreNested_SelectsOnlyNearestOwnerAsync()
    {
        var text = new ControlText("inner");
        var inner = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Orientation = Orientation.Horizontal,
            Children = { text }
        };
        var outer = new Stack
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Children = { inner, new ControlText("outer") }
        };
        await using var surface = await ComponentSurface.MountAsync(
            outer,
            new Size(10, 2),
            TestContext.Current.CancellationToken);

        await surface.Pointer.MoveToAsync(text, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(text, new Point(4, 0));
        await surface.Pointer.ReleaseAsync();

        var (innerSelection, outerSelection) = await surface.Application.Dispatcher.InvokeAsync(
            () => (Inner: inner.TextSelection, Outer: outer.TextSelection),
            TestContext.Current.CancellationToken);
        innerSelection.ShouldBe(new Selection(0, 4));
        outerSelection.ShouldBe(default);
    }

    private static Task RouteKeyAsync(
        ComponentSurface surface,
        ControlBase target,
        Code code,
        Modifiers modifiers)
    {
        var eventArgs = new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            modifiers,
            KeyAction.Press));
        return surface.UpdateAsync(
            () => _ = Router.Route(target, Events.Key, eventArgs),
            $"press {modifiers}+{code}");
    }
}
