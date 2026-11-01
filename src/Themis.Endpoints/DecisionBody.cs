using System.Globalization;
using System.Text;

using Themis.Core;

namespace Themis.Endpoints;

/// <summary>
/// The body of <c>POST requests/{id}/decisions</c>: a <see cref="DecisionRequest"/>'s members
/// but the request and the decider, which the route and the person give. Formatting it writes
/// neither its person nor its reason.
/// </summary>
internal sealed record DecisionBody
{
    public required DecisionOutcome Outcome { get; init; }

    public string? Reason { get; init; }

    public string? ActingFor { get; init; }

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Outcome = {Outcome}");
        return true;
    }
}