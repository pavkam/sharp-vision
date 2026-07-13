// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Unicode;

/// <summary>Selects the safe terminal presentation for a base-less cluster.</summary>
public enum Presentation
{
    /// <summary>Present the cluster as one U+FFFD replacement cell.</summary>
    Replacement,
}
