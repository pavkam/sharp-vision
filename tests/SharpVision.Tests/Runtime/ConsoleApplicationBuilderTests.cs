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
        var capabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Basic16 };
        var profile = TerminalProfile.CreateAnsi(
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 });
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
            CapabilitySupport.Supported,
            Origin.Database);
        var capabilities = TerminalCapabilities.Conservative with { Osc52 = database };
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseCapabilities(capabilities);

        var terminal = builder.Options.ToTerminalOptions();

        terminal.Profile.Capabilities.ShouldBeSameAs(capabilities);
        terminal.Profile.Capabilities.Osc52.ShouldBe(database);
    }

    /// <summary>Verifies Build() leaves a theme the screen published from OnAttach alone when the caller never called UseTheme.</summary>
    [Fact]
    public async Task Build_WhenScreenPublishesThemeInOnAttachAndUseThemeNotCalled_KeepsScreenThemeAsync()
    {
        var screen = new ThemePublishingScreen(Themes.White);
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => new ConsoleConnection(
                new ConsoleApplicationTransport(),
                new ConsoleApplicationResizeSource(),
                new ConsoleApplicationRestoreLease()),
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));

        var application = builder.Build();

        try
        {
            application.Theme.ShouldBeSameAs(Themes.White);
        }
        finally
        {
            await application.DisposeAsync();
        }
    }

    /// <summary>Verifies an explicit UseTheme still wins over whatever the screen published from OnAttach.</summary>
    [Fact]
    public async Task Build_WhenUseThemeCalled_OverridesScreenPublishedThemeAsync()
    {
        var screen = new ThemePublishingScreen(Themes.White);
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => new ConsoleConnection(
                new ConsoleApplicationTransport(),
                new ConsoleApplicationResizeSource(),
                new ConsoleApplicationRestoreLease()),
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative))
            .UseTheme(Themes.Dark);

        var application = builder.Build();

        try
        {
            application.Theme.ShouldBeSameAs(Themes.Dark);
        }
        finally
        {
            await application.DisposeAsync();
        }
    }

    /// <summary>Publishes a fixed theme from OnAttach, mirroring the documented screen theming pattern.</summary>
    private sealed class ThemePublishingScreen: Screen
    {
        private readonly Theme _theme;

        internal ThemePublishingScreen(Theme theme)
        {
            _theme = theme;
            InitializeContent(new ProbeControl());
        }

        protected override void OnAttach(Application application) => application.Theme = _theme;
    }
}
