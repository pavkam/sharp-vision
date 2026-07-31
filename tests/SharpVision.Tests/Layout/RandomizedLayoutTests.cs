// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

/// <summary>Exercises hostile fixed-seed tree, state, layout, focus, and capture mutation.</summary>
public sealed class RandomizedLayoutTests
{
    private const int _caseCount = 2_000;
    private const int _seed = 0x51A4_7001;

    /// <summary>Verifies every mutation preserves ownership, geometry, and wide-cell invariants.</summary>
    [Fact]
    public async Task Layout_WhenTreeMutatesRandomly_PreservesInfrastructureInvariantsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() => Run(dispatcher), TestContext.Current.CancellationToken);
    }

    private static void Run(Dispatcher dispatcher)
    {
        var random = new Random(_seed);
        var root = new ProbeContainer
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        root.Attach(dispatcher);
        using FocusManager focus = new(root);
        using PointerManager capture = new(root);
        List<ProbeControl> controls = [];
        var engine = new LayoutEngine();
        var size = new Size(80, 24);

        for (var sample = 0; sample < _caseCount; sample++)
        {
            var operation = random.Next(0, 13);
            Apply(operation, random, root, controls, focus, capture, ref size);
            engine.Layout(root, size);

            foreach (var control in controls)
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
        PointerManager capture,
        ref Size size)
    {
        var control = controls.Count == 0 ? null : controls[random.Next(controls.Count)];

        switch (operation)
        {
            case 0 when controls.Count < 32:
                var added = new ProbeControl(new Size(random.Next(0, 20), random.Next(0, 8)))
                {
                    Focusable = random.Next(0, 2) == 0,
                    Content = random.Next(0, 2) == 0 ? "界".AsMemory() : "x".AsMemory()
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
                if (control is { CanFocus: true, EffectiveIsVisible: true, EffectiveIsEnabled: true })
                {
                    _ = focus.Focus(control);
                }

                break;
            case 7 when control is not null:
                if (control is { EffectiveIsVisible: true, EffectiveIsEnabled: true })
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
            case 10 when control is not null:
                control.Dispose();
                _ = controls.Remove(control);
                break;
            case 11 when control is not null:
                var replacement = new ProbeControl(new Size(random.Next(0, 20), random.Next(0, 8)))
                {
                    Focusable = random.Next(0, 2) == 0,
                    Content = random.Next(0, 2) == 0 ? "界".AsMemory() : "r".AsMemory()
                };
                var index = controls.IndexOf(control);
                root.Children[index] = replacement;
                controls[index] = replacement;
                control.Dispose();
                break;
            case 12 when controls.Count > 0:
                var removed = controls.ToArray();
                root.Children.Clear();
                controls.Clear();

                foreach (var item in removed)
                {
                    item.Dispose();
                }

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
        PointerManager capture,
        Size size)
    {
        var context = $"seed=0x{_seed:X8}, case={sample}, operation={operation}, size={size}";
        root.Bounds.Width.ShouldBe(size.Width, context);
        root.Bounds.Height.ShouldBe(size.Height, context);
        root.Children.Count.ShouldBe(controls.Count, context);

        foreach (var control in controls)
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
        var context = $"seed=0x{_seed:X8}, case={sample}, operation={operation}";

        for (var y = 0; y < frame.Size.Height; y++)
        {
            for (var x = 0; x < frame.Size.Width; x++)
            {
                var point = new Point(x, y);
                var cell = frame.GetCell(point);

                if (!cell.IsContinuation)
                {
                    continue;
                }

                cell.Lead.X.ShouldBeLessThan(x, context);
                var lead = frame.GetCell(cell.Lead);
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
        _ => Length.Star(random.NextDouble() + 0.01)
    };
}
