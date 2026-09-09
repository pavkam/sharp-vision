// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Input;

/// <summary>Resolves the pressable sub-target, if any, at a pointer location in absolute
/// cells.</summary>
/// <typeparam name="TTarget">The pressable target type. A reference type or a value type.</typeparam>
/// <param name="cells">The pointer location to test, in absolute cells.</param>
/// <param name="target">The resolved target when this method returns true; otherwise the default
/// value for <typeparamref name="TTarget"/>.</param>
/// <returns><see langword="true"/> when <paramref name="cells"/> hits a pressable target.</returns>
/// <remarks>
/// Declared with an <see langword="out"/> parameter rather than a <c>TTarget?</c> return - unlike
/// a plain method return position, the C# compiler cannot give a single generic delegate shape
/// both a <see cref="Nullable{T}"/> return for a value-typed <typeparamref name="TTarget"/> and an
/// annotated nullable-reference return for a reference-typed one, so this mirrors the established
/// <c>TryGetValue</c> shape instead, which every substitution compiles correctly under. Public
/// because <c>ControlBase.EnableTargetedPressActivation</c> accepts one as a <see langword="protected"/>
/// parameter, and a protected member's parameter types must be at least as accessible as the
/// member itself.
/// </remarks>
public delegate bool TargetHitTest<TTarget>(Point cells, out TTarget target)
    where TTarget : notnull;
