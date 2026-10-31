using System.Collections.Generic;
using System.Linq;

using Themis.Core;

namespace Themis.Tests;

/// <summary>
/// The persons, roles, values and types the request tests use, each distinctive so a test can
/// find any of them in a log line, and the ways of making a type and a request of it.
/// </summary>
internal static class Requests
{
    internal const string Key = "purchase.request";

    internal const string Requester = "person-requester-4e1a";

    internal const string Subject = "person-subject-8b2c";

    internal const string A = "person-a-1f3d";

    internal const string B = "person-b-6c2e";

    internal const string C = "person-c-9d4f";

    internal const string Late = "person-late-0d6a";

    internal const string StepFallback = "person-stepfallback-5e8c";

    internal const string TypeFallback = "person-typefallback-3b9d";

    internal const string Watcher = "person-watcher-7c1e";

    internal const string Buyer = "role-buyer-4f2b";

    internal const string TargetId = "order-7e3c";

    internal const string Region = "value-north-5a1d";

    internal const string Reason = "reason-text-6b0e";

    internal const string Approve = "orders.approve";

    internal const string Finance = "orders.finance";

    internal static readonly Target Target = new() { Kind = "order", Id = TargetId };

    /// <summary>The person references, roles, values, reasons and target the tests use, none of which a log line may hold (<c>04</c> APR-OPS-005).</summary>
    internal static string[] Unloggable => [Requester, Subject, A, B, C, Late, StepFallback, TypeFallback, Watcher, Buyer, TargetId, Region, Reason];

    /// <summary>A step named <paramref name="name"/> of <paramref name="mode"/>, its approvers <paramref name="approvers"/>, its fallback <see cref="StepFallback"/>.</summary>
    internal static StepDefinition Step(string name, ApprovalMode mode, params ApproverReference[] approvers)
    {
        return new StepDefinition { Name = name, Approvers = approvers, Mode = mode, Fallback = StepFallback };
    }

    internal static ApproverReference Person(string person)
    {
        return new ApproverReference.Person(person);
    }

    internal static ApproverReference Permission(string permission)
    {
        return new ApproverReference.Permission(permission);
    }

    /// <summary>
    /// The type <see cref="Key"/>, protecting <c>amount</c> (a decimal) and <c>region</c> (a text),
    /// of one policy <c>main</c> of <paramref name="steps"/>, watched by <see cref="Watcher"/>, its
    /// fallback <see cref="TypeFallback"/>.
    /// </summary>
    internal static ApprovalTypeDefinition Type(params StepDefinition[] steps)
    {
        return Typed([new PolicyDefinition { Name = "main", Steps = steps }]);
    }

    /// <summary>The type <see cref="Key"/> as <see cref="Type"/> makes it, of <paramref name="policies"/>.</summary>
    internal static ApprovalTypeDefinition Typed(IReadOnlyList<PolicyDefinition> policies, DuplicateRule duplicates = DuplicateRule.Skip)
    {
        return new ApprovalTypeDefinition
        {
            Key = Key,
            ProtectedFields = [new ProtectedField { Name = "amount", Kind = FieldKind.DecimalNumber }, new ProtectedField { Name = "region", Kind = FieldKind.Text }],
            Policies = policies,
            Duplicates = duplicates,
            Watchers = [Person(Watcher)],
            Fallback = TypeFallback,
        };
    }

    /// <summary>A request of <see cref="Key"/> on <see cref="Target"/> by <see cref="Requester"/>, of <paramref name="amount"/> in <see cref="Region"/>.</summary>
    internal static ApprovalRequest Request(decimal amount = 500m, string? subject = null)
    {
        return new ApprovalRequest
        {
            Type = Key,
            Target = Target,
            Snapshot = new Dictionary<string, FieldValue> { ["amount"] = new FieldValue.DecimalNumber(amount), ["region"] = new FieldValue.PlainText(Region) },
            Requester = Requester,
            Subject = subject,
            RequesterRoles = [Buyer],
        };
    }

    /// <summary>The persons of a step's assignments in a record, in the record's order.</summary>
    internal static string[] Assigned(RequestRecord record, int step)
    {
        return [.. record.Steps.Single(found => found.Number == step).Assignments.Select(assignment => assignment.Person)];
    }
}