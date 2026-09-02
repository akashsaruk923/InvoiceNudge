namespace InvoiceNudge.Application.Common;

public readonly record struct Result(bool Succeeded, string? Error)
{
    public static Result Ok() => new(true, null);
    public static Result Fail(string error) => new(false, error);
}

public readonly record struct Result<T>(bool Succeeded, T? Value, string? Error)
{
    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}
