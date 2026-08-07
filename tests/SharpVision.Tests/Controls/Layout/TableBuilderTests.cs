// Copyright (c) SharpVision contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace SharpVision.Tests.Controls.Layout;

/// <summary>Verifies TableBuilder fluent construction, validation, and materialized Table state.</summary>
public sealed class TableBuilderTests
{
    /// <summary>Verifies a builder with columns and matching rows produces a valid Table.</summary>
    [Fact]
    public void Build_WhenColumnsAndRowsMatch_ProducesPopulatedTable()
    {
        var table = new TableBuilder()
            .Column("Name")
            .Column("Age")
            .Row("Alice", 30)
            .Row("Bob", 25)
            .Build();

        table.Columns.Count.ShouldBe(2);
        table.Columns[0].Header.ShouldBe("Name");
        table.Columns[1].Header.ShouldBe("Age");
        table.Rows.Count.ShouldBe(2);
        table.Rows[0].Cells[0].ShouldBeOfType<ControlText>().Content.ShouldBe("Alice");
        table.Rows[0].Cells[1].ShouldBeOfType<ControlText>().Content.ShouldBe("30");
        table.Rows[1].Cells[0].ShouldBeOfType<ControlText>().Content.ShouldBe("Bob");
        table.Rows[1].Cells[1].ShouldBeOfType<ControlText>().Content.ShouldBe("25");
    }

    /// <summary>Verifies an explicit column width is preserved on the built Table.</summary>
    [Fact]
    public void Build_WhenColumnHasExplicitWidth_PreservesWidth()
    {
        var table = new TableBuilder()
            .Column("Fixed", Length.Cells(10))
            .Column("Auto")
            .Row("A", "B")
            .Build();

        table.Columns[0].Width.ShouldBe(Length.Cells(10));
        table.Columns[1].Width.ShouldBe(Length.Auto);
    }

    /// <summary>Verifies a row with mismatched cell count is rejected.</summary>
    [Fact]
    public void Row_WhenCellCountMismatches_Throws()
    {
        var builder = new TableBuilder()
            .Column("A")
            .Column("B");

        var exception = Should.Throw<ArgumentException>(() => builder.Row("only one"));
        exception.ParamName.ShouldBe("cells");
    }

    /// <summary>Verifies null column header is rejected.</summary>
    [Fact]
    public void Column_WhenHeaderIsNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => new TableBuilder().Column(null!));

    /// <summary>Verifies null cells array is rejected.</summary>
    [Fact]
    public void Row_WhenCellsIsNull_Throws() =>
        _ = Should.Throw<ArgumentNullException>(() => new TableBuilder().Column("A").Row(null!));

    /// <summary>Verifies a builder with no rows builds a columns-only Table.</summary>
    [Fact]
    public void Build_WhenNoRows_ProducesColumnsOnlyTable()
    {
        var table = new TableBuilder()
            .Column("Header")
            .Build();

        table.Columns.Count.ShouldBe(1);
        table.Rows.Count.ShouldBe(0);
    }

    /// <summary>Verifies null cell values are converted to empty strings.</summary>
    [Fact]
    public void Build_WhenCellValueIsNull_ConvertsToEmptyString()
    {
        var table = new TableBuilder()
            .Column("Col")
            .Row([null])
            .Build();

        table.Rows[0].Cells[0].ShouldBeOfType<ControlText>().Content.ShouldBe(string.Empty);
    }
}
