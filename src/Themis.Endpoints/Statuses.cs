using System;
using System.Collections.Frozen;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;

namespace Themis.Endpoints;

/// <summary>
/// The status each code answers over HTTP, as the column "Over HTTP" of <c>06</c> section 1
/// gives it (<c>03</c> APR-LIB-004): 404 for a code naming something not found, 403 for one
/// refusing the person, 400 for one refusing the input, 409 for one refusing the state, 503 for
/// the directory's unavailability and 500 for any other.
/// </summary>
internal static class Statuses
{
    private static readonly FrozenDictionary<string, int> ByCode = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [Codes.TypeInvalid] = StatusCodes.Status400BadRequest,
        [Codes.TypeUnknown] = StatusCodes.Status404NotFound,
        [Codes.TypeRetired] = StatusCodes.Status409Conflict,
        [Codes.SnapshotInvalid] = StatusCodes.Status400BadRequest,
        [Codes.RequestOpen] = StatusCodes.Status409Conflict,
        [Codes.PreviousInvalid] = StatusCodes.Status400BadRequest,
        [Codes.RequestUnknown] = StatusCodes.Status404NotFound,
        [Codes.RequestClosed] = StatusCodes.Status409Conflict,
        [Codes.NotApproved] = StatusCodes.Status409Conflict,
        [Codes.CursorInvalid] = StatusCodes.Status400BadRequest,
        [Codes.NotAssigned] = StatusCodes.Status403Forbidden,
        [Codes.Separation] = StatusCodes.Status403Forbidden,
        [Codes.Duplicate] = StatusCodes.Status409Conflict,
        [Codes.NotPermitted] = StatusCodes.Status403Forbidden,
        [Codes.ReasonRequired] = StatusCodes.Status400BadRequest,
        [Codes.ReasonInvalid] = StatusCodes.Status400BadRequest,
        [Codes.FieldLocked] = StatusCodes.Status409Conflict,
        [Codes.DelegationInvalid] = StatusCodes.Status400BadRequest,
        [Codes.DelegationUnknown] = StatusCodes.Status404NotFound,
        [Codes.DelegationEnded] = StatusCodes.Status409Conflict,
        [Codes.DirectoryUnavailable] = StatusCodes.Status503ServiceUnavailable,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>The status <paramref name="code"/> answers.</summary>
    internal static int For(string code)
    {
        return ByCode.GetValueOrDefault(code, StatusCodes.Status500InternalServerError);
    }

    /// <summary>Whether an answer of <paramref name="status"/> carries the code and its details; 500 and 503 carry none.</summary>
    internal static bool CarriesCode(int status)
    {
        return status < StatusCodes.Status500InternalServerError;
    }
}