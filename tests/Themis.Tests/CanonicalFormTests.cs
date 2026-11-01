using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Themis.Core;
using Themis.Types;

using Xunit;

namespace Themis.Tests;

/// <summary>A definition's canonical form, one exact sequence of bytes for each definition (<c>05</c> APR-ENG-006).</summary>
[Trait("Category", "unit")]
public sealed class CanonicalFormTests
{
    /// <summary>The canonical form of <see cref="Fixed"/>, as <c>05</c> APR-ENG-006 gives it.</summary>
    private const string FixedForm =
        """{"key":"order.approve","protectedFields":[{"name":"amount","kind":"decimalnumber"},{"name":"count","kind":"wholenumber"},{"name":"region","kind":"text"},{"name":"urgent","kind":"flag"}]"""
        + ""","policies":[{"name":"large","startCondition":{"case":"allOf","conditions":[{"case":"compare","field":"amount","operator":"greaterorequal","value":{"case":"decimalNumber","value":1000.5}},{"case":"negate","operand":{"case":"requesterHas","role":"buyer"}}]}"""
        + ""","steps":[{"name":"manager","approvers":[{"case":"permission","name":"orders.approve"}],"mode":"anyone","fallback":"person-1","deadline":{"after":14400,"action":"escalate","escalateTo":{"case":"permission","name":"orders.escalate"}}}"""
        + """,{"name":"finance","approvers":[{"case":"person","reference":"person-2"},{"case":"person","reference":"person-3"}],"mode":"all","condition":{"case":"anyOf","conditions":[{"case":"oneOf","field":"region","values":[{"case":"plainText","value":"north"},{"case":"plainText","value":""" + "\"s\\u00FCd\"" + """}]},{"case":"compare","field":"urgent","operator":"notequal","value":{"case":"flag"}},{"case":"subjectHas","role":"auditor"}]}"""
        + ""","fallback":"person-4","deadline":{"after":172800,"action":"reject"}}]}"""
        + """,{"name":"small","steps":[{"name":"lead","approvers":[{"case":"permission","name":"orders.lead"}],"mode":"anyone","condition":{"case":"compare","field":"count","operator":"less","value":{"case":"wholeNumber","value":10}},"fallback":"person-1"}]}]"""
        + ""","duplicates":"requireother","watchers":[{"case":"person","reference":"person-5"}],"fallback":"person-6"}""";

    /// <summary>The SHA-256 of <see cref="FixedForm"/>, the hash its version was published with by the first release.</summary>
    private const string FixedHash = "27eb1ae9a570f7aac7a522020a086822f6816d92d41b7dc290c58ff6e5ec79ae";

    private static readonly ApprovalTypeDefinition Fixed = new()
    {
        Key = "order.approve",
        ProtectedFields =
        [
            new ProtectedField { Name = "amount", Kind = FieldKind.DecimalNumber },
            new ProtectedField { Name = "count", Kind = FieldKind.WholeNumber },
            new ProtectedField { Name = "region", Kind = FieldKind.Text },
            new ProtectedField { Name = "urgent", Kind = FieldKind.Flag },
        ],
        Policies =
        [
            new PolicyDefinition
            {
                Name = "large",
                StartCondition = new Condition.AllOf(
                [
                    new Condition.Compare("amount", CompareOperator.GreaterOrEqual, new FieldValue.DecimalNumber(1000.50m)),
                    new Condition.Negate(new Condition.RequesterHas("buyer")),
                ]),
                Steps =
                [
                    new StepDefinition
                    {
                        Name = "manager",
                        Approvers = [new ApproverReference.Permission("orders.approve")],
                        Mode = ApprovalMode.AnyOne,
                        Fallback = "person-1",
                        Deadline = new Deadline { After = TimeSpan.FromHours(4), Action = DeadlineAction.Escalate, EscalateTo = new ApproverReference.Permission("orders.escalate") },
                    },
                    new StepDefinition
                    {
                        Name = "finance",
                        Approvers = [new ApproverReference.Person("person-2"), new ApproverReference.Person("person-3")],
                        Mode = ApprovalMode.All,
                        Condition = new Condition.AnyOf(
                        [
                            new Condition.OneOf("region", [new FieldValue.PlainText("north"), new FieldValue.PlainText("s\u00FCd")]),
                            new Condition.Compare("urgent", CompareOperator.NotEqual, new FieldValue.Flag(false)),
                            new Condition.SubjectHas("auditor"),
                        ]),
                        Fallback = "person-4",
                        Deadline = new Deadline { After = TimeSpan.FromDays(2), Action = DeadlineAction.Reject },
                    },
                ],
            },
            new PolicyDefinition
            {
                Name = "small",
                Steps =
                [
                    new StepDefinition
                    {
                        Name = "lead",
                        Approvers = [new ApproverReference.Permission("orders.lead")],
                        Mode = ApprovalMode.AnyOne,
                        Condition = new Condition.Compare("count", CompareOperator.Less, new FieldValue.WholeNumber(10)),
                        Fallback = "person-1",
                    },
                ],
            },
        ],
        Duplicates = DuplicateRule.RequireOther,
        Watchers = [new ApproverReference.Person("person-5")],
        Fallback = "person-6",
    };

    [Fact]
    public void APR_ENG_006_AC1_DefinitionsEqualButForTheScaleOfADecimalHaveOneCanonicalForm()
    {
        ApprovalTypeDefinition scaled = WithAmount(1000.50m);
        ApprovalTypeDefinition unscaled = WithAmount(1000.5m);

        Assert.Equal(CanonicalForm.Write(unscaled), CanonicalForm.Write(scaled));
        Assert.Contains("\"value\":1000.5}", Encoding.UTF8.GetString(CanonicalForm.Write(scaled)), StringComparison.Ordinal);
    }

    [Fact]
    public void APR_ENG_006_AC2_FixedDefinitionIsWrittenAsItsLiteral()
    {
        Assert.Equal(FixedForm, Encoding.UTF8.GetString(CanonicalForm.Write(Fixed)));
    }

    [Fact]
    public void APR_TYPE_004_AC6_VersionPublishedBeforeAtLeastExistedKeepsItsHash()
    {
        byte[] published = Encoding.UTF8.GetBytes(FixedForm);

        byte[] rewritten = CanonicalForm.Write(CanonicalForm.Read(published));

        Assert.Equal(FixedForm, Encoding.UTF8.GetString(rewritten));
        Assert.Equal(FixedHash, Convert.ToHexStringLower(CanonicalForm.Hash(rewritten)));
        Assert.All(CanonicalForm.Read(published).Policies.SelectMany(policy => policy.Steps), step => Assert.Null(step.Required));
    }

    [Theory]
    [InlineData("0.00", "0")]
    [InlineData("-0.0", "0")]
    [InlineData("-12.3400", "-12.34")]
    [InlineData("0.0000001", "0.0000001")]
    [InlineData("100", "100")]
    [InlineData("79228162514264337593543950335", "79228162514264337593543950335")]
    public void Write_Decimal_IsWrittenWithoutTrailingZerosOrExponent(string value, string written)
    {
        ApprovalTypeDefinition definition = WithAmount(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture));

        Assert.Contains($"\"value\":{written}}}", Encoding.UTF8.GetString(CanonicalForm.Write(definition)), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_MemberThatIsNoneAnEmptyListOrFalse_IsLeftOut()
    {
        ApprovalTypeDefinition bare = new()
        {
            Key = "leave",
            Policies = [new PolicyDefinition { Name = "only", Steps = [new StepDefinition { Name = "one", Approvers = [new ApproverReference.Person("person-1")], Mode = ApprovalMode.AnyOne, Fallback = "person-1" }] }],
            Duplicates = DuplicateRule.Skip,
            Fallback = "person-1",
        };

        Assert.Equal(
            """{"key":"leave","policies":[{"name":"only","steps":[{"name":"one","approvers":[{"case":"person","reference":"person-1"}],"mode":"anyone","fallback":"person-1"}]}],"duplicates":"skip","fallback":"person-1"}""",
            Encoding.UTF8.GetString(CanonicalForm.Write(bare)));
        Assert.EndsWith(",\"allowAutoApprove\":true}", Encoding.UTF8.GetString(CanonicalForm.Write(bare with { AllowAutoApprove = true })), StringComparison.Ordinal);
    }

    [Fact]
    public void Read_CanonicalForm_GivesTheDefinitionWhoseFormItIs()
    {
        byte[] form = Encoding.UTF8.GetBytes(FixedForm);

        ApprovalTypeDefinition read = CanonicalForm.Read(form);

        Assert.Equal(form, CanonicalForm.Write(read));
        Assert.Empty(CanonicalForm.Read(Encoding.UTF8.GetBytes("""{"key":"leave","duplicates":"skip","fallback":"person-1"}""")).ProtectedFields);
        Assert.Empty(CanonicalForm.Read(Encoding.UTF8.GetBytes("""{"key":"leave","duplicates":"skip","fallback":"person-1"}""")).Watchers);
        Condition.AnyOf condition = Assert.IsType<Condition.AnyOf>(read.Policies[0].Steps[1].Condition);
        Condition.Compare urgent = Assert.IsType<Condition.Compare>(condition.Conditions[1]);
        Assert.Equal(new FieldValue.Flag(false), urgent.Value);
        Assert.Equal(TimeSpan.FromHours(4), read.Policies[0].Steps[0].Deadline!.After);
    }

    [Fact]
    public void Hash_CanonicalForm_IsItsSha256()
    {
        byte[] form = CanonicalForm.Write(Fixed);

        Assert.Equal(SHA256.HashData(form), CanonicalForm.Hash(form));
    }

    private static ApprovalTypeDefinition WithAmount(decimal amount)
    {
        PolicyDefinition large = Fixed.Policies[0] with
        {
            StartCondition = new Condition.Compare("amount", CompareOperator.Greater, new FieldValue.DecimalNumber(amount)),
        };
        return Fixed with { Policies = [large, Fixed.Policies[1]] };
    }
}