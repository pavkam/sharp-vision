// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Buffers;

using SharpVision.Terminal.Buffers;

/// <summary>
/// Verifies the pooled bounded writer that replaced Rendering.Buffer, Graphics.PreparedBuffer, and
/// Kitty.BoundedWriter.
/// </summary>
public sealed class BoundedBufferWriterTests
{
    /// <summary>Verifies a non-positive maximum is rejected.</summary>
    [Fact]
    public void Constructor_WhenMaximumIsNotPositive_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new BoundedBufferWriter(0, 16));

    /// <summary>Verifies a non-positive initial rent is rejected.</summary>
    [Fact]
    public void Constructor_WhenInitialRentIsNotPositive_Throws() =>
        _ = Should.Throw<ArgumentOutOfRangeException>(() => new BoundedBufferWriter(16, 0));

    /// <summary>Verifies a fresh writer starts with no written bytes.</summary>
    [Fact]
    public void WrittenCount_WhenFresh_IsZero()
    {
        using var writer = new BoundedBufferWriter(16, 4);

        writer.WrittenCount.ShouldBe(0);
        writer.WrittenSpan.Length.ShouldBe(0);
        writer.WrittenMemory.Length.ShouldBe(0);
    }

    /// <summary>Verifies a normal write round-trips through GetSpan, Advance, and WrittenSpan.</summary>
    [Fact]
    public void Advance_WhenWithinBudget_UpdatesWrittenCountAndSpan()
    {
        using var writer = new BoundedBufferWriter(16, 4);
        var span = writer.GetSpan(3);
        "abc"u8.CopyTo(span);
        writer.Advance(3);

        writer.WrittenCount.ShouldBe(3);
        writer.WrittenSpan.ToArray().ShouldBe("abc"u8.ToArray());
        writer.WrittenMemory.ToArray().ShouldBe("abc"u8.ToArray());
    }

    /// <summary>Verifies a write that grows past the initial pooled rent still succeeds and preserves prior bytes.</summary>
    [Fact]
    public void GetSpan_WhenSizeHintExceedsInitialRent_GrowsAndPreservesWrittenBytes()
    {
        using var writer = new BoundedBufferWriter(64, initialRentBytes: 4);
        "ab"u8.CopyTo(writer.GetSpan(2));
        writer.Advance(2);

        var grown = writer.GetSpan(32);
        grown.Length.ShouldBeGreaterThanOrEqualTo(32);
        "cd"u8.CopyTo(grown);
        writer.Advance(2);

        writer.WrittenSpan.ToArray().ShouldBe("abcd"u8.ToArray());
    }

    /// <summary>Verifies growth never allocates past the configured maximum.</summary>
    [Fact]
    public void GetSpan_WhenSizeHintWouldExceedMaximum_Throws()
    {
        using var writer = new BoundedBufferWriter(8, initialRentBytes: 4);

        _ = Should.Throw<InvalidOperationException>(() => writer.GetSpan(9));
    }

    /// <summary>Verifies Advance with a negative count is rejected.</summary>
    [Fact]
    public void Advance_WhenCountIsNegative_Throws()
    {
        using var writer = new BoundedBufferWriter(16, 4);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => writer.Advance(-1));
    }

    /// <summary>Verifies Advance cannot claim more than the buffer granted by the last GetSpan/GetMemory call.</summary>
    [Fact]
    public void Advance_WhenCountExceedsGrantedBuffer_ThrowsArgumentException()
    {
        using var writer = new BoundedBufferWriter(16, 4);
        _ = writer.GetSpan(2);

        _ = Should.Throw<ArgumentException>(() => writer.Advance(100));
    }

    /// <summary>Verifies Advance cannot exceed the active byte budget even when the pooled buffer has room.</summary>
    [Fact]
    public void Advance_WhenCountExceedsActiveLimit_ThrowsInvalidOperationException()
    {
        // Grow the pooled buffer to at least 64 bytes first, then narrow the active limit far
        // below it with Reset(4) - Reset never shrinks the physical rent, so this guarantees the
        // buffer has room for the write and only the policy limit rejects it, deterministically,
        // regardless of ArrayPool's actual bucket-size rounding.
        using var writer = new BoundedBufferWriter(64, initialRentBytes: 4);
        _ = writer.GetSpan(64);
        writer.Reset(4);
        _ = writer.GetSpan(4);

        _ = Should.Throw<InvalidOperationException>(() => writer.Advance(5));
    }

    /// <summary>Verifies Reset clears written bytes without disturbing the active limit.</summary>
    [Fact]
    public void Reset_WhenCalled_ClearsWrittenBytesAndKeepsLimit()
    {
        using var writer = new BoundedBufferWriter(16, 4);
        "ab"u8.CopyTo(writer.GetSpan(2));
        writer.Advance(2);

        writer.Reset();

        writer.WrittenCount.ShouldBe(0);
        writer.GetSpan(16).Length.ShouldBeGreaterThanOrEqualTo(16);
    }

    /// <summary>Verifies Reset(limit) clears written bytes and narrows the active budget.</summary>
    [Fact]
    public void ResetWithLimit_WhenWithinMaximum_NarrowsActiveBudget()
    {
        using var writer = new BoundedBufferWriter(16, 4);
        "ab"u8.CopyTo(writer.GetSpan(2));
        writer.Advance(2);

        writer.Reset(3);

        writer.WrittenCount.ShouldBe(0);
        _ = Should.Throw<InvalidOperationException>(() => writer.GetSpan(4));
        writer.GetSpan(3).Length.ShouldBeGreaterThanOrEqualTo(3);
    }

    /// <summary>Verifies Reset(limit) rejects a limit beyond the configured maximum.</summary>
    [Fact]
    public void ResetWithLimit_WhenLimitExceedsMaximum_Throws()
    {
        using var writer = new BoundedBufferWriter(8, 4);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => writer.Reset(9));
    }

    /// <summary>Verifies Reset(limit) rejects a negative limit.</summary>
    [Fact]
    public void ResetWithLimit_WhenLimitIsNegative_Throws()
    {
        using var writer = new BoundedBufferWriter(8, 4);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => writer.Reset(-1));
    }

    /// <summary>Verifies a narrowed limit widens back out after a subsequent Reset(limit) with a larger value.</summary>
    [Fact]
    public void ResetWithLimit_WhenCalledAgainWithALargerLimit_WidensActiveBudget()
    {
        using var writer = new BoundedBufferWriter(16, 4);
        writer.Reset(2);
        writer.Reset(16);

        writer.GetSpan(16).Length.ShouldBeGreaterThanOrEqualTo(16);
    }

    /// <summary>Verifies Prepend inserts bytes before the existing written content.</summary>
    [Fact]
    public void Prepend_WhenCalled_InsertsBytesBeforeExistingContent()
    {
        using var writer = new BoundedBufferWriter(16, 4);
        "cd"u8.CopyTo(writer.GetSpan(2));
        writer.Advance(2);

        writer.Prepend("ab"u8);

        writer.WrittenSpan.ToArray().ShouldBe("abcd"u8.ToArray());
    }

    /// <summary>Verifies Prepend cannot push the batch past the active byte budget.</summary>
    [Fact]
    public void Prepend_WhenResultWouldExceedActiveLimit_Throws()
    {
        using var writer = new BoundedBufferWriter(4, 4);
        "ab"u8.CopyTo(writer.GetSpan(2));
        writer.Advance(2);

        _ = Should.Throw<InvalidOperationException>(() => writer.Prepend("xyz"u8));
    }

    /// <summary>Verifies every public member throws ObjectDisposedException, not a bogus error, once disposed.</summary>
    [Fact]
    public void PublicMembers_WhenWriterIsDisposed_ThrowObjectDisposedException()
    {
        var writer = new BoundedBufferWriter(16, 4);
        writer.Dispose();

        _ = Should.Throw<ObjectDisposedException>(() => _ = writer.WrittenCount);
        _ = Should.Throw<ObjectDisposedException>(() => _ = writer.WrittenSpan);
        _ = Should.Throw<ObjectDisposedException>(() => _ = writer.WrittenMemory);
        _ = Should.Throw<ObjectDisposedException>(() => writer.Advance(1));
        _ = Should.Throw<ObjectDisposedException>(() => writer.GetSpan());
        _ = Should.Throw<ObjectDisposedException>(() => writer.GetMemory());
        _ = Should.Throw<ObjectDisposedException>(writer.Reset);
        _ = Should.Throw<ObjectDisposedException>(() => writer.Reset(1));
        _ = Should.Throw<ObjectDisposedException>(() => writer.Prepend("a"u8));
    }

    /// <summary>Verifies disposing an already-disposed writer is a quiet no-op.</summary>
    [Fact]
    public void Dispose_WhenCalledAgain_IsQuiet()
    {
        var writer = new BoundedBufferWriter(16, 4);
        writer.Dispose();

        Should.NotThrow(writer.Dispose);
    }
}
