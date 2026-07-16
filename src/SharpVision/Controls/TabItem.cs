// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Defines one content page with a header label inside a <see cref="TabControl"/>.</summary>
public sealed class TabItem: ContentControl
{
    /// <summary>Initializes an empty tab page with no header.</summary>
    public TabItem() { }

    /// <summary>Gets or sets the non-null header label shown in the tab bar.</summary>
    public string Header { get; set { ArgumentNullException.ThrowIfNull(value); _ = SetProperty(ref field, value, ChangeImpact.Measure); } } = string.Empty;
}
