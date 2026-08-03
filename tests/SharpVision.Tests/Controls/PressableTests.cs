// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies shared keyboard, pointer, focus, and cancellation activation behavior.</summary>
public sealed class PressableTests
{
    /// <summary>Verifies Space holds until matching release and Enter activates directly.</summary>
    [Fact]
    public void Dispatch_WhenKeyboardActivates_UsesExactTransitionAndCause()
    {
        var control = new ProbePressable();

        Key(control, Code.Character, new Rune(' '), KeyAction.Press);
        control.IsPressed.ShouldBeTrue();
        control.Activations.ShouldBeEmpty();
        Key(control, Code.Character, new Rune(' '), KeyAction.Repeat);
        Key(control, Code.Character, new Rune(' '), KeyAction.Release);
        Key(control, Code.Enter, character: null, KeyAction.Press);

        control.IsPressed.ShouldBeFalse();
        control.Activations.ShouldBe([
            ActivationCause.Keyboard,
            ActivationCause.Keyboard
        ]);
        control.CanFocus.ShouldBeTrue();
    }

    /// <summary>Verifies primary pointer press focuses, captures, and activates once inside.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerReleasesInside_ActivatesOnceAndReleasesCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 8) };
            var control = new ProbePressable { Bounds = new Rect(2, 2, 8, 3) };
            root.Children.Add(control);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);

            _ = capture.Dispatch(Pointer(new Point(3, 3), PointerAction.Press));
            control.IsPressed.ShouldBeTrue();
            capture.Captured.ShouldBeSameAs(control);
            focus.Focused.ShouldBeSameAs(control);
            _ = capture.Dispatch(Pointer(new Point(3, 3), PointerAction.Release));

            control.Activations.ShouldBe([ActivationCause.Pointer]);
            control.IsPressed.ShouldBeFalse();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies moving and releasing outside cancels without activation.</summary>
    [Fact]
    public async Task Dispatch_WhenCapturedPointerLeaves_CancelsPressedActivationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 8) };
            var control = new ProbePressable { Bounds = new Rect(2, 2, 8, 3) };
            root.Children.Add(control);
            root.Attach(dispatcher);
            using PointerManager capture = new(root);

            _ = capture.Dispatch(Pointer(new Point(3, 3), PointerAction.Press));
            _ = capture.Dispatch(Pointer(new Point(15, 7), PointerAction.Move));
            control.IsPressed.ShouldBeFalse();
            _ = capture.Dispatch(Pointer(new Point(15, 7), PointerAction.Release));

            control.Activations.ShouldBeEmpty();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies disable, hide, and detach clear held state without activation.</summary>
    [Fact]
    public async Task Dispatch_WhenControlBecomesUnavailable_ClearsEveryHeldStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 8) };
            var control = new ProbePressable { Bounds = new Rect(2, 2, 8, 3) };
            var other = new ProbePressable { Bounds = new Rect(12, 2, 6, 3) };
            root.Children.Add(control);
            root.Children.Add(other);
            root.Attach(dispatcher);
            using FocusManager focus = new(root);
            using PointerManager capture = new(root);
            focus.Focus(control).ShouldBeTrue();
            Key(control, Code.Character, new Rune(' '), KeyAction.Press);

            focus.Focus(other).ShouldBeTrue();
            control.IsPressed.ShouldBeFalse();
            focus.Focus(control).ShouldBeTrue();
            Key(control, Code.Character, new Rune(' '), KeyAction.Press);

            control.IsEnabled = false;

            control.IsPressed.ShouldBeFalse();
            control.Activations.ShouldBeEmpty();
            focus.Focused.ShouldBeNull();
            control.IsEnabled = true;
            _ = capture.Dispatch(Pointer(new Point(3, 3), PointerAction.Press));
            _ = root.Children.Remove(control);
            control.IsPressed.ShouldBeFalse();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies secondary pointer transitions never capture or activate.</summary>
    [Fact]
    public async Task Dispatch_WhenPointerIsNotPrimary_IgnoresTransitionAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 20, 8) };
            var control = new ProbePressable { Bounds = new Rect(2, 2, 8, 3) };
            root.Children.Add(control);
            root.Attach(dispatcher);
            using PointerManager capture = new(root);

            _ = capture.Dispatch(Pointer(
                new Point(3, 3),
                PointerAction.Press,
                Buttons.Secondary));

            capture.Captured.ShouldBeNull();
            control.Activations.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a content-originated route focuses and captures the semantic PressableBase owner itself.</summary>
    [Fact]
    public async Task Route_WhenContentIsOriginalPointerTarget_PressableOwnsFocusCaptureAndActivationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var content = new ControlText("Go");
            var control = new Button { Content = content };
            new LayoutEngine().Layout(control, new Size(8, 3));
            control.Attach(dispatcher);
            using FocusManager focus = new(control);
            using PointerManager capture = new(control);
            var point = new Point(content.Bounds.X, content.Bounds.Y);

            _ = Router.Route(content, Events.Pointer, new PointerEventArgs(Pointer(point, PointerAction.Press)));

            focus.Focused.ShouldBeSameAs(control);
            capture.Captured.ShouldBeSameAs(control);
            control.IsPressed.ShouldBeTrue();

            _ = Router.Route(content, Events.Pointer, new PointerEventArgs(Pointer(point, PointerAction.Release)));

            capture.Captured.ShouldBeNull();
            control.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies terminal focus loss clears shared held state before the protected cancellation callback.</summary>
    [Fact]
    public async Task TerminalFocusLost_WhenPointerIsHeld_CancelsBeforeCallbackAndLaterReleaseDoesNotActivateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var control = new ProbePressable { Bounds = new Rect(0, 0, 8, 3) };
            control.Attach(dispatcher);
            using PointerManager capture = new(control);
            _ = capture.Dispatch(Pointer(new Point(1, 1), PointerAction.Press));

            capture.TerminalFocusLost();
            _ = capture.Dispatch(Pointer(new Point(1, 1), PointerAction.Release));

            control.CaptureCancellations.ShouldBe([PointerCaptureLossReason.TerminalFocusLost]);
            control.HadCaptureDuringCancellation.ShouldBeFalse();
            control.WasPressedDuringCancellation.ShouldBeFalse();
            control.Activations.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a consumer-derived PressableBase's activation is recorded before command execution.</summary>
    [Fact]
    public void Dispatch_WhenConsumerDerivedPressableHasCommand_ActivatesBeforeExecute()
    {
        var parameter = new object();
        var activationCountDuringExecute = -1;
        ProbePressable control = null!;
        var command = new ProbeCommand { Executing = _ => activationCountDuringExecute = control.Activations.Count };
        control = new ProbePressable { Command = command, CommandParameter = parameter };

        Key(control, Code.Enter, character: null, KeyAction.Press);

        activationCountDuringExecute.ShouldBe(1);
        control.Activations.ShouldBe([ActivationCause.Keyboard]);
        command.Queries.ShouldBe([parameter]);
        command.Executions.ShouldBe([parameter]);
    }

    /// <summary>Verifies a command that cannot execute suppresses both activation and execution.</summary>
    [Fact]
    public void Dispatch_WhenCommandCanExecuteIsFalse_RaisesNoActivationAndDoesNotExecute()
    {
        var command = new ProbeCommand { CanExecuteValue = false };
        var control = new ProbePressable { Command = command };

        Key(control, Code.Enter, character: null, KeyAction.Press);

        control.Activations.ShouldBeEmpty();
        command.Executions.ShouldBeEmpty();
    }

    /// <summary>Verifies replacing Command unsubscribes the previous instance's CanExecuteChanged
    /// and subscribes the new one, so only the currently assigned command can invalidate render.</summary>
    [Fact]
    public void Command_WhenReplaced_UnsubscribesPreviousAndSubscribesNew()
    {
        var previous = new ProbeCommand();
        var next = new ProbeCommand();
        var control = new ProbePressable { Command = previous };
        previous.HasCanExecuteChangedSubscribers.ShouldBeTrue();

        control.Command = next;

        previous.HasCanExecuteChangedSubscribers.ShouldBeFalse();
        next.HasCanExecuteChangedSubscribers.ShouldBeTrue();
    }

    /// <summary>Verifies disposing a PressableBase with an assigned Command unsubscribes
    /// CanExecuteChanged exactly once, leaving no dangling subscription.</summary>
    [Fact]
    public async Task Dispose_WhenCommandIsAssigned_UnsubscribesCanExecuteChangedAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var command = new ProbeCommand();
            var control = new ProbePressable { Command = command };
            control.Attach(dispatcher);

            control.Dispose();

            command.HasCanExecuteChangedSubscribers.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    private static void Key(
        ProbePressable control,
        Code code,
        Rune? character,
        KeyAction action) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character,
                nativeCode: 0,
                Modifiers.None,
                action)));

    private static Pointer Pointer(
        Point cells,
        PointerAction action,
        Buttons buttons = Buttons.Primary) => new(
        cells,
        pixels: null,
        buttons,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);
}
