// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace Snake;

/// <summary>Current phase of the game lifecycle.</summary>
public enum GamePhase
{
    /// <summary>Title screen with menu and high scores.</summary>
    Title,

    /// <summary>Active gameplay.</summary>
    Playing,

    /// <summary>Snake just died — flash animation.</summary>
    DeathAnimation,

    /// <summary>Entering initials for high score.</summary>
    HighScoreEntry,

    /// <summary>Game over summary before returning to title.</summary>
    GameOver,

    /// <summary>Paused during gameplay.</summary>
    Paused
}
