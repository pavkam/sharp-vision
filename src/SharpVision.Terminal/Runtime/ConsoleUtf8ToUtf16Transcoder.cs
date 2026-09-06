// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>
/// Transcodes UTF-8 bytes destined for a Windows console into UTF-16 code units, carrying a
/// multi-byte sequence split across writes over to the next call.
/// </summary>
/// <remarks>
/// This type has no dependency on Win32 or <see cref="OperatingSystem.IsWindows"/>, so it can be
/// exercised directly by tests on every platform even though its only production caller,
/// <see cref="WindowsConsoleOutputStream"/>, is Windows-only. The renderer writes complete frames
/// through <see cref="StreamTransport"/>'s serialized write path, but nothing above this layer
/// guarantees a UTF-8 multi-byte sequence never straddles two consecutive <c>Write</c> calls, so
/// the stateful <see cref="Decoder"/> is what actually protects against a torn character at a
/// write boundary.
/// </remarks>
internal sealed class ConsoleUtf8ToUtf16Transcoder
{
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private char[] _buffer = [];

    /// <summary>Transcodes UTF-8 bytes into the code units they decode to, so far.</summary>
    /// <param name="bytes">The bytes to transcode.</param>
    /// <param name="flush">
    /// Whether this is the final call for the output's lifetime, so a leftover incomplete
    /// multi-byte sequence is replaced with U+FFFD instead of being held for continuation bytes
    /// that will never arrive.
    /// </param>
    /// <returns>
    /// The code units decoded from <paramref name="bytes"/> and any bytes carried over from a
    /// previous call. A sequence <paramref name="bytes"/> ends with that is itself incomplete
    /// contributes no code units here and is instead held for the next call. The returned span is
    /// only valid until the next call to this method.
    /// </returns>
    public ReadOnlySpan<char> Transcode(ReadOnlySpan<byte> bytes, bool flush)
    {
        // GetCharCount and GetChars are called back to back with the same flush flag, which is
        // the documented safe pattern for sizing a stateful decoder's next output - the decoder is
        // not otherwise mutated by GetCharCount.
        var maxChars = _decoder.GetCharCount(bytes, flush);

        if (_buffer.Length < maxChars)
        {
            Array.Resize(ref _buffer, maxChars);
        }

        var written = _decoder.GetChars(bytes, _buffer, flush);

        return _buffer.AsSpan(0, written);
    }
}
