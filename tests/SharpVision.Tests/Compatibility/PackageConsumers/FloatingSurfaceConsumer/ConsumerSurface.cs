// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.FloatingSurfaceConsumer;

/// <summary>Exercises the public derivation surface exposed by the packed SharpVision package.</summary>
public sealed class ConsumerSurface: FloatingSurface
{
    private ConsumerSurfaceStyle _style;

    /// <summary>Gets whether this consumer has committed its family-specific presented state.</summary>
    public bool IsPresented { get; private set; }

    /// <summary>Gets or sets the consumer-owned complete surface presentation.</summary>
    public ConsumerSurfaceStyle Style
    {
        get => _style;
        set
        {
            Border = value.Border;
            Shadow = value.Shadow;
            _style = value;
        }
    }

    /// <summary>Commits consumer-specific state through the protected opening transaction.</summary>
    public void Present() => OpenSurface(() => IsPresented = true);

    /// <summary>Commits consumer-specific state through the protected closing transaction.</summary>
    /// <returns>True when a presented surface was closed; otherwise false.</returns>
    public bool Dismiss() => CloseSurface(
        () => IsPresented = false,
        () => Content = null);
}
