// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Diagnostics;

/// <summary>Identifies the redacted source that recognized one terminal backend family.</summary>
[PublicAPI]
public enum TerminalBackendEvidenceSource
{
    /// <summary>The loaded terminal-description name identified the family.</summary>
    Description,

    /// <summary>Owned process-environment metadata identified the family.</summary>
    Environment
}
