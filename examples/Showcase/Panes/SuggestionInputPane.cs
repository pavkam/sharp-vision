// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Panes;

using Text = SharpVision.Controls.Display.Text;

/// <summary>Documents editable asynchronous suggestions with live resolution and acceptance evidence.</summary>
internal sealed class SuggestionInputPane: CompositeControlBase
{
    private static readonly string[] _destinations =
    [
        "Lisboa — Portugal",
        "Berlin — Germany",
        "Dublin — Ireland",
        "Tbilisi — Georgia",
        "Tallinn — Estonia",
        "Kigali — Rwanda",
        "Split — Croatia",
        "Lille — France",
        "Lima — Perú",
        "Lincoln — England",
        "Limerick — Ireland",
        "Alicante — España",
        "Cali — Colombia",
        "Bali — Indonesia",
        "Belize City — Belize",
        "Alice Springs — Australia",
        "Zürich — Schweiz",
        "東京 — 日本",
        "São Paulo — Brasil",
        "Reykjavík — Ísland"
    ];

    internal SuggestionInputPane() => InitializeContent(CreateContent());

    /// <summary>The exact catalog/page name.</summary>
    internal const string Title = "SuggestionInput";

    private static DocPage CreateContent()
    {
        var status = new Text("Status: type at least 2 graphemes.")
        {
            Width = Length.Cells(38),
            Height = Length.Cells(1),
            Overflow = Overflow.Clip
        };
        var activity = new Text()
        {
            Width = Length.Cells(38),
            Height = Length.Cells(4),
            Overflow = Overflow.Wrap
        };
        var activityEntries = new List<string>();

        void AppendActivity(string entry)
        {
            activityEntries.Add(entry);

            while (activityEntries.Count > 3)
            {
                activityEntries.RemoveAt(0);
            }

            activity.Content = "<d>Recent activity</d>\n" +
                string.Join('\n', activityEntries.Select(static value => Text.Escape($"• {value}")));
        }

        var suggestion = new SuggestionInput
        {
            Width = Length.Cells(38),
            Placeholder = "Search destinations…",
            StartAffix = new Affix("⌕", "?"),
            MinimumPrefixLength = 2,
            Resolver = ResolveDestinationsAsync,
            DropDownHeight = Length.Cells(5),
            ScrollBars = ScrollBars.Vertical,
            ShowScrollBars = ShowScrollBars.WhenNeeded
        };
        suggestion.PropertyChanged += (_, eventArgs) =>
        {
            if (eventArgs.PropertyName == nameof(SuggestionInput.IsResolving))
            {
                status.Content = suggestion.IsResolving
                    ? Text.Escape($"Status: resolving ‘{suggestion.Text}’…")
                    : "Status: current request settled.";
            }
            else if (eventArgs.PropertyName == nameof(SuggestionInput.IsOpen))
            {
                AppendActivity(suggestion.IsOpen
                    ? "Suggestion popup opened."
                    : "Suggestion popup dismissed.");
            }
        };
        suggestion.SuggestionsChanged += (_, _) =>
        {
            status.Content = suggestion.Suggestions.Count == 0
                ? "Status: no current matches."
                : Text.Escape($"Status: {suggestion.Suggestions.Count} current matches.");
        };
        suggestion.SuggestionAccepted += (_, eventArgs) =>
        {
            status.Content = Text.Escape($"Accepted: {eventArgs.Item}");
            AppendActivity($"Accepted by {eventArgs.Cause}: {eventArgs.Item}");
        };
        suggestion.ResolutionFailed += (_, eventArgs) =>
        {
            status.Content = Text.Escape($"Failed for ‘{eventArgs.SearchTerms}’. Type again to recover.");
            AppendActivity($"Resolver failure: {eventArgs.Exception.Message}");
        };
        AppendActivity("Type “li” for long scrollable results.");

        var minimumOne = new Button { Text = "Min &1" };
        var minimumTwo = new Button { Text = "Min &2" };
        var minimumThree = new Button { Text = "Min &3" };
        minimumOne.Click += (_, _) => SetMinimumPrefixLength(suggestion, status, 1);
        minimumTwo.Click += (_, _) => SetMinimumPrefixLength(suggestion, status, 2);
        minimumThree.Click += (_, _) => SetMinimumPrefixLength(suggestion, status, 3);

        var race = new Button { Text = "Race &slow → swift" };
        race.Click += (_, _) =>
        {
            AppendActivity("Started slow, then swift; only swift may publish.");
            suggestion.Text = "slow";
            suggestion.Text = "swift";
        };
        var outside = new Button { Text = "Outside &test" };
        outside.Click += (_, _) =>
            AppendActivity("Outside action ran after the popup was already closed.");

        var stage = ShowcasePaneHelpers.OverlayStage(40, 9, clipToBounds: false);
        suggestion.VerticalAlignment = VerticalAlignment.Top;
        stage.Children.Add(suggestion);

        var disabled = new SuggestionInput
        {
            Width = Length.Cells(32),
            Text = "Reykjavík — locked",
            MinimumPrefixLength = 100,
            IsEnabled = false
        };
        var narrow = new SuggestionInput
        {
            Width = Length.Cells(14),
            Text = "東京 → Zürich",
            StartAffix = new Affix("⌕", "?"),
            MinimumPrefixLength = 100
        };

        return new DocPage(
            Title,
            "<info>SuggestionInput</info> keeps text freely editable while a cancellable resolver supplies copied suggestions that only explicit keyboard or pointer acceptance commits.",
            new DocSection(
                "⌕",
                "Live asynchronous suggestions",
                "Resolve Unicode destination names, inspect latest-query publication, and distinguish explicit acceptance from dismissal.",
                new DocExample(
                    "Search, race, accept, and dismiss",
                    "Type <reverse>li</reverse> for enough current matches to scroll. Use arrows and <reverse>Enter</reverse>, or click a row, to accept and log the activation cause. <reverse>Escape</reverse>, <reverse>Tab</reverse>, or the first click on <reverse>Outside test</reverse> dismisses without changing text. The threshold buttons re-evaluate the current grapheme count immediately. <reverse>Race slow → swift</reverse> proves a cancellation-ignoring stale request cannot replace the newer result; type <reverse>fail</reverse> to inspect recoverable failure publication.",
                    new DocColumn(
                        stage,
                        status,
                        new DocRow(minimumOne, minimumTwo, minimumThree),
                        new DocRow(race, outside),
                        activity),
                    "var input = new SuggestionInput\n{\n    Placeholder = \"Search destinations…\",\n    MinimumPrefixLength = 2,\n    Resolver = ResolveDestinationsAsync,\n    DropDownHeight = Length.Cells(5)\n};\ninput.SuggestionAccepted += (_, e) => Use(e.Item, e.Cause);")),
            new DocSection(
                "↔",
                "Availability and width",
                "The retained editor and popup share ordinary control availability and grapheme-safe constrained layout.",
                new DocExample(
                    "Disabled and narrow Unicode fields",
                    "The disabled field remains visibly unavailable. The narrow field clips complete terminal cells from <info>東京 → Zürich</info>; it never paints half of a wide grapheme or exposes the private popup and list.",
                    new DocColumn(disabled, narrow))));
    }

    private static void SetMinimumPrefixLength(SuggestionInput suggestion, Text status, int value)
    {
        suggestion.MinimumPrefixLength = value;
        status.Content = Text.Escape($"Status: minimum prefix is {value} grapheme{(value == 1 ? string.Empty : "s")}.");
    }

    private static async ValueTask<IReadOnlyList<object?>> ResolveDestinationsAsync(
        string searchTerms,
        CancellationToken cancellationToken)
    {
        if (searchTerms.StartsWith("slow", StringComparison.OrdinalIgnoreCase))
        {
            // This deliberate bad provider ignores cancellation so the specimen proves the
            // control's generation guard instead of relying on cooperative cancellation.
            await Task.Delay(700, CancellationToken.None);
            return ["slow result — stale if superseded"];
        }

        await Task.Delay(
            searchTerms.StartsWith("swift", StringComparison.OrdinalIgnoreCase) ? 70 : 140,
            cancellationToken);

        if (searchTerms.Equals("fail", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("the demonstration provider rejected this query");
        }

        if (searchTerms.StartsWith("swift", StringComparison.OrdinalIgnoreCase))
        {
            return ["swift result — newest request"];
        }

        var matches = new List<object?>();

        foreach (var destination in _destinations)
        {
            if (destination.Contains(searchTerms, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(destination);
            }
        }

        return matches;
    }
}
