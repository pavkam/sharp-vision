// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Defines one complete immutable <see cref="Document"/> presentation. This style
/// declares no theme section of its own: it falls back to <see cref="ControlStyle"/>'s "control"
/// role section for its passive chrome, resolves its own document-specific faces from semantic
/// colors, and is themeable only through that fallback and a locally assigned
/// <see cref="Document.Style"/>.</summary>
/// <remarks>
/// <para>
/// A terminal has no true font size, so <see cref="DocumentHeading"/> levels differentiate through
/// weight, color, and underline rather than scale: <see cref="HeadingFace"/> colors and underlines
/// levels 1 and 2, while levels 3 through 6 render in the body face with bold weight added.
/// </para>
/// <para>
/// The document resolves every face and glyph here at paint time rather than caching them onto its
/// nodes, so a live theme swap or a local <see cref="Document.Style"/> assignment restyles the whole
/// tree on the next frame with no possibility of a stale node.
/// </para>
/// </remarks>
[PublicAPI]
public sealed record DocumentStyle: ControlStyle
{
    /// <summary>Gets the primary document-style definition.</summary>
    internal static StyleDefinition<DocumentStyle> Definition { get; } =
        StyleDefinitions.Control(
        static theme => theme.GetStyleSet(ControlStyle.Default),
        Complete,
        static (previous, previousTheme, current, currentTheme) =>
            Compare(previous, previousTheme, current, currentTheme));

    /// <summary>Initializes a complete document presentation.</summary>
    /// <param name="face">The complete normal body face.</param>
    /// <param name="border">The complete normal border.</param>
    /// <param name="shadow">The complete normal shadow.</param>
    /// <param name="headingFace">The complete level 1 and 2 heading face.</param>
    /// <param name="markerFace">The complete list-marker face.</param>
    /// <param name="quoteFace">The complete block-quote bar and content face.</param>
    /// <param name="codeFace">The complete preformatted code-block face.</param>
    /// <param name="ruleFace">The complete thematic-break face.</param>
    /// <param name="calloutFace">The complete callout-body face.</param>
    /// <param name="calloutTitleFace">The complete callout-title face.</param>
    /// <param name="tableFace">The complete table body and border face.</param>
    /// <param name="tableHeaderFace">The complete table header face.</param>
    /// <param name="linkFace">The complete inactive <see cref="DocumentLinkEmphasis.Standard"/> link face.</param>
    /// <param name="activeLinkFace">The complete focused <see cref="DocumentLinkEmphasis.Standard"/> link face.</param>
    /// <param name="disabledLinkFace">The complete disabled link face, shared by both emphasis kinds.</param>
    /// <param name="actionLinkFace">The complete inactive <see cref="DocumentLinkEmphasis.Action"/> link face.</param>
    /// <param name="activeActionLinkFace">The complete focused <see cref="DocumentLinkEmphasis.Action"/> link face.</param>
    /// <param name="glyphs">The complete bullet, quote-bar, and rule glyph family.</param>
    /// <param name="selectionFace">The optional complete semantic-text selection face.</param>
    [SetsRequiredMembers]
    public DocumentStyle(
        Face face,
        Border border,
        Shadow shadow,
        Face headingFace,
        Face markerFace,
        Face quoteFace,
        Face codeFace,
        Face ruleFace,
        Face calloutFace,
        Face calloutTitleFace,
        Face tableFace,
        Face tableHeaderFace,
        Face linkFace,
        Face activeLinkFace,
        Face disabledLinkFace,
        Face actionLinkFace,
        Face activeActionLinkFace,
        DocumentGlyphs glyphs,
        Face? selectionFace = null) : base(face, border, shadow)
    {
        HeadingFace = headingFace;
        MarkerFace = markerFace;
        QuoteFace = quoteFace;
        CodeFace = codeFace;
        RuleFace = ruleFace;
        CalloutFace = calloutFace;
        CalloutTitleFace = calloutTitleFace;
        TableFace = tableFace;
        TableHeaderFace = tableHeaderFace;
        LinkFace = linkFace;
        ActiveLinkFace = activeLinkFace;
        DisabledLinkFace = disabledLinkFace;
        ActionLinkFace = actionLinkFace;
        ActiveActionLinkFace = activeActionLinkFace;
        Glyphs = glyphs;
        SelectionFace = selectionFace ?? new Face(
            new ControlColor(SemanticColor.SelectedText),
            new ControlColor(SemanticColor.SelectedControl),
            TerminalAttributes.None,
            Underline.None,
            Color.Default);
    }

    /// <summary>Gets the standard document presentation.</summary>
    // A bare Theme, not ThemeCatalog.Dark: this style has no internal access to SharpVision's own
    // Theme.Unthemed (declared in a different assembly with no InternalsVisibleTo grant here), and
    // Complete never reads the theme it is given, so any valid instance resolves identically.
    public static new DocumentStyle Default => Complete(ControlStyle.Default, VisualState.Normal, new Theme());

    /// <summary>Gets the complete face used for level 1 and 2 headings. Levels 3 through 6 render in
    /// the plain <see cref="ControlStyle.Face"/> with bold weight added.</summary>
    public required Face HeadingFace { get; init; }

    /// <summary>Gets the complete face used for a list item's bullet or number.</summary>
    public required Face MarkerFace { get; init; }

    /// <summary>Gets the complete face used for a block quote's bar and quoted content.</summary>
    public required Face QuoteFace { get; init; }

    /// <summary>Gets the complete face used for a preformatted code block.</summary>
    public required Face CodeFace { get; init; }

    /// <summary>Gets the complete face used for a thematic break.</summary>
    public required Face RuleFace { get; init; }

    /// <summary>Gets the complete face used for callout body content.</summary>
    public required Face CalloutFace { get; init; }

    /// <summary>Gets the complete face used for a callout's generated kind and title.</summary>
    public required Face CalloutTitleFace { get; init; }

    /// <summary>Gets the complete face used for table body cells and borders.</summary>
    public required Face TableFace { get; init; }

    /// <summary>Gets the complete face used for table header cells and borders.</summary>
    public required Face TableHeaderFace { get; init; }

    /// <summary>Gets the complete face used for a <see cref="DocumentLinkEmphasis.Standard"/> link
    /// that is not focused.</summary>
    public required Face LinkFace { get; init; }

    /// <summary>Gets the complete face used for the <see cref="DocumentLinkEmphasis.Standard"/> link
    /// at <see cref="Document.ActiveLink"/> while the document has focus.</summary>
    public required Face ActiveLinkFace { get; init; }

    /// <summary>Gets the complete face used for a link whose <see cref="DocumentLink.IsEnabled"/> is
    /// false, regardless of its <see cref="DocumentLink.Emphasis"/>.</summary>
    /// <remarks>
    /// A disabled link is disabled either way, so it needs no separate action-emphasis variant:
    /// graying it out already communicates its state without a distinct chip appearance to gray out.
    /// </remarks>
    public required Face DisabledLinkFace { get; init; }

    /// <summary>Gets the complete face used for a <see cref="DocumentLinkEmphasis.Action"/> link that
    /// is not focused.</summary>
    /// <remarks>
    /// This is what lets a link read as a call-to-action button - a solid, high-contrast chip -
    /// entirely through the style system. Nothing about <see cref="DocumentLink"/> itself changes: an
    /// action-emphasis link is exactly as interactive as a standard one, just painted differently.
    /// </remarks>
    public required Face ActionLinkFace { get; init; }

    /// <summary>Gets the complete face used for the <see cref="DocumentLinkEmphasis.Action"/> link at
    /// <see cref="Document.ActiveLink"/> while the document has focus.</summary>
    public required Face ActiveActionLinkFace { get; init; }

    /// <summary>Gets the complete face applied to selected semantic document glyphs.</summary>
    public required Face SelectionFace { get; init; }

    /// <summary>Gets the complete bullet, quote-bar, and rule glyph family.</summary>
    public required DocumentGlyphs Glyphs { get; init; }

    private static DocumentStyle Complete(ControlStyle control, VisualState _, Theme theme) => new(
        control.Face,
        control.Border,
        control.Shadow,
        // Literal Bold, not SemanticDecoration.FocusedText: FocusedText is the interactive-focus
        // decoration, and a theme that redefines it for that purpose (for example to Reverse - the
        // exact scenario the borderless-focus fallback anticipates) must not also reskin this static
        // typography. Every bundled theme resolved the deleted per-theme "sharpVision.document"
        // sections' own authoring to plain Bold, so a literal preserves exactly what a themed user
        // already saw. MarkerFace below previously, pre-stream, defaulted to None - Bold is the
        // deliberate keep-what-themed-users-saw choice for it too.
        headingFace: new Face(
            new ControlColor(SemanticColor.Accent),
            Color.Transparent,
            TerminalAttributes.Bold,
            Underline.Straight,
            new ControlColor(SemanticColor.Accent)),
        markerFace: new Face(
            new ControlColor(SemanticColor.Accent),
            Color.Transparent,
            TerminalAttributes.Bold,
            Underline.None,
            Color.Default),
        // Quoted text is still text a reader needs to read, not secondary chrome, so it keeps the
        // full-contrast ControlText foreground - matching this style's own ordinary body face -
        // and is set apart from the surrounding body purely through the typographic convention for
        // a quotation, not by dimming it. A muted foreground on a transparent background previously
        // left quoted paragraphs barely readable against a themed surface background.
        quoteFace: new Face(
            new ControlColor(SemanticColor.ControlText),
            Color.Transparent,
            TerminalAttributes.Italic,
            Underline.None,
            Color.Default),
        codeFace: new Face(
            new ControlColor(SemanticColor.SurfaceText),
            new ControlColor(SemanticColor.Surface),
            TerminalAttributes.None,
            Underline.None,
            Color.Default),
        // A thematic break is a decorative divider rather than text a reader consumes, so a muted,
        // lower-contrast treatment is the correct choice here - unlike a quote's actual content.
        ruleFace: new Face(
            new ControlColor(SemanticColor.Muted),
            Color.Transparent,
            TerminalAttributes.None,
            Underline.None,
            Color.Default),
        calloutFace: new Face(
            new ControlColor(SemanticColor.Warning),
            Color.Transparent,
            TerminalAttributes.None,
            Underline.None,
            Color.Default),
        calloutTitleFace: new Face(
            new ControlColor(SemanticColor.Warning),
            Color.Transparent,
            TerminalAttributes.Bold,
            Underline.None,
            Color.Default),
        tableFace: new Face(
            new ControlColor(SemanticColor.SurfaceText),
            new ControlColor(SemanticColor.Surface),
            TerminalAttributes.None,
            Underline.None,
            Color.Default),
        tableHeaderFace: new Face(
            new ControlColor(SemanticColor.Accent),
            new ControlColor(SemanticColor.Surface),
            TerminalAttributes.Bold,
            Underline.None,
            Color.Default),
        // Info, not Blue: Blue resolves identical to Accent in 8 of the 15 bundled themes, and
        // document headings are painted with Accent. Info is the one chromatic-adjacent color the
        // theme test suite guarantees stays distinct from Accent, so a link stays visibly distinct
        // from a heading under every bundled theme. The underline mirrors the foreground, the same
        // pairing every other visibly underlined face in this style keeps.
        linkFace: new Face(
            new ControlColor(SemanticColor.Info),
            Color.Transparent,
            TerminalAttributes.None,
            Underline.Straight,
            new ControlColor(SemanticColor.Info)),
        activeLinkFace: new Face(
            new ControlColor(SemanticColor.SelectedText),
            new ControlColor(SemanticColor.SelectedControl),
            TerminalAttributes.None,
            Underline.Straight,
            new ControlColor(SemanticColor.SelectedText)),
        disabledLinkFace: new Face(
            new ControlColor(SemanticColor.DisabledText),
            Color.Transparent,
            TerminalAttributes.None,
            Underline.None,
            Color.Default),
        // Selected and pressed pairs are the two semantic combinations every theme must already keep
        // legible against every background - selection highlighting and press feedback are basic UI
        // needs no theme can get away with skipping - which is exactly the guarantee a solid button
        // chip needs and an arbitrary color like the general accent does not carry on its own.
        actionLinkFace: new Face(
            new ControlColor(SemanticColor.SelectedText),
            new ControlColor(SemanticColor.SelectedControl),
            TerminalAttributes.Bold,
            Underline.None,
            Color.Default),
        activeActionLinkFace: new Face(
            new ControlColor(SemanticColor.PressedText),
            new ControlColor(SemanticColor.PressedControl),
            TerminalAttributes.Bold,
            Underline.None,
            Color.Default),
        glyphs: DocumentGlyphs.Default);

    // A glyph replacement can change how many cells a marker or bar occupies, which moves the text
    // beside it, so it must reach measurement. Every face is resolved fresh during the paint pass,
    // so a face replacement only ever needs a repaint.
    [Pure]
    private static InvalidationImpact Compare(
        DocumentStyle previous,
        Theme? previousTheme,
        DocumentStyle current,
        Theme? currentTheme)
    {
        if (previous.Glyphs != current.Glyphs)
        {
            return InvalidationImpact.Measure;
        }

        var facesDiffer = !FaceResolvesEqually(previous.HeadingFace, previousTheme, current.HeadingFace, currentTheme) ||
            !FaceResolvesEqually(previous.MarkerFace, previousTheme, current.MarkerFace, currentTheme) ||
            !FaceResolvesEqually(previous.QuoteFace, previousTheme, current.QuoteFace, currentTheme) ||
            !FaceResolvesEqually(previous.CodeFace, previousTheme, current.CodeFace, currentTheme) ||
            !FaceResolvesEqually(previous.RuleFace, previousTheme, current.RuleFace, currentTheme) ||
            !FaceResolvesEqually(previous.CalloutFace, previousTheme, current.CalloutFace, currentTheme) ||
            !FaceResolvesEqually(previous.CalloutTitleFace, previousTheme, current.CalloutTitleFace, currentTheme) ||
            !FaceResolvesEqually(previous.TableFace, previousTheme, current.TableFace, currentTheme) ||
            !FaceResolvesEqually(previous.TableHeaderFace, previousTheme, current.TableHeaderFace, currentTheme) ||
            !FaceResolvesEqually(previous.LinkFace, previousTheme, current.LinkFace, currentTheme) ||
            !FaceResolvesEqually(previous.ActiveLinkFace, previousTheme, current.ActiveLinkFace, currentTheme) ||
            !FaceResolvesEqually(previous.DisabledLinkFace, previousTheme, current.DisabledLinkFace, currentTheme) ||
            !FaceResolvesEqually(previous.ActionLinkFace, previousTheme, current.ActionLinkFace, currentTheme) ||
            !FaceResolvesEqually(previous.ActiveActionLinkFace, previousTheme, current.ActiveActionLinkFace, currentTheme) ||
            !FaceResolvesEqually(previous.SelectionFace, previousTheme, current.SelectionFace, currentTheme) ||
            !SemanticColorResolvesEqually(SemanticColor.Info, previousTheme, currentTheme) ||
            !SemanticColorResolvesEqually(SemanticColor.Success, previousTheme, currentTheme) ||
            !SemanticColorResolvesEqually(SemanticColor.Accent, previousTheme, currentTheme) ||
            !SemanticColorResolvesEqually(SemanticColor.Warning, previousTheme, currentTheme) ||
            !SemanticColorResolvesEqually(SemanticColor.Error, previousTheme, currentTheme);

        return facesDiffer ? InvalidationImpact.Render : InvalidationImpact.None;
    }

    private static bool FaceResolvesEqually(
        Face previous,
        Theme? previousTheme,
        Face current,
        Theme? currentTheme) =>
        previous == current &&
        ResolveAttributes(previous.Attributes, previousTheme) == ResolveAttributes(current.Attributes, currentTheme) &&
        Document.ResolveDocumentColor(previous.Foreground, previousTheme) ==
            Document.ResolveDocumentColor(current.Foreground, currentTheme) &&
        Document.ResolveDocumentColor(previous.Background, previousTheme) ==
            Document.ResolveDocumentColor(current.Background, currentTheme) &&
        Document.ResolveDocumentColor(previous.UnderlineColor, previousTheme) ==
            Document.ResolveDocumentColor(current.UnderlineColor, currentTheme);

    private static TerminalAttributes ResolveAttributes(ControlDecoration value, Theme? theme) =>
        value.IsLiteral
            ? value.Literal
            : theme?.ResolveAttributes(value.SemanticDecoration) ?? TerminalAttributes.None;

    private static bool SemanticColorResolvesEqually(
        SemanticColor color,
        Theme? previousTheme,
        Theme? currentTheme) =>
        previousTheme?.ResolveColor(color) == currentTheme?.ResolveColor(color);
}
