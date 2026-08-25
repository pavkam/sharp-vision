// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

/// <summary>Exercises one secondary slot bound to a retained primary slot.</summary>
public sealed class StyleSlotBindingProbe: CompositeControlBase
{
    private static readonly StyleDefinition<ButtonStyle> _definition =
        StyleDefinitions.Part(
            static theme =>
            {
                var input = (theme ?? ThemeCatalog.Dark).GetStyleSet(InputStyle.Default).Normal;
                return new ButtonStyle(input.Face, input.Border, input.Shadow, ButtonStyle.Standard.Padding);
            },
            static (previous, _, current, _) => previous == current
                ? InvalidationImpact.None
                : InvalidationImpact.Measure);
    private readonly StyleSlot<ButtonStyle> _buttonStyle;
    private readonly Overlay _root;

    /// <summary>Initializes a retained target and binds its primary slot.</summary>
    public StyleSlotBindingProbe()
    {
        Target = new StyleSlotProbe();
        SecondTarget = new StyleSlotProbe();
        Slider = new Slider();
        UnboundTarget = new StyleSlotProbe();
        _root = new Overlay { Children = { Target, SecondTarget, UnboundTarget, Slider } };
        InitializeContent(_root);
        _buttonStyle = InitializePartStyle(_definition, nameof(ButtonStyle));
        BindStyle(_buttonStyle, Target);
        BindStyle(_buttonStyle, SecondTarget);
    }

    /// <summary>Gets the retained target.</summary>
    public StyleSlotProbe Target { get; }

    /// <summary>Gets the second retained matching target.</summary>
    public StyleSlotProbe SecondTarget { get; }

    /// <summary>Gets a retained matching target that starts without a binding.</summary>
    public StyleSlotProbe UnboundTarget { get; }

    /// <summary>Gets a retained mismatched style target.</summary>
    public Slider Slider { get; }

    /// <summary>Gets or sets the nullable local style forwarded to the target.</summary>
    public ButtonStyle? ButtonStyle
    {
        get => _buttonStyle.Local;
        set => _buttonStyle.Local = value;
    }

    /// <summary>Gets the locally resolved secondary style.</summary>
    public ButtonStyle ActualButtonStyle => _buttonStyle.Actual;

    /// <summary>Attempts to bind the first target a second time.</summary>
    public void BindDuplicate() => BindStyle(_buttonStyle, Target);

    /// <summary>Attempts to bind the Button-style source to a Slider-style target.</summary>
    public void BindMismatchedType() => BindStyle(_buttonStyle, Slider);

    /// <summary>Attempts to bind an arbitrary control outside this retained tree.</summary>
    /// <param name="target">The non-null external target.</param>
    public void BindCrossTree(ControlBase target) => BindStyle(_buttonStyle, target);

    /// <summary>Removes the first target without disposing it.</summary>
    public void DetachTarget() => _root.Children.Remove(Target);

    /// <summary>Binds the initially unbound retained target.</summary>
    public void BindUnboundTarget() => BindStyle(_buttonStyle, UnboundTarget);
}
