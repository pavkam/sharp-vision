// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Input;

/// <summary>Proves modal state isolation with fixed-seed valid lifetime and interaction sequences.</summary>
public sealed partial class ModalityManagerTests
{
    private const int _caseCount = 16;
    private const int _maximumScopes = 6;
    private const int _stepCount = 48;

    #region Named regression

    /// <summary>Verifies exiting a disjoint child scope reconfines all retained pointer state to its parent.</summary>
    [Fact]
    public async Task Dispose_WhenChildPointerStateIsOutsideParentPlane_ClearsThatStateAsync()
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            var root = new ProbeContainer { Bounds = new Rect(0, 0, 30, 8) };
            var outerRoot = new ProbeControl
            {
                Bounds = new Rect(0, 0, 10, 6),
                IsFocusable = true,
            };
            var childRoot = new ProbeControl
            {
                Bounds = new Rect(16, 0, 10, 6),
                IsFocusable = true,
            };
            root.Children.Add(outerRoot);
            root.Children.Add(childRoot);
            root.Attach(dispatcher);
            using var focus = new FocusManager(root);
            using var pointer = new PointerManager(root);
            using var modality = new ModalityManager(root, focus, pointer);
            using var outer = modality.Enter(outerRoot, initialFocus: outerRoot);
            var child = modality.Enter(childRoot, initialFocus: childRoot);
            pointer.Capture(childRoot).ShouldBeTrue();
            var cells = new Point(18, 2);
            _ = pointer.Dispatch(new Pointer(
                cells,
                pixels: null,
                Buttons.None,
                PointerAction.Move,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: true,
                isCellPositionInferred: false));
            _ = pointer.Dispatch(new Pointer(
                cells,
                pixels: null,
                Buttons.Primary,
                PointerAction.Press,
                wheelX: 0,
                wheelY: 0,
                Modifiers.None,
                isMotion: false,
                isCellPositionInferred: false));

            child.Dispose();

            modality.Active.ShouldBeSameAs(outer);
            pointer.Captured.ShouldBeNull();
            pointer.Hovered.ShouldBeNull();
            pointer.PressOrigin.ShouldBeNull();
            childRoot.IsPointerOver.ShouldBeFalse();
        }, TestContext.Current.CancellationToken);
    }

    #endregion

    #region Randomized state machine

    /// <summary>Verifies every generated operation preserves the independently modeled active plane.</summary>
    [Theory]
    [InlineData(0x51A4_80D1)]
    [InlineData(0x27D1_5C0D)]
    [InlineData(0x0D15_C0DE)]
    public async Task State_WhenOperationsAreRandomized_RemainsInsideActivePlaneAsync(int seed)
    {
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            for (var sample = 0; sample < _caseCount; sample++)
            {
                RunCase(dispatcher, seed, sample);
            }
        }, TestContext.Current.CancellationToken);
    }

    private static void RunCase(Dispatcher dispatcher, int seed, int sample)
    {
        var random = new Random(unchecked(seed + (sample * 7_919)));
        var root = new ProbeContainer { Bounds = new Rect(0, 0, 60, 8) };
        var first = new ProbeContainer
        {
            Bounds = new Rect(0, 0, 12, 8),
            IsFocusable = true,
        };
        var firstNested = new ProbeControl
        {
            Bounds = new Rect(2, 2, 4, 3),
            IsFocusable = true,
        };
        var second = new ProbeContainer
        {
            Bounds = new Rect(14, 0, 12, 8),
            IsFocusable = true,
        };
        var secondNested = new ProbeControl
        {
            Bounds = new Rect(16, 2, 4, 3),
            IsFocusable = true,
        };
        var third = new ProbeControl
        {
            Bounds = new Rect(28, 0, 10, 8),
            IsFocusable = true,
        };
        var fourth = new ProbeControl
        {
            Bounds = new Rect(40, 0, 10, 8),
            IsFocusable = true,
        };
        first.Children.Add(firstNested);
        second.Children.Add(secondNested);
        root.Children.Add(first);
        root.Children.Add(second);
        root.Children.Add(third);
        root.Children.Add(fourth);
        root.Attach(dispatcher);
        var controls = new List<ControlBase>
        {
            first,
            firstNested,
            second,
            secondNested,
            third,
            fourth,
        };
        var points = new Dictionary<ControlBase, Point>(ReferenceEqualityComparer.Instance)
        {
            [first] = new Point(1, 1),
            [firstNested] = new Point(3, 3),
            [second] = new Point(15, 1),
            [secondNested] = new Point(17, 3),
            [third] = new Point(32, 3),
            [fourth] = new Point(44, 3),
            [root] = new Point(55, 6),
        };
        using var focus = new FocusManager(root);
        using var pointer = new PointerManager(root);
        using var modality = new ModalityManager(root, focus, pointer);
        var handles = new List<ModalScope>();
        var expectedStack = new List<ModalScope>();
        var roots = new Dictionary<ModalScope, List<ControlBase>>(ReferenceEqualityComparer.Instance);
        var policies = new Dictionary<ModalScope, OutsideInteraction>(ReferenceEqualityComparer.Instance);
        var inactive = new HashSet<ModalScope>(ReferenceEqualityComparer.Instance);
        var dismissedScopes = new List<ModalScope>();
        var routedTargets = new List<ControlBase>();
        var recent = new Queue<string>();
        var dismissalCallbacks = 0;
        var qualifyingDismissInputs = 0;
        var qualifyingIgnoreInputs = 0;
        var dismissingPresses = 0;
        var dismissingWheels = 0;
        var entriesAfterInactiveHandles = 0;
        var entriesAfterHistoricalLimit = 0;

        foreach (var control in controls.Prepend(root))
        {
            var observed = control;
            _ = observed.AddHandler(Events.Pointer, (_, eventArgs) =>
            {
                if (eventArgs.Phase == RoutingPhase.Bubble &&
                    ReferenceEquals(eventArgs.OriginalSource, observed))
                {
                    routedTargets.Add(observed);
                }
            });
        }

        for (var step = 0; step < _stepCount; step++)
        {
            if (step == 0)
            {
                Enter(first, OutsideInteraction.Ignore, "Enter(first)");
            }
            else if (step == 1)
            {
                Include(third, "Include(third)");
            }
            else if (step == 2)
            {
                Focus(firstNested, "Focus(firstNested)");
            }
            else if (step == 3)
            {
                Capture(third, "Capture(third)");
            }
            else if (step == 4)
            {
                Dispatch(fourth, PointerAction.Move, "Move(fourth)");
            }
            else if (step == 5)
            {
                Dispatch(firstNested, PointerAction.Press, "Press(firstNested)");
            }
            else if (step == 6)
            {
                Dispatch(fourth, PointerAction.Wheel, "Wheel(fourth)");
            }
            else if (step == 7)
            {
                Enter(firstNested, OutsideInteraction.Dismiss, "NestedEnter(firstNested)");
            }
            else if (step == 8)
            {
                DispatchScriptedDismissingPress();
            }
            else if (step == 9)
            {
                Dispatch(fourth, PointerAction.Wheel, "IgnoreWheel(fourth)");
            }
            else if (step == 10)
            {
                Hide(firstNested, "Hide(firstNested)");
            }
            else if (step == 11)
            {
                Dispose(handles[0], "Dispose(first scope)");
            }
            else if (step == 12)
            {
                Enter(second, OutsideInteraction.Dismiss, "Enter(second dismiss)");
            }
            else if (step == 13)
            {
                Dispatch(fourth, PointerAction.Wheel, "DismissWheel(fourth)");
            }
            else if (step == 14)
            {
                Enter(third, OutsideInteraction.Ignore, "Enter(third churn)");
            }
            else if (step == 15)
            {
                Dispose(handles[^1], "Dispose(third churn)");
            }
            else if (step == 16)
            {
                Enter(fourth, OutsideInteraction.Ignore, "Enter(fourth churn)");
            }
            else if (step == 17)
            {
                Dispose(handles[^1], "Dispose(fourth churn)");
            }
            else if (step == 18)
            {
                Enter(second, OutsideInteraction.Ignore, "Enter(second churn)");
            }
            else if (step == 19)
            {
                Dispose(handles[^1], "Dispose(second churn)");
            }
            else if (step == 20)
            {
                ApplyRandom(operation: 0);
            }
            else if (step == 21)
            {
                Dispose(handles[^1], "Dispose(post-limit churn)");
            }
            else
            {
                ApplyRandom(random.Next(10));
            }

            AssertInvariants(step);
        }

        if (expectedStack.Count > 0)
        {
            var oldest = expectedStack[0];
            Record("FinalDispose(oldest)");
            oldest.Dispose();
            expectedStack.Clear();
        }

        foreach (var handle in handles)
        {
            handle.IsActive.ShouldBeFalse(Context(_stepCount));
        }

        dismissalCallbacks.ShouldBe(qualifyingDismissInputs, Context(_stepCount));
        dismissingPresses.ShouldBeGreaterThan(0, Context(_stepCount));
        dismissingWheels.ShouldBeGreaterThan(0, Context(_stepCount));
        qualifyingIgnoreInputs.ShouldBeGreaterThan(0, Context(_stepCount));
        entriesAfterInactiveHandles.ShouldBeGreaterThan(0, Context(_stepCount));
        entriesAfterHistoricalLimit.ShouldBeGreaterThan(0, Context(_stepCount));
        handles.Count.ShouldBeGreaterThan(_maximumScopes, Context(_stepCount));
        modality.Active.ShouldBeNull(Context(_stepCount));
        modality.Dispose();
        pointer.Dispose();
        focus.Dispose();
        root.Dispose();
        return;

        void ApplyRandom(int operation)
        {
            switch (operation)
            {
                case 0:
                    var enterCandidates = controls.Where(control =>
                        control.EffectiveIsVisible &&
                        control.EffectiveIsEnabled &&
                        expectedStack.Count < _maximumScopes &&
                        !expectedStack.Any(scope => roots[scope].Any(root => ReferenceEquals(root, control)))).ToArray();

                    if (enterCandidates.Length > 0)
                    {
                        var candidate = Choose(enterCandidates);
                        Enter(
                            candidate,
                            random.Next(2) == 0 ? OutsideInteraction.Ignore : OutsideInteraction.Dismiss,
                            $"Enter({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(enter fallback)");
                    break;
                case 1:
                    var includeCandidates = expectedStack.Count == 0
                        ? []
                        : controls.Where(control =>
                            control.EffectiveIsVisible &&
                            control.EffectiveIsEnabled &&
                            expectedStack.All(scope => roots[scope].All(root =>
                                !IsWithin(control, root) && !IsWithin(root, control)))).ToArray();

                    if (includeCandidates.Length > 0)
                    {
                        var candidate = Choose(includeCandidates);
                        Include(candidate, $"Include({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(include fallback)");
                    break;
                case 2:
                    var nestedCandidates = expectedStack.Count is 0 or >= _maximumScopes
                        ? []
                        : controls.Where(control =>
                            control.EffectiveIsVisible &&
                            control.EffectiveIsEnabled &&
                            roots[expectedStack[^1]].Any(root =>
                                !ReferenceEquals(control, root) && IsWithin(control, root)) &&
                            expectedStack.All(scope => roots[scope].All(root => !ReferenceEquals(root, control))))
                            .ToArray();

                    if (nestedCandidates.Length > 0)
                    {
                        var candidate = Choose(nestedCandidates);
                        Enter(candidate, OutsideInteraction.Ignore, $"NestedEnter({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(nested fallback)");
                    break;
                case 3:
                    var focusCandidates = AllowedControls();

                    if (focusCandidates.Length > 0)
                    {
                        var candidate = Choose(focusCandidates);
                        Focus(candidate, $"Focus({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(focus fallback)");
                    break;
                case 4:
                    var captureCandidates = AllowedControls();

                    if (captureCandidates.Length > 0)
                    {
                        var candidate = Choose(captureCandidates);
                        Capture(candidate, $"Capture({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(capture fallback)");
                    break;
                case 5:
                    DispatchRandom(PointerAction.Move, "Move(random)");
                    break;
                case 6:
                    DispatchRandom(PointerAction.Press, "Press(random)");
                    break;
                case 7:
                    DispatchRandom(PointerAction.Wheel, "Wheel(random)");
                    break;
                case 8:
                    var hideCandidates = controls.Where(static control => control.EffectiveIsVisible).ToArray();

                    if (hideCandidates.Length > 0)
                    {
                        var candidate = Choose(hideCandidates);
                        Hide(candidate, $"Hide({Name(candidate)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(hide fallback)");
                    break;
                case 9:
                    if (handles.Count > 0)
                    {
                        var handle = handles[random.Next(handles.Count)];
                        Dispose(handle, $"Dispose(scope {handles.IndexOf(handle)})");
                        break;
                    }

                    DispatchRandom(PointerAction.Move, "Move(dispose fallback)");
                    break;
                default:
                    throw new UnreachableException();
            }
        }

        void Enter(ControlBase control, OutsideInteraction outsideInteraction, string operation)
        {
            Record(operation);
            var followsInactiveHandle = handles.Exists(static handle => !handle.IsActive);
            var exceedsHistoricalLimit = handles.Count >= _maximumScopes;
            var scope = modality.Enter(control, outsideInteraction, initialFocus: control);
            handles.Add(scope);
            expectedStack.Add(scope);
            roots.Add(scope, [control]);
            policies.Add(scope, outsideInteraction);

            if (followsInactiveHandle)
            {
                entriesAfterInactiveHandles++;
            }

            if (exceedsHistoricalLimit)
            {
                entriesAfterHistoricalLimit++;
            }

            scope.DismissRequested += (_, _) =>
            {
                dismissalCallbacks++;
                dismissedScopes.Add(scope);
                var index = expectedStack.IndexOf(scope);

                if (index >= 0)
                {
                    expectedStack.RemoveRange(index, expectedStack.Count - index);
                }

                scope.Dispose();
            };

        }

        void Include(ControlBase control, string operation)
        {
            Record(operation);
            var active = expectedStack[^1];
            active.Include(control);
            roots[active].Add(control);
        }

        void Focus(ControlBase control, string operation)
        {
            Record(operation);
            focus.Focus(control).ShouldBeTrue(Context(-1));
        }

        void Capture(ControlBase control, string operation)
        {
            Record(operation);
            pointer.Capture(control).ShouldBeTrue(Context(-1));
        }

        void Hide(ControlBase control, string operation)
        {
            Record(operation);
            var unwind = -1;

            for (var index = 0; index < expectedStack.Count; index++)
            {
                if (IsWithin(expectedStack[index].Root, control))
                {
                    unwind = index;
                    break;
                }
            }

            var surviving = unwind < 0 ? expectedStack.Count : unwind;

            for (var index = 0; index < surviving; index++)
            {
                var planeRoots = roots[expectedStack[index]];

                for (var rootIndex = planeRoots.Count - 1; rootIndex > 0; rootIndex--)
                {
                    if (IsWithin(planeRoots[rootIndex], control))
                    {
                        planeRoots.RemoveAt(rootIndex);
                    }
                }
            }

            if (unwind >= 0)
            {
                expectedStack.RemoveRange(unwind, expectedStack.Count - unwind);
            }

            control.Visibility = Visibility.Hidden;
        }

        void Dispose(ModalScope scope, string operation)
        {
            Record(operation);
            var index = expectedStack.IndexOf(scope);

            if (index >= 0)
            {
                expectedStack.RemoveRange(index, expectedStack.Count - index);
            }

            scope.Dispose();
        }

        void DispatchScriptedDismissingPress()
        {
            expectedStack.Count.ShouldBe(2, Context(-1));
            var parent = expectedStack[^2];
            var child = expectedStack[^1];
            IsAllowed(third, roots[parent]).ShouldBeTrue(Context(-1));
            IsAllowed(third, roots[child]).ShouldBeFalse(Context(-1));
            modality.Active.ShouldBeSameAs(child, Context(-1));
            policies[child].ShouldBe(OutsideInteraction.Dismiss, Context(-1));
            var callbacksBefore = dismissalCallbacks;
            var dismissedBefore = dismissedScopes.Count;

            Dispatch(third, PointerAction.Press, "DismissPress(third)");

            dismissalCallbacks.ShouldBe(callbacksBefore + 1, Context(-1));
            dismissedScopes.Count.ShouldBe(dismissedBefore + 1, Context(-1));
            dismissedScopes[^1].ShouldBeSameAs(child, Context(-1));
            routedTargets.ShouldBeEmpty(Context(-1));
            expectedStack.Count.ShouldBe(1, Context(-1));
            expectedStack[^1].ShouldBeSameAs(parent, Context(-1));
            modality.Active.ShouldBeSameAs(parent, Context(-1));
        }

        void DispatchRandom(PointerAction action, string operation)
        {
            var target = Choose(points.Keys.ToArray());
            Dispatch(target, action, $"{operation}:{Name(target)}");
        }

        void Dispatch(ControlBase pointOwner, PointerAction action, string operation)
        {
            Record(operation);
            var point = points[pointOwner];
            var activeBefore = expectedStack.Count == 0 ? null : expectedStack[^1];
            var stackBefore = expectedStack.ToArray();
            var planeRoots = expectedStack.Count == 0
                ? null
                : roots[expectedStack[^1]].ToArray();
            var physical = root.HitTest(point);
            var captured = pointer.Captured;
            var eligiblePhysical = planeRoots is null || IsAllowed(physical, planeRoots)
                ? physical
                : null;
            var expectedTarget = captured ?? eligiblePhysical;
            // A wheel hit inside the active plane is never dismiss-eligible, whether or not the
            // routed target ends up handling it (e.g. a scroll endpoint or a no-range list) -
            // only a genuinely outside press/wheel qualifies.
            var qualifiesOutside = activeBefore is not null &&
                captured is null &&
                eligiblePhysical is null &&
                action is PointerAction.Press or PointerAction.Wheel;
            var qualifiesPolicyInput = qualifiesOutside;
            var expectsDismissal = qualifiesPolicyInput &&
                policies[activeBefore!] == OutsideInteraction.Dismiss;
            var callbacksBefore = dismissalCallbacks;
            var dismissedBefore = dismissedScopes.Count;

            if (qualifiesPolicyInput)
            {
                if (expectsDismissal)
                {
                    qualifyingDismissInputs++;

                    if (action == PointerAction.Press)
                    {
                        dismissingPresses++;
                    }
                    else
                    {
                        dismissingWheels++;
                    }
                }
                else
                {
                    qualifyingIgnoreInputs++;
                }
            }

            routedTargets.Clear();
            var input = new Pointer(
                point,
                pixels: null,
                action == PointerAction.Press ? Buttons.Primary : Buttons.None,
                action,
                wheelX: 0,
                wheelY: action == PointerAction.Wheel ? -1 : 0,
                Modifiers.None,
                isMotion: action == PointerAction.Move,
                isCellPositionInferred: false);

            var actualTarget = pointer.Dispatch(input);

            actualTarget.ShouldBeSameAs(expectedTarget, Context(-1));
            (dismissalCallbacks - callbacksBefore).ShouldBe(
                expectsDismissal ? 1 : 0,
                Context(-1));

            if (expectsDismissal)
            {
                dismissedScopes.Count.ShouldBe(dismissedBefore + 1, Context(-1));
                dismissedScopes[^1].ShouldBeSameAs(activeBefore, Context(-1));
                activeBefore.ShouldNotBeNull().IsActive.ShouldBeFalse(Context(-1));
                AssertStack(stackBefore[..^1], Context(-1));
            }
            else
            {
                dismissedScopes.Count.ShouldBe(dismissedBefore, Context(-1));
                AssertStack(stackBefore, Context(-1));
            }

            if (expectedTarget is null)
            {
                routedTargets.ShouldBeEmpty(Context(-1));
            }
            else
            {
                routedTargets.ShouldBe([expectedTarget], Context(-1));
            }
        }

        ControlBase[] AllowedControls()
        {
            var planeRoots = expectedStack.Count == 0 ? null : roots[expectedStack[^1]];
            return [.. controls.Where(control =>
                control.EffectiveIsVisible &&
                control.EffectiveIsEnabled &&
                control.CanFocus &&
                (planeRoots is null || IsAllowed(control, planeRoots)))];
        }

        T Choose<T>(IReadOnlyList<T> values) => values[random.Next(values.Count)];

        void AssertStack(IReadOnlyList<ModalScope> expected, string context)
        {
            expectedStack.Count.ShouldBe(expected.Count, context);

            for (var index = 0; index < expected.Count; index++)
            {
                expectedStack[index].ShouldBeSameAs(expected[index], context);
            }
        }

        void AssertInvariants(int step)
        {
            var context = Context(step);
            modality.Active.ShouldBe(expectedStack.Count == 0 ? null : expectedStack[^1], context);

            foreach (var handle in handles)
            {
                var expectedActive = expectedStack.Contains(handle);
                handle.IsActive.ShouldBe(expectedActive, context);

                if (!expectedActive)
                {
                    _ = inactive.Add(handle);
                }
            }

            foreach (var handle in inactive)
            {
                handle.IsActive.ShouldBeFalse(context);
            }

            if (expectedStack.Count > 0)
            {
                var activeRoots = roots[expectedStack[^1]];
                expectedStack[^1].RootCount.ShouldBe(activeRoots.Count, context);

                for (var index = 0; index < activeRoots.Count; index++)
                {
                    expectedStack[^1].RootAt(index).ShouldBeSameAs(activeRoots[index], context);
                }

                AssertState(focus.Focused, "focus", activeRoots, context);
                AssertState(pointer.Captured, "capture", activeRoots, context);
                AssertState(pointer.Hovered, "hover", activeRoots, context);
                AssertState(pointer.PressOrigin, "press origin", activeRoots, context);
            }

            dismissalCallbacks.ShouldBe(qualifyingDismissInputs, context);
            handles.Count.ShouldBeLessThanOrEqualTo(_stepCount, context);
            expectedStack.Count.ShouldBeLessThanOrEqualTo(_maximumScopes, context);
            controls.Count.ShouldBe(6, context);

            if (step >= 8)
            {
                dismissingPresses.ShouldBeGreaterThan(0, context);
            }

            if (step >= 9)
            {
                qualifyingIgnoreInputs.ShouldBeGreaterThan(0, context);
            }

            if (step >= 13)
            {
                dismissingWheels.ShouldBeGreaterThan(0, context);
                entriesAfterInactiveHandles.ShouldBeGreaterThan(0, context);
            }

            if (step >= 20)
            {
                entriesAfterHistoricalLimit.ShouldBeGreaterThan(0, context);
                handles.Count.ShouldBeGreaterThan(_maximumScopes, context);
            }
        }

        void Record(string operation)
        {
            recent.Enqueue(operation);

            while (recent.Count > 8)
            {
                _ = recent.Dequeue();
            }
        }

        string Context(int step) =>
            $"seed=0x{seed:X8}, case={sample}, step={step}, liveScopes={expectedStack.Count}, " +
            $"historicalHandles={handles.Count}, dismissals={dismissalCallbacks}/{qualifyingDismissInputs}, " +
            $"reentries={entriesAfterInactiveHandles}/{entriesAfterHistoricalLimit}, " +
            $"recent=[{string.Join(" | ", recent)}]";

        string Name(ControlBase control) => ReferenceEquals(control, root)
            ? "root"
            : $"control-{controls.IndexOf(control)}";
    }

    #endregion

    #region Independent plane oracle

    private static void AssertState(
        ControlBase? control,
        string name,
        IReadOnlyList<ControlBase> planeRoots,
        string context)
    {
        if (control is not null)
        {
            IsAllowed(control, planeRoots).ShouldBeTrue($"{context}, state={name}");
        }
    }

    private static bool IsAllowed(ControlBase? control, IReadOnlyList<ControlBase> planeRoots)
    {
        if (control is null)
        {
            return false;
        }

        foreach (var root in planeRoots)
        {
            if (IsWithin(control, root))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWithin(ControlBase? control, ControlBase root)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, root))
            {
                return true;
            }
        }

        return false;
    }

    #endregion
}
