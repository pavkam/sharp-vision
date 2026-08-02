// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using SharpVision.Terminal.Clipboard;

/// <summary>Verifies the UTF-8 well-formedness check shared by the clipboard protocol encoders.</summary>
public sealed class Utf8ValidationTests
{
    /// <summary>Verifies an empty span is valid.</summary>
    [Fact]
    public void IsValid_WhenSpanIsEmpty_ReturnsTrue() => ReadOnlySpan<byte>.Empty.IsValid().ShouldBeTrue();

    /// <summary>Verifies plain ASCII bytes are valid.</summary>
    [Fact]
    public void IsValid_WhenBytesAreAscii_ReturnsTrue() => "hello"u8.IsValid().ShouldBeTrue();

    /// <summary>Verifies well-formed multi-byte sequences, including one spanning a surrogate pair, are valid.</summary>
    [Fact]
    public void IsValid_WhenBytesAreWellFormedMultiByte_ReturnsTrue() =>
        "café 👩‍💻"u8.IsValid().ShouldBeTrue();

    /// <summary>Verifies a lone continuation byte is rejected.</summary>
    [Fact]
    public void IsValid_WhenBytesContainALoneContinuationByte_ReturnsFalse() =>
        ((ReadOnlySpan<byte>) [0x80]).IsValid().ShouldBeFalse();

    /// <summary>Verifies a truncated multi-byte sequence is rejected.</summary>
    [Fact]
    public void IsValid_WhenBytesEndInATruncatedSequence_ReturnsFalse() =>
        ((ReadOnlySpan<byte>) [0xE2, 0x82]).IsValid().ShouldBeFalse();

    /// <summary>Verifies the invalid byte 0xFF is rejected.</summary>
    [Fact]
    public void IsValid_WhenBytesContainAnInvalidLeadByte_ReturnsFalse() =>
        ((ReadOnlySpan<byte>) [0xFF]).IsValid().ShouldBeFalse();
}
