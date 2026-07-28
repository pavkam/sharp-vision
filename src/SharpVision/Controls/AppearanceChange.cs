// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Retains one exact appearance publication with optional committed Theme metadata.</summary>
internal readonly struct AppearanceChange
{
    /// <summary>Initializes one staged appearance publication.</summary>
    /// <param name="control">The non-null control whose concrete appearance or Theme changed.</param>
    /// <param name="themeTransition">The committed Theme metadata, or null for appearance-only publication.</param>
    /// <param name="previousAppearance">The resolved appearance before the complete transaction.</param>
    /// <param name="currentAppearance">The resolved appearance after the complete transaction.</param>
    /// <exception cref="ArgumentNullException"><paramref name="control"/> is null.</exception>
    internal AppearanceChange(
        Control control,
        ThemeTransition? themeTransition,
        ResolvedAppearance previousAppearance,
        ResolvedAppearance currentAppearance)
    {
        ArgumentNullException.ThrowIfNull(control);

        Control = control;
        ThemeTransition = themeTransition;
        PreviousAppearance = previousAppearance;
        CurrentAppearance = currentAppearance;
    }

    /// <summary>Creates parent-first publications and merges exact snapshot-derived invalidation.</summary>
    /// <param name="transitions">The non-null committed Theme transitions.</param>
    /// <param name="previous">The non-null parent-first snapshots captured before any mutation.</param>
    /// <param name="current">The non-null snapshots captured after every mutation.</param>
    /// <returns>Exact publications in parent-first snapshot order.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">More than one Theme transition targets the same control.</exception>
    /// <exception cref="KeyNotFoundException">A captured control is absent from the current snapshot map.</exception>
    internal static List<AppearanceChange> CreateChanges(
        List<ThemeTransition> transitions,
        Dictionary<Control, AppearanceSnapshot> previous,
        Dictionary<Control, AppearanceSnapshot> current)
    {
        ArgumentNullException.ThrowIfNull(transitions);
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(current);
        var transitionsByControl = new Dictionary<Control, ThemeTransition>(transitions.Count);

        foreach (var transition in transitions)
        {
            transitionsByControl.Add(transition.Control, transition);
        }

        var changes = new List<AppearanceChange>(previous.Count);
        foreach (var entry in previous)
        {
            var control = entry.Key;
            var previousActual = entry.Value.Actual;
            var currentActual = current[control].Actual;
            var impact = control.GetAppearanceChangeImpact(previousActual, currentActual);
            var hasThemeTransition = transitionsByControl.TryGetValue(control, out var transition);

            if (!hasThemeTransition && impact == InvalidationImpact.None)
            {
                continue;
            }

            if (impact != InvalidationImpact.None)
            {
                // A prospective all-state impact remains authoritative when stronger. Internal
                // invalidation is monotonic, so the exact active snapshot may only strengthen it.
                control.Invalidate(Control.InvalidationFor(impact));
            }

            changes.Add(new AppearanceChange(
                control,
                hasThemeTransition ? transition : null,
                previousActual,
                currentActual));
        }

        return changes;
    }

    /// <summary>Gets the control receiving this publication.</summary>
    internal Control Control { get; }

    /// <summary>Gets committed Theme metadata, or null for an appearance-only publication.</summary>
    internal ThemeTransition? ThemeTransition { get; }

    /// <summary>Gets the exact resolved appearance before the complete transaction.</summary>
    internal ResolvedAppearance PreviousAppearance { get; }

    /// <summary>Gets the exact resolved appearance after the complete transaction.</summary>
    internal ResolvedAppearance CurrentAppearance { get; }
}
