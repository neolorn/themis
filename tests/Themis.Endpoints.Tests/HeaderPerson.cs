using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace Themis.Endpoints.Tests;

/// <summary>The test's <see cref="ICurrentPerson"/>: the header <c>X-Person</c>, none where it is absent, or a throw where the test asks.</summary>
internal sealed class HeaderPerson : ICurrentPerson
{
    internal const string Header = "X-Person";

    private int throws;

    /// <summary>Makes each later read throw, a fault of the host's code.</summary>
    internal void Throw()
    {
        Volatile.Write(ref throws, 1);
    }

    public ValueTask<string?> ReadAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (Volatile.Read(ref throws) == 1)
        {
            throw new InvalidOperationException("The host's person seam failed.");
        }

        string person = context.Request.Headers[Header].ToString();
        return ValueTask.FromResult<string?>(person.Length > 0 ? person : null);
    }
}