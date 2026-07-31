// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Verifies ncurses native-library discovery attempts a caller-supplied name list.</summary>
public sealed class LibraryTests
{
    /// <summary>Verifies a null name list is rejected before any platform check.</summary>
    [Fact]
    public void Open_WhenNamesIsNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => Library.Open(null!));

    /// <summary>Verifies an empty list never resolves a library, matching the documented
    /// "empty means ncurses is never attempted" contract on <c>Limits.NcursesLibraryNames</c>.</summary>
    [Fact]
    public void Open_WhenNamesIsEmpty_ReturnsNull()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Library.Open([]).ShouldBeNull();
    }

    /// <summary>Verifies names that resolve to no installed library are exhausted without throwing,
    /// so a caller-supplied override that doesn't match this machine degrades safely.</summary>
    [Fact]
    public void Open_WhenNoNameResolves_ReturnsNull()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Library.Open(["definitely-not-a-real-library.so.999"]).ShouldBeNull();
    }

    /// <summary>Verifies Windows never attempts native discovery regardless of the configured names.</summary>
    [Fact]
    public void Open_OnWindows_ReturnsNullWithoutAttemptingAnyName()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Library.Open(["libncursesw.so.6"]).ShouldBeNull();
    }
}
