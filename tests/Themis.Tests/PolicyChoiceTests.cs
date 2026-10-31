using System;
using System.Collections.Generic;

using Themis.Core;
using Themis.Types;

using Xunit;

namespace Themis.Tests;

/// <summary>A request takes the first policy whose start condition holds and which has a step whose condition holds (<c>02</c> APR-TYPE-003).</summary>
[Trait("Category", "unit")]
public sealed class PolicyChoiceTests
{
    private static readonly Dictionary<string, FieldValue> Snapshot = new(StringComparer.Ordinal)
    {
        ["amount"] = new FieldValue.DecimalNumber(5000m),
        ["count"] = new FieldValue.WholeNumber(3),
        ["region"] = new FieldValue.PlainText("north"),
        ["urgent"] = new FieldValue.Flag(false),
    };

    [Fact]
    public void Choose_TwoPoliciesWhoseStartConditionsHold_AnswersTheFirst()
    {
        ApprovalTypeDefinition definition = Definitions.Valid.WithPolicy(1, policy => policy with { StartCondition = new Condition.Compare("amount", CompareOperator.Greater, new FieldValue.WholeNumber(1)) });

        PolicyDefinition? chosen = PolicyChoice.Choose(definition, WithRegion(Definitions.Region), [], subjectRoles: null);

        Assert.Equal("large", chosen?.Name);
    }

    [Fact]
    public void Choose_PolicyWhoseEveryStepsConditionIsFalse_IsPassedOver()
    {
        PolicyDefinition? chosen = PolicyChoice.Choose(Definitions.Valid, WithRegion("south"), [], subjectRoles: null);

        Assert.Equal("small", chosen?.Name);
    }

    [Fact]
    public void Choose_PolicyWhoseStartConditionIsFalse_IsPassedOver()
    {
        PolicyDefinition? chosen = PolicyChoice.Choose(Definitions.Valid, WithRegion(Definitions.Region), [Definitions.BuyerRole], subjectRoles: null);

        Assert.Equal("small", chosen?.Name);
    }

    [Fact]
    public void Choose_StepWithoutACondition_Holds()
    {
        ApprovalTypeDefinition definition = Definitions.Valid.WithStep(1, 0, step => step with { Condition = null });

        PolicyDefinition? chosen = PolicyChoice.Choose(definition, WithCount(50), [Definitions.BuyerRole], subjectRoles: null);

        Assert.Equal("small", chosen?.Name);
    }

    [Fact]
    public void Choose_NoPolicyHolds_AnswersNone()
    {
        Assert.Null(PolicyChoice.Choose(Definitions.Valid, WithCount(50), [Definitions.BuyerRole], subjectRoles: null));
    }

    [Fact]
    public void FallbackStep_Made_IsOneAnyOneStepWhoseSoleApproverAndFallbackIsTheTypesFallback()
    {
        StepDefinition step = PolicyChoice.FallbackStep(Definitions.Valid);

        Assert.Equal("fallback", step.Name);
        Assert.Equal(new ApproverReference.Person(Definitions.TypeFallback), Assert.Single(step.Approvers));
        Assert.Equal(ApprovalMode.AnyOne, step.Mode);
        Assert.Null(step.Condition);
        Assert.Equal(Definitions.TypeFallback, step.Fallback);
        Assert.Null(step.Deadline);
    }

    private static Dictionary<string, FieldValue> WithRegion(string region)
    {
        return new Dictionary<string, FieldValue>(Snapshot, StringComparer.Ordinal) { ["region"] = new FieldValue.PlainText(region) };
    }

    private static Dictionary<string, FieldValue> WithCount(long count)
    {
        return new Dictionary<string, FieldValue>(WithRegion("south"), StringComparer.Ordinal) { ["count"] = new FieldValue.WholeNumber(count) };
    }
}