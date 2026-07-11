using SharpVision.Controls;
using SharpVision.Layout;
using SharpVision.Tests.Support;
using SharpVision.Threading;

using Shouldly;

namespace SharpVision.Tests.Controls;

/// <summary>Verifies validated mutable control properties and invalidation.</summary>
public sealed class PropertyTests
{
    /// <summary>Verifies control defaults are conservative and initially dirty.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasDocumentedDefaults()
    {
        var control = new ProbeControl();

        control.Width.ShouldBe(Length.Auto);
        control.Height.ShouldBe(Length.Auto);
        control.MinWidth.ShouldBe(0);
        control.MinHeight.ShouldBe(0);
        control.MaxWidth.ShouldBe(int.MaxValue);
        control.MaxHeight.ShouldBe(int.MaxValue);
        control.Margin.ShouldBe(default);
        control.Padding.ShouldBe(default);
        control.HorizontalAlignment.ShouldBe(HorizontalAlignment.Stretch);
        control.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        control.Visibility.ShouldBe(Visibility.Visible);
        control.IsEnabled.ShouldBeTrue();
        control.EffectiveIsEnabled.ShouldBeTrue();
        control.CanFocus.ShouldBeFalse();
        control.TabIndex.ShouldBe(0);
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies inconsistent constraints are rejected before property replacement.</summary>
    [Fact]
    public void ConstraintSetter_WhenValueIsInvalid_ThrowsBeforeMutation()
    {
        var control = new ProbeControl
        {
            MinWidth = 3,
            MaxHeight = 8,
        };

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.MinWidth = -1);
        _ = Should.Throw<ArgumentException>(() => control.MaxWidth = 2);
        _ = Should.Throw<ArgumentException>(() => control.MinHeight = 9);

        control.MinWidth.ShouldBe(3);
        control.MaxWidth.ShouldBe(int.MaxValue);
        control.MinHeight.ShouldBe(0);
        control.MaxHeight.ShouldBe(8);
    }

    /// <summary>Verifies each property requests only its required phase closure.</summary>
    [Fact]
    public void PropertySetter_WhenValueChanges_InvalidatesRequiredPhases()
    {
        var control = new ProbeControl();
        control.Clear(Invalidation.All);

        control.Width = Length.Cells(10);
        control.Pending.ShouldBe(Invalidation.All);
        control.Clear(Invalidation.All);

        control.HorizontalAlignment = HorizontalAlignment.Center;
        control.Pending.ShouldBe(Invalidation.Arrange | Invalidation.Render);
        control.Clear(Invalidation.All);

        control.IsEnabled = false;
        control.Pending.ShouldBe(Invalidation.Render);
        control.Clear(Invalidation.All);

        control.Visibility = Visibility.Hidden;
        control.Pending.ShouldBe(Invalidation.Render);
        control.Clear(Invalidation.All);

        control.Visibility = Visibility.Collapsed;
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies property change notification runs once after mutation.</summary>
    [Fact]
    public void Width_WhenChanged_RaisesPropertyChangedOnceAfterMutation()
    {
        var control = new ProbeControl();
        var observed = new List<(string? Name, Length Width)>();
        control.PropertyChanged += (_, eventArgs) =>
            observed.Add((eventArgs.PropertyName, control.Width));

        control.Width = Length.Cells(12);
        control.Width = Length.Cells(12);

        observed.ShouldBe([(nameof(Control.Width), Length.Cells(12))]);
    }

    /// <summary>Verifies effective enabled state inherits and invalidates descendants.</summary>
    [Fact]
    public void IsEnabled_WhenAncestorChanges_UpdatesDescendantEffectiveState()
    {
        var parent = new ProbeContainer();
        var child = new ProbeControl();
        parent.Children.Add(child);
        parent.Clear(Invalidation.All);
        child.Clear(Invalidation.All);

        parent.IsEnabled = false;

        child.EffectiveIsEnabled.ShouldBeFalse();
        child.Pending.ShouldBe(Invalidation.Render);
        parent.IsEnabled = true;
        child.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies attached property mutation is dispatcher-affine.</summary>
    [Fact]
    public async Task Width_WhenAttachedAndSetOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.Width = Length.Cells(3));

        control.Width.ShouldBe(Length.Auto);
    }

    /// <summary>Verifies invalid enums and disposed access fail before mutation.</summary>
    [Fact]
    public void Setter_WhenStateIsInvalid_ThrowsDocumentedException()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => control.Visibility = (Visibility) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => control.HorizontalAlignment = (HorizontalAlignment) int.MaxValue);
        control.Dispose();
        _ = Should.Throw<ObjectDisposedException>(() => control.IsEnabled = false);
    }
}
