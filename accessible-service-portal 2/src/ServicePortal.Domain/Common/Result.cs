namespace ServicePortal.Domain.Common;

/// <summary>Outcome of an operation that returns no value: success, or an <see cref="Common.Error"/>.</summary>
public class Result
{
    protected Result(Error? error) => Error = error;

    public Error? Error { get; }

    public bool IsSuccess => Error is null;

    public bool IsFailure => !IsSuccess;

    public static Result Success() => new(null);

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>Outcome of an operation that returns a <typeparamref name="T"/> on success.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null) => _value = value;

    private Result(Error error)
        : base(error)
    {
    }

    /// <summary>The value. Throws if the result is a failure — check <see cref="Result.IsSuccess"/> first.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot read the value of a failed result ({Error!.Code}).");

#pragma warning disable CA1000 // Factory members on the generic type are the clearest API here.
    public static Result<T> Success(T value) => new(value);

    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
#pragma warning restore CA1000

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>Transforms a successful value; failures pass through unchanged.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Result<TOut>.Success(map(Value)) : Result<TOut>.Failure(Error!);
    }
}
