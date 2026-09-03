// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using System.Reflection;

using Terminal.Backends;
using Terminal.Clipboard;

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
            .UseClipboardPasteEvents()
            .WithClipboardOperationTimeout(TimeSpan.FromSeconds(45))
            .TreatControlCAsInput();

        builder.Options.AlternateScreen.ShouldBeFalse();
        builder.Options.MouseTracking.ShouldBeNull();
        builder.Options.ClipboardPasteEvents.ShouldBeTrue();
        builder.Options.ClipboardOperationTimeout.ShouldBe(TimeSpan.FromSeconds(45));
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

    /// <summary>Verifies disabling negotiation no longer fabricates a conservative ANSI profile as a
    /// side effect: doing so used to silently pin a Profile, which both discarded a subsequent
    /// UseNegotiation() call and bypassed real terminal-description discovery in Build().</summary>
    [Fact]
    public void WithoutNegotiation_WhenCalled_LeavesProfileAndCapabilitiesNull()
    {
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .WithoutNegotiation();

        builder.Options.Negotiation.ShouldBeNull();
        builder.Options.NegotiationDisabled.ShouldBeTrue();
        builder.Options.Profile.ShouldBeNull();
        builder.Options.Capabilities.ShouldBeNull();
    }

    /// <summary>Verifies re-enabling negotiation after disabling it clears the disabled flag, so the
    /// caller-supplied negotiation actually resolves instead of being silently discarded.</summary>
    [Fact]
    public void WithoutNegotiationThenUseNegotiation_WhenChained_ResolvesSuppliedNegotiation()
    {
        var negotiation = new NegotiationOptions(new Dictionary<string, string?>());
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .WithoutNegotiation()
            .UseNegotiation(negotiation);

        builder.Options.NegotiationDisabled.ShouldBeFalse();
        var terminal = builder.Options.ToTerminalOptions();

        terminal.Negotiation.ShouldBeSameAs(negotiation);
    }

    /// <summary>Verifies disabling negotiation still lets Build() perform real terminal-description
    /// discovery, rather than short-circuiting <c>ConsoleConnection.ResolveDescription</c> the way a
    /// fabricated Profile previously did.</summary>
    [Fact]
    public async Task Build_WhenNegotiationDisabled_StillPerformsDescriptionDiscoveryAsync()
    {
        var fixture = CreateFixture("ansi-fallback");
        _ = fixture.Builder.WithoutNegotiation();

        var application = fixture.Builder.Build();

        try
        {
            fixture.Provider.Request.ShouldNotBeNull().TerminalName.ShouldBe("dumb");
            application.TerminalProfile.Description.Name.ShouldBe("xterm-256color");
        }
        finally
        {
            await application.DisposeAsync();
        }
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

    /// <summary>Verifies UseModifyOtherKeys sets the value that later shows up in the built terminal options.</summary>
    [Fact]
    public void UseModifyOtherKeys_WhenGivenLevel_SetsValue()
    {
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseModifyOtherKeys(1);

        builder.Options.ModifyOtherKeys.ShouldBe(1);

        var terminal = builder.Options.ToTerminalOptions();

        terminal.ModifyOtherKeys.ShouldBe(1);
    }

    /// <summary>Verifies UseEscapeTimeout sets the value that reaches the built terminal's input
    /// decoder policy.</summary>
    [Fact]
    public void UseEscapeTimeout_WhenCalled_ReachesTerminalInput()
    {
        var timeout = TimeSpan.FromMilliseconds(120);
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseEscapeTimeout(timeout);

        builder.Options.EscapeTimeout.ShouldBe(timeout);
        builder.Options.ToTerminalOptions().Input.EscapeTimeout.ShouldBe(timeout);
    }

    /// <summary>Verifies UseMaxPasteBytes sets the value that reaches the built terminal's input
    /// decoder policy.</summary>
    [Fact]
    public void UseMaxPasteBytes_WhenCalled_ReachesTerminalInput()
    {
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseMaxPasteBytes(1024);

        builder.Options.MaxPasteBytes.ShouldBe(1024);
        builder.Options.ToTerminalOptions().Input.MaxPasteBytes.ShouldBe(1024);
    }

    /// <summary>Verifies UseTransferLimits sets the value that reaches the built terminal's input
    /// decoder policy.</summary>
    [Fact]
    public void UseTransferLimits_WhenCalled_ReachesTerminalInput()
    {
        var limits = TransferLimits.Default with { MaxClipboardBytes = 4 * 1024 * 1024 };
        var builder = new ConsoleApplicationBuilder(new ProbeScreen())
            .UseTransferLimits(limits);

        builder.Options.TransferLimits.ShouldBeSameAs(limits);
        builder.Options.ToTerminalOptions().Input.TransferLimits.ShouldBeSameAs(limits);
    }

    /// <summary>Verifies a null clipboard transfer limits override is rejected without changing
    /// accumulated options.</summary>
    [Fact]
    public void UseTransferLimits_WhenLimitsNull_ThrowsWithoutChangingOptions()
    {
        var builder = new ConsoleApplicationBuilder(new ProbeScreen());
        var before = builder.Options;

        _ = Should.Throw<ArgumentNullException>(() => builder.UseTransferLimits(limits: null!));

        builder.Options.ShouldBeSameAs(before);
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
        var screen = new ThemePublishingScreen(ThemeCatalog.White);
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => new ConsoleConnection(
                new ConsoleApplicationTransport(),
                new ConsoleApplicationResizeSource(),
                new ConsoleApplicationRestoreLease()),
            _ => { },
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));

        var application = builder.Build();

        try
        {
            application.Theme.ShouldBeSameAs(ThemeCatalog.White);
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
        var screen = new ThemePublishingScreen(ThemeCatalog.White);
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => new ConsoleConnection(
                new ConsoleApplicationTransport(),
                new ConsoleApplicationResizeSource(),
                new ConsoleApplicationRestoreLease()),
            _ => { },
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative))
            .UseTheme(ThemeCatalog.Dark);

        var application = builder.Build();

        try
        {
            application.Theme.ShouldBeSameAs(ThemeCatalog.Dark);
        }
        finally
        {
            await application.DisposeAsync();
        }
    }

    /// <summary>Verifies <c>Build()</c> actually threads <c>!TreatControlCAsInput</c> into
    /// <c>Application</c>'s own process-signal registration end-to-end - proving the wiring the
    /// "advanced case" section of docs/concepts/hosting.md describes is correct at the one call
    /// site that computes it, not merely correct in isolation on either side of that call.</summary>
    /// <param name="treatControlCAsInput">The option value <c>Build()</c> is configured with.</param>
    /// <param name="expectCtrlCObserved">Whether SIGINT/SIGQUIT registration should exist afterward.</param>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Build_WhenTreatControlCAsInputIsSet_TogglesCtrlCSignalObservationAsync(
        bool treatControlCAsInput,
        bool expectCtrlCObserved)
    {
        Assert.SkipUnless(!OperatingSystem.IsWindows(), "Checks the Unix SIGINT/SIGQUIT registration fields.");

        var builder = new ConsoleApplicationBuilder(
            new ProbeScreen(),
            static () => true,
            _ => new ConsoleConnection(
                new ConsoleApplicationTransport(),
                new ConsoleApplicationResizeSource(),
                new ConsoleApplicationRestoreLease()),
            _ => { },
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative));

        if (treatControlCAsInput)
        {
            builder = builder.TreatControlCAsInput();
        }

        var application = builder.Build();

        try
        {
            var processSignals = typeof(Application)
                .GetField("_processSignals", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(application);
            _ = processSignals.ShouldNotBeNull();

            // SIGTERM/SIGHUP are unconditional regardless of TreatControlCAsInput, so only the
            // Ctrl+C-gated SIGINT registration distinguishes the two cases here.
            var interrupt = processSignals!.GetType()
                .GetField("_interrupt", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(processSignals);

            (interrupt is not null).ShouldBe(expectCtrlCObserved);
        }
        finally
        {
            await application.DisposeAsync();
        }
    }

    /// <summary>Verifies one resolved Kitty context owns capability refinement and complete host cleanup.</summary>
    [Fact]
    public async Task RunAsync_WhenKittyEnvironmentRefinesXtermDescription_RetainsOneContextLineageAsync()
    {
        // Arrange
        var disposalOrder = new List<string>();
        var transport = new ConsoleApplicationTransport(disposalOrder);
        var resize = new ConsoleApplicationResizeSource(disposalOrder);
        var restore = new ConsoleApplicationRestoreLease(disposalOrder);
        var provider = new CrossLayerDescriptionProvider
        {
            Result = DescriptionResult.Loaded(XtermProfile(), Array.Empty<DescriptionDiagnostic>())
        };
        var loader = new DescriptionLoader(provider, new CrossLayerDescriptionProvider());
        var connection = new ConsoleConnection(
            transport,
            resize,
            restore,
            DescriptionPlatform.Unix,
            outputFileDescriptor: 1,
            windowsVirtualTerminal: false,
            loader,
            "xterm-256color");
        var environment = new Dictionary<string, string?> { ["TERM"] = "xterm-kitty" };
        var builder = new ConsoleApplicationBuilder(
            new ProbeScreen(),
            static () => true,
            _ => connection,
            _ => { },
            _ => { })
            .UseNegotiation(new NegotiationOptions(environment));
        var queryWritten = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var capabilitiesChanged = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        transport.Written += value =>
        {
            if (value.Span.IndexOf("\u001b[c"u8) >= 0)
            {
                _ = queryWritten.TrySetResult();
            }
        };
        resize.QueueResize(new Dimensions(new Size(20, 6), new Size(160, 96)));
        var application = builder.Build();
        application.CapabilitiesChanged += (_, eventArgs) =>
        {
            if (eventArgs.Current.KittyGraphics ==
                new Feature(CapabilitySupport.Supported, Origin.Query))
            {
                _ = capabilitiesChanged.TrySetResult();
            }
        };
        var session = application.Session;
        var initialBackend = session.Backend;

        try
        {
            initialBackend.ShouldBeSameAs(KittyBackend.Instance);

            // Act
            var starting = application.StartAsync(TestContext.Current.CancellationToken).AsTask();
            await queryWritten.Task.WaitAsync(TestContext.Current.CancellationToken);
            JoinedWrites(transport).ShouldBe(
                "\u001b[?1049h\u001b[?25l" +
                "\u001b[?u\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\" +
                "\u001b[c\u001b[>c\u001b[?2026$p\u001b[?1004$p" +
                "\u001b[?2004$p\u001b[?1006$p\u001b[?1016$p\u001b[?5522$p" +
                "\u001b[14t\u001b[16t\u001b[18t" +
                "\u001b]4;0;?\u001b\\\u001b]10;?\u001b\\\u001b]11;?\u001b\\" +
                "\u001b]1337;Capabilities\u001b\\" +
                // The terminating fence: a trailing CSI 6n.
                "\u001b[6n");
            transport.QueueInput(Encoding.ASCII.GetBytes(
                "\u001b[?3u\u001b_Gi=31;OK\u001b\\" +
                "\u001b[?1016;1$y\u001b[?1006;1$y\u001b[?2004;1$y" +
                "\u001b[?1004;1$y\u001b[?2026;1$y\u001b[?5522;1$y" +
                "\u001b[4;800;1200t\u001b[6;16;8t\u001b[8;6;20t" +
                "\u001b]4;0;rgb:1111/2222/3333\u001b\\" +
                "\u001b]10;rgb:ffff/eeee/0000\u001b\\" +
                "\u001b]11;rgb:0000/1111/ffff\u001b\\" +
                "\u001b[>41;410;0c\u001b[?1;2c"));
            await capabilitiesChanged.Task.WaitAsync(TestContext.Current.CancellationToken);
            await starting;
            await application.StopAsync(TestContext.Current.CancellationToken);

            // Assert
            application.TerminalProfile.Description.Name.ShouldBe("xterm-256color");
            application.Capabilities.KittyGraphics.ShouldBe(
                new Feature(CapabilitySupport.Supported, Origin.Query));
            session.Backend.ShouldBeSameAs(initialBackend);
            JoinedWrites(transport).ShouldEndWith(
                "\u001b[<u\u001b[?1003l\u001b[?1006l\u001b[?2004l" +
                "\u001b[?1004l\u001b[?25h\u001b[?1049l");
        }
        finally
        {
            await application.DisposeAsync();
        }

        transport.Disposals.ShouldBe(1);
        resize.Disposals.ShouldBe(1);
        restore.Disposals.ShouldBe(1);
        disposalOrder.ShouldBe(["resize", "transport", "restore"]);
    }

    private static TerminalProfile XtermProfile()
    {
        var ansi = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        var description = new Description(
            "xterm-256color",
            DescriptionOrigin.Database,
            Suitability.Usable,
            automaticMargins: true);
        return new TerminalProfile(
            description,
            ansi.Capabilities,
            ansi.Programs,
            ansi.KeyMap);
    }

    private static string JoinedWrites(ConsoleApplicationTransport transport) =>
        Encoding.ASCII.GetString([.. transport.Writes.SelectMany(static value => value)]);

    /// <summary>Verifies loaded-unsuitable, provider-failed, and missing results retain typed evidence through Build.</summary>
    /// <param name="resultKind">The deterministic description result.</param>
    [Theory]
    [InlineData("generic")]
    [InlineData("provider-failed")]
    [InlineData("missing")]
    public void Build_WhenDescriptionResultIsUnsupported_RetainsEvidenceBeforeOutput(string resultKind)
    {
        var fixture = CreateFixture(resultKind);

        var thrown = Should.Throw<UnsupportedTerminalException>(fixture.Builder.Build);

        AssertResolution(resultKind, thrown.Resolution, thrown.Message);
        AssertRejected(fixture);
    }

    /// <summary>Verifies a resolved ANSI fallback is promoted before Application construction.</summary>
    [Fact]
    public void Build_WhenDescriptionFallbackIsPromoted_ThrowsAndDisposesConnection()
    {
        var fixture = CreateFixture("ansi-fallback");
        _ = fixture.Builder.PromoteDiagnostics(DiagnosticPromotion.Fallback);

        var thrown = Should.Throw<TerminalDiagnosticException>(fixture.Builder.Build);

        thrown.Promotion.ShouldBe(DiagnosticPromotion.Fallback);
        AssertRejected(fixture);
    }

    /// <summary>Verifies all unsupported result families map to UnsupportedTerminal without terminal output.</summary>
    /// <param name="resultKind">The deterministic description result.</param>
    [Theory]
    [InlineData("generic")]
    [InlineData("provider-failed")]
    [InlineData("missing")]
    public async Task RunAsync_WhenDescriptionResultIsUnsupported_ReturnsTypedStatusAsync(
        string resultKind)
    {
        var fixture = CreateFixture(resultKind);

        var status = await fixture.Builder.RunAsync(TestContext.Current.CancellationToken);

        status.ShouldBe(ConsoleRunStatus.UnsupportedTerminal);
        fixture.Messages.ShouldBe(["unsupported"]);
        AssertRejected(fixture);
    }

    /// <summary>Verifies every individual cleanup failure leaves Build's unsupported rejection primary.</summary>
    /// <param name="resource">The resize, transport, or restore resource that fails disposal.</param>
    [Theory]
    [InlineData("resize")]
    [InlineData("transport")]
    [InlineData("restore")]
    public void Build_WhenPreflightCleanupFails_PreservesUnsupportedRejection(string resource)
    {
        var fixture = CreateFixture("provider-failed", resource);

        var thrown = Should.Throw<UnsupportedTerminalException>(fixture.Builder.Build);

        thrown.Resolution.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
        thrown.Message.ShouldContain(nameof(DescriptionLoadStatus.ProviderFailed));
        AssertRejected(fixture);
    }

    /// <summary>Verifies every individual cleanup failure leaves RunAsync's unsupported status primary.</summary>
    /// <param name="resource">The resize, transport, or restore resource that fails disposal.</param>
    [Theory]
    [InlineData("resize")]
    [InlineData("transport")]
    [InlineData("restore")]
    public async Task RunAsync_WhenPreflightCleanupFails_PreservesUnsupportedStatusAsync(
        string resource)
    {
        var fixture = CreateFixture("provider-failed", resource);

        var status = await fixture.Builder.RunAsync(TestContext.Current.CancellationToken);

        status.ShouldBe(ConsoleRunStatus.UnsupportedTerminal);
        fixture.Messages.ShouldBe(["unsupported"]);
        AssertRejected(fixture);
    }

    private static PreflightFixture CreateFixture(string resultKind, string? failingResource = null)
    {
        var screen = new ProbeScreen();
        var order = new List<string>();
        var messages = new List<string>();
        var transport = new ConsoleApplicationTransport(order);
        var resize = new ConsoleApplicationResizeSource(order);
        var restore = new ConsoleApplicationRestoreLease(order);
        var failure = new IOException("cleanup failed");

        switch (failingResource)
        {
            case "resize":
                resize.DisposalFailure = failure;
                break;
            case "transport":
                transport.DisposalFailure = failure;
                break;
            case "restore":
                restore.DisposalFailure = failure;
                break;
            case null:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(failingResource));
        }

        var provider = new CrossLayerDescriptionProvider { Result = DescriptionResultFor(resultKind) };
        var loader = new DescriptionLoader(provider, new CrossLayerDescriptionProvider());
        var connection = new ConsoleConnection(
            transport,
            resize,
            restore,
            DescriptionPlatform.Unix,
            outputFileDescriptor: 1,
            windowsVirtualTerminal: false,
            descriptionLoader: loader,
            terminalName: "dumb");
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => connection,
            messages.Add,
            _ => { })
            .WithUnsupportedTerminalMessage("unsupported");

        return new PreflightFixture(
            builder,
            screen,
            transport,
            resize,
            restore,
            provider,
            order,
            messages);
    }

    private static void AssertRejected(PreflightFixture fixture)
    {
        fixture.Provider.Request.ShouldNotBeNull().TerminalName.ShouldBe("dumb");
        fixture.Screen.Dispatcher.ShouldBeNull();
        fixture.Transport.Writes.ShouldBeEmpty();
        fixture.Transport.Disposals.ShouldBe(1);
        fixture.Resize.Disposals.ShouldBe(1);
        fixture.Restore.Disposals.ShouldBe(1);
        fixture.DisposalOrder.ShouldBe(["resize", "transport", "restore"]);
    }

    private static void AssertResolution(
        string resultKind,
        DescriptionResult resolution,
        string message)
    {
        switch (resultKind)
        {
            case "generic":
                resolution.Status.ShouldBe(DescriptionLoadStatus.Loaded);
                resolution.Profile.ShouldNotBeNull().Description.Suitability.ShouldBe(Suitability.Generic);
                message.ShouldContain(nameof(Suitability.Generic));
                break;
            case "provider-failed":
                resolution.Status.ShouldBe(DescriptionLoadStatus.ProviderFailed);
                resolution.Diagnostics.Select(static value => value.Code).ShouldBe(
                    [
                        DescriptionDiagnosticCode.NativeFailure,
                        DescriptionDiagnosticCode.EnvironmentLimit,
                        DescriptionDiagnosticCode.CleanupFailure
                    ]);
                message.ShouldContain(nameof(DescriptionLoadStatus.ProviderFailed));
                message.ShouldContain(nameof(DescriptionDiagnosticCode.NativeFailure));
                message.ShouldContain(nameof(DescriptionDiagnosticCode.EnvironmentLimit));
                message.ShouldContain(nameof(DescriptionDiagnosticCode.CleanupFailure));
                break;
            case "missing":
                resolution.Status.ShouldBe(DescriptionLoadStatus.MissingOrGeneric);
                resolution.Diagnostics.ShouldHaveSingleItem().Code.ShouldBe(
                    DescriptionDiagnosticCode.MissingOrGeneric);
                message.ShouldContain(nameof(DescriptionLoadStatus.MissingOrGeneric));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resultKind));
        }
    }

    private static DescriptionResult DescriptionResultFor(string resultKind) => resultKind switch
    {
        "generic" => DescriptionResult.Loaded(
            new TerminalProfile(
                new Description("dumb", DescriptionOrigin.Database, Suitability.Generic),
                TerminalCapabilities.Conservative),
            Array.Empty<DescriptionDiagnostic>()),
        "provider-failed" => DescriptionResult.ProviderFailed(
            [
                new DescriptionDiagnostic(DescriptionDiagnosticCode.NativeFailure),
                new DescriptionDiagnostic(DescriptionDiagnosticCode.EnvironmentLimit),
                new DescriptionDiagnostic(DescriptionDiagnosticCode.CleanupFailure)
            ]),
        "missing" => DescriptionResult.MissingOrGeneric(
            [new DescriptionDiagnostic(DescriptionDiagnosticCode.MissingOrGeneric)]),
        "ansi-fallback" => DescriptionResult.Loaded(
            XtermProfile(),
            [new DescriptionDiagnostic(DescriptionDiagnosticCode.AnsiFallback)]),
        _ => throw new ArgumentOutOfRangeException(nameof(resultKind))
    };

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

    /// <summary>Verifies Build()'s failure-cleanup catch preserves the ORIGINAL attach failure
    /// instead of letting the ensuing <c>Application.DisposeAsync()</c> cleanup call replace it
    /// with a disposal failure, when the owning dispatcher's queue is still saturated and never
    /// drains within <see cref="ConsoleRunOptions.CleanupTimeout"/> - the exact race that made
    /// <c>Application.DisposeAsync()</c> throw a fresh exception in place of the one Build() was
    /// already failing with.</summary>
    [Fact]
    public async Task Build_WhenAttachFailsAndDisposalAlsoFailsFromQueueExhaustion_PreservesOriginalFailureAsync()
    {
        var screen = new QueueSaturatingAttachFailureScreen();
        var builder = new ConsoleApplicationBuilder(
            screen,
            static () => true,
            _ => new ConsoleConnection(
                new ConsoleApplicationTransport(),
                new ConsoleApplicationResizeSource(),
                new ConsoleApplicationRestoreLease()),
            _ => { },
            _ => { })
            .UseTerminalProfile(TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative))
            .WithCleanupTimeout(TimeSpan.FromMilliseconds(50));

        // Build() calls Application.DisposeAsync() synchronously from its failure-cleanup catch,
        // which blocks the calling thread until the bounded retry (Application's own
        // InvokeWithQueueRetryAsync) exhausts CleanupTimeout - so Build() itself must run on a
        // background thread while this method controls when the saturated queue is released.
        var buildTask = Task.Run(() => builder.Build());

        // Give the bounded disposal retry room to observe the queue still full for the entire
        // CleanupTimeout window and give up, before the dispatcher thread is freed.
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        screen.Release();

        var thrown = await Should.ThrowAsync<InvalidOperationException>(
            () => buildTask.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        thrown.Message.ShouldBe("The original attach failure.");
    }

    /// <summary>Fails from OnAttach - which already runs on the dispatcher thread being attached
    /// to - after first saturating that same dispatcher's queue with a permanently blocked
    /// callback, matching <c>ApplicationTests.SaturateQueueAsync</c>'s recipe but issued from
    /// inside the callback itself since OnAttach already runs on the dispatcher thread. Used to
    /// force <c>ConsoleApplicationBuilder.Build()</c>'s subsequent <c>Application.DisposeAsync()</c>
    /// cleanup call to fail too, so a test can verify the original attach failure still wins.</summary>
    private sealed class QueueSaturatingAttachFailureScreen: Screen
    {
        private readonly ManualResetEventSlim _release = new();

        internal QueueSaturatingAttachFailureScreen() => InitializeContent(new ProbeControl());

        /// <summary>Frees the dispatcher thread blocked behind this screen's attach failure.</summary>
        internal void Release() => _release.Set();

        /// <inheritdoc/>
        protected override void OnAttach(Application application)
        {
            var dispatcher = application.Dispatcher;

            // Blocks the dispatcher thread on the very next queued item, then fills the
            // remaining capacity, so the queue stays permanently full - and every cleanup post
            // Application.DisposeAsync attempts keeps failing - until the test calls Release().
            dispatcher.Post(_release.Wait);

            while (true)
            {
                try
                {
                    dispatcher.Post(static () => { });
                }
                catch (InvalidOperationException)
                {
                    break;
                }
            }

            throw new InvalidOperationException("The original attach failure.");
        }
    }
}
