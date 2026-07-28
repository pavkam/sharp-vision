// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Runtime;

using Terminal.Capabilities;

/// <summary>Identifies only terminal-description preflight rejection for status mapping.</summary>
internal sealed class UnsupportedTerminalException: NotSupportedException
{
    /// <summary>Initializes one typed preflight rejection.</summary>
    /// <param name="resolution">The non-null immutable resolution evidence.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resolution"/> is null.</exception>
    public UnsupportedTerminalException(DescriptionResult resolution) : base(CreateMessage(resolution)) =>
        Resolution = resolution;

    /// <summary>Gets the immutable typed result that caused preflight rejection.</summary>
    public DescriptionResult Resolution { get; }

    private static string CreateMessage(DescriptionResult resolution)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        var message = resolution.Profile is { } profile
            ? $"Terminal description '{profile.Description.Name}' is {profile.Description.Suitability}."
            : $"Terminal description resolution returned {resolution.Status}.";

        return resolution.Diagnostics.Count == 0
            ? message
            : $"{message} Diagnostics: {string.Join(", ", resolution.Diagnostics.Select(static value => value.Code))}.";
    }
}
