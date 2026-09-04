// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Document;

/// <summary>Identifies which <see cref="DocumentStyle"/> face paints one laid-out run.</summary>
/// <remarks>
/// Layout records the face kind rather than a resolved color so the paint pass can resolve against
/// the current <see cref="DocumentStyle"/> every frame. A theme swap therefore restyles the document
/// without rebuilding a single line.
/// </remarks>
internal enum DocumentFaceKind
{
    /// <summary>Ordinary body text, painted with <see cref="ControlStyle.Face"/>.</summary>
    Body,

    /// <summary>A level 1 or 2 heading, painted with <see cref="DocumentStyle.HeadingFace"/>.</summary>
    Heading,

    /// <summary>A level 3 through 6 heading, painted with the body face plus bold weight.</summary>
    MinorHeading,

    /// <summary>A list item's bullet or number, painted with <see cref="DocumentStyle.MarkerFace"/>.</summary>
    Marker,

    /// <summary>Block-quote content or its bar, painted with <see cref="DocumentStyle.QuoteFace"/>.</summary>
    Quote,

    /// <summary>Preformatted text, painted with <see cref="DocumentStyle.CodeFace"/>.</summary>
    Code,

    /// <summary>A thematic break, painted with <see cref="DocumentStyle.RuleFace"/>.</summary>
    Rule,

    /// <summary>Callout body content.</summary>
    Callout,

    /// <summary>A callout's generated kind and title.</summary>
    CalloutTitle,

    /// <summary>A note callout's body and bar.</summary>
    CalloutNote,

    /// <summary>A note callout's generated kind and title.</summary>
    CalloutNoteTitle,

    /// <summary>A tip callout's body and bar.</summary>
    CalloutTip,

    /// <summary>A tip callout's generated kind and title.</summary>
    CalloutTipTitle,

    /// <summary>An important callout's body and bar.</summary>
    CalloutImportant,

    /// <summary>An important callout's generated kind and title.</summary>
    CalloutImportantTitle,

    /// <summary>A warning callout's body and bar.</summary>
    CalloutWarning,

    /// <summary>A warning callout's generated kind and title.</summary>
    CalloutWarningTitle,

    /// <summary>A caution callout's body and bar.</summary>
    CalloutCaution,

    /// <summary>A caution callout's generated kind and title.</summary>
    CalloutCautionTitle,

    /// <summary>Table body cells and borders.</summary>
    Table,

    /// <summary>Table header cells and borders.</summary>
    TableHeader,

    /// <summary>Link text, painted with the inactive, active, or disabled link face according to the
    /// link's own state and the document's focus.</summary>
    Link
}
