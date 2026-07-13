// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

using System.Diagnostics.CodeAnalysis;

/// <summary>Identifies the input path that completed one semantic activation.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Pointer is the conventional terminal input path name.")]
public enum ActivationCause
{
    /// <summary>A keyboard command completed activation.</summary>
    Keyboard,

    /// <summary>A primary pointer release completed activation.</summary>
    Pointer,

    /// <summary>A direct public API completed activation.</summary>
    Programmatic,
}
