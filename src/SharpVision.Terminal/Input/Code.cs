// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Terminal.Input;

/// <summary>Identifies a logical keyboard key independently of wire encoding.</summary>
[PublicAPI]
public enum Code
{
    /// <summary>A valid but unmapped native key.</summary>
    Unknown = 0,

    /// <summary>A Unicode character key.</summary>
    Character,

    /// <summary>The Escape key.</summary>
    Escape,

    /// <summary>The Enter key.</summary>
    Enter,

    /// <summary>The Tab key.</summary>
    Tab,

    /// <summary>The Backspace key.</summary>
    Backspace,

    /// <summary>The upward cursor key.</summary>
    Up,

    /// <summary>The downward cursor key.</summary>
    Down,

    /// <summary>The leftward cursor key.</summary>
    Left,

    /// <summary>The rightward cursor key.</summary>
    Right,

    /// <summary>The Home key.</summary>
    Home,

    /// <summary>The End key.</summary>
    End,

    /// <summary>The Insert key.</summary>
    Insert,

    /// <summary>The Delete key.</summary>
    Delete,

    /// <summary>The Page Up key.</summary>
    PageUp,

    /// <summary>The Page Down key.</summary>
    PageDown,

    /// <summary>Function key F1.</summary>
    F1 = 16,

    /// <summary>Function key F2.</summary>
    F2,

    /// <summary>Function key F3.</summary>
    F3,

    /// <summary>Function key F4.</summary>
    F4,

    /// <summary>Function key F5.</summary>
    F5,

    /// <summary>Function key F6.</summary>
    F6,

    /// <summary>Function key F7.</summary>
    F7,

    /// <summary>Function key F8.</summary>
    F8,

    /// <summary>Function key F9.</summary>
    F9,

    /// <summary>Function key F10.</summary>
    F10,

    /// <summary>Function key F11.</summary>
    F11,

    /// <summary>Function key F12.</summary>
    F12,

    /// <summary>Function key F13.</summary>
    F13,

    /// <summary>Function key F14.</summary>
    F14,

    /// <summary>Function key F15.</summary>
    F15,

    /// <summary>Function key F16.</summary>
    F16,

    /// <summary>Function key F17.</summary>
    F17,

    /// <summary>Function key F18.</summary>
    F18,

    /// <summary>Function key F19.</summary>
    F19,

    /// <summary>Function key F20.</summary>
    F20,

    /// <summary>Function key F21.</summary>
    F21,

    /// <summary>Function key F22.</summary>
    F22,

    /// <summary>Function key F23.</summary>
    F23,

    /// <summary>Function key F24.</summary>
    F24,

    /// <summary>Function key F25.</summary>
    F25,

    /// <summary>Function key F26.</summary>
    F26,

    /// <summary>Function key F27.</summary>
    F27,

    /// <summary>Function key F28.</summary>
    F28,

    /// <summary>Function key F29.</summary>
    F29,

    /// <summary>Function key F30.</summary>
    F30,

    /// <summary>Function key F31.</summary>
    F31,

    /// <summary>Function key F32.</summary>
    F32,

    /// <summary>Function key F33.</summary>
    F33,

    /// <summary>Function key F34.</summary>
    F34,

    /// <summary>Function key F35.</summary>
    F35,

    /// <summary>The Caps Lock key.</summary>
    CapsLock = 51,

    /// <summary>The Scroll Lock key.</summary>
    ScrollLock,

    /// <summary>The Num Lock key.</summary>
    NumLock,

    /// <summary>The Print Screen key.</summary>
    PrintScreen,

    /// <summary>The Pause key.</summary>
    Pause,

    /// <summary>The application menu key.</summary>
    Menu,

    /// <summary>The keypad center or Begin key.</summary>
    Begin = 57,

    /// <summary>Function key F36.</summary>
    F36 = 58,

    /// <summary>Function key F37.</summary>
    F37 = 59,

    /// <summary>Function key F38.</summary>
    F38 = 60,

    /// <summary>Function key F39.</summary>
    F39 = 61,

    /// <summary>Function key F40.</summary>
    F40 = 62,

    /// <summary>Function key F41.</summary>
    F41 = 63,

    /// <summary>Function key F42.</summary>
    F42 = 64,

    /// <summary>Function key F43.</summary>
    F43 = 65,

    /// <summary>Function key F44.</summary>
    F44 = 66,

    /// <summary>Function key F45.</summary>
    F45 = 67,

    /// <summary>Function key F46.</summary>
    F46 = 68,

    /// <summary>Function key F47.</summary>
    F47 = 69,

    /// <summary>Function key F48.</summary>
    F48 = 70,

    /// <summary>Function key F49.</summary>
    F49 = 71,

    /// <summary>Function key F50.</summary>
    F50 = 72,

    /// <summary>Function key F51.</summary>
    F51 = 73,

    /// <summary>Function key F52.</summary>
    F52 = 74,

    /// <summary>Function key F53.</summary>
    F53 = 75,

    /// <summary>Function key F54.</summary>
    F54 = 76,

    /// <summary>Function key F55.</summary>
    F55 = 77,

    /// <summary>Function key F56.</summary>
    F56 = 78,

    /// <summary>Function key F57.</summary>
    F57 = 79,

    /// <summary>Function key F58.</summary>
    F58 = 80,

    /// <summary>Function key F59.</summary>
    F59 = 81,

    /// <summary>Function key F60.</summary>
    F60 = 82,

    /// <summary>Function key F61.</summary>
    F61 = 83,

    /// <summary>Function key F62.</summary>
    F62 = 84,

    /// <summary>Function key F63.</summary>
    F63 = 85
}
