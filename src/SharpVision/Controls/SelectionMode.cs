// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Diagnostics.CodeAnalysis;

/// <summary>Selects whether List permits no, one, or many selected indexes.</summary>
[SuppressMessage(
    "Naming",
    "CA1720:Identifier contains type name",
    Justification = "Single is the conventional selection-mode name.")]
public enum SelectionMode
{
    /// <summary>Disables item selection while retaining navigation and invocation.</summary>
    None,

    /// <summary>Permits at most one selected item.</summary>
    Single,

    /// <summary>Permits independently selected items and modifier ranges.</summary>
    Multiple,
}
