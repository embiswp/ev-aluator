namespace EvAluator.Shared.Types;

public readonly struct Option<T> : IEquatable<Option<T>>
{
    private readonly bool _hasValue;
    private readonly T _value;

    private Option(T value)
    {
        _value = value;
        _hasValue = true;
    }

    public bool IsSome => _hasValue;
    public bool IsNone => !_hasValue;

    public static Option<T> Some(T value) => 
        value is null ? None() : new Option<T>(value);

    public static Option<T> None() => new();

    public TResult Match<TResult>(Func<T, TResult> some, Func<TResult> none) =>
        _hasValue ? some(_value) : none();

    public void Match(Action<T> some, Action none)
    {
        if (_hasValue)
            some(_value);
        else
            none();
    }

    public Option<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        _hasValue ? Option<TResult>.Some(mapper(_value)) : Option<TResult>.None();

    public Option<TResult> Bind<TResult>(Func<T, Option<TResult>> binder) =>
        _hasValue ? binder(_value) : Option<TResult>.None();

    public T GetValueOrDefault() => _hasValue ? _value : default!;

    public T GetValueOrDefault(T defaultValue) => _hasValue ? _value : defaultValue;

    public T GetValueOrDefault(Func<T> defaultValueFactory) => 
        _hasValue ? _value : defaultValueFactory();

    public static implicit operator Option<T>(T value) => Some(value);

    public bool Equals(Option<T> other) =>
        _hasValue == other._hasValue && 
        (!_hasValue || EqualityComparer<T>.Default.Equals(_value, other._value));

    public override bool Equals(object? obj) => 
        obj is Option<T> other && Equals(other);

    public override int GetHashCode() => 
        _hasValue ? EqualityComparer<T>.Default.GetHashCode(_value!) : 0;

    public static bool operator ==(Option<T> left, Option<T> right) => left.Equals(right);
    public static bool operator !=(Option<T> left, Option<T> right) => !left.Equals(right);

    public override string ToString() => 
        _hasValue ? $"Some({_value})" : "None";
}

public static class Option
{
    public static Option<T> Some<T>(T value) => Option<T>.Some(value);
    public static Option<T> None<T>() => Option<T>.None();
}