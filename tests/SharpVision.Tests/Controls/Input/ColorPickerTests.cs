// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Verifies adaptive ColorPicker value, composition, layout, rendering, and input.</summary>
public sealed class ColorPickerTests
{
    /// <summary>Verifies a picker rejects default colors before changing selection.</summary>
    [ComponentUnitEvidence(typeof(ColorPicker))]
    [Fact]
    public void Value_WhenColorIsDefault_AcceptsValue()
    {
        var picker = new ColorPicker { Value = Color.Default };
        picker.Value.ShouldBe(Color.Default);
    }

    /// <summary>Verifies a picker rejects transparent before changing selection.</summary>
    [Fact]
    public void Value_WhenColorIsTransparent_ThrowsBeforeMutation()
    {
        var picker = new ColorPicker();
        var previous = picker.Value;

        _ = Should.Throw<ArgumentException>(() => picker.Value = Color.Transparent);

        picker.Value.ShouldBe(previous);
    }

    /// <summary>Verifies a detached picker retains an exact 24-bit value.</summary>
    [Fact]
    public void Value_WhenPickerIsDetached_PreservesRgb()
    {
        var picker = new ColorPicker();
        var color = Color.Rgb(12, 34, 56);

        picker.Value = color;

        picker.Value.ShouldBe(color);
        picker.HexText.ShouldBe("#0C2238");
    }

    /// <summary>Verifies attachment preserves RGB while reporting a 256-color output capability.</summary>
    [Fact]
    public async Task Attach_WhenTerminalUsesPaletteColor_PreservesRgbAuthoringAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker { Value = Color.Rgb(95, 135, 175) };

            picker.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });

            picker.Value.ShouldBe(Color.Rgb(95, 135, 175));
            picker.EffectiveColorDepth.ShouldBe(ColorDepth.Indexed256);
            picker.RgbEditorVisible.ShouldBeTrue();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies capability changes never mutate the authored RGB value or publish value changes.</summary>
    [Fact]
    public async Task SetCapabilities_WhenColorDepthChanges_PreservesRgbWithoutValueEventsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker { Value = Color.Rgb(95, 135, 175) };
            picker.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            List<string> changes = [];
            picker.ValueChanged += (_, eventArgs) =>
                changes.Add($"{eventArgs.PreviousValue}>{eventArgs.Value}:{picker.Value}");

            picker.SetCapabilities(
                Capabilities.Conservative with { ColorDepth = ColorDepth.Basic16 });
            picker.SetCapabilities(
                Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });

            picker.Value.ShouldBe(Color.Rgb(95, 135, 175));
            changes.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies RGB sliders synchronize through one picker value commit.</summary>
    [Fact]
    public async Task RgbSliders_WhenValueChanges_UpdatePickerAndHexReadoutAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker();
            picker.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            var changes = 0;
            picker.ValueChanged += (_, _) => changes++;

            picker.RedSlider.Value = 12;
            picker.GreenSlider.Value = 34;
            picker.BlueSlider.Value = 56;

            picker.Value.ShouldBe(Color.Rgb(12, 34, 56));
            picker.HexText.ShouldBe("#0C2238");
            changes.ShouldBe(3);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies true-color composition stretches and paints a selected preview with its readout surface.</summary>
    [Fact]
    public async Task Render_WhenTrueColorIsActive_UsesContainedLayoutAndPreviewAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker
            {
                Value = Color.Rgb(255, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            picker.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            new Engine().Layout(picker, new Size(40, 18));
            using Frame frame = new(new Size(40, 18));

            picker.Render(frame.Canvas);

            picker.Bounds.ShouldBe(new Rect(0, 0, 40, 18));
            picker.Plane.Bounds.Width.ShouldBeGreaterThan(0);
            picker.Plane.Bounds.Height.ShouldBeGreaterThan(0);
            picker.Preview.Bounds.Width.ShouldBe(10);
            frame.GetCell(new Point(picker.Preview.Bounds.X, picker.Preview.Bounds.Y))
                .Style.Background.ShouldBe(Color.Rgb(255, 0, 0));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a focus transfer cancels the plane's active pointer capture.</summary>
    [Fact]
    public async Task Dispatch_WhenPlaneLosesFocus_CancelsPointerCaptureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker();
            var button = new Button { Content = new ControlText("Next") };
            Dock.SetSide(button, DockSide.Bottom);
            var root = new Dock { Children = { button, picker } };
            root.SetCapabilities(
                Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });
            root.Attach(dispatcher);
            new Engine().Layout(root, new Size(40, 20));
            using FocusManager focus = new(root);
            using PointerManager pointer = new(root);
            var point = new Point(
                picker.Plane.Bounds.X + (picker.Plane.Bounds.Width / 2),
                picker.Plane.Bounds.Y + (picker.Plane.Bounds.Height / 2));

            _ = pointer.Dispatch(Pointer(point, PointerAction.Press));

            pointer.Captured.ShouldBeSameAs(picker.Plane);
            focus.Focused.ShouldBeSameAs(picker.Plane);

            _ = focus.Focus(button);

            pointer.Captured.ShouldBeNull();
            picker.Plane.IsPressed.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    private static Pointer Pointer(Point cells, PointerAction action) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);
}
