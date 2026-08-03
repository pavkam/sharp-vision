// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Defines finite limits for loading and validating one terminal description.</summary>
/// <remarks>
/// Instances are immutable after construction. Use a <see langword="with"/>
/// expression to derive a stricter or more permissive profile. Every byte or
/// count limit must remain positive and within its documented bound;
/// boundedness cannot be disabled.
/// </remarks>
/// <example>
/// <code>
/// var limits = DescriptionLimits.Default with { MaxDescriptionSnapshotBytes = 512 * 1024 };
/// </code>
/// </example>
[PublicAPI]
public sealed record DescriptionLimits
{
    // Declared before Default: static field initializers run in textual declaration order, and
    // Default's own initializer constructs an instance whose NcursesLibraryNames property
    // initializer reads this field. Had this stayed below Default, it would still be null (its
    // own initializer not yet run) at the moment Default's constructor runs.
    //
    // The Linux sonames and macOS dylib names are platform-exclusive: a Mach-O binary can never
    // resolve an ELF soname and vice versa. Putting the current platform's names first lets
    // NativeLibrary.TryLoad succeed on its first or second attempt instead of exhausting every
    // candidate for the other platform first (see #246). The relative order within each platform
    // group is unchanged, so libncurses.dylib still precedes the Homebrew absolute paths.
    private static readonly IReadOnlyList<string> _defaultNcursesLibraryNames =
        OperatingSystem.IsMacOS()
            ?
            [
                "libncursesw.6.dylib",
                "libncurses.6.dylib",
                "libncurses.dylib",
                "/opt/homebrew/opt/ncurses/lib/libncursesw.6.dylib",
                "/usr/local/opt/ncurses/lib/libncursesw.6.dylib",
                "libncursesw.so.6",
                "libncurses.so.6",
                "libtinfo.so.6",
                "libncursesw.so",
                "libncurses.so",
                "libtinfo.so"
            ]
            :
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

    /// <summary>Gets the conservative limits used when no profile is supplied.</summary>
    public static DescriptionLimits Default { get; } = new();

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

    /// <summary>Gets the maximum UTF-8 bytes accepted in one legacy termcap environment variable.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not positive or exceeds 4,096.</exception>
    public int MaxTermcapVariableBytes
    {
        get;
        init => field = RequireBoundedPositive(value, 4_096, nameof(MaxTermcapVariableBytes));
    } = 1_023;

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
    /// <exception cref="ArgumentNullException">The value or one of its entries is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An entry is blank.</exception>
    public IReadOnlyList<string> NcursesLibraryNames
    {
        get;
        init => field = Snapshot(value, nameof(NcursesLibraryNames));
    } = _defaultNcursesLibraryNames;

    private static ReadOnlyCollection<string> Snapshot(IReadOnlyList<string> value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        var copy = new string[value.Count];

        for (var index = 0; index < value.Count; index++)
        {
            var candidate = value[index];
            ArgumentNullException.ThrowIfNull(candidate, parameterName);

            if (string.IsNullOrWhiteSpace(candidate))
            {
                throw new ArgumentException("Native-library names must not be blank.", parameterName);
            }

            copy[index] = candidate;
        }

        return Array.AsReadOnly(copy);
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
}
