// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves Slider behavior through an attached layout, input managers, and semantic frame.</summary>
public sealed class SliderSurfaceTests
{
    /// <summary>Verifies mounted hover, focus, Tab, keys, press, selection, and unavailable cleanup.</summary>
    [Fact]
    [ComponentBehaviorEvidence(
        typeof(Slider),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.Focus |
        ComponentBehavior.Tab |
        ComponentBehavior.Directional |
        ComponentBehavior.PressRelease |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup)]
    public async Task Surface_WhenInputIsDispatched_ExposesCompleteSliderBehaviorAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var slider = new Slider
            {
                Maximum = 100,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var root = new Stack { Children = { slider } };
            root.Attach(dispatcher);
            new Engine().Layout(root, new Size(11, 1));
            using FocusManager focus = new(root);
            using PointerManager pointer = new(root);
            using Frame frame = new(new Size(11, 1));
            var changes = 0;
            slider.ValueChanged += (_, _) => changes++;

            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Move, Buttons.None));
            slider.IsPointerOver.ShouldBeTrue();
            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(slider);
            RouteKey(slider, Code.Right);
            slider.Value.ShouldBe(1);

            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Press, Buttons.Primary));
            slider.IsPressed.ShouldBeTrue();
            pointer.Captured.ShouldBeSameAs(slider);
            slider.Value.ShouldBe(50);
            changes.ShouldBe(2);
            _ = pointer.Dispatch(Pointer(new Point(5, 0), PointerAction.Release, Buttons.Primary));
            slider.IsPressed.ShouldBeFalse();
            pointer.Captured.ShouldBeNull();

            slider.Render(frame.Canvas);
            FrameOracle.Get(frame, new Point(5, 0)).ShouldBe("◆");

            _ = pointer.Dispatch(Pointer(new Point(6, 0), PointerAction.Press, Buttons.Primary));
            slider.IsEnabled = false;
            pointer.Captured.ShouldBeNull();
            slider.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    private static void RouteKey(Control control, Code code) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

    private static Pointer Pointer(Point cells, PointerAction action, Buttons buttons) => new(
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
