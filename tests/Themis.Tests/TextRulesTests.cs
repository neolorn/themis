using System;
using System.Linq;

using Xunit;

namespace Themis.Tests;

/// <summary>Names and references as <c>00</c> section 1.5 gives them.</summary>
[Trait("Category", "unit")]
public sealed class TextRulesTests
{
    [Theory]
    [InlineData("order")]
    [InlineData("order.approve")]
    [InlineData("a1-b2_c3.d4")]
    [InlineData("step.2")]
    [InlineData("k")]
    public void IsName_Name_IsTrue(string name)
    {
        Assert.True(TextRules.IsName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Order")]
    [InlineData("2order")]
    [InlineData(".order")]
    [InlineData("order.")]
    [InlineData("order..approve")]
    [InlineData("order.-approve")]
    [InlineData("order approve")]
    [InlineData("order/approve")]
    [InlineData("ordér")]
    public void IsName_OutsideTheRule_IsFalse(string name)
    {
        Assert.False(TextRules.IsName(name));
    }

    [Fact]
    public void IsName_Length_IsAtMost64()
    {
        Assert.True(TextRules.IsName(new string('n', 64)));
        Assert.False(TextRules.IsName(new string('n', 65)));
        Assert.False(TextRules.IsName(null));
    }

    [Fact]
    public void IsReference_Length_Is1To128ScalarValues()
    {
        string grin = char.ConvertFromUtf32(0x1F600);

        Assert.True(TextRules.IsReference("p"));
        Assert.True(TextRules.IsReference(new string('p', 128)));
        Assert.True(TextRules.IsReference(string.Concat(Enumerable.Repeat(grin, 128))));
        Assert.True(TextRules.IsReference("Person Name, Département 7"));
        Assert.False(TextRules.IsReference(string.Empty));
        Assert.False(TextRules.IsReference(new string('p', 129)));
        Assert.False(TextRules.IsReference(string.Concat(Enumerable.Repeat(grin, 129))));
        Assert.False(TextRules.IsReference(null));
    }

    [Fact]
    public void IsReference_ControlCharacterOrLoneSurrogate_IsFalse()
    {
        Assert.False(TextRules.IsReference("person" + (char)0));
        Assert.False(TextRules.IsReference("person" + (char)10));
        Assert.False(TextRules.IsReference("person" + (char)0x7F));
        Assert.False(TextRules.IsReference("person" + (char)0x9F));
        Assert.False(TextRules.IsReference("person" + (char)0xD800));
        Assert.False(TextRules.IsReference((char)0xDC00 + "person"));
    }

    [Fact]
    public void IsText_LoneSurrogate_IsFalse()
    {
        Assert.True(TextRules.IsText(string.Empty));
        Assert.True(TextRules.IsText("north" + char.ConvertFromUtf32(0x1F600)));
        Assert.False(TextRules.IsText("north" + (char)0xD800));
        Assert.False(TextRules.IsText((char)0xDFFF + "north"));
        Assert.False(TextRules.IsText(string.Concat((char)0xDC00, (char)0xD800)));
    }

    [Fact]
    public void Length_Text_CountsScalarValues()
    {
        Assert.Equal(3, TextRules.Length("a" + char.ConvertFromUtf32(0x1F600) + "b"));
        Assert.Null(TextRules.Length("a" + (char)0xD800));
        Assert.Equal(0, TextRules.Length(string.Empty));
        Assert.Throws<ArgumentNullException>(() => TextRules.Length(null!));
    }
}