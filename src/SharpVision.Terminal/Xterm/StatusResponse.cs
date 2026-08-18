// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>Owns one bounded DECRQSS success or failure response.</summary>
[PublicAPI]
public readonly struct StatusResponse
{
    private readonly byte[]? _value;

    /// <summary>Initializes one validated status response.</summary>
    /// <param name="name">The recognized name, or unknown for an observable extension.</param>
    /// <param name="isValid">Whether the terminal accepted the request.</param>
    /// <param name="value">The borrowed returned CSI body without its introducer.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="name"/> is undefined.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="isValid"/> is false while <paramref name="name"/> is not
    /// <see cref="StatusName.Unknown"/> or <paramref name="value"/> is non-empty, or the returned
    /// CSI body does not match the declared status name.
    /// </exception>
    public StatusResponse(StatusName name, bool isValid, ReadOnlySpan<byte> value)
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(name, nameof(name), "The status name is unknown.");

        if (!isValid)
        {
            if (name != StatusName.Unknown || !value.IsEmpty)
            {
                throw new ArgumentException(
                    "A failed DECRQSS response has no returned selector or value.",
                    nameof(value));
            }

            Name = StatusName.Unknown;
            Valid = false;
            _value = [];
            return;
        }

        if (!XtermDecrqss.TryIdentify(value, out var identified) || identified != name)
        {
            throw new ArgumentException(
                "The returned CSI body does not match the declared DECRQSS status name.",
                nameof(value));
        }

        Name = name;
        Valid = true;
        _value = value.ToArray();
    }

    /// <summary>Gets the recognized status name.</summary>
    public StatusName Name { get; }

    /// <summary>Gets whether the terminal accepted the request.</summary>
    public bool Valid { get; }

    /// <summary>Gets the owned returned CSI body without its introducer.</summary>
    public ReadOnlyMemory<byte> Value => _value ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Gets whether this value is the default sentinel.</summary>
    public bool IsEmpty => _value is null;
}
