// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Support;

/// <summary>Supplies an externally implementable style with deliberately invalid impact metadata.</summary>
internal sealed class InvalidImpactStyle: IControlStyle
{
    event EventHandler<ThemeChangedEventArgs>? IControlStyle.Changed
    {
        add { }
        remove { }
    }

    Type IControlStyle.TargetType => typeof(Control);

    bool IControlStyle.IsFrozen => true;

    ChangeImpact IControlStyle.AggregateImpact => (ChangeImpact) 99;

    bool IControlStyle.TryGetValue(
        IStyleProperty styleProperty,
        State state,
        out object? value)
    {
        _ = styleProperty;
        _ = state;
        value = null;
        return false;
    }
}
