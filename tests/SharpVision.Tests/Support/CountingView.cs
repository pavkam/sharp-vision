// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;


/// <summary>Records Build invocations and exposes the installed content for View tests.</summary>
internal sealed class CountingView: View
{
    private readonly Control _content;

    /// <summary>Initializes a view that installs the given control on its first Build.</summary>
    /// <param name="content">The non-null control to install.</param>
    internal CountingView(Control content) => _content = content;

    /// <summary>Gets the number of Build invocations.</summary>
    internal int BuildCount { get; private set; }

    /// <summary>Gets the control installed by Build, or null before Build has run.</summary>
    internal Control? Installed => Content;

    /// <inheritdoc/>
    protected override Control Build()
    {
        BuildCount++;
        return _content;
    }
}
