// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Capabilities;

/// <summary>
/// Names the environment variables passive detection and active negotiation read, so
/// <see cref="EnvironmentSnapshot"/>'s allowlist and every reader agree by construction
/// rather than by review.
/// </summary>
internal static class EvidenceEnvironmentVars
{
    /// <summary>The terminal type identifier.</summary>
    public const string Term = "TERM";

    /// <summary>The color-capability hint some terminals set independently of TERM.</summary>
    public const string ColorTerm = "COLORTERM";

    /// <summary>Presence (any value, including empty) requests that color output be
    /// disabled, per the no-color.org convention.</summary>
    public const string NoColor = "NO_COLOR";

    /// <summary>The terminal application's own self-identification.</summary>
    public const string TermProgram = "TERM_PROGRAM";

    /// <summary>
    /// The terminal application's self-reported version. Currently unused by evidence
    /// detection; retained in the recognized-keys allowlist for forward compatibility.
    /// </summary>
    public const string TermProgramVersion = "TERM_PROGRAM_VERSION";

    /// <summary>Presence indicates an active tmux session.</summary>
    public const string Tmux = "TMUX";

    /// <summary>Presence indicates an active GNU screen session.</summary>
    public const string Sty = "STY";

    /// <summary>Presence indicates a remote SSH session (connection details).</summary>
    public const string SshConnection = "SSH_CONNECTION";

    /// <summary>Presence indicates a remote SSH session (allocated tty path).</summary>
    public const string SshTty = "SSH_TTY";
}
