// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Proves ColorPicker behavior through retained mounted composition and routed input.</summary>
public sealed class ColorPickerSurfaceTests
{
    /// <summary>Verifies mounted composition, hover, exclusion policy, selection, and cleanup.</summary>
    [Fact]
    [ComponentBehaviorEvidence(
        typeof(ColorPicker),
        ComponentBehavior.Mounted |
        ComponentBehavior.Hover |
        ComponentBehavior.FocusExcluded |
        ComponentBehavior.TabExcluded |
        ComponentBehavior.DirectionalExcluded |
        ComponentBehavior.PressReleaseExcluded |
        ComponentBehavior.Activation |
        ComponentBehavior.UnavailableCleanup |
        ComponentBehavior.Composition)]
    public async Task Surface_WhenPlaneIsSelected_ExposesAdaptiveCompositeBehaviorAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            picker.SetCapabilities(
                Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            picker.Attach(dispatcher);
            new Engine().Layout(picker, new Size(40, 18));
            using FocusManager focus = new(picker);
            using PointerManager pointer = new(picker);
            using Frame frame = new(new Size(40, 18));
            var point = new Point(
                picker.Plane.Bounds.X + (picker.Plane.Bounds.Width / 2),
                picker.Plane.Bounds.Y + (picker.Plane.Bounds.Height / 2));
            var changes = 0;
            picker.ValueChanged += (_, _) => changes++;

            picker.OwnedControlCount.ShouldBe(1);
            picker.CanFocus.ShouldBeFalse();
            picker.IsTabStop.ShouldBeFalse();
            _ = pointer.Dispatch(Pointer(point, PointerAction.Move, Buttons.None));
            picker.IsPointerOver.ShouldBeTrue();

            _ = pointer.Dispatch(Pointer(point, PointerAction.Press, Buttons.Primary));
            focus.Focused.ShouldBeSameAs(picker.Plane);
            pointer.Captured.ShouldBeSameAs(picker.Plane);
            picker.IsPressed.ShouldBeFalse();
            changes.ShouldBe(1);

            picker.Render(frame.Canvas);
            frame.GetCell(new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y))
                .Style.Background.ShouldBe(picker.Value);

            picker.IsEnabled = false;
            pointer.Captured.ShouldBeNull();
            picker.Plane.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

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
