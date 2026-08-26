// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Hosts a custom collection style for resolved equality tests.</summary>
internal sealed class SequenceStyleProbe: ControlBase, IStyled<SequenceStyle>
{
    private readonly StyleSlot<SequenceStyle> _style;

    /// <summary>Initializes the custom collection style slot.</summary>
    public SequenceStyleProbe() => _style = InitializeStyle(SequenceStyle.Definition);

    /// <inheritdoc/>
    public SequenceStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <inheritdoc/>
    public SequenceStyle ActualStyle => _style.Actual;

    /// <summary>Applies one inherited Theme.</summary>
    /// <param name="theme">The theme to propagate.</param>
    public void ApplyTheme(Theme theme) => SetTheme(theme);
}
