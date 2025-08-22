namespace EvAluator.Shared.Types;

public readonly struct Result<TSuccess, TError> : IEquatable<Result<TSuccess, TError>>
{
    private readonly bool _isSuccess;
    private readonly TSuccess _success;
    private readonly TError _error;

    private Result(TSuccess success)
    {
        _success = success;
        _error = default!;
        _isSuccess = true;
    }

    private Result(TError error)
    {
        _success = default!;
        _error = error;
        _isSuccess = false;
    }

    public bool IsSuccess => _isSuccess;
    public bool IsFailure => !_isSuccess;

    public static Result<TSuccess, TError> Success(TSuccess value) => new(value);
    public static Result<TSuccess, TError> Failure(TError error) => new(error);

    public TResult Match<TResult>(Func<TSuccess, TResult> success, Func<TError, TResult> failure) =>
        _isSuccess ? success(_success) : failure(_error);

    public void Match(Action<TSuccess> success, Action<TError> failure)
    {
        if (_isSuccess)
            success(_success);
        else
            failure(_error);
    }

    public Result<TResult, TError> Map<TResult>(Func<TSuccess, TResult> mapper) =>
        _isSuccess ? Result<TResult, TError>.Success(mapper(_success)) : Result<TResult, TError>.Failure(_error);

    public Result<TSuccess, TResult> MapError<TResult>(Func<TError, TResult> mapper) =>
        _isSuccess ? Result<TSuccess, TResult>.Success(_success) : Result<TSuccess, TResult>.Failure(mapper(_error));

    public Result<TResult, TError> Bind<TResult>(Func<TSuccess, Result<TResult, TError>> binder) =>
        _isSuccess ? binder(_success) : Result<TResult, TError>.Failure(_error);

    public static implicit operator Result<TSuccess, TError>(TSuccess success) => Success(success);
    public static implicit operator Result<TSuccess, TError>(TError error) => Failure(error);

    public bool Equals(Result<TSuccess, TError> other) =>
        _isSuccess == other._isSuccess &&
        (_isSuccess 
            ? EqualityComparer<TSuccess>.Default.Equals(_success, other._success)
            : EqualityComparer<TError>.Default.Equals(_error, other._error));

    public override bool Equals(object? obj) => 
        obj is Result<TSuccess, TError> other && Equals(other);

    public override int GetHashCode() =>
        _isSuccess 
            ? HashCode.Combine(_isSuccess, _success)
            : HashCode.Combine(_isSuccess, _error);

    public static bool operator ==(Result<TSuccess, TError> left, Result<TSuccess, TError> right) => left.Equals(right);
    public static bool operator !=(Result<TSuccess, TError> left, Result<TSuccess, TError> right) => !left.Equals(right);

    public override string ToString() =>
        _isSuccess ? $"Success({_success})" : $"Failure({_error})";
}

public readonly struct Result<TSuccess> : IEquatable<Result<TSuccess>>
{
    private readonly Result<TSuccess, string> _result;

    private Result(Result<TSuccess, string> result)
    {
        _result = result;
    }

    public bool IsSuccess => _result.IsSuccess;
    public bool IsFailure => _result.IsFailure;

    public static Result<TSuccess> Success(TSuccess value) => new(Result<TSuccess, string>.Success(value));
    public static Result<TSuccess> Failure(string error) => new(Result<TSuccess, string>.Failure(error));

    public TResult Match<TResult>(Func<TSuccess, TResult> success, Func<string, TResult> failure) =>
        _result.Match(success, failure);

    public void Match(Action<TSuccess> success, Action<string> failure) =>
        _result.Match(success, failure);

    public Result<TResult> Map<TResult>(Func<TSuccess, TResult> mapper) =>
        new(Result<TResult, string>.Success(mapper(_result.Match(s => s, _ => default!))));

    public Result<TResult> Bind<TResult>(Func<TSuccess, Result<TResult>> binder) =>
        _result.Match(
            success => binder(success),
            error => Result<TResult>.Failure(error));

    public static implicit operator Result<TSuccess>(TSuccess success) => Success(success);
    public static implicit operator Result<TSuccess>(string error) => Failure(error);

    public bool Equals(Result<TSuccess> other) => _result.Equals(other._result);

    public override bool Equals(object? obj) => 
        obj is Result<TSuccess> other && Equals(other);

    public override int GetHashCode() => _result.GetHashCode();

    public static bool operator ==(Result<TSuccess> left, Result<TSuccess> right) => left.Equals(right);
    public static bool operator !=(Result<TSuccess> left, Result<TSuccess> right) => !left.Equals(right);

    public override string ToString() => _result.ToString();
}

public static class Result
{
    public static Result<TSuccess, TError> Success<TSuccess, TError>(TSuccess value) => 
        Result<TSuccess, TError>.Success(value);
    
    public static Result<TSuccess, TError> Failure<TSuccess, TError>(TError error) => 
        Result<TSuccess, TError>.Failure(error);

    public static Result<TSuccess> Success<TSuccess>(TSuccess value) => 
        Result<TSuccess>.Success(value);
    
    public static Result<TSuccess> Failure<TSuccess>(string error) => 
        Result<TSuccess>.Failure(error);
}