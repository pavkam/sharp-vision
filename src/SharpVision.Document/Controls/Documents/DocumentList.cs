// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one bulleted or numbered list.</summary>
/// <remarks>
/// <para>
/// Nest a list by adding it to a <see cref="DocumentListItem"/>'s own
/// <see cref="DocumentListItem.Blocks"/>, matching CommonMark's model where <c>"- Item"</c> followed by
/// an indented <c>"- Nested"</c> is one item owning both its paragraph and a nested list. Nesting
/// depth is derived from the tree during layout rather than stored, so moving a list between items -
/// or out to the top level - always renders at its true depth.
/// </para>
/// <para>
/// Markers never collide with content: the document measures the widest marker in the list and
/// reserves exactly that much gutter, so a list numbering past nine or ninety-nine stays aligned.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class DocumentList: DocumentBlock
{
    /// <summary>Initializes an empty bulleted list.</summary>
    public DocumentList() => Items = new DocumentListItemCollection(this);

    /// <summary>Initializes an empty list with a marker style.</summary>
    /// <param name="kind">The marker style.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is unknown.</exception>
    public DocumentList(DocumentListKind kind) : this()
    {
        ArgumentOutOfRangeException.ThrowIfNotDefined(kind, nameof(kind), "The list marker style is unknown.");
        Kind = kind;
    }

    /// <summary>Gets or sets the marker style applied to every item.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public DocumentListKind Kind
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The list marker style is unknown.");
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets or sets the non-negative first ordinal used by a numbered list.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int Start
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    } = 1;

    /// <summary>Gets or sets whether the list separates its items with a blank line.</summary>
    /// <remarks>
    /// False is CommonMark's tight list: items follow one another directly. True is its loose list,
    /// which suits items whose content runs to several blocks.
    /// </remarks>
    public bool IsLoose
    {
        get;
        set
        {
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets the owned ordered items.</summary>
    public DocumentListItemCollection Items { get; }
}
