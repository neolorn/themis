using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Themis.Core;

/// <summary>
/// Creates, decides, cancels and reads requests (<c>01</c>). An overload that takes a
/// <see cref="DbTransaction"/> writes on the host's open <c>NpgsqlTransaction</c> alone and
/// commits nothing (<c>03</c> APR-LIB-008); the others run in a transaction of the library's
/// own. Each call made before the host's start has run the library's startup checks throws
/// <see cref="InvalidOperationException"/>, and an argument outside its rule throws
/// <see cref="ArgumentException"/>, a fault of the calling code.
/// </summary>
public interface IApprovals
{
    /// <summary>
    /// Records <paramref name="request"/> under the latest version of its type, chooses its
    /// policy and starts its first step (<c>01</c> APR-REQ-001), in a transaction of its own.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The request's identifier and status; or the failure that refused it, nothing recorded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="ArgumentException">A person reference, a role or the target is outside its rule, or subject roles are given without a subject.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run, or the directory answers a person reference outside its rule.</exception>
    ValueTask<Result<CreatedRequest>> CreateAsync(ApprovalRequest request, CancellationToken cancellationToken);

    /// <summary>Records <paramref name="request"/> as <see cref="CreateAsync(ApprovalRequest, CancellationToken)"/> does, on the host's <paramref name="transaction"/>.</summary>
    /// <param name="request">The request.</param>
    /// <param name="transaction">The host's open <c>NpgsqlTransaction</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The request's identifier and status; or the failure that refused it, nothing of the call's left on the transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="transaction"/> is null.</exception>
    /// <exception cref="ArgumentException">An argument is outside its rule, or <paramref name="transaction"/> is of another provider or has ended.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run, or the directory answers a person reference outside its rule.</exception>
    ValueTask<Result<CreatedRequest>> CreateAsync(ApprovalRequest request, DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>Records a person's decision on a pending request and moves it (<c>01</c> APR-REQ-004).</summary>
    /// <param name="decision">The decision.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The decision's identifier and the request's status; or the failure of the first check it fails, nothing changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    /// <exception cref="ArgumentException">A person reference is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<DecisionReceipt>> DecideAsync(DecisionRequest decision, CancellationToken cancellationToken);

    /// <summary>Cancels an open request (<c>01</c> APR-REQ-010), in a transaction of its own.</summary>
    /// <param name="cancel">The cancellation.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A success; or the failure that refused it, nothing changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cancel"/> is null.</exception>
    /// <exception cref="ArgumentException">A person reference is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result> CancelAsync(CancelRequest cancel, CancellationToken cancellationToken);

    /// <summary>Cancels an open request as <see cref="CancelAsync(CancelRequest, CancellationToken)"/> does, on the host's <paramref name="transaction"/>.</summary>
    /// <param name="cancel">The cancellation.</param>
    /// <param name="transaction">The host's open <c>NpgsqlTransaction</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A success; or the failure that refused it, nothing of the call's left on the transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cancel"/> or <paramref name="transaction"/> is null.</exception>
    /// <exception cref="ArgumentException">An argument is outside its rule, or <paramref name="transaction"/> is of another provider or has ended.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result> CancelAsync(CancelRequest cancel, DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>Marks an approved request completed once the host has acted on it, after which no change voids it (<c>01</c> APR-REQ-012), in a transaction of its own.</summary>
    /// <param name="request">The request.</param>
    /// <param name="actor">The person the host says completed it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A success; or <c>approvals.request.notapproved</c> where it is pending, <c>approvals.request.closed</c> where it is closed, <c>approvals.request.unknown</c>, nothing changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="actor"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="actor"/> is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result> CompleteAsync(RequestId request, string actor, CancellationToken cancellationToken);

    /// <summary>Completes an approved request as <see cref="CompleteAsync(RequestId, string, CancellationToken)"/> does, on the host's <paramref name="transaction"/>, so the host's action and the completion commit together.</summary>
    /// <param name="request">The request.</param>
    /// <param name="actor">The person the host says completed it.</param>
    /// <param name="transaction">The host's open <c>NpgsqlTransaction</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A success; or the failure that refused it, nothing of the call's left on the transaction.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="actor"/> or <paramref name="transaction"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="actor"/> is outside its rule, or <paramref name="transaction"/> is of another provider or has ended.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result> CompleteAsync(RequestId request, string actor, DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>Reports whether <paramref name="field"/> of <paramref name="target"/> may change: <c>Locked</c> where a pending request covers it, <c>Approved</c> where an approved one does, <c>Free</c> otherwise (<c>01</c> APR-REQ-011).</summary>
    /// <param name="target">The target.</param>
    /// <param name="field">The field's name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The field's protection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> or <paramref name="field"/> is null.</exception>
    /// <exception cref="ArgumentException">The target or the field's name is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<FieldProtection> ProtectionAsync(Target target, string field, CancellationToken cancellationToken);

    /// <summary>Records a change the host made to <paramref name="field"/> of <paramref name="target"/>, voiding every approved request covering it (<c>01</c> APR-REQ-011), in a transaction of its own.</summary>
    /// <param name="target">The target.</param>
    /// <param name="field">The field's name.</param>
    /// <param name="actor">The person the host says changed it.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The identifiers of the requests voided, none where no approved request covers it; or <c>approvals.field.locked</c> where a pending one does, nothing changed.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The target, the field's name or <paramref name="actor"/> is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<IReadOnlyList<RequestId>>> RecordChangeAsync(Target target, string field, string actor, CancellationToken cancellationToken);

    /// <summary>Records a change as <see cref="RecordChangeAsync(Target, string, string, CancellationToken)"/> does, on the host's <paramref name="transaction"/>, so the host's change and the voiding commit together.</summary>
    /// <param name="target">The target.</param>
    /// <param name="field">The field's name.</param>
    /// <param name="actor">The person the host says changed it.</param>
    /// <param name="transaction">The host's open <c>NpgsqlTransaction</c>.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The identifiers of the requests voided; or the failure that refused it, nothing of the call's left on the transaction.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">An argument is outside its rule, or <paramref name="transaction"/> is of another provider or has ended.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<IReadOnlyList<RequestId>>> RecordChangeAsync(Target target, string field, string actor, DbTransaction transaction, CancellationToken cancellationToken);

    /// <summary>Reads a request's whole record (<c>01</c> APR-REQ-013).</summary>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The record; or the failure <c>approvals.request.unknown</c>.</returns>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<RequestRecord>> ReadAsync(RequestId request, CancellationToken cancellationToken);

    /// <summary>Reads the requests on <paramref name="target"/>, newest first, at most 100 (<c>01</c> APR-REQ-013).</summary>
    /// <param name="target">The target.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The requests' summaries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is null.</exception>
    /// <exception cref="ArgumentException">The target is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<IReadOnlyList<RequestSummary>> ByTargetAsync(Target target, CancellationToken cancellationToken);

    /// <summary>Reads what the record holds that names <paramref name="person"/> (<c>01</c> APR-REQ-015): the requests they made or are the subject of, their assignments, the decisions they made or that were made for them, and the delegations from and to them.</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The export.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="person"/> is outside its rule.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<PersonExport> ExportAsync(string person, CancellationToken cancellationToken);

    /// <summary>Reads a page of the requests <paramref name="person"/> may decide now, oldest request first (<c>01</c> APR-REQ-013).</summary>
    /// <param name="person">The person reference.</param>
    /// <param name="cursor">The cursor a page answered as its next, or none for the first page.</param>
    /// <param name="size">The page's size, 1 to 100, or none for 20.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The page; or the failure <c>approvals.request.cursorinvalid</c> where the cursor does not read.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="person"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="person"/> is outside its rule, or <paramref name="size"/> is outside 1 to 100.</exception>
    /// <exception cref="InvalidOperationException">The startup checks have not run.</exception>
    ValueTask<Result<PendingPage>> PendingAsync(string person, string? cursor, int? size, CancellationToken cancellationToken);
}