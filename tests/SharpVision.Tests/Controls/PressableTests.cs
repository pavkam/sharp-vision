namespace SharpVision.Tests.Controls;

using System.Text;

using SharpVision.Input;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Input;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

using KeyAction = Terminal.Input.Action;

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
            ActivationCause.Keyboard,
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
            using var focus = new FocusManager(root);
            using var capture = new CaptureManager(root);

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
            using var capture = new CaptureManager(root);

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
            using var focus = new FocusManager(root);
            using var capture = new CaptureManager(root);
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
            using var capture = new CaptureManager(root);

            _ = capture.Dispatch(Pointer(
                new Point(3, 3),
                PointerAction.Press,
                Buttons.Secondary));

            capture.Captured.ShouldBeNull();
            control.Activations.ShouldBeEmpty();
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
