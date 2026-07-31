// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

using RenderMetrics = RenderingMetrics;

/// <summary>
/// Verifies commit-on-success rendering, invalidation, cleanup, and backpressure.
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
            new Programs(new Dictionary<string, Program>
            {
                ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new Program("\u001b[0m"u8),
                ["clear"] = new Program("\u001b[2J"u8),
                ["civis"] = new Program("\u001b[?25l"u8),
                ["cnorm"] = new Program("\u001b[?25h"u8),
                ["setaf"] = new Program(
                    "%?%p1%{1}%=%t\u001b[38;5;1m%eBROKEN%p1%PA%{1}%{0}%/%d%;"u8),
                ["setab"] = new Program("\u001b[48;5;%p1%dm"u8)
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
        using var second = CreateStyled(new CellStyle(attributes: Attributes.Bold), visible: false);

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
        using var second = CreateStyled(new CellStyle(attributes: Attributes.Bold), visible: false);

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
            new Programs(new Dictionary<string, Program>
            {
                ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new Program("\u001b[0m"u8),
                ["clear"] = new Program("%?%gA%{5}%=%t%{1}%{0}%/%d%e\u001b[2J%;"u8),
                ["setaf"] = new Program("%p1%PA\u001b[38;5;%p1%dm"u8),
                ["setab"] = new Program("\u001b[48;5;%p1%dm"u8),
                ["civis"] = new Program("\u001b[?25l"u8),
                ["cnorm"] = new Program("\u001b[?25h"u8)
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
            $"S%p1%{Limits.Default.MaxProgramOutputBytes}d");
        var profile = new TerminalProfile(
            new Description("cursor-live-failure", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative,
            new Programs(new Dictionary<string, Program>
            {
                ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
                ["sgr0"] = new Program("\u001b[0m"u8),
                ["clear"] = new Program("\u001b[2J"u8),
                ["civis"] = new Program("\u001b[?25l"u8),
                ["cnorm"] = new Program("\u001b[?25h"u8),
                ["Ss"] = new Program(shapeSource),
                ["Se"] = new Program("\u001b[0 q"u8)
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
        using var recovery = CreateStyled(new CellStyle(attributes: Attributes.Bold), visible: false);
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

        // The framing must be byte-identical to Modes.SynchronizedOutput's own encoding rather
        // than a hand-typed literal that happens to agree with it today (see #93 item 2).
        var expectedBegin = new ArrayBufferWriter<byte>();
        Modes.SynchronizedOutput(new Writer(expectedBegin), enabled: true);
        var expectedEnd = new ArrayBufferWriter<byte>();
        Modes.SynchronizedOutput(new Writer(expectedEnd), enabled: false);
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
        new Programs(new Dictionary<string, Program>
        {
            ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new Program("\u001b[0m"u8),
            ["clear"] = new Program("\u001b[2J"u8),
            ["setaf"] = new Program("%p1%PA\u001b[38;5;%p1%dm"u8),
            ["setab"] = new Program("\u001b[48;5;%p1%dm"u8),
            ["bold"] = new Program("\u001b[%gA%dm"u8),
            ["civis"] = new Program("\u001b[?25l"u8),
            ["cnorm"] = new Program(
                failingNormalCursor
                    ? "%?%gA%{9}%=%t%{1}%{0}%/%d%e\u001b[?25h%;"u8
                    : "\u001b[?25h"u8)
        }),
        KeyMap.Empty);

    private static TerminalProfile CreateFailingRequiredProfile(string failingProgram)
    {
        var programs = new Dictionary<string, Program>
        {
            ["cup"] = new Program("\u001b[%i%p1%d;%p2%dH"u8),
            ["sgr0"] = new Program("\u001b[0m"u8),
            ["clear"] = new Program("\u001b[2J"u8),
            ["setaf"] = new Program("\u001b[38;5;%p1%dm"u8),
            ["setab"] = new Program("\u001b[48;5;%p1%dm"u8),
            ["civis"] = new Program("\u001b[?25l"u8),
            ["cnorm"] = new Program("\u001b[?25h"u8)
        };

        if (string.Equals(failingProgram, "ed", StringComparison.Ordinal))
        {
            _ = programs.Remove("clear");
            programs["el"] = new Program("\u001b[K"u8);
            programs["ed"] = new Program("%p1%d"u8);
        }

        programs[failingProgram] = new Program("%p1%d"u8);

        return new TerminalProfile(
            new Description("failing-required", DescriptionOrigin.Database, Suitability.Usable),
            TerminalCapabilities.Conservative with { ColorDepth = ColorDepth.Indexed256 },
            new Programs(programs),
            KeyMap.Empty);
    }

    private static TerminalProfile CreateProfile(ReadOnlySpan<byte> cup) => new(
        new Description("renderer-test", DescriptionOrigin.Database, Suitability.Usable),
        TerminalCapabilities.Conservative,
        new Programs(new Dictionary<string, Program>
        {
            ["cup"] = new Program(cup),
            ["sgr0"] = new Program("\u001b[0m"u8),
            ["clear"] = new Program("\u001b[2J"u8),
            ["civis"] = new Program("\u001b[?25l"u8),
            ["cnorm"] = new Program("\u001b[?25h"u8)
        }),
        KeyMap.Empty);
}
