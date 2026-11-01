using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

using Themis.Core;

namespace Themis.Endpoints;

/// <summary>
/// The body of <c>POST delegations</c>: a <see cref="DelegationDefinition"/>'s members but
/// <c>From</c> and <c>CreatedBy</c>, both of which the person gives. Formatting it writes no
/// person.
/// </summary>
internal sealed record DelegationBody
{
    public required string To { get; init; }

    public required DateTimeOffset Starts { get; init; }

    public required DateTimeOffset Ends { get; init; }

    public IReadOnlyList<string> Types { get; init; } = [];

    private bool PrintMembers(StringBuilder builder)
    {
        builder.Append(CultureInfo.InvariantCulture, $"Starts = {Starts:O}, Ends = {Ends:O}, Types = {Types?.Count}");
        return true;
    }
}