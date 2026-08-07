// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls;

using System.Reflection;

/// <summary>Verifies the retained private-tree component authoring role.</summary>
public sealed class CompositeControlTests
{
    /// <summary>Verifies the role is an honest direct base with a protected one-shot contract.</summary>
    [Fact]
    public void Type_WhenInspected_ExposesPrivateRetainedComposition()
    {
        var content = typeof(CompositeControlBase).GetProperty(
            "Content",
            BindingFlags.Instance | BindingFlags.NonPublic);
        var initialize = typeof(CompositeControlBase).GetMethod(
            "InitializeContent",
            BindingFlags.Instance | BindingFlags.NonPublic);

        typeof(CompositeControlBase).IsPublic.ShouldBeTrue();
        typeof(CompositeControlBase).IsAbstract.ShouldBeTrue();
        typeof(CompositeControlBase).BaseType.ShouldBe(typeof(ControlBase));
        typeof(Container).IsAssignableFrom(typeof(CompositeControlBase)).ShouldBeFalse();
        typeof(CompositeControlBase).GetProperty("Children").ShouldBeNull();
        typeof(CompositeControlBase).GetProperty("Content").ShouldBeNull();
        _ = content.ShouldNotBeNull();
        content.PropertyType.ShouldBe(typeof(ControlBase));
        content.GetMethod!.IsFamily.ShouldBeTrue();
        content.GetMethod.IsVirtual.ShouldBeFalse();
        content.SetMethod.ShouldBeNull();
        _ = initialize.ShouldNotBeNull();
        initialize.IsFamily.ShouldBeTrue();
        initialize.IsVirtual.ShouldBeFalse();
        initialize.ReturnType.ShouldBe(typeof(void));
        initialize.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe([typeof(ControlBase)]);
    }

    /// <summary>Verifies one successful initialization commits the private composition-root edge permanently.</summary>
    [Fact]
    public void InitializeContent_WhenCalledOnce_CommitsOnePrivateRootAndRejectsReinitialization()
    {
        var root = new ProbeControl();
        var replacement = new ProbeControl();
        var owner = new ProbeCompositeControl();

        owner.Initialize(root);

        owner.ExposedContent.ShouldBeSameAs(root);
        root.Parent.ShouldBeSameAs(owner);
        root.OwningSlot.ShouldNotBeNull().Options.Role.ShouldBe(OwnedControlRole.CompositionRoot);
        root.OwningSlot.Options.Layer.ShouldBe(OwnedControlLayer.Normal);
        root.OwningSlot.Options.ParticipatesInHitTesting.ShouldBeTrue();
        root.OwningSlot.Options.ParticipatesInNavigation.ShouldBeTrue();
        root.OwningSlot.Options.Impact.ShouldBe(InvalidationImpact.Measure);
        root.OwningSlot.Capacity.ShouldBe(1);

        _ = Should.Throw<InvalidOperationException>(() => owner.Initialize(root));
        _ = Should.Throw<InvalidOperationException>(() => owner.Initialize(replacement));

        owner.ExposedContent.ShouldBeSameAs(root);
        root.Parent.ShouldBeSameAs(owner);
        replacement.Parent.ShouldBeNull();
    }

    /// <summary>Verifies every pre-commit rejection leaves initialization available for a later valid root.</summary>
    [Fact]
    public void InitializeContent_WhenCandidateIsInvalid_DoesNotConsumeInitialization()
    {
        var owner = new ProbeCompositeControl();
        var other = new ProbeOwnedControl();
        var owned = new ProbeControl();
        var disposed = new ProbeControl();
        var valid = new ProbeControl();
        other.AddPrimary(owned);
        disposed.Dispose();

        _ = Should.Throw<ArgumentNullException>(() => owner.Initialize(null!));
        _ = Should.Throw<ArgumentException>(() => owner.Initialize(owner));
        _ = Should.Throw<ArgumentException>(() => owner.Initialize(owned));
        _ = Should.Throw<ObjectDisposedException>(() => owner.Initialize(disposed));

        owner.Initialize(valid);

        owner.ExposedContent.ShouldBeSameAs(valid);
        valid.Parent.ShouldBeSameAs(owner);
        owned.Parent.ShouldBeSameAs(other);
    }

    /// <summary>Verifies an independently attached candidate is rejected without consuming initialization.</summary>
    [Fact]
    public async Task InitializeContent_WhenCandidateIsAttached_DoesNotConsumeInitializationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeCompositeControl();
        var attached = new ProbeControl();
        var valid = new ProbeControl();
        await dispatcher.InvokeAsync(
            () => attached.Attach(dispatcher),
            TestContext.Current.CancellationToken);

        _ = Should.Throw<ArgumentException>(() => owner.Initialize(attached));

        owner.Initialize(valid);

        owner.ExposedContent.ShouldBeSameAs(valid);
        attached.Dispatcher.ShouldBeSameAs(dispatcher);
        attached.Parent.ShouldBeNull();
    }

    /// <summary>Verifies a callback failure after edge commit consumes initialization and exposes the committed root.</summary>
    [Fact]
    public void InitializeContent_WhenParentCallbackThrows_PreservesCommittedPermanentRoot()
    {
        var owner = new ProbeCompositeControl();
        var root = new OwnershipObserverControl();
        var replacement = new ProbeControl();
        var callbackObservedRoot = false;
        root.ParentChanging = (_, previous, current) =>
        {
            previous.ShouldBeNull();
            current.ShouldBeSameAs(owner);
            owner.ExposedContent.ShouldBeSameAs(root);
            callbackObservedRoot = true;
            throw new InvalidOperationException("The initialization callback failed.");
        };

        var exception = Should.Throw<InvalidOperationException>(() => owner.Initialize(root));

        exception.Message.ShouldBe("The initialization callback failed.");
        callbackObservedRoot.ShouldBeTrue();
        owner.ExposedContent.ShouldBeSameAs(root);
        root.Parent.ShouldBeSameAs(owner);
        _ = Should.Throw<InvalidOperationException>(() => owner.Initialize(replacement));
        replacement.Parent.ShouldBeNull();
    }

    /// <summary>Verifies an incomplete composite cannot enter a tree or receive a dispatcher.</summary>
    [Fact]
    public async Task Attach_WhenContentWasNotInitialized_RejectsBeforeContextMutationAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var owner = new ProbeCompositeControl();

        await dispatcher.InvokeAsync(() =>
        {
            var exception = Should.Throw<InvalidOperationException>(() => owner.Attach(dispatcher));

            exception.Message.ShouldContain("composition root");
            owner.Dispatcher.ShouldBeNull();
            owner.Parent.ShouldBeNull();
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies an incomplete composite cannot enter a retained parent tree.</summary>
    [Fact]
    public void Add_WhenCompositeWasNotInitialized_RejectsBeforeOwnershipMutation()
    {
        var parent = new ProbeOwnedControl();
        var owner = new ProbeCompositeControl();

        var exception = Should.Throw<InvalidOperationException>(() => parent.AddPrimary(owner));

        exception.Message.ShouldContain("composition root");
        owner.Parent.ShouldBeNull();
        parent.PrimaryCount.ShouldBe(0);
    }

    /// <summary>Verifies layout never lazily creates or silently accepts a missing implementation tree.</summary>
    [Fact]
    public void Layout_WhenContentWasNotInitialized_RejectsWithoutOwnershipMutation()
    {
        var owner = new ProbeCompositeControl();

        var exception = Should.Throw<InvalidOperationException>(() =>
            new LayoutEngine().Layout(owner, new Size(8, 2)));

        exception.Message.ShouldContain("composition root");
        owner.Parent.ShouldBeNull();
        owner.OwnedControlCount.ShouldBe(0);
    }

    /// <summary>Verifies first layout uses the constructor-owned tree without mutating it or repeating work.</summary>
    [Fact]
    public void Layout_WhenContentIsInitialized_UsesRetainedRootWithoutRedundantPass()
    {
        var root = new ProbeControl(new Size(3, 1)) { Margin = new Thickness(left: 1, top: 2, right: 3, bottom: 4) };
        var owner = new ProbeCompositeControl(root)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var slot = root.OwningSlot;
        var engine = new LayoutEngine();

        engine.Layout(owner, new Size(20, 10));
        engine.Layout(owner, new Size(20, 10));

        owner.DesiredSize.ShouldBe(new Size(7, 7));
        root.Bounds.ShouldBe(new Rect(1, 2, 16, 4));
        root.MeasureConstraints.ShouldBe([new Constraint(16, 4)]);
        root.ArrangeBounds.ShouldBe([root.Bounds]);
        root.Parent.ShouldBeSameAs(owner);
        root.OwningSlot.ShouldBeSameAs(slot);
    }

    /// <summary>Verifies collapsed content contributes no margin and clears stale geometry.</summary>
    [Fact]
    public void Layout_WhenContentBecomesCollapsed_ExcludesItWithoutReplacingTheRoot()
    {
        var root = new ProbeControl(new Size(4, 2)) { Margin = new Thickness(1) };
        var owner = new ProbeCompositeControl(root);
        var engine = new LayoutEngine();
        engine.Layout(owner, new Size(10, 4));
        var measures = root.MeasureConstraints.Count;
        var arrangements = root.ArrangeBounds.Count;

        root.Visibility = Visibility.Collapsed;
        engine.Layout(owner, new Size(10, 4));

        owner.DesiredSize.ShouldBe(default);
        root.DesiredSize.ShouldBe(default);
        root.Bounds.ShouldBe(default);
        root.MeasureConstraints.Count.ShouldBe(measures);
        root.ArrangeBounds.Count.ShouldBe(arrangements);
        owner.ExposedContent.ShouldBeSameAs(root);
    }

    /// <summary>Verifies normal registry traversal renders and hit-tests the private root.</summary>
    [Fact]
    public void RenderAndHitTest_WhenContentIsVisible_UsesOwnedTraversal()
    {
        var root = new ProbeControl(new Size(1, 1)) { Content = "X".AsMemory() };
        var owner = new ProbeCompositeControl(root);
        new LayoutEngine().Layout(owner, new Size(1, 1));
        using Frame frame = new(new Size(1, 1));

        owner.Render(frame.Canvas);

        FrameOracle.Get(frame, default).ShouldBe("X");
        root.RenderCalls.ShouldBe(1);
        owner.HitTest(default).ShouldBeSameAs(root);
    }

    /// <summary>Verifies the private root participates in focus navigation through slot metadata.</summary>
    [Fact]
    public async Task MoveNext_WhenContentCanFocus_NavigatesToPrivateRootAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var root = new ProbeControl { Focusable = true };
        var owner = new ProbeCompositeControl(root);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher);
            using FocusManager focus = new(owner);

            focus.MoveNext().ShouldBeTrue();
            focus.Focused.ShouldBeSameAs(root);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies elevated popup discovery descends through the private root.</summary>
    [Fact]
    public void HitTest_WhenContentContainsPopupBranch_UsesPopupTraversal()
    {
        var popup = new PopupHitProbe();
        var owner = new ProbeCompositeControl(popup);

        var hit = owner.HitTest(default);

        hit.ShouldBeSameAs(popup);
        popup.PopupHitTestCalls.ShouldBe(1);
    }

    /// <summary>Verifies dispatcher, Unicode, theme, focus, and capture context follow the composition edge.</summary>
    [Fact]
    public async Task Attach_WhenContentIsInitialized_PropagatesCompleteInheritedContextAsync()
    {
        await using var dispatcher = Dispatcher.Start();
        var policy = new UnicodePolicy(Ambiguous.Wide);
        var context = new Theme();
        var root = new OwnershipObserverControl();
        var owner = new ProbeCompositeControl(root);

        await dispatcher.InvokeAsync(() =>
        {
            owner.Attach(dispatcher, policy);
            owner.PropagateTheme(context);
            using FocusManager focus = new(owner);
            using PointerManager capture = new(owner);

            root.Dispatcher.ShouldBeSameAs(dispatcher);
            root.InheritedCellPolicy.ShouldBeSameAs(policy);
            root.InheritedThemeValue.ShouldBeSameAs(context);
            root.InheritedFocusOwner.ShouldBeSameAs(focus);
            root.InheritedCaptureOwner.ShouldBeSameAs(capture);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies direct root disposal permanently empties rather than reopening the component.</summary>
    [Fact]
    public void Dispose_WhenContentIsDisposedDirectly_PreventsReinitializationAndAttachment()
    {
        var root = new ProbeControl();
        var replacement = new ProbeControl();
        var owner = new ProbeCompositeControl(root);

        root.Dispose();

        root.Disposed.ShouldBeTrue();
        root.Parent.ShouldBeNull();
        _ = Should.Throw<InvalidOperationException>(() => _ = owner.ExposedContent);
        _ = Should.Throw<InvalidOperationException>(() => owner.Initialize(replacement));
        _ = Should.Throw<InvalidOperationException>(owner.ValidateAttachment);
        replacement.Parent.ShouldBeNull();
    }

    /// <summary>Verifies owner disposal owns the retained root exactly once.</summary>
    [Fact]
    public void Dispose_WhenOwnerIsDisposed_DisposesRetainedRootExactlyOnce()
    {
        var root = new ProbeControl();
        var owner = new ProbeCompositeControl(root);

        owner.Dispose();
        owner.Dispose();

        owner.Disposed.ShouldBeTrue();
        root.Disposed.ShouldBeTrue();
        root.DisposingCalls.ShouldBe(1);
        root.Parent.ShouldBeNull();
    }
}
