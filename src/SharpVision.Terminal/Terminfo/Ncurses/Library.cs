// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Discovers a current ncurses or split terminfo library without a package-time dependency.</summary>
internal static class Library
{
    private static readonly string[] _names =
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

    /// <summary>Attempts each platform library name in deterministic order.</summary>
    /// <returns>An owned native boundary, or <see langword="null"/> when no compatible export set exists.</returns>
    public static INative? Open()
    {
        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        foreach (var name in _names)
        {
            if (!NativeLibrary.TryLoad(name, out var handle))
            {
                continue;
            }

            try
            {
                return new Native(handle);
            }
            catch (EntryPointNotFoundException)
            {
                NativeLibrary.Free(handle);
            }
        }

        return null;
    }
}
