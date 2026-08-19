// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Clipboard;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Defines finite limits for OSC 52 and Kitty OSC 5522 clipboard transfers.</summary>
/// <remarks>
/// Instances are immutable after construction. Use a <see langword="with"/>
/// expression to derive a stricter or more permissive profile. Every limit
/// must remain positive; boundedness cannot be disabled.
/// </remarks>
/// <example>
/// <code>
/// var limits = TransferLimits.Default with { MaxClipboardBytes = 4 * 1024 * 1024 };
/// </code>
/// </example>
[PublicAPI]
public sealed record TransferLimits
{
    /// <summary>Gets the conservative limits used when no profile is supplied.</summary>
    public static TransferLimits Default { get; } = new();

    /// <summary>Gets the maximum decoded clipboard bytes retained by one transaction.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    [NonNegativeValue]
    public int MaxClipboardBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxClipboardBytes));
    } = 16_777_216;

    /// <summary>Gets the maximum OSC 5522 metadata bytes accepted in one packet.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    [NonNegativeValue]
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
