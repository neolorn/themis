namespace Themis.Core;

/// <summary>
/// An operation's outcome where it answers a value: the value on a success, or the failure
/// that refused it (<c>05</c> APR-ENG-003). Made by <see cref="Result.Success{T}(T)"/> and
/// <see cref="Result.Fail{T}(Failure)"/>.
/// </summary>
/// <typeparam name="T">The value's type.</typeparam>
public sealed record Result<T>
{
    internal Result(T value)
    {
        Value = value;
    }

    internal Result(Failure failure)
    {
        Failure = failure;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => Failure is null;

    /// <summary>The value on a success; its type's default on a refusal.</summary>
    public T? Value { get; }

    /// <summary>The failure that refused the operation, or none on a success.</summary>
    public Failure? Failure { get; }
}