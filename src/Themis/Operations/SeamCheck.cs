using Microsoft.Extensions.DependencyInjection;

using Themis.Core;

namespace Themis.Operations;

/// <summary>The seams a host supplies that the start requires (<c>03</c> APR-LIB-005).</summary>
internal static class SeamCheck
{
    /// <summary>
    /// Answers the <c>details.key</c> of the first required seam the host has not registered, in
    /// the order of APR-LIB-005's table, or none: the secret source and the directory always,
    /// the observer where <c>Themis:Observer:Enabled</c> is on and the application runs the jobs.
    /// </summary>
    internal static string? Missing(IServiceProviderIsService services, ApprovalsOptions options)
    {
        if (!services.IsService(typeof(ISecretSource)))
        {
            return "secretSource";
        }

        if (!services.IsService(typeof(IApproverDirectory)))
        {
            return "approverDirectory";
        }

        if (options.Observer.Enabled && options.Jobs.Enabled && !services.IsService(typeof(IApprovalObserver)))
        {
            return "approvalObserver";
        }

        return null;
    }
}