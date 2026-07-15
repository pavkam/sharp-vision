// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

using SharpVision.Consumer.Tests.PackageSpecimens;

/// <summary>Verifies retained components compile and behave through the unfriended public surface.</summary>
public sealed class CompositeContractTests
{
    /// <summary>Verifies an external component keeps its implementation tree private.</summary>
    [Fact]
    public void StatusCard_WhenInspected_UsesOnlyThePublicCompositeSurface()
    {
        var type = typeof(StatusCard);

        type.BaseType.ShouldBe(typeof(CompositeControl));
        typeof(Container).IsAssignableFrom(type).ShouldBeFalse();
        type.GetProperty("Children").ShouldBeNull();
        type.GetProperty("Content").ShouldBeNull();
    }

    /// <summary>Verifies inherited layout, rendering, hit testing, and dispatcher affinity need no internal access.</summary>
    [Fact]
    public async Task StatusCard_WhenHosted_UsesRetainedCompositionThroughPublicContractsAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(12, 4)));
        var card = new StatusCard("Service", "Ready")
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        await using var application = new Application(
            card,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync(TestContext.Current.CancellationToken);

        card.Bounds.ShouldBe(new Rect(0, 0, 12, 4));
        card.DesiredSize.ShouldBe(new Size(7, 3));
        card.HitTest(default).ShouldNotBeNull().ShouldNotBeSameAs(card);
        _ = Should.Throw<InvalidOperationException>(() => card.Status = "Busy");

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                card.Status = "Busy";
            },
            TestContext.Current.CancellationToken);

        card.Status.ShouldBe("Busy");
        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
