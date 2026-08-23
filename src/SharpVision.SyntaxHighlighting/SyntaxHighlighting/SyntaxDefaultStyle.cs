// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>
/// Identifies one of the Kate/KSyntaxHighlighting default token-style roles (the XML format's
/// <c>dsNormal</c>, <c>dsKeyword</c>, and so on), independent of any specific syntax definition
/// or theme's colors.
/// </summary>
/// <remarks>
/// Every <see cref="SyntaxItemData"/> in a loaded <see cref="SyntaxDefinition"/> resolves to
/// exactly one of these roles. SharpVision colors tokens purely by role through
/// <c>CodeViewStyle</c>: a syntax definition's own optional literal
/// <c>color</c>/<c>bold</c>/<c>italic</c> hints are intentionally not read, so a theme swap
/// always restyles every embedded and external definition consistently.
/// </remarks>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Char, String, and Float directly name their KDE default-style roles dsChar, dsString, and dsFloat.")]
[PublicAPI]
public enum SyntaxDefaultStyle
{
    /// <summary>Normal text with no special meaning.</summary>
    Normal,

    /// <summary>A language keyword.</summary>
    Keyword,

    /// <summary>A function or method call or declaration.</summary>
    Function,

    /// <summary>A variable name.</summary>
    Variable,

    /// <summary>Control-flow keywords such as <c>if</c>, <c>else</c>, or <c>break</c>.</summary>
    ControlFlow,

    /// <summary>An operator such as <c>+</c>, <c>-</c>, or <c>::</c>.</summary>
    Operator,

    /// <summary>A built-in language class or function.</summary>
    BuiltIn,

    /// <summary>An identifier introduced by a well-known extension, such as Boost or Qt.</summary>
    Extension,

    /// <summary>A preprocessor statement.</summary>
    Preprocessor,

    /// <summary>An attribute or annotation, such as Java's <c>@Override</c>.</summary>
    Attribute,

    /// <summary>A single character literal.</summary>
    Char,

    /// <summary>An escaped character within a string or character literal.</summary>
    SpecialChar,

    /// <summary>A string literal.</summary>
    String,

    /// <summary>A verbatim string, such as a here-document.</summary>
    VerbatimString,

    /// <summary>A special string, such as a regular expression or LaTeX math.</summary>
    SpecialString,

    /// <summary>An include, import, or module statement.</summary>
    Import,

    /// <summary>A data-type name.</summary>
    DataType,

    /// <summary>A decimal numeric literal.</summary>
    DecimalValue,

    /// <summary>A numeric literal with a base other than 10, such as hexadecimal or octal.</summary>
    BaseN,

    /// <summary>A floating-point numeric literal.</summary>
    Float,

    /// <summary>A language constant.</summary>
    Constant,

    /// <summary>A comment.</summary>
    Comment,

    /// <summary>A comment that is API documentation, such as a Doxygen block.</summary>
    Documentation,

    /// <summary>An annotation within a documentation comment, such as Doxygen's <c>@param</c>.</summary>
    Annotation,

    /// <summary>A variable referenced within a documentation comment.</summary>
    CommentVariable,

    /// <summary>A folding region marker.</summary>
    RegionMarker,

    /// <summary>Informational text within a documentation comment, such as <c>@note</c>.</summary>
    Information,

    /// <summary>A warning within a documentation comment, such as <c>@warning</c>.</summary>
    Warning,

    /// <summary>An alert message, such as <c>TODO</c> or <c>FIXME</c>, typically in a comment.</summary>
    Alert,

    /// <summary>Text that does not fit any other role.</summary>
    Others,

    /// <summary>Text identified as an error by the syntax definition.</summary>
    Error,
}
