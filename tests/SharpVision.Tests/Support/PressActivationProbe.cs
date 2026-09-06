// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Composes <see cref="ControlBase.EnablePressActivation"/> directly, without deriving
/// <see cref="InputBase"/>, to prove the capability is reachable from any control.</summary>
internal sealed class PressActivationProbe: ControlBase
{
    /// <summary>Initializes a stretching, focusable probe with press activation enabled.</summary>
    internal PressActivationProbe()
    {
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        IsFocusable = true;
        IsTabStop = true;
        EnablePressActivation();
    }

    /// <summary>Gets how many times <see cref="Activate"/> committed.</summary>
    internal int ActivationCount { get; private set; }

    /// <summary>Gets the cause captured by the most recent activation, or null before any.</summary>
    internal ActivationCause? LastActivationCause { get; private set; }

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause)
    {
        ActivationCount++;
        LastActivationCause = cause;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Constraint constraint)
    {
        _ = constraint;
        return new Size(1, 1);
    }

    /// <inheritdoc/>
    protected override void OnEvent(RoutedEventArgs eventArgs)
    {
        base.OnEvent(eventArgs);
        HandlePressActivation(eventArgs);
    }
}
