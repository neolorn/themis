using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace Themis.Repository.Tests;

/// <summary>
/// The public types the tables of <c>03</c> APR-LIB-002 and APR-LIB-007 give, by package, each
/// in its package's namespace (<c>05</c> APR-ENG-001), a case nested in the type it is a case
/// of, as the public API files write it.
/// </summary>
internal static class PublicTypeTable
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> Types = new Dictionary<string, string[]>
    {
        [Packages.Core] =
        [
            // Values and identifiers.
            "RequestId", "DecisionId", "DelegationId", "ApprovalEventId",
            "Target",
            "FieldValue", "FieldValue.PlainText", "FieldValue.WholeNumber", "FieldValue.DecimalNumber", "FieldValue.Flag",
            "Result", "Result`1", "Failure",
            "ApprovalsStartupException",
            "FieldKind", "CompareOperator", "ApprovalMode", "DeadlineAction", "DuplicateRule", "DecisionOutcome", "RequestStatus",
            "StepStatus", "AssignmentOrigin", "ReasonCode", "FieldProtection", "EventKind", "ApprovalJob",

            // Approval types.
            "ApprovalTypeDefinition", "ProtectedField", "PolicyDefinition", "StepDefinition", "Deadline",
            "ApproverReference", "ApproverReference.Permission", "ApproverReference.Person",
            "Condition", "Condition.AllOf", "Condition.AnyOf", "Condition.Negate", "Condition.Compare", "Condition.OneOf",
            "Condition.RequesterHas", "Condition.SubjectHas",
            "PublishedVersion", "ApprovalTypeVersion",

            // Requests.
            "ApprovalRequest", "CreatedRequest", "DecisionRequest", "DecisionReceipt", "CancelRequest",
            "RequestRecord", "StepRecord", "AssignmentRecord", "DecisionRecord",
            "PendingPage", "PendingItem", "RequestSummary",
            "PersonExport", "AssignmentExport", "DecisionExport",

            // Delegations and events.
            "DelegationDefinition", "DelegationRecord", "ApprovalEvent",

            // Operations.
            "IApprovalTypes", "IApprovals", "IDelegations", "IApprovalJobs",

            // Seams a host supplies.
            "IApproverDirectory", "ApproverQuery", "ApproverCheck",
            "IApprovalObserver",
            "ISecretSource", "SecretVersions", "SecretVersion",
            "IAlertSink",
        ],
        [Packages.Engine] = ["ApprovalsServiceCollectionExtensions"],
        [Packages.Testing] =
        [
            "InMemoryApproverDirectory", "RecordingObserver", "FixedSecretSource", "TestJobs",
            "ApprovalsTestDatabase", "ApprovalsTestingServiceCollectionExtensions",
        ],
        [Packages.Cli] = [],
    }.ToFrozenDictionary(
        entry => entry.Key,
        entry => entry.Value.Select(type => entry.Key + "." + type).ToFrozenSet(StringComparer.Ordinal),
        StringComparer.Ordinal);

    /// <summary>Answers each public type of <paramref name="package"/> whose name the tables do not give.</summary>
    internal static IEnumerable<string> Violations(string package, IEnumerable<PublicType> types)
    {
        FrozenSet<string> given = Types.GetValueOrDefault(package, FrozenSet<string>.Empty);
        return types.Select(type => type.Name)
            .Where(name => !given.Contains(name))
            .Order(StringComparer.Ordinal)
            .Select(name => $"{package} declares the public type {name}, which no table of 03 APR-LIB-002 or APR-LIB-007 gives.");
    }
}