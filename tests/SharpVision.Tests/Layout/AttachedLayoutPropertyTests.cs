// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Layout;

using System.Runtime.CompilerServices;

/// <summary>Verifies weak attached layout-property storage and parent-scoped invalidation.</summary>
public sealed class AttachedLayoutPropertyTests
{
    /// <summary>Verifies detached and wrong-parent values persist without dirtying unrelated
    /// controls, while reparenting makes the current eligible owner the sole invalidation target.</summary>
    [Fact]
    public void Set_WhenParentEligibilityChanges_InvalidatesOnlyTheCurrentMatchingOwner()
    {
        AttachedLayoutProperty<Dock, int> property = new(0, InvalidationImpact.Measure);
        var child = new ProbeControl();
        var wrongParent = new Overlay();
        var firstOwner = new Dock();
        var secondOwner = new Dock();

        property.Set(child, 1);
        wrongParent.Children.Add(child);
        wrongParent.Clear(Invalidation.All);
        property.Set(child, 2);
        wrongParent.Pending.ShouldBe(Invalidation.None);
        _ = wrongParent.Children.Remove(child);
        firstOwner.Children.Add(child);
        firstOwner.Clear(Invalidation.All);
        property.Set(child, 3);
        _ = firstOwner.Children.Remove(child);
        secondOwner.Children.Add(child);
        secondOwner.Clear(Invalidation.All);
        property.Set(child, 4);

        property.Get(child).ShouldBe(4);
        firstOwner.Pending.ShouldBe(Invalidation.All);
        secondOwner.Pending.ShouldBe(Invalidation.All);
    }

    /// <summary>Verifies validation and mutability checks complete before storage changes, and an
    /// equal write remains silent.</summary>
    [Fact]
    public void Set_WhenRejectedOrUnchanged_PreservesStoredValueAndInvalidation()
    {
        AttachedLayoutProperty<Dock, int> property = new(
            0,
            InvalidationImpact.Render,
            static (_, value) => ArgumentOutOfRangeException.ThrowIfNegative(value));
        var owner = new Dock();
        var child = new ProbeControl();
        owner.Children.Add(child);
        property.Set(child, 2);
        owner.Clear(Invalidation.All);

        _ = Should.Throw<ArgumentOutOfRangeException>(() => property.Set(child, -1));
        property.Set(child, 2);

        property.Get(child).ShouldBe(2);
        owner.Pending.ShouldBe(Invalidation.None);

        child.Dispose();
        _ = Should.Throw<ObjectDisposedException>(() => property.Set(child, 3));
        property.Get(child).ShouldBe(2);
    }

    /// <summary>Verifies weak storage does not keep a detached control alive.</summary>
    [Fact]
    public void Storage_WhenDetachedControlLosesStrongReferences_DoesNotRootControl()
    {
        AttachedLayoutProperty<Dock, int> property = new(0, InvalidationImpact.Measure);
        var reference = CreateStoredControl(property);

        for (var attempt = 0; attempt < 5 && reference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        reference.IsAlive.ShouldBeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateStoredControl(AttachedLayoutProperty<Dock, int> property)
    {
        var control = new ProbeControl();
        property.Set(control, 1);
        return new WeakReference(control);
    }
}
