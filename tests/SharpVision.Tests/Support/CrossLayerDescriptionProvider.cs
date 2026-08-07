// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Returns one deterministic description result for cross-layer hosting tests.</summary>
internal sealed class CrossLayerDescriptionProvider: IDescriptionProvider
{
    /// <summary>Gets or sets the result returned by the provider.</summary>
    internal DescriptionResult Result { get; set; } = DescriptionResult.PlatformUnavailable();

    /// <summary>Gets the exact request observed by the provider, or null before loading.</summary>
    internal DescriptionRequest? Request { get; private set; }

    /// <inheritdoc/>
    public DescriptionResult Load(DescriptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
        return Result;
    }
}
