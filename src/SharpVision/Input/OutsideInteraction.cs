// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Defines how an active modal plane responds to interaction outside its roots.</summary>
[PublicAPI]
public enum OutsideInteraction
{
    /// <summary>Consumes outside interaction without notifying the scope owner.</summary>
    Ignore,

    /// <summary>Consumes outside interaction and requests dismissal from the scope owner.</summary>
    Dismiss,
}
