// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Dialogs;

/// <summary>Defines the semantic result returned by a completed <see cref="MessageBox"/>.</summary>
[PublicAPI]
public enum MessageBoxResult
{
    /// <summary>The user chose OK.</summary>
    Ok,

    /// <summary>The user chose Cancel or dismissed the message box.</summary>
    Cancel,

    /// <summary>The user chose Yes.</summary>
    Yes,

    /// <summary>The user chose No.</summary>
    No
}
