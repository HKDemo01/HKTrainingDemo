using AITrainingDemo.Api.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AITrainingDemo.Tests;

/// <summary>
/// 全局异常过滤器的回归护栏。
/// </summary>
public class ExceptionFilterTests(AppFactory factory) : IClassFixture<AppFactory>
{
    /// <summary>
    /// 异常过滤器只允许一条注册路径（AddScoped + AddService）。
    /// 若有人再加回 options.Filters.Add&lt;ApiExceptionFilter&gt;()，异常会被处理两次，此用例必须失败。
    /// </summary>
    [Fact]
    public void ApiExceptionFilter_IsRegistered_ExactlyOnce()
    {
        var options = factory.Services.GetRequiredService<IOptions<MvcOptions>>().Value;

        var registered = options.Filters.Count(filter =>
            (filter is TypeFilterAttribute typeFilter && typeFilter.ImplementationType == typeof(ApiExceptionFilter)) ||
            (filter is ServiceFilterAttribute serviceFilter && serviceFilter.ServiceType == typeof(ApiExceptionFilter)));

        Assert.Equal(1, registered);
    }
}
