// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Preflights one subtree context change before committing non-throwing field and cache updates.</summary>
internal sealed class ContextTransitionPlan
{
    private readonly List<ControlContextTransition> _entries;

    private ContextTransitionPlan(
        List<ControlContextTransition> entries,
        List<ThemeTransition> themeTransitions,
        Dictionary<ControlBase, AppearanceSnapshot> currentAppearance,
        List<ControlBase> attached,
        List<ControlBase> detached)
    {
        _entries = entries;
        ThemeTransitions = themeTransitions;
        CurrentAppearance = currentAppearance;
        Attached = attached;
        Detached = detached;
    }

    /// <summary>Gets the resolved Theme transitions in parent-first order.</summary>
    internal List<ThemeTransition> ThemeTransitions { get; }

    /// <summary>Gets the prospective cache-neutral appearance snapshots.</summary>
    internal Dictionary<ControlBase, AppearanceSnapshot> CurrentAppearance { get; }

    /// <summary>Gets controls that will become attached in parent-first order.</summary>
    internal List<ControlBase> Attached { get; }

    /// <summary>Gets controls that will become detached in parent-first order.</summary>
    internal List<ControlBase> Detached { get; }

    /// <summary>Resolves every virtual hook and prospective snapshot before observable mutation.</summary>
    internal static ContextTransitionPlan Create(
        ControlBase root,
        Dispatcher? dispatcher,
        UnicodePolicy cellPolicy,
        FocusManager? focusOwner,
        PointerManager? captureOwner,
        ModalityManager? modalityOwner,
        Theme? theme,
        Dictionary<ControlBase, AppearanceSnapshot> previousAppearance,
        Face? currentParentAmbientFace,
        bool useContinuousBackground,
        bool propagateContext)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(cellPolicy);
        ArgumentNullException.ThrowIfNull(previousAppearance);
        // Sized to this call's own root subtree, not previousAppearance.Count: a batch commit
        // shares one previousAppearance map across every changing root, so that count reflects
        // the whole transaction. Using it here made every single-item plan in an n-item batch
        // allocate and zero an O(n)-capacity list and dictionary, turning the batch O(n^2).
        var entries = new List<ControlContextTransition>();
        var themeTransitions = new List<ThemeTransition>();
        var currentAppearance = new Dictionary<ControlBase, AppearanceSnapshot>();
        var attached = new List<ControlBase>();
        var detached = new List<ControlBase>();
        var stack = new Stack<(ControlBase Control, Face? ParentAmbient, bool ContinuousBackground, bool IsRoot)>();
        stack.Push((root, currentParentAmbientFace, useContinuousBackground, true));

        while (stack.TryPop(out var entry))
        {
            var control = entry.Control;
            control.VerifyMutable();
            var commitsContext = propagateContext || entry.IsRoot;
            var currentDispatcher = commitsContext ? dispatcher : control.Dispatcher;
            var currentCellPolicy = commitsContext ? cellPolicy : control.CellPolicy;
            var currentFocusOwner = commitsContext ? focusOwner : control.FocusOwner;
            var currentCaptureOwner = commitsContext ? captureOwner : control.CaptureOwner;
            var currentModalityOwner = commitsContext ? modalityOwner : control.ModalityOwner;
            var currentTheme = commitsContext ? theme : control.Theme;
            var previousSnapshot = previousAppearance[control];
            var themeTransition = control.PrepareThemeTransition(
                currentTheme,
                previousSnapshot.ParentAmbientFace,
                entry.ParentAmbient);
            var profile = control.ResolveAppearanceStates(currentTheme);
            var state = control.GetAppearanceState();
            var actual = control.ResolveSnapshot(
                state,
                currentTheme,
                profile,
                entry.ParentAmbient,
                entry.ContinuousBackground);
            var ambientFace = control.AmbientAppearanceState == state
                ? actual.Face
                : control.ResolveSnapshot(
                    control.AmbientAppearanceState,
                    currentTheme,
                    profile,
                    entry.ParentAmbient,
                    entry.ContinuousBackground).Face;
            currentAppearance.Add(
                control,
                new AppearanceSnapshot(entry.ParentAmbient, actual, ambientFace));
            entries.Add(new ControlContextTransition(
                control,
                currentDispatcher,
                currentCellPolicy,
                currentFocusOwner,
                currentCaptureOwner,
                currentModalityOwner,
                currentTheme,
                commitsContext,
                themeTransition));

            if (themeTransition is { } transition)
            {
                themeTransitions.Add(transition);
            }

            if (control.Dispatcher is null && currentDispatcher is not null)
            {
                attached.Add(control);
            }
            else if (control.Dispatcher is not null && currentDispatcher is null)
            {
                detached.Add(control);
            }

            for (var index = control.OwnedControlCount - 1; index >= 0; index--)
            {
                stack.Push((
                    control.OwnedControlAt(index),
                    ambientFace,
                    entry.ContinuousBackground || control.ProvidesContinuousBackground,
                    false));
            }
        }

        return new ContextTransitionPlan(
            entries,
            themeTransitions,
            currentAppearance,
            attached,
            detached);
    }

    /// <summary>Commits only prevalidated field, cache, and invalidation changes.</summary>
    internal void Commit()
    {
        foreach (var entry in _entries)
        {
            entry.Control.CommitContext(entry);
        }
    }
}
