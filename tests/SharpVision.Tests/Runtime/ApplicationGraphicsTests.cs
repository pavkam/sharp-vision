// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Terminal.Capabilities;
using Terminal.Graphics;

using GraphicsImage = Terminal.Graphics.ImageSource;
using MultiplexerKind = Terminal.Multiplexing.MultiplexerKind;
using MultiplexingPolicy = Terminal.Multiplexing.MultiplexingPolicy;

/// <summary>Proves application-owned graphics selection, geometry, routing, and cleanup ordering.</summary>
public sealed class ApplicationGraphicsTests
{
    /// <summary>Verifies the public Image control emits its cell fallback before selected graphics.</summary>
    [Fact]
    public async Task StartAsync_WhenImageControlUsesSixel_RendersFallbackBeforeExactPlacementAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var image = new Image
        {
            Source = Rgba(),
            AlternateText = "AL",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
        });
        await using Application application = new(
            image,
            terminal,
            terminal,
            TerminalOptions.Minimal with { Profile = profile });

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        var fallback = bytes.AsSpan().IndexOf("AL"u8);
        var graphics = bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8);
        fallback.ShouldBeGreaterThanOrEqualTo(0);
        graphics.ShouldBeGreaterThan(fallback);
        bytes.AsSpan().IndexOf("\"1;1;5;3"u8).ShouldBeGreaterThan(graphics);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a public PNG Image reaches explicit iTerm2 multipart after cell fallback.</summary>
    [Fact]
    public async Task StartAsync_WhenPngImageUsesIterm_RendersFallbackBeforeMultipartAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1)));
        var image = new Image
        {
            Source = Png(),
            AlternateText = "PN",
            Width = Length.Cells(2),
            Height = Length.Cells(1),
            Stretch = ImageStretch.Contain
        };
        await using Application application = new(
            image,
            terminal,
            terminal,
            Options(iterm: true));

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        var fallback = bytes.AsSpan().IndexOf("PN"u8);
        var multipart = bytes.AsSpan().IndexOf("\u001b]1337;MultipartFile="u8);
        fallback.ShouldBeGreaterThanOrEqualTo(0);
        multipart.ShouldBeGreaterThan(fallback);
        bytes.AsSpan().IndexOf("\u001b]1337;FileEnd"u8).ShouldBeGreaterThan(multipart);
        await application.StopAsync(TestContext.Current.CancellationToken);
        terminal.Disposals.ShouldBe(1);
    }

    /// <summary>
    /// Verifies a placement whose format has no encodable path on any enabled protocol pushes a
    /// GraphicsDiagnostic event instead of leaving the degradation observable only through a
    /// renderer the hosted Application never exposes (see #233).
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenImageFormatHasNoEncodablePath_RaisesGraphicsDiagnosticAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var image = new Image
        {
            Source = Rgba(),
            AlternateText = "PN",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        await using Application application = new(
            image,
            terminal,
            terminal,
            Options(iterm: true));
        var diagnosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        GraphicsDiagnosticEventArgs? received = null;
        application.GraphicsDiagnostic += OnGraphicsDiagnostic;

        await application.StartAsync(TestContext.Current.CancellationToken);
        await diagnosed.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.GraphicsDiagnostic -= OnGraphicsDiagnostic;

        _ = received.ShouldNotBeNull();
        var placement = received.Placements.ShouldHaveSingleItem();
        placement.Reason.ShouldBe(GraphicsPlacementSkipReason.FormatNotEncodable);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnGraphicsDiagnostic(object? sender, GraphicsDiagnosticEventArgs eventArgs)
        {
            _ = sender;
            received = eventArgs;
            _ = diagnosed.TrySetResult();
        }
    }

    /// <summary>
    /// Verifies a clean frame - every placement encodes normally - never raises GraphicsDiagnostic
    /// at all, matching how the other opportunistic terminal-diagnostic events on Application only
    /// fire on an actual occurrence rather than every frame (see #233).
    /// </summary>
    [Fact]
    public async Task StartAsync_WhenFrameHasNoSkippedPlacements_NeverRaisesGraphicsDiagnosticAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var image = new Image
        {
            Source = Rgba(),
            AlternateText = "AL",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        await using Application application = new(
            image,
            terminal,
            terminal,
            Options(sixel: true));
        var raised = false;
        application.GraphicsDiagnostic += OnGraphicsDiagnostic;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;

        await application.StartAsync(TestContext.Current.CancellationToken);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;
        application.GraphicsDiagnostic -= OnGraphicsDiagnostic;

        raised.ShouldBeFalse();
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnGraphicsDiagnostic(object? sender, GraphicsDiagnosticEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            raised = true;
        }

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = rendered.TrySetResult();
        }
    }

    /// <summary>Verifies a resolved Kitty identity cannot promote tentative graphics capability evidence.</summary>
    [Fact]
    public async Task StartAsync_WhenKittyIdentityOnlyHasTentativeGraphics_PreservesCellFallbackAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" },
                limits: QueryLimits.Default with { QueryTimeout = TimeSpan.FromMilliseconds(100) })
        };
        await using Application application = new(
            new Image { Source = Rgba(), AlternateText = "F" },
            terminal,
            terminal,
            options);

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("F"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("a=t,"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("\u001b]1337;MultipartFile="u8).ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an occluded upper Image transitively blocks an overlapping lower Kitty placement.</summary>
    [Fact]
    public async Task StartAsync_WhenUpperImageFallsBack_BlocksOverlappingLowerKittyPlacementAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(3, 1), new Size(6, 3)));
        var lower = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), [255, 0, 0, 255]),
            AlternateText = "AA",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var upper = new Image
        {
            Source = GraphicsImage.FromRgba(new Size(1, 1), [0, 255, 0, 255]),
            AlternateText = "BB",
            Width = Length.Cells(2),
            Height = Length.Cells(1)
        };
        var cover = new ControlText("X")
        {
            Width = Length.Cells(1),
            Height = Length.Cells(1)
        };
        var root = new Overlay { Children = { lower, upper, cover } };
        Overlay.SetLeft(upper, Length.Cells(1));
        Overlay.SetLeft(cover, Length.Cells(2));
        await using Application application = new(
            root,
            terminal,
            terminal,
            Options(kitty: true));

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("ABX"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("a=t,"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("a=p,"u8).ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies negotiated graphics selection waits for the profile barrier and receives live exact metrics.</summary>
    [Fact]
    public async Task StartAsync_WhenSixelIsNegotiated_SelectsBackendAfterBarrierWithLiveMetricsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(2, 1), new Size(5, 3)));
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                new CapabilityOverrides { Sixel = true })
        };
        var queried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += value =>
        {
            if (value.Span.IndexOf("\u001b[c"u8) >= 0)
            {
                _ = queried.TrySetResult();
            }
        };
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            options);

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await queried.Task.WaitAsync(TestContext.Current.CancellationToken);
        Joined(terminal).AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        terminal.QueueInput(NegotiationReplies());
        await starting;

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("\"1;1;5;3"u8).ShouldBeGreaterThanOrEqualTo(0);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies detected multiplexer layers prevent unwrapped graphics when routing is unauthorized.</summary>
    [Fact]
    public async Task StartAsync_WhenLayeredRouteIsUnauthorized_PreservesCellFallbackWithoutDirectLeakAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));
        var options = TerminalOptions.Minimal with
        {
            Negotiation = new NegotiationOptions(
                new Dictionary<string, string?>(),
                new CapabilityOverrides { Sixel = true },
                limits: null,
                multiplexing: policy)
        };
        var queried = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminal.Written += value =>
        {
            if (value.Span.IndexOf("\u001b[c"u8) >= 0)
            {
                _ = queried.TrySetResult();
            }
        };
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            options);

        var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
        await queried.Task.WaitAsync(TestContext.Current.CancellationToken);
        terminal.QueueInput(NegotiationReplies());
        await starting;

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("\u001bPtmux;"u8).ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a pinned profile - which suppresses startup negotiation entirely - still
    /// routes graphics selection through an explicit Multiplexing policy carried independently of
    /// Negotiation, so an unauthorized route cannot silently degrade into a direct leak just because
    /// the host pinned capabilities to avoid probing.</summary>
    [Fact]
    public async Task StartAsync_WhenProfileIsPinnedWithUnauthorizedMultiplexing_PreservesCellFallbackWithoutDirectLeakAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Supported, Origin.Override)
        });
        var options = TerminalOptions.Minimal with
        {
            Profile = profile,
            Multiplexing = policy
        };
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            options);

        await application.StartAsync(TestContext.Current.CancellationToken);

        var bytes = Joined(terminal);
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("\u001bPtmux;"u8).ShouldBe(-1);
        await application.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies Kitty cleanup delete and flush complete before Session disposes transport.</summary>
    [Fact]
    public async Task StopAsync_WhenKittyStateExists_DeletesAndFlushesBeforeTransportDisposalAsync()
    {
        List<string> order = [];
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        terminal.Written += value =>
        {
            if (value.Span.IndexOf("a=d,d=I"u8) >= 0)
            {
                order.Add("delete");
            }
        };
        terminal.Flushed += () =>
        {
            if (order.Contains("delete", StringComparer.Ordinal) &&
                !order.Contains("flush", StringComparer.Ordinal))
            {
                order.Add("flush");
            }
        };
        terminal.Disposed += () => order.Add("dispose");
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            Options(kitty: true));
        await application.StartAsync(TestContext.Current.CancellationToken);

        await application.StopAsync(TestContext.Current.CancellationToken);

        order.ShouldBe(["delete", "flush", "dispose"]);
    }

    /// <summary>Verifies cleanup write failure remains diagnostic and cannot skip transport disposal.</summary>
    [Fact]
    public async Task StopAsync_WhenGraphicsCleanupFails_StillDisposesSessionTransportAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        await using Application application = new(
            new GraphicsProbeControl(Rgba()),
            terminal,
            terminal,
            Options(kitty: true));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var failure = new IOException("graphics cleanup failed");
        terminal.FailWriteNumber = terminal.Writes.Count + 1;
        terminal.WriteFailure = failure;

        _ = await Should.ThrowAsync<IOException>(async () =>
            await application.StopAsync(TestContext.Current.CancellationToken));

        terminal.Disposals.ShouldBe(1);
        application.LastCleanupException.ShouldBeSameAs(failure);
    }

    /// <summary>Verifies a later profile revocation removes retained graphics without another upload.</summary>
    [Fact]
    public async Task Profile_WhenKittySupportIsRevoked_RemovesRemoteImageAndStopsGraphicsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        await using Application application = new(
            new Image { Source = Rgba(), AlternateText = "F" },
            terminal,
            terminal,
            Options(kitty: true));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var writeCount = terminal.Writes.Count;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;
        var revoked = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Unsupported, Origin.Override)
        };

        application.Profile(revoked);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;

        var bytes = terminal.Writes.Skip(writeCount).SelectMany(static value => value).ToArray();
        bytes.AsSpan().IndexOf("a=d,d=I"u8).ShouldBeGreaterThanOrEqualTo(0);
        bytes.AsSpan().IndexOf("a=t,"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("a=p,"u8).ShouldBe(-1);
        application.Capabilities.ShouldBeSameAs(revoked);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = rendered.TrySetResult();
        }
    }

    /// <summary>Verifies profile revocation waits for an in-flight Kitty transaction to commit.</summary>
    [Fact]
    public async Task Profile_WhenKittyFlushIsPaused_CommitsThenRemovesOnNextFrameAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        var image = new Image { Source = Rgba(), AlternateText = "F" };
        await using Application application = new(
            image,
            terminal,
            terminal,
            Options(kitty: true));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var writeCount = terminal.Writes.Count;
        terminal.PauseFlush();
        var uploaded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var frameCount = 0;
        terminal.Written += OnWritten;
        application.FrameRendered += OnFrameRendered;

        await application.Dispatcher.InvokeAsync(
            () =>
            {
                image.Source = GraphicsImage.FromRgba(new Size(1, 1), [0, 255, 0, 255]);
            },
            TestContext.Current.CancellationToken);
        await uploaded.Task.WaitAsync(TestContext.Current.CancellationToken);
        var revoked = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Unsupported, Origin.Override)
        };
        application.Profile(revoked);
        await application.Dispatcher.InvokeAsync(
            static () => { },
            TestContext.Current.CancellationToken);

        application.Capabilities.ShouldBeSameAs(revoked);
        terminal.ReleaseFlush();
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        terminal.Written -= OnWritten;
        application.FrameRendered -= OnFrameRendered;

        var bytes = terminal.Writes.Skip(writeCount).SelectMany(static value => value).ToArray();
        var upload = bytes.AsSpan().IndexOf("a=t,"u8);
        var removal = bytes.AsSpan().IndexOf("a=d,d=I"u8);
        upload.ShouldBeGreaterThanOrEqualTo(0);
        removal.ShouldBeGreaterThan(upload);
        await application.StopAsync(TestContext.Current.CancellationToken);
        terminal.Disposals.ShouldBe(1);
        return;

        void OnWritten(ReadOnlyMemory<byte> value)
        {
            if (value.Span.IndexOf("a=t,"u8) >= 0)
            {
                _ = uploaded.TrySetResult();
            }
        }

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            frameCount++;

            if (frameCount == 2)
            {
                _ = rendered.TrySetResult();
            }
        }
    }

    /// <summary>Verifies a later profile revocation stops sixel and repairs the cell fallback.</summary>
    [Fact]
    public async Task Profile_WhenSixelSupportIsRevoked_StopsRasterOutputAndRepairsCellsAsync()
    {
        await using FakeTerminal terminal = new();
        terminal.QueueResize(new Dimensions(new Size(1, 1), new Size(2, 3)));
        await using Application application = new(
            new Image { Source = Rgba(), AlternateText = "F" },
            terminal,
            terminal,
            Options(sixel: true));
        await application.StartAsync(TestContext.Current.CancellationToken);
        var writeCount = terminal.Writes.Count;
        var rendered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        application.FrameRendered += OnFrameRendered;
        var revoked = TerminalCapabilities.Conservative with
        {
            Sixel = new Feature(CapabilitySupport.Unsupported, Origin.Override)
        };

        application.Profile(revoked);
        await rendered.Task.WaitAsync(TestContext.Current.CancellationToken);
        application.FrameRendered -= OnFrameRendered;

        var bytes = terminal.Writes.Skip(writeCount).SelectMany(static value => value).ToArray();
        bytes.ShouldNotBeEmpty();
        bytes.AsSpan().IndexOf("\u001bP0;1;0q"u8).ShouldBe(-1);
        bytes.AsSpan().IndexOf("F"u8).ShouldBeGreaterThanOrEqualTo(0);
        application.Capabilities.ShouldBeSameAs(revoked);
        await application.StopAsync(TestContext.Current.CancellationToken);
        return;

        void OnFrameRendered(object? sender, FrameRenderedEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            _ = rendered.TrySetResult();
        }
    }

    private static TerminalOptions Options(
        bool kitty = false,
        bool sixel = false,
        bool iterm = false) => TerminalOptions.Minimal with
        {
            Profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
            {
                KittyGraphics = new Feature(
                kitty ? CapabilitySupport.Supported : CapabilitySupport.Unsupported,
                Origin.Override),
                Sixel = new Feature(
                sixel ? CapabilitySupport.Supported : CapabilitySupport.Unsupported,
                Origin.Override),
                ItermImages = new Feature(
                iterm ? CapabilitySupport.Supported : CapabilitySupport.Unsupported,
                Origin.Override)
            })
        };

    private static GraphicsImage Rgba() => GraphicsImage.FromRgba(
        new Size(1, 1),
        [255, 0, 0, 255]);

    private static GraphicsImage Png() => GraphicsImage.FromPng(Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAEUlEQVR4nGP4z8DA8B+MgBgAHfAD/dPQfSYAAAAASUVORK5CYII="));

    private static byte[] NegotiationReplies() => Encoding.ASCII.GetBytes(
        "\u001b[?1016;1$y\u001b[?1006;1$y\u001b[?2004;1$y" +
        "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?3u\u001b[?1;2c");

    private static byte[] Joined(FakeTerminal terminal) =>
        [.. terminal.Writes.SelectMany(static value => value)];
}
