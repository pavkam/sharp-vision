// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Represents one activatable link flowing inside a <see cref="DocumentParagraph"/> or
/// <see cref="DocumentHeading"/>.</summary>
/// <remarks>
/// <para>
/// The owning <see cref="Document"/> - not this data node - owns link focus and input: it paints every link with
/// <see cref="DocumentStyle.LinkFace"/>, highlights the one at <see cref="Document.ActiveLink"/> with
/// <see cref="DocumentStyle.ActiveLinkFace"/>, moves between links on Tab and Shift+Tab, and raises
/// <see cref="Clicked"/> on Enter, Space, or a primary click. Arrow keys scroll the document rather
/// than moving between links.
/// </para>
/// <para>
/// <see cref="DocumentInlineContainer.Inlines"/> carries the link label, including semantic nested
/// emphasis. A link that wraps across lines stays one logical link and remains activatable on every
/// line it occupies.
/// </para>
/// <para>
/// <see cref="Target"/> is independent of <see cref="Clicked"/>. Setting it emits an OSC 8 terminal
/// hyperlink around the link's cells so a capable terminal can offer its own open or copy affordance;
/// handling <see cref="Clicked"/> is what makes the link do something inside the application. Either,
/// both, or neither is valid.
/// </para>
/// </remarks>
[PublicAPI]
public sealed class DocumentLink: DocumentInlineContainer
{
    /// <summary>Initializes an empty link.</summary>
    public DocumentLink()
    {
    }

    /// <summary>Initializes a link with non-null literal text.</summary>
    /// <param name="text">The non-null literal text.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is null.</exception>
    public DocumentLink(string text) : this()
    {
        ArgumentNullException.ThrowIfNull(text);
        Inlines.Add(new DocumentTextRun(TextMarkup.Escape(text)));
    }

    /// <summary>Initializes a link with non-null literal text and an OSC 8 target.</summary>
    /// <param name="text">The non-null literal text.</param>
    /// <param name="target">The non-null OSC 8 hyperlink target.</param>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> or <paramref name="target"/> is null.</exception>
    public DocumentLink(string text, string target) : this(text)
    {
        ArgumentNullException.ThrowIfNull(target);
        Target = target;
    }

    /// <summary>Raised after the link is activated by Enter, Space, or a primary click.</summary>
    public event EventHandler<EventArgs>? Clicked;

    /// <summary>Gets or sets a convenience plain-text label.</summary>
    /// <remarks>Assigning replaces all current inline label content with one escaped
    /// <see cref="DocumentTextRun"/>. Reading flattens semantic inline containers to visible source
    /// text and represents hard and soft breaks with newline and space respectively.</remarks>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public string Text
    {
        get
        {
            var text = new StringBuilder();

            foreach (var inline in Inlines)
            {
                AppendText(text, inline);
            }

            return text.ToString();
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            VerifyMutable();

            Inlines.Clear();

            if (value.Length > 0)
            {
                Inlines.Add(new DocumentTextRun(TextMarkup.Escape(value)));
            }
        }
    }

    /// <summary>Gets or sets the OSC 8 hyperlink target emitted around this link's cells, or null to
    /// emit none.</summary>
    /// <remarks>
    /// A terminal without OSC 8 support ignores the target and renders the link's text unchanged, so
    /// setting it is always safe.
    /// </remarks>
    public string? Target
    {
        get;
        set
        {
            VerifyMutable();

            if (string.Equals(field, value, StringComparison.Ordinal))
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Gets or sets whether the link can be focused and activated.</summary>
    /// <remarks>
    /// A disabled link renders with <see cref="DocumentStyle.DisabledLinkFace"/> regardless of
    /// <see cref="Emphasis"/>, is skipped by link navigation, and never raises <see cref="Clicked"/>.
    /// </remarks>
    public bool IsEnabled
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
            OwnerDocument?.OnLinkAvailabilityChanged(this);
            InvalidateContent();
        }
    } = true;

    /// <summary>Gets or sets which <see cref="DocumentStyle"/> face family paints this link.</summary>
    /// <remarks>
    /// Emphasis is a presentation choice, not a behavioral one: an <see cref="DocumentLinkEmphasis.Action"/>
    /// link is exactly as focusable and activatable as a <see cref="DocumentLinkEmphasis.Standard"/>
    /// one. Mixing emphases within one paragraph - an ordinary inline link beside a call-to-action
    /// button - is how a single <see cref="Document"/> renders both without needing a second,
    /// separately styled document.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is unknown.</exception>
    public DocumentLinkEmphasis Emphasis
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNotDefined(value, nameof(value), "The link emphasis is unknown.");
            VerifyMutable();

            if (field == value)
            {
                return;
            }

            field = value;
            InvalidateContent();
        }
    }

    /// <summary>Raises <see cref="Clicked"/> for an enabled link.</summary>
    internal void Activate()
    {
        if (IsEnabled)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }

    private static void AppendText(StringBuilder text, DocumentInline inline)
    {
        switch (inline)
        {
            case DocumentTextRun run:
                _ = TextMarkup.Parse(run.Text.AsSpan(), out var display);
                _ = text.Append(display);
                break;
            case DocumentCodeSpan code:
                _ = text.Append(code.Text);
                break;
            case DocumentSoftBreak:
                _ = text.Append(' ');
                break;
            case DocumentLineBreak:
                _ = text.Append('\n');
                break;
            case DocumentInlineControl:
                break;
            case DocumentInlineContainer container:
                foreach (var child in container.Inlines)
                {
                    AppendText(text, child);
                }

                break;
            default:
                throw new UnreachableException(
                    "DocumentInline's hierarchy is closed to this assembly, so every inline kind is handled.");
        }
    }
}
