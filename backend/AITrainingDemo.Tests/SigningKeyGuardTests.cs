using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace AITrainingDemo.Tests;

/// <summary>
/// 仓库内的演示签名密钥是公开的，任何拿到仓库的人都能用它自签 token。
/// 因此它只能在 Development 环境使用，其他环境一旦检测到就必须拒绝启动。
/// </summary>
public class DemoSigningKeyAppFactory : AppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Jwt:SigningKey", "dev-only-signing-key-change-me-in-production-0123456789");
    }
}

/// <summary>开发环境使用演示密钥：允许启动，仅告警。</summary>
public class DemoSigningKeyDevelopmentAppFactory : AppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseEnvironment("Development");
        builder.UseSetting("Jwt:SigningKey", "dev-only-signing-key-change-me-in-production-0123456789");
    }
}

public class SigningKeyGuardTests
{
    /// <summary>非开发环境使用演示密钥 → 拒绝启动。</summary>
    [Fact]
    public void DemoSigningKey_OutsideDevelopment_RefusesToStart()
    {
        using var factory = new DemoSigningKeyAppFactory();

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains(
            AllMessages(exception),
            message => message.Contains("演示 JWT 签名密钥", StringComparison.Ordinal));
    }

    /// <summary>开发环境使用演示密钥 → 可以启动。</summary>
    [Fact]
    public async Task DemoSigningKey_InDevelopment_StartsNormally()
    {
        using var factory = new DemoSigningKeyDevelopmentAppFactory();

        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>收集异常链上的全部消息（宿主会把启动期的异常包一层）。</summary>
    private static IEnumerable<string> AllMessages(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            yield return current.Message;
        }
    }
}
