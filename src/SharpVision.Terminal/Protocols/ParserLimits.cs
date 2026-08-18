// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Protocols;

/// <summary>Defines finite limits for control-sequence parsing.</summary>
/// <remarks>
/// Instances are immutable after construction. Use a <see langword="with"/>
/// expression to derive a stricter or more permissive profile. Every byte
/// limit must remain positive; boundedness cannot be disabled.
/// </remarks>
/// <example>
/// <code>
/// var limits = ParserLimits.Default with { MaxStringBytes = 64 * 1024 };
/// </code>
/// </example>
[PublicAPI]
public sealed record ParserLimits
{
    /// <summary>Gets the conservative limits used when no profile is supplied.</summary>
    public static ParserLimits Default { get; } = new();

    /// <summary>Gets the maximum retained CSI or DCS parameter bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxParameterBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxParameterBytes));
    } = 256;

    /// <summary>Gets the maximum retained ESC, CSI, or DCS intermediate bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxIntermediateBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxIntermediateBytes));
    } = 16;

    /// <summary>Gets the maximum retained OSC, DCS, APC, PM, or SOS payload bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
    public int MaxStringBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxStringBytes));
    } = 1_048_576;

    /// <summary>Gets whether BEL may terminate an incoming OSC string.</summary>
    public bool AcceptBellTerminatedOsc { get; init; } = true;

    /// <summary>Gets whether raw C1 bytes are controls rather than UTF-8 data.</summary>
    /// <remarks>
    /// The default is <see langword="false"/> so a UTF-8 continuation byte is
    /// never reinterpreted as a C1 introducer.
    /// </remarks>
    public bool AcceptEightBitControls { get; init; }

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
