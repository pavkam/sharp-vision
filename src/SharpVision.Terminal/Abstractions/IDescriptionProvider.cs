// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Abstractions;

using Capabilities;

/// <summary>Loads one immutable terminal profile from a platform description source.</summary>
internal interface IDescriptionProvider
{
    /// <summary>Loads the exact requested terminal description without selecting a fallback terminal type.</summary>
    /// <param name="request">The non-null bounded request snapshot.</param>
    /// <returns>A typed load result which owns every retained value.</returns>
    public DescriptionResult Load(DescriptionRequest request);
}
