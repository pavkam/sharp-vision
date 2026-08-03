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
}
