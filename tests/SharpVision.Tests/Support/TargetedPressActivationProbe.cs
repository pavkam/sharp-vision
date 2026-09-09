// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Composes <see cref="ControlBase.EnableTargetedPressActivation{TTarget}"/> directly,
/// without deriving <see cref="InputBase"/>, exposing two disjoint whole-cell rectangles - target
/// 0 at the left two columns of the top row, target 1 at the right two columns of the top row - so
/// a test can press one, move across both, and release inside or outside either.</summary>
internal sealed class TargetedPressActivationProbe: ControlBase
{
    /// <summary>Initializes a stretching, focusable probe with targeted press activation
    /// enabled.</summary>
    /// <param name="enableWholeControlPressFirst">When true, calls
    /// <see cref="ControlBase.EnablePressActivation"/> before enabling targeted press activation,
    /// so a test can observe the documented mutual-exclusivity exception.</param>
    internal TargetedPressActivationProbe(bool enableWholeControlPressFirst = false)
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsFocusable = true;
        IsTabStop = true;

        if (enableWholeControlPressFirst)
        {
            EnablePressActivation();
        }

        EnableTargetedPressActivation<int>(
            TryHitTarget,
            TargetBounds,
            _ => IsTargetCurrentOverride,
            (target, pressed) => _ = pressed ? PressedTargets.Add(target) : PressedTargets.Remove(target),
            (target, cause) =>
            {
                ActivatedTargets.Add(target);
                LastActivationCause = cause;
            });
    }

    /// <summary>Gets or sets whether a resolved target is reported as still current. A test flips
    /// this to false mid-gesture to prove a target that stops being current cancels the press
    /// without activating.</summary>
    internal bool IsTargetCurrentOverride { get; set; } = true;

    /// <summary>Gets every target index currently committed pressed.</summary>
    internal HashSet<int> PressedTargets { get; } = [];

    /// <summary>Gets every target index that completed activation, in order.</summary>
    internal List<int> ActivatedTargets { get; } = [];

    /// <summary>Gets the cause captured by the most recent activation, or null before any.</summary>
    internal ActivationCause? LastActivationCause { get; private set; }

    /// <summary>Gets the left target's whole-cell rectangle at the probe's current bounds.</summary>
    internal Rect FirstTargetBounds => TargetBounds(0);

    /// <summary>Gets the right target's whole-cell rectangle at the probe's current bounds.</summary>
    internal Rect SecondTargetBounds => TargetBounds(1);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(4, 1);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }

    private bool TryHitTarget(Point cells, out int target)
    {
        if (FirstTargetBounds.Contains(cells))
        {
            target = 0;
            return true;
        }

        if (SecondTargetBounds.Contains(cells))
        {
            target = 1;
            return true;
        }

        target = -1;
        return false;
    }

    private Rect TargetBounds(int target) => target == 0
        ? new Rect(Bounds.X, Bounds.Y, 2, 1)
        : new Rect(Bounds.X + 2, Bounds.Y, 2, 1);
}
