// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies style-property registration and class defaults.</summary>
public sealed class StylePropertyTests
{
    /// <summary>Verifies an unknown change impact is rejected before property registration.</summary>
    [Fact]
    public void Register_WhenImpactIsUnknown_ThrowsBeforeRegistration()
    {
        const string name = "probe-unknown-impact";
        var unknown = (ChangeImpact) 99;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            StyleProperty<int>.Register<Control>(name, 0, unknown));

        exception.ParamName.ShouldBe("impact");
        StylePropertyRegistry.FindProperty(typeof(Control), name).ShouldBeNull();
    }

    /// <summary>Verifies an arrange-impact local value invalidates arrange and render without measurement.</summary>
    [Fact]
    public void SetValue_WhenPropertyHasArrangeImpact_InvalidatesArrangeAndRender()
    {
        var property = StyleProperty<int>.Register<Control>(
            "probe-arrange-impact",
            0,
            ChangeImpact.Arrange);
        var control = new ProbeControl();
        control.Clear(Invalidation.All);

        control.SetValue(property, 1);

        property.Impact.ShouldBe(ChangeImpact.Arrange);
        control.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
    }

    /// <summary>Verifies assigning an equivalent local value publishes no property change or invalidation.</summary>
    [Fact]
    public void SetValue_WhenLocalValueIsEquivalent_IsNoOp()
    {
        var property = StyleProperty<int>.Register<Control>("probe-equivalent-local", 0, ChangeImpact.Render);
        var control = new ProbeControl();
        control.SetValue(property, 7);
        control.Clear(Invalidation.All);
        var changes = new List<string?>();
        control.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        control.SetValue(property, 7);

        control.Pending.ShouldBe(Invalidation.None);
        changes.ShouldBeEmpty();
    }

    /// <summary>Verifies duplicate class-default registration fails before publication.</summary>
    [Fact]
    public void RegisterClassDefault_WhenTypeIsDuplicated_ThrowsBeforePublication()
    {
        var property = StyleProperty<int>.Register<Control>("probe-default-dup", 0, ChangeImpact.Render);
        _ = property.RegisterClassDefault<ProbeControl>(7);

        _ = Should.Throw<ArgumentException>(() => property.RegisterClassDefault<ProbeControl>(8));
    }

    /// <summary>Verifies class defaults override the registered default for derived types.</summary>
    [Fact]
    public void Resolve_WhenClassDefaultExists_UsesMostDerivedDefault()
    {
        var property = StyleProperty<int>.Register<Control>("probe-class-host", 0, ChangeImpact.Render);
        _ = property.RegisterClassDefault<ProbeControl>(7);
        var control = new ProbeControl();

        ThemeTestSupport.Resolve(control, property, State.Normal).ShouldBe(7);
    }

    /// <summary>Verifies the most-derived class default wins over a base-type class default.</summary>
    [Fact]
    public void TryGetClassDefault_WhenBaseAndDerivedRegistered_PrefersMostDerived()
    {
        var property = StyleProperty<int>.Register<Control>("probe-derived-precedence", 0, ChangeImpact.Render);
        _ = property.RegisterClassDefault<Control>(1);
        _ = property.RegisterClassDefault<ProbeControl>(2);

        property.TryGetClassDefault(typeof(ProbeControl), out var value).ShouldBeTrue();
        value.ShouldBe(2);
    }

    /// <summary>Verifies the public registry enumerates a type's own and inherited properties.</summary>
    [Fact]
    public void GetProperties_IncludesInheritedAndDeclaredProperties()
    {
        var declared = DemoPanel.LabelPlacementProperty;

        var properties = StylePropertyRegistry.GetProperties(typeof(DemoPanel));

        properties.ShouldContain(declared);
        properties.ShouldContain(Control.ForegroundProperty);
    }

    /// <summary>Verifies the public registry finds a property by declaring type and serialized name.</summary>
    [Fact]
    public void FindProperty_ReturnsRegisteredPropertyByName()
    {
        var declared = DemoPanel.LabelPlacementProperty;

        StylePropertyRegistry.FindProperty(typeof(DemoPanel), "label-placement").ShouldBeSameAs(declared);
        StylePropertyRegistry.FindProperty(typeof(DemoPanel), "missing").ShouldBeNull();
    }

    /// <summary>Verifies a theme's per-control style overrides a class default, which remains the themeless baseline.</summary>
    [Fact]
    public void Resolve_WhenThemeDefinesPerControlDefault_OverridesClassDefault()
    {
        var property = StyleProperty<int>.Register<Control>("probe-theme-vs-class", 0, ChangeImpact.Render);
        _ = property.RegisterClassDefault<ProbeControl>(1);
        var theme = new Theme();
        var style = new ControlStyle<ProbeControl>();
        style.Set(property, State.Normal, 2);
        theme.SetStyle(style);
        var themed = new ProbeControl();
        ThemeTestSupport.ApplyTheme(themed, theme);

        themed.GetValue(property).ShouldBe(2);
        new ProbeControl().GetValue(property).ShouldBe(1);
    }

    /// <summary>Verifies control-free resolution applies class defaults and the theme cascade.</summary>
    [Fact]
    public void Resolve_WithoutControl_UsesThemeCascadeForType()
    {
        var theme = new Theme();
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(7));
        theme.SetStyle(style);

        ThemeResolver.Resolve(theme, typeof(ProbeControl), Control.ForegroundProperty, State.Normal)
            .ShouldBe(Color.Indexed(7));
    }
}
