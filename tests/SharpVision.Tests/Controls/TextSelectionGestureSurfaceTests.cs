// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies inherited cross-child text selection through mounted input and final cells.</summary>
public sealed class TextSelectionGestureSurfaceTests
{
    /// <summary>Verifies focus loss cancels the framework gesture before invoking a component hook
    /// that deliberately omits its base call.</summary>
    [Fact]
    public async Task Focus_WhenSelectableOverrideOmitsBase_CancelsGestureBeforeComponentCallbackAsync()
    {
        var owner = new TextSelectionLifecycleProbe("abcd")
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Height = Length.Cells(1),
        };
        var next = new Button { Text = "Next", Height = Length.Cells(1) };
        var root = new Stack { Children = { owner, next } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(owner.Text, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(3, 0));
        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Selecting);

        await surface.UpdateAsync(() => next.Focus().ShouldBeTrue(), "move focus away from selecting owner");

        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
        owner.FocusChangedCalls.ShouldBe(2);
        owner.FocusCleanupWasCommitted.ShouldBeTrue();
        surface.ShouldHaveCapture(null);
        await surface.Pointer.ReleaseAsync();
        await surface.UpdateAsync(() => owner.Focus().ShouldBeTrue(), "refocus selection owner");
        await surface.Pointer.MoveToAsync(owner.Text, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(2, 0));
        await surface.Pointer.ReleaseAsync();
        var selectedText = await surface.Application.Dispatcher.InvokeAsync(
            () => owner.SelectedText,
            TestContext.Current.CancellationToken);
        selectedText.ShouldBe("ab");
    }

    /// <summary>Verifies ordinary pointer-capture loss cancels the framework gesture before a
    /// component hook that deliberately omits its base call.</summary>
    [Fact]
    public async Task Capture_WhenSelectableOverrideOmitsBase_CancelsGestureBeforeComponentCallbackAsync()
    {
        var owner = new TextSelectionLifecycleProbe("abcd")
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
        };
        await using var surface = await ComponentSurface.MountAsync(
            owner,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(owner.Text, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(3, 0));
        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Selecting);

        await surface.UpdateAsync(owner.ReleaseProbePointer, "release selection capture");

        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
        owner.LostPointerCaptureCalls.ShouldBe(1);
        owner.CaptureCleanupWasCommitted.ShouldBeTrue();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies every availability transition cancels the framework gesture even when the
    /// component unavailability hook deliberately omits its base call.</summary>
    [Theory]
    [InlineData("hide")]
    [InlineData("disable")]
    [InlineData("detach")]
    [InlineData("dispose")]
    public async Task Availability_WhenSelectableOverrideOmitsBase_CancelsGestureAsync(string transition)
    {
        var owner = new TextSelectionLifecycleProbe("abcd")
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
        };
        var root = new Stack { Children = { owner } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(4, 1),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(owner.Text, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(3, 0));
        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Selecting);

        await surface.UpdateAsync(
            () =>
            {
                switch (transition)
                {
                    case "hide":
                        owner.Visibility = Visibility.Hidden;
                        break;
                    case "disable":
                        owner.IsEnabled = false;
                        break;
                    case "detach":
                        root.Children.Remove(owner).ShouldBeTrue();
                        break;
                    case "dispose":
                        owner.Dispose();
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown transition '{transition}'.");
                }
            },
            $"make selecting owner unavailable through {transition}");

        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
        owner.UnavailableCalls.ShouldBeGreaterThanOrEqualTo(1);
        owner.UnavailableCleanupWasCommitted.ShouldBeTrue();
        surface.ShouldHaveCapture(null);
    }

    /// <summary>Verifies component hook failures remain primary while framework selection cleanup
    /// finishes before focus, capture-loss, or unavailability callbacks.</summary>
    [Theory]
    [InlineData("focus")]
    [InlineData("capture")]
    [InlineData("unavailable")]
    public async Task Lifecycle_WhenSelectableOverrideThrows_PreservesFailureAfterFrameworkCleanupAsync(string transition)
    {
        var owner = new TextSelectionLifecycleProbe("abcd")
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Height = Length.Cells(1),
        };
        var next = new Button { Text = "Next", Height = Length.Cells(1) };
        var root = new Stack { Children = { owner, next } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(owner.Text, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(3, 0));

        Action transitionAction;
        string expectedMessage;
        switch (transition)
        {
            case "focus":
                owner.ThrowOnFocusChanged = true;
                transitionAction = () => next.Focus().ShouldBeTrue();
                expectedMessage = "The focus callback failed.";
                break;
            case "capture":
                owner.ThrowOnLostPointerCapture = true;
                transitionAction = owner.ReleaseProbePointer;
                expectedMessage = "The capture-loss callback failed.";
                break;
            case "unavailable":
                owner.ThrowOnUnavailable = true;
                transitionAction = () => owner.IsEnabled = false;
                expectedMessage = "The unavailable callback failed.";
                break;
            default:
                throw new InvalidOperationException($"Unknown transition '{transition}'.");
        }

        var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
            surface.UpdateAsync(transitionAction, $"fail {transition} component hook"));

        exception.Message.ShouldBe(expectedMessage);
        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
        surface.ShouldHaveCapture(null);
        owner.ThrowOnFocusChanged = false;
        owner.ThrowOnLostPointerCapture = false;
        owner.ThrowOnUnavailable = false;
    }

    /// <summary>Verifies a component focus hook may synchronously make the owner unavailable after
    /// framework cleanup without reviving or duplicating the selection gesture.</summary>
    [Fact]
    public async Task Focus_WhenSelectableOverrideReentersUnavailability_KeepsGestureCancelledAsync()
    {
        var owner = new TextSelectionLifecycleProbe("abcd")
        {
            IsFocusable = true,
            IsTextSelectionEnabled = true,
            Height = Length.Cells(1),
        };
        var next = new Button { Text = "Next", Height = Length.Cells(1) };
        var root = new Stack { Children = { owner, next } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(6, 2),
            TestContext.Current.CancellationToken);
        await surface.Pointer.MoveToAsync(owner.Text, new Point(0, 0));
        await surface.Pointer.PressAsync();
        await surface.Pointer.MovePressedToAsync(new Point(3, 0));
        owner.FocusChanging = (control, focused) =>
        {
            if (focused)
            {
                return;
            }

            control.FocusChanging = null;
            control.IsEnabled = false;
        };

        await surface.UpdateAsync(() => next.Focus().ShouldBeTrue(), "disable owner from focus callback");

        owner.IsEnabled.ShouldBeFalse();
        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
        owner.UnavailableCalls.ShouldBe(1);
        surface.ShouldHaveCapture(null);
    }

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
        await surface.UpdateAsync(() => owner.IsEnabled = false, "disable autoscrolling selection owner");
        owner.TextSelectionPhase.ShouldBe(TextSelectionGesturePhase.Idle);
        surface.ShouldHaveCapture(null);
        await surface.AdvanceAsync(TimeSpan.FromMilliseconds(200), "prove cancelled autoscroll timer stays retired");
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
