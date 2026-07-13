// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

using SharpVision.Terminal.Input;


/// <summary>Exercises hostile fixed-seed tree, state, layout, focus, and capture mutation.</summary>
public sealed class RandomizedLayoutTests
{
    private const int _caseCount = 2_000;
    private const int _seed = 0x51A4_7001;

    /// <summary>Verifies every mutation preserves ownership, geometry, and wide-cell invariants.</summary>
    [Fact]
    public async Task Layout_WhenTreeMutatesRandomly_PreservesInfrastructureInvariantsAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() => Run(dispatcher), TestContext.Current.CancellationToken);
    }

    private static void Run(Dispatcher dispatcher)
    {
        Random random = new(_seed);
        ProbeContainer root = new()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        root.Attach(dispatcher);
        using FocusManager focus = new(root);
        using CaptureManager capture = new(root);
        List<ProbeControl> controls = [];
        Engine engine = new();
        Size size = new(80, 24);

        for (int sample = 0; sample < _caseCount; sample++)
        {
            int operation = random.Next(0, 10);
            Apply(operation, random, root, controls, focus, capture, ref size);
            engine.Layout(root, size);

            foreach (ProbeControl control in controls)
            {
                engine.Layout(control, size);
            }

            AssertInvariants(sample, operation, root, controls, focus, capture, size);

            if (sample % 25 == 0)
            {
                using Frame frame = new(size);
                root.Render(frame.Canvas);
                AssertWideOwnership(sample, operation, frame);
            }
        }

        root.Dispose();
    }

    private static void Apply(
        int operation,
        Random random,
        ProbeContainer root,
        List<ProbeControl> controls,
        FocusManager focus,
        CaptureManager capture,
        ref Size size)
    {
        ProbeControl? control = controls.Count == 0 ? null : controls[random.Next(controls.Count)];

        switch (operation)
        {
            case 0 when controls.Count < 32:
                ProbeControl added = new(new Size(random.Next(0, 20), random.Next(0, 8)))
                {
                    CanFocus = random.Next(0, 2) == 0,
                    Content = random.Next(0, 2) == 0 ? "界".AsMemory() : "x".AsMemory(),
                };
                root.Children.Add(added);
                controls.Add(added);
                break;
            case 1 when control is not null:
                _ = root.Children.Remove(control);
                _ = controls.Remove(control);
                control.Dispose();
                break;
            case 2 when control is not null:
                control.Width = NextLength(random);
                control.Height = NextLength(random);
                break;
            case 3 when control is not null:
                control.Margin = new Thickness(random.Next(0, 5));
                control.Padding = new Thickness(random.Next(0, 4));
                break;
            case 4 when control is not null:
                control.Visibility = (Visibility) random.Next(0, 3);
                break;
            case 5 when control is not null:
                control.IsEnabled = !control.IsEnabled;
                break;
            case 6 when control is not null:
                if (control.CanFocus && control.EffectiveIsVisible && control.EffectiveIsEnabled)
                {
                    _ = focus.Focus(control);
                }

                break;
            case 7 when control is not null:
                if (control.EffectiveIsVisible && control.EffectiveIsEnabled)
                {
                    _ = capture.Capture(control);
                }

                break;
            case 8:
                size = new Size(random.Next(0, 241), random.Next(0, 81));
                break;
            case 9:
                _ = capture.Dispatch(new Pointer(
                    new Point(
                        size.Width == 0 ? 0 : random.Next(size.Width),
                        size.Height == 0 ? 0 : random.Next(size.Height)),
                    pixels: null,
                    Buttons.Primary,
                    random.Next(0, 2) == 0 ? PointerAction.Move : PointerAction.Press,
                    wheelX: 0,
                    wheelY: 0,
                    Modifiers.None,
                    isMotion: true,
                    isCellPositionInferred: false));
                break;
            default:
                break;
        }
    }

    private static void AssertInvariants(
        int sample,
        int operation,
        ProbeContainer root,
        List<ProbeControl> controls,
        FocusManager focus,
        CaptureManager capture,
        Size size)
    {
        string context = $"seed=0x{_seed:X8}, case={sample}, operation={operation}, size={size}";
        root.Bounds.Width.ShouldBe(size.Width, context);
        root.Bounds.Height.ShouldBe(size.Height, context);
        root.Children.Count.ShouldBe(controls.Count, context);

        foreach (ProbeControl control in controls)
        {
            control.Parent.ShouldBeSameAs(root, context);
            control.Dispatcher.ShouldBeSameAs(root.Dispatcher, context);
            control.Bounds.X.ShouldBeGreaterThanOrEqualTo(0, context);
            control.Bounds.Y.ShouldBeGreaterThanOrEqualTo(0, context);
            control.Bounds.Right.ShouldBeLessThanOrEqualTo(root.Bounds.Right, context);
            control.Bounds.Bottom.ShouldBeLessThanOrEqualTo(root.Bounds.Bottom, context);
        }

        if (focus.Focused is { } focused)
        {
            controls.ShouldContain(focused, context);
            focused.IsFocused.ShouldBeTrue(context);
            focused.EffectiveIsEnabled.ShouldBeTrue(context);
            focused.EffectiveIsVisible.ShouldBeTrue(context);
        }

        if (capture.Captured is { } captured)
        {
            controls.ShouldContain(captured, context);
            captured.EffectiveIsEnabled.ShouldBeTrue(context);
            captured.EffectiveIsVisible.ShouldBeTrue(context);
        }
    }

    private static void AssertWideOwnership(int sample, int operation, Frame frame)
    {
        string context = $"seed=0x{_seed:X8}, case={sample}, operation={operation}";

        for (int y = 0; y < frame.Size.Height; y++)
        {
            for (int x = 0; x < frame.Size.Width; x++)
            {
                Point point = new(x, y);
                CellInfo cell = frame.GetCell(point);

                if (!cell.IsContinuation)
                {
                    continue;
                }

                cell.Lead.X.ShouldBeLessThan(x, context);
                CellInfo lead = frame.GetCell(cell.Lead);
                lead.IsContinuation.ShouldBeFalse(context);
                lead.Width.ShouldBe(2, context);
            }
        }
    }

    private static Length NextLength(Random random) => random.Next(0, 4) switch
    {
        0 => Length.Auto,
        1 => Length.Cells(random.Next(0, 101)),
        2 => Length.Percent(random.NextDouble() * 100),
        _ => Length.Star(random.NextDouble() + 0.01),
    };
}
