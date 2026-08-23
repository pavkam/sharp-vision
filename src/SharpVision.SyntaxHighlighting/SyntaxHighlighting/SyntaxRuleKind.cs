// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.SyntaxHighlighting;

/// <summary>Identifies which KDE context-rule element a <see cref="SyntaxRule"/> represents.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Float and Integer directly name the KDE rule elements <Float> and <Int>.")]
[PublicAPI]
public enum SyntaxRuleKind
{
    /// <summary>A <c>&lt;keyword&gt;</c> rule matching one word against a named keyword list.</summary>
    Keyword,

    /// <summary>A <c>&lt;Float&gt;</c> rule matching a floating-point literal.</summary>
    Float,

    /// <summary>A <c>&lt;HlCOct&gt;</c> rule matching a C-style octal literal.</summary>
    Octal,

    /// <summary>A <c>&lt;HlCHex&gt;</c> rule matching a C-style hexadecimal literal.</summary>
    Hex,

    /// <summary>An <c>&lt;Int&gt;</c> rule matching a run of decimal digits.</summary>
    Integer,

    /// <summary>A <c>&lt;DetectChar&gt;</c> rule matching one exact character.</summary>
    Character,

    /// <summary>A <c>&lt;Detect2Chars&gt;</c> rule matching two exact consecutive characters.</summary>
    TwoCharacter,

    /// <summary>An <c>&lt;AnyChar&gt;</c> rule matching one character from a set.</summary>
    AnyCharacter,

    /// <summary>A <c>&lt;StringDetect&gt;</c> rule matching one literal string.</summary>
    StringMatch,

    /// <summary>A <c>&lt;WordDetect&gt;</c> rule matching one literal string at word boundaries.</summary>
    WordMatch,

    /// <summary>A <c>&lt;RegExpr&gt;</c> rule matching a regular expression.</summary>
    RegularExpression,

    /// <summary>A <c>&lt;LineContinue&gt;</c> rule matching a trailing line-continuation character.</summary>
    LineContinuation,

    /// <summary>A <c>&lt;HlCStringChar&gt;</c> rule matching one C-style escape sequence.</summary>
    EscapedCharacter,

    /// <summary>A <c>&lt;RangeDetect&gt;</c> rule matching from a start character to an end character.</summary>
    Range,

    /// <summary>A <c>&lt;HlCChar&gt;</c> rule matching a single-quoted C-style character literal.</summary>
    QuotedCharacter,

    /// <summary>An <c>&lt;IncludeRules&gt;</c> rule splicing another context's rules in place.</summary>
    IncludeRules,

    /// <summary>A <c>&lt;DetectSpaces&gt;</c> rule matching a run of whitespace.</summary>
    DetectSpaces,

    /// <summary>
    /// A <c>&lt;DetectIdentifier&gt;</c> rule matching a letter/underscore followed by letters,
    /// digits, or underscores.
    /// </summary>
    DetectIdentifier,
}
