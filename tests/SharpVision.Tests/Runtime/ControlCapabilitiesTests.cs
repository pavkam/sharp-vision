// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;


/// <summary>Verifies immutable terminal capabilities inherited by retained controls.</summary>
public sealed class ControlCapabilitiesTests
{
    /// <summary>Verifies exact resize metrics reach inherited context before measure and resize publication.</summary>
    [Fact]
    public async Task Resize_WhenExactMetricsArrive_PublishesContextBeforeLayoutAsync()
    {
        await using FakeTerminal terminal = new();
        var dimensions = new Dimensions(new Size(3, 2), new Size(10, 5));
        terminal.QueueResize(dimensions);
        var root = new CellMetricsProbe();
        await using Application application = new(
            root,
            terminal,
            terminal,
            TerminalOptions.Minimal);
        CellMetrics? resizeMetrics = null;
        application.Resize += (_, _) => resizeMetrics = root.InheritedMetrics;

        await application.StartAsync(TestContext.Current.CancellationToken);

        root.MeasureMetrics.ShouldNotBeEmpty();
        root.MeasureMetrics[0].ShouldBe(dimensions.CellMetrics);
        resizeMetrics.ShouldBe(dimensions.CellMetrics);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a child inserted after attachment inherits the owner's exact cell metrics.</summary>
    [Fact]
    public async Task ChildrenAdd_WhenOwnerHasExactMetrics_InheritsCurrentGeometryAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Stack();
            root.Attach(dispatcher);
            var metrics = new CellMetrics(new Size(3, 2), new Size(10, 5));
            root.SetCellMetrics(metrics);
            var child = new CellMetricsProbe();

            root.Children.Add(child);

            child.InheritedMetrics.ShouldBe(metrics);
            child.Transitions.ShouldBe([metrics]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies attachment publishes the supplied profile before the attached callback.</summary>
    [Fact]
    public async Task Attach_WhenProfileIsSupplied_InheritsColorDepthAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var profile = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 };
            var control = new CapabilityProbe();

            control.Attach(dispatcher, UnicodePolicy.Default, profile);

            control.ColorDepth.ShouldBe(ColorDepth.Indexed256);
            control.Transitions.ShouldBe([ColorDepth.Indexed256]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies runtime publication reaches an attached subtree in ownership order.</summary>
    [Fact]
    public async Task SetCapabilities_WhenTreeIsAttached_PublishesToDescendantsAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var child = new CapabilityProbe();
            var root = new Stack { Children = { child } };
            root.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Basic16 });
            var profile = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };

            root.SetCapabilities(profile);

            child.ColorDepth.ShouldBe(ColorDepth.TrueColor);
            child.Transitions.ShouldBe([ColorDepth.TrueColor]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a child inserted after attachment inherits the owner's active profile.</summary>
    [Fact]
    public async Task ChildrenAdd_WhenOwnerIsAttached_InheritsCapabilitiesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new Stack();
            root.Attach(
                dispatcher,
                UnicodePolicy.Default,
                TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });
            var child = new CapabilityProbe();

            root.Children.Add(child);

            child.ColorDepth.ShouldBe(ColorDepth.Indexed256);
            child.Transitions.ShouldBe([ColorDepth.Indexed256]);
        }, TestContext.Current.CancellationToken);
    }
}
