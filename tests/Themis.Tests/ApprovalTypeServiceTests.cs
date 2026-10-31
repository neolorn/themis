using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Themis.Core;

using Xunit;

namespace Themis.Tests;

/// <summary>The faults of the calling code <see cref="IApprovalTypes"/> throws, and its registration (<c>03</c> APR-LIB-003; <c>05</c> APR-ENG-003).</summary>
[Trait("Category", "unit")]
public sealed class ApprovalTypeServiceTests
{
    [Fact]
    public async Task AddThemis_ApprovalTypes_IsAScopedService()
    {
        await using TestHost host = TestHost.Build(before: TestHost.Seams(new SecretSourceFake()));
        AsyncServiceScope one = host.Services.CreateAsyncScope();
        AsyncServiceScope other = host.Services.CreateAsyncScope();
        await using (one)
        await using (other)
        {
            IApprovalTypes types = one.ServiceProvider.GetRequiredService<IApprovalTypes>();

            Assert.Same(types, one.ServiceProvider.GetRequiredService<IApprovalTypes>());
            Assert.NotSame(types, other.ServiceProvider.GetRequiredService<IApprovalTypes>());
        }
    }

    [Fact]
    public async Task ApprovalTypes_BeforeTheStartupChecks_ThrowsInvalidOperationException()
    {
        await using TestHost host = TestHost.Build(before: TestHost.Seams(new SecretSourceFake()));
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            IApprovalTypes types = scope.ServiceProvider.GetRequiredService<IApprovalTypes>();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await types.PublishAsync(Definitions.Valid, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await types.ReadAsync("purchase.order", null, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await types.RetireAsync("purchase.order", TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task ApprovalTypes_NoDefinitionOrNoOrEmptyKey_ThrowsArgumentException()
    {
        await using TestHost host = TestHost.Build(before: TestHost.Seams(new SecretSourceFake()));
        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            IApprovalTypes types = scope.ServiceProvider.GetRequiredService<IApprovalTypes>();

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await types.PublishAsync(null!, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await types.ReadAsync(null!, null, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(async () => await types.ReadAsync(string.Empty, 1, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentNullException>(async () => await types.RetireAsync(null!, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<ArgumentException>(async () => await types.RetireAsync(string.Empty, TestContext.Current.CancellationToken));
        }
    }
}