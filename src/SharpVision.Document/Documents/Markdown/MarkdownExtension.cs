// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Documents.Markdown;

/// <summary>Identifies independently selectable non-CommonMark syntax families.</summary>
[Flags]
[PublicAPI]
public enum MarkdownExtension
{
    /// <summary>Enables only baseline CommonMark syntax.</summary>
    None = 0,

    /// <summary>Enables GFM strikethrough delimiters.</summary>
    Strikethrough = 1 << 0,

    /// <summary>Enables GFM pipe tables.</summary>
    Tables = 1 << 1,

    /// <summary>Enables GFM task-list markers.</summary>
    TaskLists = 1 << 2,

    /// <summary>Enables extended URL autolinks.</summary>
    Autolinks = 1 << 3,

    /// <summary>Enables double-bracket wiki links.</summary>
    WikiLinks = 1 << 4,

    /// <summary>Enables Obsidian callout markers in block quotes.</summary>
    Callouts = 1 << 5,

    /// <summary>Enables interactive radio-list markers.</summary>
    RadioLists = 1 << 6,

    /// <summary>Enables all GitHub Flavored Markdown additions.</summary>
    GitHubFlavored = Strikethrough | Tables | TaskLists | Autolinks,

    /// <summary>Enables every supported optional syntax family.</summary>
    All = GitHubFlavored | WikiLinks | Callouts | RadioLists
}
