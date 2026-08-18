// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Owns one bounded copy of a native terminal-description string result.</summary>
internal readonly struct NativeString
{
    private readonly byte[]? _bytes;

    private NativeString(NativeStringStatus status, ReadOnlySpan<byte> bytes)
    {
        Status = status;
        _bytes = bytes.ToArray();
    }

    /// <summary>Gets the stable absent result.</summary>
    public static NativeString Absent { get; } = new(NativeStringStatus.Absent, []);

    /// <summary>Gets the stable wrong-type result corresponding to <c>(char *)-1</c>.</summary>
    public static NativeString WrongType { get; } = new(NativeStringStatus.WrongType, []);

    /// <summary>Gets the stable unterminated/over-limit result.</summary>
    public static NativeString OverLimit { get; } = new(NativeStringStatus.OverLimit, []);

    /// <summary>Gets the native result classification.</summary>
    public NativeStringStatus Status { get; }

    /// <summary>Gets the owned exact bytes for a present value.</summary>
    public ReadOnlyMemory<byte> Bytes => _bytes ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Creates one present result by copying exact raw bytes.</summary>
    /// <param name="bytes">The exact bytes to own, including a present empty value.</param>
    /// <returns>The owned native string value.</returns>
    public static NativeString Present(ReadOnlySpan<byte> bytes) => new NativeString(NativeStringStatus.Present, bytes);
}
