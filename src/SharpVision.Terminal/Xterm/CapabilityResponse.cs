// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Xterm;

/// <summary>Owns one bounded XTGETTCAP reply without exposing arbitrary terminal resources.</summary>
[PublicAPI]
public sealed class CapabilityResponse
{
    /// <summary>Initializes one validated immutable response.</summary>
    /// <param name="isValid">Whether the terminal accepted the request.</param>
    /// <param name="items">The finite approved values.</param>
    /// <exception cref="ArgumentNullException"><paramref name="items"/> is null.</exception>
    internal CapabilityResponse(bool isValid, IDictionary<CapabilityName, byte[]> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        // A failed response may still echo the unsupported capability names it was asked for
        // so only a successful response requires at least one value; a failed one
        // may carry zero or more echoed names.
        if (isValid && items.Count == 0)
        {
            throw new ArgumentException(
                "A successful XTGETTCAP response requires values.",
                nameof(items));
        }

        Valid = isValid;
        var copy = new Dictionary<CapabilityName, ReadOnlyMemory<byte>>();

        foreach (var pair in items)
        {
            if (!Enum.IsDefined(pair.Key))
            {
                throw new ArgumentOutOfRangeException(nameof(items), pair.Key, "A capability name is undefined.");
            }

            ArgumentNullException.ThrowIfNull(pair.Value);
            copy.Add(pair.Key, pair.Value.ToArray());
        }

        Items = new ReadOnlyDictionary<CapabilityName, ReadOnlyMemory<byte>>(copy);
    }

    /// <summary>Gets whether the terminal accepted the request.</summary>
    public bool Valid { get; }

    /// <summary>Gets the approved owned name/value pairs.</summary>
    public IReadOnlyDictionary<CapabilityName, ReadOnlyMemory<byte>> Items { get; }
}
