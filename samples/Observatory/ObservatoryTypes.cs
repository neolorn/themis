using System;
using System.Collections.Generic;

using Themis.Core;

namespace Observatory;

/// <summary>The observatory's two approval types, which it publishes at its start.</summary>
internal static class ObservatoryTypes
{
    internal const string TelescopeTime = "telescope.time";

    internal const string InstrumentLoan = "instrument.loan";

    /// <summary>The value of a loan above which the custodian approves it after the desk.</summary>
    internal const decimal DeskLimit = 10_000m;

    private const string Director = "director";

    private const string Custodian = "custodian";

    /// <summary>
    /// Time on the telescope for a proposal: a scheduler approves it, then the director. A
    /// schedule left undecided for two days escalates to the deputy director.
    /// </summary>
    internal static ApprovalTypeDefinition TelescopeTimeType { get; } = new()
    {
        Key = TelescopeTime,
        ProtectedFields = [new ProtectedField { Name = "hours", Kind = FieldKind.DecimalNumber }],
        Policies =
        [
            new PolicyDefinition
            {
                Name = "allocation",
                Steps =
                [
                    new StepDefinition
                    {
                        Name = "schedule",
                        Approvers = [new ApproverReference.Permission(PermissionTable.Schedule)],
                        Mode = ApprovalMode.AnyOne,
                        Fallback = Director,
                        Deadline = new Deadline { After = TimeSpan.FromDays(2), Action = DeadlineAction.Escalate, EscalateTo = new ApproverReference.Permission(PermissionTable.Deputise) },
                    },
                    new StepDefinition
                    {
                        Name = "director",
                        Approvers = [new ApproverReference.Permission(PermissionTable.Direct)],
                        Mode = ApprovalMode.AnyOne,
                        Fallback = Director,
                    },
                ],
            },
        ],
        Duplicates = DuplicateRule.Skip,
        Fallback = Director,
    };

    /// <summary>
    /// The loan of an instrument: the desk approves it, and the custodian too where its value is
    /// above <see cref="DeskLimit"/>.
    /// </summary>
    internal static ApprovalTypeDefinition InstrumentLoanType { get; } = new()
    {
        Key = InstrumentLoan,
        ProtectedFields = [new ProtectedField { Name = "value", Kind = FieldKind.DecimalNumber }],
        Policies =
        [
            new PolicyDefinition
            {
                Name = "loan",
                Steps =
                [
                    new StepDefinition
                    {
                        Name = "desk",
                        Approvers = [new ApproverReference.Permission(PermissionTable.Lend)],
                        Mode = ApprovalMode.AnyOne,
                        Fallback = Custodian,
                    },
                    new StepDefinition
                    {
                        Name = "custody",
                        Approvers = [new ApproverReference.Permission(PermissionTable.Keep)],
                        Mode = ApprovalMode.AnyOne,
                        Condition = new Condition.Compare("value", CompareOperator.Greater, new FieldValue.DecimalNumber(DeskLimit)),
                        Fallback = Custodian,
                    },
                ],
            },
        ],
        Duplicates = DuplicateRule.Skip,
        Fallback = Custodian,
    };

    internal static IReadOnlyList<ApprovalTypeDefinition> All { get; } = [TelescopeTimeType, InstrumentLoanType];
}