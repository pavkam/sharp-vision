// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Allocates stable global routed-handler registration order.</summary>
internal static class Sequence
{
    private static long _value;

    /// <summary>Gets a unique global order for a newly added registration.</summary>
    internal static long Next() => Interlocked.Increment(ref _value);

    /// <summary>Gets the latest order included in a new route snapshot.</summary>
    internal static long Current => Volatile.Read(ref _value);
}
