// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>Classifies one non-sensitive terminal-description diagnostic.</summary>
[PublicAPI]
public enum DescriptionDiagnosticCode
{
    /// <summary>A canonical identifier was queried through the wrong ncurses type.</summary>
    WrongType,

    /// <summary>A retained parameter program was malformed or exceeded a finite limit.</summary>
    InvalidProgram,

    /// <summary>A retained command requested unsupported output padding.</summary>
    UnsupportedPadding,

    /// <summary>A required full-screen command was absent after validation.</summary>
    MissingRequired,

    /// <summary>The native provider failed while loading or copying an entry.</summary>
    NativeFailure,

    /// <summary>Restoring or releasing native terminal state failed.</summary>
    CleanupFailure,

    /// <summary>An inline TERMCAP value exceeded its fixed historical bound.</summary>
    TermcapLimit,

    /// <summary>Two canonical key names supplied one conflicting exact byte sequence.</summary>
    ConflictingKey,

    /// <summary>setupterm could not distinguish a missing entry from a generic description.</summary>
    MissingOrGeneric,

    /// <summary>The relevant live host environment exceeded a configured lookup bound.</summary>
    EnvironmentLimit,

    /// <summary>An explicitly permitted ANSI profile replaced absent Unix provider evidence.</summary>
    AnsiFallback,

    /// <summary>An owned terminal description exceeded a configured retention limit.</summary>
    DescriptionLimit,

    /// <summary>An optional terminal key string was malformed, unreachable, or over its parser limit.</summary>
    InvalidKey = 12
}
