// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Owns an immutable copy of one bracketed-paste UTF-8 payload.</summary>
[PublicAPI]
public readonly struct Paste
{
    /// <summary>Initializes a paste by copying caller-owned bytes.</summary>
    /// <param name="utf8">The exact UTF-8 payload to copy.</param>
    public Paste(ReadOnlySpan<byte> utf8) => Utf8 = utf8.ToArray();

    private Paste(byte[] owned) => Utf8 = owned;

    /// <summary>Gets immutable owned UTF-8 bytes.</summary>
    public ReadOnlyMemory<byte> Utf8 { get; }

    /// <summary>Transfers an already isolated array into a paste value.</summary>
    /// <param name="owned">The non-null isolated payload.</param>
    /// <returns>The paste owning <paramref name="owned"/>.</returns>
    internal static Paste Take(byte[] owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        return new Paste(owned);
    }
}
