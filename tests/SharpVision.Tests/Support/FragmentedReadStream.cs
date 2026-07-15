// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes borrowed bytes through bounded non-seekable read fragments.</summary>
internal sealed class FragmentedReadStream: Stream
{
    private readonly byte[] _bytes;
    private readonly int _fragmentLength;
    private int _position;

    /// <summary>Initializes one readable fragment stream at a validated starting offset.</summary>
    /// <param name="bytes">The non-null borrowed byte array.</param>
    /// <param name="position">The initial position inside the array.</param>
    /// <param name="fragmentLength">The positive maximum bytes returned by one read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A numeric argument is outside its valid range.</exception>
    internal FragmentedReadStream(byte[] bytes, int position, int fragmentLength)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfNegative(position);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(position, bytes.Length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(fragmentLength);
        _bytes = bytes;
        _position = position;
        _fragmentLength = fragmentLength;
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Read(buffer.AsSpan(offset, count));
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer)
    {
        var length = Math.Min(Math.Min(buffer.Length, _fragmentLength), _bytes.Length - _position);

        if (length == 0)
        {
            return 0;
        }

        _bytes.AsSpan(_position, length).CopyTo(buffer);
        _position += length;
        return length;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
