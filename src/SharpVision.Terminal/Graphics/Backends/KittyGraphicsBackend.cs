// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

using System.Collections.Concurrent;

using Buffers;

using Graphics;

using Kitty.Graphics;

using Rendering;


/// <summary>Implements finite transactional direct Kitty image upload and placement.</summary>
internal sealed class KittyGraphicsBackend: IGraphicsBackend
{
    private readonly KittyGraphicsIdentifierAllocator _imageIds;
    private readonly KittyGraphicsIdentifierAllocator _placementIds;
    private readonly int _maxPreparedBytes;
    private readonly BoundedBufferWriter _output;
    private readonly MultiplexerRoute? _route;
    private Dictionary<ulong, KittyGraphicsImageState> _images = [];
    private List<KittyGraphicsPlacementState> _placements = [];

    // Quarantined identifiers are double-buffered rather than kept in one set, because the two
    // sources that quarantine an identifier need different flush timing from the very next
    // ReturnUncertain call:
    //  - Invalidate() runs as its own standalone call, never immediately followed by
    //    ReturnUncertain in the same method. Its entries go straight into "prior" so the very
    //    next ReturnUncertain (whichever future Commit/CommitCleanup/Dispose calls it) reclaims
    //    them right away - exactly the original single-set behavior.
    //  - ReturnRetired/ReturnAllCommitted run immediately before ReturnUncertain, in the same
    //    Commit/CommitCleanup call. An identifier they just quarantined must NOT be reclaimed by
    //    that same call's ReturnUncertain (zero real protection), so they add to "current"
    //    instead. ReturnUncertain only ever flushes "prior," then rotates the two, promoting this
    //    transaction's additions to be the next cycle's flush candidates.
    // Either way, an identifier gets exactly one full untouched cycle of protection before it can
    // be handed to an unrelated image, matching Invalidate()'s original guarantee - without
    // needing to snapshot anything or allocate on every call.
    private Dictionary<uint, uint> _uncertainImages;
    private Dictionary<uint, uint> _priorUncertainImages;
    private readonly ConcurrentQueue<KittyGraphicsResponse> _responses = new();
    private List<GraphicsPlacementDiagnostic>? _pendingUploadFailureDiagnostics;
    // Counts outstanding stale replies owed for a wire number, not just whether one is owed: a
    // number transferred while its retiring image was unconfirmed can be transferred AGAIN, to a
    // second replacement, before the first retiring image's own stale reply ever arrives - each
    // such transfer owes one more stale reply that must be dropped before a reply for that number
    // can safely be trusted as the current tenant's own. A plain set can only ever forgive one.
    private readonly Dictionary<uint, int> _ambiguousTransferredNumbers = [];
    private List<KittyGraphicsUncertainPlacementState> _uncertainPlacements;
    private List<KittyGraphicsUncertainPlacementState> _priorUncertainPlacements;
    private Dictionary<ulong, KittyGraphicsImageState>? _preparedImages;
    private List<KittyGraphicsPlacementState>? _preparedPlacements;
    private List<uint>? _rentedImageIds;
    private List<uint>? _rentedPlacementIds;
    private List<KittyGraphicsUncertainPlacementState>? _rentedPlacementStates;
    private List<uint>? _transferredImageIds;
    private List<uint>? _retiredImageIds;
    private List<uint>? _retiredPlacementIds;
    private byte[] _uploads = [];
    private byte[] _cellPreludeBytes = [];
    private byte[] _placementBytes = [];
    private byte[] _removals = [];
    private byte[] _cleanup = [];
    private bool _invalidated = true;
    private bool _prepared;
    private bool _cleanupPrepared;
    private bool _disposed;

    #region Construction

    /// <summary>Initializes finite identifier and prepared-output ownership.</summary>
    /// <param name="maxImages">The positive simultaneous image limit.</param>
    /// <param name="maxPlacements">The positive simultaneous placement limit.</param>
    /// <param name="maxPreparedBytes">The positive maximum bytes retained per complete transaction.</param>
    /// <param name="route">An optional explicitly authorized tmux-only graphics route.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit is not positive.</exception>
    /// <exception cref="NotSupportedException"><paramref name="route"/> cannot carry graphics.</exception>
    public KittyGraphicsBackend(
        int maxImages = 4_096,
        int maxPlacements = 4_096,
        int maxPreparedBytes = GraphicsBackendSelector.DefaultMaxPreparedBytes,
        MultiplexerRoute? route = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxImages);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPlacements);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPreparedBytes);
        _imageIds = new KittyGraphicsIdentifierAllocator(maxImages);
        _placementIds = new KittyGraphicsIdentifierAllocator(maxPlacements);
        _uncertainImages = new Dictionary<uint, uint>(maxImages);
        _priorUncertainImages = new Dictionary<uint, uint>(maxImages);
        _uncertainPlacements = new List<KittyGraphicsUncertainPlacementState>(maxPlacements);
        _priorUncertainPlacements = new List<KittyGraphicsUncertainPlacementState>(maxPlacements);
        _maxPreparedBytes = maxPreparedBytes;
        _output = new BoundedBufferWriter(maxPreparedBytes, initialRentBytes: 256);
        _route = route;

        if (route is not null &&
            (!route.CanRouteGraphics ||
             route.GetMaximumGraphicsFrameBytes(escapeBytes: 2) < KittyGraphicsWriter.MaximumFrameBytes))
        {
            _output.Dispose();
            throw new NotSupportedException("The explicit multiplexer route cannot carry Kitty graphics.");
        }
    }

    #endregion

    #region Frame transactions

    /// <inheritdoc />
    public GraphicsCellOverlay? CommittedCellOverlay { get; private set; }

    /// <inheritdoc />
    public GraphicsCellOverlay? PreparedCellOverlay { get; private set; }

    /// <inheritdoc />
    public void Accept(KittyGraphicsResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        // A successful reply is only meaningful once it carries both the terminal-assigned image
        // id and the client's echoed number. A failed reply (the terminal explicitly rejected a
        // number-addressed upload, e.g. "i=5,I=1;ENOENT") carries no reliable assigned id, so only
        // its echoed number is required - it still needs to reach ApplyAssignedImageIds so the
        // rejected upload is not silently dropped with no diagnostic.
        if (response.Valid && response.ImageNumber != 0 &&
            (!response.Succeeded || response.ImageId != 0))
        {
            _responses.Enqueue(response);
        }
    }


    /// <inheritdoc />
    public GraphicsBackendResult Prepare(
        Frame? front,
        Frame back,
        bool full,
        GraphicsContext context = default)
    {
        ArgumentNullException.ThrowIfNull(back);
        ThrowIfDisposed();
        ApplyAssignedImageIds();

        if (_prepared || _cleanupPrepared)
        {
            throw new InvalidOperationException("A Kitty backend transaction is already prepared.");
        }

        var reconstruct = full || _invalidated;
        var images = new Dictionary<ulong, KittyGraphicsImageState>();
        var placements = new List<KittyGraphicsPlacementState>(back.PlacementCount);
        var rentedImages = new List<uint>();
        var rentedPlacements = new List<uint>();
        var rentedPlacementStates = new List<KittyGraphicsUncertainPlacementState>();
        var output = _output;
        output.Reset(_maxPreparedBytes);
        var uploadCount = 0;
        var placementCount = 0;
        var cellPreludeCount = 0;
        var removalCount = 0;
        var enabled = context.Profile is null || GraphicsBackendSelector.Authoritative(
            context.Profile.Capabilities.KittyGraphics,
            allowQuery: true);
        var placeholderColorDepth = context.Profile?.RenderingColorDepth ?? ColorDepth.TrueColor;
        var encodable = new bool[back.PlacementCount];
        List<GraphicsPlacementDiagnostic>? skippedPlacements = null;

        // Merges in diagnostics ApplyAssignedImageIds (called just above, and asynchronously on
        // every Accept-driven drain) queued for a terminal-rejected number-addressed upload. Those
        // are discovered whenever a reply arrives, not synchronously with the rest of this method's
        // own diagnostics, so they are stashed on the backend until the next Prepare call folds
        // them in here.
        if (_pendingUploadFailureDiagnostics is { Count: > 0 })
        {
            skippedPlacements = [.. _pendingUploadFailureDiagnostics];
            _pendingUploadFailureDiagnostics.Clear();
        }

        for (var index = 0; index < encodable.Length; index++)
        {
            encodable[index] = enabled && back.IsPlacementEffective(index);

            // Mirrors NonRetainedGraphicsBackend's diagnostic reporting: an otherwise-
            // effective placement dropped only because Kitty graphics is deauthorized on the
            // current profile is reported, rather than falling back silently with no observable
            // signal at all - Kitty accepts both RGBA and PNG, so protocol authorization is its
            // only eligibility gate.
            if (!enabled && back.IsPlacementEffective(index))
            {
                (skippedPlacements ??= []).Add(new GraphicsPlacementDiagnostic(
                    back.GetPlacement(index).ImageIdentity,
                    GraphicsPlacementSkipReason.ProtocolNotAuthorized));
            }
        }

        var blocked = back.FindFallbackBlockedPlacements(encodable);
        skippedPlacements = back.AppendOverlapBlockedDiagnostics(encodable, blocked, skippedPlacements);

        // Identifiers for images the new frame no longer needs. Renting a fresh identifier for a
        // logical replacement threw at full capacity even though the retiring image's own
        // identifier was about to free up — the retire delete was only planned later, after
        // allocation had already failed. Reusing a retiring identifier directly needs no delete at
        // all: transmitting new content under the same identifier is itself a protocol-level
        // replacement. Only images that are provably being dropped by this exact frame
        // are offered for transfer, so an unrelated in-flight image is never touched.
        var neededImageIdentities = new HashSet<ulong>();

        for (var index = 0; index < back.PlacementCount; index++)
        {
            if (encodable[index] && !blocked[index])
            {
                _ = neededImageIdentities.Add(back.GetPlacement(index).Image!.Identity);
            }
        }

        var retiringImageIds = new Queue<(uint Number, bool WasUnconfirmed)>();

        foreach (var previous in _images)
        {
            // An identifier already quarantined as uncertain (a prior transfer that was itself
            // invalidated before it could commit) is deliberately excluded here rather than
            // transferred again. Its disposition already depends on a tombstone this transaction
            // has not yet proven it will actually send, so layering a second transfer on top
            // would need to reconcile two independent pending resolutions for the same physical
            // identifier — safe in principle, but not yet proven safe across every
            // invalidate/retry interleaving. Falling back to a fresh rental here (or exhaustion,
            // if the allocator has no other room) is the conservative choice.
            if (!neededImageIdentities.Contains(previous.Key) && !IsUncertainImage(previous.Value.Number))
            {
                retiringImageIds.Enqueue((previous.Value.Number, previous.Value.UsesImageNumber));
            }
        }

        var transferredImageIds = new List<uint>();

        // Numbers newly flagged as owing a stale reply by this call, applied to the real
        // _ambiguousTransferredNumbers debt ledger only once the method reaches its success-commit
        // point below. Recording the mutation locally first - mirroring the existing
        // transferredImageIds/rentedImages/etc. idiom in this same method - keeps a failed Prepare
        // call (e.g. WriteUpload throwing over budget, after this transfer already ran) from
        // leaving behind debt for a transfer whose upload never actually transmitted. The catch
        // block below needs no change: on any exception this local list is simply discarded along
        // with everything else, leaving _ambiguousTransferredNumbers exactly as it was before this
        // call, including any unrelated pre-existing debt for the same number from an earlier,
        // already-committed call.
        var newlyAmbiguousNumbers = new List<uint>();

        // Same reasoning as retiringImageIds, for placement identifiers. This mirrors the
        // position/identity matching the main loop below performs to decide whether a placement
        // is retained in place (the effectiveIndex comparison against _placements later in this
        // method) — it must stay in exact sync with that check, since it exists only to learn,
        // before the loop runs, which old placement identifiers the loop will NOT retain and can
        // therefore offer for transfer.
        var retainedPlacementIds = new HashSet<uint>();
        var neededEffectiveIndex = 0;

        for (var index = 0; index < back.PlacementCount; index++)
        {
            if (!encodable[index] || blocked[index])
            {
                continue;
            }

            if (neededEffectiveIndex < _placements.Count &&
                _placements[neededEffectiveIndex].Placement.ImageIdentity ==
                back.GetPlacement(index).ImageIdentity)
            {
                _ = retainedPlacementIds.Add(_placements[neededEffectiveIndex].PlacementId);
            }

            neededEffectiveIndex++;
        }

        var retiringPlacementIds = new Queue<uint>();

        foreach (var previous in _placements)
        {
            // See the identical caution for images above: an already-uncertain placement
            // identifier is deliberately excluded from transfer.
            if (!retainedPlacementIds.Contains(previous.PlacementId) &&
                !IsUncertainPlacement(previous.PlacementId))
            {
                retiringPlacementIds.Enqueue(previous.PlacementId);
            }
        }

        var transferredPlacementIds = new List<uint>();

        try
        {
            for (var index = 0; index < back.PlacementCount; index++)
            {
                if (!encodable[index] || blocked[index])
                {
                    continue;
                }

                var placement = back.GetPlacement(index);
                Debug.Assert(!placement.IsEmpty, "Active frame placements cannot be empty.");
                var image = placement.Image!;

                if (!images.TryGetValue(image.Identity, out var imageState))
                {
                    if (!_images.TryGetValue(image.Identity, out imageState))
                    {
                        uint id;

                        if (retiringImageIds.TryDequeue(out var transferred))
                        {
                            id = transferred.Number;
                            transferredImageIds.Add(id);

                            // The retiring image this number came from is still unconfirmed (its
                            // own transmit reply, success or failure, has not yet arrived): the
                            // terminal may yet deliver that stale reply correlated by this exact
                            // number, indistinguishable from one meant for the image now taking it
                            // over. Recording the number as ambiguous makes a later reply drop
                            // instead of misattribute once ApplyAssignedImageIds sees it.
                            if (transferred.WasUnconfirmed)
                            {
                                newlyAmbiguousNumbers.Add(id);
                            }
                        }
                        else
                        {
                            id = _imageIds.Rent();
                            rentedImages.Add(id);
                        }

                        imageState = new KittyGraphicsImageState(image, id);
                    }

                    images.Add(image.Identity, imageState);

                    if (reconstruct || !_images.ContainsKey(image.Identity))
                    {
                        WriteUpload(imageState, output);
                        uploadCount++;
                    }
                }

                KittyGraphicsPlacementState placementState;

                var effectiveIndex = placements.Count;

                if (effectiveIndex < _placements.Count &&
                    _placements[effectiveIndex].Placement.ImageIdentity == placement.ImageIdentity)
                {
                    var previous = _placements[effectiveIndex];
                    placementState = new KittyGraphicsPlacementState(
                        placement,
                        imageState.Reference,
                        previous.PlacementId,
                        imageState.UsesImageNumber);
                }
                else
                {
                    uint id;

                    if (retiringPlacementIds.TryDequeue(out var transferredPlacementId))
                    {
                        id = transferredPlacementId;
                        transferredPlacementIds.Add(id);
                    }
                    else
                    {
                        id = _placementIds.Rent();
                        rentedPlacements.Add(id);
                    }

                    placementState = new KittyGraphicsPlacementState(
                        placement,
                        imageState.Reference,
                        id,
                        imageState.UsesImageNumber);
                    rentedPlacementStates.Add(new KittyGraphicsUncertainPlacementState(
                        imageState.Reference,
                        id,
                        imageState.UsesImageNumber));
                }

                // Stamped now, while placeholderColorDepth and every other input CanUsePlaceholder
                // depends on are in scope, so it can be compared against the equivalent prior-frame
                // flag captured on _placements[index] once this state is committed — see the
                // placeholder-eligibility checks below and in PlaceholderEligibilityChanged.
                placementState = placementState.WithUsedPlaceholder(
                    CanUsePlaceholder(back, placementState, placeholderColorDepth));
                placements.Add(placementState);
            }

            var remaining = _maxPreparedBytes;
            _uploads = FinishApcPhase(output, ref remaining);

            var preparedCellOverlay = BuildCellOverlay(back, placements, placeholderColorDepth);
            var virtualPlacementsChanged = !CellOverlaysEqual(
                CommittedCellOverlay,
                preparedCellOverlay);

            if (!virtualPlacementsChanged)
            {
                virtualPlacementsChanged = PlacementsChanged(
                    back,
                    placements,
                    placeholderColorDepth,
                    placeholders: true);
            }

            // Defensive symmetry with the real-placement loop's own eligibility check below: even
            // though a flip into or out of placeholder eligibility should already change the cell
            // overlay (BuildCellOverlay only paints currently-eligible placements), that isn't
            // relied upon here as the sole guarantee - an explicit per-placement disagreement
            // against the prior committed frame also forces the placeholder batch to be rewritten.
            if (!virtualPlacementsChanged)
            {
                virtualPlacementsChanged = PlaceholderEligibilityChanged(placements);
            }

            if (virtualPlacementsChanged)
            {
                for (var index = 0; index < placements.Count; index++)
                {
                    var placementState = placements[index];

                    if (CanUsePlaceholder(back, placementState, placeholderColorDepth))
                    {
                        WriteVirtualPlacement(placementState, index, output);
                        cellPreludeCount++;
                    }
                }
            }

            _cellPreludeBytes = FinishApcPhase(output, ref remaining);

            for (var index = 0; index < placements.Count; index++)
            {
                var placementState = placements[index];
                var placement = placementState.Placement;

                if (!placementState.UsedPlaceholder &&
                    (reconstruct ||
                    index >= _placements.Count ||
                    _placements[index].PlacementId != placementState.PlacementId ||
                    _placements[index].Placement != placement ||
                    // The placement held identity and geometry across frames but was rendered
                    // through a virtual placeholder last time and just lost that eligibility (e.g.
                    // a color-depth change) - the placeholder loop above no longer emits anything
                    // for it, so the real placement command must be forced here or the image
                    // silently vanishes with zero bytes and no diagnostic.
                    _placements[index].UsedPlaceholder))
                {
                    WritePlacement(placementState, index, output);
                    placementCount++;
                }
            }

            if (placementCount != 0)
            {
                // Kitty implementations are VT terminals. Restore the semantic absolute cursor
                // after placement CUP commands without consuming the terminal save/restore slot.
                WriteCursor(back.Cursor.Position, output);
            }

            _placementBytes = output.WrittenSpan.ToArray();
            remaining -= _placementBytes.Length;
            output.Reset(remaining);
            var retiredPlacements = new List<uint>();
            var transferredImageIdSet = new HashSet<uint>(transferredImageIds);
            var hardDeletedImageIds = UncertainImageReferences();

            foreach (var previous in _images)
            {
                if (!images.ContainsKey(previous.Key) && !transferredImageIdSet.Contains(previous.Value.Number))
                {
                    _ = hardDeletedImageIds.Add(new KittyGraphicsImageReference(
                        previous.Value.Reference,
                        previous.Value.UsesImageNumber));
                }
            }

            removalCount += WriteUncertainDeletes(output, hardDeletedImageIds);

            var transferredPlacementIdSet = new HashSet<uint>(transferredPlacementIds);

            for (var index = 0; index < _placements.Count; index++)
            {
                var previous = _placements[index];
                var retained = placements.Any(candidate =>
                    candidate.PlacementId == previous.PlacementId &&
                    candidate.ImageId == previous.ImageId);

                if (retained)
                {
                    continue;
                }

                // Transferring the placement identifier only means the number is reused by a new
                // (image, placement) pair above (see the retiringPlacementIds.TryDequeue transfer
                // branch earlier in this method); the old pair itself is not that new pair unless
                // retained already matched, so its image still needs the explicit delete or it
                // stays rendered as a ghost.
                if (images.ContainsKey(previous.Placement.ImageIdentity))
                {
                    KittyGraphicsWriter.Write(
                        UseImageReference(
                            KittyGraphicsCommand.DeletePlacement(previous.ImageId, previous.PlacementId),
                            previous.UsesImageNumber),
                        [],
                        output);
                    removalCount++;
                }

                // A transferred identifier is still actively owned by the new pair, so it must not
                // be returned to the free pool alongside placements that are genuinely retiring.
                if (!transferredPlacementIdSet.Contains(previous.PlacementId))
                {
                    retiredPlacements.Add(previous.PlacementId);
                }
            }

            var retiredImages = new List<uint>();

            foreach (var previous in _images)
            {
                if (!images.ContainsKey(previous.Key))
                {
                    if (transferredImageIdSet.Contains(previous.Value.Number))
                    {
                        // Reused directly by a new image above instead of retired: the upload
                        // already transmitted under this identifier, which is itself a protocol
                        // replacement. Deleting it first would be at best redundant and at worst a
                        // race against the transmit that just claimed it.
                        continue;
                    }

                    KittyGraphicsWriter.Write(
                        UseImageReference(
                            KittyGraphicsCommand.DeleteImage(previous.Value.Reference),
                            previous.Value.UsesImageNumber),
                        [],
                        output);
                    retiredImages.Add(previous.Value.Number);
                    removalCount++;
                }
            }

            _removals = FinishApcPhase(output, ref remaining);

            // Only now, with the method guaranteed to succeed, does deferred ambiguous-transfer debt
            // recorded above become real - see the newlyAmbiguousNumbers declaration for why this
            // must not happen any earlier.
            foreach (var newlyAmbiguousId in newlyAmbiguousNumbers)
            {
                _ambiguousTransferredNumbers[newlyAmbiguousId] =
                    _ambiguousTransferredNumbers.GetValueOrDefault(newlyAmbiguousId) + 1;
            }

            _preparedImages = images;
            _preparedPlacements = placements;
            PreparedCellOverlay = preparedCellOverlay;
            _rentedImageIds = rentedImages;
            _rentedPlacementIds = rentedPlacements;
            _rentedPlacementStates = rentedPlacementStates;
            _transferredImageIds = transferredImageIds;
            _retiredImageIds = retiredImages;
            _retiredPlacementIds = retiredPlacements;
            _prepared = true;

            // A retained image is independent of ordinary cell damage. When the last placement
            // leaves the frame, request the same clear-and-reconstruct boundary used by
            // non-retained graphics. The Kitty protocol explicitly requires the standard clear
            // screen operation to clear visible images, while the exact hard deletes below still
            // release their stored image data.
            var requiresFinalPlacementClear = _placements.Count != 0 && placements.Count == 0;
            return new GraphicsBackendResult(
                uploadCount + cellPreludeCount + placementCount + removalCount != 0 ||
                    !CellOverlaysEqual(CommittedCellOverlay, preparedCellOverlay),
                uploadCount,
                placementCount,
                removalCount,
                fullCellRedraw: requiresFinalPlacementClear,
                skippedPlacements,
                cellPreludes: cellPreludeCount);
        }
        catch
        {
            ReturnRented(rentedImages, rentedPlacements);
            _uploads = [];
            _cellPreludeBytes = [];
            _placementBytes = [];
            _removals = [];
            output.Reset(_maxPreparedBytes);
            throw;
        }
    }

    /// <inheritdoc />
    public void WriteUploads(IBufferWriter<byte> destination)
    {
        EnsurePrepared();
        WritePrepared(_uploads, destination);
    }

    /// <inheritdoc />
    public void WriteCellPreludes(IBufferWriter<byte> destination)
    {
        EnsurePrepared();
        WritePrepared(_cellPreludeBytes, destination);
    }

    /// <inheritdoc />
    public void WritePlacements(IBufferWriter<byte> destination)
    {
        EnsurePrepared();
        WritePrepared(_placementBytes, destination);
    }

    /// <inheritdoc />
    public void WriteRemovals(IBufferWriter<byte> destination)
    {
        EnsurePrepared();
        WritePrepared(_removals, destination);
    }

    /// <inheritdoc />
    public void Commit()
    {
        Debug.Assert(_prepared, "Only prepared Kitty state can commit.");
        ReturnRetired();
        ReturnUncertain();
        _images = _preparedImages!;
        _placements = _preparedPlacements!;
        CommittedCellOverlay = PreparedCellOverlay;
        ClearPrepared(returnRented: false);
        _invalidated = false;
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        if (_prepared)
        {
            // Transferred identifiers are excluded from this bound: they were already active
            // (owned by the _images entry they are replacing) and are only being relabeled from
            // "owned by _images" to "owned by _uncertainImageIds," not newly consuming allocator
            // capacity the way a fresh rental does.
            Debug.Assert(
                _uncertainImages.Count + _priorUncertainImages.Count + _rentedImageIds!.Count <=
                    _imageIds.Capacity,
                "The bounded image allocator must fit every uncertain rental.");
            Debug.Assert(
                _uncertainPlacements.Count + _priorUncertainPlacements.Count + _rentedPlacementStates!.Count <=
                    _uncertainPlacements.Capacity,
                "The bounded placement allocator must fit every uncertain rental.");
            // These go straight into the "prior" bucket - see the field comment above - so the
            // very next ReturnUncertain call reclaims them, exactly as before this identifier was
            // split into two buckets.
            foreach (var number in _rentedImageIds!)
            {
                _ = _priorUncertainImages.TryAdd(number, 0);
            }

            // A transferred identifier's upload is exactly as unconfirmed as a freshly rented
            // one's: it must be quarantined the same way, so a future transaction cannot reuse it
            // again until a delete tombstone for it has actually flushed.
            foreach (var number in _transferredImageIds!)
            {
                _ = _priorUncertainImages.TryAdd(number, 0);
            }
            _priorUncertainPlacements.AddRange(_rentedPlacementStates!);
            ClearPrepared(returnRented: false);
        }

        if (_cleanupPrepared)
        {
            _cleanup = [];
            _cleanupPrepared = false;
        }

        _invalidated = true;
    }

    #endregion

    #region Cleanup and lifetime

    /// <inheritdoc />
    public int PrepareCleanup()
    {
        ThrowIfDisposed();
        ApplyAssignedImageIds();

        if (_prepared || _cleanupPrepared)
        {
            throw new InvalidOperationException("A Kitty backend transaction is already prepared.");
        }

        var output = _output;
        output.Reset(_maxPreparedBytes);

        foreach (var image in _images.Values)
        {
            KittyGraphicsWriter.Write(
                UseImageReference(KittyGraphicsCommand.DeleteImage(image.Reference), image.UsesImageNumber),
                [],
                output);
        }

        var hardDeletedImageIds = UncertainImageReferences();

        foreach (var image in _images.Values)
        {
            _ = hardDeletedImageIds.Add(new KittyGraphicsImageReference(image.Reference, image.UsesImageNumber));
        }

        var cleanupCount = _images.Count + WriteUncertainDeletes(output, hardDeletedImageIds);

        var remaining = _maxPreparedBytes;
        _cleanup = FinishApcPhase(output, ref remaining);
        _cleanupPrepared = true;
        return cleanupCount;
    }

    /// <inheritdoc />
    public void WriteCleanup(IBufferWriter<byte> destination)
    {
        if (!_cleanupPrepared)
        {
            throw new InvalidOperationException("Remote cleanup has not been prepared.");
        }

        WritePrepared(_cleanup, destination);
    }

    /// <inheritdoc />
    public void CommitCleanup()
    {
        Debug.Assert(_cleanupPrepared, "Only prepared Kitty cleanup can commit.");
        ReturnAllCommitted();
        ReturnUncertain();
        _images.Clear();
        _placements.Clear();
        CommittedCellOverlay = null;
        _cleanup = [];
        _cleanupPrepared = false;
        _invalidated = false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Invalidate();

        ReturnAllCommitted();
        ReturnUncertain();
        _images.Clear();
        _placements.Clear();
        CommittedCellOverlay = null;
        _output.Dispose();
        _disposed = true;
    }

    #endregion

    #region Encoding

    private static GraphicsCellOverlay? BuildCellOverlay(
        Frame back,
        List<KittyGraphicsPlacementState> placements,
        ColorDepth colorDepth)
    {
        GraphicsCellOverlay? overlay = null;

        foreach (var state in placements)
        {
            if (!CanUsePlaceholder(back, state, colorDepth))
            {
                continue;
            }

            overlay ??= new GraphicsCellOverlay(back);
            overlay.Paint(
                back,
                state.Placement.Destination,
                state.ImageId,
                state.PlacementId,
                colorDepth);
        }

        return overlay;
    }

    private static bool CanUsePlaceholder(
        Frame back,
        KittyGraphicsPlacementState state,
        ColorDepth colorDepth) =>
        !state.UsesImageNumber &&
        state.Placement.Mode == PlacementMode.Contain &&
        (colorDepth switch
        {
            ColorDepth.TrueColor => state.PlacementId <= 0x00FF_FFFF,
            ColorDepth.Indexed256 => state.ImageId <= byte.MaxValue && state.PlacementId <= byte.MaxValue,
            ColorDepth.Basic16 or ColorDepth.Monochrome => false,
            _ => throw new ArgumentOutOfRangeException(
                nameof(colorDepth), colorDepth, "The color depth is unknown.")
        }) &&
        state.Placement.Destination.Width <= KittyGraphicsPlaceholderWriter.CoordinateLimit &&
        state.Placement.Destination.Height <= KittyGraphicsPlaceholderWriter.CoordinateLimit &&
        !SplitsWideGrapheme(back, state.Placement.Destination);

    /// <summary>
    /// Reports whether any column of a placeholder destination would fall in the middle of a
    /// wide (two-column) grapheme: any interior or edge column landing on a continuation cell -
    /// which the encoder silently skips regardless of an active overlay, desynchronizing the
    /// row's emitted column count from every column after it - or the right edge landing on a
    /// wide lead cell whose trailing continuation cell sits outside the rect and would be left
    /// orphaned with no lead. A Kitty placeholder is exactly one protocol column wide regardless
    /// of the frame content it replaces, so either case corrupts the row - see
    /// <see cref="GraphicsCellOverlay.Paint"/>, which blind-fills every destination cell with no
    /// continuation-boundary awareness of its own.
    /// </summary>
    private static bool SplitsWideGrapheme(Frame back, Rect destination)
    {
        if (destination.Width <= 0 || destination.Height <= 0)
        {
            return false;
        }

        var leftColumn = destination.X;
        var rightColumn = destination.Right - 1;

        for (var row = destination.Y; row < destination.Bottom; row++)
        {
            var rowStart = checked(row * back.Size.Width);

            for (var column = leftColumn; column <= rightColumn; column++)
            {
                var cell = back.GetCellByIndex(checked(rowStart + column));

                if (cell.IsContinuation)
                {
                    return true;
                }

                if (column == rightColumn && cell.Width == 2)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool PlacementsChanged(
        Frame back,
        List<KittyGraphicsPlacementState> placements,
        ColorDepth colorDepth,
        bool placeholders)
    {
        if (placements.Count != _placements.Count)
        {
            return true;
        }

        for (var index = 0; index < placements.Count; index++)
        {
            if (CanUsePlaceholder(back, placements[index], colorDepth) == placeholders &&
                placements[index].Placement != _placements[index].Placement)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reports whether any placement's placeholder eligibility this frame (already stamped onto
    /// <see cref="KittyGraphicsPlacementState.UsedPlaceholder"/> for every entry in
    /// <paramref name="placements"/> while it was being built) disagrees with the eligibility
    /// captured on the equivalent committed <see cref="_placements"/> entry from the prior frame.
    /// This is the one caller-independent signal that a placement's placeholder/real-placement
    /// rendering channel needs to change, regardless of <c>reconstruct</c>, identity, or geometry.
    /// </summary>
    private bool PlaceholderEligibilityChanged(List<KittyGraphicsPlacementState> placements)
    {
        if (placements.Count != _placements.Count)
        {
            return true;
        }

        for (var index = 0; index < placements.Count; index++)
        {
            if (placements[index].UsedPlaceholder != _placements[index].UsedPlaceholder)
            {
                return true;
            }
        }

        return false;
    }

    private static bool CellOverlaysEqual(
        GraphicsCellOverlay? left,
        GraphicsCellOverlay? right) =>
        left is null ? right is null : left.SemanticallyEquals(right);

    private static void WriteVirtualPlacement(
        KittyGraphicsPlacementState state,
        int zIndex,
        IBufferWriter<byte> destination)
    {
        Debug.Assert(!state.UsesImageNumber, "Placeholder cells require a terminal-assigned image id.");
        var command = KittyGraphicsCommand.Place(
            state.ImageId,
            state.PlacementId,
            state.Placement.Source,
            new Size(state.Placement.Destination.Width, state.Placement.Destination.Height),
            zIndex,
            unicodePlaceholder: true);

        // Written raw, unlike the sibling real-placement phase's WritePlacementCommand call: the
        // cell-prelude phase contains only APC frames (no interleaved raw CSI cursor moves) and is
        // finalized by the caller's own FinishApcPhase call, which performs the single tmux-route
        // wrap. Routing through WritePlacementCommand here would wrap this command through _route
        // a first time and then FinishApcPhase would wrap the already-wrapped buffer a second time,
        // leaving the outer terminal with a nested envelope instead of the Kitty APC.
        KittyGraphicsWriter.Write(command, [], destination);
    }

    private void WritePlacement(KittyGraphicsPlacementState state, int zIndex, IBufferWriter<byte> destination)
    {
        WriteCursor(
            new Point(state.Placement.Destination.X, state.Placement.Destination.Y),
            destination);
        var command = UseImageReference(KittyGraphicsCommand.Place(
            state.ImageId,
            state.PlacementId,
            state.Placement.Source,
            new Size(state.Placement.Destination.Width, state.Placement.Destination.Height),
            zIndex), state.UsesImageNumber);

        WritePlacementCommand(command, destination);
    }

    private void WritePlacementCommand(
        KittyGraphicsCommand command,
        IBufferWriter<byte> destination)
    {
        if (_route is null)
        {
            KittyGraphicsWriter.Write(command, [], destination);
            return;
        }

        var apc = new ArrayBufferWriter<byte>(256);
        KittyGraphicsWriter.Write(command, [], apc);

        if (!_route.TryWriteGraphics(destination, apc.WrittenSpan))
        {
            throw new InvalidOperationException("The Kitty placement exceeded its authorized route.");
        }
    }

    private static void WriteCursor(Point value, IBufferWriter<byte> destination) =>
        Csi.Position(new ProtocolWriter(destination), value.Y + 1, value.X + 1);

    private static void WriteUpload(KittyGraphicsImageState state, IBufferWriter<byte> destination)
    {
        var format = state.Image.Format == ImageFormat.Rgba ? KittyGraphicsFormat.Rgba : KittyGraphicsFormat.Png;
        KittyGraphicsWriter.WriteTransmission(
            UseImageReference(
                KittyGraphicsCommand.Transmit(
                    state.Reference,
                    state.Image.Size,
                    format,
                    quiet: state.UsesImageNumber ? 0 : 2),
                state.UsesImageNumber),
            state.Image.Source,
            destination,
            maxPayloadBytes: state.Image.ByteCount);
    }

    private static void WritePrepared(ReadOnlySpan<byte> bytes, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(bytes);
    }

    private static KittyGraphicsCommand UseImageReference(
        KittyGraphicsCommand command,
        bool usesImageNumber) =>
        usesImageNumber ? command.WithImageNumber() : command;

    private byte[] FinishApcPhase(BoundedBufferWriter output, ref int remaining)
    {
        var raw = output.WrittenSpan.ToArray();
        output.Reset(remaining);

        if (_route is null || raw.Length == 0)
        {
            remaining -= raw.Length;
            output.Reset(remaining);
            return raw;
        }

        RouteApcFrames(raw, output);

        var routed = output.WrittenSpan.ToArray();
        remaining -= routed.Length;
        output.Reset(remaining);
        return routed;
    }

    private void RouteApcFrames(ReadOnlySpan<byte> commands, IBufferWriter<byte> destination)
    {
        Debug.Assert(_route is not null, "Only explicit routes encode APC frames.");

        while (!commands.IsEmpty)
        {
            var terminator = commands.IndexOf("\u001b\\"u8);

            if (terminator < 0)
            {
                throw new InvalidOperationException("Prepared Kitty output contains an incomplete APC frame.");
            }

            var length = terminator + 2;

            if (!_route.TryWriteGraphics(destination, commands[..length]))
            {
                throw new InvalidOperationException("A Kitty APC frame exceeded its authorized route.");
            }

            commands = commands[length..];
        }
    }

    #endregion

    #region Identifier and prepared state

    private void ApplyAssignedImageIds()
    {
        while (_responses.TryDequeue(out var response))
        {
            // A number that was transferred directly to its current tenant while the retiring image
            // it came from was still unconfirmed cannot be told apart from that stale retiring image's
            // own late reply: attributing this response to the current tenant here risks reporting a
            // healthy, unrelated image as terminal-rejected, or - just as bad - stamping its assigned
            // id (or a placement's) with an id that was never really meant for it. Dropping the reply
            // silently for this one number, regardless of whether it succeeded or failed, is the same
            // conservative outcome every reply had before this diagnostic existed. The number can have
            // been transferred more than once while still unconfirmed (a chain of replacements before
            // any of their replies arrive), so one stale reply is consumed per outstanding transfer
            // rather than forgiving the whole number after the first.
            if (TryConsumeAmbiguousTransfer(response.ImageNumber))
            {
                continue;
            }

            if (!response.Succeeded)
            {
                RecordUploadFailureDiagnostics(response);
                continue;
            }

            foreach (var identity in _images
                .Where(pair => pair.Value.UsesImageNumber && pair.Value.Number == response.ImageNumber)
                .Select(static pair => pair.Key)
                .ToArray())
            {
                _images[identity] = _images[identity].WithAssignedId(response.ImageId);
            }

            for (var index = 0; index < _placements.Count; index++)
            {
                var placement = _placements[index];

                if (placement.UsesImageNumber && placement.ImageId == response.ImageNumber)
                {
                    _placements[index] = placement.WithAssignedImageId(response.ImageId);
                }
            }

            ApplyAssignedUncertainImageId(_uncertainImages, response);
            ApplyAssignedUncertainImageId(_priorUncertainImages, response);
            ApplyAssignedUncertainPlacementId(_uncertainPlacements, response);
            ApplyAssignedUncertainPlacementId(_priorUncertainPlacements, response);
        }
    }

    /// <summary>
    /// Consumes one outstanding stale reply owed for <paramref name="number"/>, if any are owed.
    /// Returns <see langword="true"/> (and decrements or clears the owed count) when the reply
    /// should be dropped as ambiguous; returns <see langword="false"/> when no transfer is
    /// outstanding for this number and the reply can be trusted as the current tenant's own.
    /// </summary>
    private bool TryConsumeAmbiguousTransfer(uint number)
    {
        if (!_ambiguousTransferredNumbers.TryGetValue(number, out var owed))
        {
            return false;
        }

        if (owed <= 1)
        {
            _ = _ambiguousTransferredNumbers.Remove(number);
        }
        else
        {
            _ambiguousTransferredNumbers[number] = owed - 1;
        }

        return true;
    }

    /// <summary>
    /// Records a skip diagnostic for every currently tracked image whose number-addressed upload
    /// the terminal explicitly rejected (a valid but unsuccessful reply, e.g. <c>i=5,I=1;ENOENT</c>).
    /// Without this, a rejected upload's placement would never be retried and would silently never
    /// render, with no observable signal. The diagnostic is queued here and merged into the next
    /// <see cref="Prepare"/> call's <c>skippedPlacements</c>, since Prepare's own diagnostics are all
    /// computed synchronously at Prepare-time while this is discovered asynchronously, whenever the
    /// terminal's reply arrives. The terminal's raw <see cref="KittyGraphicsResponse.Message"/> is
    /// never carried into the diagnostic - see that property's own redaction note.
    /// </summary>
    private void RecordUploadFailureDiagnostics(KittyGraphicsResponse response)
    {
        foreach (var identity in _images
            .Where(pair => pair.Value.UsesImageNumber && pair.Value.Number == response.ImageNumber)
            .Select(static pair => pair.Key)
            .ToArray())
        {
            (_pendingUploadFailureDiagnostics ??= []).Add(new GraphicsPlacementDiagnostic(
                identity,
                GraphicsPlacementSkipReason.TerminalRejectedUpload));
        }
    }

    private static void ApplyAssignedUncertainImageId(Dictionary<uint, uint> images, KittyGraphicsResponse response)
    {
        if (images.ContainsKey(response.ImageNumber))
        {
            images[response.ImageNumber] = response.ImageId;
        }
    }

    private static void ApplyAssignedUncertainPlacementId(
        List<KittyGraphicsUncertainPlacementState> placements,
        KittyGraphicsResponse response)
    {
        for (var index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];

            if (placement.UsesImageNumber && placement.ImageId == response.ImageNumber)
            {
                placements[index] = placement.WithAssignedImageId(response.ImageId);
            }
        }
    }

    private HashSet<KittyGraphicsImageReference> UncertainImageReferences()
    {
        var references = new HashSet<KittyGraphicsImageReference>();
        AddUncertainImageReferences(_uncertainImages, references);
        AddUncertainImageReferences(_priorUncertainImages, references);
        return references;
    }

    private static void AddUncertainImageReferences(
        Dictionary<uint, uint> images,
        HashSet<KittyGraphicsImageReference> references)
    {
        foreach (var image in images)
        {
            _ = references.Add(new KittyGraphicsImageReference(
                image.Value == 0 ? image.Key : image.Value,
                image.Value == 0));
        }
    }

    private int WriteUncertainDeletes(
        IBufferWriter<byte> destination,
        HashSet<KittyGraphicsImageReference> hardDeletedImageIds)
    {
        var count = WriteUncertainImageDeletes(_uncertainImages, destination);
        count += WriteUncertainImageDeletes(_priorUncertainImages, destination);
        count += WriteUncertainPlacementDeletes(_uncertainPlacements, destination, hardDeletedImageIds);
        count += WriteUncertainPlacementDeletes(_priorUncertainPlacements, destination, hardDeletedImageIds);
        return count;
    }

    private static int WriteUncertainImageDeletes(Dictionary<uint, uint> images, IBufferWriter<byte> destination)
    {
        var count = 0;

        foreach (var image in images)
        {
            var imageId = image.Value == 0 ? image.Key : image.Value;
            KittyGraphicsWriter.Write(
                UseImageReference(KittyGraphicsCommand.DeleteImage(imageId), image.Value == 0),
                [],
                destination);
            count++;
        }

        return count;
    }

    private static int WriteUncertainPlacementDeletes(
        List<KittyGraphicsUncertainPlacementState> placements,
        IBufferWriter<byte> destination,
        HashSet<KittyGraphicsImageReference> hardDeletedImageIds)
    {
        var count = 0;

        foreach (var placement in placements)
        {
            if (hardDeletedImageIds.Contains(new KittyGraphicsImageReference(
                placement.ImageId,
                placement.UsesImageNumber)))
            {
                continue;
            }

            KittyGraphicsWriter.Write(
                UseImageReference(
                    KittyGraphicsCommand.DeletePlacement(placement.ImageId, placement.PlacementId),
                    placement.UsesImageNumber),
                [],
                destination);
            count++;
        }

        return count;
    }

    private void ReturnRetired()
    {
        // A retiring identifier can also already be quarantined in an uncertain set: an earlier
        // transaction may have transferred it directly from a still-committed entry and then been
        // invalidated, leaving it referenced by both the (unchanged) committed state and a
        // quarantine bucket at once. ReturnUncertain owns reclaiming an already-quarantined
        // identifier on its own schedule; returning or re-quarantining one here too would double-
        // free it or track it twice.
        //
        // A retiring identifier that is NOT already quarantined but still awaits its own transmit
        // response (UsesImageNumber/its placement's UsesImageNumber is still true) cannot be
        // returned directly either: the terminal may yet deliver a stale response correlated by
        // this exact number, and handing the number to an unrelated later image before that
        // response arrives would misattribute it. Such an identifier is quarantined instead, via
        // the same _uncertainImages/_uncertainPlacements bucket Invalidate() uses.
        foreach (var id in _retiredPlacementIds!)
        {
            if (IsUncertainPlacement(id))
            {
                continue;
            }

            if (TryGetUnconfirmedRetiredPlacementImageId(id, out var imageId))
            {
                _uncertainPlacements.Add(new KittyGraphicsUncertainPlacementState(imageId, id, usesImageNumber: true));
            }
            else
            {
                _placementIds.Return(id);
            }
        }

        foreach (var id in _retiredImageIds!)
        {
            if (IsUncertainImage(id))
            {
                continue;
            }

            if (IsUnconfirmedRetiredImage(id))
            {
                _ = _uncertainImages.TryAdd(id, 0);
            }
            else
            {
                _imageIds.Return(id);
            }
        }
    }

    /// <summary>Finds whether the pre-commit image still identified by <paramref name="number"/> was never
    /// confirmed by the terminal, using the committed state that is about to be replaced.</summary>
    private bool IsUnconfirmedRetiredImage(uint number)
    {
        // Enumerates _images directly rather than through _images.Values: the ValueCollection
        // wrapper is allocated lazily on its first-ever access, and this lookup can otherwise be
        // the very first thing to touch it on the hot Commit path.
        foreach (var pair in _images)
        {
            if (pair.Value.Number == number)
            {
                return pair.Value.UsesImageNumber;
            }
        }

        return false;
    }

    /// <summary>Finds whether the pre-commit placement still identified by <paramref name="placementId"/>
    /// referenced an image never confirmed by the terminal, using the committed state that is about to be
    /// replaced.</summary>
    private bool TryGetUnconfirmedRetiredPlacementImageId(uint placementId, out uint imageId)
    {
        foreach (var placement in _placements)
        {
            if (placement.PlacementId == placementId)
            {
                imageId = placement.ImageId;
                return placement.UsesImageNumber;
            }
        }

        imageId = 0;
        return false;
    }

    private void ReturnAllCommitted()
    {
        // See the identical caution in ReturnRetired, for both the double-free case and the
        // still-unconfirmed case.
        foreach (var placement in _placements)
        {
            if (IsUncertainPlacement(placement.PlacementId))
            {
                continue;
            }

            if (placement.UsesImageNumber)
            {
                _uncertainPlacements.Add(new KittyGraphicsUncertainPlacementState(
                    placement.ImageId, placement.PlacementId, usesImageNumber: true));
            }
            else
            {
                _placementIds.Return(placement.PlacementId);
            }
        }

        foreach (var pair in _images)
        {
            var image = pair.Value;

            if (IsUncertainImage(image.Number))
            {
                continue;
            }

            if (image.UsesImageNumber)
            {
                _ = _uncertainImages.TryAdd(image.Number, 0);
            }
            else
            {
                _imageIds.Return(image.Number);
            }
        }
    }

    private bool IsUncertainPlacement(uint placementId)
    {
        foreach (var placement in _uncertainPlacements)
        {
            if (placement.PlacementId == placementId)
            {
                return true;
            }
        }

        foreach (var placement in _priorUncertainPlacements)
        {
            if (placement.PlacementId == placementId)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsUncertainImage(uint number) =>
        _uncertainImages.ContainsKey(number) || _priorUncertainImages.ContainsKey(number);

    private void ReturnUncertain()
    {
        // Only the "prior" bucket - whatever was already quarantined going into this cycle - is
        // ever reclaimed here. The "current" bucket may have just been populated by
        // ReturnRetired/ReturnAllCommitted above (same call, same cycle); flushing it here too
        // would give a newly retired-but-unconfirmed identifier zero real protection. Rotating
        // the two buckets instead promotes this cycle's additions to be next cycle's flush
        // candidates, giving every quarantined identifier exactly one full untouched cycle before
        // it can be handed to an unrelated image.
        foreach (var placement in _priorUncertainPlacements)
        {
            _placementIds.Return(placement.PlacementId);
        }

        // Enumerates the dictionary directly rather than through its .Keys wrapper: like
        // .Values, that wrapper is allocated lazily on first-ever access, and this can be the
        // first thing to touch it on the hot Commit/CommitCleanup path.
        foreach (var pair in _priorUncertainImages)
        {
            _imageIds.Return(pair.Key);

            // The identifier is fully re-entering circulation here: any leftover ambiguous-transfer
            // debt for it belongs to whatever image(s) previously held it, not to whoever rents it
            // next. Leaving a stale entry behind would let a later, completely unrelated image's own
            // first reply be silently dropped once this number gets recycled.
            _ = _ambiguousTransferredNumbers.Remove(pair.Key);
        }

        _priorUncertainPlacements.Clear();
        _priorUncertainImages.Clear();

        (_uncertainPlacements, _priorUncertainPlacements) = (_priorUncertainPlacements, _uncertainPlacements);
        (_uncertainImages, _priorUncertainImages) = (_priorUncertainImages, _uncertainImages);
    }

    private void ReturnRented(List<uint> images, List<uint> placements)
    {
        foreach (var id in placements)
        {
            _placementIds.Return(id);
        }

        foreach (var id in images)
        {
            _imageIds.Return(id);
        }
    }

    private void ClearPrepared(bool returnRented)
    {
        if (returnRented)
        {
            ReturnRented(_rentedImageIds!, _rentedPlacementIds!);
        }

        _preparedImages = null;
        _preparedPlacements = null;
        PreparedCellOverlay = null;
        _rentedImageIds = null;
        _rentedPlacementIds = null;
        _rentedPlacementStates = null;
        _transferredImageIds = null;
        _retiredImageIds = null;
        _retiredPlacementIds = null;
        _uploads = [];
        _cellPreludeBytes = [];
        _placementBytes = [];
        _removals = [];
        _prepared = false;
    }

    private void ThrowIfDisposed() => GraphicsBackendSupport.ThrowIfDisposed(_disposed, this);

    private void EnsurePrepared() => GraphicsBackendSupport.EnsurePrepared(
        _disposed,
        this,
        _prepared,
        "A Kitty backend transaction has not been prepared.");

    #endregion
}
