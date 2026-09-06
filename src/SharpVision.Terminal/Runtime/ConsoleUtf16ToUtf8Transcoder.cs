// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>
/// Transcodes UTF-16 code units read from a Windows console into UTF-8 bytes, carrying both a
/// split surrogate pair and undelivered output bytes across calls.
/// </summary>
/// <remarks>
/// This type has no dependency on Win32 or <see cref="OperatingSystem.IsWindows"/>, so it can be
/// exercised directly by tests on every platform even though its only production caller,
/// <see cref="WindowsConsoleInputStream"/>, is Windows-only. Two hazards make it more than a
/// direct <see cref="Encoding.UTF8"/> call: a native <c>ReadConsoleW</c> call can end with a lone
/// high surrogate that only completes once the next call's low surrogate arrives, and a caller's
/// destination buffer can be smaller than the bytes one native read produces (buffers as small as
/// one byte are used for interactive terminal input). The stateful <see cref="Encoder"/> handles
/// the first hazard by construction; this type adds the pending-output buffer that handles the
/// second.
/// </remarks>
internal sealed class ConsoleUtf16ToUtf8Transcoder
{
    private readonly Encoder _encoder = Encoding.UTF8.GetEncoder();
    private byte[] _pending = [];
    private int _pendingLength;

    /// <summary>Gets whether transcoded bytes remain undelivered from a previous call.</summary>
    /// <remarks>
    /// A caller should drain this before issuing another native read: the pending bytes already
    /// represent completed output and take priority over blocking for more input.
    /// </remarks>
    public bool HasPendingOutput => _pendingLength > 0;

    /// <summary>Copies previously transcoded bytes that did not fit an earlier destination.</summary>
    /// <param name="destination">The buffer to fill.</param>
    /// <returns>The number of bytes copied, which can be fewer than the pending amount.</returns>
    public int DrainPending(Span<byte> destination)
    {
        var count = Math.Min(destination.Length, _pendingLength);

        if (count <= 0)
        {
            return count;
        }

        _pending.AsSpan(0, count).CopyTo(destination);
        _pending.AsSpan(count, _pendingLength - count).CopyTo(_pending);
        _pendingLength -= count;

        return count;
    }

    /// <summary>Transcodes newly read UTF-16 code units and fills as much of the destination as fits.</summary>
    /// <param name="chars">The code units read from the native call.</param>
    /// <param name="flush">
    /// Whether this is the final call for the input's lifetime, so a leftover unpaired high
    /// surrogate is replaced with U+FFFD instead of being held for a low surrogate that will
    /// never arrive.
    /// </param>
    /// <param name="destination">The caller's buffer.</param>
    /// <returns>
    /// The number of bytes written into <paramref name="destination"/>. Any transcoded bytes that
    /// did not fit are retained and returned by a later <see cref="DrainPending"/> or
    /// <see cref="Transcode"/> call.
    /// </returns>
    public int Transcode(ReadOnlySpan<char> chars, bool flush, Span<byte> destination)
    {
        if (!chars.IsEmpty || flush)
        {
            // GetByteCount and GetBytes are called back to back with the same flush flag, which is
            // the documented safe pattern for sizing a stateful encoder's next output - the encoder
            // is not otherwise mutated by GetByteCount.
            var maxBytes = _encoder.GetByteCount(chars, flush);

            if (maxBytes > 0)
            {
                if (_pending.Length < _pendingLength + maxBytes)
                {
                    Array.Resize(ref _pending, _pendingLength + maxBytes);
                }

                var written = _encoder.GetBytes(chars, _pending.AsSpan(_pendingLength, maxBytes), flush);
                _pendingLength += written;
            }
        }

        return DrainPending(destination);
    }
}
