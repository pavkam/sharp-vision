// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Collections;

using System.Text.Json;

/// <summary>Describes one projected JSON display line as independently styled syntax segments.</summary>
internal sealed class JsonViewLine
{
    /// <summary>Initializes one display line.</summary>
    /// <param name="leading">Text before the selectable label.</param>
    /// <param name="label">The selectable property or array-index token.</param>
    /// <param name="separator">Punctuation between the label and value.</param>
    /// <param name="value">The scalar lexeme or structural punctuation.</param>
    /// <param name="suffix">Punctuation after the value.</param>
    /// <param name="valueKind">The scalar syntax kind, or null when the value is punctuation.</param>
    /// <param name="node">The represented entry, or null for structural lines.</param>
    internal JsonViewLine(
        string leading,
        string label,
        string separator,
        string value,
        string suffix,
        JsonValueKind? valueKind,
        JsonViewNode? node)
    {
        Leading = leading;
        Label = label;
        Separator = separator;
        Value = value;
        Suffix = suffix;
        ValueKind = valueKind;
        Node = node;
        Text = $"{leading}{label}{separator}{value}{suffix}";
    }

    /// <summary>Gets text before the selectable label.</summary>
    internal string Leading { get; }

    /// <summary>Gets the selectable token, or an empty string for structural lines.</summary>
    internal string Label { get; }

    /// <summary>Gets punctuation between the selectable token and value.</summary>
    internal string Separator { get; }

    /// <summary>Gets the scalar lexeme or structural punctuation.</summary>
    internal string Value { get; }

    /// <summary>Gets punctuation after the value.</summary>
    internal string Suffix { get; }

    /// <summary>Gets the scalar syntax kind, or null when <see cref="Value"/> is punctuation.</summary>
    internal JsonValueKind? ValueKind { get; }

    /// <summary>Gets the complete rendered line.</summary>
    internal string Text { get; }

    /// <summary>Gets the represented entry, or null for structural lines.</summary>
    internal JsonViewNode? Node { get; }
}
