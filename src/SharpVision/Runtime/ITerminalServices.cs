// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

/// <summary>Exposes implemented terminal output protocols to an application.</summary>
public interface ITerminalServices
{
    /// <summary>Gets the terminal alert.</summary>
    public IBell Bell { get; }

    /// <summary>Gets clipboard access.</summary>
    public IClipboard Clipboard { get; }

    /// <summary>Sets the window title using OSC 2.</summary>
    /// <param name="title">The non-null title.</param>
    /// <exception cref="ArgumentNullException"><paramref name="title"/> is null.</exception>
    public void SetTitle(string title);
}
