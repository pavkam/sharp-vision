// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Layout;

using SharpVision.Controls.Input;

using NonNegativeValue = JetBrains.Annotations.NonNegativeValueAttribute;

/// <summary>Retains one private TextInput edit transaction for a Table.</summary>
internal sealed class TableEditState
{
    /// <summary>Initializes one validated edit snapshot.</summary>
    /// <param name="row">The owned row being edited.</param>
    /// <param name="columnIndex">The non-negative edited column.</param>
    /// <param name="editor">The non-null retained editor control.</param>
    /// <param name="originalText">The text to restore on cancel.</param>
    internal TableEditState(TableRow row, int columnIndex, TextInput editor, string originalText)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(originalText);
        Row = row;
        ColumnIndex = columnIndex;
        Editor = editor;
        OriginalText = originalText;
    }

    /// <summary>Gets the edited row.</summary>
    internal TableRow Row { get; }

    /// <summary>Gets the edited column.</summary>
    [NonNegativeValue]
    internal int ColumnIndex { get; }

    /// <summary>Gets the retained TextInput editor.</summary>
    internal TextInput Editor { get; }

    /// <summary>Gets the original text snapshot.</summary>
    internal string OriginalText { get; }
}
