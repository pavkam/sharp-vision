// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;

using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;
using SharpVision.Tests.Support;

using Shouldly;

/// <summary>Verifies style-property registration and class defaults.</summary>
public sealed class StylePropertyTests
{
    /// <summary>Verifies duplicate class-default registration fails before publication.</summary>
    [Fact]
    public void RegisterClassDefault_WhenTypeIsDuplicated_ThrowsBeforePublication()
    {
        StyleProperty<int> property = StyleProperty<int>.Register<Control>("probe-default-dup", 0, Impact.Render);
        _ = property.RegisterClassDefault<ProbeControl>(7);

        _ = Should.Throw<ArgumentException>(() => property.RegisterClassDefault<ProbeControl>(8));
    }

    /// <summary>Verifies class defaults override the registered default for derived types.</summary>
    [Fact]
    public void Resolve_WhenClassDefaultExists_UsesMostDerivedDefault()
    {
        StyleProperty<int> property = StyleProperty<int>.Register<Control>("probe-class-host", 0, Impact.Render);
        _ = property.RegisterClassDefault<ProbeControl>(7);
        ProbeControl control = new ProbeControl();

        ThemeTestSupport.Resolve(control, property, State.Normal).ShouldBe(7);
    }

    /// <summary>Verifies the most-derived class default wins over a base-type class default.</summary>
    [Fact]
    public void TryGetClassDefault_WhenBaseAndDerivedRegistered_PrefersMostDerived()
    {
        StyleProperty<int> property = StyleProperty<int>.Register<Control>("probe-derived-precedence", 0, Impact.Render);
        _ = property.RegisterClassDefault<Control>(1);
        _ = property.RegisterClassDefault<ProbeControl>(2);

        property.TryGetClassDefault(typeof(ProbeControl), out var value).ShouldBeTrue();
        value.ShouldBe(2);
    }

    /// <summary>Verifies the public registry enumerates a type's own and inherited properties.</summary>
    [Fact]
    public void GetProperties_IncludesInheritedAndDeclaredProperties()
    {
        StyleProperty<DemoLabelPlacement> declared = DemoPanel.LabelPlacementProperty;

        IReadOnlyList<IStyleProperty> properties = StylePropertyRegistry.GetProperties(typeof(DemoPanel));

        properties.ShouldContain(declared);
        properties.ShouldContain(Control.ForegroundProperty);
    }

    /// <summary>Verifies the public registry finds a property by declaring type and serialized name.</summary>
    [Fact]
    public void FindProperty_ReturnsRegisteredPropertyByName()
    {
        StyleProperty<DemoLabelPlacement> declared = DemoPanel.LabelPlacementProperty;

        StylePropertyRegistry.FindProperty(typeof(DemoPanel), "label-placement").ShouldBeSameAs(declared);
        StylePropertyRegistry.FindProperty(typeof(DemoPanel), "missing").ShouldBeNull();
    }

    /// <summary>Verifies control-free resolution applies class defaults and the theme cascade.</summary>
    [Fact]
    public void Resolve_WithoutControl_UsesThemeCascadeForType()
    {
        Theme theme = new Theme();
        ControlStyle<Control> style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(7));
        theme.SetStyle(style);

        ThemeResolver.Resolve(theme, typeof(ProbeControl), Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(7));
    }
}
