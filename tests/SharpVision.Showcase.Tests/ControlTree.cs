// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Tests;

/// <summary>Traverses every registered control ownership edge for showcase behavior tests.</summary>
internal static class ControlTree
{
    /// <summary>Finds every control of one type in stable ownership order.</summary>
    /// <typeparam name="T">The control type to collect.</typeparam>
    /// <param name="root">The non-null root to visit.</param>
    /// <returns>The collected controls in depth-first ownership order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    internal static IReadOnlyList<T> FindAll<T>(Control root) where T : Control
    {
        ArgumentNullException.ThrowIfNull(root);
        List<T> matches = [];
        Visit(root, matches);
        return matches;
    }

    /// <summary>Concatenates every marked Text source string in stable ownership order.</summary>
    /// <param name="root">The non-null root to visit.</param>
    /// <returns>The concatenated marked text.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is null.</exception>
    internal static string Text(Control root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var text = new StringBuilder();

        foreach (var control in FindAll<ControlText>(root))
        {
            _ = text.AppendLine(control.Content);
        }

        return text.ToString();
    }

    private static void Visit<T>(Control control, List<T> matches) where T : Control
    {
        if (control is T match)
        {
            matches.Add(match);
        }

        control.VisitChildren(child => Visit(child, matches));
    }
}
