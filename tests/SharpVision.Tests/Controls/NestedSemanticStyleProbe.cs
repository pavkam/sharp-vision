// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Hosts a nested semantic style for Theme notification tests.</summary>
public sealed class NestedSemanticStyleProbe: ControlBase, IStyled<NestedSemanticStyle>
{
    private readonly StyleSlot<NestedSemanticStyle> _style;

    /// <summary>Initializes the nested style slot.</summary>
    public NestedSemanticStyleProbe() => _style = InitializeStyle(NestedSemanticStyle.Definition);

    /// <inheritdoc/>
    public NestedSemanticStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <inheritdoc/>
    public NestedSemanticStyle ActualStyle => _style.Actual;

    /// <summary>Applies one inherited Theme.</summary>
    /// <param name="theme">The theme to apply.</param>
    public void ApplyTheme(Theme theme) => SetTheme(theme);
}
