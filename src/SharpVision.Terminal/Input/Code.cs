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
    F63 = 85,

    /// <summary>The keypad 0 key.</summary>
    Keypad0 = 86,

    /// <summary>The keypad 1 key.</summary>
    Keypad1 = 87,

    /// <summary>The keypad 2 key.</summary>
    Keypad2 = 88,

    /// <summary>The keypad 3 key.</summary>
    Keypad3 = 89,

    /// <summary>The keypad 4 key.</summary>
    Keypad4 = 90,

    /// <summary>The keypad 5 key.</summary>
    Keypad5 = 91,

    /// <summary>The keypad 6 key.</summary>
    Keypad6 = 92,

    /// <summary>The keypad 7 key.</summary>
    Keypad7 = 93,

    /// <summary>The keypad 8 key.</summary>
    Keypad8 = 94,

    /// <summary>The keypad 9 key.</summary>
    Keypad9 = 95,

    /// <summary>The keypad decimal separator key.</summary>
    KeypadDecimal = 96,

    /// <summary>The keypad divide key.</summary>
    KeypadDivide = 97,

    /// <summary>The keypad multiply key.</summary>
    KeypadMultiply = 98,

    /// <summary>The keypad subtract key.</summary>
    KeypadSubtract = 99,

    /// <summary>The keypad add key.</summary>
    KeypadAdd = 100,

    /// <summary>The keypad Enter key.</summary>
    KeypadEnter = 101,

    /// <summary>The keypad equal key.</summary>
    KeypadEqual = 102,

    /// <summary>The keypad separator key.</summary>
    KeypadSeparator = 103,

    /// <summary>The keypad left cursor key.</summary>
    KeypadLeft = 104,

    /// <summary>The keypad right cursor key.</summary>
    KeypadRight = 105,

    /// <summary>The keypad up cursor key.</summary>
    KeypadUp = 106,

    /// <summary>The keypad down cursor key.</summary>
    KeypadDown = 107,

    /// <summary>The keypad Page Up key.</summary>
    KeypadPageUp = 108,

    /// <summary>The keypad Page Down key.</summary>
    KeypadPageDown = 109,

    /// <summary>The keypad Home key.</summary>
    KeypadHome = 110,

    /// <summary>The keypad End key.</summary>
    KeypadEnd = 111,

    /// <summary>The keypad Insert key.</summary>
    KeypadInsert = 112,

    /// <summary>The keypad Delete key.</summary>
    KeypadDelete = 113,

    /// <summary>The media Play key.</summary>
    MediaPlay = 114,

    /// <summary>The media Pause key.</summary>
    MediaPause = 115,

    /// <summary>The media Play/Pause key.</summary>
    MediaPlayPause = 116,

    /// <summary>The media Reverse key.</summary>
    MediaReverse = 117,

    /// <summary>The media Stop key.</summary>
    MediaStop = 118,

    /// <summary>The media Fast Forward key.</summary>
    MediaFastForward = 119,

    /// <summary>The media Rewind key.</summary>
    MediaRewind = 120,

    /// <summary>The media next-track key.</summary>
    MediaTrackNext = 121,

    /// <summary>The media previous-track key.</summary>
    MediaTrackPrevious = 122,

    /// <summary>The media Record key.</summary>
    MediaRecord = 123,

    /// <summary>The lower-volume key.</summary>
    LowerVolume = 124,

    /// <summary>The raise-volume key.</summary>
    RaiseVolume = 125,

    /// <summary>The mute-volume key.</summary>
    MuteVolume = 126,

    /// <summary>The left Shift key, pressed alone rather than as a modifier on another key.</summary>
    LeftShift = 127,

    /// <summary>The left Control key, pressed alone rather than as a modifier on another key.</summary>
    LeftControl = 128,

    /// <summary>The left Alt key, pressed alone rather than as a modifier on another key.</summary>
    LeftAlt = 129,

    /// <summary>The left Super key, pressed alone rather than as a modifier on another key.</summary>
    LeftSuper = 130,

    /// <summary>The left Hyper key, pressed alone rather than as a modifier on another key.</summary>
    LeftHyper = 131,

    /// <summary>The left Meta key, pressed alone rather than as a modifier on another key.</summary>
    LeftMeta = 132,

    /// <summary>The right Shift key, pressed alone rather than as a modifier on another key.</summary>
    RightShift = 133,

    /// <summary>The right Control key, pressed alone rather than as a modifier on another key.</summary>
    RightControl = 134,

    /// <summary>The right Alt key, pressed alone rather than as a modifier on another key.</summary>
    RightAlt = 135,

    /// <summary>The right Super key, pressed alone rather than as a modifier on another key.</summary>
    RightSuper = 136,

    /// <summary>The right Hyper key, pressed alone rather than as a modifier on another key.</summary>
    RightHyper = 137,

    /// <summary>The right Meta key, pressed alone rather than as a modifier on another key.</summary>
    RightMeta = 138,

    /// <summary>The ISO Level3 Shift key.</summary>
    IsoLevel3Shift = 139,

    /// <summary>The ISO Level5 Shift key.</summary>
    IsoLevel5Shift = 140
}
