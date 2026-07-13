// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Records completed activations from the shared press behavior.</summary>
internal sealed class ProbePressable: Pressable
{
    /// <summary>Initializes an empty pressable probe.</summary>
    internal ProbePressable() : base(capacity: 0)
    {
    }

    /// <summary>Gets completed activation causes in commit order.</summary>
    internal List<ActivationCause> Activations { get; } = [];

    /// <inheritdoc/>
    protected override void Activate(ActivationCause cause) => Activations.Add(cause);
}
