// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Runtime;

/// <summary>Owns one exact terminal-mode enable and disable byte pair.</summary>
internal readonly struct Lease
{
    private readonly byte[]? _enable;
    private readonly byte[]? _disable;

    /// <summary>Initializes an owned non-empty exact byte pair.</summary>
    /// <param name="enable">The exact bytes that attempt to acquire the mode.</param>
    /// <param name="disable">The exact bytes that restore the mode.</param>
    /// <exception cref="ArgumentException">Either command is empty.</exception>
    public Lease(ReadOnlySpan<byte> enable, ReadOnlySpan<byte> disable)
    {
        if (enable.IsEmpty)
        {
            throw new ArgumentException("A terminal-mode enable command cannot be empty.", nameof(enable));
        }

        if (disable.IsEmpty)
        {
            throw new ArgumentException("A terminal-mode disable command cannot be empty.", nameof(disable));
        }

        _enable = enable.ToArray();
        _disable = disable.ToArray();
    }

    /// <summary>Gets the owned exact enable bytes.</summary>
    public ReadOnlyMemory<byte> Enable => _enable ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Gets the owned exact reverse-cleanup bytes.</summary>
    public ReadOnlyMemory<byte> Disable => _disable ?? ReadOnlyMemory<byte>.Empty;
}
