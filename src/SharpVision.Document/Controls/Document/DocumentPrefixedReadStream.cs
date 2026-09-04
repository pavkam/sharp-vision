// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Replays bytes consumed during encoding detection before continuing through an owned
/// source stream without taking ownership of that source.</summary>
internal sealed class DocumentPrefixedReadStream: Stream
{
    private readonly byte[] _prefix;
    private readonly Stream _source;
    private readonly int _prefixEnd;
    private int _prefixOffset;

    /// <summary>Initializes a forward-only view over a prefix slice followed by a source.</summary>
    /// <param name="source">The readable source that remains caller-owned.</param>
    /// <param name="prefix">The buffer containing already-consumed bytes.</param>
    /// <param name="offset">The first prefix byte to replay.</param>
    /// <param name="count">The number of prefix bytes to replay.</param>
    public DocumentPrefixedReadStream(Stream source, byte[] prefix, int offset, int count)
    {
        Debug.Assert(source.CanRead, "The decoding source is readable.");
        Debug.Assert(offset >= 0 && count >= 0 && offset + count <= prefix.Length,
            "The replay slice belongs to its prefix buffer.");

        _source = source;
        _prefix = prefix;
        _prefixOffset = offset;
        _prefixEnd = offset + count;
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
        if (_prefixOffset < _prefixEnd)
        {
            var count = Math.Min(buffer.Length, _prefixEnd - _prefixOffset);
            _prefix.AsSpan(_prefixOffset, count).CopyTo(buffer);
            _prefixOffset += count;
            return count;
        }

        return _source.Read(buffer);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(
        Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        if (_prefixOffset < _prefixEnd)
        {
            var count = Math.Min(buffer.Length, _prefixEnd - _prefixOffset);
            _prefix.AsMemory(_prefixOffset, count).CopyTo(buffer);
            _prefixOffset += count;
            return ValueTask.FromResult(count);
        }

        return _source.ReadAsync(buffer, cancellationToken);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
