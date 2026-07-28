// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Performance;

using Label = ControlText;
using UiListView = ListView;

/// <summary>Gates interactive control allocation reuse and detached-item retention.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class InteractivePerformanceTests
{
    /// <summary>Verifies warmed interactive layout/render reuse at representative terminal sizes.</summary>
    [Theory]
    [InlineData(80, 24)]
    [InlineData(200, 60)]
    public void Render_WhenInteractiveTreeIsWarm_AllocatesNoManagedMemory(int width, int height)
    {
        var root = Representative();
        var size = new Size(width, height);
        var engine = new Engine();
        using Frame frame = new(size);
        Render();

        var allocated = Minimum(Render, iterations: 200, out var elapsed);

        allocated.ShouldBe(0);
        Report($"interactive {width}x{height}", elapsed, 200);
        return;

        void Render()
        {
            engine.Layout(root, size);
            frame.Clear();
            root.Invalidate(Invalidation.Render);
            root.Render(frame.Canvas);
        }
    }

    /// <summary>Verifies repeated public TextInput replacement stays within a finite per-edit budget.</summary>
    [Fact]
    public void Text_WhenRepeatedlyEdited_HasBoundedManagedAllocation()
    {
        var input = new TextInput { UndoLimit = 32 };
        var toggle = false;
        Edit();

        var allocated = Minimum(Edit, iterations: 1_000, out var elapsed);

        allocated.ShouldBeLessThanOrEqualTo(1_024L * 1_000);
        input.CanUndo.ShouldBeTrue();
        Report("1,000 text replacements", elapsed, 1_000);
        return;

        void Edit()
        {
            input.Text = toggle ? "e\u0301" : "界";
            toggle = !toggle;
        }
    }

    /// <summary>Verifies captured ScrollBar motion remains within a finite routed-event budget.</summary>
    [Fact]
    public async Task Dispatch_WhenThumbIsDragged_HasBoundedManagedAllocationAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var bar = new ScrollBar
            {
                Bounds = new Rect(0, 0, 102, 1),
                Orientation = Orientation.Horizontal,
                Maximum = 10_000
            };
            bar.Attach(dispatcher);
            using PointerManager capture = new(bar);
            _ = capture.Dispatch(Pointer(new Point(1, 0), PointerAction.Press));
            var position = 2;
            Drag();

            var allocated = Minimum(Drag, iterations: 1_000, out var elapsed);

            allocated.ShouldBeLessThanOrEqualTo(768L * 1_000);
            bar.Value.ShouldBeInRange(0, bar.Maximum);
            _ = capture.Dispatch(Pointer(new Point(position, 0), PointerAction.Release));
            Report("1,000 captured thumb moves", elapsed, 1_000);
            return;

            void Drag()
            {
                position = position == 100 ? 2 : position + 1;
                _ = capture.Dispatch(Pointer(new Point(position, 0), PointerAction.Move));
            }
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies 1,000-item unchanged layout reuse and complete detached-control collection.</summary>
    [Fact]
    public void Items_WhenOneThousandAreReplaced_AllocatesNoUnchangedLayoutAndRetainsNone()
    {
        var list = new UiListView();
        var weak = PopulateAndReplace(list, count: 1_000);
        var engine = new Engine();
        var size = new Size(200, 60);
        engine.Layout(list, size);

        var allocated = Minimum(() => engine.Layout(list, size), 1_000, out var elapsed);
        ForceCollection();

        allocated.ShouldBe(0);
        weak.Count(reference => reference.TryGetTarget(out _)).ShouldBe(0);
        list.Items.ShouldBeEmpty();
        Report("1,000-item replacement and unchanged layout", elapsed, 1_000);
    }

    /// <summary>Verifies repeated nested wheel routing stays within a finite command budget.</summary>
    [Fact]
    public void Dispatch_WhenNestedScrollCommandsRepeat_HasBoundedManagedAllocation()
    {
        var leaf = new Label(string.Join('\n', Enumerable.Range(0, 20))) { Width = Length.Cells(5) };
        var inner = Hidden(leaf);
        inner.Width = Length.Cells(5);
        inner.Height = Length.Cells(8);
        var outer = Hidden(inner);
        new Engine().Layout(outer, new Size(5, 4));
        var down = new PointerEventArgs(Pointer(default, PointerAction.Wheel, wheelY: -20));
        var up = new PointerEventArgs(Pointer(default, PointerAction.Wheel, wheelY: 20));
        var descending = true;
        Dispatch();

        var allocated = Minimum(Dispatch, iterations: 1_000, out var elapsed);

        allocated.ShouldBeLessThanOrEqualTo(256L * 1_000);
        Report("1,000 nested scroll commands", elapsed, 1_000);
        return;

        void Dispatch()
        {
            _ = Router.Route(leaf, Events.Pointer, descending ? down : up);
            descending = !descending;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<Control>[] PopulateAndReplace(UiListView list, int count)
    {
        var weak = new WeakReference<Control>[count];
        var created = 0;
        list.ItemTemplate = item =>
        {
            var control = new Label(Convert.ToString(item, CultureInfo.InvariantCulture) ?? string.Empty);
            weak[created++] = new WeakReference<Control>(control);
            return control;
        };
        list.Items = Enumerable.Range(0, count).Select(static value => (object?) value).ToArray();
        list.Items = Array.Empty<object?>();
        return weak;
    }

    private static Grid Representative()
    {
        var root = new Grid();
        root.Columns.Add(Track.Cells(24));
        root.Columns.Add(Track.Star(1));
        var list = new UiListView
        {
            Items = Enumerable.Range(0, 20).Select(static value => (object?) $"Item {value}").ToArray()
        };
        Grid.SetColumn(list, 0);
        var content = new Stack();
        Grid.SetColumn(content, 1);
        content.Children.Add(new TextInput { Text = "e\u0301 · 界 · 👩‍💻" });
        content.Children.Add(new ScrollBar
        {
            Orientation = Orientation.Horizontal,
            Maximum = 100,
            Value = 40,
            ViewportSize = 20
        });
        content.Children.Add(
            new Stack { AutoScroll = true, Children = { new Label("wide 界 and emoji 👩‍💻 content") } });
        root.Children.Add(list);
        root.Children.Add(content);
        return root;
    }

    private static Stack Hidden(Control content) => new()
    {
        AutoScroll = true,
        ScrollBars = ScrollBars.Both,
        ShowScrollBars = ShowScrollBars.Never,
        Children = { content }
    };

    private static Pointer Pointer(Point cells, PointerAction action, int wheelY = 0) => new(
        cells,
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);

    private static long Minimum(System.Action action, int iterations, out TimeSpan elapsed)
    {
        for (var index = 0; index < iterations; index++)
        {
            action();
        }

        var minimum = long.MaxValue;
        var watch = Stopwatch.StartNew();

        for (var sample = 0; sample < 5; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var index = 0; index < iterations; index++)
            {
                action();
            }

            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        watch.Stop();
        elapsed = watch.Elapsed;
        return minimum;
    }

    private static void ForceCollection()
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    private static void Report(string scenario, TimeSpan elapsed, int iterations)
    {
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"{scenario}: {iterations} iterations in {elapsed.TotalMilliseconds:F3} ms; " +
            $"{RuntimeInformation.FrameworkDescription}; {RuntimeInformation.ProcessArchitecture}; " +
            RuntimeInformation.OSDescription);
    }
}
