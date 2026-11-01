using System;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Themis.Core;
using Themis.Requests;

using Xunit;

using static Themis.Tests.Requests;

namespace Themis.Tests;

/// <summary>The internal query <c>Themis.Endpoints</c> asks for its watcher check (<c>03</c> APR-LIB-004; <c>05</c> APR-ENG-001), against a real database in a container.</summary>
[Trait("Category", "integration")]
public sealed class RequestWatchersTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task NamesAsync_PersonAnEventNamesInNotify_AnswersTrueAndAnyOtherFalse()
    {
        await using RequestHost host = await RequestHost.StartAsync(postgres);
        await host.PublishAsync(Type(Step("review", ApprovalMode.AnyOne, Person(A))));
        RequestId request = await host.CreatedAsync(Request());

        AsyncServiceScope scope = host.Services.CreateAsyncScope();
        await using (scope)
        {
            RequestWatchers watchers = scope.ServiceProvider.GetRequiredService<RequestWatchers>();
            bool watcher = await watchers.NamesAsync(request, Watcher, TestContext.Current.CancellationToken);
            bool other = await watchers.NamesAsync(request, C, TestContext.Current.CancellationToken);
            bool unknown = await watchers.NamesAsync(new RequestId(Guid.CreateVersion7()), Watcher, TestContext.Current.CancellationToken);

            Assert.Equal((true, false, false), (watcher, other, unknown));
        }

        host.AssertNoLogHoldsAValueUsed();
    }
}