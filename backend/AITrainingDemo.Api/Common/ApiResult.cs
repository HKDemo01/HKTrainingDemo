namespace AITrainingDemo.Api.Common;

public record ApiResult<T>
{
    public int Code { get; init; }
    public string Message { get; init; } = "ok";
    public T? Data { get; init; }

    public static ApiResult<T> Ok(T data) => new() { Code = 0, Message = "ok", Data = data };
    public static ApiResult<T> Fail(int code, string message) => new() { Code = code, Message = message, Data = default };
}
