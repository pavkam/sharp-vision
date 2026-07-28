// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Tests.Capabilities;

using SharpVision.Terminal.Capabilities;

/// <summary>Records deterministic terminal-description requests for consumer tests.</summary>
internal sealed class FakeDescriptionProvider: IDescriptionProvider
{
    /// <summary>Gets or sets the deterministic result returned by <see cref="Load"/>.</summary>
    internal DescriptionResult Result { get; set; } = DescriptionResult.PlatformUnavailable();

    /// <summary>Gets the most recent owned request.</summary>
    internal DescriptionRequest? Request { get; private set; }

    /// <inheritdoc/>
    public DescriptionResult Load(DescriptionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        Request = request;
        return Result;
    }
}
