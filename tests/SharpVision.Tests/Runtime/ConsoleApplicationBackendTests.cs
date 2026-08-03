// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Runtime;

using Terminal.Backends;

/// <summary>Proves console hosting retains one terminal-context lineage through discovery and cleanup.</summary>
/// <remarks>
/// The session owns the only terminal context. The application keeps just the immutable profile,
/// so there is no second lineage that could drift from the resolved backend identity.
/// </remarks>
public sealed class ConsoleApplicationBackendTests
{
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
                // The terminating fence (see #247): a trailing CSI 6n.
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
                "\u001b[<u\u001b[?1006l\u001b[?1003l\u001b[?2004l" +
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
        var ansi = TerminalProfile.CreateAnsi(Capabilities.Conservative);
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
}
