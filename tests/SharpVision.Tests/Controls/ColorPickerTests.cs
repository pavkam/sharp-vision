// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Verifies adaptive ColorPicker value, composition, layout, rendering, and input.</summary>
public sealed class ColorPickerTests
{
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

    /// <summary>Verifies attachment projects RGB through the active indexed palette.</summary>
    [Fact]
    public async Task Attach_WhenTerminalUsesIndexedColor_CommitsNearestPaletteEntryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker { Value = Color.Rgb(95, 135, 175) };

            picker.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });

            picker.Value.ShouldBe(Color.Indexed(67));
            picker.EffectiveColorDepth.ShouldBe(ColorDepth.Indexed256);
            picker.IndexedPaletteVisible.ShouldBeTrue();
            picker.TrueColorVisible.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a downgrade is lossy and a later upgrade does not resurrect RGB.</summary>
    [Fact]
    public async Task SetCapabilities_WhenColorDepthChanges_NormalizesWithoutResurrectionAsync()
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
            var downgraded = picker.Value;
            picker.SetCapabilities(
                Capabilities.Conservative with { ColorDepth = ColorDepth.TrueColor });

            downgraded.ShouldBe(Color.Indexed(8));
            picker.Value.ShouldBe(Color.Rgb(127, 127, 127));
            changes.Count.ShouldBe(2);
            changes[0].ShouldEndWith($":{downgraded}");
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

    /// <summary>Verifies true-color composition stretches and paints a selected preview.</summary>
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
                VerticalAlignment = VerticalAlignment.Stretch,
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
            picker.Preview.Bounds.Width.ShouldBeGreaterThan(0);
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
            Dock.SetSide(button, Side.Bottom);
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

    /// <summary>Verifies indexed palette arrows move by one swatch and one complete row.</summary>
    [Fact]
    public async Task Dispatch_WhenIndexedPaletteReceivesKeys_UpdatesPickerValueAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker { Value = Color.Indexed(17) };
            picker.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });

            Key(picker.IndexedGrid, Code.Right);
            picker.Value.ShouldBe(Color.Indexed(18));
            Key(picker.IndexedGrid, Code.Down);

            picker.Value.ShouldBe(Color.Indexed(34));
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a basic palette pointer press selects the mapped corner swatch.</summary>
    [Fact]
    public async Task Dispatch_WhenBasicPaletteIsPressed_SelectsMappedSwatchAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var picker = new ColorPicker();
            picker.Attach(
                dispatcher,
                Policy.Default,
                Capabilities.Conservative with { ColorDepth = ColorDepth.Basic16 });
            new Engine().Layout(picker, new Size(8, 4));
            using PointerManager pointer = new(picker);
            var bounds = picker.BasicGrid.Bounds;

            _ = pointer.Dispatch(Pointer(
                new Point(bounds.Right - 1, bounds.Bottom - 1),
                PointerAction.Press));

            picker.Value.ShouldBe(Color.Indexed(15));
            pointer.Captured.ShouldBeSameAs(picker.BasicGrid);
        }, TestContext.Current.CancellationToken);
    }

    private static void Key(Control control, Code code) =>
        Router.Route(
            control,
            Events.Key,
            new KeyEventArgs(new Stroke(
                code,
                character: null,
                nativeCode: 0,
                Modifiers.None,
                KeyAction.Press)));

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
