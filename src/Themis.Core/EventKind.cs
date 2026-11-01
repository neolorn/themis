using System.Text.Json.Serialization;

namespace Themis.Core;

/// <summary>What an event tells (<c>06</c> section 6); each member is written as its spelling.</summary>
public enum EventKind
{
    /// <summary>A request is created.</summary>
    [JsonStringEnumMemberName("requested")]
    Requested,

    /// <summary>A step after the first starts.</summary>
    [JsonStringEnumMemberName("stepstarted")]
    StepStarted,

    /// <summary>A deadline adds escalation assignments.</summary>
    [JsonStringEnumMemberName("escalated")]
    Escalated,

    /// <summary>The request is approved.</summary>
    [JsonStringEnumMemberName("approved")]
    Approved,

    /// <summary>The request is rejected.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,

    /// <summary>The request is cancelled.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    /// <summary>The request is voided.</summary>
    [JsonStringEnumMemberName("voided")]
    Voided,

    /// <summary>The request is completed.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>A delegation is created.</summary>
    [JsonStringEnumMemberName("delegationcreated")]
    DelegationCreated,

    /// <summary>A reminder before a step's deadline falls due.</summary>
    [JsonStringEnumMemberName("reminded")]
    Reminded,
}