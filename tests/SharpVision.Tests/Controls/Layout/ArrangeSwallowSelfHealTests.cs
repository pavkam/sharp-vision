// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies a parent that measures a child during its own Arrange but then does not
/// actually arrange it - a deliberate contract violation, not a shipped panel's behavior; Grid
/// and TablePresenter both arrange every child they measure - does not permanently strand that
/// child's pending Arrange request. <see cref="ControlBase.Invalidate(Invalidation)"/> swallows
/// the child's upward propagation on the trust that an arranging parent will arrange it in the
/// same transaction; these tests exercise the self-heal that covers the rare case where that
/// trust is not honored.
///
/// A child's own Pending already starts dirty for Arrange at construction and only clears once
/// it has actually been arranged, so the swallow can only ever matter for a child re-dirtied
/// after already settling once - every test here first lays out normally to settle the tree,
/// then drives a second pass that skips a child mid-arrange.</summary>
public sealed class ArrangeSwallowSelfHealTests
{
    /// <summary>Verifies the skipped child's own pending Arrange bit survives the second pass
    /// (it was never cleared, since it was never arranged) and an ancestor above the arranging
    /// parent picks up a fresh pending Arrange request - the propagation the child's own
    /// Invalidate call could not deliver because it was swallowed. A sibling the panel does
    /// arrange is unaffected: its pending bit clears normally.</summary>
    [Fact]
    public void Arrange_WhenAnArrangingParentMeasuresButSkipsAChild_RecordsAPendingArrangeOnAnAncestor()
    {
        var root = new ArrangeSkippingPanel();
        var inner = new ArrangeSkippingPanel();
        var target = new ProbeControl();
        var sibling = new ProbeControl();
        inner.Children.Add(target);
        inner.Children.Add(sibling);
        root.Children.Add(inner);
        var engine = new LayoutEngine();

        engine.Layout(root, new Size(10, 4));

        // Second pass: the panel now skips target, and its own remeasure of target uses a
        // constraint that genuinely differs from what settled during the first pass - like
        // Grid's finite-track remeasure resolving to a width its own initial measure never saw -
        // so target's trailing Invalidate(Arrange) is a fresh announcement, not a repeat of one
        // already recorded. Re-dirtying inner directly is what makes this pass reach Arrange at
        // all: nothing else changed since the settled first pass.
        inner.SkippedChild = target;
        inner.ArrangeRemeasureWidth = 99;
        inner.Invalidate(Invalidation.Arrange);
        engine.Layout(root, new Size(10, 4));

        (target.Pending & Invalidation.Arrange).ShouldBe(Invalidation.Arrange);
        (sibling.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
        (root.Pending & Invalidation.Arrange).ShouldBe(Invalidation.Arrange);
    }

    /// <summary>Verifies the self-healed ancestor pending is not inert bookkeeping: because
    /// <see cref="LayoutEngine.Layout"/> re-walks from the root every call, and Arrange only
    /// short-circuits when its own pending Arrange bit is clear, the root's recovered pending
    /// bit is exactly what lets a third pass reach back down and arrange the previously-skipped
    /// child once the parent stops skipping it.</summary>
    [Fact]
    public void Arrange_WhenALaterPassStopsSkippingTheChild_ArrangesItUsingTheRecoveredPending()
    {
        var root = new ArrangeSkippingPanel();
        var inner = new ArrangeSkippingPanel();
        var target = new ProbeControl();
        inner.Children.Add(target);
        root.Children.Add(inner);
        var engine = new LayoutEngine();

        engine.Layout(root, new Size(10, 4));
        var settledBounds = target.Bounds;

        inner.SkippedChild = target;
        inner.ArrangeRemeasureWidth = 99;
        inner.Invalidate(Invalidation.Arrange);
        engine.Layout(root, new Size(10, 4));
        target.Bounds.ShouldBe(settledBounds, "a skipped child's bounds do not move on their own");

        inner.SkippedChild = null;
        engine.Layout(root, new Size(10, 4));

        target.Bounds.ShouldBe(new Rect(0, 0, 10, 4));
        (target.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
    }

    /// <summary>Control case: a panel that arranges every child it measures - the contract every
    /// shipped container honors - never needs the self-heal. Even though the second pass's
    /// remeasure genuinely differs from the first (the same condition that triggers the swallow
    /// in the tests above), arranging the child right afterward clears its pending Arrange bit
    /// before this panel's own Arrange finishes, so no ancestor is left holding one.</summary>
    [Fact]
    public void Arrange_WhenAnArrangingParentArrangesEveryMeasuredChild_LeavesNoPendingArrangeBehind()
    {
        var root = new ArrangeSkippingPanel();
        var inner = new ArrangeSkippingPanel();
        var first = new ProbeControl();
        var second = new ProbeControl();
        inner.Children.Add(first);
        inner.Children.Add(second);
        root.Children.Add(inner);
        var engine = new LayoutEngine();

        engine.Layout(root, new Size(10, 4));

        inner.ArrangeRemeasureWidth = 99;
        inner.Invalidate(Invalidation.Arrange);
        engine.Layout(root, new Size(10, 4));

        (first.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
        (second.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
        (inner.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
        (root.Pending & Invalidation.Arrange).ShouldBe(Invalidation.None);
    }

    /// <summary>Measures every child, then arranges every child except
    /// <see cref="SkippedChild"/>, which is left null in the common case so the panel behaves
    /// like an ordinary container that honors the contract <see
    /// cref="ControlBase.Invalidate(Invalidation)"/> trusts.</summary>
    private sealed class ArrangeSkippingPanel: Container
    {
        /// <summary>Fills its assigned slot regardless of children's intrinsic size, so the probe
        /// children below - none of which report real content - still land at deterministic,
        /// non-zero bounds.</summary>
        internal ArrangeSkippingPanel()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
        }

        /// <summary>Gets or sets the one child measured during Arrange but deliberately not
        /// arranged.</summary>
        internal ControlBase? SkippedChild { get; set; }

        /// <summary>Gets or sets the width Arrange hands each child's remeasure, overriding the
        /// assigned bounds width. Real panels naturally remeasure against a width their own
        /// initial pass never saw - Grid's resolved finite-track width, for one; this test tree
        /// has no such source of difference on its own, so a later pass sets this explicitly to
        /// produce one.</summary>
        internal int? ArrangeRemeasureWidth { get; set; }

        protected override Size MeasureOverride(Constraint constraint)
        {
            var size = default(Size);

            foreach (var child in Children)
            {
                var childSize = MeasureChild(child, constraint);
                size = new Size(
                    Math.Max(size.Width, childSize.Width),
                    Math.Max(size.Height, childSize.Height));
            }

            return size;
        }

        protected override void ArrangeOverride(Rect bounds)
        {
            var remeasureConstraint = new Constraint(ArrangeRemeasureWidth ?? bounds.Width, bounds.Height);

            foreach (var child in Children)
            {
                _ = MeasureChild(child, remeasureConstraint);

                if (!ReferenceEquals(child, SkippedChild))
                {
                    ArrangeChild(child, bounds, ResolvedAxes.Both);
                }
            }
        }
    }
}
