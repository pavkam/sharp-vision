// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.FloatingSurfaceConsumer;

/// <summary>Proves an external package consumer can declare the typed style contract directly
/// over <see cref="ControlBase"/>.</summary>
public sealed class ConsumerControl: ControlBase, IStyled<ConsumerSurfaceStyle>
{
    private readonly StyleSlot<ConsumerSurfaceStyle> _style;

    /// <summary>Initializes one externally defined typed control.</summary>
    public ConsumerControl()
    {
        EnableChromeAuthoring();
        _style = InitializeStyle(ConsumerSurfaceStyle.Definition, OnStyleChanged);
    }

    /// <summary>Gets or sets the complete local style, or null to use the definition fallback.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public ConsumerSurfaceStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <summary>Gets the cached complete local, Theme-owned, or fallback style.</summary>
    public ConsumerSurfaceStyle ActualStyle => _style.Actual;

    private void OnStyleChanged(ConsumerSurfaceStyle previous, ConsumerSurfaceStyle current)
    {
        _ = previous;
        Border = current.Border;
        Shadow = current.Shadow;
    }
}
