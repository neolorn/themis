using System;

namespace Themis.Core;

/// <summary>
/// An operation's outcome where it answers no value: a success, or the failure that refused it
/// (<c>05</c> APR-ENG-003). A fault of the calling code is thrown, never answered.
/// </summary>
public sealed record Result
{
    private static readonly Result Succeeded = new(failure: null);

    private Result(Failure? failure)
    {
        Failure = failure;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess => Failure is null;

    /// <summary>The failure that refused the operation, or none on a success.</summary>
    public Failure? Failure { get; }

    /// <summary>A success.</summary>
    /// <returns>The success.</returns>
    public static Result Success()
    {
        return Succeeded;
    }

    /// <summary>A refusal with <paramref name="failure"/>.</summary>
    /// <param name="failure">The failure.</param>
    /// <returns>The refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static Result Fail(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new Result(failure);
    }

    /// <summary>A success answering <paramref name="value"/>.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The success.</returns>
    public static Result<T> Success<T>(T value)
    {
        return new Result<T>(value);
    }

    /// <summary>A refusal with <paramref name="failure"/>, answering no value.</summary>
    /// <typeparam name="T">The type of the value a success would answer.</typeparam>
    /// <param name="failure">The failure.</param>
    /// <returns>The refusal.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="failure"/> is null.</exception>
    public static Result<T> Fail<T>(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new Result<T>(failure);
    }
}