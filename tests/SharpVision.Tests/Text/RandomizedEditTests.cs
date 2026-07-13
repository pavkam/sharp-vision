// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Text;

using SharpVision.Terminal.Unicode;
using SharpVision.Text;


/// <summary>Proves deterministic grapheme-safe editing across mixed Unicode sequences.</summary>
public sealed class RandomizedEditTests
{
    private const int _caseCount = 10_000;
    private const int _seed = 0x00ED_175A;
    private static readonly string[] _insertions =
    [
        "a",
        "界",
        "e\u0301",
        "👩‍💻",
        "🇵🇹",
        "\uD800",
        "",
    ];

    /// <summary>Verifies mixed operations never create a split index or exceed maximum length.</summary>
    [Fact]
    public void Apply_WhenOperationsAreRandomized_PreservesEveryEditInvariant()
    {
        EditResult first = Replay(_seed);
        EditResult second = Replay(_seed);

        second.ShouldBe(first);
    }

    private static EditResult Replay(int seed)
    {
        Random random = new(seed);
        EditResult state = new(string.Empty, new Selection(0, 0), changed: false);

        for (int sample = 0; sample < _caseCount; sample++)
        {
            state = random.Next(0, 5) switch
            {
                0 => Edit.Replace(
                    state.Text,
                    state.Selection,
                    _insertions[random.Next(_insertions.Length)],
                    maxLength: 64),
                1 => Edit.Backspace(state.Text, state.Selection),
                2 => Edit.Delete(state.Text, state.Selection),
                3 => Edit.MovePrevious(state.Text, state.Selection, random.Next(0, 2) == 0),
                _ => Edit.MoveNext(state.Text, state.Selection, random.Next(0, 2) == 0),
            };

            string context = $"seed=0x{seed:X8}, case={sample}";
            Edit.Validate(state.Text, state.Selection);
            Edit.GraphemeCount(state.Text).ShouldBeLessThanOrEqualTo(64, context);
            Boundary(state.Text, state.Selection.Anchor).ShouldBeTrue(context);
            Boundary(state.Text, state.Selection.Caret).ShouldBeTrue(context);
        }

        return state;
    }

    private static bool Boundary(string value, int index)
    {
        if (index is 0 || index == value.Length)
        {
            return true;
        }

        foreach (Grapheme grapheme in Graphemes.Enumerate(value))
        {
            if (grapheme.Offset == index)
            {
                return true;
            }
        }

        return false;
    }
}
