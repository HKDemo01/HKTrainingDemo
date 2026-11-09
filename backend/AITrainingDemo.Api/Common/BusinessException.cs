namespace AITrainingDemo.Api.Common;

public class BusinessException(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}
