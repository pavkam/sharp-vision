// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Controls;

/// <summary>Widens <see cref="Container"/>'s protected <see cref="Border"/>/<see cref="Shadow"/>
/// authoring surface to public, for the panel-shaped containers whose whole purpose is letting a
/// caller author their own chrome directly (<c>Dock</c>, <c>Grid</c>, <c>Overlay</c>, <c>Stack</c>).
/// Not widened on <see cref="Container"/> itself: other <see cref="Container"/>-derived types (e.g.
/// <c>TablePresenter</c>, <c>StatusBarHost</c>, <c>ListViewHost</c>) are composite parts that
/// intentionally do not want to expose raw chrome authoring, since they own their own semantic
/// style instead - publicizing on <see cref="Container"/> itself would leak it onto those.</summary>
[PublicAPI]
public abstract class ChromeAuthoringContainer: Container
{
    /// <summary>Gets or sets the complete locally authored border.</summary>
    public new Border Border { get => base.Border; set => base.Border = value; }

    /// <summary>Returns border ownership to the active Theme.</summary>
    public new void ResetBorder() => base.ResetBorder();

    /// <summary>Gets or sets the complete locally authored shadow.</summary>
    public new Shadow Shadow { get => base.Shadow; set => base.Shadow = value; }

    /// <summary>Returns shadow ownership to the active Theme.</summary>
    public new void ResetShadow() => base.ResetShadow();
}
