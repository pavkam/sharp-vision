// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Navigation;

/// <summary>Compares Pager layout and scalar transitions with independent fixed-seed models.</summary>
public sealed class PagerLayoutTests
{
    /// <summary>Verifies every selected target, identity, state, and whole-cell bound across a
    /// deterministic matrix of page ranges, windows, and finite widths.</summary>
    [Fact]
    public void Layout_WhenFixedSeedVariesStateAndWidth_MatchesArithmeticOracle()
    {
        const int seed = 0x50414745;
        var random = new Random(seed);

        for (var caseIndex = 0; caseIndex < 1_000; caseIndex++)
        {
            var pageCount = random.Next(0, 81);
            var pageIndex = pageCount == 0 ? -1 : random.Next(pageCount);
            var maximumVisiblePages = random.Next(1, 12);
            var width = random.Next(0, 41);
            var expected = PagerLayoutModel.Create(pageCount, pageIndex, maximumVisiblePages, width);
            using var pager = new Pager
            {
                MaximumVisiblePages = maximumVisiblePages,
                PageCount = pageCount,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (pageCount > 0)
            {
                pager.PageIndex = pageIndex;
            }

            new LayoutEngine().Layout(pager, new Size(width, 1));
            var actual = pager.LayoutSnapshot.Targets;
            var transcript = FormattableString.Invariant(
                $"seed={seed}, case={caseIndex}, count={pageCount}, index={pageIndex}, max={maximumVisiblePages}, width={width}");

            actual.Count.ShouldBe(expected.Count, transcript);

            for (var targetIndex = 0; targetIndex < expected.Count; targetIndex++)
            {
                var expectedTarget = expected[targetIndex];
                var actualTarget = actual[targetIndex];
                actualTarget.Kind.ShouldBe(expectedTarget.Kind, transcript);
                actualTarget.PageIndex.ShouldBe(expectedTarget.PageIndex, transcript);
                actualTarget.Text.ShouldBe(expectedTarget.Text, transcript);
                actualTarget.Bounds.ShouldBe(expectedTarget.Bounds, transcript);
                actualTarget.IsEnabled.ShouldBe(expectedTarget.IsEnabled, transcript);
                actualTarget.IsCurrent.ShouldBe(expectedTarget.IsCurrent, transcript);
                actualTarget.Bounds.Right.ShouldBeLessThanOrEqualTo(width, transcript);
            }
        }
    }

    /// <summary>Verifies random page-count, direct-index, availability, and keyboard transitions
    /// preserve the scalar invariant and exact event stream.</summary>
    [Fact]
    public void State_WhenFixedSeedMutatesRangesAndInput_MatchesScalarOracle()
    {
        const int seed = 0x50414752;
        var random = new Random(seed);
        using var pager = new Pager();
        var expectedCount = 0;
        var expectedIndex = -1;
        var expectedEnabled = true;
        var expectedVisible = true;
        var expectedChanges = new List<(int Previous, int Current, ActivationCause Cause)>();
        var actualChanges = new List<(int Previous, int Current, ActivationCause Cause)>();
        pager.PageChanged += (_, eventArgs) => actualChanges.Add((
            eventArgs.PreviousPageIndex,
            eventArgs.CurrentPageIndex,
            eventArgs.Cause));

        for (var step = 0; step < 1_000; step++)
        {
            var operation = random.Next(6);

            switch (operation)
            {
                case 0:
                    {
                        var nextCount = random.Next(0, 51);
                        var nextIndex = nextCount == 0
                            ? -1
                            : expectedCount == 0
                                ? 0
                                : Math.Min(expectedIndex, nextCount - 1);

                        if (nextCount != expectedCount && nextIndex != expectedIndex)
                        {
                            expectedChanges.Add((expectedIndex, nextIndex, ActivationCause.Programmatic));
                        }

                        pager.PageCount = nextCount;
                        expectedCount = nextCount;
                        expectedIndex = nextIndex;
                        break;
                    }
                case 1 when expectedCount > 0:
                    {
                        var nextIndex = random.Next(expectedCount);

                        if (nextIndex != expectedIndex)
                        {
                            expectedChanges.Add((expectedIndex, nextIndex, ActivationCause.Programmatic));
                        }

                        pager.PageIndex = nextIndex;
                        expectedIndex = nextIndex;
                        break;
                    }
                case 1:
                    break;
                case 2:
                    pager.MaximumVisiblePages = random.Next(1, 12);
                    break;
                case 3:
                    {
                        var codes = new[] { Code.Left, Code.Up, Code.PageUp, Code.Right, Code.Down, Code.PageDown, Code.Home, Code.End };
                        var code = codes[random.Next(codes.Length)];
                        var nextIndex = expectedIndex;

                        if (expectedEnabled && expectedVisible && expectedCount > 1)
                        {
                            if (code is Code.Left or Code.Up or Code.PageUp)
                            {
                                nextIndex = Math.Max(0, expectedIndex - 1);
                            }
                            else if (code is Code.Right or Code.Down or Code.PageDown)
                            {
                                nextIndex = Math.Min(expectedCount - 1, expectedIndex + 1);
                            }
                            else if (code == Code.Home)
                            {
                                nextIndex = 0;
                            }
                            else if (code == Code.End)
                            {
                                nextIndex = expectedCount - 1;
                            }
                        }

                        if (nextIndex != expectedIndex)
                        {
                            expectedChanges.Add((expectedIndex, nextIndex, ActivationCause.Keyboard));
                        }

                        _ = RouteKey(pager, code);
                        expectedIndex = nextIndex;
                        break;
                    }
                case 4:
                    expectedEnabled = !expectedEnabled;
                    pager.IsEnabled = expectedEnabled;
                    break;
                case 5:
                    expectedVisible = !expectedVisible;
                    pager.Visibility = expectedVisible ? Visibility.Visible : Visibility.Hidden;
                    break;
                default:
                    throw new UnreachableException();
            }

            var transcript = FormattableString.Invariant($"seed={seed}, step={step}, operation={operation}");
            pager.PageCount.ShouldBe(expectedCount, transcript);
            pager.PageIndex.ShouldBe(expectedIndex, transcript);
            (pager.PageCount == 0 ? pager.PageIndex == -1 : pager.PageIndex >= 0 && pager.PageIndex < pager.PageCount)
                .ShouldBeTrue(transcript);
        }

        actualChanges.ShouldBe(expectedChanges, $"seed={seed}");
    }

    /// <summary>Verifies fixed-seed pointer presses retain target identity only when no layout or
    /// availability transition supersedes the captured snapshot.</summary>
    [Fact]
    public async Task Pointer_WhenFixedSeedVariesPressLifecycle_ActivatesOnlyUninterruptedIdentityAsync()
    {
        const int seed = 0x504F494E;
        var random = new Random(seed);
        await using var dispatcher = Dispatcher.Start();

        await dispatcher.InvokeAsync(() =>
        {
            for (var caseIndex = 0; caseIndex < 500; caseIndex++)
            {
                var pageCount = random.Next(2, 81);
                var pageIndex = random.Next(pageCount);
                var width = random.Next(5, 41);
                var pager = new Pager
                {
                    PageCount = pageCount,
                    PageIndex = pageIndex,
                    MaximumVisiblePages = random.Next(1, 12),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                pager.Attach(dispatcher);
                new LayoutEngine().Layout(pager, new Size(width, 1));

                using (var focus = new FocusManager(pager))
                using (var pointer = new PointerManager(pager))
                {
                    var candidates = pager.LayoutSnapshot.Targets
                        .Where(static target => target.IsEnabled)
                        .ToArray();

                    if (candidates.Length == 0)
                    {
                        pager.Dispose();
                        continue;
                    }

                    var target = candidates[random.Next(candidates.Length)];
                    var operation = random.Next(6);
                    var releaseBounds = target.Bounds;
                    var changes = 0;
                    pager.PageChanged += (_, _) => changes++;
                    _ = pointer.Dispatch(PointerAt(target.Bounds, PointerAction.Press));

                    switch (operation)
                    {
                        case 0:
                            break;
                        case 1:
                            var resizedWidth = width == 40 ? width - 1 : width + 1;
                            new LayoutEngine().Layout(pager, new Size(resizedWidth, 1));
                            break;
                        case 2:
                            pager.PageCount = pageCount + 1;
                            break;
                        case 3:
                            pager.Visibility = Visibility.Hidden;
                            break;
                        case 4:
                            pager.IsEnabled = false;
                            break;
                        case 5:
                            var dragX = target.Bounds.X == 0
                                ? target.Bounds.Right
                                : target.Bounds.X - 1;
                            releaseBounds = new Rect(dragX, 0, 1, 1);
                            _ = pointer.Dispatch(PointerAt(releaseBounds, PointerAction.Move));
                            break;
                        default:
                            throw new UnreachableException();
                    }

                    _ = pointer.Dispatch(PointerAt(releaseBounds, PointerAction.Release));
                    var transcript = FormattableString.Invariant(
                        $"seed={seed}, case={caseIndex}, count={pageCount}, index={pageIndex}, width={width}, operation={operation}");
                    pager.PageIndex.ShouldBe(operation == 0 ? target.PageIndex : pageIndex, transcript);
                    changes.ShouldBe(operation == 0 ? 1 : 0, transcript);
                    pointer.Captured.ShouldBeNull(transcript);
                    pager.IsPressed.ShouldBeFalse(transcript);
                }

                pager.Dispose();
            }
        }, TestContext.Current.CancellationToken);
    }

    private static KeyEventArgs RouteKey(Pager pager, Code code)
    {
        var eventArgs = new KeyEventArgs(new Stroke(
            code,
            character: null,
            nativeCode: 0,
            Modifiers.None,
            KeyAction.Press));
        _ = Router.Route(pager, Events.Key, eventArgs);
        return eventArgs;
    }

    private static Pointer PointerAt(Rect bounds, PointerAction action) => new(
        new Point(bounds.X, bounds.Y),
        pixels: null,
        Buttons.Primary,
        action,
        wheelX: 0,
        wheelY: 0,
        Modifiers.None,
        isMotion: action == PointerAction.Move,
        isCellPositionInferred: false);
}
