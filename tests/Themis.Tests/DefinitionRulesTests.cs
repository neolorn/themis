using System.Collections.Generic;
using System.Linq;

using Themis.Core;
using Themis.Types;

using Xunit;

namespace Themis.Tests;

/// <summary>The rules a definition is checked by at publish, each answering the path of the first member outside it (<c>02</c> APR-TYPE-002, APR-TYPE-004, APR-TYPE-005).</summary>
[Trait("Category", "unit")]
public sealed class DefinitionRulesTests
{
    public static TheoryData<string> OutsideARule => [.. InvalidDefinitions.TypeRules.Keys.Concat(InvalidDefinitions.StepRules.Keys).Concat(InvalidDefinitions.RequiredRules.Keys).Concat(InvalidDefinitions.ReminderRules.Keys).Concat(InvalidDefinitions.ConditionRules.Keys)];

    public static TheoryData<string> AtTheEdge => [.. InvalidDefinitions.AtTheEdge.Keys];

    [Fact]
    public void Invalid_DefinitionWithinEveryRule_AnswersNone()
    {
        Assert.Null(DefinitionRules.Invalid(Definitions.Valid));
    }

    [Theory]
    [MemberData(nameof(OutsideARule))]
    public void Invalid_DefinitionOutsideARule_AnswersThePathOfTheFirstMemberOutsideIt(string rule)
    {
        (ApprovalTypeDefinition definition, string member) = Find(rule);

        Assert.Equal(member, DefinitionRules.Invalid(definition));
    }

    [Theory]
    [MemberData(nameof(AtTheEdge))]
    public void Invalid_DefinitionAtTheEdgeOfARule_AnswersNone(string rule)
    {
        Assert.Null(DefinitionRules.Invalid(InvalidDefinitions.AtTheEdge[rule]));
    }

    [Fact]
    public void Invalid_TwoMembersOutsideTheirRules_AnswersTheFirstInTheCanonicalOrder()
    {
        ApprovalTypeDefinition definition = Definitions.Valid.WithStep(0, 1, step => step with { Mode = (ApprovalMode)5, Fallback = string.Empty }) with { Fallback = string.Empty };

        Assert.Equal("policies[0].steps[1].mode", DefinitionRules.Invalid(definition));
    }

    [Fact]
    public void Invalid_CanonicalFormOf64KiB_AnswersNoneAndTheFormAndOneByteMoreAnswersTheEmptyPath()
    {
        ApprovalTypeDefinition largest = Definitions.OfSize(65_536);
        ApprovalTypeDefinition larger = Definitions.OfSize(65_537);

        Assert.Equal((65_536, 65_537), (CanonicalForm.Write(largest).Length, CanonicalForm.Write(larger).Length));
        Assert.Null(DefinitionRules.Invalid(largest, out byte[] form));
        Assert.Equal(CanonicalForm.Write(largest), form);
        Assert.Equal(string.Empty, DefinitionRules.Invalid(larger, out byte[] none));
        Assert.Empty(none);
    }

    [Fact]
    public void Invalid_CanonicalFormOver64KiBAndAMemberOutsideItsRule_AnswersTheMember()
    {
        ApprovalTypeDefinition definition = Definitions.OfSize(70_000) with { Fallback = "person-" + (char)7 };

        Assert.True(CanonicalForm.Write(definition).Length > 65_536);
        Assert.Equal("fallback", DefinitionRules.Invalid(definition));
    }

    private static (ApprovalTypeDefinition Definition, string Member) Find(string rule)
    {
        foreach (IReadOnlyDictionary<string, (ApprovalTypeDefinition, string)> rules in new[] { InvalidDefinitions.TypeRules, InvalidDefinitions.StepRules, InvalidDefinitions.RequiredRules, InvalidDefinitions.ReminderRules, InvalidDefinitions.ConditionRules })
        {
            if (rules.TryGetValue(rule, out (ApprovalTypeDefinition, string) found))
            {
                return found;
            }
        }

        throw new KeyNotFoundException(rule);
    }
}