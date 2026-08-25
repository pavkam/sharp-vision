// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Hosts an aggregate semantic-color style to prove framework-owned invalidation.</summary>
public sealed class SemanticColorStyleProbe: ControlBase, IStyled<SemanticColorStyle>
{
    private readonly StyleSlot<SemanticColorStyle> _style;

    /// <summary>Initializes the aggregate style slot.</summary>
    public SemanticColorStyleProbe() => _style = InitializeStyle(SemanticColorStyle.Definition);

    /// <inheritdoc/>
    public SemanticColorStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <inheritdoc/>
    public SemanticColorStyle ActualStyle => _style.Actual;

    /// <summary>Propagates an inherited theme for focused invalidation tests.</summary>
    /// <param name="theme">The prospective theme.</param>
    public void ApplyTheme(Theme theme) => SetTheme(theme);
}
