// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Publishes one temporal value payload through its owner's current transaction.</summary>
/// <typeparam name="T">The immutable temporal value type.</typeparam>
/// <param name="transition">The current-aware owner transaction.</param>
/// <param name="previous">The previous nullable value.</param>
/// <param name="current">The committed nullable value.</param>
internal delegate void TemporalValueChangedPublisher<T>(
    ref CallbackTransitionTransaction transition,
    T? previous,
    T? current)
    where T : struct;
