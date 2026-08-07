// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty;

/// <summary>Defines finite limits for one delimited Kitty protocol metadata section.</summary>
/// <remarks>
/// Shared by <see cref="Graphics.KittyGraphicsResponse.Parse"/> (the graphics APC payload) and
/// <see cref="Clipboard.KittyClipboardPacket.Parse"/> (the OSC 5522 metadata section) - both are the same
/// bounded, size-limited, textual Kitty metadata shape, independent of the clipboard-only decoded
/// payload size that <see cref="Terminal.Clipboard.TransferLimits.MaxClipboardBytes"/> bounds.
/// Instances are immutable after construction. Use a <see langword="with"/>
/// expression to derive a stricter or more permissive profile. The limit
/// must remain positive; boundedness cannot be disabled.
/// </remarks>
/// <example>
/// <code>
/// var limits = KittyMetadataLimits.Default with { MaxMetadataBytes = 16_384 };
/// </code>
/// </example>
[PublicAPI]
public sealed record KittyMetadataLimits
{
    /// <summary>Gets the conservative limits used when no profile is supplied.</summary>
    public static KittyMetadataLimits Default { get; } = new();

    /// <summary>Gets the maximum metadata bytes accepted in one Kitty protocol message.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxMetadataBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxMetadataBytes));
    } = 8_192;

    private static int RequirePositive(int value, string parameterName)
    {
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The limit must be positive.");
    }
}
