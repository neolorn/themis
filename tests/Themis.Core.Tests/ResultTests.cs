using System;
using System.Collections.Generic;

using Xunit;

namespace Themis.Core.Tests;

/// <summary>A refusal a person, a host's data or a moment can cause is a failure the operation answers (<c>05</c> APR-ENG-003).</summary>
[Trait("Category", "unit")]
public sealed class ResultTests
{
    private static readonly Failure Unknown = new() { Code = "approvals.type.unknown" };

    [Fact]
    public void Success_Made_IsASuccessWithNoFailure()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void Fail_Made_IsNoSuccessAndCarriesItsFailure()
    {
        Result result = Result.Fail(Unknown);

        Assert.False(result.IsSuccess);
        Assert.Same(Unknown, result.Failure);
    }

    [Fact]
    public void SuccessOfAValue_Made_CarriesTheValue()
    {
        Result<int> result = Result.Success(7);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
        Assert.Null(result.Failure);
    }

    [Fact]
    public void FailOfAValue_Made_CarriesTheFailureAndNoValue()
    {
        Result<string> result = Result.Fail<string>(Unknown);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Same(Unknown, result.Failure);
    }

    [Fact]
    public void Fail_NoFailure_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Fail(null!));
        Assert.Throws<ArgumentNullException>(() => Result.Fail<int>(null!));
    }

    [Fact]
    public void Failure_MadeWithoutDetails_HasNone()
    {
        Assert.Empty(Unknown.Details);
    }

    [Fact]
    public void Failure_MadeWithDetails_CarriesThem()
    {
        Failure failure = new() { Code = "approvals.type.invalid", Details = new Dictionary<string, string> { ["member"] = "key" } };

        Assert.Equal(new Dictionary<string, string> { ["member"] = "key" }, failure.Details);
    }
}