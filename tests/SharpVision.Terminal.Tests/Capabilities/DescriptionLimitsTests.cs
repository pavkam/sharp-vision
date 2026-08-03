// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>
/// Verifies the finite terminal-description lookup limit contract.
/// </summary>
public sealed class DescriptionLimitsTests
{
    /// <summary>
    /// Verifies that every integer limit rejects zero.
    /// </summary>
    [Fact]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeException()
    {
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new DescriptionLimits { MaxTerminalNameBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new DescriptionLimits { MaxDescriptionPathEntries = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new DescriptionLimits { MaxDescriptionPathBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new DescriptionLimits { MaxDescriptionSnapshotBytes = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new DescriptionLimits { MaxDescriptionKeyBindings = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () =>
            new DescriptionLimits { MaxDescriptionRgbComponentBits = 0 });
        _ = Should.Throw<ArgumentOutOfRangeException>(static () => new DescriptionLimits { MaxTermcapVariableBytes = 0 });
    }

    /// <summary>Verifies NcursesLibraryNames rejects null and accepts a full override, so a caller
    /// can replace the built-in search list rather than being stuck with a hardcoded array
    /// (see #98).</summary>
    [Fact]
    public void NcursesLibraryNames_WhenConfigured_RejectsNullAndReplacesTheDefaultList()
    {
        _ = Should.Throw<ArgumentNullException>(static () => new DescriptionLimits { NcursesLibraryNames = null! });

        var limits = DescriptionLimits.Default with { NcursesLibraryNames = ["/opt/custom/libncursesw.so"] };

        limits.NcursesLibraryNames.ShouldBe(["/opt/custom/libncursesw.so"]);
    }

    /// <summary>Verifies later caller mutations cannot rewrite an immutable limit profile.</summary>
    [Fact]
    public void NcursesLibraryNames_WhenSourceChanges_PreservesOwnedSnapshot()
    {
        string[] names = ["original-ncurses.so"];
        var limits = DescriptionLimits.Default with { NcursesLibraryNames = names };

        names[0] = "rewritten-ncurses.so";

        limits.NcursesLibraryNames.ShouldBe(["original-ncurses.so"]);
    }

    /// <summary>Verifies invalid native-library candidates are rejected at configuration time.</summary>
    [Fact]
    public void NcursesLibraryNames_WhenEntryIsInvalid_ThrowsBeforePublishingProfile()
    {
        _ = Should.Throw<ArgumentNullException>(() =>
            new DescriptionLimits { NcursesLibraryNames = [null!] });
        _ = Should.Throw<ArgumentException>(() =>
            new DescriptionLimits { NcursesLibraryNames = [" \t"] });
    }

    /// <summary>
    /// Verifies that the default profile is bounded and retains a non-empty native search list.
    /// </summary>
    [Fact]
    public void Default_WhenRead_HasFiniteInteractiveBounds()
    {
        var limits = DescriptionLimits.Default;

        limits.MaxTerminalNameBytes.ShouldBeInRange(1, 1_024);
        limits.MaxDescriptionPathEntries.ShouldBeInRange(1, 256);
        limits.MaxDescriptionPathBytes.ShouldBeInRange(1, 32_768);
        limits.MaxDescriptionSnapshotBytes.ShouldBeInRange(1, 16_777_216);
        limits.MaxDescriptionKeyBindings.ShouldBeInRange(1, 1_024);
        limits.MaxDescriptionRgbComponentBits.ShouldBeInRange(1, 63);
        limits.MaxTermcapVariableBytes.ShouldBeInRange(1, 4_096);
        limits.NcursesLibraryNames.ShouldNotBeEmpty();
    }

    /// <summary>
    /// Verifies the default candidate order tries the current platform's own library naming
    /// convention first, so the loop in <c>NcursesLibrary.Open</c> does not exhaust every
    /// candidate for the other platform before reaching a name that can ever resolve (see #246).
    /// </summary>
    [Fact]
    public void Default_WhenPlatformIsMacOS_TriesDylibNamesBeforeSonamesAndHomebrewLast()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var names = DescriptionLimits.Default.NcursesLibraryNames;
        var lastDylibIndex = -1;
        var firstSoIndex = int.MaxValue;

        for (var index = 0; index < names.Count; index++)
        {
            if (names[index].Contains(".dylib", StringComparison.Ordinal))
            {
                lastDylibIndex = index;
            }
            else if (firstSoIndex == int.MaxValue && names[index].Contains(".so", StringComparison.Ordinal))
            {
                firstSoIndex = index;
            }
        }

        lastDylibIndex.ShouldBeLessThan(firstSoIndex);

        var pinnedIndex = names.ToList().IndexOf("libncurses.dylib");
        var homebrewIndex = names.ToList()
            .IndexOf("/opt/homebrew/opt/ncurses/lib/libncursesw.6.dylib");

        pinnedIndex.ShouldBeLessThan(homebrewIndex);
    }

    /// <summary>
    /// Verifies the default candidate order tries Linux sonames before macOS dylib names, the
    /// mirror of the macOS assertion above (see #246).
    /// </summary>
    [Fact]
    public void Default_WhenPlatformIsLinux_TriesSonamesBeforeDylibNames()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var names = DescriptionLimits.Default.NcursesLibraryNames;
        var lastSoIndex = -1;
        var firstDylibIndex = int.MaxValue;

        for (var index = 0; index < names.Count; index++)
        {
            if (names[index].Contains(".dylib", StringComparison.Ordinal))
            {
                firstDylibIndex = Math.Min(firstDylibIndex, index);
            }
            else if (names[index].Contains(".so", StringComparison.Ordinal))
            {
                lastSoIndex = index;
            }
        }

        lastSoIndex.ShouldBeLessThan(firstDylibIndex);
    }

    /// <summary>
    /// Verifies an explicit override is used verbatim in the caller's order with no
    /// platform-based reordering applied, preserving the #98 override contract (see #246).
    /// </summary>
    [Fact]
    public void NcursesLibraryNames_WhenOverridden_IsUsedUnfilteredOnEveryPlatform()
    {
        string[] names = ["libncurses.dylib", "libncursesw.so.6", "libtinfo.so"];
        var limits = DescriptionLimits.Default with { NcursesLibraryNames = names };

        limits.NcursesLibraryNames.ShouldBe(names);
    }
}
