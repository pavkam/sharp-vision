// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Identifies mounted behavioral evidence required for one concrete control.</summary>
[Flags]
internal enum ComponentBehavior
{
    /// <summary>No behavioral evidence.</summary>
    None = 0,

    /// <summary>The control renders through a mounted application.</summary>
    Mounted = 1 << 0,

    /// <summary>The control or its semantic face observes pointer hover.</summary>
    Hover = 1 << 1,

    /// <summary>The control explicitly excludes pointer hover.</summary>
    HoverExcluded = 1 << 2,

    /// <summary>The control owns keyboard focus.</summary>
    Focus = 1 << 3,

    /// <summary>The control explicitly excludes direct keyboard focus.</summary>
    FocusExcluded = 1 << 4,

    /// <summary>The control participates in sequential Tab navigation.</summary>
    Tab = 1 << 5,

    /// <summary>The control explicitly excludes sequential Tab navigation.</summary>
    TabExcluded = 1 << 6,

    /// <summary>The control assigns semantic behavior to arrow or range-navigation keys.</summary>
    Directional = 1 << 7,

    /// <summary>The control explicitly leaves directional keys to another owner.</summary>
    DirectionalExcluded = 1 << 8,

    /// <summary>The control exposes semantic held and released press state.</summary>
    PressRelease = 1 << 9,

    /// <summary>The control explicitly has no semantic press state.</summary>
    PressReleaseExcluded = 1 << 10,

    /// <summary>The control exposes semantic activation or selection.</summary>
    Activation = 1 << 11,

    /// <summary>The control cleans input state when it becomes unavailable.</summary>
    UnavailableCleanup = 1 << 12,

    /// <summary>The control owns or participates in a transient layer.</summary>
    Transient = 1 << 13,

    /// <summary>The control participates in public retained composition.</summary>
    Composition = 1 << 14,

    /// <summary>Terminal pointer input produces the control's semantic activation.</summary>
    PointerActivation = 1 << 15,

    /// <summary>Terminal keyboard input produces the control's semantic activation.</summary>
    KeyboardActivation = 1 << 16,

    /// <summary>Terminal pointer input on a retained descendant reaches the public owner.</summary>
    RetainedPointerActivation = 1 << 17,

    /// <summary>A held press produces an asserted complete semantic frame.</summary>
    PressedFrame = 1 << 18,

    /// <summary>The control proves direct and ancestor-inherited disabled state, disabled
    /// appearance, stable geometry across a genuine resize, and re-enable recovery on the
    /// mounted surface.</summary>
    Disabled = 1 << 19,

    /// <summary>The control explicitly excludes its own disabled evidence because its disabled
    /// contract is fully proven by a base class. Currently unused; it exists for future
    /// exceptions.</summary>
    DisabledExcluded = 1 << 20
}
