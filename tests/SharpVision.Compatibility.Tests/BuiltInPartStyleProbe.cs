// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Compatibility.Tests;

using SharpVision.Controls;
using SharpVision.Controls.Display;
using SharpVision.Controls.Input;
using SharpVision.Controls.Layout;

/// <summary>Models an external composite that forwards built-in styles through public definitions.</summary>
internal sealed class BuiltInPartStyleProbe: CompositeControlBase
{
    private readonly StyleSlot<ButtonStyle> _buttonStyle;
    private readonly StyleSlot<CheckBoxStyle> _checkBoxStyle;
    private readonly StyleSlot<SeparatorStyle> _separatorStyle;

    /// <summary>Initializes and binds all externally reusable built-in part definitions.</summary>
    public BuiltInPartStyleProbe()
    {
        _buttonStyle = InitializePartStyle(ButtonStyle.ForwardingDefinition, nameof(ForwardedButtonStyle));
        _checkBoxStyle = InitializePartStyle(CheckBoxStyle.ForwardingDefinition, nameof(ForwardedCheckBoxStyle));
        _separatorStyle = InitializePartStyle(SeparatorStyle.ForwardingDefinition, nameof(ForwardedSeparatorStyle));
        Button = new Button();
        CheckBox = new CheckBox();
        Separator = new Separator();
        InitializeContent(new Stack { Children = { Button, CheckBox, Separator } });
        BindStyle(_buttonStyle, Button);
        BindStyle(_checkBoxStyle, CheckBox);
        BindStyle(_separatorStyle, Separator);
    }

    /// <summary>Gets or sets the forwarded Button style.</summary>
    public ButtonStyle? ForwardedButtonStyle
    {
        get => _buttonStyle.Local;
        set => _buttonStyle.Local = value;
    }

    /// <summary>Gets or sets the forwarded CheckBox style.</summary>
    public CheckBoxStyle? ForwardedCheckBoxStyle
    {
        get => _checkBoxStyle.Local;
        set => _checkBoxStyle.Local = value;
    }

    /// <summary>Gets or sets the forwarded Separator style.</summary>
    public SeparatorStyle? ForwardedSeparatorStyle
    {
        get => _separatorStyle.Local;
        set => _separatorStyle.Local = value;
    }

    /// <summary>Gets the retained Button.</summary>
    public Button Button { get; }

    /// <summary>Gets the retained CheckBox.</summary>
    public CheckBox CheckBox { get; }

    /// <summary>Gets the retained Separator.</summary>
    public Separator Separator { get; }
}
