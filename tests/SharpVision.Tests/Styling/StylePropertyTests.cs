using SharpVision.Controls;
using SharpVision.Styling;
using SharpVision.Tests.Support;

using Shouldly;

namespace SharpVision.Tests.Styling;

/// <summary>Verifies style-property registration and class defaults.</summary>
public sealed class StylePropertyTests
{
    /// <summary>Verifies duplicate class-default registration fails before publication.</summary>
    [Fact]
    public void RegisterClassDefault_WhenTypeIsDuplicated_ThrowsBeforePublication()
    {
        var property = StyleProperty<int>.Register<Control>("probe-default-dup", 0, Impact.Render);
        _ = property.RegisterClassDefault<ProbeControl>(7);

        _ = Should.Throw<ArgumentException>(() => property.RegisterClassDefault<ProbeControl>(8));
    }

    /// <summary>Verifies class defaults override the registered default for derived types.</summary>
    [Fact]
    public void Resolve_WhenClassDefaultExists_UsesMostDerivedDefault()
    {
        var property = StyleProperty<int>.Register<Control>("probe-class-host", 0, Impact.Render);
        _ = property.RegisterClassDefault<ProbeControl>(7);
        var control = new ProbeControl();

        ThemeTestSupport.Resolve(control, property, State.Normal).ShouldBe(7);
    }
}
