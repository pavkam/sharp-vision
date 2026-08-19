// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

/// <summary>Shares correlation-identifier validation common to the Kitty clipboard and query protocols.</summary>
/// <remarks>
/// A valid identifier is a non-empty run of <c>[a-zA-Z0-9\-_+.]</c>, matching the character set
/// the Kitty protocol specifies for correlation IDs. Empty input is rejected: a zero-length token
/// correlates nothing.
/// </remarks>
internal static class IdentifierValidation
{
    [SuppressMessage(
        "Style",
        "IDE0052:Remove unread private members",
        Justification = "Used only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static readonly SearchValues<byte> _validBytes =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_+."u8);

    [SuppressMessage(
        "Style",
        "IDE0052:Remove unread private members",
        Justification = "Used only from within extension(...) blocks; the analyzer doesn't track that usage yet.")]
    private static readonly SearchValues<char> _validChars =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_+.");

    extension(ReadOnlySpan<byte> value)
    {
        /// <summary>Reports whether the span is a non-empty valid Kitty correlation identifier.</summary>
        /// <returns>Whether every byte belongs to the identifier character set and the span is non-empty.</returns>
        [Pure]
        public bool IsIdentifier() => !value.IsEmpty && !value.ContainsAnyExcept(_validBytes);
    }

    extension(ReadOnlySpan<char> value)
    {
        /// <summary>Reports whether the span is a non-empty valid Kitty correlation identifier.</summary>
        /// <returns>Whether every character belongs to the identifier character set and the span is non-empty.</returns>
        [Pure]
        public bool IsIdentifier() => !value.IsEmpty && !value.ContainsAnyExcept(_validChars);
    }
}
