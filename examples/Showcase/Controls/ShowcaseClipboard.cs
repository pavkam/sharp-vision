// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Showcase.Controls;

/// <summary>
/// Provides every <c>CodeView</c> recipe access to the one running Showcase Application's real
/// clipboard, despite recipes living several composite layers below <see cref="Gallery"/> in the
/// retained tree.
/// </summary>
/// <remarks>
/// <see cref="SharpVision.Controls.SyntaxHighlighting.CodeView.ClipboardWriter"/> is deliberately
/// not auto-discovered the way <c>TextInput</c>'s is: that mechanism is hard-typed to
/// <c>TextInput</c> in core <c>SharpVision</c> and cannot be extended from another assembly. The
/// natural alternative - <see cref="Gallery"/> walking its own newly built page tree after every
/// navigation to find and wire each <c>CodeView</c> directly - is not reachable either:
/// <c>CompositeControlBase.Content</c> is <c>protected</c>, so <c>DocPage</c>, <c>DocSection</c>,
/// and <c>DocExample</c> are opaque boxes from outside their own declaring code, even to the
/// Gallery that owns them. Since this Showcase is a single-process, single-instance console
/// application with exactly one <see cref="Application"/> ever attached at a time, one
/// process-wide indirection is a reasonable trade for library controls that would otherwise have
/// no way to reach it at all - <see cref="Gallery.OnAttach"/> is the one place that both has real
/// <see cref="Application"/> access and runs before any user input is possible, and
/// <see cref="DocExample"/> wires every recipe's <c>ClipboardWriter</c> to <see cref="Write"/>
/// unconditionally at construction time, long before that attachment ever happens - the
/// indirection resolves lazily, at the moment a user actually presses Ctrl+C or opens Copy, by
/// which point attachment has always already completed.
/// </remarks>
internal static class ShowcaseClipboard
{
    /// <summary>Gets or sets the delegate that writes to the currently attached Application's real
    /// clipboard. Null before the first <see cref="Gallery"/> attaches.</summary>
    internal static Action<string>? Writer { get; set; }

    /// <summary>Writes to the current clipboard writer, if one is attached yet; otherwise a
    /// silent no-op, matching <see cref="IClipboard.Write"/>'s own graceful-degradation contract
    /// for an unsupported terminal.</summary>
    /// <param name="value">The non-null text to copy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    internal static void Write(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Writer?.Invoke(value);
    }
}
