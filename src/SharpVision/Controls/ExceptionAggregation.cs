// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

using System.Runtime.ExceptionServices;

/// <summary>Preserves the first failure while ordered control cleanup continues.</summary>
internal static class ExceptionAggregation
{
    /// <summary>Runs one action and captures its failure when no earlier failure exists.</summary>
    /// <param name="action">The non-null synchronous action.</param>
    /// <param name="failure">The first captured failure, or null.</param>
    public static void Capture(Action action, ref ExceptionDispatchInfo? failure)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
        }
        catch (Exception exception)
        {
            failure ??= ExceptionDispatchInfo.Capture(exception);
        }
    }
}
