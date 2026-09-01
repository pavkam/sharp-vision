// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Input;

/// <summary>Exercises suggestion resolution and acceptance against an independent deterministic model.</summary>
public sealed class SuggestionInputRandomizedTests
{
    /// <summary>Verifies stale completions, attachment changes, popup intent, provisional
    /// navigation, selector failure, and acceptance remain equivalent to an independent model.</summary>
    [Fact]
    public async Task Transcript_WhenSeeded_PreservesLatestResolutionAndAcceptanceInvariantsAsync()
    {
        const int seed = 0x51A7_2026;
        var random = new Random(seed);
        var transcript = new List<string>();
        var completions = new Dictionary<int, TaskCompletionSource<IReadOnlyList<object?>>>();
        var observations = new Dictionary<int, Task>();
        var queries = new Dictionary<int, string>();
        var cancellationTokens = new Dictionary<int, CancellationToken>();
        var settled = new HashSet<int>();
        var issued = 0;
        var acceptedCount = 0;
        var input = new SuggestionInput
        {
            Width = Length.Cells(18),
            Height = Length.Cells(3),
            DropDownHeight = Length.Cells(4),
            Resolver = (searchTerms, cancellationToken) =>
            {
                var id = ++issued;
                var completion = new TaskCompletionSource<IReadOnlyList<object?>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                completions.Add(id, completion);
                queries.Add(id, searchTerms);
                cancellationTokens.Add(id, cancellationToken);
                return new ValueTask<IReadOnlyList<object?>>(completion.Task);
            }
        };
        input.SuggestionAccepted += (_, _) => acceptedCount++;
        var root = new Overlay { Children = { input } };
        await using var surface = await ComponentSurface.MountAsync(
            root,
            new Size(24, 10),
            TestContext.Current.CancellationToken);
        var list = OwnedTree.Find<UiListView>(input).ShouldNotBeNull();
        var editor = OwnedTree.Find<TextInput>(input).ShouldNotBeNull();

        var modelText = string.Empty;
        object?[] modelSuggestions = [];
        var modelResolving = false;
        var modelOpen = false;
        var modelWantsOpen = false;
        var modelAttached = true;
        var modelEnabled = true;
        var modelVisible = true;
        var modelAncestorEnabled = true;
        var modelGeneration = 1;
        int? modelSnapshotGeneration = 1;
        int? currentRequest = null;
        var modelSelectedIndex = -1;
        int? openingSnapshotGeneration = null;
        var openingSelectedIndex = -1;
        var modelAcceptedCount = 0;
        var minimumPrefixLength = 1;

        object?[] ResultsFor(string query) =>
        [
            $"{query}:0",
            $"{query}:1",
            $"{query}:2"
        ];

        bool IsAvailable() =>
            modelAttached && modelEnabled && modelVisible && modelAncestorEnabled;

        bool IsOperationEligible(int operation) => operation switch
        {
            0 => modelAttached,
            1 => completions.Keys.Any(id => !settled.Contains(id)),
            2 => modelAttached && modelOpen,
            3 => modelAttached && !modelOpen && !modelResolving &&
                 modelSnapshotGeneration == modelGeneration &&
                 modelSuggestions.Length > 0 && IsAvailable(),
            4 => modelOpen && !modelResolving &&
                 modelSnapshotGeneration == modelGeneration && IsAvailable(),
            5 or 6 => modelOpen && !modelResolving && modelSelectedIndex >= 0 &&
                      modelSnapshotGeneration == modelGeneration && IsAvailable(),
            7 => modelAttached && modelOpen && modelEnabled && modelVisible,
            8 => modelAttached && (!modelEnabled || !modelVisible),
            9 => true,
            10 => modelAttached,
            11 => true,
            _ => false
        };

        void OpenModel()
        {
            if (modelOpen || !IsAvailable() || modelSuggestions.Length == 0)
            {
                return;
            }

            modelOpen = true;
            openingSnapshotGeneration = modelSnapshotGeneration;
            openingSelectedIndex = modelSelectedIndex;
            modelSelectedIndex = 0;
        }

        void CloseModel(bool accepted)
        {
            if (!modelOpen)
            {
                modelWantsOpen = false;
                return;
            }

            modelOpen = false;
            modelWantsOpen = false;

            if (!accepted && openingSnapshotGeneration == modelSnapshotGeneration)
            {
                modelSelectedIndex = openingSelectedIndex;
            }

            openingSnapshotGeneration = null;
            openingSelectedIndex = -1;
        }

        void CaptureCurrentObservation()
        {
            if (currentRequest is { } previousRequest && !settled.Contains(previousRequest))
            {
                cancellationTokens[previousRequest].IsCancellationRequested.ShouldBeTrue();
            }

            currentRequest = issued;
            observations[issued] = input.LastResolutionObservation.ShouldNotBeNull();
        }

        void AssertModel(int step)
        {
            input.Text.ShouldBe(modelText, $"seed {seed}, step {step}");
            input.Suggestions.ShouldBe(modelSuggestions, $"seed {seed}, step {step}");
            input.IsResolving.ShouldBe(modelResolving, $"seed {seed}, step {step}");
            input.IsOpen.ShouldBe(modelOpen, $"seed {seed}, step {step}");
            acceptedCount.ShouldBe(modelAcceptedCount, $"seed {seed}, step {step}");
            (input.Dispatcher is not null).ShouldBe(modelAttached, $"seed {seed}, step {step}");

            if (modelOpen && modelSnapshotGeneration is not null && !modelResolving && IsAvailable())
            {
                list.SelectedIndex.ShouldBe(modelSelectedIndex, $"seed {seed}, step {step}");
                list.ActiveIndex.ShouldBe(modelSelectedIndex, $"seed {seed}, step {step}");
            }
        }

        try
        {
            for (var step = 0; step < 96; step++)
            {
                var eligibleOperations = Enumerable.Range(0, 12).Where(IsOperationEligible).ToArray();
                int? coverageOperation =
                    !transcript.Any(entry => entry.Contains(":edit:", StringComparison.Ordinal)) &&
                    eligibleOperations.Contains(0) ? 0 :
                    !transcript.Any(entry => entry.Contains(":complete:", StringComparison.Ordinal)) &&
                    eligibleOperations.Contains(1) ? 1 :
                    !transcript.Any(entry => entry.Contains(":selector-failure", StringComparison.Ordinal)) &&
                    eligibleOperations.Contains(6) ? 6 :
                    !transcript.Any(entry => entry.Contains(":accept:", StringComparison.Ordinal)) &&
                    eligibleOperations.Contains(5) ? 5 :
                    !transcript.Any(entry => entry.EndsWith(":detach", StringComparison.Ordinal)) &&
                    modelAttached ? 9 :
                    !transcript.Any(entry => entry.EndsWith(":attach", StringComparison.Ordinal)) &&
                    !modelAttached ? 9 :
                    null;
                var operation = coverageOperation ?? eligibleOperations[random.Next(eligibleOperations.Length)];

                switch (operation)
                {
                    case 0 when modelAttached:
                        {
                            var nextText = $"q{step}";
                            transcript.Add($"{step}:edit:{nextText}");
                            await surface.UpdateAsync(() => input.Text = nextText, $"transcript edit {step}");
                            modelText = nextText;
                            modelGeneration++;
                            modelWantsOpen = true;

                            if (nextText.Length < minimumPrefixLength)
                            {
                                if (currentRequest is { } previousRequest && !settled.Contains(previousRequest))
                                {
                                    cancellationTokens[previousRequest].IsCancellationRequested.ShouldBeTrue();
                                }

                                currentRequest = null;
                                modelSnapshotGeneration = modelGeneration;
                                modelSuggestions = [];
                                modelSelectedIndex = -1;
                                modelResolving = false;

                                if (modelOpen)
                                {
                                    CloseModel(accepted: false);
                                }
                            }
                            else
                            {
                                modelSnapshotGeneration = null;
                                modelResolving = true;
                                CaptureCurrentObservation();
                            }

                            break;
                        }

                    case 1 when completions.Keys.Any(id => !settled.Contains(id)):
                        {
                            var candidates = completions.Keys.Where(id => !settled.Contains(id)).ToArray();
                            var id = candidates[random.Next(candidates.Length)];
                            var results = ResultsFor(queries[id]);
                            transcript.Add($"{step}:complete:{id}:{queries[id]}");
                            completions[id].SetResult(results);
                            _ = settled.Add(id);
                            await observations[id].WaitAsync(TestContext.Current.CancellationToken);
                            await surface.UpdateAsync(static () => { }, $"render transcript completion {step}");

                            if (currentRequest == id)
                            {
                                modelSuggestions = results;
                                modelResolving = false;
                                modelSnapshotGeneration = modelGeneration;
                                currentRequest = null;
                                modelSelectedIndex = -1;

                                if (modelWantsOpen && modelSuggestions.Length > 0)
                                {
                                    if (modelOpen)
                                    {
                                        modelSelectedIndex = 0;
                                    }
                                    else
                                    {
                                        OpenModel();
                                    }
                                }
                                else if (modelOpen)
                                {
                                    CloseModel(accepted: false);
                                }
                            }

                            break;
                        }

                    case 2 when modelAttached && modelOpen:
                        transcript.Add($"{step}:close");
                        await surface.UpdateAsync(input.Close, $"transcript close {step}");
                        CloseModel(accepted: false);
                        break;

                    case 3 when modelAttached && !modelOpen && !modelResolving &&
                                     modelSnapshotGeneration == modelGeneration &&
                                     modelSuggestions.Length > 0 && IsAvailable():
                        transcript.Add($"{step}:open");
                        modelWantsOpen = true;
                        await surface.UpdateAsync(() => _ = input.Open(), $"transcript open {step}");
                        OpenModel();
                        break;

                    case 4 when modelOpen && !modelResolving &&
                                     modelSnapshotGeneration == modelGeneration && IsAvailable():
                        {
                            var down = random.Next(2) == 0;
                            transcript.Add($"{step}:navigate:{(down ? "down" : "up")}");
                            await surface.UpdateAsync(() => _ = input.Open(), $"focus transcript owner {step}");

                            if (down)
                            {
                                await surface.Keyboard.PressAsync(Code.Down);
                                modelSelectedIndex = Math.Min(modelSuggestions.Length - 1, modelSelectedIndex + 1);
                            }
                            else
                            {
                                await surface.Keyboard.PressAsync(Code.Up);
                                modelSelectedIndex = Math.Max(0, modelSelectedIndex - 1);
                            }

                            break;
                        }

                    case 5 when modelOpen && !modelResolving && modelSelectedIndex >= 0 &&
                                     modelSnapshotGeneration == modelGeneration && IsAvailable():
                        {
                            var pointer = random.Next(2) == 0;
                            var acceptedIndex = pointer ? 0 : modelSelectedIndex;
                            var acceptedText = (string) modelSuggestions[acceptedIndex]!;
                            transcript.Add($"{step}:accept:{(pointer ? "pointer" : "enter")}:{acceptedIndex}");

                            if (pointer)
                            {
                                await surface.ResizeAsync(new Size(24, 10));
                                await surface.Pointer.ClickAsync(list, new Point(1, acceptedIndex));
                            }
                            else
                            {
                                await surface.UpdateAsync(() => _ = input.Open(), $"focus transcript acceptance {step}");
                                await surface.Keyboard.PressAsync(Code.Enter);
                            }

                            modelText = acceptedText;
                            modelSelectedIndex = acceptedIndex;
                            modelGeneration++;
                            modelSnapshotGeneration = null;
                            modelResolving = true;
                            modelAcceptedCount++;
                            CloseModel(accepted: true);
                            CaptureCurrentObservation();
                            break;
                        }

                    case 6 when modelOpen && !modelResolving && modelSelectedIndex >= 0 &&
                                     modelSnapshotGeneration == modelGeneration && IsAvailable():
                        {
                            var expected = new InvalidOperationException($"selector {step}");
                            transcript.Add($"{step}:selector-failure");
                            await surface.UpdateAsync(
                                () => input.TextSelector = _ => throw expected,
                                $"arm transcript selector failure {step}");
                            await surface.UpdateAsync(() => _ = input.Open(), $"focus transcript selector failure {step}");
                            var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
                                await surface.Application.Dispatcher.InvokeAsync(
                                    () => _ = Router.Route(
                                        editor,
                                        Events.Key,
                                        new KeyEventArgs(new Stroke(
                                            Code.Enter,
                                            character: null,
                                            nativeCode: 0,
                                            Modifiers.None,
                                            KeyAction.Press))),
                                    TestContext.Current.CancellationToken));
                            thrown.ShouldBeSameAs(expected);
                            await surface.UpdateAsync(
                                () => input.TextSelector = null,
                                $"clear transcript selector failure {step}");
                            break;
                        }

                    case 7 when modelAttached && modelOpen && modelEnabled && modelVisible:
                        {
                            var hide = random.Next(2) == 0;
                            transcript.Add($"{step}:owner-unavailable:{(hide ? "hidden" : "disabled")}");
                            await surface.UpdateAsync(
                                () =>
                                {
                                    if (hide)
                                    {
                                        input.Visibility = Visibility.Hidden;
                                    }
                                    else
                                    {
                                        input.IsEnabled = false;
                                    }
                                },
                                $"transcript owner unavailable {step}");
                            CloseModel(accepted: false);
                            modelVisible = !hide;
                            modelEnabled = hide;
                            break;
                        }

                    case 8 when modelAttached && (!modelEnabled || !modelVisible):
                        transcript.Add($"{step}:owner-available");
                        await surface.UpdateAsync(
                            () =>
                            {
                                input.IsEnabled = true;
                                input.Visibility = Visibility.Visible;
                            },
                            $"transcript owner available {step}");
                        modelEnabled = true;
                        modelVisible = true;
                        break;

                    case 9 when modelAttached:
                        var detachedRequest = currentRequest;
                        transcript.Add($"{step}:detach");
                        await surface.UpdateAsync(() => root.Children.Remove(input), $"transcript detach {step}");

                        if (detachedRequest is { } request && !settled.Contains(request))
                        {
                            cancellationTokens[request].IsCancellationRequested.ShouldBeTrue();
                        }

                        modelAttached = false;
                        modelGeneration++;
                        modelSnapshotGeneration = null;
                        modelResolving = false;
                        currentRequest = null;
                        CloseModel(accepted: false);
                        break;

                    case 9 when !modelAttached:
                        transcript.Add($"{step}:attach");
                        await surface.UpdateAsync(() => root.Children.Add(input), $"transcript attach {step}");
                        modelAttached = true;
                        break;

                    case 10 when modelAttached:
                        {
                            minimumPrefixLength = minimumPrefixLength == 1 ? 100 : 1;
                            transcript.Add($"{step}:threshold:{minimumPrefixLength}");
                            await surface.UpdateAsync(
                                () => input.MinimumPrefixLength = minimumPrefixLength,
                                $"transcript threshold {step}");
                            modelGeneration++;

                            if (minimumPrefixLength > modelText.Length)
                            {
                                if (currentRequest is { } previousRequest && !settled.Contains(previousRequest))
                                {
                                    cancellationTokens[previousRequest].IsCancellationRequested.ShouldBeTrue();
                                }

                                modelSnapshotGeneration = modelGeneration;
                                modelSuggestions = [];
                                modelSelectedIndex = -1;
                                modelResolving = false;
                                currentRequest = null;

                                if (modelOpen)
                                {
                                    CloseModel(accepted: false);
                                }
                            }
                            else
                            {
                                modelSnapshotGeneration = null;
                                modelResolving = true;
                                CaptureCurrentObservation();
                            }

                            break;
                        }

                    case 11:
                    default:
                        {
                            var width = random.Next(1, 25);
                            var height = random.Next(1, 12);
                            transcript.Add($"{step}:resize:{width}x{height}");
                            await surface.ResizeAsync(new Size(width, height));
                            break;
                        }
                }

                AssertModel(step);
            }

            foreach (var id in completions.Keys.Where(id => !settled.Contains(id)).ToArray())
            {
                transcript.Add($"drain:complete:{id}:{queries[id]}");
                completions[id].SetResult(ResultsFor(queries[id]));
                _ = settled.Add(id);
                await observations[id].WaitAsync(TestContext.Current.CancellationToken);
            }

            transcript.Any(entry => entry.Contains(":complete:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":accept:", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.Contains(":selector-failure", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.EndsWith(":detach", StringComparison.Ordinal)).ShouldBeTrue();
            transcript.Any(entry => entry.EndsWith(":attach", StringComparison.Ordinal)).ShouldBeTrue();
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                $"SuggestionInput randomized transcript failed for seed {seed}.\n{string.Join(Environment.NewLine, transcript)}",
                exception);
        }
        finally
        {
            if (!modelAttached)
            {
                await surface.UpdateAsync(() => root.Children.Add(input), "reattach randomized suggestion input for disposal");
            }
        }
    }
}
