using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AITrainingDemo.Api.Dtos;
using Microsoft.AspNetCore.Hosting;

namespace AITrainingDemo.Tests;

/// <summary>Jwt:ExpiresHours 配置为 1 小时的测试宿主。</summary>
public class ExpiresHoursAppFactory : AppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Jwt:ExpiresHours", "1");
    }
}

/// <summary>Jwt:ExpiresHours 配置为非法值的测试宿主。</summary>
public class InvalidExpiresHoursAppFactory : AppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Jwt:ExpiresHours", "not-a-number");
    }
}

public class AuthExpiresHoursTests :
    IClassFixture<ExpiresHoursAppFactory>,
    IClassFixture<InvalidExpiresHoursAppFactory>
{
    private readonly ExpiresHoursAppFactory _configuredFactory;
    private readonly InvalidExpiresHoursAppFactory _invalidFactory;

    public AuthExpiresHoursTests(
        ExpiresHoursAppFactory configuredFactory,
        InvalidExpiresHoursAppFactory invalidFactory)
    {
        _configuredFactory = configuredFactory;
        _invalidFactory = invalidFactory;
    }

    [Fact]
    public async Task Login_ExpiresAt_FollowsConfiguredHours()
    {
        var client = _configuredFactory.CreateClient();

        var (token, expiresAt) = await LoginAsync(client);

        Assert.InRange((expiresAt - DateTime.UtcNow).TotalHours, 0.9, 1.1);

        // token 的 exp 必须与响应中的 expiresAt 一致，否则前端展示的过期时间会骗人
        var exp = ReadExpClaim(token);
        Assert.True(Math.Abs((exp - expiresAt).TotalSeconds) < 5);
    }

    [Fact]
    public async Task Login_WithInvalidExpiresHours_FallsBackToEightHours()
    {
        var client = _invalidFactory.CreateClient();

        var (_, expiresAt) = await LoginAsync(client);

        Assert.InRange((expiresAt - DateTime.UtcNow).TotalHours, 7.9, 8.1);
    }

    private static async Task<(string Token, DateTime ExpiresAt)> LoginAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "admin123"));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = body.GetProperty("data");

        var expiresAt = DateTime.Parse(
            data.GetProperty("expiresAt").GetString()!,
            null,
            System.Globalization.DateTimeStyles.RoundtripKind);

        return (data.GetProperty("token").GetString()!, expiresAt);
    }

    /// <summary>从 JWT 载荷里取 exp（秒级时间戳），不引入额外的 JWT 解析依赖。</summary>
    private static DateTime ReadExpClaim(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload += (payload.Length % 4) switch
        {
            2 => "==",
            3 => "=",
            _ => string.Empty
        };

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        var exp = document.RootElement.GetProperty("exp").GetInt64();
        return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
    }
}
