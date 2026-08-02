// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Terminfo;

/// <summary>
/// Names the terminfo capability strings whose loss must fail loudly rather than silently
/// degrade a feature, so a typo in one consumption site cannot drift from the others (see #93).
/// </summary>
/// <remarks>
/// This is not the complete terminfo capability catalog -- <see cref="Ncurses.NcursesNames"/> already
/// owns that database-level list. This type names only the capabilities multiple consumption sites
/// independently re-type today, starting with the pairs <see cref="Programs.IsFullScreenReady"/>
/// and the enable/disable terminal-mode leases depend on.
/// </remarks>
internal static class CapabilityNames
{
    /// <summary>cup, absolute cursor positioning.</summary>
    public const string Cup = "cup";

    /// <summary>sgr0, reset all attributes.</summary>
    public const string Sgr0 = "sgr0";

    /// <summary>el, erase to end of line.</summary>
    public const string El = "el";

    /// <summary>ed, erase to end of display.</summary>
    public const string Ed = "ed";

    /// <summary>clear, clear the full screen.</summary>
    public const string Clear = "clear";

    /// <summary>civis, make the cursor invisible.</summary>
    public const string Civis = "civis";

    /// <summary>cnorm, restore the cursor to its normal visibility.</summary>
    public const string Cnorm = "cnorm";

    /// <summary>smcup, enter the alternate screen.</summary>
    public const string Smcup = "smcup";

    /// <summary>rmcup, leave the alternate screen.</summary>
    public const string Rmcup = "rmcup";

    /// <summary>smkx, enter application keypad mode.</summary>
    public const string Smkx = "smkx";

    /// <summary>rmkx, leave application keypad mode.</summary>
    public const string Rmkx = "rmkx";
}
