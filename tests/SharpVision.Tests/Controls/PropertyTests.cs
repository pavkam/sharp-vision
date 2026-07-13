// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;



/// <summary>Verifies validated mutable control properties and invalidation.</summary>
public sealed class PropertyTests
{
    /// <summary>Verifies control defaults are content-sized and initially dirty.</summary>
    [Fact]
    public void Constructor_WhenCreated_HasDocumentedDefaults()
    {
        ProbeControl control = new();

        control.Width.ShouldBe(Length.Auto);
        control.Height.ShouldBe(Length.Auto);
        control.MinWidth.ShouldBe(0);
        control.MinHeight.ShouldBe(0);
        control.MaxWidth.ShouldBe(int.MaxValue);
        control.MaxHeight.ShouldBe(int.MaxValue);
        control.Margin.ShouldBe(default);
        control.Padding.ShouldBe(default);
        control.HorizontalAlignment.ShouldBe(HorizontalAlignment.Left);
        control.VerticalAlignment.ShouldBe(VerticalAlignment.Stretch);
        control.Visibility.ShouldBe(Visibility.Visible);
        control.IsEnabled.ShouldBeTrue();
        control.EffectiveIsEnabled.ShouldBeTrue();
        control.IsHitTestVisible.ShouldBeTrue();
        control.CanFocus.ShouldBeFalse();
        control.TabIndex.ShouldBe(0);
        control.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies an automatic-width control defaults to its intrinsic content width.</summary>
    [Fact]
    public void Layout_WhenHorizontalAlignmentIsDefault_UsesIntrinsicContentWidth()
    {
        ProbeControl control = new(new Size(3, 2));

        new Engine().Layout(control, new Size(10, 6));

        control.Bounds.ShouldBe(new Rect(0, 0, 3, 6));
    }

    /// <summary>Verifies hit-test transparency does not suppress rendering or focus eligibility.</summary>
    [Fact]
    public void HitTest_WhenControlIsTransparent_RejectsPointerTargetOnly()
    {
        ProbeControl control = new()
        {
            Bounds = new Rect(0, 0, 2, 1),
            CanFocus = true,
            IsHitTestVisible = false,
        };

        control.HitTest(default).ShouldBeNull();
        control.CanFocus.ShouldBeTrue();
        control.EffectiveIsVisible.ShouldBeTrue();
    }

    /// <summary>Verifies inconsistent constraints are rejected before property replacement.</summary>
    [Fact]
    public void ConstraintSetter_WhenValueIsInvalid_ThrowsBeforeMutation()
    {
        ProbeControl control = new()
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
        ProbeControl control = new();
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
        ProbeControl control = new();
        List<(string? Name, Length Width)> observed = [];
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
        ProbeContainer parent = new();
        ProbeControl child = new();
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
        await using Dispatcher dispatcher = Dispatcher.Start();
        ProbeControl control = new();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.Width = Length.Cells(3));

        control.Width.ShouldBe(Length.Auto);
    }

    /// <summary>Verifies hit-test policy mutation is dispatcher-affine.</summary>
    [Fact]
    public async Task IsHitTestVisible_WhenAttachedAndSetOffThread_ThrowsBeforeMutationAsync()
    {
        await using Dispatcher dispatcher = Dispatcher.Start();
        ProbeControl control = new();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.IsHitTestVisible = false);

        control.IsHitTestVisible.ShouldBeTrue();
    }

    /// <summary>Verifies invalid enums and disposed access fail before mutation.</summary>
    [Fact]
    public void Setter_WhenStateIsInvalid_ThrowsDocumentedException()
    {
        ProbeControl control = new();

        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => control.Visibility = (Visibility) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(
            () => control.HorizontalAlignment = (HorizontalAlignment) int.MaxValue);
        control.Dispose();
        _ = Should.Throw<ObjectDisposedException>(() => control.IsEnabled = false);
    }
}
