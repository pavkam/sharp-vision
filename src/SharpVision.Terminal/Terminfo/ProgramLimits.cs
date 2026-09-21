// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

using ValueRange = JetBrains.Annotations.ValueRangeAttribute;

/// <summary>Defines finite limits for compiling and interpreting terminfo parameter programs.</summary>
/// <remarks>
/// Instances are immutable after construction. Use a <see langword="with"/>
/// expression to derive a stricter or more permissive profile. Every limit
/// must remain positive and within its documented bound; boundedness cannot
/// be disabled.
/// </remarks>
/// <example>
/// <code>
/// var limits = ProgramLimits.Default with { MaxProgramStackDepth = 32 };
/// </code>
/// </example>
[PublicAPI]
public sealed record ProgramLimits
{
    /// <summary>Gets the conservative limits used when no profile is supplied.</summary>
    public static ProgramLimits Default { get; } = new();

    /// <summary>Gets the maximum raw bytes accepted in one terminfo parameter program.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 1 MiB.</exception>
    [ValueRange(1, 1_048_576)]
    public int MaxProgramBytes
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotPositiveOrGreaterThan(value, 1_048_576, nameof(MaxProgramBytes));
            field = value;
        }
    } = 65_536;

    /// <summary>Gets the maximum compiled operations retained for one terminfo program.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 16,384.</exception>
    [ValueRange(1, 16_384)]
    public int MaxProgramOperations
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotPositiveOrGreaterThan(value, 16_384, nameof(MaxProgramOperations));
            field = value;
        }
    } = 2_048;

    /// <summary>Gets the maximum evaluation-stack depth for one terminfo expansion.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 256.</exception>
    [ValueRange(1, 256)]
    public int MaxProgramStackDepth
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotPositiveOrGreaterThan(value, 256, nameof(MaxProgramStackDepth));
            field = value;
        }
    } = 64;

    /// <summary>Gets the maximum raw output bytes produced by one terminfo expansion.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 1 MiB.</exception>
    [ValueRange(1, 1_048_576)]
    public int MaxProgramOutputBytes
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotPositiveOrGreaterThan(value, 1_048_576, nameof(MaxProgramOutputBytes));
            field = value;
        }
    } = 65_536;

    /// <summary>Gets the maximum raw bytes accepted in one terminfo string parameter.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 1 MiB.</exception>
    [ValueRange(1, 1_048_576)]
    public int MaxStringParameterBytes
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNotPositiveOrGreaterThan(value, 1_048_576, nameof(MaxStringParameterBytes));
            field = value;
        }
    } = 65_536;
}
