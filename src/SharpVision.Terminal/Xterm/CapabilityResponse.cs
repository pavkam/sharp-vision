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

        if (isValid == (items.Count == 0))
        {
            throw new ArgumentException(
                "A successful XTGETTCAP response requires values and a failed response requires none.",
                nameof(items));
        }

        IsValid = isValid;
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
    public bool IsValid { get; }

    /// <summary>Gets the approved owned name/value pairs.</summary>
    public IReadOnlyDictionary<CapabilityName, ReadOnlyMemory<byte>> Items { get; }
}
