// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Consumer.Tests;

using SharpVision.Consumer.Tests.PackageSpecimens;

using ControlText = Controls.Text;

/// <summary>Verifies third-party typed item controls need no ownership internals.</summary>
public sealed class ItemsContractTests
{
    /// <summary>Verifies the external control exposes typed semantics without leaking the host or child collection.</summary>
    [Fact]
    public void TagCloud_WhenInspected_ExposesOnlyTypedSemanticItems()
    {
        var type = typeof(TagCloud);

        type.BaseType.ShouldBe(typeof(ItemsControl));
        typeof(Container).IsAssignableFrom(type).ShouldBeFalse();
        type.GetProperty("Children").ShouldBeNull();
        type.GetProperties()
            .ShouldNotContain(static property => typeof(Container).IsAssignableFrom(property.PropertyType));
        type.GetProperty(nameof(TagCloud.Tags))!.PropertyType.ShouldBe(typeof(IReadOnlyList<string>));
    }

    /// <summary>Verifies typed mutation realizes private controls through inherited layout and rendering.</summary>
    [Fact]
    public async Task Add_WhenCloudIsAttached_UsesPrivateItemPresentationAsync()
    {
        await using var terminal = new ConsumerTerminal();
        terminal.QueueResize(new Dimensions(new Size(12, 2)));
        var cloud = new TagCloud
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        cloud.Add("red");
        cloud.Add("blue");
        await using var application = new Application(
            cloud,
            terminal,
            terminal,
            TerminalOptions.Minimal);

        await application.StartAsync(TestContext.Current.CancellationToken);

        cloud.Tags.ShouldBe(["red", "blue"]);
        cloud.Count.ShouldBe(2);
        cloud.DesiredSize.ShouldBe(new Size(7, 1));
        cloud.HitTest(default).ShouldNotBeNull().GetType().ShouldBe(typeof(ControlText));

        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
