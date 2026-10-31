using System;
using System.Collections.Generic;

using Themis.Core;
using Themis.Types;

using Xunit;

namespace Themis.Tests;

/// <summary>A condition decides on a request's snapshot and roles alone (<c>02</c> APR-TYPE-005).</summary>
[Trait("Category", "unit")]
public sealed class ConditionCheckTests
{
    private static readonly string[] NoRoles = [];

    [Fact]
    public void APR_TYPE_005_AC1_CompareAmountGreaterThan1000HoldsFor1000Point01AndNotFor1000()
    {
        Condition whole = new Condition.Compare("amount", CompareOperator.Greater, new FieldValue.WholeNumber(1000));
        Condition dec = new Condition.Compare("amount", CompareOperator.Greater, new FieldValue.DecimalNumber(1000m));

        Assert.True(Holds(whole, Amount(1000.01m)));
        Assert.False(Holds(whole, Amount(1000m)));
        Assert.True(Holds(dec, Amount(1000.01m)));
        Assert.False(Holds(dec, Amount(1000.00m)));
    }

    [Fact]
    public void APR_TYPE_005_AC3_SubjectHasIsFalseOnARequestWithNoSubject()
    {
        Condition auditor = new Condition.SubjectHas("auditor");

        Assert.False(ConditionCheck.Holds(auditor, Amount(1m), ["auditor"], subjectRoles: null));
        Assert.True(ConditionCheck.Holds(auditor, Amount(1m), NoRoles, ["auditor"]));
        Assert.False(ConditionCheck.Holds(auditor, Amount(1m), NoRoles, NoRoles));
    }

    [Theory]
    [InlineData(CompareOperator.Equal, 5, 5, true)]
    [InlineData(CompareOperator.Equal, 5, 6, false)]
    [InlineData(CompareOperator.NotEqual, 5, 6, true)]
    [InlineData(CompareOperator.NotEqual, 5, 5, false)]
    [InlineData(CompareOperator.Greater, 6, 5, true)]
    [InlineData(CompareOperator.Greater, 5, 5, false)]
    [InlineData(CompareOperator.GreaterOrEqual, 5, 5, true)]
    [InlineData(CompareOperator.GreaterOrEqual, 4, 5, false)]
    [InlineData(CompareOperator.Less, 4, 5, true)]
    [InlineData(CompareOperator.Less, 5, 5, false)]
    [InlineData(CompareOperator.LessOrEqual, 5, 5, true)]
    [InlineData(CompareOperator.LessOrEqual, 6, 5, false)]
    public void Holds_WholeNumbersCompared_AnswersTheComparison(CompareOperator comparison, long field, long value, bool holds)
    {
        Condition condition = new Condition.Compare("count", comparison, new FieldValue.WholeNumber(value));

        Assert.Equal(holds, Holds(condition, Snapshot("count", new FieldValue.WholeNumber(field))));
    }

    [Fact]
    public void Holds_WholeNumberFieldComparedWithADecimal_ComparesThemAsDecimals()
    {
        Condition condition = new Condition.Compare("count", CompareOperator.Less, new FieldValue.DecimalNumber(2.5m));

        Assert.True(Holds(condition, Snapshot("count", new FieldValue.WholeNumber(2))));
        Assert.False(Holds(condition, Snapshot("count", new FieldValue.WholeNumber(3))));
        Assert.True(Holds(new Condition.Compare("count", CompareOperator.Equal, new FieldValue.DecimalNumber(3.00m)), Snapshot("count", new FieldValue.WholeNumber(3))));
    }

    [Fact]
    public void Holds_TextCompared_ComparesOrdinally()
    {
        Condition north = new Condition.Compare("region", CompareOperator.Equal, new FieldValue.PlainText("north"));

        Assert.True(Holds(north, Snapshot("region", new FieldValue.PlainText("north"))));
        Assert.False(Holds(north, Snapshot("region", new FieldValue.PlainText("North"))));
        Assert.True(Holds(new Condition.Compare("region", CompareOperator.NotEqual, new FieldValue.PlainText("north")), Snapshot("region", new FieldValue.PlainText("north "))));
    }

    [Fact]
    public void Holds_FlagCompared_ComparesByEquality()
    {
        Assert.True(Holds(new Condition.Compare("urgent", CompareOperator.Equal, new FieldValue.Flag(true)), Snapshot("urgent", new FieldValue.Flag(true))));
        Assert.False(Holds(new Condition.Compare("urgent", CompareOperator.Equal, new FieldValue.Flag(true)), Snapshot("urgent", new FieldValue.Flag(false))));
        Assert.True(Holds(new Condition.Compare("urgent", CompareOperator.NotEqual, new FieldValue.Flag(true)), Snapshot("urgent", new FieldValue.Flag(false))));
    }

    [Fact]
    public void Holds_OneOf_HoldsWhereTheValueIsListed()
    {
        Condition regions = new Condition.OneOf("region", [new FieldValue.PlainText("north"), new FieldValue.PlainText("south")]);
        Condition amounts = new Condition.OneOf("amount", [new FieldValue.DecimalNumber(10.5m)]);

        Assert.True(Holds(regions, Snapshot("region", new FieldValue.PlainText("south"))));
        Assert.False(Holds(regions, Snapshot("region", new FieldValue.PlainText("east"))));
        Assert.True(Holds(amounts, Amount(10.50m)));
    }

    [Fact]
    public void Holds_RequesterHas_HoldsWhereTheRequesterRolesHoldTheRole()
    {
        Condition buyer = new Condition.RequesterHas("buyer");

        Assert.True(ConditionCheck.Holds(buyer, Amount(1m), ["clerk", "buyer"], subjectRoles: null));
        Assert.False(ConditionCheck.Holds(buyer, Amount(1m), ["buyers"], ["buyer"]));
    }

    [Fact]
    public void Holds_AllOfAnyOfAndNegate_CombineTheirConditions()
    {
        Condition yes = new Condition.RequesterHas("buyer");
        Condition no = new Condition.RequesterHas("clerk");
        string[] roles = ["buyer"];

        Assert.True(ConditionCheck.Holds(new Condition.AllOf([yes, yes]), Amount(1m), roles, subjectRoles: null));
        Assert.False(ConditionCheck.Holds(new Condition.AllOf([yes, no]), Amount(1m), roles, subjectRoles: null));
        Assert.True(ConditionCheck.Holds(new Condition.AnyOf([no, yes]), Amount(1m), roles, subjectRoles: null));
        Assert.False(ConditionCheck.Holds(new Condition.AnyOf([no, no]), Amount(1m), roles, subjectRoles: null));
        Assert.True(ConditionCheck.Holds(new Condition.Negate(no), Amount(1m), roles, subjectRoles: null));
        Assert.False(ConditionCheck.Holds(new Condition.Negate(yes), Amount(1m), roles, subjectRoles: null));
    }

    [Fact]
    public void Holds_SnapshotWithoutTheField_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => Holds(new Condition.Compare("count", CompareOperator.Equal, new FieldValue.WholeNumber(1)), Amount(1m)));
    }

    private static bool Holds(Condition condition, IReadOnlyDictionary<string, FieldValue> snapshot)
    {
        return ConditionCheck.Holds(condition, snapshot, NoRoles, subjectRoles: null);
    }

    private static Dictionary<string, FieldValue> Amount(decimal amount)
    {
        return Snapshot("amount", new FieldValue.DecimalNumber(amount));
    }

    private static Dictionary<string, FieldValue> Snapshot(string field, FieldValue value)
    {
        return new Dictionary<string, FieldValue>(StringComparer.Ordinal) { [field] = value };
    }
}