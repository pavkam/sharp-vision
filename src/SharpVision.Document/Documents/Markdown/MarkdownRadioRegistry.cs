// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents.Markdown;

using System.Runtime.CompilerServices;

using SharpVision.Controls.Input;

/// <summary>Tracks radios created by the Markdown reader so presentation scoping never relies on a
/// caller-visible group-name prefix.</summary>
internal static class MarkdownRadioRegistry
{
    private static readonly ConditionalWeakTable<RadioButton, object> _generated = [];

    /// <summary>Marks one parser-created radio for per-document group scoping.</summary>
    /// <param name="radio">The generated radio.</param>
    internal static void Register(RadioButton radio)
    {
        ArgumentNullException.ThrowIfNull(radio);
        _generated.Add(radio, new object());
    }

    /// <summary>Gets whether a radio originated in Markdown parsing.</summary>
    /// <param name="radio">The candidate radio.</param>
    /// <returns>True only for a registered generated radio.</returns>
    internal static bool IsGenerated(RadioButton radio)
    {
        ArgumentNullException.ThrowIfNull(radio);
        return _generated.TryGetValue(radio, out _);
    }
}
