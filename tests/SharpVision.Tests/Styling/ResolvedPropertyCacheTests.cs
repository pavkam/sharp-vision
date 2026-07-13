using SharpVision.Controls;
using SharpVision.Terminal.Protocols;
using SharpVision.Tests.Support;

using Shouldly;

namespace SharpVision.Tests.Styling;

/// <summary>Verifies the control-level resolved-property cache handles null results.</summary>
public sealed class ResolvedPropertyCacheTests
{
    /// <summary>Verifies a null-resolving property is stable across reads and set/clear cycles.</summary>
    [Fact]
    public void Foreground_WhenNoValueResolves_CachesNullAndUpdatesAfterSetAndClear()
    {
        var control = new ProbeControl();

        control.Foreground.ShouldBeNull();
        control.Foreground.ShouldBeNull();

        control.Foreground = Color.Indexed(1);
        control.Foreground.ShouldBe(Color.Indexed(1));

        control.ClearValue(Control.ForegroundProperty);
        control.Foreground.ShouldBeNull();
    }
}
