using Themis.Types;

using Xunit;

namespace Themis.Tests;

/// <summary>The type's lock (<c>04</c> APR-OPS-003; <c>06</c> section 7).</summary>
[Trait("Category", "unit")]
public sealed class TypeLockTests
{
    [Fact]
    public void Text_IsTheReferencesTypeLockText()
    {
        Assert.Equal("approvals-type", TypeLock.Text);
    }

    [Theory]
    [InlineData("purchase.order")]
    [InlineData("p")]
    [InlineData("expense_claim.travel")]
    public void Key_OfAKey_IsTheFirst8BytesOfTheHashReadAsASignedBigEndianInteger(string key)
    {
        Assert.Equal(TypeHost.TypeLockKey(key), TypeLock.Key(key));
    }

    [Fact]
    public void Key_OfTwoKeys_Differs()
    {
        Assert.NotEqual(TypeLock.Key("purchase.order"), TypeLock.Key("purchase.other"));
    }
}