// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Exposes committed callback-transition behavior for ControlBase regression tests.</summary>
internal sealed class TransitionPropertyProbe: ControlBase
{
    private readonly CallbackTransitionStream _valueTransitions = new();

    /// <summary>Raised while the committing transition remains current.</summary>
    internal event EventHandler? ValueChanged;

    /// <summary>Gets or sets the versioned test value.</summary>
    internal int Value
    {
        get;
        set
        {
            if (!SetTransitionProperty(
                    ref field,
                    value,
                    InvalidationImpact.Render,
                    _valueTransitions,
                    out var transition))
            {
                return;
            }

            transition.CaptureRequired(() => RequiredContinuations++);
            transition.PublishCurrent(ValueChanged, this, EventArgs.Empty);
            transition.ThrowIfFailed();
        }
    }

    /// <summary>Gets how many required continuations completed.</summary>
    internal int RequiredContinuations { get; private set; }
}
