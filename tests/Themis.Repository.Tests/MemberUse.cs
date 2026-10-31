namespace Themis.Repository.Tests;

/// <summary>
/// A member a method body of a built assembly uses, as its type's full name and its own name,
/// with the method that uses it and whether that method is of generated code.
/// </summary>
internal sealed record MemberUse(string Type, string Member, string Method, bool Generated);