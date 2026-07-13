// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Styling;



/// <summary>Verifies mutable control-style storage, validation, and freezing.</summary>
public sealed class ControlStyleTests
{
    /// <summary>Verifies set, remove, and try-get round-trip one stored value.</summary>
    [Fact]
    public void Set_WhenValueIsStored_TryGetReturnsIt()
    {
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(3));

        style.TryGet(Control.ForegroundProperty, State.Normal, out Color? value).ShouldBeTrue();
        value.ShouldBe(Color.Indexed(3));
        style.Remove(Control.ForegroundProperty, State.Normal).ShouldBeTrue();
        style.TryGet(Control.ForegroundProperty, State.Normal, out _).ShouldBeFalse();
    }

    /// <summary>Verifies combined overlay states are stored and read back.</summary>
    [Fact]
    public void Set_WhenOverlayStatesAreCombined_StoresValue()
    {
        ControlStyle<Control> style = new();
        State combined = State.Hovered | State.Focused;

        style.Set(Control.ForegroundProperty, combined, Color.Indexed(1));

        style.TryGet(Control.ForegroundProperty, combined, out Color? value).ShouldBeTrue();
        value.ShouldBe(Color.Indexed(1));
    }

    /// <summary>Verifies unknown state flags are still rejected.</summary>
    [Fact]
    public void Set_WhenStateHasUnknownFlags_Throws()
    {
        ControlStyle<Control> style = new();

        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            style.Set(Control.ForegroundProperty, (State) (1 << 20), Color.Indexed(1)));
    }

    /// <summary>Verifies measure-impact properties may now be stored in overlay states.</summary>
    [Fact]
    public void Set_WhenMeasurePropertyUsesOverlayState_StoresValue()
    {
        ControlStyle<Control> style = new();

        style.Set(Control.PaddingProperty, State.Pressed, new Thickness(1));

        style.TryGet(Control.PaddingProperty, State.Pressed, out Thickness value).ShouldBeTrue();
        value.ShouldBe(new Thickness(1));
    }

    /// <summary>Verifies frozen styles reject further mutation.</summary>
    [Fact]
    public void Set_WhenStyleIsFrozen_Throws()
    {
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(2));
        ControlStyle<Control> frozen = style.FreezeCopy();

        _ = Should.Throw<InvalidOperationException>(() =>
            frozen.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4)));
    }

    /// <summary>Verifies clones are independent mutable copies.</summary>
    [Fact]
    public void Clone_WhenSourceMutatesAfterCopy_DoesNotAffectClone()
    {
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1));
        ControlStyle<Control> clone = style.Clone();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(5));

        clone.TryGet(Control.ForegroundProperty, State.Normal, out Color? value).ShouldBeTrue();
        value.ShouldBe(Color.Indexed(1));
    }

    /// <summary>Verifies the public read-only interface view exposes stored values.</summary>
    [Fact]
    public void TryGetValue_ThroughPublicInterface_ReadsStoredValue()
    {
        ControlStyle<Control> style = new();
        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(4));

        style.TryGetValue(Control.ForegroundProperty, State.Normal, out object? value).ShouldBeTrue();
        value.ShouldBe(Color.Indexed(4));
    }

    /// <summary>Verifies committed mutations raise the changed event once.</summary>
    [Fact]
    public void Set_WhenValueChanges_RaisesChanged()
    {
        ControlStyle<Control> style = new();
        int raised = 0;
        style.Changed += (_, _) => raised++;

        style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(6));

        raised.ShouldBe(1);
    }

    /// <summary>Verifies the changed event runs outside the internal lock so handlers may re-enter the style.</summary>
    [Fact]
    public async Task Set_WhenHandlerReentersStyle_DoesNotDeadlockAsync()
    {
        ControlStyle<Control> style = new();
        style.Changed += (_, _) => _ = style.Clone();

        Task work = Task.Run(
            () => style.Set(Control.ForegroundProperty, State.Normal, Color.Indexed(1)),
            TestContext.Current.CancellationToken);
        Task finished = await Task.WhenAny(
            work,
            Task.Delay(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));

        ReferenceEquals(finished, work).ShouldBeTrue();
        await work;
    }
}
