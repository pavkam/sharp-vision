// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Traverses every registered ownership edge for test-only structural assertions.</summary>
internal static class OwnedTree
{
    /// <summary>Finds the first descendant of the requested type in stable ownership order.</summary>
    /// <typeparam name="T">The control type to locate.</typeparam>
    /// <param name="root">The non-null root whose complete owned tree is searched.</param>
    /// <returns>The first matching control, or null when no match exists.</returns>
    internal static T? Find<T>(Control root) where T : Control
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root is T match)
        {
            return match;
        }

        for (var index = 0; index < root.OwnedControlCount; index++)
        {
            if (Find<T>(root.OwnedControlAt(index)) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
