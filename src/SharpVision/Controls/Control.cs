// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines a control whose primary complete style is owned by the framework.</summary>
/// <typeparam name="TStyle">The small immutable complete style value.</typeparam>
[PublicAPI]
public abstract class Control<TStyle>: ControlBase
    where TStyle : struct, IEquatable<TStyle>
{
    /// <summary>Initializes one Theme-aware primary style slot.</summary>
    /// <param name="definition">The immutable primary or aggregate style policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> is null.</exception>
    protected Control(StyleDefinition<TStyle> definition) =>
        StyleSlot = InitializeStyle(definition, OnStyleChanged);

    /// <summary>Gets or sets the complete local style, or null to use the definition fallback.</summary>
    /// <exception cref="InvalidOperationException">The attached control is mutated off-dispatcher.</exception>
    /// <exception cref="ObjectDisposedException">The control is disposed.</exception>
    public TStyle? Style
    {
        get => StyleSlot.Local;
        set => StyleSlot.Local = value;
    }

    /// <summary>Gets the cached complete local, Theme-owned, or fallback style.</summary>
    public virtual TStyle ActualStyle => StyleSlot.Actual;

    /// <summary>Gets the framework-owned primary style slot for specialized resolved projections.</summary>
    protected StyleSlot<TStyle> StyleSlot { get; }

    /// <summary>Runs genuine post-commit behavior after a changed resolved style is published.</summary>
    /// <param name="previous">The previous complete resolved style.</param>
    /// <param name="current">The current complete resolved style.</param>
    protected virtual void OnStyleChanged(TStyle previous, TStyle current)
    {
        _ = previous;
        _ = current;
    }
}
