using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AITrainingDemo.Api.Common;

public class ApiExceptionFilter(ILogger<ApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var ex = context.Exception;
        var (code, message) = ex is BusinessException be
            ? (be.Code, be.Message)
            : (500, "服务器内部错误");

        if (ex is not BusinessException)
        {
            logger.LogError(ex, "未处理异常: {Path}", context.HttpContext.Request.Path);
        }

        context.Result = new ObjectResult(ApiResult<object>.Fail(code, message)) { StatusCode = code };
        context.ExceptionHandled = true;
    }
}
