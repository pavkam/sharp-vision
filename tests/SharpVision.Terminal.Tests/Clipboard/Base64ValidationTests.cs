// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Clipboard;

using System.Buffers.Text;

using SharpVision.Terminal.Clipboard;

/// <summary>Verifies the canonical Base64 checks and bounded encoding shared by the clipboard and Kitty graphics encoders.</summary>
public sealed class Base64ValidationTests
{
    private static readonly byte[] _source = "sharp vision"u8.ToArray();

    /// <summary>Verifies a fitting destination receives the exact encoded length and bytes.</summary>
    [Fact]
    public void EncodeBase64OrThrow_WhenDestinationFits_ReturnsExactLengthAndBytes()
    {
        var destination = new byte[Base64.GetMaxEncodedToUtf8Length(_source.Length)];

        var written = ((ReadOnlySpan<byte>) _source).EncodeBase64OrThrow(destination, "unreachable");

        written.ShouldBe(destination.Length);
        Encoding.UTF8.GetString(destination.AsSpan(0, written)).ShouldBe(Convert.ToBase64String(_source));
    }

    /// <summary>Verifies an undersized destination throws with the exact supplied message.</summary>
    [Fact]
    public void EncodeBase64OrThrow_WhenDestinationIsTooSmall_ThrowsWithSuppliedMessage()
    {
        var destination = new byte[Base64.GetMaxEncodedToUtf8Length(_source.Length) - 1];

        Should.Throw<InvalidOperationException>(() => ((ReadOnlySpan<byte>) _source).EncodeBase64OrThrow(destination, "boom"))
            .Message.ShouldBe("boom");
    }

    /// <summary>Verifies an empty source returns zero without throwing.</summary>
    [Fact]
    public void EncodeBase64OrThrow_WhenSourceIsEmpty_ReturnsZero() =>
        ReadOnlySpan<byte>.Empty.EncodeBase64OrThrow([], "unreachable").ShouldBe(0);

    /// <summary>Verifies a canonical, unpadded quartet is accepted.</summary>
    [Fact]
    public void IsCanonicalBase64_WhenQuartetIsCanonical_ReturnsTrue() => "c2hhcnA="u8.IsCanonicalBase64().ShouldBeTrue();

    /// <summary>Verifies a span whose length is not a multiple of four is rejected.</summary>
    [Fact]
    public void IsCanonicalBase64_WhenLengthIsNotAMultipleOfFour_ReturnsFalse() => "abc"u8.IsCanonicalBase64().ShouldBeFalse();
}
