// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Verifies shared interaction-plane discovery and focus-relative candidate traversal.</summary>
public sealed class InteractionPlaneCandidateWalkerTests
{
    /// <summary>Verifies included modal roots retain insertion order and traversal starts after
    /// the candidate containing focus before wrapping across the complete plane.</summary>
    [Fact]
    public async Task VisitAfterFocus_WhenModalPlaneHasIncludedRoot_WrapsInSharedDeterministicOrderAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var primary = new Stack();
            var firstFocus = new TextInput();
            var first = new GroupBox { HeaderText = "First", Content = firstFocus };
            primary.Children.Add(first);
            var included = new Stack();
            var second = new GroupBox { HeaderText = "Second", Content = new TextInput() };
            included.Children.Add(second);
            root.Children.Add(primary);
            root.Children.Add(included);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(primary, initialFocus: firstFocus);
            scope.Include(included);
            var walker = new InteractionPlaneCandidateWalker(root, focus, modality);
            var candidates = walker.Collect(static (control, _) => control as GroupBox);
            var visited = new List<GroupBox>();

            walker.VisitAfterFocus(
                candidates,
                static _ => true,
                candidate =>
                {
                    visited.Add(candidate);
                    return false;
                }).ShouldBeFalse();

            visited.ShouldBe([second, first]);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies a candidate reparented after discovery is skipped rather than invoked
    /// from an ordering snapshot that no longer describes its ownership position.</summary>
    [Fact]
    public async Task VisitAfterFocus_WhenEarlierCallbackReparentsLaterCandidate_SkipsStaleCandidateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var primary = new Stack();
            var destination = new Stack();
            var first = new Button { Text = "First" };
            var stale = new Button { Text = "Stale" };
            var last = new Button { Text = "Last" };
            primary.Children.Add(first);
            primary.Children.Add(stale);
            primary.Children.Add(last);
            root.Children.Add(primary);
            root.Children.Add(destination);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var scope = modality.Enter(primary);
            focus.Focus(null).ShouldBeTrue();
            var walker = new InteractionPlaneCandidateWalker(root, focus, modality);
            var candidates = walker.Collect(static (control, _) => control as Button);
            var visited = new List<Button>();

            walker.VisitAfterFocus(
                candidates,
                static _ => true,
                candidate =>
                {
                    visited.Add(candidate);

                    if (ReferenceEquals(candidate, first))
                    {
                        primary.Children.Remove(stale).ShouldBeTrue();
                        destination.Children.Add(stale);
                    }

                    return false;
                }).ShouldBeFalse();

            visited.ShouldBe([first, last]);
            stale.Parent.ShouldBeSameAs(destination);
        }, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies replacing the active modal plane from a candidate callback prevents the
    /// remaining snapshot from invoking controls that are now outside the interaction plane.</summary>
    [Fact]
    public async Task VisitAfterFocus_WhenCallbackReplacesModalPlane_SkipsFormerPlaneCandidatesAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            using var root = new Stack();
            var original = new Stack();
            var first = new Button { Text = "First" };
            var stale = new Button { Text = "Stale" };
            original.Children.Add(first);
            original.Children.Add(stale);
            var replacementRoot = new Stack { Children = { new Button { Text = "Replacement" } } };
            root.Children.Add(original);
            root.Children.Add(replacementRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            var originalScope = modality.Enter(original);
            focus.Focus(null).ShouldBeTrue();
            var walker = new InteractionPlaneCandidateWalker(root, focus, modality);
            var candidates = walker.Collect(static (control, _) => control as Button);
            var visited = new List<Button>();
            ModalScope? replacementScope = null;

            try
            {
                walker.VisitAfterFocus(
                    candidates,
                    static _ => true,
                    candidate =>
                    {
                        visited.Add(candidate);
                        originalScope.Dispose();
                        replacementScope = modality.Enter(replacementRoot);
                        return false;
                    }).ShouldBeFalse();

                visited.ShouldBe([first]);
            }
            finally
            {
                replacementScope?.Dispose();
                originalScope.Dispose();
            }
        }, TestContext.Current.CancellationToken);
    }
}
