// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>
/// Defines finite limits for protocol parsing, clipboard transactions, and
/// capability queries.
/// </summary>
/// <remarks>
/// Instances are immutable after construction. Use a <see langword="with"/>
/// expression to derive a stricter or more permissive profile. Every limit must
/// remain positive; boundedness cannot be disabled.
/// </remarks>
/// <example>
/// <code>
/// var limits = Limits.Default with { MaxStringBytes = 64 * 1024 };
/// </code>
/// </example>
[PublicAPI]
public sealed record Limits
{
    // Declared before Default: static field initializers run in textual declaration order, and
    // Default's own initializer constructs an instance whose NcursesLibraryNames property
    // initializer reads this field. Had this stayed below Default, it would still be null (its
    // own initializer not yet run) at the moment Default's constructor runs.
    private static readonly IReadOnlyList<string> _defaultNcursesLibraryNames =
    [
        "libncursesw.so.6",
        "libncurses.so.6",
        "libtinfo.so.6",
        "libncursesw.so",
        "libncurses.so",
        "libtinfo.so",
        "libncursesw.6.dylib",
        "libncurses.6.dylib",
        "libncurses.dylib",
        "/opt/homebrew/opt/ncurses/lib/libncursesw.6.dylib",
        "/usr/local/opt/ncurses/lib/libncursesw.6.dylib"
    ];

    /// <summary>
    /// Gets the conservative limits used when no profile is supplied.
    /// </summary>
    public static Limits Default { get; } = new();

    /// <summary>
    /// Gets the maximum retained CSI or DCS parameter bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxParameterBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxParameterBytes));
    } = 256;

    /// <summary>
    /// Gets the maximum retained ESC, CSI, or DCS intermediate bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxIntermediateBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxIntermediateBytes));
    } = 16;

    /// <summary>
    /// Gets the maximum retained OSC, DCS, APC, PM, or SOS payload bytes.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxStringBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxStringBytes));
    } = 1_048_576;

    /// <summary>
    /// Gets the maximum decoded clipboard bytes retained by one transaction.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxClipboardBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxClipboardBytes));
    } = 16_777_216;

    /// <summary>
    /// Gets the maximum OSC 5522 metadata bytes accepted in one packet.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxMetadataBytes
    {
        get;
        init => field = RequirePositive(value, nameof(MaxMetadataBytes));
    } = 8_192;

    /// <summary>
    /// Gets the maximum number of capability or clipboard queries in flight.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive.
    /// </exception>
    public int MaxConcurrentQueries
    {
        get;
        init => field = RequirePositive(value, nameof(MaxConcurrentQueries));
    } = 32;

    /// <summary>Gets the maximum raw bytes accepted in one terminfo parameter program.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 1 MiB.</exception>
    public int MaxProgramBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 1_048_576, nameof(MaxProgramBytes));
    } = 65_536;

    /// <summary>Gets the maximum compiled operations retained for one terminfo program.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 16,384.</exception>
    public int MaxProgramOperations
    {
        get;
        init => field = RequireBoundedPositive(value, 16_384, nameof(MaxProgramOperations));
    } = 2_048;

    /// <summary>Gets the maximum evaluation-stack depth for one terminfo expansion.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 256.</exception>
    public int MaxProgramStackDepth
    {
        get;
        init => field = RequireBoundedPositive(value, 256, nameof(MaxProgramStackDepth));
    } = 64;

    /// <summary>Gets the maximum raw output bytes produced by one terminfo expansion.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 1 MiB.</exception>
    public int MaxProgramOutputBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 1_048_576, nameof(MaxProgramOutputBytes));
    } = 65_536;

    /// <summary>Gets the maximum raw bytes accepted in one terminfo string parameter.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 1 MiB.</exception>
    public int MaxStringParameterBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 1_048_576, nameof(MaxStringParameterBytes));
    } = 65_536;

    /// <summary>Gets the maximum UTF-8 byte count of one terminal database name.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 1,024.</exception>
    public int MaxTerminalNameBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 1_024, nameof(MaxTerminalNameBytes));
    } = 128;

    /// <summary>Gets the maximum entries accepted in one terminal database path list.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 256.</exception>
    public int MaxDescriptionPathEntries
    {
        get;
        init => field = RequireBoundedPositive(value, 256, nameof(MaxDescriptionPathEntries));
    } = 64;

    /// <summary>Gets the maximum UTF-8 bytes accepted in one database path.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 32,768.</exception>
    public int MaxDescriptionPathBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 32_768, nameof(MaxDescriptionPathBytes));
    } = 4_096;

    /// <summary>Gets the maximum owned environment and accepted-description snapshot bytes.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 16 MiB.</exception>
    public int MaxDescriptionSnapshotBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 16_777_216, nameof(MaxDescriptionSnapshotBytes));
    } = 1_048_576;

    /// <summary>Gets the maximum exact key byte sequences retained from one description.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 1,024.</exception>
    public int MaxDescriptionKeyBindings
    {
        get;
        init => field = RequireBoundedPositive(value, 1_024, nameof(MaxDescriptionKeyBindings));
    } = 256;

    /// <summary>Gets the maximum bits accepted in one RGB component descriptor.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 63.</exception>
    public int MaxDescriptionRgbComponentBits
    {
        get;
        init => field = RequireBoundedPositive(value, 63, nameof(MaxDescriptionRgbComponentBits));
    } = 16;

    /// <summary>Gets the maximum name/value pairs accepted in one XTGETTCAP reply.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 256.</exception>
    public int MaxCapabilityItems
    {
        get;
        init => field = RequireBoundedPositive(value, 256, nameof(MaxCapabilityItems));
    } = 32;

    /// <summary>Gets the maximum decoded bytes accepted for one XTGETTCAP value.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 64 KiB.</exception>
    public int MaxCapabilityValueBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 65_536, nameof(MaxCapabilityValueBytes));
    } = 4_096;

    /// <summary>
    /// Gets the deadline applied to a terminal query before safe fallback.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is zero, negative, or infinite.
    /// </exception>
    public TimeSpan QueryTimeout
    {
        get;
        init => field = RequireFinitePositive(value, nameof(QueryTimeout));
    } = TimeSpan.FromMilliseconds(750);

    /// <summary>
    /// Gets whether BEL may terminate an incoming OSC string.
    /// </summary>
    public bool AcceptBellTerminatedOsc { get; init; } = true;

    /// <summary>
    /// Gets whether raw C1 bytes are controls rather than UTF-8 data.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/> so a UTF-8 continuation byte is
    /// never reinterpreted as a C1 introducer.
    /// </remarks>
    public bool AcceptEightBitControls { get; init; }

    /// <summary>
    /// Gets the ordered native library names or absolute paths attempted when opening ncurses on
    /// Unix platforms. The first name <see cref="NativeLibrary.TryLoad(string, out nint)"/>
    /// resolves is used; an empty list means ncurses is never attempted.
    /// </summary>
    /// <remarks>
    /// Defaults to the sonames and Homebrew install locations this library previously hardcoded.
    /// Overriding replaces the list entirely rather than appending to it, so a distribution with a
    /// nonstandard install path (or a minimal container without a package manager's default
    /// locations) can be supported without recompiling (see #98).
    /// </remarks>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public IReadOnlyList<string> NcursesLibraryNames
    {
        get;
        init => field = RequireNonNull(value, nameof(NcursesLibraryNames));
    } = _defaultNcursesLibraryNames;

    private static IReadOnlyList<string> RequireNonNull(IReadOnlyList<string> value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return value;
    }

    private static int RequirePositive(int value, string parameterName)
    {
        return value > 0
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The limit must be positive.");
    }

    private static int RequireBoundedPositive(int value, int maximum, string parameterName)
    {
        return value is > 0 && value <= maximum
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"The limit must be positive and no greater than {maximum}.");
    }

    private static TimeSpan RequireFinitePositive(TimeSpan value, string parameterName)
    {
        return value > TimeSpan.Zero && value != Timeout.InfiniteTimeSpan
            ? value
            : throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The query timeout must be finite and positive.");
    }
}
