using System.Net;
using System.Text.Json;

namespace AITrainingDemo.Tests;

/// <summary>
/// 统一响应形状的回归护栏：项目规范要求所有失败响应都是 { code, message, data }，
/// 且 code 等于 HTTP 状态码。异常过滤器只覆盖 MVC 管线内的异常，
/// 路由层的 404 / 405 由框架直接短路，需要单独兜底。
/// </summary>
public class UnifiedResponseTests(AppFactory factory) : IClassFixture<AppFactory>
{
    /// <summary>访问不存在的接口，应返回 404 且响应体保持 ApiResult 形状。</summary>
    [Fact]
    public async Task UnknownRoute_Returns404WithUnifiedBody()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/this-route-does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await AssertUnifiedBodyAsync(response, 404);
    }

    /// <summary>用错误的方法访问已存在的接口，应返回 405 且响应体保持 ApiResult 形状。</summary>
    [Fact]
    public async Task WrongMethod_Returns405WithUnifiedBody()
    {
        var client = factory.CreateClient();

        var response = await client.DeleteAsync("/api/auth/login");

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        await AssertUnifiedBodyAsync(response, 405);
    }

    /// <summary>
    /// 断言响应体是 { code, message, data } 形状，且 code 等于 HTTP 状态码。
    /// </summary>
    private static async Task AssertUnifiedBodyAsync(HttpResponseMessage response, int expectedCode)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        Assert.Equal(expectedCode, root.GetProperty("code").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("message").GetString()));
        Assert.True(root.TryGetProperty("data", out _));
    }
}
