// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Graphics.Backends;

using Buffers;

using Graphics;

using Rendering;

using ItermWriter = Iterm.Writer;
using MultiplexerRoute = Multiplexing.MultiplexerRoute;
using SixelWriter = Sixel.Writer;

/// <summary>
/// Prepares one frame-ordered non-retained stream across enabled graphics protocols and repairs
/// stale pixels through ordinary cell reconstruction.
/// </summary>
internal sealed class NonRetainedGraphicsBackend: IGraphicsBackend
{
    private readonly bool _enableIterm;
    private readonly bool _enableSixel;
    private readonly int _maxPreparedBytes;
    private readonly MultiplexerRoute? _route;
    private bool _cleanupPrepared;
    private bool _disposed;
    private bool _hadMetricPlacements;
    private bool _hadPlacements;
    private bool _invalidated = true;
    private Geometry.Metrics? _metrics;
    private byte[] _placementBytes = [];
    private bool _prepared;
    private bool _preparedHasMetricPlacements;
    private bool _preparedHasPlacements;
    private Geometry.Metrics? _preparedMetrics;

    /// <summary>Initializes enabled protocols, finite output, and an optional authorized route.</summary>
    /// <param name="enableSixel">
    /// Whether exact-metric RGBA or decodable PNG placements may use sixel. A PNG source outside
    /// this decoder's scope (interlaced, a bit depth other than 8, or indexed without a palette)
    /// falls back to iTerm when it is also enabled, rather than being dropped.
    /// </param>
    /// <param name="enableIterm">Whether compatible full-source PNG placements may use iTerm2.</param>
    /// <param name="maxPreparedBytes">The positive complete prepared byte bound.</param>
    /// <param name="route">An optional explicit tmux-only graphics route.</param>
    /// <exception cref="ArgumentException">No protocol is enabled.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxPreparedBytes"/> is not positive.</exception>
    /// <exception cref="NotSupportedException"><paramref name="route"/> cannot carry graphics.</exception>
    public NonRetainedGraphicsBackend(
        bool enableSixel,
        bool enableIterm,
        int maxPreparedBytes = GraphicsBackendSelector.DefaultMaxPreparedBytes,
        MultiplexerRoute? route = null)
    {
        if (!enableSixel && !enableIterm)
        {
            throw new ArgumentException("At least one non-retained graphics protocol must be enabled.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPreparedBytes);

        if (route is not null && !route.CanRouteGraphics)
        {
            throw new NotSupportedException("The explicit multiplexer route cannot carry graphics.");
        }

        _enableSixel = enableSixel;
        _enableIterm = enableIterm;
        _maxPreparedBytes = maxPreparedBytes;
        _route = route;
    }

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
            throw new InvalidOperationException("A non-retained graphics transaction is already prepared.");
        }

        var metrics = context.Metrics;
        var enableSixel = _enableSixel && (context.Profile is null || GraphicsBackendSelector.IsAuthoritative(
            context.Profile.Capabilities.Sixel,
            allowQuery: true));
        var enableIterm = _enableIterm && (context.Profile is null || GraphicsBackendSelector.IsAuthoritative(
            context.Profile.Capabilities.ItermImages,
            allowQuery: true));
        var encodable = new bool[back.PlacementCount];
        var metricDependent = new bool[back.PlacementCount];
        var sixelImages = new ImageSource?[back.PlacementCount];
        var skippedPlacements = ClassifyPlacements(
            back,
            metrics,
            enableSixel,
            enableIterm,
            encodable,
            metricDependent,
            sixelImages);
        var blocked = back.FindFallbackBlockedPlacements(encodable);
        var currentCount = CountRenderable(
            encodable,
            metricDependent,
            blocked,
            out var currentHasMetricPlacements);
        var placementsChanged = Damage.PlacementsChanged(front, back, full || _invalidated);
        var geometryChanged = (_hadMetricPlacements || currentHasMetricPlacements) && _metrics != metrics;
        var stateChanged = placementsChanged || geometryChanged;
        var fullCellRedraw = stateChanged && (_hadPlacements || currentCount != 0);
        var reconstruct = full || _invalidated || stateChanged;
        var repaint = SelectRepaints(front, back, encodable, blocked, reconstruct);
        using var output = new BoundedBufferWriter(_maxPreparedBytes, initialRentBytes: 256);
        var placementCount = 0;

        try
        {
            for (var index = 0; index < back.PlacementCount; index++)
            {
                var placement = back.GetPlacement(index);

                if (!repaint[index])
                {
                    continue;
                }

                // Each placement is validated against what's actually left in the shared frame
                // buffer, not the full budget — a pre-flight check against the full constant can
                // pass in isolation while still overflowing the buffer once combined with bytes
                // earlier placements in this same frame already consumed (see #117).
                var remaining = _maxPreparedBytes - output.WrittenCount;

                if (remaining <= 0)
                {
                    continue;
                }

                // Classification decodes PNG once so an unsupported source is observable and a
                // supported source does not pay for the same decode twice in one frame.
                if (sixelImages[index] is { } sixelImage &&
                    TryGetSixelPixels(placement, metrics, enableSixel, out var pixels) &&
                    TryWriteSixel(placement, sixelImage, pixels, output, remaining))
                {
                    placementCount++;
                    continue;
                }

                // A failed sixel attempt above may have written an orphaned cursor move before
                // decoding gave out, so the iTerm fallback re-reads the budget instead of reusing
                // the pre-attempt estimate.
                remaining = _maxPreparedBytes - output.WrittenCount;

                if (remaining > 0 && CanEncodeIterm(placement, enableIterm, remaining))
                {
                    WriteIterm(placement, output, remaining);
                    placementCount++;
                }
            }

            if (placementCount != 0)
            {
                WriteCursor(back.Cursor.Position, output);
            }

            _placementBytes = output.WrittenSpan.ToArray();
            _preparedHasPlacements = currentCount != 0;
            _preparedHasMetricPlacements = currentHasMetricPlacements;
            _preparedMetrics = currentHasMetricPlacements ? metrics : null;
            _prepared = true;
            return new GraphicsBackendResult(
                changed: fullCellRedraw || placementCount != 0,
                uploads: 0,
                placements: placementCount,
                removals: 0,
                fullCellRedraw,
                skippedPlacements);
        }
        catch
        {
            ClearPrepared();
            throw;
        }
    }

    /// <inheritdoc />
    public void WriteUploads(IBufferWriter<byte> destination)
    {
        EnsurePrepared();
        ArgumentNullException.ThrowIfNull(destination);
    }

    /// <inheritdoc />
    public void WritePlacements(IBufferWriter<byte> destination)
    {
        EnsurePrepared();
        ArgumentNullException.ThrowIfNull(destination);
        destination.Write(_placementBytes);
    }

    /// <inheritdoc />
    public void WriteRemovals(IBufferWriter<byte> destination)
    {
        EnsurePrepared();
        ArgumentNullException.ThrowIfNull(destination);
    }

    /// <inheritdoc />
    public void Commit()
    {
        Debug.Assert(_prepared, "Only prepared non-retained state can commit.");
        _hadPlacements = _preparedHasPlacements;
        _hadMetricPlacements = _preparedHasMetricPlacements;
        _metrics = _preparedMetrics;
        ClearPrepared();
        _invalidated = false;
    }

    /// <inheritdoc />
    public void Invalidate()
    {
        if (_prepared)
        {
            ClearPrepared();
        }

        _cleanupPrepared = false;
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
            throw new InvalidOperationException("A non-retained graphics transaction is already prepared.");
        }

        _cleanupPrepared = true;
        return 0;
    }

    /// <inheritdoc />
    public void WriteCleanup(IBufferWriter<byte> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        if (!_cleanupPrepared)
        {
            throw new InvalidOperationException("Non-retained cleanup has not been prepared.");
        }
    }

    /// <inheritdoc />
    public void CommitCleanup()
    {
        Debug.Assert(_cleanupPrepared, "Only prepared non-retained cleanup can commit.");
        _hadPlacements = false;
        _hadMetricPlacements = false;
        _metrics = null;
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
        _hadPlacements = false;
        _hadMetricPlacements = false;
        _metrics = null;
        _disposed = true;
    }

    #endregion

    #region Protocol encoding

    // Skips a placement whose encoded size exceeds the shared buffer's remaining budget instead
    // of letting the write throw mid-frame — the caller has already re-checked maxOutputBytes
    // against what's actually left, not the full constant, so this is the deterministic
    // degrade path the full-budget pre-check couldn't provide on its own (see #117). PNG decode
    // failures are excluded during classification, before any cursor bytes can be written.
    private bool TryWriteSixel(
        Placement placement,
        ImageSource image,
        Rect pixels,
        IBufferWriter<byte> destination,
        int maxOutputBytes)
    {
        try
        {
            WriteSixel(placement, image, pixels, destination, maxOutputBytes);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private void WriteSixel(
        Placement placement,
        ImageSource image,
        Rect pixels,
        IBufferWriter<byte> destination,
        int maxOutputBytes)
    {
        var source = placement.Source;

        WriteCursor(new Point(placement.Destination.X, placement.Destination.Y), destination);

        if (_route is null)
        {
            SixelWriter.Write(
                image,
                source,
                new Size(pixels.Width, pixels.Height),
                placement.Mode,
                destination,
                maxOutputBytes);
            return;
        }

        var dcs = new ArrayBufferWriter<byte>();
        SixelWriter.Write(
            image,
            source,
            new Size(pixels.Width, pixels.Height),
            placement.Mode,
            dcs,
            maxOutputBytes);
        WriteRoutedFrame(dcs.WrittenSpan, destination);
    }

    private void WriteIterm(Placement placement, IBufferWriter<byte> destination, int maxOutputBytes)
    {
        WriteCursor(new Point(placement.Destination.X, placement.Destination.Y), destination);
        var destinationCells = new Size(
            placement.Destination.Width,
            placement.Destination.Height);

        if (_route is null)
        {
            ItermWriter.Write(
                placement.Image!,
                destinationCells,
                placement.Mode,
                destination,
                maxOutputBytes: maxOutputBytes);
            return;
        }

        var transaction = new ArrayBufferWriter<byte>();
        var maxSequenceBytes = Math.Min(
            ItermWriter.MaximumSequenceBytes,
            _route.GetMaximumGraphicsFrameBytes(escapeBytes: 2));

        if (maxSequenceBytes == 0)
        {
            throw new InvalidOperationException("The authorized route cannot hold an iTerm2 OSC frame.");
        }

        ItermWriter.Write(
            placement.Image!,
            destinationCells,
            placement.Mode,
            transaction,
            maxSequenceBytes,
            maxOutputBytes: maxOutputBytes);
        var remaining = transaction.WrittenSpan;

        while (!remaining.IsEmpty)
        {
            var end = remaining.IndexOf("\u001b\\"u8);

            if (end < 0)
            {
                throw new InvalidOperationException("The prepared iTerm2 transaction lost its OSC boundary.");
            }

            var length = end + 2;
            WriteRoutedFrame(remaining[..length], destination);
            remaining = remaining[length..];
        }
    }

    private void WriteRoutedFrame(ReadOnlySpan<byte> frame, IBufferWriter<byte> destination)
    {
        Debug.Assert(_route is not null, "Only explicit routes wrap graphics frames.");

        if (!_route.TryWriteGraphics(destination, frame))
        {
            throw new InvalidOperationException("The graphics frame exceeded its authorized route.");
        }
    }

    #endregion

    #region Eligibility and damage

    private List<GraphicsPlacementDiagnostic>? ClassifyPlacements(
        Frame frame,
        Geometry.Metrics? metrics,
        bool enableSixel,
        bool enableIterm,
        Span<bool> encodable,
        Span<bool> metricDependent,
        Span<ImageSource?> sixelImages)
    {
        List<GraphicsPlacementDiagnostic>? skipped = null;

        for (var index = 0; index < frame.PlacementCount; index++)
        {
            if (!frame.IsPlacementEffective(index))
            {
                continue;
            }

            var placement = frame.GetPlacement(index);

            var sixelEligible = TryGetSixelPixels(placement, metrics, enableSixel, out _);

            if (sixelEligible && TryGetSixelImage(placement, out var sixelImage))
            {
                encodable[index] = true;
                metricDependent[index] = true;
                sixelImages[index] = sixelImage;
            }
            else if (CanEncodeIterm(placement, enableIterm))
            {
                encodable[index] = true;
            }
            else if (!IsFormatEncodable(placement.Image!.Format, enableSixel, enableIterm) ||
                     (sixelEligible && placement.Image.Format == Format.Png))
            {
                // Isolated from the mode/rect/metrics checks above: this placement's format has
                // no encodable path on any enabled protocol, distinct from an otherwise-encodable
                // format that failed on some other eligibility condition (see #233). When neither
                // protocol is authorized at all, every format necessarily fails IsFormatEncodable -
                // that is a deauthorized-profile condition, not a format mismatch, so it gets its
                // own reason instead of being mislabeled as FormatNotEncodable.
                var reason = !enableSixel && !enableIterm
                    ? GraphicsPlacementSkipReason.ProtocolNotAuthorized
                    : GraphicsPlacementSkipReason.FormatNotEncodable;
                (skipped ??= []).Add(new GraphicsPlacementDiagnostic(placement.ImageIdentity, reason));
            }
        }

        return skipped;
    }

    private static bool TryGetSixelImage(Placement placement, [NotNullWhen(true)] out ImageSource? image)
    {
        image = placement.Image!;

        if (image.Format == Format.Rgba)
        {
            return true;
        }

        try
        {
            var rgba = image.Source.DecodeRgba();
            image = ImageSource.FromDecodedRgba(image.Size, rgba);
            return true;
        }
        catch (NotSupportedException)
        {
            image = null;
            return false;
        }
        catch (ArgumentException)
        {
            image = null;
            return false;
        }
    }

    private static bool IsFormatEncodable(Format format, bool enableSixel, bool enableIterm) =>
        (enableSixel && format is Format.Rgba or Format.Png) || (enableIterm && format == Format.Png);

    private static int CountRenderable(
        ReadOnlySpan<bool> encodable,
        ReadOnlySpan<bool> metricDependent,
        ReadOnlySpan<bool> blocked,
        out bool hasMetricPlacements)
    {
        var count = 0;
        hasMetricPlacements = false;

        for (var index = 0; index < encodable.Length; index++)
        {
            if (!encodable[index] || blocked[index])
            {
                continue;
            }

            count++;
            hasMetricPlacements |= metricDependent[index];
        }

        return count;
    }

    private static bool[] SelectRepaints(
        Frame? front,
        Frame back,
        ReadOnlySpan<bool> encodable,
        ReadOnlySpan<bool> blocked,
        bool reconstruct)
    {
        var repaint = new bool[back.PlacementCount];

        for (var index = 0; index < back.PlacementCount; index++)
        {
            repaint[index] = encodable[index] &&
                             !blocked[index] &&
                             (reconstruct || IntersectsDamage(front, back, back.GetPlacement(index).Destination));
        }

        if (reconstruct)
        {
            return repaint;
        }

        // A selected lower image can cover a later image even when the original cell damage did
        // not touch that upper image. Replay the finite transitive overlap closure in paint order.
        for (var lower = 0; lower < back.PlacementCount; lower++)
        {
            if (!repaint[lower])
            {
                continue;
            }

            var lowerBounds = back.GetPlacement(lower).Destination;

            for (var upper = lower + 1; upper < back.PlacementCount; upper++)
            {
                if (encodable[upper] &&
                    !blocked[upper] &&
                    lowerBounds.Overlaps(back.GetPlacement(upper).Destination))
                {
                    repaint[upper] = true;
                }
            }
        }

        return repaint;
    }

    private static bool TryGetSixelPixels(
        Placement placement,
        Geometry.Metrics? metrics,
        bool enableSixel,
        out Rect pixels)
    {
        pixels = default;
        Debug.Assert(!placement.IsEmpty, "Active frame placements cannot be empty.");
        return enableSixel &&
               placement.Image!.Format is Format.Rgba or Format.Png &&
               metrics is { } available &&
               available.TryMapCells(placement.Destination, out pixels);
    }

    private bool CanEncodeIterm(Placement placement, bool enableIterm) =>
        CanEncodeIterm(placement, enableIterm, _maxPreparedBytes);

    private bool CanEncodeIterm(Placement placement, bool enableIterm, int maxOutputBytes)
    {
        Debug.Assert(!placement.IsEmpty, "Active frame placements cannot be empty.");
        var image = placement.Image!;
        if (!enableIterm ||
            image.Format != Format.Png ||
            placement.Source != new Rect(0, 0, image.Size.Width, image.Size.Height) ||
            placement.Mode is not PlacementMode.Contain and not PlacementMode.Stretch)
        {
            return false;
        }

        var maxSequenceBytes = _route is null
            ? ItermWriter.MaximumSequenceBytes
            : Math.Min(
                ItermWriter.MaximumSequenceBytes,
                _route.GetMaximumGraphicsFrameBytes(escapeBytes: 2));
        return ItermWriter.CanWrite(
            image,
            new Size(placement.Destination.Width, placement.Destination.Height),
            placement.Mode,
            maxSequenceBytes,
            maxOutputBytes);
    }

    private static bool IntersectsDamage(Frame? front, Frame back, Rect placement)
    {
        foreach (var span in Damage.Enumerate(front, back))
        {
            if (span.Row >= placement.Y &&
                span.Row < placement.Bottom &&
                span.Start < placement.Right &&
                span.Start + span.Length > placement.X)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    private static void WriteCursor(Point value, IBufferWriter<byte> destination) =>
        Csi.Position(new Writer(destination), value.Y + 1, value.X + 1);

    private void ClearPrepared()
    {
        _placementBytes = [];
        _preparedHasPlacements = false;
        _preparedHasMetricPlacements = false;
        _preparedMetrics = null;
        _prepared = false;
    }

    private void EnsurePrepared() => GraphicsBackendSupport.EnsurePrepared(
        _disposed,
        this,
        _prepared,
        "No non-retained graphics transaction is prepared.");

    private void ThrowIfDisposed() => GraphicsBackendSupport.ThrowIfDisposed(_disposed, this);
}
