// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo.Ncurses;

/// <summary>Discovers a current ncurses or split terminfo library without a package-time dependency.</summary>
internal static class Library
{
    /// <summary>Attempts each candidate library name in order.</summary>
    /// <param name="names">The non-null ordered candidate names or absolute paths.</param>
    /// <returns>An owned native boundary, or <see langword="null"/> when no compatible export set exists.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="names"/> is <see langword="null"/>.</exception>
    public static INative? Open(IReadOnlyList<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (OperatingSystem.IsWindows())
        {
            return null;
        }

        foreach (var name in names)
        {
            if (!NativeLibrary.TryLoad(name, out var handle))
            {
                continue;
            }

            try
            {
                return new NcursesBinding(handle);
            }
            catch (EntryPointNotFoundException)
            {
                NativeLibrary.Free(handle);
            }
        }

        return null;
    }
}
