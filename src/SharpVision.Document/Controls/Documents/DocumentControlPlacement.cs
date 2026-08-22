// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls.Documents;

/// <summary>Describes one embedded control's projected content-relative rectangle.</summary>
internal readonly struct DocumentControlPlacement
{
    /// <summary>Initializes a placement.</summary>
    /// <param name="control">The embedded control.</param>
    /// <param name="bounds">Its content-relative rectangle.</param>
    public DocumentControlPlacement(ControlBase control, Rect bounds)
    {
        Debug.Assert(control is not null, "A placement always identifies its control.");
        Control = control;
        Bounds = bounds;
    }

    /// <summary>Gets the embedded control.</summary>
    public ControlBase Control { get; }

    /// <summary>Gets its content-relative rectangle.</summary>
    public Rect Bounds { get; }
}
