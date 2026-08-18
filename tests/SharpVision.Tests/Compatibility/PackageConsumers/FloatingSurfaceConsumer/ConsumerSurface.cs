// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.FloatingSurfaceConsumer;

/// <summary>Exercises the public derivation surface exposed by the packed SharpVision package -
/// proving an external consumer can still get a typed style by declaring
/// <see cref="IStyled{TStyle}"/> directly on a type derived from <see cref="FloatingSurfaceBase"/>.</summary>
public sealed class ConsumerSurface: FloatingSurfaceBase, IStyled<ConsumerSurfaceStyle>
{
    private readonly StyleSlot<ConsumerSurfaceStyle> _style;

    /// <summary>Initializes one externally defined typed surface.</summary>
    public ConsumerSurface()
    {
        EnableChromeAuthoring();
        _style = InitializeStyle(ConsumerSurfaceStyle.Definition, OnStyleChanged);
    }

    /// <summary>Gets or sets the complete local style, or null to use the definition fallback.</summary>
    /// <exception cref="InvalidOperationException">The attached surface is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The surface is disposed.</exception>
    public ConsumerSurfaceStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the cached complete local, Theme-owned, or fallback style.</summary>
    public ConsumerSurfaceStyle ActualStyle => _style.Actual;

    /// <summary>Gets whether this consumer has committed its family-specific presented state.</summary>
    public bool IsPresented { get; private set; }

    /// <summary>Commits consumer-specific state through the protected opening transaction.</summary>
    public void Present() => OpenSurface(() => IsPresented = true);

    /// <summary>Commits consumer-specific state through the protected closing transaction.</summary>
    /// <returns>True when a presented surface was closed; otherwise false.</returns>
    public bool Dismiss() => CloseSurface(
        () => IsPresented = false,
        () => Content = null);

    private void OnStyleChanged(ConsumerSurfaceStyle previous, ConsumerSurfaceStyle current)
    {
        _ = previous;
        Border = current.Border;
        Shadow = current.Shadow;
    }
}
