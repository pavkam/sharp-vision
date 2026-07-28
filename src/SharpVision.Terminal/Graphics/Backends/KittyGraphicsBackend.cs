// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

using Graphics;

using Kitty;

using Rendering;

using ImageFormat = Format;
using KittyFormat = Kitty.Format;
using MultiplexerRoute = Multiplexing.Route;

/// <summary>Implements finite transactional direct Kitty image upload and placement.</summary>
internal sealed class KittyGraphicsBackend: IGraphicsBackend
{
    private readonly IdentifierAllocator _imageIds;
    private readonly IdentifierAllocator _placementIds;
    private readonly int _maxPreparedBytes;
    private readonly BoundedWriter _output;
    private readonly MultiplexerRoute? _route;
    private Dictionary<ulong, ImageState> _images = [];
    private List<PlacementState> _placements = [];
    private readonly List<uint> _uncertainImageIds;
    private readonly List<UncertainPlacementState> _uncertainPlacements;
    private Dictionary<ulong, ImageState>? _preparedImages;
    private List<PlacementState>? _preparedPlacements;
    private List<uint>? _rentedImageIds;
    private List<uint>? _rentedPlacementIds;
    private List<UncertainPlacementState>? _rentedPlacementStates;
    private List<uint>? _retiredImageIds;
    private List<uint>? _retiredPlacementIds;
    private byte[] _uploads = [];
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
        int maxPreparedBytes = 16 * 1024 * 1024,
        MultiplexerRoute? route = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxImages);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPlacements);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPreparedBytes);
        _imageIds = new IdentifierAllocator(maxImages);
        _placementIds = new IdentifierAllocator(maxPlacements);
        _uncertainImageIds = new List<uint>(maxImages);
        _uncertainPlacements = new List<UncertainPlacementState>(maxPlacements);
        _maxPreparedBytes = maxPreparedBytes;
        _output = new BoundedWriter(maxPreparedBytes);
        _route = route;

        if (route is not null && !route.CanRouteGraphics)
        {
            _output.Dispose();
            throw new NotSupportedException("The explicit multiplexer route cannot carry Kitty graphics.");
        }
    }

    #endregion

    #region Frame transactions

    /// <inheritdoc />
    public GraphicsBackendResult Prepare(
        Frame? front,
        Frame back,
        bool full,
        GraphicsContext context = default)
    {
        ArgumentNullException.ThrowIfNull(back);
        ThrowIfDisposed();

        if (_prepared || _cleanupPrepared)
        {
            throw new InvalidOperationException("A Kitty backend transaction is already prepared.");
        }

        var reconstruct = full || _invalidated;
        var images = new Dictionary<ulong, ImageState>();
        var placements = new List<PlacementState>(back.PlacementCount);
        var rentedImages = new List<uint>();
        var rentedPlacements = new List<uint>();
        var rentedPlacementStates = new List<UncertainPlacementState>();
        var output = _output;
        output.Reset(_maxPreparedBytes);
        var uploadCount = 0;
        var placementCount = 0;
        var removalCount = 0;
        var enabled = context.Profile is null || GraphicsBackendSelector.IsAuthoritative(
            context.Profile.Capabilities.KittyGraphics,
            allowQuery: true);
        var encodable = new bool[back.PlacementCount];

        for (var index = 0; index < encodable.Length; index++)
        {
            encodable[index] = enabled && back.IsPlacementEffective(index);
        }

        var blocked = FindFallbackBlockedPlacements(back, encodable);

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
                        var id = _imageIds.Rent();
                        rentedImages.Add(id);
                        imageState = new ImageState(image, id);
                    }

                    images.Add(image.Identity, imageState);

                    if (reconstruct || !_images.ContainsKey(image.Identity))
                    {
                        WriteUpload(imageState, output);
                        uploadCount++;
                    }
                }

                PlacementState placementState;

                var effectiveIndex = placements.Count;

                if (effectiveIndex < _placements.Count &&
                    _placements[effectiveIndex].Placement.ImageIdentity == placement.ImageIdentity)
                {
                    var previous = _placements[effectiveIndex];
                    placementState = new PlacementState(placement, imageState.Id, previous.PlacementId);
                }
                else
                {
                    var id = _placementIds.Rent();
                    rentedPlacements.Add(id);
                    placementState = new PlacementState(placement, imageState.Id, id);
                    rentedPlacementStates.Add(new UncertainPlacementState(imageState.Id, id));
                }

                placements.Add(placementState);
            }

            var remaining = _maxPreparedBytes;
            _uploads = FinishApcPhase(output, ref remaining);

            for (var index = 0; index < placements.Count; index++)
            {
                var placementState = placements[index];
                var placement = placementState.Placement;

                if (reconstruct ||
                    index >= _placements.Count ||
                    _placements[index].PlacementId != placementState.PlacementId ||
                    _placements[index].Placement != placement)
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
            var hardDeletedImageIds = new HashSet<uint>(_uncertainImageIds);

            foreach (var previous in _images)
            {
                if (!images.ContainsKey(previous.Key))
                {
                    _ = hardDeletedImageIds.Add(previous.Value.Id);
                }
            }

            removalCount += WriteUncertainDeletes(output, hardDeletedImageIds);

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

                if (images.ContainsKey(previous.Placement.ImageIdentity))
                {
                    Writer.Write(
                        Command.DeletePlacement(previous.ImageId, previous.PlacementId),
                        [],
                        output);
                    removalCount++;
                }

                retiredPlacements.Add(previous.PlacementId);
            }

            var retiredImages = new List<uint>();

            foreach (var previous in _images)
            {
                if (!images.ContainsKey(previous.Key))
                {
                    Writer.Write(Command.DeleteImage(previous.Value.Id), [], output);
                    retiredImages.Add(previous.Value.Id);
                    removalCount++;
                }
            }

            _removals = FinishApcPhase(output, ref remaining);
            _preparedImages = images;
            _preparedPlacements = placements;
            _rentedImageIds = rentedImages;
            _rentedPlacementIds = rentedPlacements;
            _rentedPlacementStates = rentedPlacementStates;
            _retiredImageIds = retiredImages;
            _retiredPlacementIds = retiredPlacements;
            _prepared = true;
            return new GraphicsBackendResult(
                uploadCount + placementCount + removalCount != 0,
                uploadCount,
                placementCount,
                removalCount);
        }
        catch
        {
            ReturnRented(rentedImages, rentedPlacements);
            _uploads = [];
            _placementBytes = [];
            _removals = [];
            output.Reset(_maxPreparedBytes);
            throw;
        }
    }

    private static bool[] FindFallbackBlockedPlacements(
        Frame frame,
        ReadOnlySpan<bool> encodable)
    {
        var blocked = new bool[frame.PlacementCount];

        // A lower Kitty placement is safe only when every overlapping later placement can
        // retain its image semantics. Walk backward so an ordinary-cell fallback propagates
        // transitively through the finite paint-order DAG without unsafe clipping.
        for (var lower = frame.PlacementCount - 1; lower >= 0; lower--)
        {
            if (!encodable[lower])
            {
                continue;
            }

            var lowerBounds = frame.GetPlacement(lower).Destination;

            for (var upper = lower + 1; upper < frame.PlacementCount; upper++)
            {
                var intersection = lowerBounds.Intersect(frame.GetPlacement(upper).Destination);

                if (intersection.Width != 0 &&
                    intersection.Height != 0 &&
                    (!encodable[upper] || blocked[upper]))
                {
                    blocked[lower] = true;
                    break;
                }
            }
        }

        return blocked;
    }

    /// <inheritdoc />
    public void WriteUploads(IBufferWriter<byte> destination)
    {
        EnsurePrepared();
        WritePrepared(_uploads, destination);
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
        ClearPrepared(returnRented: false);
        _invalidated = false;
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        if (_prepared)
        {
            Debug.Assert(
                _uncertainImageIds.Count + _rentedImageIds!.Count <= _uncertainImageIds.Capacity,
                "The bounded image allocator must fit every uncertain rental.");
            Debug.Assert(
                _uncertainPlacements.Count + _rentedPlacementStates!.Count <= _uncertainPlacements.Capacity,
                "The bounded placement allocator must fit every uncertain rental.");
            _uncertainImageIds.AddRange(_rentedImageIds!);
            _uncertainPlacements.AddRange(_rentedPlacementStates!);
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

        if (_prepared || _cleanupPrepared)
        {
            throw new InvalidOperationException("A Kitty backend transaction is already prepared.");
        }

        var output = _output;
        output.Reset(_maxPreparedBytes);

        foreach (var image in _images.Values)
        {
            Writer.Write(Command.DeleteImage(image.Id), [], output);
        }

        var hardDeletedImageIds = new HashSet<uint>(_uncertainImageIds);

        foreach (var image in _images.Values)
        {
            _ = hardDeletedImageIds.Add(image.Id);
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
        _output.Dispose();
        _disposed = true;
    }

    #endregion

    #region Encoding

    private void WritePlacement(PlacementState state, int zIndex, IBufferWriter<byte> destination)
    {
        WriteCursor(
            new Point(state.Placement.Destination.X, state.Placement.Destination.Y),
            destination);
        var command = Command.Place(
            state.ImageId,
            state.PlacementId,
            state.Placement.Source,
            new Size(state.Placement.Destination.Width, state.Placement.Destination.Height),
            zIndex);

        if (_route is null)
        {
            Writer.Write(command, [], destination);
            return;
        }

        var apc = new ArrayBufferWriter<byte>(256);
        Writer.Write(command, [], apc);

        if (!_route.TryWriteGraphics(destination, apc.WrittenSpan))
        {
            throw new InvalidOperationException("The Kitty placement exceeded its authorized route.");
        }
    }

    private static void WriteCursor(Point value, IBufferWriter<byte> destination)
    {
        Span<byte> cursor = stackalloc byte[64];
        var position = 0;
        cursor[position++] = 0x1b;
        cursor[position++] = (byte) '[';
        _ = Utf8Formatter.TryFormat(value.Y + 1, cursor[position..], out var row);
        position += row;
        cursor[position++] = (byte) ';';
        _ = Utf8Formatter.TryFormat(value.X + 1, cursor[position..], out var column);
        position += column;
        cursor[position++] = (byte) 'H';
        destination.Write(cursor[..position]);
    }

    private static void WriteUpload(ImageState state, IBufferWriter<byte> destination)
    {
        var format = state.Image.Format == ImageFormat.Rgba ? KittyFormat.Rgba : KittyFormat.Png;
        Writer.WriteTransmission(
            Command.Transmit(state.Id, state.Image.Size, format),
            state.Image.Source,
            destination,
            maxPayloadBytes: state.Image.ByteCount);
    }

    private static void WritePrepared(ReadOnlySpan<byte> bytes, IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(bytes);
    }

    private byte[] FinishApcPhase(BoundedWriter output, ref int remaining)
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

    private int WriteUncertainDeletes(
        IBufferWriter<byte> destination,
        HashSet<uint> hardDeletedImageIds)
    {
        var count = 0;

        foreach (var imageId in _uncertainImageIds)
        {
            Writer.Write(Command.DeleteImage(imageId), [], destination);
            count++;
        }

        foreach (var placement in _uncertainPlacements)
        {
            if (hardDeletedImageIds.Contains(placement.ImageId))
            {
                continue;
            }

            Writer.Write(
                Command.DeletePlacement(placement.ImageId, placement.PlacementId),
                [],
                destination);
            count++;
        }

        return count;
    }

    private void ReturnRetired()
    {
        foreach (var id in _retiredPlacementIds!)
        {
            _placementIds.Return(id);
        }

        foreach (var id in _retiredImageIds!)
        {
            _imageIds.Return(id);
        }
    }

    private void ReturnAllCommitted()
    {
        foreach (var placement in _placements)
        {
            _placementIds.Return(placement.PlacementId);
        }

        foreach (var image in _images.Values)
        {
            _imageIds.Return(image.Id);
        }
    }

    private void ReturnUncertain()
    {
        foreach (var placement in _uncertainPlacements)
        {
            _placementIds.Return(placement.PlacementId);
        }

        foreach (var imageId in _uncertainImageIds)
        {
            _imageIds.Return(imageId);
        }

        _uncertainPlacements.Clear();
        _uncertainImageIds.Clear();
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
        _rentedImageIds = null;
        _rentedPlacementIds = null;
        _rentedPlacementStates = null;
        _retiredImageIds = null;
        _retiredPlacementIds = null;
        _uploads = [];
        _placementBytes = [];
        _removals = [];
        _prepared = false;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private void EnsurePrepared()
    {
        ThrowIfDisposed();

        if (!_prepared)
        {
            throw new InvalidOperationException("A Kitty backend transaction has not been prepared.");
        }
    }

    #endregion
}
