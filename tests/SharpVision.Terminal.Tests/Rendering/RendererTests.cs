// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using System.Runtime.CompilerServices;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;

/// <summary>
/// Verifies commit-on-success rendering, invalidation, cleanup, and backpressure; atomic cell and
/// semantic-graphics renderer transactions; committed-frame read access; and explicit
/// borrowed-transport shutdown.
/// </summary>
[Collection(PerformanceGroup.Name)]
public sealed class RendererTests
{
    /// <summary>Verifies an optional actual expansion failure still commits one safely degraded frame.</summary>
    [Fact]
    public async Task RenderAsync_WhenActualOptionalColorExpansionFails_CommitsDegradedFrameAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default, new CellStyle(ReferenceColors.Get(2)));
        var profile = new TerminalProfile(
            new Description("optional-failure", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 },
            new Programs(new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("\u001b[2J"u8),
                ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8),
                ["setaf"] = new DescriptionProgram(
                    "%?%p1%{1}%=%t\u001b[38;5;1m%eBROKEN%p1%PA%{1}%{0}%/%d%;"u8),
                ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8)
            }),
            KeyMap.Empty);

        var result = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken);

        result.Full.ShouldBeTrue();
        transport.Writes.Count.ShouldBe(1);
        var screen = new VirtualScreen(frame.Size);
        screen.Apply(transport.Writes[0]);
        using Frame expected = new(new Size(1, 1));
        _ = expected.Canvas.Draw("x", default);
        screen.ShouldMatch(expected);
    }

    /// <summary>Verifies a description-program change forces a full redraw even with equal capabilities.</summary>
    [Fact]
    public async Task RenderAsync_WhenDescriptionProgramChanges_ForcesFullRedrawAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("x");
        var firstProfile = CreateProfile("\u001b[%i%p1%d;%p2%dH"u8);
        var secondProfile = CreateProfile("\u001b[%i%p1%d;%p2%df"u8);

        _ = await renderer.RenderAsync(
            frame,
            transport,
            firstProfile,
            TestContext.Current.CancellationToken);
        var changed = await renderer.RenderAsync(
            frame,
            transport,
            secondProfile,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        transport.Writes.Count.ShouldBe(2);
        transport.Writes[1].AsSpan().IndexOf("\u001b[1;1f"u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies one renderer preserves ncurses uppercase variables across committed frames.</summary>
    [Fact]
    public async Task RenderAsync_WhenProgramsUseStaticVariable_PreservesItAcrossFramesAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        var profile = CreateStatefulProfile("stateful", failingNormalCursor: false);
        using var first = CreateStyled(new CellStyle(ReferenceColors.Get(5)), visible: false);
        using var second = CreateStyled(new CellStyle(attributes: TerminalAttributes.Bold), visible: false);

        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        _ = await renderer.RenderAsync(second, transport, profile, TestContext.Current.CancellationToken);

        transport.Writes[1].AsSpan().IndexOf("\u001b[5m"u8).ShouldBeGreaterThanOrEqualTo(0);
    }

    /// <summary>Verifies a different terminal description starts with independent static variables.</summary>
    [Fact]
    public async Task RenderAsync_WhenProfileChanges_DoesNotLeakStaticVariablesAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var first = CreateStyled(new CellStyle(ReferenceColors.Get(5)), visible: false);
        using var second = CreateStyled(new CellStyle(attributes: TerminalAttributes.Bold), visible: false);

        _ = await renderer.RenderAsync(
            first,
            transport,
            CreateStatefulProfile("first", failingNormalCursor: false),
            TestContext.Current.CancellationToken);
        _ = await renderer.RenderAsync(
            second,
            transport,
            CreateStatefulProfile("second", failingNormalCursor: false),
            TestContext.Current.CancellationToken);

        transport.Writes[1].AsSpan().IndexOf("\u001b[0m"u8).ShouldBeGreaterThanOrEqualTo(0);
        transport.Writes[1].AsSpan().IndexOf("\u001b[5m"u8).ShouldBe(-1);
    }

    /// <summary>Verifies a conditional cursor pair cannot commit visibility from a live-static miss.</summary>
    [Fact]
    public async Task RenderAsync_WhenConditionalNormalCursorDependsOnLiveStaticState_OmitsVisibilityPairAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        var profile = CreateStatefulProfile("conditional-cnorm", failingNormalCursor: true);
        using var committed = CreateStyled(new CellStyle(ReferenceColors.Get(5)), visible: false);
        using var failing = CreateStyled(new CellStyle(ReferenceColors.Get(9)), visible: true);
        _ = await renderer.RenderAsync(
            committed,
            transport,
            profile,
            TestContext.Current.CancellationToken);

        _ = await renderer.RenderAsync(
            failing,
            transport,
            profile,
            TestContext.Current.CancellationToken);

        transport.Writes.Count.ShouldBe(2);
        var screen = new VirtualScreen(failing.Size);
        screen.Apply(transport.Writes[0]);
        screen.Apply(transport.Writes[1]);
        screen.ShouldMatch(failing);
    }

    /// <summary>Verifies an actual required-program failure leaves its frame batch byte-quiet.</summary>
    [Fact]
    public async Task RenderAsync_WhenActualRequiredProgramExpansionFails_WritesNothingAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = CreateStyled(new CellStyle(ReferenceColors.Get(5)), visible: false);
        var profile = new TerminalProfile(
            new Description("required-actual-failure", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 },
            new Programs(new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("%?%gA%{5}%=%t%{1}%{0}%/%d%e\u001b[2J%;"u8),
                ["setaf"] = new DescriptionProgram("%p1%PA\u001b[38;5;%p1%dm"u8),
                ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8),
                ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
            }),
            KeyMap.Empty);
        _ = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken);
        renderer.Invalidate();

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                profile,
                TestContext.Current.CancellationToken));

        transport.Writes.Count.ShouldBe(1);
    }

    /// <summary>Verifies an accepted cursor transition that fails live aborts and retries the staged frame.</summary>
    [Fact]
    public async Task RenderAsync_WhenAcceptedCursorShapeFailsLive_AbortsAndRetriesFrameAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = CreateStyled(default, visible: true);
        frame.SetCursor(default, visible: true, CursorShape.Bar);
        var shapeSource = Encoding.ASCII.GetBytes(
            $"S%p1%{ProgramLimits.Default.MaxProgramOutputBytes}d");
        var profile = new TerminalProfile(
            new Description("cursor-live-failure", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative,
            new Programs(new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
                ["clear"] = new DescriptionProgram("\u001b[2J"u8),
                ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8),
                ["Ss"] = new DescriptionProgram(shapeSource),
                ["Se"] = new DescriptionProgram("\u001b[0 q"u8)
            }),
            KeyMap.Empty);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                profile,
                TestContext.Current.CancellationToken));
        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                profile,
                TestContext.Current.CancellationToken));

        transport.Writes.ShouldBeEmpty();
    }

    /// <summary>Verifies required full-redraw failures neither write bytes nor replace committed program state.</summary>
    /// <param name="failingProgram">The required program that fails during the candidate profile's full redraw.</param>
    [Theory]
    [InlineData("sgr0")]
    [InlineData("clear")]
    [InlineData("ed")]
    public async Task RenderAsync_WhenRequiredFullRedrawProgramFails_RollsBackCandidateProfileAsync(
        string failingProgram)
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        var committedProfile = CreateStatefulProfile("committed", failingNormalCursor: false);
        using var committed = CreateStyled(new CellStyle(ReferenceColors.Get(5)), visible: false);
        using var candidate = CreateStyled(new CellStyle(ReferenceColors.Get(9)), visible: false);
        using var recovery = CreateStyled(new CellStyle(attributes: TerminalAttributes.Bold), visible: false);
        var candidateProfile = CreateFailingRequiredProfile(failingProgram);
        _ = await renderer.RenderAsync(
            committed,
            transport,
            committedProfile,
            TestContext.Current.CancellationToken);

        candidateProfile.Description.Suitability.ShouldBe(Suitability.Incomplete);
        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.RenderAsync(
                candidate,
                transport,
                candidateProfile,
                TestContext.Current.CancellationToken));
        var recovered = await renderer.RenderAsync(
            recovery,
            transport,
            committedProfile,
            TestContext.Current.CancellationToken);

        recovered.Full.ShouldBeTrue();
        transport.Writes.Count.ShouldBe(2);
        transport.Writes[1].AsSpan().IndexOf("\u001b[5m"u8).ShouldBeGreaterThanOrEqualTo(0);
        transport.Writes[1].AsSpan().IndexOf("\u001b[9m"u8).ShouldBe(-1);
    }

    /// <summary>
    /// Verifies a successful frame commits and an identical frame becomes a no-op.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenWriteSucceeds_CommitsOnlyOnceAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("ab");

        var first = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var second = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        first.Full.ShouldBeTrue();
        first.Writes.ShouldBe(1);
        first.Bytes.ShouldBeGreaterThan(0);
        second.ShouldBe(new RenderMetrics(
            0,
            0,
            0,
            false,
            second.Elapsed));
        transport.Writes.Count.ShouldBe(1);
    }

    /// <summary>
    /// Verifies explicit and capability invalidation force a complete redraw.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenStateIsInvalidated_ForcesFullRedrawAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("ab");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        renderer.Invalidate();
        var explicitResult = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var changedCapabilities = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };
        var capabilityResult = await renderer.RenderAsync(
            frame,
            transport,
            changedCapabilities,
            TestContext.Current.CancellationToken);

        explicitResult.Full.ShouldBeTrue();
        capabilityResult.Full.ShouldBeTrue();
    }

    /// <summary>Verifies a color-tier change reprojects the complete semantic frame.</summary>
    [Fact]
    public async Task RenderAsync_WhenColorDepthChanges_WritesDifferentFullRepresentationAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using Frame frame = new(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default, new CellStyle(Color.Rgb(255, 0, 0)));
        var trueColor = TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.TrueColor };

        var first = await renderer.RenderAsync(
            frame,
            transport,
            trueColor,
            TestContext.Current.CancellationToken);
        var second = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        first.Full.ShouldBeTrue();
        second.Full.ShouldBeTrue();
        transport.Writes.Count.ShouldBe(2);
        transport.Writes[0].AsSpan().IndexOf("\u001b[38;2;255;0;0m"u8).ShouldBeGreaterThanOrEqualTo(0);
        transport.Writes[1].AsSpan().IndexOf("\u001b[91m"u8).ShouldBeGreaterThanOrEqualTo(0);
        transport.Writes[1].AsSpan().IndexOf("38;2"u8).ShouldBe(-1);
    }

    /// <summary>
    /// Verifies changed frame dimensions select a full redraw and replace front geometry.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenFrameSizeChanges_ForcesFullRedrawAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var first = Create("a");
        using var resized = Create("ab");
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var result = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var unchanged = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        result.Full.ShouldBeTrue();
        unchanged.Writes.ShouldBe(0);
    }

    /// <summary>
    /// Verifies synchronized output wraps only non-empty complete frame batches.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenSynchronizedOutputIsSupported_WrapsExactBatchAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("x");
        var capabilities = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query)
        };

        _ = await renderer.RenderAsync(
            frame,
            transport,
            capabilities,
            TestContext.Current.CancellationToken);
        _ = await renderer.RenderAsync(
            frame,
            transport,
            capabilities,
            TestContext.Current.CancellationToken);

        transport.Writes.Count.ShouldBe(1);
        transport.Writes[0].AsSpan().StartsWith("\u001b[?2026h"u8).ShouldBeTrue();
        transport.Writes[0].AsSpan().EndsWith("\u001b[?2026l"u8).ShouldBeTrue();

        // The framing must be byte-identical to ProtocolModes.SynchronizedOutput's own encoding rather
        // than a hand-typed literal that happens to agree with it today.
        var expectedBegin = new ArrayBufferWriter<byte>();
        ProtocolModes.SynchronizedOutput(new ProtocolWriter(expectedBegin), enabled: true);
        var expectedEnd = new ArrayBufferWriter<byte>();
        ProtocolModes.SynchronizedOutput(new ProtocolWriter(expectedEnd), enabled: false);
        transport.Writes[0].AsSpan()[..expectedBegin.WrittenCount].ToArray().ShouldBe(expectedBegin.WrittenSpan.ToArray());
        transport.Writes[0].AsSpan()[^expectedEnd.WrittenCount..].ToArray().ShouldBe(expectedEnd.WrittenSpan.ToArray());
    }

    /// <summary>
    /// Verifies write failure leaves front state unknown and the next frame full.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenWriteFails_InvalidatesNextFrameAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("ab");
        var failure = new IOException("write failed");
        transport.QueueFailure(failure, prefixBytes: 3);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        thrown.ShouldBeSameAs(failure);
        recovered.Full.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies a flush failure also makes terminal state unknown.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenFlushFails_InvalidatesNextFrameAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("ab");
        transport.FlushFailure = new IOException("flush failed");

        _ = await Should.ThrowAsync<IOException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        recovered.Full.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies cancellation before output does not disturb known committed state.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenCancelledBeforeWrite_PreservesCommittedFrameAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("a");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                cancellation.Token));
        var unchanged = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        unchanged.Writes.ShouldBe(0);
    }

    /// <summary>
    /// Verifies cancellation during an uncertain write forces recovery redraw.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenCancelledDuringWrite_InvalidatesNextFrameAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("a");
        transport.Block();
        using var cancellation = new CancellationTokenSource();
        var pending = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            cancellation.Token).AsTask();
        await transport.WriteStarted;

        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
        transport.Release();
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        recovered.Full.ShouldBeTrue();
    }

    /// <summary>
    /// Verifies cleanup failure is observable without replacing the write failure.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenWriteAndCleanupFail_PreservesOriginalExceptionAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("x");
        var capabilities = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query)
        };
        var original = new IOException("frame failed");
        var cleanup = new IOException("cleanup failed");
        transport.QueueFailure(original, prefixBytes: 2);
        transport.QueueFailure(cleanup);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                capabilities,
                TestContext.Current.CancellationToken));

        thrown.ShouldBeSameAs(original);
        renderer.LastCleanupException.ShouldBeSameAs(cleanup);
    }

    /// <summary>
    /// Verifies a slow transport naturally backpressures and cannot commit early.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTransportIsBlocked_WaitsBeforeCommitAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("a");
        transport.Block();

        var pending = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken).AsTask();
        await transport.WriteStarted;

        pending.IsCompleted.ShouldBeFalse();
        transport.Release();
        var result = await pending;
        result.Writes.ShouldBe(1);
    }

    /// <summary>
    /// Verifies concurrent render attempts are rejected before shared buffer mutation.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenCalledConcurrently_ThrowsInvalidOperationExceptionAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("a");
        transport.Block();
        var first = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken).AsTask();
        await transport.WriteStarted;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
        transport.Release();
        _ = await first;
    }

    /// <summary>
    /// Verifies a finite batch limit fails before any terminal bytes are attempted.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenOutputExceedsLimit_ThrowsBeforeWriteAsync()
    {
        using Renderer renderer = new(maxOutputBytes: 1);
        await using FakeTransport transport = new();
        using var frame = Create("a");

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));

        transport.Writes.ShouldBeEmpty();
    }

    /// <summary>
    /// Verifies a warmed unchanged frame allocates no managed memory.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenFrameIsUnchanged_AllocatesZeroBytesAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = Create("unchanged");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        for (var index = 0; index < 10_000; index++)
        {
            _ = await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                CancellationToken.None);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++)
        {
            _ = await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                CancellationToken.None);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.ShouldBe(0);
    }

    /// <summary>
    /// Verifies renderer disposal is idempotent and never assumes transport ownership.
    /// </summary>
    [Fact]
    public async Task Dispose_WhenCalledTwice_ReleasesOnlyRendererOwnershipAsync()
    {
        var renderer = new Renderer();
        await using FakeTransport transport = new();
        using var frame = Create("a");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        renderer.Dispose();
        renderer.Dispose();

        transport.DisposeCount.ShouldBe(0);
        _ = Should.Throw<ObjectDisposedException>(renderer.Invalidate);
    }

    /// <summary>Verifies a written frame's Elapsed telemetry reflects the injected clock rather
    /// than the wall-clock Stopwatch, so a reversion to <c>Stopwatch.GetTimestamp()</c>/
    /// <c>Stopwatch.GetElapsedTime()</c> at either call site fails this test.</summary>
    [Fact]
    public async Task RenderAsync_WhenTimeProviderIsInjected_ReportsProviderAdvancedElapsedForWrittenFrameAsync()
    {
        var provider = new ManualTimeProvider { AdvanceOnRead = TimeSpan.FromMilliseconds(250) };
        using Renderer renderer = new(timeProvider: provider);
        await using FakeTransport transport = new();
        using var frame = Create("ab");

        var result = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        result.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Verifies an identical no-op frame's Elapsed telemetry - the early-return path that
    /// never reaches transport I/O - also reflects the injected clock.</summary>
    [Fact]
    public async Task RenderAsync_WhenTimeProviderIsInjected_ReportsProviderAdvancedElapsedForNoOpFrameAsync()
    {
        var provider = new ManualTimeProvider { AdvanceOnRead = TimeSpan.FromMilliseconds(250) };
        using Renderer renderer = new(timeProvider: provider);
        await using FakeTransport transport = new();
        using var frame = Create("ab");
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var result = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        result.Writes.ShouldBe(0);
        result.Elapsed.ShouldBe(TimeSpan.FromMilliseconds(250));
    }

    /// <summary>Verifies a missing backend preserves the ordinary text render as fallback.</summary>
    [Fact]
    public async Task RenderAsync_WhenNoBackendExists_EmitsOnlyTextFallbackAsync()
    {
        using Renderer expectedRenderer = new();
        using Renderer actualRenderer = new();
        await using FakeTransport expectedTransport = new();
        await using FakeTransport actualTransport = new();
        using var expected = CreateGraphicsFrame(withImage: false);
        using var actual = CreateGraphicsFrame(withImage: true);

        _ = await expectedRenderer.RenderAsync(
            expected,
            expectedTransport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        _ = await actualRenderer.RenderAsync(
            actual,
            actualTransport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        actualTransport.Writes.Single().ShouldBe(expectedTransport.Writes.Single());
        Encoding.UTF8.GetString(actualTransport.Writes.Single()).ShouldContain("text");
    }

    /// <summary>Verifies a backend that already exists is left untouched by republished capability
    /// evidence - it already re-checks authoritative support per frame and gracefully self-cleans
    /// on revocation, so replacing it here would discard that state before it can run.</summary>
    [Fact]
    public async Task UpdateGraphicsBackend_WhenBackendAlreadyExists_LeavesItUntouchedAsync()
    {
        var initialBackend = new FakeGraphicsBackend();
        using Renderer renderer = new(initialBackend);
        await using FakeTransport transport = new();
        using var before = CreateGraphicsFrame(withImage: true);

        _ = await renderer.RenderAsync(
            before,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(transport.Writes.Single()).ShouldContain("<place>");

        var changed = renderer.UpdateGraphicsBackend(TerminalCapabilities.Conservative, route: null);

        changed.ShouldBeFalse();
        initialBackend.DisposeCount.ShouldBe(0);

        using var after = CreateGraphicsFrame(withImage: true, "other");
        _ = await renderer.RenderAsync(
            after,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        // The pre-existing backend was never swapped, so it keeps handling frames itself.
        Encoding.UTF8.GetString(transport.Writes[^1]).ShouldContain("<place>");
    }

    /// <summary>Verifies a republish that proves Kitty support over an already-active, lesser
    /// backend gracefully drains the retiring backend's placements and swaps in Kitty output on the
    /// very render that performs the upgrade, rather than staying frozen on the inferior protocol
    /// for the rest of the session.</summary>
    [Fact]
    public async Task UpdateGraphicsBackend_WhenKittyBecomesAuthoritativeOverExistingBackend_SwapsGracefullyAsync()
    {
        var retiringBackend = new FakeGraphicsBackend();
        using Renderer renderer = new(retiringBackend);
        await using FakeTransport transport = new();
        using var before = CreateGraphicsFrame(withImage: true);

        _ = await renderer.RenderAsync(
            before,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(transport.Writes.Single()).ShouldContain("<place>");

        var kittyCapabilities = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Query)
        };

        var staged = renderer.UpdateGraphicsBackend(kittyCapabilities, route: null);

        // The upgrade is only staged, not applied yet - the retiring backend keeps ownership of
        // its placement-tracking state until a render can gracefully drain it.
        staged.ShouldBeTrue();
        retiringBackend.DisposeCount.ShouldBe(0);
        retiringBackend.CleanupPrepareCount.ShouldBe(0);

        using var after = CreateGraphicsFrame(withImage: true, "other");
        _ = await renderer.RenderAsync(
            after,
            transport,
            kittyCapabilities,
            TestContext.Current.CancellationToken);

        var output = Encoding.UTF8.GetString(transport.Writes[^1]);

        // (a) The retiring backend's own graceful removal reached the terminal in the same render
        // that performs the swap.
        output.ShouldContain("<cleanup>");
        retiringBackend.CleanupPrepareCount.ShouldBe(1);
        retiringBackend.CleanupCommitCount.ShouldBe(1);
        retiringBackend.DisposeCount.ShouldBe(1);

        // (b) The newly authorized Kitty backend is active and already producing output on that
        // same render.
        output.ShouldContain("_G");

        // A further republish has nothing left to upgrade to.
        renderer.UpdateGraphicsBackend(kittyCapabilities, route: null).ShouldBeFalse();
    }

    /// <summary>Verifies capability evidence actually reaching Kitty authorization activates the protocol.</summary>
    [Fact]
    public async Task UpdateGraphicsBackend_WhenCapabilitiesGainKittySupport_EmitsKittyGraphicsAsync()
    {
        using Renderer renderer = new(TerminalCapabilities.Conservative);
        await using FakeTransport transport = new();
        using var before = CreateGraphicsFrame(withImage: true);

        _ = await renderer.RenderAsync(
            before,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(transport.Writes.Single()).ShouldNotContain("_G");

        var kittyCapabilities = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Query)
        };

        renderer.UpdateGraphicsBackend(kittyCapabilities, route: null).ShouldBeTrue();

        using var after = CreateGraphicsFrame(withImage: true, "other");
        _ = await renderer.RenderAsync(
            after,
            transport,
            kittyCapabilities,
            TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(transport.Writes[^1]).ShouldContain("_G");
    }

    /// <summary>Verifies backend selection is skipped while a frame render is in flight, and applies
    /// once the render completes.</summary>
    [Fact]
    public async Task UpdateGraphicsBackend_WhenRenderIsInFlight_IsSkippedThenAppliesAfterwardAsync()
    {
        using var renderer = new Renderer(TerminalCapabilities.Conservative);
        await using var transport = new FakeTransport();
        using var frame = CreateGraphicsFrame(withImage: true);
        transport.Block();
        var render = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            CancellationToken.None).AsTask();
        await transport.WriteStarted;

        var kittyCapabilities = TerminalCapabilities.Conservative with
        {
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Query)
        };

        var duringRender = renderer.UpdateGraphicsBackend(kittyCapabilities, route: null);

        transport.Release();
        _ = await render;

        duringRender.ShouldBeFalse();

        var afterRender = renderer.UpdateGraphicsBackend(kittyCapabilities, route: null);

        afterRender.ShouldBeTrue();

        using var after = CreateGraphicsFrame(withImage: true, "other");
        _ = await renderer.RenderAsync(
            after,
            transport,
            kittyCapabilities,
            TestContext.Current.CancellationToken);

        Encoding.UTF8.GetString(transport.Writes[^1]).ShouldContain("_G");
    }

    /// <summary>Verifies resize reconstructs unchanged graphics and commits the new exact front.</summary>
    [Fact]
    public async Task RenderAsync_WhenFrameSizeChanges_ReconstructsGraphicsAndCommitsExactFrontAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        var image = CreateImage(1);
        using var first = CreateGraphicsFrame(new Size(4, 1), image, Ambiguous.Narrow);
        using var resized = CreateGraphicsFrame(new Size(5, 1), image, Ambiguous.Narrow);
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var changed = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var unchanged = await renderer.RenderAsync(
            resized,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        unchanged.Bytes.ShouldBe(0);
        backend.FullPreparations.ShouldBe([true, true, false]);
        backend.UploadWriteCount.ShouldBe(2);
        backend.PlacementWriteCount.ShouldBe(2);
    }

    /// <summary>Verifies ambiguous-width policy changes reconstruct unchanged graphics.</summary>
    [Fact]
    public async Task RenderAsync_WhenAmbiguousWidthChanges_ReconstructsGraphicsAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        var image = CreateImage(1);
        using var narrow = CreateGraphicsFrame(new Size(4, 1), image, Ambiguous.Narrow);
        using var wide = CreateGraphicsFrame(new Size(4, 1), image, Ambiguous.Wide);
        _ = await renderer.RenderAsync(
            narrow,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var changed = await renderer.RenderAsync(
            wide,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        backend.FullPreparations.ShouldBe([true, true]);
        backend.UploadWriteCount.ShouldBe(2);
        backend.PlacementWriteCount.ShouldBe(2);
    }

    /// <summary>Verifies uploads precede cells and placements while removals follow replacement output.</summary>
    [Fact]
    public async Task RenderAsync_WhenGraphicsChange_OrdersBackendLifecycleAroundCellOutputAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var first = CreateGraphicsFrame(withImage: true, "old!");
        using var frame = CreateGraphicsFrame(withImage: true);

        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        transport.Writes.Clear();

        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        var output = Encoding.UTF8.GetString(transport.Writes.Single());
        var upload = output.IndexOf("<upload>", StringComparison.Ordinal);
        var cell = output.IndexOf("text", StringComparison.Ordinal);
        var placement = output.IndexOf("<place>", StringComparison.Ordinal);
        var removal = output.IndexOf("<remove>", StringComparison.Ordinal);
        upload.ShouldBeGreaterThanOrEqualTo(0);
        cell.ShouldBeGreaterThan(upload);
        placement.ShouldBeGreaterThan(cell);
        removal.ShouldBeGreaterThan(placement);
        backend.CommitCount.ShouldBe(2);
    }

    /// <summary>Verifies backend preparation failure occurs before transport I/O.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendEncodingFails_WritesNothingAndForcesFullRepairAsync()
    {
        var backend = new FakeGraphicsBackend { FailPlacements = true };
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));

        transport.Writes.ShouldBeEmpty();
        backend.CommitCount.ShouldBe(0);
        backend.InvalidateCount.ShouldBe(1);
        backend.FailPlacements = false;
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        backend.FullPreparations.ShouldBe([true, true]);
    }

    /// <summary>Verifies backend allocation/preparation failure occurs before transport I/O.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendPrepareFails_WritesNothingAsync()
    {
        var backend = new FakeGraphicsBackend { FailPrepare = true };
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));

        transport.Writes.ShouldBeEmpty();
        backend.CommitCount.ShouldBe(0);
        backend.InvalidateCount.ShouldBe(1);
    }

    /// <summary>Verifies prepare failure after a commit schedules a complete repair.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendPrepareFailsAfterCommit_RetriesFullAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var first = CreateGraphicsFrame(withImage: true, "old!");
        using var second = CreateGraphicsFrame(withImage: true);
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        backend.FailPrepare = true;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));

        backend.FailPrepare = false;
        _ = await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        backend.FullPreparations.ShouldBe([true, false, true]);
    }

    /// <summary>Verifies failed terminal output invalidates graphics and does not commit backend state.</summary>
    [Fact]
    public async Task RenderAsync_WhenTransportFails_InvalidatesBackendAndRetriesFullAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);
        transport.QueueFailure(new IOException("write failure"));

        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));

        backend.CommitCount.ShouldBe(0);
        backend.InvalidateCount.ShouldBe(1);
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        backend.FullPreparations.ShouldBe([true, true]);
        backend.CommitCount.ShouldBe(1);
    }

    /// <summary>Verifies flush failure invalidates backend and semantic front before full repair.</summary>
    [Fact]
    public async Task RenderAsync_WhenGraphicsFlushFails_InvalidatesBackendAndRetriesFullAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var first = CreateGraphicsFrame(withImage: true, "old!");
        using var second = CreateGraphicsFrame(withImage: true);
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        var failure = new IOException("flush failure");
        transport.FlushFailure = failure;

        var thrown = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));
        var recovered = await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        thrown.ShouldBeSameAs(failure);
        backend.InvalidateCount.ShouldBe(1);
        backend.FullPreparations.ShouldBe([true, false, true]);
        recovered.Full.ShouldBeTrue();
    }

    /// <summary>Verifies cancellation during graphics output invalidates and fully repairs state.</summary>
    [Fact]
    public async Task RenderAsync_WhenGraphicsOutputIsCancelled_InvalidatesBackendAndRetriesFullAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var first = CreateGraphicsFrame(withImage: true, "old!");
        using var second = CreateGraphicsFrame(withImage: true);
        _ = await renderer.RenderAsync(
            first,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        transport.Writes.Clear();
        transport.Block();
        using var cancellation = new CancellationTokenSource();
        var pending = renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            cancellation.Token).AsTask();
        await transport.WriteStarted;

        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(pending);
        transport.Release();
        var recovered = await renderer.RenderAsync(
            second,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        backend.InvalidateCount.ShouldBe(1);
        backend.CommitCount.ShouldBe(2);
        backend.FullPreparations.ShouldBe([true, false, true]);
        recovered.Full.ShouldBeTrue();
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies synchronized graphics write and cleanup failures preserve the frame failure.</summary>
    [Fact]
    public async Task RenderAsync_WhenSynchronizedGraphicsWriteAndCleanupFail_PreservesOriginalAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);
        var profile = CreateSynchronizedProfile(ColorDepth.Basic16);
        var original = new IOException("graphics write failure");
        var cleanup = new IOException("graphics cleanup failure");
        transport.QueueFailure(original, prefixBytes: 2);
        transport.QueueFailure(cleanup);

        var thrown = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken));
        var observedCleanup = renderer.LastCleanupException;
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken);

        thrown.ShouldBeSameAs(original);
        observedCleanup.ShouldBeSameAs(cleanup);
        backend.InvalidateCount.ShouldBe(1);
        backend.CommitCount.ShouldBe(1);
        backend.FullPreparations.ShouldBe([true, true]);
        recovered.Full.ShouldBeTrue();
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies synchronized graphics flush and cleanup failures preserve the flush failure.</summary>
    [Fact]
    public async Task RenderAsync_WhenSynchronizedGraphicsFlushAndCleanupFail_PreservesOriginalAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);
        var profile = CreateSynchronizedProfile(ColorDepth.Basic16);
        var original = new IOException("graphics flush failure");
        var cleanup = new IOException("graphics cleanup flush failure");
        transport.QueueFlushFailure(original);
        transport.QueueFlushFailure(cleanup);

        var thrown = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken));
        var observedCleanup = renderer.LastCleanupException;
        var recovered = await renderer.RenderAsync(
            frame,
            transport,
            profile,
            TestContext.Current.CancellationToken);

        thrown.ShouldBeSameAs(original);
        observedCleanup.ShouldBeSameAs(cleanup);
        backend.InvalidateCount.ShouldBe(1);
        backend.CommitCount.ShouldBe(1);
        backend.FullPreparations.ShouldBe([true, true]);
        recovered.Full.ShouldBeTrue();
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies profile changes reconstruct otherwise unchanged remote graphics.</summary>
    [Fact]
    public async Task RenderAsync_WhenProfileChanges_ReconstructsGraphicsAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);
        var first = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        var second = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Monochrome
        });
        _ = await renderer.RenderAsync(frame, transport, first, TestContext.Current.CancellationToken);

        var changed = await renderer.RenderAsync(
            frame,
            transport,
            second,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        backend.FullPreparations.ShouldBe([true, true]);
        backend.UploadWriteCount.ShouldBe(2);
        backend.PlacementWriteCount.ShouldBe(2);
        backend.CommitCount.ShouldBe(2);
        backend.InvalidateCount.ShouldBe(0);
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies explicit invalidation reconstructs otherwise unchanged remote graphics.</summary>
    [Fact]
    public async Task Invalidate_WhenGraphicsAreCommitted_ForcesCompleteReconstructionAsync()
    {
        var backend = new FakeGraphicsBackend();
        using Renderer renderer = new(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        renderer.Invalidate();
        var changed = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        changed.Full.ShouldBeTrue();
        backend.InvalidateCount.ShouldBe(1);
        backend.FullPreparations.ShouldBe([true, true]);
        backend.CommitCount.ShouldBe(2);
        AssertGraphicsOrder(transport.Writes[^1]);
    }

    /// <summary>Verifies render and disposal are excluded while backend preparation owns the writer.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendPrepareIsInFlight_ExcludesRenderAndDisposeAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);
        backend.BlockPrepare();
        var pending = Task.Run(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));
        await backend.PrepareStarted;

        try
        {
            _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));
            _ = Should.Throw<InvalidOperationException>(renderer.Dispose);
        }
        finally
        {
            backend.ReleasePrepare();
        }

        var completed = await pending;
        var unchanged = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        renderer.Dispose();

        completed.Full.ShouldBeTrue();
        unchanged.Bytes.ShouldBe(0);
        backend.CommitCount.ShouldBe(2);
        backend.InvalidateCount.ShouldBe(0);
        backend.DisposeCount.ShouldBe(1);
    }

    /// <summary>Verifies render and disposal are excluded while backend output owns the writer.</summary>
    [Fact]
    public async Task RenderAsync_WhenBackendOutputIsInFlight_ExcludesRenderAndDisposeAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);
        transport.Block();
        var pending = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken).AsTask();
        await transport.WriteStarted;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));
        _ = Should.Throw<InvalidOperationException>(renderer.Dispose);
        transport.Release();
        var completed = await pending;
        var unchanged = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);
        renderer.Dispose();

        completed.Full.ShouldBeTrue();
        unchanged.Bytes.ShouldBe(0);
        backend.CommitCount.ShouldBe(2);
        backend.InvalidateCount.ShouldBe(0);
        backend.DisposeCount.ShouldBe(1);
        AssertGraphicsOrder(transport.Writes.Single());
    }

    /// <summary>
    /// Verifies a render that races a disposal already holding the exclusion flag always observes a
    /// clean mutual-exclusion failure, never an <see cref="ObjectDisposedException"/> surfaced from
    /// partially torn-down state. RenderAsync used to check disposal before claiming the exclusion
    /// flag, so a Dispose() that ran to completion in the window between the disposed-check and the
    /// claim could leave a losing render observing disposed dependencies directly. Pinning Dispose()
    /// mid-flight (after it has claimed the flag and marked the renderer disposed, but before it
    /// finishes releasing owned state) reproduces exactly that window deterministically: under the
    /// old check-then-claim order the concurrent render would see the disposed flag already set and
    /// throw ObjectDisposedException; the fixed claim-then-check order instead loses the exclusion
    /// race outright and throws a clean InvalidOperationException.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenDisposeHoldsRenderingFlag_ThrowsInvalidOperationNeverObjectDisposedAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using FakeTransport transport = new();
        using var frame = new Frame(new Size(1, 1));
        backend.BlockDispose();

        var disposing = Task.Run(renderer.Dispose, TestContext.Current.CancellationToken);
        await backend.DisposeStarted;

        try
        {
            var thrown = await Should.ThrowAsync<InvalidOperationException>(async () => await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                TestContext.Current.CancellationToken));

            thrown.Message.ShouldBe("A frame render is already in progress.");
        }
        finally
        {
            backend.ReleaseDispose();
        }

        await disposing;

        backend.DisposeCount.ShouldBe(1);
        _ = Should.Throw<ObjectDisposedException>(renderer.Invalidate);
    }

    /// <summary>Verifies renderer disposal releases its backend exactly once.</summary>
    [Fact]
    public void Dispose_WhenBackendExists_DisposesOwnedBackendOnce()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);

        renderer.Dispose();
        renderer.Dispose();

        backend.DisposeCount.ShouldBe(1);
    }

    /// <summary>Verifies backend cleanup failure cannot leak local ownership or the writer gate.</summary>
    [Fact]
    public async Task Dispose_WhenBackendThrows_ReleasesLocalOwnershipAndRemainsIdempotentAsync()
    {
        var failure = new IOException("backend cleanup failure");
        var backend = new FakeGraphicsBackend { DisposeFailure = failure };
        var renderer = new Renderer(backend);
        await using FakeTransport transport = new();
        var imageReference = CommitImageAndDisposeBack(renderer, transport);

        var thrown = Should.Throw<IOException>(renderer.Dispose);
        renderer.Dispose();
        ForceCollection();

        thrown.ShouldBeSameAs(failure);
        backend.DisposeCount.ShouldBe(1);
        imageReference.TryGetTarget(out _).ShouldBeFalse();
        _ = Should.Throw<ObjectDisposedException>(renderer.Invalidate);
        using var disposedProbe = new Frame(new Size(1, 1));
        _ = await Should.ThrowAsync<ObjectDisposedException>(async () => await renderer.RenderAsync(
            disposedProbe,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies warmed unchanged semantic placements allocate no managed memory.</summary>
    [Fact]
    public async Task RenderAsync_WhenPlacementIsUnchanged_AllocatesZeroBytesAsync()
    {
        using Renderer renderer = new();
        await using FakeTransport transport = new();
        using var frame = CreateGraphicsFrame(withImage: true);
        _ = await renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            TestContext.Current.CancellationToken);

        for (var index = 0; index < 10_000; index++)
        {
            _ = await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                CancellationToken.None);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 10_000; index++)
        {
            _ = await renderer.RenderAsync(
                frame,
                transport,
                TerminalCapabilities.Conservative,
                CancellationToken.None);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        allocated.ShouldBe(0);
    }

    /// <summary>
    /// Verifies attaching the committed frame lets a target read previous cells without ever
    /// handing out the frame that damage tracking compares against.
    /// </summary>
    [Fact]
    public async Task AttachCommittedFrame_WhenRenderCommitted_ExposesPreviousCellsForCopyingAsync()
    {
        await using MemoryStream output = new();
        await using StreamTransport transport = new(Stream.Null, output, leaveOpen: true);
        using var renderer = new Renderer();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(new Size(4, 1));
        _ = first.Canvas.Draw("A", new Point(0, 0));
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        using var second = new Frame(new Size(4, 1));

        var attached = renderer.AttachCommittedFrame(second);

        attached.ShouldBeTrue();
        second.Canvas.HasPreviousFrame.ShouldBeTrue();
        second.Canvas.CopyFromPrevious(new Rect(0, 0, 4, 1));
        FrameTests.GetText(second, new Point(0, 0)).ShouldBe("A");
    }

    /// <summary>Verifies attaching before any commit reports that nothing is available to copy.</summary>
    [Fact]
    public void AttachCommittedFrame_WhenNothingCommitted_ReportsNoPreviousFrame()
    {
        using var renderer = new Renderer();
        using var frame = new Frame(new Size(4, 1));

        var attached = renderer.AttachCommittedFrame(frame);

        attached.ShouldBeFalse();
        frame.Canvas.HasPreviousFrame.ShouldBeFalse();
    }

    /// <summary>Verifies the attach seam validates its argument and owner state.</summary>
    [Fact]
    public void AttachCommittedFrame_WhenArgumentsAreInvalid_Throws()
    {
        var renderer = new Renderer();
        var frame = new Frame(new Size(4, 1));

        _ = Should.Throw<ArgumentNullException>(() => renderer.AttachCommittedFrame(null!));

        frame.Dispose();
        _ = Should.Throw<ObjectDisposedException>(() => renderer.AttachCommittedFrame(frame));

        renderer.Dispose();
        using var live = new Frame(new Size(4, 1));
        _ = Should.Throw<ObjectDisposedException>(() => renderer.AttachCommittedFrame(live));
    }

    /// <summary>
    /// Verifies a legitimate transition still emits output after the committed frame was exposed
    /// for copying, proving damage tracking stayed synchronized with the terminal.
    /// </summary>
    [Fact]
    public async Task RenderAsync_WhenTargetChangesAfterAttach_EmitsDamageAsync()
    {
        await using MemoryStream output = new();
        await using StreamTransport transport = new(Stream.Null, output, leaveOpen: true);
        using var renderer = new Renderer();
        var profile = TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative);
        using var first = new Frame(new Size(4, 1));
        _ = first.Canvas.Draw("A", new Point(0, 0));
        _ = await renderer.RenderAsync(first, transport, profile, TestContext.Current.CancellationToken);
        using var second = new Frame(new Size(4, 1));
        _ = renderer.AttachCommittedFrame(second);
        _ = second.Canvas.Draw("B", new Point(0, 0));
        var before = output.Length;

        _ = await renderer.RenderAsync(second, transport, profile, TestContext.Current.CancellationToken);

        output.Length.ShouldBeGreaterThan(before);
        Encoding.ASCII.GetString(output.ToArray()).ShouldContain("B");
    }

    /// <summary>Verifies cleanup writes and flushes once before local backend disposal.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenGraphicsWereCommitted_FlushesCleanupAndIsIdempotentAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateShutdownFrame();
        _ = await renderer.RenderAsync(frame, transport, TerminalCapabilities.Conservative, CancellationToken.None);

        await renderer.ShutdownAsync(transport, CancellationToken.None);
        await renderer.ShutdownAsync(transport, CancellationToken.None);

        Encoding.ASCII.GetString(transport.Writes[^1]).ShouldBe("<cleanup>");
        backend.CleanupPrepareCount.ShouldBe(1);
        backend.CleanupCommitCount.ShouldBe(1);
        backend.DisposeCount.ShouldBe(1);
        transport.Flushes.ShouldBe(2);
    }

    /// <summary>Verifies a renderer without a graphics backend performs byte-quiet local shutdown.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenNoBackendExists_WritesNothingAsync()
    {
        var renderer = new Renderer();
        await using var transport = new FakeTransport();

        await renderer.ShutdownAsync(transport, CancellationToken.None);

        transport.Writes.ShouldBeEmpty();
        transport.Flushes.ShouldBe(0);
        _ = Should.Throw<ObjectDisposedException>(renderer.Invalidate);
    }

    /// <summary>Verifies a non-default committed cursor shape is reset back to the terminal default.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenLastFrameUsedNonBlockCursorShape_ResetsShapeAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        var (profile, seBytes) = CreateShapeCapableProfile();
        using var frame = CreateStyled(default, visible: true);
        frame.SetCursor(default, visible: true, CursorShape.Bar);
        _ = await renderer.RenderAsync(frame, transport, profile, TestContext.Current.CancellationToken);
        transport.Writes.Clear();
        var flushesBefore = transport.Flushes;

        await renderer.ShutdownAsync(transport, TestContext.Current.CancellationToken);

        var bytes = transport.Writes.ShouldHaveSingleItem();
        bytes.ShouldBe(seBytes);
        transport.Flushes.ShouldBe(flushesBefore + 1);
    }

    /// <summary>Verifies a committed Block cursor shape needs no shutdown reset write.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenLastFrameUsedBlockCursorShape_WritesNoResetAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        var (profile, _) = CreateShapeCapableProfile();
        using var frame = CreateStyled(default, visible: true);
        _ = await renderer.RenderAsync(frame, transport, profile, TestContext.Current.CancellationToken);
        transport.Writes.Clear();
        var flushesBefore = transport.Flushes;

        await renderer.ShutdownAsync(transport, TestContext.Current.CancellationToken);

        transport.Writes.ShouldBeEmpty();
        transport.Flushes.ShouldBe(flushesBefore);
    }

    /// <summary>Verifies a profile missing the shape capability pair attempts no shutdown reset.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenProfileLacksShapeCapability_WritesNoResetAsync()
    {
        using var renderer = new Renderer();
        await using var transport = new FakeTransport();
        var profile = CreateProfile("\x1b[%i%p1%d;%p2%dH"u8);
        using var frame = CreateStyled(default, visible: true);
        frame.SetCursor(default, visible: true, CursorShape.Bar);
        _ = await renderer.RenderAsync(frame, transport, profile, TestContext.Current.CancellationToken);
        transport.Writes.Clear();
        var flushesBefore = transport.Flushes;

        await renderer.ShutdownAsync(transport, TestContext.Current.CancellationToken);

        transport.Writes.ShouldBeEmpty();
        transport.Flushes.ShouldBe(flushesBefore);
    }

    /// <summary>Verifies shutdown cannot overlap an in-flight frame render.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenRenderIsInFlight_RejectsConcurrencyAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateShutdownFrame();
        transport.Block();
        var render = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            CancellationToken.None).AsTask();
        await transport.WriteStarted;

        _ = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await renderer.ShutdownAsync(transport, CancellationToken.None));
        transport.Release();
        _ = await render;
        await renderer.ShutdownAsync(transport, CancellationToken.None);
    }

    /// <summary>Verifies cancellation invalidates remote state while still disposing local ownership.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenCleanupIsCancelled_ReleasesLocalStateAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateShutdownFrame();
        _ = await renderer.RenderAsync(frame, transport, TerminalCapabilities.Conservative, CancellationToken.None);
        transport.Block();
        using var cancellation = new CancellationTokenSource();
        var shutdown = renderer.ShutdownAsync(transport, cancellation.Token).AsTask();
        await transport.WriteStarted;

        cancellation.Cancel();
        _ = await Should.ThrowAsync<OperationCanceledException>(shutdown);

        backend.InvalidateCount.ShouldBe(1);
        backend.CleanupCommitCount.ShouldBe(0);
        backend.DisposeCount.ShouldBe(1);
        renderer.Dispose();
    }

    /// <summary>Verifies cleanup write failure remains the observed exception after local disposal.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenCleanupWriteFails_PreservesOriginalAndReleasesLocalStateAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateShutdownFrame();
        _ = await renderer.RenderAsync(frame, transport, TerminalCapabilities.Conservative, CancellationToken.None);
        var failure = new IOException("cleanup write failure");
        transport.QueueFailure(failure);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.ShutdownAsync(transport, CancellationToken.None));

        thrown.ShouldBeSameAs(failure);
        backend.InvalidateCount.ShouldBe(1);
        backend.DisposeCount.ShouldBe(1);
        renderer.Dispose();
    }

    /// <summary>Verifies cleanup flush failure remains the observed exception after local disposal.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenCleanupFlushFails_PreservesOriginalAndReleasesLocalStateAsync()
    {
        var backend = new FakeGraphicsBackend();
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateShutdownFrame();
        _ = await renderer.RenderAsync(frame, transport, TerminalCapabilities.Conservative, CancellationToken.None);
        var failure = new IOException("cleanup flush failure");
        transport.QueueFlushFailure(failure);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.ShutdownAsync(transport, CancellationToken.None));

        thrown.ShouldBeSameAs(failure);
        backend.InvalidateCount.ShouldBe(1);
        backend.DisposeCount.ShouldBe(1);
    }

    /// <summary>Verifies the first cleanup diagnostic survives later renders and secondary shutdown failure.</summary>
    [Fact]
    public async Task ShutdownAsync_WhenCleanupDiagnosticAlreadyExists_PreservesFirstFailureAsync()
    {
        var disposeFailure = new IOException("backend dispose failure");
        var backend = new FakeGraphicsBackend { DisposeFailure = disposeFailure };
        var renderer = new Renderer(backend);
        await using var transport = new FakeTransport();
        using var frame = CreateShutdownFrame();
        var capabilities = TerminalCapabilities.Conservative with
        {
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query)
        };
        var renderFailure = new IOException("frame write failure");
        var firstCleanupFailure = new IOException("synchronized cleanup failure");
        transport.QueueFailure(renderFailure, prefixBytes: 1);
        transport.QueueFailure(firstCleanupFailure);
        _ = await Should.ThrowAsync<IOException>(async () => await renderer.RenderAsync(
            frame,
            transport,
            capabilities,
            CancellationToken.None));
        renderer.LastCleanupException.ShouldBeSameAs(firstCleanupFailure);
        _ = await renderer.RenderAsync(frame, transport, capabilities, CancellationToken.None);
        var shutdownFailure = new IOException("shutdown write failure");
        transport.QueueFailure(shutdownFailure);

        var thrown = await Should.ThrowAsync<IOException>(async () =>
            await renderer.ShutdownAsync(transport, CancellationToken.None));

        thrown.ShouldBeSameAs(shutdownFailure);
        renderer.LastCleanupException.ShouldBeSameAs(firstCleanupFailure);
    }

    private static Frame Create(string value)
    {
        var frame = new Frame(new Size(value.Length, 1));
        _ = frame.Canvas.Draw(value.AsSpan(), new Point(0, 0));
        return frame;
    }

    private static Frame CreateStyled(CellStyle style, bool visible)
    {
        var frame = new Frame(new Size(1, 1));
        _ = frame.Canvas.Draw("x", default, style);
        frame.SetCursor(default, visible);
        return frame;
    }

    private static TerminalProfile CreateStatefulProfile(string name, bool failingNormalCursor) => new(
        new Description(name, DescriptionOrigin.Database, Suitability.Usable),
        TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 },
        new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["setaf"] = new DescriptionProgram("%p1%PA\u001b[38;5;%p1%dm"u8),
            ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8),
            ["bold"] = new DescriptionProgram("\u001b[%gA%dm"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram(
                failingNormalCursor
                    ? "%?%gA%{9}%=%t%{1}%{0}%/%d%e\u001b[?25h%;"u8
                    : "\u001b[?25h"u8)
        }),
        KeyMap.Empty);

    private static TerminalProfile CreateFailingRequiredProfile(string failingProgram)
    {
        var programs = new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["setaf"] = new DescriptionProgram("\u001b[38;5;%p1%dm"u8),
            ["setab"] = new DescriptionProgram("\u001b[48;5;%p1%dm"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
        };

        if (string.Equals(failingProgram, "ed", StringComparison.Ordinal))
        {
            _ = programs.Remove("clear");
            programs["el"] = new DescriptionProgram("\u001b[K"u8);
            programs["ed"] = new DescriptionProgram("%p1%d"u8);
        }

        programs[failingProgram] = new DescriptionProgram("%p1%d"u8);

        return new TerminalProfile(
            new Description("failing-required", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 },
            new Programs(programs),
            KeyMap.Empty);
    }

    private static TerminalProfile CreateProfile(ReadOnlySpan<byte> cup) => new(
        new Description("renderer-test", DescriptionOrigin.Database, Suitability.Usable),
        TerminalCapabilities.Conservative,
        new Programs(new Dictionary<string, DescriptionProgram>
        {
            ["cup"] = new DescriptionProgram(cup),
            ["sgr0"] = new DescriptionProgram("\u001b[0m"u8),
            ["clear"] = new DescriptionProgram("\u001b[2J"u8),
            ["civis"] = new DescriptionProgram("\u001b[?25l"u8),
            ["cnorm"] = new DescriptionProgram("\u001b[?25h"u8)
        }),
        KeyMap.Empty);

    /// <summary>Builds a profile advertising the DECSCUSR shape pair alongside the exact reset bytes it expands.</summary>
    private static (TerminalProfile Profile, byte[] SeBytes) CreateShapeCapableProfile()
    {
        var seBytes = "\x1b[0 q"u8.ToArray();
        var profile = new TerminalProfile(
            new Description("shape-capable", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative,
            new Programs(new Dictionary<string, DescriptionProgram>
            {
                ["cup"] = new DescriptionProgram("\x1b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new DescriptionProgram("\x1b[0m"u8),
                ["clear"] = new DescriptionProgram("\x1b[2J"u8),
                ["civis"] = new DescriptionProgram("\x1b[?25l"u8),
                ["cnorm"] = new DescriptionProgram("\x1b[?25h"u8),
                ["Ss"] = new DescriptionProgram("\x1b[%p1%d q"u8),
                ["Se"] = new DescriptionProgram(seBytes)
            }),
            KeyMap.Empty);
        return (profile, seBytes);
    }

    private static Frame CreateGraphicsFrame(bool withImage, string text = "text")
    {
        var frame = new Frame(new Size(4, 1));
        _ = frame.Canvas.Draw(text, default);

        if (withImage)
        {
            frame.Canvas.DrawImage(
                GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]),
                new Rect(0, 0, 2, 1),
                PlacementMode.Contain);
        }

        return frame;
    }

    private static Frame CreateGraphicsFrame(Size size, GraphicsImage image, Ambiguous ambiguous)
    {
        var frame = new Frame(size, ambiguousWidth: ambiguous);
        _ = frame.Canvas.Draw("text", default);
        frame.Canvas.DrawImage(image, new Rect(0, 0, 2, 1), PlacementMode.Contain);
        return frame;
    }

    private static GraphicsImage CreateImage(byte value) =>
        GraphicsImage.FromRgba(new Size(1, 1), [value, value, value, 255]);

    private static TerminalProfile CreateSynchronizedProfile(ColorDepth depth) =>
        TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative with
        {
            ColorDepth = depth,
            SynchronizedOutput = new Feature(CapabilitySupport.Supported, Origin.Query)
        });

    private static void AssertGraphicsOrder(byte[] output)
    {
        var value = Encoding.UTF8.GetString(output);
        var upload = value.IndexOf("<upload>", StringComparison.Ordinal);
        var placement = value.IndexOf("<place>", StringComparison.Ordinal);
        var removal = value.IndexOf("<remove>", StringComparison.Ordinal);
        upload.ShouldBeGreaterThanOrEqualTo(0);
        placement.ShouldBeGreaterThan(upload);

        if (removal >= 0)
        {
            removal.ShouldBeGreaterThan(placement);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<GraphicsImage> CommitImageAndDisposeBack(
        Renderer renderer,
        FakeTransport transport)
    {
        using var frame = CreateGraphicsFrame(withImage: true);
        var reference = new WeakReference<GraphicsImage>(frame.GetPlacement(0).Image!);
        _ = renderer.RenderAsync(
            frame,
            transport,
            TerminalCapabilities.Conservative,
            CancellationToken.None).AsTask().GetAwaiter().GetResult();
        return reference;
    }

    private static void ForceCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static Frame CreateShutdownFrame()
    {
        var frame = new Frame(new Size(1, 1));
        frame.Canvas.DrawImage(
            GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]),
            new Rect(0, 0, 1, 1),
            PlacementMode.Stretch);
        return frame;
    }
}
