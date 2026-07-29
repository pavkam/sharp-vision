// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Rendering;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies the committed frame stays renderer-owned and is reachable only for reading.
/// </summary>
public sealed class RendererCommittedFrameTests
{
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
}
