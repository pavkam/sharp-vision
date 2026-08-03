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
        var control = new ProbeControl();

        control.Width.ShouldBe(Length.Auto);
        control.Height.ShouldBe(Length.Auto);
        control.MinWidth.ShouldBe(0);
        control.MinHeight.ShouldBe(0);
        control.MaxWidth.ShouldBe(int.MaxValue);
        control.MaxHeight.ShouldBe(int.MaxValue);
        control.Margin.ShouldBe(default);
        control.Padding.ShouldBe(default);
        control.Face.Background.ShouldBe(ThemeColor.Control);
        control.Border.Foreground.ShouldBe(ThemeColor.ControlBorder);
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
        var control = new ProbeControl(new Size(3, 2));

        new LayoutEngine().Layout(control, new Size(10, 6));

        control.Bounds.ShouldBe(new Rect(0, 0, 3, 6));
    }

    /// <summary>Verifies hit-test transparency does not suppress rendering or focus eligibility.</summary>
    [Fact]
    public void HitTest_WhenControlIsTransparent_RejectsPointerTargetOnly()
    {
        var control = new ProbeControl { Bounds = new Rect(0, 0, 2, 1), Focusable = true, IsHitTestVisible = false };

        control.HitTest(default).ShouldBeNull();
        control.CanFocus.ShouldBeTrue();
        control.EffectiveIsVisible.ShouldBeTrue();
    }

    /// <summary>Verifies inconsistent constraints are rejected before property replacement.</summary>
    [Fact]
    public void ConstraintSetter_WhenValueIsInvalid_ThrowsBeforeMutation()
    {
        var control = new ProbeControl { MinWidth = 3, MaxHeight = 8 };

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
        List<(string? Name, Length Width)> observed = [];
        control.PropertyChanged += (_, eventArgs) =>
            observed.Add((eventArgs.PropertyName, control.Width));

        control.Width = Length.Cells(12);
        control.Width = Length.Cells(12);

        observed.ShouldBe([(nameof(ControlBase.Width), Length.Cells(12))]);
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

    /// <summary>Verifies an ancestor's IsEnabled change publishes the derived properties it flips
    /// on itself and on every descendant whose derived value actually changed, and stays silent
    /// where nothing changed (see #241).</summary>
    [Fact]
    public void IsEnabled_WhenAncestorChanges_PublishesDerivedPropertiesOnAffectedDescendants()
    {
        var parent = new ProbeContainer();
        var focusableChild = new ProbeControl { Focusable = true };
        var alreadyDisabledChild = new ProbeControl { Focusable = true, IsEnabled = false };
        parent.Children.Add(focusableChild);
        parent.Children.Add(alreadyDisabledChild);
        List<string?> parentEvents = [];
        List<string?> childEvents = [];
        List<string?> alreadyDisabledEvents = [];
        parent.PropertyChanged += (_, eventArgs) => parentEvents.Add(eventArgs.PropertyName);
        focusableChild.PropertyChanged += (_, eventArgs) => childEvents.Add(eventArgs.PropertyName);
        alreadyDisabledChild.PropertyChanged += (_, eventArgs) => alreadyDisabledEvents.Add(eventArgs.PropertyName);

        parent.IsEnabled = false;

        parentEvents.ShouldBe([nameof(ControlBase.IsEnabled), nameof(ControlBase.EffectiveIsEnabled)]);
        childEvents.ShouldBe([
            nameof(ControlBase.EffectiveIsEnabled),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.IsTabStop)
        ]);
        alreadyDisabledEvents.ShouldBeEmpty();
    }

    /// <summary>Verifies a Visibility change publishes the derived properties it flips, leaving
    /// the unrelated EffectiveIsEnabled axis untouched (see #241).</summary>
    [Fact]
    public void Visibility_WhenAncestorChanges_PublishesDerivedPropertiesWithoutTouchingEnabledAxis()
    {
        var parent = new ProbeContainer();
        var child = new ProbeControl { Focusable = true };
        parent.Children.Add(child);
        List<string?> childEvents = [];
        child.PropertyChanged += (_, eventArgs) => childEvents.Add(eventArgs.PropertyName);

        parent.Visibility = Visibility.Collapsed;

        childEvents.ShouldBe([
            nameof(ControlBase.EffectiveIsVisible),
            nameof(ControlBase.CanFocus),
            nameof(ControlBase.IsTabStop)
        ]);
        child.EffectiveIsEnabled.ShouldBeTrue();
    }

    /// <summary>Verifies a throwing visibility subscriber cannot strand manager ownership.</summary>
    [Fact]
    public async Task Visibility_WhenPropertySubscriberThrows_ReleasesFocusAndCaptureBeforeRethrowAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { Focusable = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            focus.Focus(child).ShouldBeTrue();
            child.CaptureProbePointer().ShouldBeTrue();
            var expected = new InvalidOperationException("The visibility subscriber failed.");
            ControlBase? observedFocus = null;
            ControlBase? observedCapture = null;
            child.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(ControlBase.Visibility))
                {
                    return;
                }

                observedFocus = focus.Focused;
                observedCapture = capture.Captured;
                throw expected;
            };

            var exception = Should.Throw<InvalidOperationException>(() => child.Visibility = Visibility.Collapsed);

            exception.ShouldBeSameAs(expected);
            child.Visibility.ShouldBe(Visibility.Collapsed);
            observedFocus.ShouldBeNull();
            observedCapture.ShouldBeNull();
            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies cleanup failure remains authoritative while enabled-state publication completes.</summary>
    [Fact]
    public async Task IsEnabled_WhenCleanupAndPropertySubscriberThrow_CompletesCleanupAndPreservesFirstFailureAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer();
            var child = new ProbeControl { Focusable = true, ThrowOnPointerCaptureCancellation = true };
            root.Children.Add(child);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var capture = new PointerManager(root);
            focus.Focus(child).ShouldBeTrue();
            child.CaptureProbePointer().ShouldBeTrue();
            var propertySubscriberRan = false;
            child.PropertyChanged += (_, eventArgs) =>
            {
                if (eventArgs.PropertyName != nameof(ControlBase.IsEnabled))
                {
                    return;
                }

                propertySubscriberRan = true;
                focus.Focused.ShouldBeNull();
                capture.Captured.ShouldBeNull();
                throw new InvalidOperationException("The enabled subscriber failed.");
            };

            var exception = Should.Throw<InvalidOperationException>(() => child.IsEnabled = false);

            exception.Message.ShouldBe("The probe capture-cancellation callback failed.");
            child.IsEnabled.ShouldBeFalse();
            propertySubscriberRan.ShouldBeTrue();
            focus.Focused.ShouldBeNull();
            capture.Captured.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
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

    /// <summary>Verifies hit-test policy mutation is dispatcher-affine.</summary>
    [Fact]
    public async Task IsHitTestVisible_WhenAttachedAndSetOffThread_ThrowsBeforeMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(() => control.IsHitTestVisible = false);

        control.IsHitTestVisible.ShouldBeTrue();
    }

    /// <summary>Verifies the public Invalidate() requests only Render, leaving composition-based
    /// code that does not own a subclass able to request a repaint without the protected
    /// measure/arrange seam (see #226).</summary>
    [Fact]
    public void Invalidate_WhenCalled_RequestsRenderOnly()
    {
        var control = new ProbeControl();
        control.Clear(Invalidation.All);

        control.Invalidate();

        control.Pending.ShouldBe(Invalidation.Render);
    }

    /// <summary>Verifies the public Invalidate() is dispatcher-affine, matching every other public
    /// mutation seam instead of silently deferring (see #226).</summary>
    [Fact]
    public async Task Invalidate_WhenAttachedAndCalledOffThread_ThrowsAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var control = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => control.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<InvalidOperationException>(control.Invalidate);
    }

    /// <summary>Verifies invalid enums and disposed access fail before mutation.</summary>
    [Fact]
    public void Setter_WhenStateIsInvalid_ThrowsDocumentedException()
    {
        var control = new ProbeControl();

        _ = Should.Throw<ArgumentOutOfRangeException>(() => control.Visibility = (Visibility) int.MaxValue);
        _ = Should.Throw<ArgumentOutOfRangeException>(() =>
            control.HorizontalAlignment = (HorizontalAlignment) int.MaxValue);
        control.Dispose();
        _ = Should.Throw<ObjectDisposedException>(() => control.IsEnabled = false);
    }
}
