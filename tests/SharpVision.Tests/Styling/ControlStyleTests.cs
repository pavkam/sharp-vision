using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Styling;
using SharpVision.Terminal.Protocols;

using Shouldly;

namespace SharpVision.Tests.Styling;

/// <summary>Verifies mutable control-style storage, validation, and freezing.</summary>
public sealed class ControlStyleTests
{
    /// <summary>Verifies set, remove, and try-get round-trip one stored value.</summary>
    [Fact]
    public void Set_WhenValueIsStored_TryGetReturnsIt()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(3));

        style.TryGet(Control.ForegroundProperty, State.Normal, out var value).ShouldBeTrue();
        value.ShouldBe(Color.Indexed(3));
        style.Remove(Control.ForegroundProperty, State.Normal).ShouldBeTrue();
        style.TryGet(Control.ForegroundProperty, State.Normal, out _).ShouldBeFalse();
    }

    /// <summary>Verifies combined overlay states are rejected during mutation.</summary>
    [Fact]
    public void Set_WhenOverlayStatesAreCombined_Throws()
    {
        var style = new ControlStyle<Control>();

        _ = Should.Throw<ArgumentException>(() =>
            style.Set(Control.ForegroundProperty, State.Hovered | State.Focused, Color.Indexed(1)));
    }

    /// <summary>Verifies measure-impact properties cannot be stored in overlay states.</summary>
    [Fact]
    public void Set_WhenMeasurePropertyUsesOverlayState_Throws()
    {
        var style = new ControlStyle<Control>();

        _ = Should.Throw<ArgumentException>(() =>
            style.Set(Control.PaddingProperty, State.Hovered, new Thickness(1)));
    }

    /// <summary>Verifies frozen styles reject further mutation.</summary>
    [Fact]
    public void Set_WhenStyleIsFrozen_Throws()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));
        var frozen = style.FreezeCopy();

        _ = Should.Throw<InvalidOperationException>(() =>
            frozen.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4)));
    }

    /// <summary>Verifies clones are independent mutable copies.</summary>
    [Fact]
    public void Clone_WhenSourceMutatesAfterCopy_DoesNotAffectClone()
    {
        var style = new ControlStyle<Control>();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1));
        var clone = style.Clone();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(5));

        clone.TryGet(Control.ForegroundProperty, State.Normal, out var value).ShouldBeTrue();
        value.ShouldBe(Color.Indexed(1));
    }

    /// <summary>Verifies committed mutations raise the changed event once.</summary>
    [Fact]
    public void Set_WhenValueChanges_RaisesChanged()
    {
        var style = new ControlStyle<Control>();
        var raised = 0;
        style.Changed += (_, _) => raised++;

        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(6));

        raised.ShouldBe(1);
    }
}
