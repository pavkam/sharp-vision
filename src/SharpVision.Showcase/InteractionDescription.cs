// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase;

/// <summary>Describes one supported interaction in a showcase page's interaction table.</summary>
internal readonly struct InteractionDescription
{
    /// <summary>Initializes one complete interaction description.</summary>
    /// <param name="input">The input gesture, event, or programmatic path.</param>
    /// <param name="behavior">The control behavior caused by the input.</param>
    /// <param name="result">The observable state, event, or rendering result.</param>
    /// <exception cref="ArgumentException">A value is blank.</exception>
    internal InteractionDescription(string input, string behavior, string result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(behavior);
        ArgumentException.ThrowIfNullOrWhiteSpace(result);
        Input = input;
        Behavior = behavior;
        Result = result;
    }

    /// <summary>Gets the supported input path.</summary>
    internal string Input { get; }

    /// <summary>Gets the behavior performed by the control.</summary>
    internal string Behavior { get; }

    /// <summary>Gets the observable result of the interaction.</summary>
    internal string Result { get; }
}
