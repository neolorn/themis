using System;
using System.Collections.Generic;

using Themis.Core;

namespace Themis.Endpoints.Tests;

/// <summary>The persons, the types and the requests of the endpoints' tests, each value distinctive, so a log line holding one is found.</summary>
internal static class Approvals
{
    internal const string Key = "purchase.request";

    internal const string OtherKey = "travel.request";

    internal const string LargeKey = "purchase.large";

    internal const string Finance = "orders.finance";

    internal const string Requester = "person-requester-2d7a";

    internal const string Subject = "person-subject-5f1c";

    internal const string A = "person-a-8e3b";

    internal const string B = "person-b-0c6d";

    internal const string Deputy = "person-deputy-4a9e";

    internal const string Watcher = "person-watcher-1b5f";

    internal const string Stranger = "person-stranger-7d2c";

    internal const string Fallback = "person-fallback-6e0a";

    internal const string TargetId = "order-3c8f";

    internal const string Reason = "reason-text-9a4d";

    internal static string[] Unloggable => [Requester, Subject, A, B, Deputy, Watcher, Stranger, Fallback, TargetId, Reason];

    /// <summary>The type <paramref name="key"/>, protecting <c>amount</c>, of one step <c>review</c> that <see cref="A"/> and <see cref="B"/> both approve, watched by <see cref="Watcher"/>.</summary>
    internal static ApprovalTypeDefinition Type(string key = Key)
    {
        return Typed(key, new StepDefinition { Name = "review", Approvers = [new ApproverReference.Person(A), new ApproverReference.Person(B)], Mode = ApprovalMode.All, Fallback = Fallback });
    }

    /// <summary>The type <see cref="LargeKey"/>: <see cref="A"/> approves, then any holder of <see cref="Finance"/>.</summary>
    internal static ApprovalTypeDefinition LargeType()
    {
        return Typed(
            LargeKey,
            new StepDefinition { Name = "review", Approvers = [new ApproverReference.Person(A)], Mode = ApprovalMode.AnyOne, Fallback = Fallback },
            new StepDefinition { Name = "finance", Approvers = [new ApproverReference.Permission(Finance)], Mode = ApprovalMode.AnyOne, Fallback = Fallback });
    }

    /// <summary>A request of <paramref name="key"/> by <see cref="Requester"/> about <see cref="Subject"/>.</summary>
    internal static ApprovalRequest Request(string key = Key, string target = TargetId)
    {
        return new ApprovalRequest
        {
            Type = key,
            Target = new Target { Kind = "order", Id = target },
            Snapshot = new Dictionary<string, FieldValue> { ["amount"] = new FieldValue.DecimalNumber(1250.5m) },
            Requester = Requester,
            Subject = Subject,
        };
    }

    /// <summary>A delegation from <paramref name="from"/> to <paramref name="to"/> from <paramref name="starts"/> for ten days, for <paramref name="types"/>.</summary>
    internal static DelegationDefinition Delegation(string from, string to, DateTimeOffset starts, params string[] types)
    {
        return new DelegationDefinition { From = from, To = to, Starts = starts, Ends = starts + TimeSpan.FromDays(10), Types = types, CreatedBy = from };
    }

    private static ApprovalTypeDefinition Typed(string key, params StepDefinition[] steps)
    {
        return new ApprovalTypeDefinition
        {
            Key = key,
            ProtectedFields = [new ProtectedField { Name = "amount", Kind = FieldKind.DecimalNumber }],
            Policies = [new PolicyDefinition { Name = "main", Steps = steps }],
            Duplicates = DuplicateRule.Skip,
            Watchers = [new ApproverReference.Person(Watcher)],
            Fallback = Fallback,
        };
    }
}