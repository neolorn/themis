using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Themis.Endpoints;

namespace Observatory;

/// <summary>
/// The person a request acts for, read from the header <c>X-Person</c>, which stands in for the
/// signed-in person a real host would read from its authentication; none where it is absent.
/// </summary>
internal sealed class HeaderPerson : ICurrentPerson
{
    internal const string Header = "X-Person";

    public ValueTask<string?> ReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string person = context.Request.Headers[Header].ToString();
        return ValueTask.FromResult<string?>(person.Length > 0 ? person : null);
    }
}