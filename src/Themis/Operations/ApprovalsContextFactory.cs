using Microsoft.EntityFrameworkCore.Design;

namespace Themis.Operations;

/// <summary>Makes the context for EF Core's tool when it generates a migration; it never connects.</summary>
internal sealed class ApprovalsContextFactory : IDesignTimeDbContextFactory<ApprovalsContext>
{
    public ApprovalsContext CreateDbContext(string[] args)
    {
        return ApprovalsContext.Create("Host=localhost");
    }
}