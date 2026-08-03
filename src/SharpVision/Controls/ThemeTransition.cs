// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Retains prospective Theme metadata until atomic subtree snapshots are finalized.</summary>
internal readonly struct ThemeTransition
{
    /// <summary>Initializes one committed Theme transition without appearance snapshots.</summary>
    /// <param name="control">The non-null control whose inherited Theme changed.</param>
    /// <param name="previousTheme">The previously inherited Theme, or null.</param>
    /// <param name="currentTheme">The currently inherited Theme, or null.</param>
    /// <param name="resolvedStylePropertyNames">The exact changed resolved-style properties.</param>
    /// <param name="impact">The prevalidated strongest invalidation required by every visual state.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    internal ThemeTransition(
        ControlBase control,
        Theme? previousTheme,
        Theme? currentTheme,
        IReadOnlyList<string> resolvedStylePropertyNames,
        InvalidationImpact impact)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(resolvedStylePropertyNames);

        Control = control;
        PreviousTheme = previousTheme;
        CurrentTheme = currentTheme;
        ResolvedStylePropertyNames = resolvedStylePropertyNames;
        Impact = impact;
    }

    /// <summary>Gets the control whose inherited Theme changed.</summary>
    internal ControlBase Control { get; }

    /// <summary>Gets the previously inherited Theme, or null.</summary>
    internal Theme? PreviousTheme { get; }

    /// <summary>Gets the currently inherited Theme, or null.</summary>
    internal Theme? CurrentTheme { get; }

    /// <summary>Gets the exact changed resolved-style property names.</summary>
    internal IReadOnlyList<string> ResolvedStylePropertyNames { get; }

    /// <summary>Gets the prevalidated strongest invalidation required by every visual state.</summary>
    internal InvalidationImpact Impact { get; }
}
