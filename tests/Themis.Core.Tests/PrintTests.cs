using System;
using System.Collections.Generic;

using Xunit;

namespace Themis.Core.Tests;

/// <summary>
/// A record carrying a person reference, a target, a value or a secret prints none of them
/// (<c>03</c> APR-LIB-002), so a log line that formats one writes none (<c>04</c> APR-OPS-005).
/// </summary>
[Trait("Category", "unit")]
public sealed class PrintTests
{
    private const string Person = "person-7f3a";

    private const string Other = "person-91c2";

    private const string TargetId = "target-5d10";

    private const string Secret = "Host=db;Password=secret-41ab";

    private const string Role = "role-c41d";

    private const string Unprintable = $"{Person}|{Other}|{Role}|value-e2b4|4711";

    private static readonly Target KnownTarget = new() { Kind = "order", Id = TargetId };

    private static readonly ApprovalTypeDefinition Definition = new()
    {
        Key = "purchase",
        ProtectedFields = [new ProtectedField { Name = "note", Kind = FieldKind.Text }, new ProtectedField { Name = "amount", Kind = FieldKind.DecimalNumber }],
        Policies =
        [
            new PolicyDefinition
            {
                Name = "large",
                StartCondition = new Condition.AllOf([new Condition.RequesterHas(Role), new Condition.Compare("note", CompareOperator.Equal, new FieldValue.PlainText("value-e2b4"))]),
                Steps =
                [
                    new StepDefinition
                    {
                        Name = "review",
                        Approvers = [new ApproverReference.Person(Person), new ApproverReference.Permission("orders.approve")],
                        Mode = ApprovalMode.All,
                        Condition = new Condition.OneOf("amount", [new FieldValue.DecimalNumber(4711.25m)]),
                        Fallback = Other,
                        Deadline = new Deadline { After = TimeSpan.FromHours(1), Action = DeadlineAction.Escalate, EscalateTo = new ApproverReference.Person(Person) },
                    },
                ],
            },
        ],
        Duplicates = DuplicateRule.RequireOther,
        Watchers = [new ApproverReference.Person(Other)],
        Fallback = Person,
    };

    private static readonly ApproverQuery Query = new()
    {
        Request = new RequestId(Guid.CreateVersion7()),
        Type = "purchase",
        Version = 3,
        Target = KnownTarget,
        Requester = Person,
        Subject = Other,
        Snapshot = new Dictionary<string, FieldValue>(StringComparer.Ordinal)
        {
            ["note"] = new FieldValue.PlainText("value-e2b4"),
            ["amount"] = new FieldValue.DecimalNumber(4711.25m),
        },
        Step = 2,
    };

    [Fact]
    public void Target_ToString_WritesNeitherItsKindNorItsIdentifier()
    {
        string printed = KnownTarget.ToString();

        Assert.DoesNotContain(TargetId, printed, StringComparison.Ordinal);
        Assert.DoesNotContain("order", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void FieldValue_ToString_WritesNoValue()
    {
        FieldValue[] values = [new FieldValue.PlainText("value-e2b4"), new FieldValue.WholeNumber(98765), new FieldValue.DecimalNumber(4711.25m), new FieldValue.Flag(true)];

        Assert.All(values, value => Assert.DoesNotMatch("value-e2b4|98765|4711|True", value.ToString()));
    }

    [Fact]
    public void ApproverReference_PersonsToString_WritesNoReference()
    {
        Assert.DoesNotContain(Person, new ApproverReference.Person(Person).ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ApproverQuery_ToString_WritesItsRequestTypeVersionAndStepAlone()
    {
        string printed = Query.ToString();

        Assert.DoesNotMatch($"{Person}|{Other}|{TargetId}|value-e2b4|4711", printed);
        Assert.Contains(Query.Request.Value.ToString(), printed, StringComparison.Ordinal);
        Assert.Contains("purchase", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void ApproverCheck_ToString_WritesNoPersonTargetOrValue()
    {
        ApproverCheck check = new() { Person = Person, Via = new ApproverReference.Person(Other), ActingFor = Other, Query = Query };

        Assert.DoesNotMatch($"{Person}|{Other}|{TargetId}|value-e2b4|4711", check.ToString());
    }

    [Fact]
    public void ApprovalEvent_ToString_WritesNoPersonOrTarget()
    {
        ApprovalEvent approvalEvent = new()
        {
            Event = new ApprovalEventId(Guid.CreateVersion7()),
            Kind = EventKind.Approved,
            At = DateTimeOffset.UnixEpoch,
            Sequence = 4,
            Request = Query.Request,
            Type = "purchase",
            Version = 3,
            Target = KnownTarget,
            Step = 2,
            Actor = Person,
            ActingFor = Other,
            Notify = [Person, Other],
        };

        string printed = approvalEvent.ToString();

        Assert.DoesNotMatch($"{Person}|{Other}|{TargetId}", printed);
        Assert.Contains("Approved", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovalTypeDefinition_ToString_WritesItsKeyAndNoPersonRoleOrValue()
    {
        string printed = Definition.ToString();

        Assert.DoesNotMatch(Unprintable, printed);
        Assert.Contains("purchase", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void PolicyDefinition_ToString_WritesNoPersonRoleOrValue()
    {
        Assert.DoesNotMatch(Unprintable, Definition.Policies[0].ToString());
    }

    [Fact]
    public void StepDefinition_ToString_WritesItsNameAndNoPersonRoleOrValue()
    {
        string printed = Definition.Policies[0].Steps[0].ToString();

        Assert.DoesNotMatch(Unprintable, printed);
        Assert.Contains("review", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void Condition_ToString_WritesNoRoleOrValue()
    {
        Condition[] conditions =
        [
            new Condition.RequesterHas(Role),
            new Condition.SubjectHas(Role),
            new Condition.Compare("note", CompareOperator.Equal, new FieldValue.PlainText("value-e2b4")),
            new Condition.OneOf("amount", [new FieldValue.DecimalNumber(4711.25m)]),
            new Condition.Negate(new Condition.RequesterHas(Role)),
            new Condition.AllOf([new Condition.SubjectHas(Role)]),
            new Condition.AnyOf([new Condition.SubjectHas(Role)]),
        ];

        Assert.All(conditions, condition => Assert.DoesNotMatch(Unprintable, condition.ToString()));
    }

    [Fact]
    public void ApprovalTypeVersion_ToString_WritesNoPersonRoleOrValue()
    {
        ApprovalTypeVersion version = new() { Key = "purchase", Version = 3, Definition = Definition, PublishedAt = DateTimeOffset.UnixEpoch, Retired = false };

        string printed = version.ToString();

        Assert.DoesNotMatch(Unprintable, printed);
        Assert.Contains("Version = 3", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void SecretVersion_ToString_WritesItsNumberAndNeverItsValue()
    {
        SecretVersion version = new() { Version = 2, Value = System.Text.Encoding.UTF8.GetBytes(Secret) };

        string printed = version.ToString();

        Assert.DoesNotContain("secret-41ab", printed, StringComparison.Ordinal);
        Assert.Contains("Version = 2", printed, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovalsStartupException_Made_CarriesItsCodeAndDetails()
    {
        ApprovalsStartupException failure = new("approvals.startup.seammissing", new Dictionary<string, string>(StringComparer.Ordinal) { ["key"] = "secretSource" });

        Assert.Equal("approvals.startup.seammissing", failure.Code);
        Assert.Equal(new Dictionary<string, string> { ["key"] = "secretSource" }, failure.Details);
        Assert.Contains("approvals.startup.seammissing", failure.Message, StringComparison.Ordinal);
    }
}