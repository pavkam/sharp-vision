// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Styling;

/// <summary>Retains cache-neutral actual and ambient appearance for one control tree state.</summary>
internal readonly struct AppearanceSnapshot
{
    /// <summary>Initializes one cache-neutral control appearance snapshot.</summary>
    /// <param name="parentAmbientFace">The explicit parent ambient face used for this resolution, or null.</param>
    /// <param name="actual">The exact resolved appearance for the control's active state.</param>
    /// <param name="ambientFace">The exact face contributed to transparent descendants.</param>
    internal AppearanceSnapshot(
        Face? parentAmbientFace,
        ResolvedAppearance actual,
        Face ambientFace)
    {
        ParentAmbientFace = parentAmbientFace;
        Actual = actual;
        AmbientFace = ambientFace;
    }

    /// <summary>Gets the explicit parent ambient face used for this resolution, or null.</summary>
    internal Face? ParentAmbientFace { get; }

    /// <summary>Gets the exact resolved appearance for the control's active state.</summary>
    internal ResolvedAppearance Actual { get; }

    /// <summary>Gets the exact face contributed to transparent descendants.</summary>
    internal Face AmbientFace { get; }

    /// <summary>Captures one subtree top-down without reading or populating appearance caches.</summary>
    /// <param name="root">The non-null subtree root.</param>
    /// <returns>Snapshots keyed by every control in the subtree.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    internal static Dictionary<ControlBase, AppearanceSnapshot> CaptureSubtree(ControlBase root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var snapshots = new Dictionary<ControlBase, AppearanceSnapshot>();
        CaptureSubtree(root, snapshots);
        return snapshots;
    }

    /// <summary>Appends one subtree's top-down cache-neutral snapshots to a transaction map,
    /// skipping any complete overlapping subtree already captured by the transaction.</summary>
    /// <param name="root">The non-null subtree root.</param>
    /// <param name="snapshots">The non-null destination, which may already contain complete overlapping subtrees.</param>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    internal static void CaptureSubtree(
        ControlBase root,
        Dictionary<ControlBase, AppearanceSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(snapshots);
        var stack = new Stack<(ControlBase Control, Face? ParentAmbient, bool ContinuousBackground)>();
        stack.Push((
            root,
            ResolveParentAmbient(root.Parent),
            ResolveContinuousBackground(root)));

        while (stack.TryPop(out var entry))
        {
            if (snapshots.ContainsKey(entry.Control))
            {
                continue;
            }

            var state = entry.Control.GetAppearanceState();
            var actual = entry.Control.ResolveSnapshot(
                state,
                entry.ParentAmbient,
                entry.ContinuousBackground);
            var ambientFace = entry.Control.AmbientAppearanceState == state
                ? actual.Face
                : entry.Control.ResolveSnapshot(
                    entry.Control.AmbientAppearanceState,
                    entry.ParentAmbient,
                    entry.ContinuousBackground).Face;
            snapshots.Add(
                entry.Control,
                new AppearanceSnapshot(entry.ParentAmbient, actual, ambientFace));

            for (var index = entry.Control.OwnedControlCount - 1; index >= 0; index--)
            {
                var child = entry.Control.OwnedControlAt(index);
                stack.Push((
                    child,
                    ambientFace,
                    ContinuesBackgroundPlane(
                        child,
                        entry.ContinuousBackground || entry.Control.ProvidesContinuousBackground)));
            }
        }
    }

    /// <summary>Resolves whether an external parent chain establishes a continuous background plane.</summary>
    /// <param name="control">The control whose plane membership is resolved.</param>
    /// <returns>True when <paramref name="control"/> must leave an ancestor's plane visible.</returns>
    /// <remarks>
    /// The walk stops at the first <see cref="ControlBase.IsAppearanceBoundary"/> control, including
    /// <paramref name="control"/> itself: a floating surface such as a submenu popup is logically
    /// owned by a menu item, so its <see cref="ControlBase.Parent"/> chain runs straight into the
    /// menu bar's plane, but visually it is a separate surface drawn over arbitrary content. Letting
    /// the bar's plane reach through it would make the popup frame, the nested menu, and every
    /// unselected row transparent onto whatever happens to be behind the popup.
    /// </remarks>
    internal static bool ResolveContinuousBackground(ControlBase control)
    {
        if (control.IsAppearanceBoundary)
        {
            return false;
        }

        for (var current = control.Parent; current is not null; current = current.Parent)
        {
            if (current.ProvidesContinuousBackground)
            {
                return true;
            }

            if (current.IsAppearanceBoundary)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Gates one inherited continuous-plane flag on the receiving control's own boundary.</summary>
    /// <param name="control">The control about to inherit the plane.</param>
    /// <param name="inheritedPlane">Whether the parent chain offers a continuous plane.</param>
    /// <returns>False when <paramref name="control"/> starts a fresh surface; otherwise <paramref name="inheritedPlane"/>.</returns>
    internal static bool ContinuesBackgroundPlane(ControlBase control, bool inheritedPlane) =>
        inheritedPlane && !control.IsAppearanceBoundary;

    /// <summary>Resolves one external parent chain top-down without reading or populating caches.</summary>
    /// <param name="parent">The nearest parent, or null for no ambient source.</param>
    /// <returns>The parent's concrete ambient face, or null.</returns>
    internal static Face? ResolveParentAmbient(ControlBase? parent)
    {
        if (parent is null)
        {
            return null;
        }

        var ancestors = new List<ControlBase>();
        for (var current = parent; current is not null; current = current.Parent)
        {
            ancestors.Add(current);
        }

        Face? ambientFace = null;
        for (var index = ancestors.Count - 1; index >= 0; index--)
        {
            var control = ancestors[index];
            ambientFace = control.ResolveSnapshot(control.AmbientAppearanceState, ambientFace).Face;
        }

        return ambientFace;
    }
}
