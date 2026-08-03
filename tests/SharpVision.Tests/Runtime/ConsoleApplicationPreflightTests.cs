// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

/// <summary>Proves description results compose through complete console-hosting preflight.</summary>
public sealed class ConsoleApplicationPreflightTests
{
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
            messages.Add)
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
        _ => throw new ArgumentOutOfRangeException(nameof(resultKind))
    };
}
