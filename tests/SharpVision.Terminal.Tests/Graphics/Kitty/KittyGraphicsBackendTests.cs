// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Graphics.Kitty;

using SharpVision.Terminal.Capabilities;
using SharpVision.Terminal.Graphics;
using SharpVision.Terminal.Kitty.Graphics;
using SharpVision.Terminal.Multiplexing;

using RenderFrame = Frame;

/// <summary>Proves finite renderer-owned Kitty image and placement transactions.</summary>
[Collection(PerformanceGroup.Name)]
public sealed class KittyGraphicsBackendTests
{
    /// <summary>Verifies retained ownership begins with a collision-safe image number and switches
    /// every later command to the terminal-assigned id returned for that number.</summary>
    [Fact]
    public void Prepare_WhenTerminalAssignsImageId_UsesAssignedIdForLaterPlacementAndCleanup()
    {
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var first = Frame(image, new Rect(0, 0, 1, 1));
        using var moved = Frame(image, new Rect(1, 0, 1, 1));
        using var empty = new RenderFrame(new Size(4, 2));
        _ = backend.Prepare(null, first, full: true);
        var initial = WritePrepared(backend);
        backend.Commit();

        backend.Accept(KittyGraphicsResponse.Parse("Gi=99,I=1;OK"u8));
        _ = backend.Prepare(first, moved, full: false);
        var movement = WritePrepared(backend);
        backend.Commit();
        _ = backend.Prepare(moved, empty, full: false);
        var cleanup = WritePrepared(backend);
        backend.Commit();

        Encoding.ASCII.GetString(initial.Uploads).ShouldContain(",I=1;");
        Encoding.ASCII.GetString(initial.Placements).ShouldContain("a=p,I=1,p=1");
        Encoding.ASCII.GetString(movement.CellPreludes).ShouldContain("a=p,i=99,p=1");
        Encoding.ASCII.GetString(movement.CellPreludes).ShouldContain("U=1");
        movement.Placements.ShouldBeEmpty();
        Encoding.ASCII.GetString(cleanup.Removals).ShouldBe("\u001b_Ga=d,d=I,i=99,q=2\u001b\\");
    }

    /// <summary>Verifies color depths that cannot preserve protocol identifiers retain cursor placement.</summary>
    [Fact]
    public void Prepare_WhenColorDepthCannotEncodeExactPlaceholderIds_UsesCursorPlacement()
    {
        var capabilities = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Basic16,
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Query)
        };
        var context = new GraphicsContext(TerminalProfile.CreateAnsi(capabilities), null);
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var frame = Frame(image, new Rect(0, 0, 1, 1));
        using var moved = Frame(image, new Rect(1, 0, 1, 1));
        _ = backend.Prepare(null, frame, full: true, context);
        _ = WritePrepared(backend);
        backend.Commit();
        backend.Accept(KittyGraphicsResponse.Parse("Gi=42,I=1;OK"u8));

        _ = backend.Prepare(frame, moved, full: false, context);
        var assigned = WritePrepared(backend);

        assigned.CellPreludes.ShouldBeEmpty();
        backend.PreparedCellOverlay.ShouldBeNull();
        Encoding.ASCII.GetString(assigned.Placements).ShouldContain("a=p,i=42,p=1");
    }

    /// <summary>Verifies a placement that was rendered through a virtual placeholder in the
    /// previously committed frame, and loses that eligibility on a later frame purely because the
    /// color depth dropped - with identical placement identity and geometry, and no full
    /// reconstruct - still emits the real placement command. Neither the placeholder loop (gated
    /// on the same eligibility check) nor the real-placement diff (previously keyed only on
    /// reconstruct/identity/geometry) would otherwise have anything left to trigger it, silently
    /// dropping the image.</summary>
    [Fact]
    public void Prepare_WhenPlaceholderEligibilityIsLostWithoutReconstruct_StillEmitsPlacement()
    {
        var basic16Capabilities = TerminalCapabilities.Conservative with
        {
            ColorDepth = ColorDepth.Basic16,
            KittyGraphics = new Feature(CapabilitySupport.Supported, Origin.Query)
        };
        var basic16Context = new GraphicsContext(TerminalProfile.CreateAnsi(basic16Capabilities), null);
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var frame = Frame(image, new Rect(0, 0, 1, 1));

        _ = backend.Prepare(null, frame, full: true);
        _ = WritePrepared(backend);
        backend.Commit();
        backend.Accept(KittyGraphicsResponse.Parse("Gi=42,I=1;OK"u8));

        // Establishes the placeholder-eligible baseline: default (TrueColor) context, identical
        // geometry, full: false. The terminal-assigned id and small placement id keep it eligible.
        _ = backend.Prepare(frame, frame, full: false);
        var baseline = WritePrepared(backend);
        backend.Commit();

        Encoding.ASCII.GetString(baseline.CellPreludes).ShouldContain("a=p,i=42,p=1");
        baseline.Placements.ShouldBeEmpty();

        // Only the color depth changes on this call: same placement identity, same geometry,
        // still full: false. Basic16 makes CanUsePlaceholder false unconditionally.
        _ = backend.Prepare(frame, frame, full: false, basic16Context);
        var afterEligibilityLost = WritePrepared(backend);

        Encoding.ASCII.GetString(afterEligibilityLost.Placements).ShouldContain("a=p,i=42,p=1");
    }

    /// <summary>Verifies shutdown consumes a queued terminal assignment even when no later frame
    /// was prepared, avoiding an unsafe image-number delete during application teardown.</summary>
    [Fact]
    public void PrepareCleanup_WhenAssignmentArrivedAfterLastFrame_DeletesTerminalAssignedId()
    {
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var frame = Frame(image, new Rect(0, 0, 1, 1));
        _ = backend.Prepare(null, frame, full: true);
        _ = WritePrepared(backend);
        backend.Commit();
        backend.Accept(KittyGraphicsResponse.Parse("Gi=99,I=1;OK"u8));
        _ = backend.PrepareCleanup();
        var cleanup = new ArrayBufferWriter<byte>();

        backend.WriteCleanup(cleanup);

        Encoding.ASCII.GetString(cleanup.WrittenSpan).ShouldBe("\u001b_Ga=d,d=I,i=99,q=2\u001b\\");
    }

    /// <summary>Verifies a cleanly retired image whose transmit response has not yet arrived keeps its
    /// client-side number quarantined rather than returning it to the pool immediately, so a later
    /// unrelated image cannot rent the exact same number and later be misattributed by the first
    /// image's own stale terminal response.</summary>
    [Fact]
    public void Accept_WhenStaleResponseArrivesAfterRetiredNumberWasReused_DoesNotMisattributeToLaterImage()
    {
        using var backend = new KittyGraphicsBackend();
        var imageA = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        var imageB = GraphicsImage.FromRgba(new Size(1, 1), [4, 5, 6, 255]);
        using var frameA = Frame(imageA, new Rect(0, 0, 1, 1));
        using var empty = new RenderFrame(new Size(4, 2));
        using var frameB = Frame(imageB, new Rect(0, 0, 1, 1));
        using var frameBMoved = Frame(imageB, new Rect(2, 1, 1, 1));

        // Image A rents Number 1 and transmits. Its assignment response is deliberately never
        // accepted here, simulating a still-outstanding terminal round-trip.
        _ = backend.Prepare(null, frameA, full: true);
        var aBytes = WritePrepared(backend);
        backend.Commit();

        // A's only placement retires. Number 1 must be quarantined rather than returned outright.
        _ = backend.Prepare(frameA, empty, full: false);
        _ = WritePrepared(backend);
        backend.Commit();

        // Image B is prepared and committed next. It must rent a fresh number rather than reuse
        // A's still-quarantined one.
        _ = backend.Prepare(empty, frameB, full: false);
        var bBytes = WritePrepared(backend);
        backend.Commit();

        // A's stale transmit response finally arrives, claiming Number 1.
        backend.Accept(KittyGraphicsResponse.Parse("Gi=77,I=1;OK"u8));

        // Draining the response (and forcing B's placement to be rewritten by moving it) must not
        // retarget B, which never used Number 1.
        _ = backend.Prepare(frameB, frameBMoved, full: false);
        var moved = WritePrepared(backend);

        Encoding.ASCII.GetString(aBytes.Uploads).ShouldContain(",I=1;");
        Encoding.ASCII.GetString(bBytes.Uploads).ShouldContain(",I=2;");
        Encoding.ASCII.GetString(moved.Placements).ShouldContain("I=2");
        Encoding.ASCII.GetString(moved.Placements).ShouldNotContain("i=77");
    }

    /// <summary>Verifies upload, cursor-positioned placement, movement reuse, and last-use deletion.</summary>
    [Fact]
    public void Prepare_WhenImageMoves_ReusesIdsWithoutUploadingAndDeletesOnLastUse()
    {
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var first = Frame(image, new Rect(1, 0, 2, 1));
        using var moved = Frame(image, new Rect(2, 1, 1, 1));
        using var empty = new RenderFrame(new Size(4, 2));

        var initial = backend.Prepare(null, first, full: true);
        var initialBytes = WritePrepared(backend);
        backend.Commit();
        var movement = backend.Prepare(first, moved, full: false);
        var movementBytes = WritePrepared(backend);
        backend.Commit();
        var removal = backend.Prepare(moved, empty, full: false);
        var removalBytes = WritePrepared(backend);
        backend.Commit();

        initial.ShouldBe(new GraphicsBackendResult(changed: true, uploads: 1, placements: 1, removals: 0));
        Encoding.ASCII.GetString(initialBytes.Uploads).ShouldContain("a=t,f=32,t=d,s=1,v=1,I=1;AQID/w==");
        Encoding.ASCII.GetString(initialBytes.Placements).ShouldBe(
            "\u001b[1;2H\u001b_Ga=p,I=1,p=1,x=0,y=0,w=1,h=1,c=2,r=1,q=2,C=1\u001b\\" +
            "\u001b[1;1H");
        movement.ShouldBe(new GraphicsBackendResult(changed: true, uploads: 0, placements: 1, removals: 0));
        movementBytes.Uploads.ShouldBeEmpty();
        Encoding.ASCII.GetString(movementBytes.Placements).ShouldSatisfyAllConditions(
            value => value.ShouldContain("\u001b[2;3H\u001b_Ga=p,I=1,p=1"),
            value => value.ShouldEndWith("\u001b[1;1H"));
        removal.ShouldBe(new GraphicsBackendResult(
            changed: true,
            uploads: 0,
            placements: 0,
            removals: 1,
            fullCellRedraw: true));
        Encoding.ASCII.GetString(removalBytes.Removals).ShouldBe("\u001b_Ga=d,d=N,I=1,q=2\u001b\\");
    }

    /// <summary>Verifies a logical image replacement can reuse the retiring image's own identifier
    /// at full identifier capacity instead of renting a fresh one that the allocator has no room
    /// for — renting before planning the retiring delete threw even though the replaced image's
    /// identifier was about to free up.</summary>
    [Fact]
    public void Prepare_WhenReplacingTheOnlyImageAtFullCapacity_TransfersTheRetiringIdentifier()
    {
        using var backend = new KittyGraphicsBackend(maxImages: 1, maxPlacements: 1);
        var imageA = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        var imageB = GraphicsImage.FromRgba(new Size(1, 1), [4, 5, 6, 255]);
        using var frameA = Frame(imageA, new Rect(0, 0, 1, 1));
        using var frameB = Frame(imageB, new Rect(0, 0, 1, 1));
        _ = backend.Prepare(null, frameA, full: true);
        _ = WritePrepared(backend);
        backend.Commit();

        var replacement = backend.Prepare(frameA, frameB, full: false);
        var bytes = WritePrepared(backend);
        backend.Commit();

        replacement.ShouldBe(new GraphicsBackendResult(changed: true, uploads: 1, placements: 1, removals: 0));
        var upload = Encoding.ASCII.GetString(bytes.Uploads);
        upload.ShouldContain(",I=1;");
        upload.ShouldContain("BAUG/w==");
        bytes.Removals.ShouldBeEmpty();

        // The transferred identifier is available again after a second, unrelated replacement,
        // proving it was not permanently lost or leaked.
        var imageC = GraphicsImage.FromRgba(new Size(1, 1), [7, 8, 9, 255]);
        using var frameC = Frame(imageC, new Rect(0, 0, 1, 1));
        var second = backend.Prepare(frameB, frameC, full: false);
        second.ShouldBe(new GraphicsBackendResult(changed: true, uploads: 1, placements: 1, removals: 0));
    }

    /// <summary>Verifies transfer eligibility deliberately excludes an identifier that is already
    /// quarantined as uncertain by an earlier invalidated attempt: a retry that would need to
    /// reuse that same identifier still throws today, rather than risk the double-resolution of
    /// two independent pending dispositions for one physical identifier. By design, this is a
    /// documented scope limit of the fix landed here, not the desired end state.</summary>
    [Fact]
    public void Prepare_WhenRetryingAfterInvalidateAtFullCapacity_StillThrowsForAnAlreadyUncertainIdentifier()
    {
        using var backend = new KittyGraphicsBackend(maxImages: 1, maxPlacements: 1);
        var imageA = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var frameA = Frame(imageA, new Rect(0, 0, 1, 1));
        _ = backend.Prepare(null, frameA, full: true);
        _ = WritePrepared(backend);
        backend.Commit();

        var imageB = GraphicsImage.FromRgba(new Size(1, 1), [4, 5, 6, 255]);
        using var frameB = Frame(imageB, new Rect(0, 0, 1, 1));
        _ = backend.Prepare(frameA, frameB, full: false);
        backend.Invalidate();

        _ = Should.Throw<InvalidOperationException>(() => backend.Prepare(frameA, frameB, full: false));
    }

    /// <summary>Verifies a logical placement replacement can reuse the retiring placement's own
    /// identifier at full placement capacity even when the image allocator has spare room,
    /// isolating placement-identifier transfer from image-identifier transfer.</summary>
    [Fact]
    public void Prepare_WhenReplacingTheOnlyPlacementAtFullPlacementCapacity_TransfersTheRetiringIdentifier()
    {
        using var backend = new KittyGraphicsBackend(maxImages: 2, maxPlacements: 1);
        var imageA = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        var imageB = GraphicsImage.FromRgba(new Size(1, 1), [4, 5, 6, 255]);
        using var frameA = Frame(imageA, new Rect(0, 0, 1, 1));
        using var frameB = Frame(imageB, new Rect(0, 0, 1, 1));
        _ = backend.Prepare(null, frameA, full: true);
        _ = WritePrepared(backend);
        backend.Commit();

        var replacement = backend.Prepare(frameA, frameB, full: false);
        var bytes = WritePrepared(backend);
        backend.Commit();

        replacement.ShouldBe(new GraphicsBackendResult(changed: true, uploads: 1, placements: 1, removals: 0));
        Encoding.ASCII.GetString(bytes.Placements).ShouldContain(",p=1,");
    }

    /// <summary>Verifies a placement identifier transferred to a new (image, placement) pair still
    /// deletes the old pair when its image survives into the new frame, rather than leaving a
    /// ghost image rendered where the retiring placement used to sit.</summary>
    [Fact]
    public void Prepare_WhenTransferredPlacementIdRetiresFromASurvivingImage_DeletesTheOldPair()
    {
        using var backend = new KittyGraphicsBackend(maxImages: 2, maxPlacements: 2);
        var imageA = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        var imageB = GraphicsImage.FromRgba(new Size(1, 1), [4, 5, 6, 255]);
        using var first = new RenderFrame(new Size(4, 2));
        first.Canvas.DrawImage(imageA, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
        first.Canvas.DrawImage(imageA, new Rect(2, 0, 1, 1), PlacementMode.Stretch);
        using var second = new RenderFrame(new Size(4, 2));
        second.Canvas.DrawImage(imageA, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
        second.Canvas.DrawImage(imageB, new Rect(0, 1, 1, 1), PlacementMode.Stretch);
        _ = backend.Prepare(null, first, full: true);
        _ = WritePrepared(backend);
        backend.Commit();

        var result = backend.Prepare(first, second, full: false);
        var bytes = WritePrepared(backend);

        result.Removals.ShouldBe(1);
        Encoding.ASCII.GetString(bytes.Removals).ShouldContain("a=d,d=n,I=1,p=2");
    }

    /// <summary>Verifies removing one shared placement preserves image data through a soft delete.</summary>
    [Fact]
    public void Prepare_WhenSharedImagePlacementIsRemoved_DeletesOnlyExactPlacement()
    {
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [4, 5, 6, 255]);
        using var two = new RenderFrame(new Size(4, 1));
        two.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
        two.Canvas.DrawImage(image, new Rect(2, 0, 1, 1), PlacementMode.Stretch);
        using var one = Frame(image, new Rect(0, 0, 1, 1));
        _ = backend.Prepare(null, two, full: true);
        _ = WritePrepared(backend);
        backend.Commit();

        var result = backend.Prepare(two, one, full: false);
        var bytes = WritePrepared(backend);

        result.Removals.ShouldBe(1);
        Encoding.ASCII.GetString(bytes.Removals).ShouldBe(
            "\u001b_Ga=d,d=n,I=1,p=2,q=2\u001b\\");
    }

    /// <summary>Verifies invalidation reconstructs all committed remote images and placements.</summary>
    [Fact]
    public void Prepare_WhenBackendIsInvalidated_ReconstructsCompleteState()
    {
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 1, 1, 255]);
        using var frame = Frame(image, new Rect(0, 0, 1, 1));
        _ = backend.Prepare(null, frame, full: true);
        _ = WritePrepared(backend);
        backend.Commit();
        backend.Invalidate();

        var result = backend.Prepare(frame, frame, full: false);

        result.Uploads.ShouldBe(1);
        result.Placements.ShouldBe(1);
    }

    /// <summary>Verifies the hard output cap fails atomically and returns newly rented identifiers.</summary>
    [Fact]
    public void Prepare_WhenEncodedOutputExceedsBudget_RejectsBeforePreparedStateCanEscape()
    {
        using var backend = new KittyGraphicsBackend(maxPreparedBytes: 160);
        var large = GraphicsImage.FromRgba(new Size(100, 1), new byte[400]);
        var small = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var excessive = Frame(large, new Rect(0, 0, 1, 1));
        using var accepted = Frame(small, new Rect(0, 0, 1, 1));

        _ = Should.Throw<InvalidOperationException>(() => backend.Prepare(null, excessive, full: true));
        _ = Should.Throw<InvalidOperationException>(() => backend.WriteUploads(new ArrayBufferWriter<byte>()));
        var result = backend.Prepare(null, accepted, full: true);

        result.Uploads.ShouldBe(1);
        result.Placements.ShouldBe(1);
    }

    /// <summary>Verifies the success-boundary commit returns IDs and swaps state without allocation.</summary>
    [Fact]
    public void Commit_WhenRetiringState_AllocatesNoManagedMemory()
    {
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var frame = Frame(image, new Rect(0, 0, 1, 1));
        using var empty = new RenderFrame(new Size(4, 2));
        _ = backend.Prepare(null, frame, full: true);
        backend.Commit();
        _ = backend.Prepare(frame, empty, full: false);

        var before = GC.GetAllocatedBytesForCurrentThread();
        backend.Commit();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.ShouldBe(0);
        _ = Should.Throw<InvalidOperationException>(() =>
            backend.WriteRemovals(new ArrayBufferWriter<byte>()));
    }

    /// <summary>Verifies pane-local CUP remains inside tmux while only Kitty APC uses passthrough.</summary>
    [Fact]
    public void Prepare_WhenTmuxRouteIsAuthorized_RoutesOnlyPlacementApc()
    {
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics);
        using var backend = new KittyGraphicsBackend(route: new MultiplexerRoute(policy));
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var frame = Frame(image, new Rect(1, 0, 1, 1));

        _ = backend.Prepare(null, frame, full: true);
        var bytes = WritePrepared(backend);
        var placement = Encoding.ASCII.GetString(bytes.Placements);

        placement.ShouldStartWith("\u001b[1;2H\u001bPtmux;");
        placement.ShouldContain("\u001b\u001b_Ga=p,I=1,p=1");
        placement.ShouldEndWith("\u001b\u001b\\\u001b\\\u001b[1;1H");
    }

    /// <summary>Verifies screen topology disables Kitty backend selection atomically.</summary>
    [Fact]
    public void Constructor_WhenRouteContainsScreen_RejectsBackendSelection()
    {
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Screen],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics);

        _ = Should.Throw<NotSupportedException>(() => new KittyGraphicsBackend(route: new MultiplexerRoute(policy)));
    }

    /// <summary>Verifies a route too small to carry even one worst-case encoded chunk rejects
    /// backend selection at construction, rather than letting Prepare throw later when the
    /// undersized budget is discovered mid-transaction.</summary>
    [Fact]
    public void Constructor_WhenRouteIsTooSmallForOneChunk_RejectsBackendSelection()
    {
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics,
            maxEnvelopeBytes: 10);

        _ = Should.Throw<NotSupportedException>(() => new KittyGraphicsBackend(route: new MultiplexerRoute(policy)));
    }

    /// <summary>Verifies each direct-data chunk receives its own bounded tmux envelope.</summary>
    [Fact]
    public void Prepare_WhenUploadHasMultipleChunks_RoutesEachApcIndependently()
    {
        var policy = new MultiplexingPolicy(
            [MultiplexerKind.Tmux],
            TerminalProfile.CreateAnsi(TerminalCapabilities.Conservative),
            PassthroughMode.All,
            paneVisible: true,
            MultiplexingOperation.Graphics,
            maxEnvelopeBytes: 5_000);
        using var backend = new KittyGraphicsBackend(route: new MultiplexerRoute(policy));
        var image = GraphicsImage.FromRgba(new Size(1_024, 1), new byte[4_096]);
        using var frame = Frame(image, new Rect(0, 0, 1, 1));

        _ = backend.Prepare(null, frame, full: true);
        var bytes = WritePrepared(backend);

        Count(bytes.Uploads, "\u001bPtmux;"u8).ShouldBe(2);
    }

    /// <summary>Verifies an unchanged frame is byte-quiet and cleanup invalidation is recoverable.</summary>
    [Fact]
    public void Prepare_WhenStateIsUnchanged_IsNoOpAndCleanupInvalidationReconstructs()
    {
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var frame = Frame(image, new Rect(0, 0, 1, 1));
        _ = backend.Prepare(null, frame, full: true);
        _ = WritePrepared(backend);
        backend.Commit();

        var unchanged = backend.Prepare(frame, frame, full: false);
        var bytes = WritePrepared(backend);
        backend.Commit();
        _ = backend.PrepareCleanup();
        backend.Invalidate();

        unchanged.Changed.ShouldBeFalse();
        bytes.Uploads.ShouldBeEmpty();
        bytes.Placements.ShouldBeEmpty();
        bytes.Removals.ShouldBeEmpty();
        _ = Should.Throw<InvalidOperationException>(() =>
            backend.WriteCleanup(new ArrayBufferWriter<byte>()));
        backend.Prepare(frame, frame, full: false).ShouldSatisfyAllConditions(
            result => result.Uploads.ShouldBe(1),
            result => result.Placements.ShouldBe(1));
    }

    /// <summary>Verifies later cell paint retires Kitty state and earlier underlay paint reveals it again.</summary>
    [Fact]
    public void Prepare_WhenCellPaintOccludesThenRevealsPlacement_DeletesThenRecreatesRemoteState()
    {
        using var backend = new KittyGraphicsBackend();
        var image = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        using var visible = PaintedFrame(image, imageLast: true);
        using var occluded = PaintedFrame(image, imageLast: false);
        using var revealed = PaintedFrame(image, imageLast: true);
        _ = backend.Prepare(null, visible, full: true);
        _ = WritePrepared(backend);
        backend.Commit();

        var hidden = backend.Prepare(visible, occluded, full: false);
        var hiddenBytes = WritePrepared(backend);
        backend.Commit();
        var shown = backend.Prepare(occluded, revealed, full: false);

        hidden.Placements.ShouldBe(0);
        hidden.Removals.ShouldBe(1);
        Encoding.ASCII.GetString(hiddenBytes.Removals).ShouldContain("a=d,d=N,I=1");
        shown.Uploads.ShouldBe(1);
        shown.Placements.ShouldBe(1);
    }

    /// <summary>Verifies a later overlapping placement that is not effective (occluded by cell
    /// paint that arrived after it) suppresses an overlapping lower placement with an
    /// overlap-blocked diagnostic rather than falling back silently.</summary>
    [Fact]
    public void Prepare_WhenLaterOverlapIsIneffective_SuppressesLowerPlacementWithDiagnostic()
    {
        using var backend = new KittyGraphicsBackend();
        var lowerImage = GraphicsImage.FromRgba(new Size(1, 1), [1, 2, 3, 255]);
        var upperImage = GraphicsImage.FromRgba(new Size(1, 1), [4, 5, 6, 255]);
        using var frame = new RenderFrame(new Size(4, 2));
        frame.Canvas.DrawImage(lowerImage, new Rect(0, 0, 2, 1), PlacementMode.Stretch);
        frame.Canvas.DrawImage(upperImage, new Rect(1, 0, 2, 1), PlacementMode.Stretch);

        // Painted only over the cell that is exclusive to the upper placement's destination, so
        // only the upper placement becomes ineffective; the lower placement's own destination is
        // untouched by this paint and stays effective on its own.
        _ = frame.Canvas.Draw("x", new Point(2, 0));

        var result = backend.Prepare(null, frame, full: true);
        var bytes = WritePrepared(backend);

        result.Placements.ShouldBe(0);
        bytes.Placements.ShouldBeEmpty();
        result.SkippedPlacements.ShouldHaveSingleItem().Reason.ShouldBe(
            GraphicsPlacementSkipReason.OverlapBlocked);
    }

    private static RenderFrame Frame(GraphicsImage image, Rect destination)
    {
        var frame = new RenderFrame(new Size(4, 2));
        frame.Canvas.DrawImage(image, destination, PlacementMode.Stretch);
        return frame;
    }

    private static RenderFrame PaintedFrame(GraphicsImage image, bool imageLast)
    {
        var frame = new RenderFrame(new Size(1, 1));

        if (imageLast)
        {
            _ = frame.Canvas.Draw("x", default);
            frame.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
        }
        else
        {
            frame.Canvas.DrawImage(image, new Rect(0, 0, 1, 1), PlacementMode.Stretch);
            _ = frame.Canvas.Draw("x", default);
        }

        return frame;
    }

    private static PreparedBytes WritePrepared(KittyGraphicsBackend backend)
    {
        var uploads = new ArrayBufferWriter<byte>();
        var cellPreludes = new ArrayBufferWriter<byte>();
        var placements = new ArrayBufferWriter<byte>();
        var removals = new ArrayBufferWriter<byte>();
        backend.WriteUploads(uploads);
        backend.WriteCellPreludes(cellPreludes);
        backend.WritePlacements(placements);
        backend.WriteRemovals(removals);
        return new PreparedBytes(
            uploads.WrittenSpan.ToArray(),
            cellPreludes.WrittenSpan.ToArray(),
            placements.WrittenSpan.ToArray(),
            removals.WrittenSpan.ToArray());
    }

    private static int Count(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
    {
        var count = 0;

        while (source.IndexOf(value) is var index && index >= 0)
        {
            count++;
            source = source[(index + value.Length)..];
        }

        return count;
    }
}
