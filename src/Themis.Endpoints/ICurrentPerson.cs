using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace Themis.Endpoints;

/// <summary>
/// The seam through which a host names the person an HTTP request acts for, where the
/// endpoints of <c>Themis.Endpoints</c> are mapped (<c>03</c> APR-LIB-004, APR-LIB-005). It is
/// bounded by the HTTP request's own cancellation alone. A throw, or a person reference outside
/// its rule, is a fault of the host's code, which the call answers with 500.
/// </summary>
public interface ICurrentPerson
{
    /// <summary>Reads the person the HTTP request acts for.</summary>
    /// <param name="context">The HTTP request's context.</param>
    /// <param name="cancellationToken">The HTTP request's cancellation.</param>
    /// <returns>The person reference, or none where the request acts for no one, which the call answers with 401.</returns>
    ValueTask<string?> ReadAsync(HttpContext context, CancellationToken cancellationToken);
}