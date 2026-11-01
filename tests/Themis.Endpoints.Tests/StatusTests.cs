using Xunit;

namespace Themis.Endpoints.Tests;

/// <summary>The status each code of <c>06</c> section 1 answers over HTTP (<c>03</c> APR-LIB-004).</summary>
[Trait("Category", "unit")]
public sealed class StatusTests
{
    [Theory]
    [InlineData("approvals.type.invalid", 400)]
    [InlineData("approvals.type.unknown", 404)]
    [InlineData("approvals.type.retired", 409)]
    [InlineData("approvals.request.snapshotinvalid", 400)]
    [InlineData("approvals.request.open", 409)]
    [InlineData("approvals.request.previousinvalid", 400)]
    [InlineData("approvals.request.unknown", 404)]
    [InlineData("approvals.request.closed", 409)]
    [InlineData("approvals.request.notapproved", 409)]
    [InlineData("approvals.request.cursorinvalid", 400)]
    [InlineData("approvals.decision.notassigned", 403)]
    [InlineData("approvals.decision.separation", 403)]
    [InlineData("approvals.decision.duplicate", 409)]
    [InlineData("approvals.decision.notpermitted", 403)]
    [InlineData("approvals.decision.reasonrequired", 400)]
    [InlineData("approvals.reason.invalid", 400)]
    [InlineData("approvals.field.locked", 409)]
    [InlineData("approvals.delegation.invalid", 400)]
    [InlineData("approvals.delegation.unknown", 404)]
    [InlineData("approvals.delegation.ended", 409)]
    [InlineData("approvals.directory.unavailable", 503)]
    [InlineData("approvals.directory.toomany", 500)]
    [InlineData("approvals.erasure.pending", 500)]
    [InlineData("approvals.erasure.typeversion", 500)]
    [InlineData("approvals.retention.notgranted", 500)]
    [InlineData("approvals.startup.seammissing", 500)]
    [InlineData("approvals.unheard.of", 500)]
    public void APR_LIB_004_AC4_EachCodeAnswersTheStatusSection1GivesIt(string code, int status)
    {
        Assert.Equal(status, Statuses.For(code));
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(403, true)]
    [InlineData(404, true)]
    [InlineData(409, true)]
    [InlineData(500, false)]
    [InlineData(503, false)]
    public void APR_LIB_004_AC4_OnlyAStatusUnder500CarriesTheCodeAndDetails(int status, bool carries)
    {
        Assert.Equal(carries, Statuses.CarriesCode(status));
    }
}