// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Kitty.Keyboard;

/// <summary>Identifies Kitty progressive keyboard enhancements.</summary>
[Flags]
[PublicAPI]
public enum KittyKeyboardEnhancement
{
    /// <summary>No progressive enhancement.</summary>
    None = 0,

    /// <summary>Disambiguate legacy escape-code overlaps.</summary>
    Disambiguate = 1,

    /// <summary>Report press, repeat, and release event types.</summary>
    EventTypes = 2,

    /// <summary>Report shifted and base-layout alternate keys.</summary>
    AlternateKeys = 4,

    /// <summary>Report all keys through escape sequences.</summary>
    AllKeys = 8,

    /// <summary>Report text code points associated with key events.</summary>
    AssociatedText = 16
}
