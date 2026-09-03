// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Forwards one parent-owned command-bar style slot into a retained command bar.</summary>
public sealed class CommandBarStyleBindingProbe: CompositeControlBase, IStyled<CommandBarStyle>
{
    private readonly StyleSlot<CommandBarStyle> _style;

    /// <summary>Initializes the retained target and binds its primary style slot.</summary>
    public CommandBarStyleBindingProbe()
    {
        Target = new CommandBar();
        InitializeContent(Target);
        _style = InitializeStyle(CommandBarStyle.Definition);
        BindStyle(_style, Target);
    }

    /// <summary>Gets the retained command bar receiving the forwarded nullable local style.</summary>
    public CommandBar Target { get; }

    /// <inheritdoc/>
    public CommandBarStyle? Style
    {
        get => _style.Local;
        set => _style.Local = value;
    }

    /// <inheritdoc/>
    public CommandBarStyle ActualStyle => _style.Actual;
}
