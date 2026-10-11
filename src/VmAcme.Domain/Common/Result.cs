namespace VmAcme.Common;

public interface IValidationResult<TSelf> where TSelf : IValidationResult<TSelf>
{
    static abstract TSelf Failure(Error error);
}

public readonly record struct Unit;

public interface IResultOutcome
{
    bool IsFailure { get; }
}

public sealed class Result<T> : IValidationResult<Result<T>>, IResultOutcome
{
    private readonly T? _value;

    private Result(T? value, Error? error)
    {
        _value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failure result has no value.");
    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Failure(Error error) =>
        new(default, error ?? throw new ArgumentNullException(nameof(error)));
}