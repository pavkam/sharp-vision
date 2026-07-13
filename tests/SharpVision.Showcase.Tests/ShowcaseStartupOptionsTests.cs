// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

using System.Text;

using SharpVision.Runtime;
using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Geometry;
using SharpVision.Terminal.Protocols;
using SharpVision.Terminal.Runtime;

using Shouldly;

using CapabilityOrigin = Terminal.Capabilities.Origin;
using CapabilitySupport = Terminal.Capabilities.Support;

/// <summary>Verifies the executable showcase explicitly requests its interactive terminal modes.</summary>
public sealed class StartupOptionsTests
{
    /// <summary>Verifies executable negotiation owns evidence and starts conservatively.</summary>
    [Fact]
    public void Create_WhenNegotiationIsEnabled_OwnsEnvironmentAndPreservesOverride()
    {
        // Arrange
        Dictionary<string, string?> environment = new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-kitty",
        };

        // Act
        var options = StartupOptions.Create(environment, negotiate: true);
        environment["TERM"] = "dumb";

        // Assert
        var negotiation = options.Negotiation.ShouldNotBeNull();
        negotiation.Environment["TERM"].ShouldBe("xterm-kitty");
        negotiation.Overrides.ShouldNotBeNull().CellMouse.ShouldBe(true);
        options.Capabilities.CellMouse.ShouldBe(
            new Feature(CapabilitySupport.Supported, CapabilityOrigin.Override));
        options.Capabilities.KittyKeyboard.State.ShouldBe(CapabilitySupport.Unknown);
    }

    /// <summary>Verifies executable startup queries before enabling showcase mouse input.</summary>
    [Fact]
    public async Task Create_WhenNegotiatedShowcaseStarts_EnablesMouseAfterQueryBatchAsync()
    {
        // Arrange
        await using FakeTerminal terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using Gallery gallery = new Gallery();
        var options = StartupOptions.Create(
            new Dictionary<string, string?> { ["TERM"] = "xterm-256color" },
            negotiate: true);
        await using Application application = new Application(gallery, terminal, terminal, options);
        TaskCompletionSource queryWritten = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += value =>
        {
            if (value.Span.StartsWith("\u001b[?u"u8))
            {
                _ = queryWritten.TrySetResult();
            }
        };

        // Act
        Task starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await queryWritten.Task.WaitAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput(
            "\u001b[?1016;2$y\u001b[?1006;1$y\u001b[?2004;1$y"u8.ToArray());
        terminal.QueueInput(
            "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?3u\u001b[?1;2c"u8.ToArray());
        await starting;

        // Assert
        var output = Encoding.ASCII.GetString([.. terminal.Writes.SelectMany(static value => value)]);
        var queries =
            "\u001b[?u\u001b[c\u001b[?2026$p\u001b[?1004$p" +
            "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p";
        var startup = "\u001b[?1049h\u001b[?25l" + queries;
        output.ShouldStartWith(startup);
        output.IndexOf("\u001b[?1003h\u001b[?1006h", StringComparison.Ordinal)
            .ShouldBeGreaterThanOrEqualTo(startup.Length);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies application startup emits the typed SGR any-event mouse enable sequence.</summary>
    [Fact]
    public async Task Create_WhenShowcaseStarts_EnablesSgrAnyEventMouseAsync()
    {
        await using FakeTerminal terminal = new FakeTerminal();
        terminal.QueueResize(new Dimensions(new Size(80, 24)));
        using Gallery gallery = new Gallery();
        var options = StartupOptions.Create(new Dictionary<string, string?>
        {
            ["TERM"] = "xterm-256color",
        });
        await using Application application = new Application(gallery, terminal, terminal, options);

        options.Tracking.ShouldBe(MouseTracking.Any);
        options.Coordinates.ShouldBe(MouseCoordinates.Sgr);
        options.Capabilities.CellMouse.IsSupported.ShouldBeTrue();

        await application.StartAsync(TestContext.Current.CancellationToken);

        var output = Encoding.ASCII.GetString([.. terminal.Writes.SelectMany(static value => value)]);
        output.ShouldContain("\u001b[?1003h\u001b[?1006h");

        await application.StopAsync(TestContext.Current.CancellationToken);
    }
}
