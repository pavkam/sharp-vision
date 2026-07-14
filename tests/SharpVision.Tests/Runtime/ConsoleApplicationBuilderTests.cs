// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using SharpVision.Runtime;

/// <summary>Verifies <see cref="ConsoleApplicationBuilder"/> fluent setters accumulate onto <see cref="ConsoleRunOptions"/>.</summary>
public sealed class ConsoleApplicationBuilderTests
{
    /// <summary>Verifies chained setters accumulate onto the exposed options.</summary>
    [Fact]
    public void FluentSetters_WhenChained_AccumulateOntoOptions()
    {
        ConsoleApplicationBuilder builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseAlternateScreen(false)
            .WithoutMouse()
            .TreatControlCAsInput();

        builder.Options.AlternateScreen.ShouldBeFalse();
        builder.Options.MouseTracking.ShouldBeNull();
        builder.Options.TreatControlCAsInput.ShouldBeTrue();
    }

    /// <summary>Verifies UseMouse sets both the tracking level and the coordinate encoding.</summary>
    [Fact]
    public void UseMouse_WhenGivenLevel_SetsTrackingAndCoordinates()
    {
        ConsoleApplicationBuilder builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseMouse(MouseTracking.Press, MouseCoordinates.Pixel);

        builder.Options.MouseTracking.ShouldBe(MouseTracking.Press);
        builder.Options.MouseCoordinates.ShouldBe(MouseCoordinates.Pixel);
    }
}
