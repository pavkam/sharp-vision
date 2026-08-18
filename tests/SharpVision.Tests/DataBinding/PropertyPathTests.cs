// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.DataBinding;

using System.Linq.Expressions;

using SharpVision.DataBinding;

using Support;

/// <summary>Verifies nested notifying source paths and null recovery.</summary>
public sealed class PropertyPathTests
{
    /// <summary>Verifies replacing an intermediate unsubscribes the old branch and observes the new branch.</summary>
    [Fact]
    public void Bind_WhenIntermediateChanges_RewiresTheBranch()
    {
        var oldAddress = new BindingAddress { City = "Porto" };
        var current = new BindingAddress { City = "Lisbon" };
        var model = new BindingModel { Address = oldAddress };
        var target = new ControlText();
        using var binding = target.Bind(model, source => source.Address!.City);

        model.Address = current;
        oldAddress.City = "ignored";
        current.City = "Coimbra";

        target.Content.ShouldBe("Coimbra");
    }

    /// <summary>Verifies an unavailable nested branch uses fallback and resumes when it becomes reachable.</summary>
    [Fact]
    public void Bind_WhenIntermediateIsNull_UsesFallbackAndRecovers()
    {
        var model = new BindingModel();
        var target = new ControlText("stale");
        using var binding = target.Bind(model, source => source.Address!.City);

        target.Content.ShouldBeEmpty();

        model.Address = new BindingAddress { City = "Braga" };

        target.Content.ShouldBe("Braga");
    }

    /// <summary>Verifies a reachable branch becoming null immediately restores fallback.</summary>
    [Fact]
    public void Bind_WhenIntermediateBecomesNull_RestoresFallback()
    {
        var model = new BindingModel { Address = new BindingAddress { City = "Faro" } };
        var target = new ControlText();
        using var binding = target.Bind(model, source => source.Address!.City);

        model.Address = null;

        target.Content.ShouldBeEmpty();
    }

    /// <summary>Verifies an all-properties notification re-reads the complete nested path.</summary>
    [Fact]
    public void Bind_WhenAllPropertiesAreRaised_RefreshesValue()
    {
        var model = new BindingModel { Address = new BindingAddress { City = "Évora" } };
        var target = new ControlText();
        using var binding = target.Bind(model, source => source.Address!.City);
        target.Content = "stale";

        model.RaiseAll();

        target.Content.ShouldBe("Évora");
    }

    /// <summary>Verifies unrelated source notifications do not refresh a bound leaf.</summary>
    [Fact]
    public void Bind_WhenUnrelatedPropertyIsRaised_IgnoresNotification()
    {
        var model = new BindingModel { Name = "Bound" };
        var target = new ControlText();
        using var binding = target.Bind(model, source => source.Name);
        target.Content = "manual";

        model.Raise(nameof(BindingModel.Number));

        target.Content.ShouldBe("manual");
    }

    /// <summary>Verifies a two-way nested binding writes a reachable leaf.</summary>
    [Fact]
    public void Bind_WhenTargetChanges_WritesNestedLeaf()
    {
        var address = new BindingAddress { City = "Porto" };
        var model = new BindingModel { Address = address };
        var target = new TextInput();
        using var binding = target.Bind(model, source => source.Address!.City);

        target.Text = "Aveiro";

        address.City.ShouldBe("Aveiro");
    }

    /// <summary>Verifies a two-way binding never constructs a missing intermediate, and skips the
    /// reverse write instead of throwing from the target's own property setter: the target has
    /// already committed its value and raised its change event by the time the reverse write would
    /// run, so throwing there would erupt out of ordinary user input.</summary>
    [Fact]
    public void Bind_WhenNestedTargetChangesThroughNull_SkipsWriteWithoutThrowing()
    {
        var model = new BindingModel();
        var target = new TextInput();
        using var binding = target.Bind(model, source => source.Address!.City);

        _ = Should.NotThrow(() => target.Text = "Lost");

        model.Address.ShouldBeNull();
        target.Text.ShouldBe("Lost");
    }

    /// <summary>Verifies the next forward update reconciles the target once the previously null
    /// intermediate becomes reachable, overwriting the transient value a skipped reverse write
    /// could not commit to the model.</summary>
    [Fact]
    public void Bind_WhenIntermediateBecomesReachableAfterSkippedWrite_ReconcilesTarget()
    {
        var model = new BindingModel();
        var target = new TextInput();
        using var binding = target.Bind(model, source => source.Address!.City);
        target.Text = "Lost";

        model.Address = new BindingAddress { City = "Braga" };

        target.Text.ShouldBe("Braga");
    }

    /// <summary>Verifies a lambda without one root parameter is rejected.</summary>
    [Fact]
    public void Create_WhenLambdaHasNoRoot_Throws()
    {
        var expression = Expression.Lambda(Expression.Constant("value"));

        _ = Should.Throw<ArgumentException>(() => PropertyPath.Create(expression, requireWritable: false));
    }

    /// <summary>Verifies boundary conversions unwrap to the same cached property path.</summary>
    [Fact]
    public void Create_WhenBoundaryConverts_ReadsProperty()
    {
        Expression<Func<BindingModel, object?>> expression = source => source.Name;
        var model = new BindingModel { Name = "Converted" };
        var path = PropertyPath.Create(expression, requireWritable: false);

        path.TryRead(model, out var value).ShouldBeTrue();

        value.ShouldBe("Converted");
    }

    /// <summary>Verifies checked numeric conversion is removed from property-path identity.</summary>
    [Fact]
    public void Create_WhenBoundaryUsesCheckedConversion_ReadsUnderlyingProperty()
    {
        var parameter = Expression.Parameter(typeof(BindingModel), "source");
        var property = Expression.Property(parameter, nameof(BindingModel.Number));
        var converted = Expression.ConvertChecked(property, typeof(long));
        var expression = Expression.Lambda<Func<BindingModel, long>>(converted, parameter);
        var model = new BindingModel { Number = 42 };
        var path = PropertyPath.Create(expression, requireWritable: false);

        path.TryRead(model, out var value).ShouldBeTrue();

        value.ShouldBe(42);
    }

    /// <summary>Verifies a value-type intermediate is rejected at bind time: reading it through a
    /// boxing getter would make any leaf write on it discard the mutation silently.</summary>
    [Fact]
    public void Create_WhenIntermediateIsValueType_Throws()
    {
        var model = new BindingModel { Address = new BindingAddress() };
        var target = new Slider { Maximum = 50 };

        _ = Should.Throw<ArgumentException>(() => target.Bind(model, source => source.Address!.Location.X));
    }

    /// <summary>Verifies a value-type leaf still writes the real owner, proving the intermediate
    /// check does not over-reject a struct in the last segment of the path.</summary>
    [Fact]
    public void Create_WhenLeafIsValueType_WritesRealOwner()
    {
        Expression<Func<BindingModel, BindingLocation>> expression = source => source.Address!.Location;
        var address = new BindingAddress { Location = new BindingLocation { X = 1 } };
        var model = new BindingModel { Address = address };
        var path = PropertyPath.Create(expression, requireWritable: true);

        path.Write(model, new BindingLocation { X = 9 }).ShouldBeTrue();

        address.Location.X.ShouldBe(9);
    }
}
