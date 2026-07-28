// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Verifies <see cref="ConsoleApplicationBuilder"/> fluent setters accumulate onto <see cref="ConsoleRunOptions"/>.</summary>
public sealed class ConsoleApplicationBuilderTests
{
    /// <summary>Verifies chained setters accumulate onto the exposed options.</summary>
    [Fact]
    public void FluentSetters_WhenChained_AccumulateOntoOptions()
    {
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
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
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseMouse(MouseTracking.Press, MouseCoordinates.Pixel);

        builder.Options.MouseTracking.ShouldBe(MouseTracking.Press);
        builder.Options.MouseCoordinates.ShouldBe(MouseCoordinates.Pixel);
    }

    /// <summary>Verifies a complete profile replaces the compatibility capability override.</summary>
    [Fact]
    public void UseTerminalProfile_WhenCapabilitiesWereSet_PrefersCompleteProfile()
    {
        var capabilities = Capabilities.Conservative with { ColorDepth = ColorDepth.Basic16 };
        var profile = TerminalProfile.CreateAnsi(
            Capabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseCapabilities(capabilities)
            .UseTerminalProfile(profile);

        builder.Options.Profile.ShouldBeSameAs(profile);
        builder.Options.Capabilities.ShouldBeNull();
    }

    /// <summary>Verifies null complete-profile overrides are rejected without changing accumulated options.</summary>
    [Fact]
    public void UseTerminalProfile_WhenProfileNull_ThrowsWithoutChangingOptions()
    {
        var builder = new ConsoleApplicationBuilder(new ProbeScreen());
        var before = builder.Options;

        _ = Should.Throw<ArgumentNullException>(() => builder.UseTerminalProfile(profile: null!));

        builder.Options.ShouldBeSameAs(before);
    }

    /// <summary>Verifies the unsupported-terminal message fluent setter is independent of redirect output.</summary>
    [Fact]
    public void WithUnsupportedTerminalMessage_WhenCalled_SetsOnlyUnsupportedMessage()
    {
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .WithRedirectedMessage("redirected")
            .WithUnsupportedTerminalMessage("unsupported");

        builder.Options.RedirectedMessage.ShouldBe("redirected");
        builder.Options.UnsupportedTerminalMessage.ShouldBe("unsupported");
    }

    /// <summary>Verifies UseCapabilities preserves exact trusted evidence through public compatibility mapping.</summary>
    [Fact]
    public void UseCapabilities_WhenDatabaseEvidenceExists_PreservesExactCapabilities()
    {
        var database = new Feature(
            Terminal.Capabilities.Support.Supported,
            Origin.Database);
        var capabilities = Capabilities.Conservative with { Osc52 = database };
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseCapabilities(capabilities);

        var terminal = builder.Options.ToTerminalOptions();

        terminal.Profile.Capabilities.ShouldBeSameAs(capabilities);
        terminal.Profile.Capabilities.Osc52.ShouldBe(database);
    }
}
